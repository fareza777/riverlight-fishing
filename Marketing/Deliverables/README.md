# Riverlight — English launch assets

**Campaign hook: One old journal. Five living legends.**

All marketing copy is in English. The app icon features a golden carp, with no human character or face. Characters in captured gameplay are small, viewed from above or behind, with hats obscuring their faces. The promotional key art contains no people.

## Upload-ready images

| Asset | File | Specification |
|---|---|---|
| App icon | `PlayStore/app-icon-512.png` | 512 × 512, 32-bit PNG, below 1 MB |
| Feature graphic / banner | `PlayStore/feature-graphic-1024x500.png` | 1024 × 500, RGB PNG |
| Eight phone screenshots | `PlayStore/Screenshots/01-…` through `08-…` | 1080 × 1920, RGB PNG |

Use the screenshot files in numbered order. They introduce the fishing experience, reeling, fish collection, four locations, night fishing, Grandpa’s journal, the decorated cabin and further exploration.

`Previews/screenshot-overview.png` is a review sheet, not a store screenshot.

## Video

- `Video/riverlight-trailer-1920x1080.mp4`: 30-second landscape trailer for YouTube and the Play Store preview.
- `Video/riverlight-promo-1080x1920.mp4`: 30-second portrait version for social promotion.
- `Video/trailer-thumbnail-1920x1080.png`: matching video cover.
- `Video/riverlight-voice-en.srt`: English subtitle file for Grandpa’s spoken passage.

Both videos use H.264 video, AAC audio and 30 fps. They combine an immediate story hook, actual gameplay, original game music, English narration and sound effects, followed by a closing invitation.

Google Play accepts a YouTube URL for its preview video. Upload the landscape MP4 to the owner’s YouTube account as public or unlisted, disable ads on that video, then add its URL in Play Console. No content has been published or uploaded by this campaign task.

## Masters and editable source

- `Masters/app-icon-1024.png`
- `Masters/banner-2048x1000.png`
- The editable Remotion project is in `../Remotion`, with local fonts, footage, images and audio in `public/`.
- Original captured frame sequences are preserved locally in `../Source/CaptureFrames`; the ZIP includes the encoded gameplay clips used by Remotion and omits the large raw sequences.
- Image-generation prompts and provenance are in `../Source/IMAGE-PROMPTS.md`.

From the Remotion folder:

```powershell
npm install
npm run dev -- --no-open --port=3047
npm run render:assets
npm run render:videos
```

In Studio, open `Riverlight-Trailer` or `Riverlight-Vertical`. Each substantial scene also has its own composition under `Editable-Scenes`. The eight screenshots and identity exports are under `Play-Store`.

## Sources

Gameplay and interface captures come from this Unity project. Captures use a curated local gameplay session and the real game UI. The player’s original save was backed up and restored. Production scenes and scripts were not changed.

The golden-carp icon and riverside feature-graphic illustration were created with the built-in image generation tool. These are promotional art; they are not represented as gameplay screenshots. Existing game illustrations, fish art, music, effects and English narration were reused from the project.

Typography: Cormorant Garamond and Manrope, with their SIL Open Font License files included in the Remotion project.

Store dimensions and content guidance were checked against [Google Play’s preview asset requirements](https://support.google.com/googleplay/android-developer/answer/9866151?hl=en) on 18 October 2026.
