# Garden Cannon (Unity)

Unity port of the web prototype (`index.html`), using the original art, prefabs and SFX
already in the project, and the 50 levels from `Assets/addressable/locallevels_assets_all.bundle`.
Reference screen: **1440 x 3120 portrait**.

## Run

1. Menu **Garden Cannon > Setup (catalog + addressables + scene)**:
   - `Data/ItemCatalog.asset` (item type -> prefab, behaviour, SFX) and `Data/GameAssets.asset` (sprites, ball, font, SFX).
   - Addressables groups (see below).
   - `Scenes/GardenCannon.unity`: only a camera + `GameBootstrap` (addresses, layout). No direct asset references.
   - Game view set to 1440x3120, Player orientation portrait.
2. Press Play. Drag to aim, release to fire. `R` restart, `[` / `]` previous / next level.

Nothing uses `Resources`. `GameBootstrap` loads `GardenCannon/GameAssets` and every TextAsset with label
`levels` through Addressables, then builds the world at runtime. Layout values live on `GameBootstrap.layout`.

## Addressables groups (same split as the original bundles)

| Group | Original bundle | Content |
|-------|-----------------|---------|
| Default | default_assets_all | GameAssets, ItemCatalog, item/ball/FX prefabs, sprites |
| AudioClips | audioclips_assets_all | all SFX |
| LocalLevels | locallevels_assets_all | `Levels/*.json` (label `levels`) |
| LocalFont | localfont_assets_all | Eigerdals Norm Heavy |
| LocalShaders | localshaders_assets_all | custom shaders used by the prefabs |

The other original bundles (team, chests, league, notifications, profile, gift boxes, icons) belong to
meta features that this game does not have. **Garden Cannon > Refresh Addressables Groups** re-adds entries
after you change the catalog or add levels.

## Levels

`Tools/ConvertLevels.py` (needs `python -m pip install UnityPy`) reads the bundle and writes
`Assets/GardenCannon/Levels/level_XXX.json` + `index.json`. Re-run it, then Refresh Addressables Groups.
The FlatBuffer layout it decodes is documented at the top of the script.

## Item types (edit in ItemCatalog, Setup rebuilds it when the defaults version changes)

| id | item | notes |
|----|------|-------|
| 1  | Block | star block, 1 hit, colour = variant |
| 26 | Number Block | MultiBlockItemView, HP number from the level, 1 per ball hit, colour = variant |
| 23 | Stone Corner | triangle, variant 0..3 = corner TL, TR, BR, BL |
| 25 | Twirler | rotating arms (variant 0/1 = 4 arms, 2/3 = 2 arms), unbreakable |
| 4  | Moving Barrier | 3 cells, moves on its lane (variant 1 = vertical) |
| 5  | Egg Box | 2x2 goal, 6 hits (one egg per hit) |
| 24 | Container | big footprint, HP shown as number |
| 2  | Brick Wall | unbreakable, autotiled |
| 9, 7, 8, 13, 6 | Moving Target, Log Cutter, Target Stand, Wooden Box, Electric Orb | only after level 10 - best guesses |

Colour variants follow the original palette (checked against the gameplay video): v0 blue, v1 black, v13 slate,
v15 orange, v22 red, v23 beige, v25 yellow (palette starts at the bottom-left atlas tile).

Level data rows start at the top (original GridView origin); the converter flips them so y counts from the bottom.

## Layout / camera (copied from the original Game scene)

- World units as in the original: GrassTop (0, 4.1), Water (0, -8.48), GrassBottom (0, -12.37), GridView top-left (-5.708, 9.614), cell 1.28, cannon pivot (0, -10.54).
- Main camera: orthographic 12.8 at 1440x3120, tilted 20 degrees with a 1/cos vertical stretch (`CameraRig`), size grows on narrower screens.
- UI camera (layer UI) for level plate / restart / goals / banner / popups, kept inside the safe area by `UiAnchor`.

## Feel (from the reference video)

5 balls per shot with green trails; next shot only after every ball dropped in the water; balls speed up after 6 s;
break chips + sparkles + small screen shake; squash/shake on hit; splash in the water; "Drag to aim" banner on level 1; items pop in row by row at level start; settings button opens Music / Sound / Replay.

The original scripts are AssetRipper stubs, so prefabs are used only as visuals; all gameplay is in
`Scripts/Runtime`. uGUI / TextMeshPro assemblies are stubs too, so the HUD uses SpriteRenderer + TextMesh.
The stub copies of Unity.Addressables / Unity.ResourceManager / Unity.ScriptableBuildPipeline were removed
from `Assets/Scripts` in favour of the real package `com.unity.addressables` 1.21.21.

## Sound & animation

- Gameplay SFX: `Assets/AudioClip` (original). UI / flow SFX and the gameplay music were extracted from
  `audioclips_assets_all` into `Assets/GardenCannon/Audio` (button click, appear, goal hit / complete,
  level win + fireworks, out of balls, level fail, "Music - Core Game Play"). All are in the AudioClips group.
- Break sounds rise in pitch for consecutive breaks by the same ball; music ducks under win / lose jingles.
- Item hit animations use the prefabs' original controllers (trigger `shake` or state `Shake`).
- Cannon uses the original CannonController: `Fire` on each shot, `PlayOn` (refill) at level start. The
  Piv/Base/Rail hierarchy its clips expect is created at runtime, the scene is untouched.
- After changing catalog / assets: **Garden Cannon > Update Catalog + Assets + Addressables (keeps scene)**.
  Only use Setup to rebuild the scene from scratch.
