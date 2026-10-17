# Riverlight ads

This APK intentionally uses **Google test ads**, as requested. Test impressions do not earn revenue. Integration: Google Mobile Ads Unity **11.6.0**, Android standard SDK **25.5.0**, UMP **4.0.0**; the package manager and Android resolver pin the dependencies.

## Placements

No banner is created or enabled in this build. Android's system navigation area stays free of banners, as requested.

| Format | Placement and limits |
| --- | --- |
| Interstitial | An explicit trip to another unlocked location. First opportunity after 120 seconds in the foreground and three trips; at least 180 seconds after the previous full-screen ad closes; at most four per session. Never on launch, resume, hero selection, or a fishing action. If no ad is ready, travel proceeds immediately. |
| Rewarded | Optional “Watch an ad” offer at Mira’s market: 50 coins after completing the video, at most three per game day. Counts are saved. At least 60 foreground seconds between full-screen ads. Closing early or a failed video gives no reward. Bonus coins do not advance fishing-sale story milestones. |

Each format caches one ad and refreshes it after 50 minutes. Failed requests retry after 5, 10, 20, 40, then 60 seconds; no immediate retry loops. Callbacks run through the game's main-thread queue. Game time, sound, and touch interaction pause during full-screen ads, and the prior pause/audio state is restored on close or show failure. A reward callback is accepted only once per video.

## IDs in this test build

| Android ID | Value |
| --- | --- |
| App | `ca-app-pub-3940256099942544~3347511713` |
| Interstitial | `ca-app-pub-3940256099942544/1033173712` |
| Rewarded | `ca-app-pub-3940256099942544/5224354917` |

The App ID is in `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`. Unit IDs and the explicit test-mode flag are in `Assets/Scripts/Monetization/AdMobConfig.cs`. Test mode skips publisher consent messages because Google's sample App ID has no publisher-owned UMP configuration.

## Before using live ads

Replace the App ID and both unit IDs with the publisher's own Android IDs, and change `UseGoogleTestAds` to `false`. Configure the app's privacy messages in AdMob. Live mode updates UMP consent at launch, gates SDK initialization and ad requests on `CanRequestAds()`, and exposes “Ad privacy choices” in Settings when UMP requires it. Changing consent discards cached ads and rejects requests started under the previous decision. Configure audience settings to match the published app and use registered test devices when validating live IDs.

Account-level mediation, ad-unit frequency caps, eCPM floors, and privacy messages must be configured in the publisher's AdMob account. There is no account connection in this repository; the changes here cover the app integration.

## Verification

`Assets/Tests/Editor/AdPolicyTests.cs` exercises the initial grace period, travel counting, both cooldown sources, session cap, daily allowance, save serialization, and duplicate reward callbacks. Run EditMode tests with Unity Test Runner, then build through `CozyFishing/Build Android APK` or `CozyFishing.EditorTools.BuildScript.BuildAndroidApk` in batch mode. The installable output is `Builds/Android/Riverlight.apk`.

On a connected Android device, check the Google “Test Ad” label, finish/cancel a rewarded video, disconnect networking, travel with no ad loaded, background/resume, and reopen the game to check the saved daily limit. APK manifest/signature inspection and automated tests do not replace native ad playback on a phone.

## Hero selection art

`Assets/Art/UI/Portraits/arka.png` and `laras.png` were edited with the built-in imagegen tool, preserving their Unity GUIDs. The prompt for each original portrait was to erase all eyes, brows, nose, mouth, freckles, and facial-feature shadows into a smooth skin-colored face, while retaining the head outline, ears, hair, hat, clothes, pose, and background. In-game models remain the existing assets.

References: [Google installation guide](https://developers.google.com/admob/unity/quick-start), [test IDs](https://developers.google.com/admob/unity/test-ads), [interstitial lifecycle](https://developers.google.com/admob/unity/interstitial), [rewarded lifecycle](https://developers.google.com/admob/unity/rewarded), [UMP consent](https://developers.google.com/admob/unity/privacy).
