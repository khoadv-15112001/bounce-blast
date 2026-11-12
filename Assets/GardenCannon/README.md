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
| 1  | Block | 1 hit, colour = variant (BlockColorAtlas tile) |
| 26 | Wooden Box | HP from level / 5 = hits (1-4), shows prefab children stage_4..stage_1 |
| 23 | Stone Corner | triangle, variant 0..3 = corner TL, TR, BR, BL |
| 25 | Stone | 2x2, unbreakable |
| 4  | Moving Barrier | 3 cells, moves on its lane (variant 1 = vertical) |
| 5  | Target Stand | 2x2 goal |
| 24 | Container | big footprint, HP shown as number |
| 2  | Brick Wall | unbreakable, autotiled (module mesh picked from same-type neighbours) |
| 9, 7, 8, 13, 6 | Moving Target, Log Cutter, Egg Box, Multi Block, Electric Orb | best guesses - check visually |

Level data rows start at the top (original GridView origin); the converter flips them so y counts from the bottom.

## Layout / camera (copied from the original Game scene)

- World units as in the original: GrassTop (0, 4.1), Water (0, -8.48), GrassBottom (0, -12.37), GridView top-left (-5.708, 9.614), cell 1.28, cannon pivot (0, -10.54).
- Main camera: orthographic 12.8 at 1440x3120, tilted 20 degrees with a 1/cos vertical stretch (`CameraRig`), size grows on narrower screens.
- UI camera (layer UI) for level plate / restart / goals / banner / popups, kept inside the safe area by `UiAnchor`.

## Feel (from the reference video)

One ball per shot with a light green trail; next shot only after the ball drops in the water; balls speed up after 6 s;
break chips + sparkles + small screen shake; squash/shake on hit; splash in the water; "Drag to aim" banner on level 1.

The original scripts are AssetRipper stubs, so prefabs are used only as visuals; all gameplay is in
`Scripts/Runtime`. uGUI / TextMeshPro assemblies are stubs too, so the HUD uses SpriteRenderer + TextMesh.
The stub copies of Unity.Addressables / Unity.ResourceManager / Unity.ScriptableBuildPipeline were removed
from `Assets/Scripts` in favour of the real package `com.unity.addressables` 1.21.21.
