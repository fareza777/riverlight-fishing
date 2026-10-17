using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CozyFishing.Tests
{
    // Reflection lets these integration tests exercise the game's predefined Assembly-CSharp
    // without moving the existing game into new assembly definitions.
    public sealed class AdPolicyTests
    {
        static Type GameType(string name)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CozyFishing.Monetization." + name)).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, $"The game must implement {name}.");
            return type;
        }

        static object New(string name) => Activator.CreateInstance(GameType(name));
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
        static void Travel(object policy, int count = 3) { for (int i = 0; i < count; i++) Call(policy, "RecordTravel"); }
        static bool Eligible(object policy, double seconds) => (bool)Call(policy, "CanShow", seconds);

        [Test]
        public void InterstitialDoesNotInterruptTheFirstTwoMinutes()
        {
            var policy = New("InterstitialPolicy");
            Travel(policy);
            Assert.That(Eligible(policy, 119.9), Is.False);
            Assert.That(Eligible(policy, 120.0), Is.True);
        }

        [Test]
        public void InterstitialRequiresThreeExplicitTravels()
        {
            var policy = New("InterstitialPolicy");
            Travel(policy, 2);
            Assert.That(Eligible(policy, 600.0), Is.False);
            Travel(policy, 1);
            Assert.That(Eligible(policy, 600.0), Is.True);
        }

        [Test]
        public void InterstitialResetsTravelCountAfterAnImpression()
        {
            var policy = New("InterstitialPolicy");
            Travel(policy);
            Call(policy, "RecordClosed", 120.0, true);
            Assert.That(Eligible(policy, 600.0), Is.False);
        }

        [Test]
        public void InterstitialWaitsThreeMinutesAfterClosing()
        {
            var policy = New("InterstitialPolicy");
            Travel(policy);
            Call(policy, "RecordClosed", 200.0, true);
            Travel(policy);
            Assert.That(Eligible(policy, 379.9), Is.False);
            Assert.That(Eligible(policy, 380.0), Is.True);
        }

        [Test]
        public void RewardedVideoAlsoDelaysTheNextInterstitial()
        {
            var policy = New("InterstitialPolicy");
            Travel(policy);
            Call(policy, "RecordClosed", 200.0, false);
            Assert.That(Eligible(policy, 379.9), Is.False);
            Assert.That(Eligible(policy, 380.0), Is.True);
        }

        [Test]
        public void InterstitialStopsAfterFourImpressionsInASession()
        {
            var policy = New("InterstitialPolicy");
            for (int i = 0; i < 4; i++)
            {
                Travel(policy);
                Assert.That(Eligible(policy, 300.0 + i * 300.0), Is.True);
                Call(policy, "RecordClosed", 300.0 + i * 300.0, true);
            }
            Travel(policy);
            Assert.That(Eligible(policy, 2000.0), Is.False);
        }

        [Test]
        public void DailyRewardsStopAtThreeAndDoNotIncreaseOnRejectedClaims()
        {
            var daily = New("DailyAdRewards");
            Assert.That(Call(daily, "TryClaim", 1), Is.True);
            Assert.That(Call(daily, "TryClaim", 1), Is.True);
            Assert.That(Call(daily, "TryClaim", 1), Is.True);
            Assert.That(Call(daily, "TryClaim", 1), Is.False);
            Assert.That(Call(daily, "CanClaim", 1), Is.False);
            Assert.That(daily.GetType().GetField("claimed").GetValue(daily), Is.EqualTo(3));
        }

        [Test]
        public void ANewGameDayRestoresTheDailyRewardAllowance()
        {
            var daily = New("DailyAdRewards");
            for (int i = 0; i < 3; i++) Call(daily, "TryClaim", 4);
            Assert.That(Call(daily, "CanClaim", 5), Is.True);
            Assert.That(Call(daily, "TryClaim", 5), Is.True);
            Assert.That(daily.GetType().GetField("claimed").GetValue(daily), Is.EqualTo(1));
        }

        [Test]
        public void ReloadingASaveDoesNotResetTheDailyRewardLimit()
        {
            var daily = New("DailyAdRewards");
            for (int i = 0; i < 3; i++) Call(daily, "TryClaim", 2);
            var restored = JsonUtility.FromJson(JsonUtility.ToJson(daily), daily.GetType());
            Assert.That(Call(restored, "TryClaim", 2), Is.False);
        }

        [Test]
        public void DuplicateSdkRewardCallbacksGrantTheRewardOnlyOnce()
        {
            var claim = New("RewardedClaim");
            int coins = 0;
            Action grant = () => coins += 50;
            Call(claim, "Earn", grant);
            Call(claim, "Earn", grant);
            Assert.That(coins, Is.EqualTo(50));
        }

        [Test]
        public void AnUnfinishedVideoGrantsNothing()
        {
            var claim = New("RewardedClaim");
            Assert.That(claim.GetType().GetProperty("Earned").GetValue(claim), Is.False);
        }

        [TestCase(1f, false)]
        [TestCase(0f, false)]
        [TestCase(0.5f, true)]
        public void AdsSilenceSourcesThatNormallyKeepPlayingInPauseMenus(float scale, bool audioPaused)
        {
            bool originalPause = AudioListener.pause;
            float originalScale = Time.timeScale;
            var audioObject = new GameObject("Ad audio regression");
            var managerObject = new GameObject("Ad manager regression");
            var source = audioObject.AddComponent<AudioSource>();
            source.ignoreListenerPause = true; // MusicPlayer, SfxPlayer, and VoicePlayer all use this.
            var manager = managerObject.AddComponent(GameType("AdManager"));
            var begin = manager.GetType().GetMethod("BeginPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
            var end = manager.GetType().GetMethod("EndPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Time.timeScale = scale;
                AudioListener.pause = audioPaused;
                begin.Invoke(manager, null);
                Assert.That(source.ignoreListenerPause, Is.False, "Game audio must obey the ad pause.");
                Assert.That(AudioListener.pause, Is.True);
                end.Invoke(manager, new object[] { false, false });
                Assert.That(source.ignoreListenerPause, Is.True, "Restore the normal menu-audio behavior.");
                Assert.That(Time.timeScale, Is.EqualTo(scale));
                Assert.That(AudioListener.pause, Is.EqualTo(audioPaused));
            }
            finally
            {
                end.Invoke(manager, new object[] { false, false });
                AudioListener.pause = originalPause;
                Time.timeScale = originalScale;
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(audioObject);
            }
        }

        [Test]
        public void MarketCannotCloseUnderARewardedAdAndLeaveTheWorldPaused()
        {
            var managerType = GameType("AdManager");
            var singleton = managerType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            object previousManager = singleton.GetValue(null);
            float originalScale = Time.timeScale;
            bool originalPause = AudioListener.pause;
            var managerObject = new GameObject("Ad pause regression");
            var shopObject = new GameObject("Market pause regression");
            var manager = managerObject.AddComponent(managerType);
            var shop = shopObject.AddComponent(managerType.Assembly.GetType("CozyFishing.UI.ShopUI"));
            var group = shopObject.AddComponent<CanvasGroup>();
            group.alpha = 1;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            shop.GetType().GetField("group", flags).SetValue(shop, group);
            shop.GetType().GetField("oldScale", flags).SetValue(shop, 1f);
            var close = shop.GetType().GetMethod("Close", flags);
            var end = managerType.GetMethod("EndPresentation", flags);
            try
            {
                singleton.SetValue(null, manager);
                Time.timeScale = 0; // The market was already pausing the world.
                managerType.GetMethod("BeginPresentation", flags).Invoke(manager, null);
                close.Invoke(shop, null); // Escape or controller Cancel while the video is open.
                Assert.That(group.alpha, Is.EqualTo(1f), "Keep the market open behind the ad.");
                Assert.That(Time.timeScale, Is.Zero);
                end.Invoke(manager, new object[] { true, false });
                Assert.That(Time.timeScale, Is.Zero, "The market must still own the pause after the ad.");
                close.Invoke(shop, null);
                Assert.That(group.alpha, Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(1f), "Closing the market afterwards resumes the world.");
            }
            finally
            {
                end.Invoke(manager, new object[] { false, false });
                UnityEngine.Object.DestroyImmediate(shopObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
                singleton.SetValue(null, previousManager);
                Time.timeScale = originalScale;
                AudioListener.pause = originalPause;
            }
        }
    }
}
