"""
Convert the level bundle (Assets/addressable/locallevels_assets_all.bundle) into
JSON files that the Unity game loads through Addressables (group LocalLevels).

Usage (needs `pip install UnityPy`):
    python Tools/ConvertLevels.py
    python Tools/ConvertLevels.py --bundle path/to/locallevels.bundle --out Assets/GardenCannon/Levels

Each level TextAsset (id_1 .. id_N) is a FlatBuffer without a shipped schema.
The layout below was reverse engineered from the data:

  LevelModel (root table)
    0  width        : ubyte
    1  height       : ubyte
    2  cells        : [Cell]       row-major, index = row * width + col, row 0 = TOP
                                   (the original GridView origin is the top-left corner)
    3  goal         : Goal
    4  version      : int          (1 or 66, unused by the game)

  Cell
    0  items        : [Item]
    1  floorItems   : [Item]       (empty in every shipped level)

  Item
    0  type         : int          item type id (see ItemCatalog in Unity)
    1  layers       : ubyte        default 1 (Container uses 2)
    2  position     : GridPoint    struct { short row; short col; }
    3  footprint    : [GridPoint]  extra cells, offsets from position (row, col)
    4  variant      : int          block colour / wedge rotation / lane axis
    5  hp           : int          hit points in "damage units" (balls deal 5)
    7  links        : [?]          always empty
    8  lanePos      : float        moving items: start position on the lane
    10 laneMin      : int          moving items: first lane cell
    11 laneMax      : int          moving items: last lane cell
    12 maxHp        : int          same as hp

  Goal
    0  items        : [GoalItem { 0 type:int, 1 count:int }]
    1, 2            : int (always 100, unused)

Move counts come from the LevelAssetConfig MonoBehaviour in the same bundle
(LevelPath[i]._moveCount for level i + 1).

Output JSON uses Unity-style coordinates: x = column, y = row counted from the BOTTOM
(y = height - 1 - row). Footprint offsets and vertical lanes (variant 1) are flipped the same way.
Wedge variants keep their original meaning: 0 = top-left corner, 1 = top-right, 2 = bottom-right, 3 = bottom-left.
"""
import argparse
import json
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_BUNDLE = os.path.join(ROOT, "Assets", "addressable", "locallevels_assets_all.bundle")
DEFAULT_OUT = os.path.join(ROOT, "Assets", "GardenCannon", "Levels")


class FlatTable:
    def __init__(self, buf, pos):
        self.b = buf
        self.pos = pos
        vt = pos - struct.unpack_from("<i", buf, pos)[0]
        vt_size = struct.unpack_from("<H", buf, vt)[0]
        self.size = struct.unpack_from("<H", buf, vt + 2)[0]
        self.fields = [struct.unpack_from("<H", buf, vt + 4 + 2 * i)[0] for i in range((vt_size - 4) // 2)]

    def _off(self, i):
        return self.fields[i] if i < len(self.fields) else 0

    def has(self, i):
        return self._off(i) != 0

    def u8(self, i, d=0):
        o = self._off(i)
        return self.b[self.pos + o] if o else d

    def i32(self, i, d=0):
        o = self._off(i)
        return struct.unpack_from("<i", self.b, self.pos + o)[0] if o else d

    def f32(self, i, d=0.0):
        o = self._off(i)
        return struct.unpack_from("<f", self.b, self.pos + o)[0] if o else d

    def struct_at(self, i):
        o = self._off(i)
        return self.pos + o if o else None

    def ref(self, i):
        o = self._off(i)
        if not o:
            return None
        p = self.pos + o
        return p + struct.unpack_from("<I", self.b, p)[0]

    def table(self, i):
        p = self.ref(i)
        return FlatTable(self.b, p) if p is not None else None

    def vec_len(self, i):
        p = self.ref(i)
        return struct.unpack_from("<I", self.b, p)[0] if p is not None else 0

    def vec_tables(self, i):
        p = self.ref(i)
        if p is None:
            return []
        n = struct.unpack_from("<I", self.b, p)[0]
        out = []
        for j in range(n):
            e = p + 4 + 4 * j
            out.append(FlatTable(self.b, e + struct.unpack_from("<I", self.b, e)[0]))
        return out

    def vec_points(self, i):
        """Vector of GridPoint structs (short row, short col)."""
        p = self.ref(i)
        if p is None:
            return []
        n = struct.unpack_from("<I", self.b, p)[0]
        return [struct.unpack_from("<hh", self.b, p + 4 + 4 * j) for j in range(n)]


KNOWN_ITEM_FIELDS = {0, 1, 2, 3, 4, 5, 7, 8, 10, 11, 12}


def parse_item(t, cell_row, cell_col, warnings):
    pos = t.struct_at(2)
    row, col = struct.unpack_from("<hh", t.b, pos) if pos is not None else (cell_row, cell_col)
    if (row, col) != (cell_row, cell_col):
        warnings.append(f"item position {row},{col} differs from its cell {cell_row},{cell_col}")
    for i, off in enumerate(t.fields):
        if off and i not in KNOWN_ITEM_FIELDS:
            warnings.append(f"type {t.i32(0)} has unknown field f{i}")
    if t.vec_len(7):
        warnings.append(f"type {t.i32(0)} has non-empty links vector (ignored)")
    item = {
        "type": t.i32(0),
        "x": col,
        "y": row,
        "layers": t.u8(1, 1),
        "variant": t.i32(4),
        "hp": t.i32(5),
        "maxHp": t.i32(12),
        "footprint": [{"x": c, "y": r} for (r, c) in t.vec_points(3)],
        "hasLane": t.has(8) or t.has(10) or t.has(11),
        "lanePos": round(t.f32(8), 4),
        "laneMin": t.i32(10),
        "laneMax": t.i32(11),
    }
    return item


def flip_rows(item, height):
    """Data rows count from the top; the game uses y counted from the bottom."""
    item["y"] = height - 1 - item["y"]
    for o in item["footprint"]:
        o["y"] = -o["y"]
    if item["hasLane"] and item["variant"] == 1:  # vertical lane: values are rows
        lo, hi = item["laneMin"], item["laneMax"]
        item["laneMin"], item["laneMax"] = height - 1 - hi, height - 1 - lo
        item["lanePos"] = round(height - 1 - item["lanePos"], 4)
    return item


def parse_level(data, warnings):
    root = FlatTable(data, struct.unpack_from("<I", data, 0)[0])
    w, h = root.u8(0), root.u8(1)
    cells = root.vec_tables(2)
    if len(cells) != w * h:
        warnings.append(f"cell count {len(cells)} != {w}x{h}")
    items = []
    for idx, cell in enumerate(cells):
        r, c = divmod(idx, w)
        for it in cell.vec_tables(0):
            items.append(flip_rows(parse_item(it, r, c, warnings), h))
        for it in cell.vec_tables(1):
            fi = flip_rows(parse_item(it, r, c, warnings), h)
            fi["floor"] = True
            items.append(fi)
    goals = []
    goal = root.table(3)
    if goal is not None:
        for g in goal.vec_tables(0):
            goals.append({"type": g.i32(0), "count": g.i32(1)})
    return {"width": w, "height": h, "goals": goals, "items": items, "format": root.i32(4)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bundle", default=DEFAULT_BUNDLE)
    ap.add_argument("--out", default=DEFAULT_OUT)
    args = ap.parse_args()

    try:
        import UnityPy
    except ImportError:
        sys.exit("UnityPy is required: python -m pip install UnityPy")

    env = UnityPy.load(args.bundle)
    texts = {}
    config = None
    for obj in env.objects:
        if obj.type.name == "TextAsset":
            d = obj.read()
            raw = d.m_Script
            raw = raw.encode("utf-8", "surrogateescape") if isinstance(raw, str) else bytes(raw)
            texts[d.m_Name] = raw
        elif obj.type.name == "MonoBehaviour":
            try:
                tree = obj.read_typetree()
            except Exception:
                continue
            if "LevelPath" in tree:
                config = tree

    os.makedirs(args.out, exist_ok=True)
    level_path = (config or {}).get("LevelPath", [])
    ids = sorted((int(k.split("_")[1]) for k in texts if k.startswith("id_")))
    index = []
    for n in ids:
        warnings = []
        level = parse_level(texts[f"id_{n}"], warnings)
        meta = level_path[n - 1] if n - 1 < len(level_path) else {}
        out = {
            "id": n,
            "name": str(meta.get("_levelName", n)),
            "moves": int(meta.get("_moveCount", 30)),
            "difficulty": int(meta.get("_levelDifficulty", 1)),
            **level,
        }
        fname = f"level_{n:03d}.json"
        with open(os.path.join(args.out, fname), "w", encoding="utf-8") as f:
            json.dump(out, f, indent=1)
        index.append({"id": n, "file": fname[:-5], "moves": out["moves"], "difficulty": out["difficulty"]})
        for wmsg in sorted(set(warnings)):
            print(f"  level {n}: {wmsg}")
    with open(os.path.join(args.out, "index.json"), "w", encoding="utf-8") as f:
        json.dump({"levels": index}, f, indent=1)
    print(f"Converted {len(index)} levels -> {args.out}")


if __name__ == "__main__":
    main()
