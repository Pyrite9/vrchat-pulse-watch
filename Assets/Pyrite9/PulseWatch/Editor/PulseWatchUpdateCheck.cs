// Pyrite9 Pulse Watch — new version check (inspector header), after Drink Can's DcUpdateCheck (_common/update-check.md)
//   Compares the tag (vX.Y.Z) of the public repository's GitHub Releases 'latest' with PulseWatchConst.Version. No auth, sends
//   nothing (one GET). Automatic once a day (when the inspector opens), result kept in EditorPrefs. A failure is a quiet "couldn't check".
//   Turned on with 0.1.0, the public mirror's first Release (update-check.md 2-4). Mirror Pyrite9/vrchat-pulse-watch.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Pyrite9.PulseWatch
{
    public static class PwUpdateCheck
    {
        public static readonly bool Enabled = true;    // on since 0.1.0, the first public Release (docs/pulse-watch.md 4-6)
        public const string Api = "https://api.github.com/repos/Pyrite9/vrchat-pulse-watch/releases/latest";
        public const string ReleasesPage = "https://github.com/Pyrite9/vrchat-pulse-watch/releases";
        const string KeyTag = "Pyrite9PW.LatestTag", KeyUrl = "Pyrite9PW.LatestUrl", KeyAt = "Pyrite9PW.CheckedAt", KeyFail = "Pyrite9PW.CheckFailed";
        static readonly System.TimeSpan Interval = System.TimeSpan.FromHours(24);

        public enum State { Unknown, Checking, Latest, Newer, Failed }

        [System.Serializable] class Release { public string tag_name; public string html_url; }

        static UnityWebRequest running;
        public static event System.Action Changed;

        public static string LatestTag => EditorPrefs.GetString(KeyTag, "");
        public static string LatestUrl { get { var u = EditorPrefs.GetString(KeyUrl, ""); return string.IsNullOrEmpty(u) ? ReleasesPage : u; } }

        public static State Current
        {
            get
            {
                if (running != null) return State.Checking;
                if (EditorPrefs.GetBool(KeyFail, false)) return State.Failed;
                var tag = LatestTag;
                if (string.IsNullOrEmpty(tag)) return State.Unknown;
                return Compare(tag, PulseWatchConst.Version) > 0 ? State.Newer : State.Latest;
            }
        }

        // check if a day has passed
        public static void CheckIfDue()
        {
            if (!Enabled) return;
            long at = System.Convert.ToInt64(EditorPrefs.GetString(KeyAt, "0"));
            if (System.DateTime.UtcNow - new System.DateTime(at, System.DateTimeKind.Utc) >= Interval) Check();
        }

        public static void Check()
        {
            if (!Enabled || running != null) return;
            var req = UnityWebRequest.Get(Api);
            req.timeout = 10;
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            running = req;
            Changed?.Invoke();
            req.SendWebRequest().completed += _ =>
            {
                bool ok = req.result == UnityWebRequest.Result.Success;
                Release rel = null;
                if (ok) { try { rel = JsonUtility.FromJson<Release>(req.downloadHandler.text); } catch { } }
                ok = ok && rel != null && !string.IsNullOrEmpty(rel.tag_name);
                if (ok) { EditorPrefs.SetString(KeyTag, rel.tag_name.Trim()); EditorPrefs.SetString(KeyUrl, rel.html_url ?? ""); }
                EditorPrefs.SetBool(KeyFail, !ok);
                EditorPrefs.SetString(KeyAt, System.DateTime.UtcNow.Ticks.ToString());
                req.Dispose();
                running = null;
                Changed?.Invoke();
            };
        }

        // "v0.2.1" vs "0.2.1-proto" -> positive when a is newer. A suffix (-proto, -beta) is cut off before comparing, and with
        // the same numbers the one without a suffix (the release) is newer. Unreadable -> 0 (treated as the same)
        public static int Compare(string a, string b)
        {
            var va = Parse(a, out bool preA);
            var vb = Parse(b, out bool preB);
            if (va == null || vb == null) return 0;
            int c = va.CompareTo(vb);
            return c != 0 ? c : preA == preB ? 0 : preA ? -1 : 1;
        }

        static System.Version Parse(string s, out bool pre)
        {
            s = (s ?? "").Trim().TrimStart('v', 'V');
            int i = s.IndexOfAny(new[] { '-', '+' });
            pre = i >= 0;
            if (pre) s = s.Substring(0, i);
            return System.Version.TryParse(s, out var v) ? v : null;
        }
    }
}
#endif
