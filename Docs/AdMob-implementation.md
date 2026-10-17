# AdMob and hero portraits — 18 October 2026

The requested build uses Google's official Android test IDs. Integrate Google Mobile Ads Unity 11.6.0 through the official documented OpenUPM workflow. Keep the current Unity 6 project and build in place so the open editor and Android cache remain available. Preserve the pre-existing Interior_Backdrop material change and exclude it from the commit.

Additional user constraint: banners must not cover Android's native navigation buttons. This implementation creates no banner views; only interstitial and rewarded formats are enabled.

- [x] Add regression tests for interstitial grace period, three travel opportunities, cooldown after either full-screen format, session cap, one-time rewarded callbacks, and the persistent daily reward limit.
- [x] Add a persistent Android ad manager: UMP consent before requests, main-thread callbacks, one preloaded ad per format, bounded retry backoff, expiry refresh, full-screen exclusion, pause/audio restoration, and safe travel fallback.
- [x] Offer interstitials only during explicit location travel: 120-second initial grace, at least 180 seconds since the previous full-screen ad, every three travels, maximum four per session. Never show on launch, resume, character selection, or during fishing.
- [x] Offer optional rewarded videos at the market for 50 coins, at most three per game day. Grant and save only from the SDK reward callback, once per video; ordinary market actions remain available offline.
- [x] Expose privacy choices in Settings when required. Keep test IDs centralized and document what must be configured before monetizing.
- [x] Replace only the two hero selection portraits with featureless skin-colored faces, preserving their Unity GUIDs.
- [x] Run the complete project tests, inspect the updated portraits and market layout/interaction, build the Android APK, and inspect its manifest/signature.
Delivery target: push the verified task commit to origin/main.

Review focus: duplicate/late callbacks must not duplicate rewards or travel; unavailable ads must never block travel; cached ads must expire; full-screen close/failure must restore the previous pause/audio state; saved daily counts must survive reloads. Android hardware testing is limited by whether an adb device is connected.

Verification ledger: the initial policy tests failed before implementation (11/11 missing behavior), then passed. Review reproduced the audio pause defect (11 passed / 1 failed) and the market-close pause defect (14 passed / 1 failed). Both are fixed; the final real Unity EditMode suite passed 15/15. Fresh review found no remaining important issue for the configured Google Android test-ad runtime. Live consent retry was also decoupled from SDK initialization. No mediation adapters are configured. The editor recovery snapshot is retained privately under .utmp/EditorRecovery.

APK verification: Android build succeeded with 0 errors. APK: Builds/Android/Riverlight.apk, 122339586 bytes, ARM64, target SDK 36. ZIP CRC and APK v2 signature verified. Manifest contains the Google test App ID and both optimized initialization/loading flags; both test unit IDs were verified inside the packaged native data. SHA-256: 3b536d92aed23fffeb08cb6c9bb5d68db14a1a2edc2e4c70d47defc186f10873. No adb device is attached, so native ad playback remains untested on hardware.

Final text verification: gameplay punctuation and the rewarded title were checked inside the rebuilt APK; corrupt punctuation is absent. All 15 tests passed again. APK v2 signing and 16 KB alignment passed.
