using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// One level, converted from the original FlatBuffer level bundle by Tools/ConvertLevels.py.
    /// Grid coordinates: x = column (0 = left), y = row (0 = bottom).
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int id;
        public string name;
        public int moves = 30;
        public int difficulty = 1;
        public int width = 9;
        public int height = 12;
        public List<GoalData> goals = new List<GoalData>();
        public List<ItemData> items = new List<ItemData>();
    }

    [Serializable]
    public class GoalData
    {
        public int type;
        public int count;
    }

    [Serializable]
    public class ItemData
    {
        public int type;
        public int x;
        public int y;
        public int layers = 1;
        public int variant;
        public int hp;
        public int maxHp;
        public List<GridOffset> footprint = new List<GridOffset>();
        public bool hasLane;
        public float lanePos;
        public int laneMin;
        public int laneMax;
        public bool floor;

        /// <summary>All occupied cells (anchor first), in absolute grid coordinates.</summary>
        public List<Vector2Int> Cells()
        {
            var list = new List<Vector2Int> { new Vector2Int(x, y) };
            if (footprint != null)
                foreach (var o in footprint)
                    list.Add(new Vector2Int(x + o.x, y + o.y));
            return list;
        }
    }

    [Serializable]
    public struct GridOffset
    {
        public int x;
        public int y;
    }

    [Serializable]
    public class LevelIndex
    {
        public List<LevelIndexEntry> levels = new List<LevelIndexEntry>();
    }

    [Serializable]
    public class LevelIndexEntry
    {
        public int id;
        public string file;
        public int moves;
        public int difficulty;
    }

    /// <summary>
    /// Level JSONs (Addressables group LocalLevels, label "levels"). Filled once by <see cref="GameBootstrap"/>
    /// with every TextAsset loaded from the label; files are matched by name (index, level_001, ...).
    /// </summary>
    public static class LevelLibrary
    {
        public const string Label = "levels";
        public const string IndexName = "index";

        static readonly Dictionary<string, TextAsset> _files = new Dictionary<string, TextAsset>();
        static LevelIndex _index = new LevelIndex();

        public static bool Ready { get; private set; }
        public static LevelIndex Index => _index;
        public static int Count => _index.levels.Count;

        public static void Initialize(IEnumerable<TextAsset> assets)
        {
            _files.Clear();
            foreach (var ta in assets)
                if (ta != null) _files[ta.name] = ta;
            _index = _files.TryGetValue(IndexName, out var idx) ? JsonUtility.FromJson<LevelIndex>(idx.text) : new LevelIndex();
            if (_index.levels.Count == 0)
                Debug.LogError("[GardenCannon] No level index in Addressables label '" + Label + "'. Run Garden Cannon > Setup.");
            Ready = true;
        }

        /// <param name="number">1-based level number.</param>
        public static LevelData Load(int number)
        {
            if (Count == 0) return null;
            number = Mathf.Clamp(number, 1, Count);
            var entry = _index.levels[number - 1];
            if (!_files.TryGetValue(entry.file, out var ta))
            {
                Debug.LogError("[GardenCannon] Missing level file " + entry.file + " in Addressables label '" + Label + "'.");
                return null;
            }
            var level = JsonUtility.FromJson<LevelData>(ta.text);
            if (level.moves <= 0) level.moves = entry.moves > 0 ? entry.moves : 30;
            return level;
        }
    }
}
