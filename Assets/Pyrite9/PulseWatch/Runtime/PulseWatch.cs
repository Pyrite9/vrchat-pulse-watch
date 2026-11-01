using UnityEngine;
using VRC.SDKBase;

namespace Pyrite9.PulseWatch
{
    public enum PulseWatchSide { Left = 0, Right = 1 }
    public enum PulseWatchMenuLanguage { English = 0, Japanese = 1, Korean = 2 }

    /// <summary>
    /// Wrist watch settings. Editor-only (stripped from the uploaded avatar); the build plugin (Editor/PulseWatchPlugin.cs)
    /// places the watch on the avatar's wrist and builds the look-at-wrist detection, the pop-up panel, menu and FX layers.
    /// A radar disc lying on the wrist with the wearer's clock plate below it, turned upright to the wearer's view and shown only
    /// while the wrist is turned to the eyes (docs/pulse-watch.md, layout D16-D18); the features below switch on and off (D32).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pyrite9/Pulse Watch")]
    public class PulseWatch : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Expression Menu labels (and the inspector's text), applied at build.")]
        public PulseWatchMenuLanguage menuLanguage = PulseWatchMenuLanguage.English;

        [Tooltip("Wrist the watch is worn on at first. The menu (Pulse Watch > Settings > Wear on right wrist) switches it in game; Settings > Wear on inside of wrist moves it to the palm side.")]
        public PulseWatchSide side = PulseWatchSide.Left;

        // D68: baked at build (no parameters). Defaults = PulseWatchConst.Accent / WaypointColor
        [Header("Colors")]
        [Tooltip("The hologram's accent (hands, gauges, highlights) and the scan wave. Applied at build.")]
        public Color accentColor = new Color(1f, 0.88f, 0.05f);
        [Tooltip("The waypoint's laser, mark, name tag and reticle, its arrow on the disc and the waypoint button. Applied at build.")]
        public Color waypointColor = new Color(0.30f, 0.85f, 1f);

        [Header("Placement")]
        [Range(0.6f, 1f), Tooltip("Where along the forearm (elbow 0 -> wrist joint 1) the watch sits.")]
        public float alongForearm = 0.88f;
        [Range(0.03f, 0.2f), Tooltip("Height of the watch face above the forearm's axis, as a share of the forearm length.")]
        public float wristRadius = 0.09f;

        [Header("Detection (degrees)")]
        [Tooltip("Watch face toward the eyes: opens inside this angle (normal sensitivity). Loose / strict add +-10.")]
        [Range(10f, 60f)] public float faceOpen = 40f;
        [Tooltip("...and closes outside this angle (normal sensitivity).")]
        [Range(20f, 80f)] public float faceClose = 55f;
        [Tooltip("Watch near the middle of the view (head forward vs. direction to the watch): opens inside this angle.")]
        [Range(10f, 70f)] public float gazeOpen = 45f;
        [Range(20f, 85f)] public float gazeClose = 60f;
        [Tooltip("Both conditions have to hold this long before the panel opens (s).")]
        [Range(0f, 1f)] public float dwell = 0.15f;

        [Header("Time")]
        [Tooltip("Use this PC's time zone (its standard offset) when the avatar is built. Off = the offset below.")]
        public bool autoTimeZone = true;
        [Tooltip("Wearer's standard UTC offset in minutes (Korea / Japan +540, UK 0, US Eastern -300, India +330). Everyone sees this time.")]
        [Range(-720, 840)] public int utcOffsetMinutes = 540;

        // Features (docs/pulse-watch.md 5-1, D32): a feature switched off is left out of the build entirely — no layers,
        // parameters, contacts or menu items
        [Header("Radar")]
        [Tooltip("Players' heads around you on a 12-direction x 5-ring grid out to 14 m, shown on the disc under the clock, with the occupied-cell count — to you only (others see the empty rings). 120 Local Only contacts, no FX layer of its own (in the shared value layer).")]
        public bool radar = true;

        [Header("Scan")]
        [Tooltip("Opening the watch sends a scan wave from your eyes out to 14 m — to you only. With a depth texture it runs over floors, walls and people; without one, a ring of light and a floor grid. Not again within 5 s. 1 FX layer.")]
        public bool scan = true;
        [Tooltip("Makes the depth texture in worlds that have none, so the wave runs over floors and walls everywhere: a directional light with shadows, on for the wave only and on your side only. It lights nothing and casts no visible shadow (strength 0, an empty layer). 1 real Light — Performance Very Poor together with the flashlight's.")]
        public bool scanDepth = true;

        [Header("Waypoint")]
        [Tooltip("A button under the clock: press it with the other hand's index, that hand pointing, for a laser; pull that hand's trigger to mark where it hits — floor, wall or ceiling (menu on desktop); the disc shows its direction and distance. 4 synced bits, 2 VRC Raycasts, 5 FX layers.")]
        public bool waypoint = true;
        [Tooltip("A name tag above the mark: your name in a small dark tab, bobbing with it and keeping its size in the world like the mark (others see it when the mark is shown to them). Baked when the avatar is built. 1 quad, 1 small texture.")]
        public bool waypointName = true;
        [Tooltip("The name on the tag. Empty = the display name of the account signed in to the VRChat SDK when the avatar is built (rebuild after a name change).")]
        public string waypointNameText = "";

        [Header("Flashlight")]
        [Tooltip("A second tab beside the waypoint button: press it with the other index, that hand pointing (or the menu), to light a spot lamp from the watch toward where you look, with a short beam from the watch. You see it, others don't. 1 real Light (Performance: Poor), 2 FX layers.")]
        public bool flashlight = true;

        [Header("Display")]
        [Tooltip("The disc's outer ring glows while you speak (built-in Voice) and turns red with a mic mark while you are muted (MuteSelf). 2 FX layers.")]
        public bool voiceRing = true;
        [Tooltip("Your frame rate above the seconds on the clock — on your own screen only, no FX layer of its own (in the shared value layer).")]
        public bool fps = true;
        [Tooltip("The watch's world position (X Y Z, 0.1 m) next to the date on the clock. Shader only.")]
        public bool coordinates = true;
        [Tooltip("North on the disc's ring — a triangle and an N that turn as you turn (the world's +Z is north). One skinned quad over the disc, no FX layer.")]
        public bool compass = true;
        [Tooltip("How long you have been in this world (from the avatar's load) right of the seconds gauge — on your own screen only. 1 FX layer.")]
        public bool sessionTime = true;
        [Tooltip("Frosted glass: a smoky glass behind the disc and the clock that catches the surroundings softly (the world's reflection probe, blurred) with a faint sheen. No screen copy (no GrabPass), 2 quads.")]
        public bool frost = true;
        [Tooltip("Seen from the side, the hologram's light runs straight down to your wrist; gone when you look at the watch, so the radar stays sharp. No screen copy: it reads a picture of this watch baked at build time (2 small textures, about 0.5 MB) and draws the time, radar, mute and voice live. 1 more material on the disc, 1 quad.")]
        public bool drip = true;

        [Header("Prototype")]
        [Tooltip("Debug meter above the forearm (owner only, menu toggle, starts shown): face / gaze closeness with the open (bright) and close (dim) marks. Off = not built. 1 FX layer.")]
        public bool debugMeter = false;
    }
}
