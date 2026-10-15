# Riverlight — Game Design (v2)

Cozy 3D fishing adventure for Android (Play Store), **portrait**, one-thumb friendly.
Languages: Bahasa Indonesia + English (switch in Settings).

## Story — "Warisan Kakek" (Grandpa's Legacy)

The player (chosen at start: **Kai** or **Lena**) receives a letter: Grandpa Theo, the old river fisherman,
has passed away and left them his cabin, his rowboat and a worn leather **fishing journal**.

The journal lists **five legendary fish** of the Riverlight valley, one per chapter. Each legend completes a journal page
and plays a narrated **memory cinematic** (sepia flashback in Grandpa's voice). Each memory unlocks the next location.
After the fifth memory the **ending** plays: the player becomes the new keeper of the river.

| Chapter | Goal | Legend | Where / when | Memory |
|---|---|---|---|---|
| 0 | Catch your first fish | — | River | (intro: the bus, the cabin, Grandpa's letter) |
| 1 | Grandpa's first page | Golden Carp | River · Waterfall Pool · 10:30–15:00 | I — The Golden Hook (tying the hook together) |
| 2 | Willow Lake opens | Lantern Koi | Lake · night 20:00–04:00 | II — Lanterns on the Lake (meeting Grandma Rose) |
| 3 | Pinefall Gorge opens | Ironscale Sturgeon (rod tier 2) | Gorge · Deep Hole | III — The Iron River (strength = giving line) |
| 4 | Saltreed Estuary opens | Old King Grouper (rod tier 3) | Estuary · Lighthouse Rocks · 17:00–22:00 | IV — The Lighthouse (the storm, Ben the keeper) |
| 5 | Grandpa's bamboo rod is found (tier 4) | Moon Eel | River · night 21:00–04:30 | V — The Moon Eel → Ending |

## Content (v1.0)

- **4 locations**, each its own scene: Riverlight River (cabin + interior), Willow Lake, Pinefall Gorge (waterfall), Saltreed Estuary (lighthouse).
  Every location has a dock, the rowboat, Mira's market stall, a map board, fishing spots and fish schools.
- **48 fish** (12 per location: 6 common, 3 uncommon, 2 rare, 1 legendary) with Blender models, Recraft illustrations,
  sizes, prices, active hours, habitats and EN/ID descriptions — `ArtSource/fish_catalog.json` is the source of truth.
- **Items**: 5 rods (willow, ash, oak, sea, Grandpa's bamboo — story reward), 3 baits, 4 creels.
- **Story**: intro, 5 memories, ending — 25 illustrated slides, voiced in English and Indonesian (`ArtSource/story.json`).
- **Voice**: objectives and Mira's lines are voiced too (ElevenLabs; Deepgram as English fallback).
- **Audio**: 7 music tracks, 5 ambiences, 20 SFX (fal.ai).
- English is the default language; Indonesian in Settings.

## Progression (v1.1)

- **A day lasts 20 real minutes.** Days also roll over at midnight; sleeping in the evening skips to morning.
- **Learn the water first:** a chapter's legend only bites once the player has found enough species there
  (ch1 river 5 · ch2 lake 5 · ch3 gorge 5 · ch4 estuary 5 · ch5 river 9). Grandpa's voice announces when it is ready.
- **Mira's orders:** two fish orders a day at the market (2.2x the market price, +100 for rares).
- **Collection rewards:** 6 and 12 species per location (+150 / +500 coins), all 48 = Master Angler (+1500).
- **Gear prices:** Ash 200 · Oak 700 · Sea 1600; creels 300 / 900 / 2000.
- **Fishing challenge:** nibbles before the real bite (striking early spooks the fish), strike windows by rarity
  (1.0 / 0.85 / 0.7 / 0.6 s), the safe band narrows and drifts with the fish's strength and size, strong fish make runs
  (warning, then they take line — keep reeling and the tension spikes), 0.25 s grace before the line snaps.
- Estimated play time: main story ~5–6 h, full journal ~8–10 h (not yet measured in a full playthrough).

## Life in the valley

- Animals (Blender, procedural animation, `Tools/blender_critters.py`): duck families, herons, songbird flocks, gulls,
  rabbits, deer, butterflies, fireflies at night, jumping fish, Grandpa's cat asleep on the porch.
- Villagers (Meshy): Old Ben (lighthouse keeper, fishes at the estuary), Hugo (fisherman, lake/gorge),
  Martha and Leo (walk around). Each has three voiced lines (Talk); Mira runs the market.
- Graphics quality: Low / Medium / High (auto-detected; Settings > Graphics). Some animals are hidden on Low.

## Rebuilding everything

1. Blender: `exec(open("Tools/blender_fish.py").read()); build_all(export=True)` and `Tools/blender_locations.py` → `build_location(id)`.
2. Art / audio (dev keys from env vars): `Tools/gen_fish_art.py`, `Tools/gen_story_art.py`, `Tools/gen_narration.py`, `Tools/fal_audio.py batch`.
3. Unity: **CozyFishing → Build Everything (Content + All Scenes)**, then **Build Windows Player** or **Build Android App Bundle**.
4. Self-tests in the player: `-selftest-tour` (whole game, screenshots + a catch per location), `-selftest-rowing`.

## Core loop

Explore on foot → board the boat → row to a spot → cast → reel (tension) → catch → journal / creel →
return to dock to store or sell → upgrade gear → chase the next legend.

## Controls (portrait)

- Left-bottom: floating **virtual joystick** (walk / row).
- Right-bottom: one **context button** (Board · Leave boat · Enter · Cast · Hook · Reel · Talk).
- Top: creel counter, clock, menu button.

## Systems

- **Player on foot**: walk/run, interact (door, boat, mailbox, bed), enter the cabin interior (separate scene).
- **Boat**: board/leave at the dock, rowing synced to the character's rowing animation, current, bob.
- **Fishing**: cast (charge), wait, bite, reel tension game; character animations for cast / hold / pull.
- **Inventory**: creel (8) + storage chest in the cabin; items: fish, bait, rods, hats.
- **Journal**: fish collection (size records), legend pages, memories replay.
- **Character page**: name, look (Kai/Lena), equipped rod & hat, stats.
- **Shop** (mailbox order catalogue): sell fish for coins, buy bait/rods/hats.
- **Time of day**: ~10 min day; sleep in bed to skip to morning.
- **Save**: auto-save to local file.

## Screens

Splash (studio logo → Riverlight logo) → Title (Continue / New Game / Settings) → character select →
intro cinematic → game. Pause menu: Journal · Inventory · Character · Settings · Save & Quit.

## Content pipeline

- 3D characters & props: **Meshy** (concept image → image-to-3D → auto-rig → animation library / text-to-motion).
- Environment / boat / cabin: Blender (shared `Pal_*` palette).
- UI icons, logo, journal illustrations: **Recraft**.
- Narration & SFX: **ElevenLabs**.
- API keys are used only at development time to generate assets; **they are never shipped in the app**.

## Milestones

1. Foundation: portrait + touch controls, rigged characters walking, boat boarding, rowing/fishing animations, cabin interior, fixes.
2. Menus & systems: splash, title, character select, inventory, journal, character page, shop, save.
3. Story: intro + 6 memory cinematics, narration, chapters.
4. Audio & polish: music, SFX, ambience, haptics, mobile performance.
5. Play Store: AAB, icon, store art, keystore. Upload by the owner's Google Play developer account.
