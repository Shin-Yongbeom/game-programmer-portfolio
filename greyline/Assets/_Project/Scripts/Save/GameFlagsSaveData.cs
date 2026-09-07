using System;
using System.Collections.Generic;
using Greyline.Core;

namespace Greyline.Save
{
    /// <summary>JsonUtility-friendly save DTO: schema version plus bool GameFlags only.</summary>
    [Serializable]
    public sealed class GameFlagsSaveData
    {
        public int schemaVersion;
        public List<GameFlagEntry> flags = new();

        public static GameFlagsSaveData Capture(GameFlags source)
        {
            var data = new GameFlagsSaveData();
            foreach (var pair in source.All)
            {
                data.flags.Add(new GameFlagEntry { key = pair.Key, value = pair.Value });
            }

            data.flags.Sort((left, right) => string.CompareOrdinal(left.key, right.key));
            return data;
        }

        public void Restore(GameFlags target)
        {
            target.Clear();
            foreach (GameFlagEntry flag in flags)
            {
                if (!string.IsNullOrWhiteSpace(flag.key))
                {
                    target.Set(flag.key, flag.value);
                }
            }
        }
    }

    [Serializable]
    public sealed class GameFlagEntry
    {
        public string key;
        public bool value;
    }
}
