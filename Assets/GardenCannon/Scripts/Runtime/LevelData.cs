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

    /// <summary>Loads converted levels from Resources/GardenCannon/Levels.</summary>
    public static class LevelLibrary
    {
        public const string ResourceFolder = "GardenCannon/Levels/";
        static LevelIndex _index;

        public static LevelIndex Index
        {
            get
            {
                if (_index == null)
                {
                    var ta = Resources.Load<TextAsset>(ResourceFolder + "index");
                    _index = ta != null ? JsonUtility.FromJson<LevelIndex>(ta.text) : new LevelIndex();
                    if (ta == null) Debug.LogError("[GardenCannon] Missing Resources/" + ResourceFolder + "index.json. Run Tools/ConvertLevels.py.");
                }
                return _index;
            }
        }

        public static int Count => Index.levels.Count;

        /// <param name="number">1-based level number.</param>
        public static LevelData Load(int number)
        {
            if (Count == 0) return null;
            number = Mathf.Clamp(number, 1, Count);
            var entry = Index.levels[number - 1];
            var ta = Resources.Load<TextAsset>(ResourceFolder + entry.file);
            if (ta == null)
            {
                Debug.LogError("[GardenCannon] Missing level file " + entry.file);
                return null;
            }
            var level = JsonUtility.FromJson<LevelData>(ta.text);
            if (level.moves <= 0) level.moves = entry.moves > 0 ? entry.moves : 30;
            return level;
        }
    }
}
