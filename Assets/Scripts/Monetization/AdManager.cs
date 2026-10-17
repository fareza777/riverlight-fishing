using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CozyFishing.Core;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFishing.Monetization
{
    /// <summary>Owns consent, one cached ad per format, retries, and the full-screen lifecycle.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class AdManager : MonoBehaviour
    {
        static AdManager instance;
        public static AdManager Instance
        {
            get
            {
                if (instance == null) new GameObject("AdManager").AddComponent<AdManager>();
                return instance;
            }
        }

        public static bool Supported => Application.isEditor || Application.platform == RuntimePlatform.Android;
        public static bool IsPresenting => instance != null && instance.presenting;
        public event Action StateChanged;
        readonly ConcurrentQueue<Action> callbacks = new();
        readonly InterstitialPolicy policy = new();
        readonly List<AudioSource> listenerIgnoringSources = new();
        InterstitialAd interstitial;
        RewardedAd rewarded;
        double sessionSeconds, nextPoll, interstitialAt, rewardedAt, nextInterstitial, nextRewarded, nextConsent;
        int interstitialFailures, rewardedFailures, consentRevision;
        bool initializing, initialized, loadingInterstitial, loadingRewarded, gatheringConsent, paused, presenting;
        bool lastRewardedReady, previousAudioPause;
        float previousTimeScale;
        GameObject inputBlocker;
        volatile bool disposed;

        bool MayRequestAds => AdMobConfig.UseGoogleTestAds || ConsentInformation.CanRequestAds();
        public bool PrivacyOptionsRequired => Supported && !AdMobConfig.UseGoogleTestAds
            && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
        public bool RewardedCoolingDown => sessionSeconds - policy.LastClosedTime < 60;
        public bool RewardedReady => initialized && !presenting && !gatheringConsent && MayRequestAds
            && !RewardedCoolingDown && rewarded != null && Fresh(rewardedAt) && rewarded.CanShowAd();
        static double Now => Time.realtimeSinceStartupAsDouble;
        static bool Fresh(double loadedAt) => Now - loadedAt < AdMobConfig.CacheLifetimeSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { if (Supported) _ = Instance; }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (!Supported) return;
            // Google's sample App ID has no publisher-owned UMP messages. Test units never serve live ads.
            if (AdMobConfig.UseGoogleTestAds) InitializeAds();
            else GatherConsent();
        }

        void Post(Action action) { if (!disposed) callbacks.Enqueue(action); }

        void Update()
        {
            while (callbacks.TryDequeue(out var action))
                try { action(); } catch (Exception e) { Debug.LogException(e); }
            if (!paused && !presenting) sessionSeconds += Time.unscaledDeltaTime;
            if (!Supported || paused || presenting || Now < nextPoll) return;
            nextPoll = Now + 1;
            if (AdMobConfig.UseGoogleTestAds && !initialized && !initializing && Now >= nextConsent) InitializeAds();
            if (!AdMobConfig.UseGoogleTestAds && (!initialized || !MayRequestAds)
                && !gatheringConsent && Now >= nextConsent) GatherConsent();
            if (initialized && MayRequestAds && !gatheringConsent)
            {
                if (interstitial != null && !Fresh(interstitialAt)) { interstitial.Destroy(); interstitial = null; }
                if (rewarded != null && !Fresh(rewardedAt)) { rewarded.Destroy(); rewarded = null; }
                if (interstitial == null && !loadingInterstitial && Now >= nextInterstitial) LoadInterstitial();
                if (rewarded == null && !loadingRewarded && Now >= nextRewarded) LoadRewarded();
            }
            bool ready = RewardedReady;
            if (ready != lastRewardedReady) { lastRewardedReady = ready; StateChanged?.Invoke(); }
        }

        void GatherConsent()
        {
            gatheringConsent = true;
            nextConsent = Now + 60;
            ConsentInformation.Update(new ConsentRequestParameters(), error => Post(() =>
            {
                if (error != null)
                {
                    Debug.LogWarning("[AdMob] Consent update: " + error.Message);
                    gatheringConsent = false;
                    if (MayRequestAds) InitializeAds();
                    StateChanged?.Invoke();
                    return;
                }
                bool pauseForForm = ConsentInformation.ConsentStatus == ConsentStatus.Required;
                if (pauseForForm) BeginPresentation();
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => Post(() =>
                {
                    gatheringConsent = false;
                    if (pauseForForm) EndPresentation(false, false);
                    if (formError != null) Debug.LogWarning("[AdMob] Consent form: " + formError.Message);
                    if (MayRequestAds) InitializeAds();
                    StateChanged?.Invoke();
                }));
            }));
            // UMP may already permit requests using a consent decision from the previous session.
            if (MayRequestAds) InitializeAds();
        }

        void InitializeAds()
        {
            if (initializing || initialized || !MayRequestAds) return;
            initializing = true;
            nextConsent = Now + 60;
            MobileAds.Initialize(status => Post(() =>
            {
                initializing = false;
                initialized = status != null;
                nextPoll = 0;
                StateChanged?.Invoke();
                Debug.Log("[AdMob] Initialized; Google test ads=" + AdMobConfig.UseGoogleTestAds);
            }));
        }

        static double RetryDelay(int failures) => Math.Min(60, 5 * Math.Pow(2, Math.Min(failures - 1, 4)));

        void LoadInterstitial()
        {
            loadingInterstitial = true;
            int revision = consentRevision;
            InterstitialAd.Load(AdMobConfig.AndroidInterstitial, new AdRequest(), (ad, error) => Post(() =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null || !MayRequestAds || revision != consentRevision)
                {
                    ad?.Destroy();
                    nextInterstitial = Now + RetryDelay(++interstitialFailures);
                    if (error != null) Debug.LogWarning("[AdMob] Interstitial load: " + error.GetMessage());
                    return;
                }
                interstitialFailures = 0;
                interstitial = ad;
                interstitialAt = Now;
            }));
        }

        void LoadRewarded()
        {
            loadingRewarded = true;
            int revision = consentRevision;
            RewardedAd.Load(AdMobConfig.AndroidRewarded, new AdRequest(), (ad, error) => Post(() =>
            {
                loadingRewarded = false;
                if (error != null || ad == null || !MayRequestAds || revision != consentRevision)
                {
                    ad?.Destroy();
                    nextRewarded = Now + RetryDelay(++rewardedFailures);
                    if (error != null) Debug.LogWarning("[AdMob] Rewarded load: " + error.GetMessage());
                }
                else
                {
                    rewardedFailures = 0;
                    rewarded = ad;
                    rewardedAt = Now;
                }
                StateChanged?.Invoke();
            }));
        }

        /// <summary>Never waits for an ad to load. Unavailable/ineligible ads continue travel immediately.</summary>
        public void AtTravelBreak(Action continueTravel)
        {
            policy.RecordTravel();
            if (!initialized || presenting || gatheringConsent || !MayRequestAds || !policy.CanShow(sessionSeconds)
                || interstitial == null || !Fresh(interstitialAt) || !interstitial.CanShowAd())
            {
                continueTravel?.Invoke();
                return;
            }
            var ad = interstitial;
            interstitial = null;
            bool completed = false, opened = false;
            BeginPresentation();
            void Finish()
            {
                if (completed) return;
                completed = true;
                ad.Destroy();
                EndPresentation(opened, true);
                continueTravel?.Invoke();
            }
            ad.OnAdFullScreenContentOpened += () => Post(() => opened = true);
            ad.OnAdFullScreenContentClosed += () => Post(Finish);
            ad.OnAdFullScreenContentFailed += error => Post(() =>
            {
                Debug.LogWarning("[AdMob] Interstitial show: " + error.GetMessage());
                Finish();
            });
            try { ad.Show(); }
            catch (Exception e) { Debug.LogWarning("[AdMob] Interstitial show: " + e.Message); Finish(); }
        }

        public bool TryShowRewarded(Action grantReward, Action onClosed)
        {
            if (!RewardedReady || grantReward == null) return false;
            var ad = rewarded;
            rewarded = null;
            var claim = new RewardedClaim();
            bool completed = false, opened = false;
            BeginPresentation();
            void Finish()
            {
                if (completed) return;
                completed = true;
                ad.Destroy();
                EndPresentation(opened, false);
                onClosed?.Invoke();
            }
            ad.OnAdFullScreenContentOpened += () => Post(() => opened = true);
            ad.OnAdFullScreenContentClosed += () => Post(Finish);
            ad.OnAdFullScreenContentFailed += error => Post(() =>
            {
                Debug.LogWarning("[AdMob] Rewarded show: " + error.GetMessage());
                Finish();
            });
            try
            {
                ad.Show(reward => Post(() =>
                {
                    if (completed) return;
                    opened = true;
                    claim.Earn(grantReward);
                }));
            }
            catch (Exception e) { Debug.LogWarning("[AdMob] Rewarded show: " + e.Message); Finish(); }
            return true;
        }

        void BeginPresentation()
        {
            presenting = true;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            Time.timeScale = 0;
            AudioListener.pause = true;
            // Menu audio intentionally ignores the listener pause; ads must silence it too.
            listenerIgnoringSources.Clear();
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!source.ignoreListenerPause) continue;
                listenerIgnoringSources.Add(source);
                source.ignoreListenerPause = false;
            }
#if !UNITY_EDITOR
            if (inputBlocker == null)
            {
                inputBlocker = new GameObject("Ad Input Blocker", typeof(Canvas), typeof(GraphicRaycaster));
                inputBlocker.transform.SetParent(transform, false);
                var canvas = inputBlocker.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 3000;
                var image = new GameObject("Block", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(inputBlocker.transform, false);
                image.color = Color.clear;
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            }
            inputBlocker.SetActive(true);
#endif
            StateChanged?.Invoke();
        }

        void EndPresentation(bool opened, bool isInterstitial)
        {
            if (!presenting) return;
            if (opened) policy.RecordClosed(sessionSeconds, isInterstitial);
            RestorePresentationState();
            nextPoll = 0;
            StateChanged?.Invoke();
        }

        void RestorePresentationState()
        {
            presenting = false;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            foreach (var source in listenerIgnoringSources)
                if (source != null) source.ignoreListenerPause = true;
            listenerIgnoringSources.Clear();
            if (inputBlocker != null) inputBlocker.SetActive(false);
        }

        public void ShowPrivacyOptions()
        {
            if (presenting || !PrivacyOptionsRequired) return;
            gatheringConsent = true;
            consentRevision++;
            BeginPresentation();
            ConsentForm.ShowPrivacyOptionsForm(error => Post(() =>
            {
                gatheringConsent = false;
                // Discard ads loaded under the previous privacy decision, including in-flight requests.
                interstitial?.Destroy(); interstitial = null;
                rewarded?.Destroy(); rewarded = null;
                if (error != null) Debug.LogWarning("[AdMob] Privacy options: " + error.Message);
                EndPresentation(false, false);
                if (MayRequestAds) InitializeAds();
            }));
        }

        void OnApplicationPause(bool value) { paused = value; if (!value) nextPoll = 0; }

        void OnDestroy()
        {
            if (instance != this) return;
            disposed = true;
            interstitial?.Destroy();
            rewarded?.Destroy();
            if (presenting) RestorePresentationState();
            instance = null;
        }
    }
}
