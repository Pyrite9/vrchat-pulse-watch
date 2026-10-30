// Pyrite9 Pulse Watch — last build per avatar (inspector section 'Last build'), after Drink Can's DcBuildRecord
//   The build plugin writes Library/Pyrite9PulseWatch/last_builds.json keyed by avatar name.
//   Library is per project and outside git, so nothing is left in the user's assets. A failed read counts as 'no record'.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Pyrite9.PulseWatch
{
    public static class PwBuildRecord
    {
        public const string Dir = "Library/Pyrite9PulseWatch";
        public const string FilePath = Dir + "/last_builds.json";
        const int MaxEntries = 32;

        [System.Serializable]
        public class Entry
        {
            public string avatar;
            public string time;          // yyyy-MM-dd HH:mm:ss
            public string version;
            public bool built;           // the watch was built (humanoid with arm and head bones)
            public int side;             // PulseWatchSide (the default wrist)
            public float scale;          // sizes x this (that wrist's forearm vs. the reference)
            public int utcOffsetMinutes;
            public bool autoTimeZone;
            public bool daylightSaving;  // a daylight saving rule was baked (D72 — the watch switches by date)
            public int syncedBits;
            public int fxLayers;
            public int contacts;         // receivers + senders
            public int featuresOn;       // of the 13 feature switches (the name tag and the scan's depth light count, the debug meter does not)
            public bool debugMeter;
            public List<string> warnings = new List<string>();   // English, as in the build log
        }

        [System.Serializable]
        class Store { public List<Entry> entries = new List<Entry>(); }

        public static string CleanName(string n) => n == null ? "" : n.Replace("(Clone)", "").Trim();

        static Store Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) ?? new Store();
            }
            catch { }
            return new Store();
        }

        public static Entry Get(string avatar)
        {
            var n = CleanName(avatar);
            return Load().entries.FirstOrDefault(e => e.avatar == n);
        }

        public static long Stamp() { try { return File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath).Ticks : 0; } catch { return 0; } }

        public static void Put(Entry e)
        {
            try
            {
                e.avatar = CleanName(e.avatar);
                var st = Load();
                st.entries.RemoveAll(x => x.avatar == e.avatar);
                st.entries.Insert(0, e);
                if (st.entries.Count > MaxEntries) st.entries.RemoveRange(MaxEntries, st.entries.Count - MaxEntries);
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonUtility.ToJson(st, true));
            }
            catch (System.Exception ex) { Debug.LogWarning("[Pyrite9 Pulse Watch] build record not saved: " + ex.Message); }
        }
    }
}
#endif
