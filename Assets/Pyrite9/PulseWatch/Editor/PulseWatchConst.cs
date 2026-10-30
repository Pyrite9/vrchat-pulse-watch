#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.PulseWatch
{
    static class PulseWatchConst
    {
        public const string Version = "0.1.0";
        public const string LogPath = "Logs/Pyrite9_PulseWatch_build.txt";
        public const long LogLimit = 512 * 1024;   // bytes; past this the build log starts over, the previous one kept as .old
        public const string ShaderName = "Pyrite9/PulseWatch/Proto";
        public const string ScanShaderName = "Pyrite9/PulseWatch/Scan";
        public const string FrostShaderName = "Pyrite9/PulseWatch/Frost";   // the blur under the disc and the plate (D41, GrabPass)
        public const string DripShaderName = "Pyrite9/PulseWatch/Drip";     // the hologram's light running down to the forearm (D41, GrabPass)
        public const string GlyphsGuid = "f97988e5840b4dc88720d89e763ac789";   // Textures/PulseWatchGlyphs.png (Source/glyph_atlas.py)
        public const string MenuIconsGuid = "40cac3afea1d401d8c66d4d13ff823b8";   // Textures/MenuIcons/ (Source/menu_icons.py, D69)

        // objects (relative to the PulseWatch root — FX paths)
        public const string Pivot = "WatchPivot";                 // + L / R: on each forearm axis at the watch, twists with the hand (Aim Constraint); its Seat = the watch pose
        public const string Watch = "Watch";                      // the watch face (Parent Constraint: source0 left Seat, source1 right — D18); local +Y = face normal, +Z = toward the hand
        public const string Holo = Watch + "/Holo";               // turns about the face normal so its +X (text right) follows right across the line of sight, +Z = 12 o'clock = up as seen (D17)
        public const string Face = Holo + "/Face";                // unit scale, animated with the panel; the sized radar disc is Face/Disc
        public const string Radar = Face + "/Disc";               // ring + radar disc on the wrist (D16); the cells only fill for the owner
        public const string WpDisc = Face + "/WpDisc";            // the waypoint on the disc (D27 3): skinned quad over Disc, root bone Face/WpEcho
        public const string Compass = Face + "/Compass";          // north on the disc's ring (D27 2): skinned quad over Disc, root bone the radar frame
        public const string Panel = Holo + "/Panel";              // unit scale, animated; holds the plate's spot and the plate
        public const string PlateBase = Panel + "/PlateBase";     // the plate's spot on the disc plane (Side clips flip its X); PlateAim under it turns to the eyes, PlateTip on it
        public const string Plate = Panel + "/Plate";             // follows PlateTip (Position Constraint), turned as the disc; the clock Plate/Quad and Plate/Beam0-3
        public const string PlateQuad = Plate + "/Quad";
        public const string Meter = Watch + "/Debug";
        public const string Button = Plate + "/Button";           // the waypoint button under the plate (D28 B); PressL / PressR under it: the depth receivers (D30)
        public const string ButtonQuad = Button + "/Quad";
        public const string LightTab = Plate + "/LightTab";       // the flashlight tab beside the waypoint button (D46); PressL / PressR as the button's
        public const string LightTabQuad = LightTab + "/Quad";
        public const string LightPressL = LightTab + "/PressL";
        public const string LightPressR = LightTab + "/PressR";
        public const string Flashlight = Watch + "/Flashlight";   // D46: the Spot Light, along the forearm toward the fingers (owner only)
        public const string PressL = Button + "/PressL";          // takes the left index (watch on the right wrist)
        public const string PressR = Button + "/PressR";          // takes the right index (watch on the left wrist)
        public const string Laser = "Laser";                      // + L / R: on each hand at the index tip, +Z along the finger (D29 Q14); VRCRaycast -> LaserHit; Beam under it
        public const string Reticle = "LaserHit/Reticle";
        public const string LaserHit = "LaserHit";                // where the laser hits (+Z = surface normal); Reticle on it (owner only), MarkSeat (+Y = normal, D35)
        public const string WpFeet = "WpFeet";                    // the avatar's origin: the desktop mark (D29 Q19)
        public const string Waypoint = "Waypoint";                // follows LaserHit/MarkSeat (VR) or WpFeet (desktop), frozen to the world when marked; Mark/Vis/Diamond (+ Tag)
        public const string DorsalRef = "DorsalRef";              // + L / R: under the hand bone, local +Y = back of the hand
        public const string HeadPivot = "HeadPivot";              // under the neck, copies the head rotation (first-person head shrink)
        public const string RadarRoot = "RadarRoot";              // at the eyes, turns with the head's yaw only; the receiver grid under it
        public const string YawFrame = "YawFrame";                // the same frame for the waypoint and the compass when the radar is left out
        public const float SessionMax = 359999f;                  // the session clip's length (s): 99:59:59, then it holds
        public const string ScanRoot = "ScanRoot";                // at the eyes: the scan wave's shell and surface spheres (owner only)
        public const string ScanShell = ScanRoot + "/Shell";
        public const string ScanSurface = ScanRoot + "/Surface";
        public const string ScanFloor = "ScanFloor";              // at the feet under the eyes (X / Z follow the eyes): the floor disc

        // parameters
        public const string Open = "PulseWatch/Open";             // synced: others see the panel
        public const string FaceProx = "PulseWatch/Face";         // receiver: watch face toward the eyes
        public const string GazeProx = "PulseWatch/Gaze";         // receiver: watch near the middle of the view
        public const string Manual = "PulseWatch/Manual";         // menu toggle: keep open (desktop)
        public const string Sens = "PulseWatch/Sens";             // menu: 0 normal, 1 loose, 2 strict
        public const string DebugOn = "PulseWatch/Debug";         // menu toggle: debug meter
        public const string Right = "PulseWatch/Right";           // menu toggle, synced + saved: watch on the right wrist (default from the setting, D18)
        public const string Inside = "PulseWatch/Inside";         // menu toggle, synced + saved: the watch on the inside of the wrist (palm side, D34)
        public const string Hide = "PulseWatch/Hide";             // menu toggle, synced + saved: others don't see the hologram (the wearer still does)
        public const string Voice = "Voice";                      // VRChat built-in: the wearer's mic level 0-1 -> the disc's outer ring glows (D20)
        // voice ring (D58, 10-68): Voice -> a steeper curve (quiet speech already lights the ring) -> VoiceS, smoothed in the FX each
        // frame (VoiceS = k * VoiceS + (1 - k) * curve): k = VoiceRise while Voice > VoiceOn, VoiceFall below it, so the ring fades out
        // after speech. k is per frame — at 90 fps a time constant of about 0.03 s / 0.24 s; lower frame rates fade slower
        public static readonly float[] VoiceIn = { 0f, 0.02f, 0.06f, 0.15f, 0.35f };
        // D59 (10-69): the top halved (v32 at full voice was too strong) — the same curve shape at half height
        public static readonly float[] VoiceOut = { 0f, 0.175f, 0.325f, 0.45f, 0.5f };
        public const float VoiceOn = 0.01f, VoiceRise = 0.7f, VoiceFall = 0.955f;
        public const string VoiceS = "PulseWatch/VoiceS";         // local, animated by the FX (the smoothed ring level 0-1)
        // constant weights of the smoothing trees (k and 1 - k, a Direct tree's weights must be parameters)
        public const string VoiceRiseK = "PulseWatch/VoiceRiseK", VoiceRiseK1 = "PulseWatch/VoiceRiseK1";
        public const string VoiceFallK = "PulseWatch/VoiceFallK", VoiceFallK1 = "PulseWatch/VoiceFallK1";
        public const string MuteSelf = "MuteSelf";                // VRChat built-in: the wearer muted -> the disc's ring red + mic (D27)
        public const string One = "PulseWatch/One";               // constant 1 (Direct blend tree weight)
        public const string Local = "PulseWatch/Local";           // D70: 1 on the wearer's copy (Detect's driver), 0 on others' — IsLocal as a blend tree weight
        public const string IsLocal = "IsLocal";
        public const string GestureLeft = "GestureLeft", GestureRight = "GestureRight", VRMode = "VRMode";   // VRChat built-ins
        public const int GesturePoint = 3;                        // D67: the tabs answer only the other hand in this gesture (index out)
        // waypoint (D26-D30): 4 synced bits
        public const string WpOn = "PulseWatch/WpOn";             // synced: a waypoint is set
        public const string WpSeq = "PulseWatch/WpSeq";           // synced: flipped on every mark — the flip is the mark (late joiners wait for one)
        public const string WpLaser = "PulseWatch/WpLaser";       // synced: the laser is on (others' copies cast it too, so their mark lands in the same place)
        public const string WpShow = "PulseWatch/WpShow";         // synced + saved, menu toggle: others see the mark, the laser and the disc's waypoint (D27 / D29 Q17, 10-95)
        public const string WpPress = "PulseWatch/WpPress";       // receiver: how deep the other index is in the button (D30)
        public const string WpTap = "PulseWatch/WpTap";           // menu button: mark (desktop: at the feet; VR: laser on)
        public const string WpClear = "PulseWatch/WpClear";       // menu button: clear
        public const string Flash = "PulseWatch/Flash";           // D46: the flashlight is on — local, not synced (menu toggle, the tab flips it)
        public const string FlashPress = "PulseWatch/FlashPress"; // receiver: how deep the other index is in the flashlight tab
        public const string WpRay = "PulseWatch/WpRay";           // VRCRaycast parameter prefix -> _Hit / _Ratio / _Distance
        public const string WpRayHit = WpRay + "_Hit", WpRayRatio = WpRay + "_Ratio";
        // receiver: proximity of the nearest head to that slice's centre (0-1, D23); one slice per cell on the inner rings (D36)
        public static string Cell(int dir, int band, int slice) => RadarSlices[band] == 1 ? $"PulseWatch/Radar/{dir}_{band}" : $"PulseWatch/Radar/{dir}_{band}_{slice}";
        // radar material property of a slice: ring b's slices in order (dir x slices + slice) four to a vector, _Rad{b}_{m / 4}.{m % 4}
        public static string CellProp(int dir, int band, int slice) { int m = dir * RadarSlices[band] + slice; return $"_Rad{band}_{m / 4}.{"xyzw"[m % 4]}"; }
        // every slice, ring by ring (the order the parameters and the tree take)
        public static IEnumerable<(int dir, int band, int slice)> AllSlices()
        {
            for (int b = 0; b < RadarRings.Length - 1; b++)
                for (int k = 0; k < RadarDirs; k++)
                    for (int i = 0; i < RadarSlices[b]; i++)
                        yield return (k, b, i);
        }

        // contact tags (self only)
        public const string FaceTag = "Pyrite9PulseWatchFace";
        public const string GazeTag = "Pyrite9PulseWatchGaze";
        public const string HeadTag = "Head";                     // VRChat built-in contact tag on every avatar's head (others only)

        // radar grid (docs/pulse-watch.md 4-4, D9): 12 directions (dir 0 = ahead, clockwise from above) x 5 bands between these
        // distances (m, world). Cells are vertical capsules, so heads above / below (sitting, lying) still count. At 8-14 m a
        // 7.5 deg slice is about as much as one receiver covers (contact limit: radius 3 m, any size 6 m — D36)
        public const int RadarDirs = 12;
        public static readonly float[] RadarRings = { 0.4f, 1.5f, 3f, 5f, 8f, 14f };
        public const float RadarDrop = 0.5f;                      // capsule centre below the eyes (m)

        // a slice's receiver: one circle (seen from above) covering the slice's depth and its arc — on the inner rings it reaches well
        // into the neighbours (0.4-1.5 m: 0.58 m against a half-width of 0.25), so one head near the wearer reaches 2-3 receivers.
        // D36: one circle per 30 deg cell left its corners open from 3 m out (12% of the disc, 15% of the far ring — a head on a
        // cell border at 7-9 m or past 12 m went unseen, docs 10-51), and the far ring's can't grow past the 3 m limit, so the
        // outer rings split each cell into equal slices and every slice's circle reaches its corners (a head touching it counts)
        public static readonly int[] RadarSlices = { 1, 1, 2, 2, 4 };
        public const float CoverMargin = 0.03f;                   // m past the slice's farthest corner (less the head radius)
        public static float CellMid(int band) => 0.5f * (RadarRings[band] + RadarRings[band + 1]);
        public static float SliceAngle(int dir, int band, int slice) => (dir + (slice + 0.5f) / RadarSlices[band] - 0.5f) * 360f / RadarDirs;   // deg, clockwise from ahead
        public static float CellDepth(int band) => RadarRings[band + 1] - RadarRings[band];
        public static float CellArc(int band) => 2f * CellMid(band) * Mathf.Sin(Mathf.PI / (RadarDirs * RadarSlices[band]));   // between slice centres
        static float Corner(float r, float half, float mid) => Mathf.Sqrt(r * r + mid * mid - 2f * r * mid * Mathf.Cos(half));
        public static float CoverRadius(int band)
        {
            float half = Mathf.PI / (RadarDirs * RadarSlices[band]), mid = CellMid(band);
            return Mathf.Max(Corner(RadarRings[band], half, mid), Corner(RadarRings[band + 1], half, mid)) - HeadRadius + CoverMargin;
        }
        public static float CellRadius(int band) => Mathf.Min(Mathf.Max(0.5f * Mathf.Max(CellDepth(band), CellArc(band)) * 1.05f, CoverRadius(band)), 3f);
        // D23: a Proximity receiver reads 1 - (distance - sender radius) / radius (clamped; measured in the editor). One head between
        // two neighbouring slices is at least their spacing (the arc) away from both centres together, so its two readings add up
        // to at most 2 - (arc - 2 x head radius) / radius. A larger pair sum takes two heads; below it the shader keeps only the
        // higher of the two. The head sender's radius differs per avatar (HeadRadius is a guess) — PairMargin absorbs some of it
        public const float HeadRadius = 0.12f;                    // VRChat's built-in Head sender, world m (확인 필요)
        public const float PairMargin = 0.08f;
        public static float PairProximity(int band) =>
            Mathf.Clamp(2f - (CellArc(band) - 2f * HeadRadius) / CellRadius(band) + PairMargin, 0f, 2f);
        // across rings (same direction) the radii differ, so the shader compares distances instead: (1 - p) x radius + HeadRadius
        // from each centre — the nearer wins unless both are closer together than one head could be (spacing - margin x radius)

        // scan wave on opening (stage 5, docs/pulse-watch.md 10-11 / 10-12): out to the radar's reach, owner only
        public const float ScanTime = 1.4f;                       // s, 0 -> RadarRings[^1], fast start easing out
        public const float ScanCooldown = 5f;                     // s from a scan's start: opening again inside this skips the wave
        // the scan's depth light (10-96): a shadowed directional light makes Unity render the camera depth texture in worlds
        // without one. Intensity above 0 (0 = skipped), shadow strength 0, culling only an empty reserved layer — the mask
        // must not be Nothing (skipped too); editor test 10-96
        public const float DepthLightIntensity = 0.001f;
        public const int DepthLightLayer = 21;                    // VRChat's reserved4: nothing is drawn there

        // detection geometry: tip arms of L, receiver radius = chord at 90 deg -> proximity = 1 - sin(a/2) / sin(45)
        public const float Arm = 0.1f;                            // m, avatar scale (L)
        public const float MaxAngle = 90f;
        public const float SensStep = 10f;                        // loose = +10 deg, strict = -10 deg

        public static float ProxAt(float deg) =>
            1f - Mathf.Sin(Mathf.Clamp(deg, 0f, MaxAngle) * 0.5f * Mathf.Deg2Rad) / Mathf.Sin(MaxAngle * 0.5f * Mathf.Deg2Rad);

        // layout D16 (docs/pulse-watch.md 10-13), metres at a 25 cm forearm (x s), in the Holo frame (+Y face normal, +Z = 12 o'clock
        // = up on the face as the wearer sees it, +X = text right): the disc lies on the wrist, the clock plate floats below it toward the hand
        // (D18: lower right on the left wrist, lower left on the right)
        public const float DiscSize = 0.095f;                     // disc diameter
        public const float DepthBias = 0.08f;                     // the hologram draws over the wearer's hand / sleeve up to this close in front of it (v7 VR)
        public const float DiscLift = 0.012f;                     // disc above the watch face (clear of sleeves)
        public const float PlateWidth = 0.060f, PlateAspect = 1.8f;
        public const float FrostBlur = 0.008f;                    // blur radius under the disc / plate (D41, 10-55: 4 mm hardly showed)
        // the radar disc's layers (D42, 10-57 "cells high"): height up the normal per layer — 0 backing, 1 grid + ring distances,
        // 2 occupied cells, 3 sweep + scan front, 4 outer ring / ticks / heading, 5 centre arrow + count tab. D53 (10-64, v27 "cells
        // still too high"): all lower. D56 (10-66, v29 "the backing and the grid apart, a little higher, gaps a little wider"):
        // 0 / 1.2 grid / 1.4 sweep / 1.9 ring / 3.0 cells / 3.5 compass / 4.0 arrow / 4.5 waypoint mm. D57 (10-67): the scan front
        // (the yellow ring at the grid's edge) and the sweep above the outer (mute) ring — 0 / 1.2 grid / 1.9 ring / 2.4 sweep / 3.0 cells
        public static readonly float[] DiscLayers = { 0f, 0.0012f, 0.003f, 0.0024f, 0.0019f, 0.004f };
        public const float CompassLift = 0.0035f, WaypointLift = 0.0045f;   // D53: the compass N and the waypoint on the disc, up the normal like a layer (m x s)
        public const float DripLength = 0.02f;                    // light drip (D41, 10-56): longest streak down the normal — about the plate's height over the face
        public const float DripTop = 0.04f;                       // its board reaches this far up the normal (over the lifted plate)
        public const float DripHalfWidth = 0.1f;                  // and this far each side across the view (disc + plate)
        public const float PlateDripHalfWidth = 0.045f;           // the plate's own board (D43): across the view, over the plate's half diagonal
        public const float PlateDripTop = 0.02f;                  // and up Face's normal from the plate's origin (the plate leans toward the eyes)
        public const float DripBias = 0.03f;                      // drawn over skin / sleeve this much farther than the hologram's own DepthBias
        public const float PlateLift = 0.015f;                    // plate lifted from its spot toward the wearer's eyes (D19) — just in front of the disc (v10 VR: 40 too high)
        public const float PlateOut = 0.055f, PlateDown = 0.050f; // the plate's spot on the disc plane: toward the hand / below the disc centre (D19 A). Lifted along the
                                                                  // line of sight it stays on that spot on screen whatever the angle (lifted along the face normal it
                                                                  // rode up the screen the more the wrist was seen at a slant — v8 VR)
        public const float BeamLength = 0.0105f;                  // the plate's corner beams: straight down toward the disc plane, fading (0.7 x the lift)

        // waypoint button (D28 B / D30), x s like the layout: a tab below the plate; the depth receiver's centre sits behind its face
        // (away from the eyes), so proximity = how far the fingertip is pushed in: at the face 0.5, PressDepth x radius past it 1
        public const float ButtonDown = 0.0246f;                  // button centre below the plate centre
        public const float ButtonW = 0.018f, ButtonH = 0.0108f;
        public const float ButtonThick = 0.0025f;                 // D52: the tabs' raised top over their base (m x s)
        public const float ButtonCut = 0.22f;                     // the tabs' clipped corners (of the height) — PulseWatchProto WpButton's cut
        public const float PressDepth = 0.0075f;                  // receiver centre behind the face
        public const float PressRadius = 0.015f;                  // D66 (10-79): halved (was 30 / 15 mm) — it reached 45 mm behind the face and 30 mm aside
        public const float RevealTime = 0.3f;                     // D47 opening: the disc opens from its centre in this (s), easing out
        public const float RevealEnd = 1.05f;                     // ... to this radius (1 = the rim; the warm ring has faded by then)
        public const float PlateOutAt = 0.18f;                    // the plate starts sliding out at this (s)
        public const float PlateOutTime = 0.22f;                  // over this (s), growing from nothing
        public const float FoldTime = 0.25f;                      // closing: the plate folds and the disc shrinks in this (s), D43
        public const float PlateTuck = 0.38f;                     // starting this far of the way from its spot to the disc centre
        public const float TabGap = 0.004f;                       // D46: between the waypoint button and the flashlight tab (centres ButtonW + this apart)
        public const float FlashRange = 20f;                      // D46 flashlight (world m, not scaled), cone, strength, colour
        public const float FlashAngle = 55f;
        public const float FlashIntensity = 1.6f;
        public static readonly Color FlashColor = new Color(1f, 0.95f, 0.86f);
        public const float FlashBeamLength = 0.35f;               // D49: the short visible beam from the watch along the gaze (m x s) ...
        public const float FlashBeamWidth = 0.12f;                // ... its width at the far end (m x s), FlashBeamStart of that at the watch
        public const float FlashBeamStart = 0.15f;
        public const float FlashBeamGain = 0.25f;                 // D50: was 0.35 (with the glint, v27 "a bit much")
        public const float PressOn = 0.6f, PressOff = 0.3f;       // D30: in past this = down, back below this = up (start values — VR)
        public const float HoldTime = 1.2f;                       // s held = clear (D29 Q16)
        public const float ArmTime = 0.3f;                        // s after opening the button ignores the finger (4-5)
        // misfire guards (D60, 10-70): a press counts once the finger has stayed past PressOn for PressMin (a brush passes through
        // quicker); a tab is ignored while the other tab is pressed (past PressOn — one finger on both); after a tap the tab
        // ignores the finger for Refractory (a shaky finger flipping twice); a held clear waits UndoTime (the red bar running
        // back) before it is sent — a tap in that time keeps the waypoint
        public const float PressMin = 0.1f, Refractory = 0.2f, UndoTime = 2f;
        // laser (D29), world metres
        public const float LaserReach = 50f;
        public const float LaserTimeout = 10f;                    // s without a shot -> off (D29 Q16)
        public const float FireHold = 0.3f;                       // s the laser stays on after a shot (others' copies cast until the flip reaches them)
        public const float TrackTime = 0.05f;                     // s the mark follows its source before freezing (portal gun)
        public const float DiamondSize = 0.22f, DiamondLift = 0.45f, ReticleSize = 0.12f, LaserWidth = 0.006f;
        // the mark (D63, 10-74): the diamond alone (the floor ring left out), bobbing HoverAmp (m) up and down every HoverPeriod (s),
        // its name tag with it
        public const float HoverAmp = 0.04f, HoverPeriod = 3.5f;
        // D64 (10-75): true = past TagNear the diamond grows with the distance like its tag (both about the same size on screen);
        // false (chosen) = both keep their size in the world — the tag grown alone outgrew the diamond far off
        public const bool MarkGrow = false;
        // name tag (D61, 10-71): TagHeight tall (m) TagGap above the diamond — with MarkGrow held at that size out to TagNear (m)
        // and growing with the distance past it, the diamond too (D64); the name is cut to TagMaxChars. The texture: TagPixels tall
        public const float TagHeight = 0.09f, TagGap = 0.03f, TagNear = 4f;
        public const int TagMaxChars = 32, TagPixels = 128;
        // OS fonts for the name, in order — later ones fill the characters the first lacks (Japanese kana from Yu Gothic, Korean from Malgun)
        public static readonly string[] TagFonts = { "Bahnschrift", "Segoe UI", "Yu Gothic UI", "Malgun Gothic", "Microsoft YaHei UI", "Arial" };

        // palette B (D16): dark glass, white lines and text, yellow accent. D68: the defaults of the inspector's two colours
        // (PulseWatch.accentColor / waypointColor — keep them the same); the build uses the component's
        public static readonly Color Accent = new Color(1f, 0.88f, 0.05f);
        public static readonly Color WaypointColor = new Color(0.30f, 0.85f, 1f);   // cyan (D26 proto)
        public const float DimColor = 0.5f;   // D68: the inspector warns below this brightest channel (the hologram is light on dark glass)
        public const string MarkShaderName = "Pyrite9/PulseWatch/Mark";
    }
}
#endif
