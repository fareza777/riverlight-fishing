# Riverlight campaign — editable Remotion source

English Play Store artwork and two 30-second video compositions for Riverlight.

```powershell
npm install
npm run dev -- --no-open --port=3047
```

Open the local Studio URL printed in the terminal. The main compositions are `Riverlight-Trailer` (1920 × 1080) and `Riverlight-Vertical` (1080 × 1920), both 30 fps / 900 frames.

Each scene has an individual editable timeline under `Editable-Scenes`. Store artwork is under `Play-Store`.

```powershell
npm run lint
npm run render:assets
npm run render:samples
npm run render:videos
```

The rendering scripts write to `../Deliverables` and use local assets. No remote media is required after installation. The score is derived from the game’s `title.ogg`; Grandpa’s English voice comes from `memory_1_1.mp3`. Fonts and their license files are in `public/fonts`.

See `../CREATIVE-DIRECTION.md` and `../Deliverables/README.md` for the campaign, upload order and provenance.
