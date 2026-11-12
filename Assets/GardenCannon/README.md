# Garden Cannon (Unity)

Unity port of the web prototype (`index.html`), using the original art, prefabs and SFX
already in the project, and the 50 levels from `Assets/addressable/locallevels_assets_all.bundle`.

## Run

1. Menu **Garden Cannon > Setup (catalog + scene)**.
   - Creates `Data/ItemCatalog.asset` (item type -> prefab, behaviour, SFX).
   - Builds `Scenes/GardenCannon.unity` (GrassTop / Water / GrassBottom / Cannon sprites) and puts it first in Build Settings.
2. Press Play. Drag to aim, release to fire. `R` restart, `[` / `]` previous / next level, `<` `>` next to the level label too.

## Levels

`Tools/ConvertLevels.py` (needs `python -m pip install UnityPy`) reads the bundle and writes
`Resources/GardenCannon/Levels/level_XXX.json` + `index.json`. Re-run it if the bundle changes.
The FlatBuffer layout it decodes is documented at the top of the script.

Grid: x = column, y = row, row 0 = bottom. 9x12 boards use 1 world unit per cell; 12x17 boards are scaled to the same lawn.

## Item types (edit in ItemCatalog)

| id | item | notes |
|----|------|-------|
| 1  | Block | 1 hit, colour = variant (BlockColorAtlas tile) |
| 26 | Brick Wall | HP from level (`hp / damagePerHit`, 5 per hit) |
| 23 | Stone Corner | triangle, variant 0..3 = corner BL, BR, TR, TL |
| 25 | Stone | 2x2, unbreakable |
| 4  | Moving Barrier | 3 cells, moves on its lane (variant 1 = vertical) |
| 5  | Target Stand | 2x2 goal |
| 24 | Container | big footprint, HP shown as number |
| 9  | Moving Target | guess: moves on a lane |
| 7, 8, 13, 6, 2 | Log Cutter, Wooden Box, Egg Box, Electric Orb, Rising Bumper | guesses from data shape - check visually |

The original scripts are AssetRipper stubs, so prefabs are used only as visuals; all gameplay is in
`Scripts/Runtime`. uGUI / TextMeshPro assemblies are stubs too, so the HUD uses SpriteRenderer + TextMesh.
