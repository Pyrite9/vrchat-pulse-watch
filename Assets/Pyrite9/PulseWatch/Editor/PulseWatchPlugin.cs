// Pyrite9 Pulse Watch — builds everything per avatar at build time (the prefab only carries the PulseWatch settings).
//   Resolving, before MA (docs/pulse-watch.md 4-1 / 9):
//     watch    : WatchPivotL / R on each forearm axis near the wrist (MA Bone Proxy to the lower arm). A VRC Aim Constraint keeps
//                its +Z on the hand and its +Y on the back of the hand (DorsalRefL / R under the hand bone, ObjectRotationUp) — the
//                seat follows the hand's twist even where the lower arm bone only takes part of it (R1).
//                Watch (face, +Y = face normal) on one Seat by a VRC Parent Constraint (menu: right hand, D18) with Holo — turned about the face normal so the disc's 12 o'clock and the text's
//                up are up as the wearer sees the face (D17, VRC Aim Constraint) — holding Face/Disc (ring + radar lying on
//                the wrist) and Panel/Plate (clock plate below the disc toward the hand — D18 — lifted from its spot PlateBase toward
//                the eyes — D19: PlateAim aimed at EyePoint, Plate follows PlateTip) with Quad + Beam0-3; Debug
//                (owner-only meter) on Watch — disc and plate show only while open (D16)
//     detect   : angle -> distance. Two tips of length L from one point are 2L sin(a/2) apart; a receiver of radius
//                2L sin(45) on one tip gives proximity 1 - sin(a/2)/sin(45) (PulseWatchConst.ProxAt)
//                  face: FaceTip (receiver) on the face normal, EyeTip (sender) aimed at the eyes
//                  gaze: GazeTip (receiver) ahead of the eyes, WatchTip (sender) aimed at the watch
//                eyes: HeadPivot under the neck copying the head rotation, EyePoint at the view position, Sight (aimed at the watch)
//                (not under the head bone: VRChat shrinks it in first person and its children collapse — Drink Can 9-15)
//                all contacts local only, self only
//     radar    : RadarRoot at the eyes (yaw only), 12 x 5 cells of Local Only Proximity receivers for others' Head tag (the
//                outer rings' cells in 2-4 slices, a receiver each — D36) -> slice params drawn on the disc (docs/pulse-watch.md 4-4)
//     scan     : ScanRoot at the eyes (shell + surface spheres) and ScanFloor at the feet (disc), PulseWatchScan.shader —
//                a wave out to the radar's reach on opening, owner only (docs/pulse-watch.md 10-12)
//     waypoint : (D26-D30, docs/pulse-watch.md 4-5-1) Plate/Button — the tab (Quad) and two Local Only Proximity receivers
//                PressL / PressR behind its face for the other hand's built-in index tag (D30 depth); LaserL / R on each hand at
//                the index tip (MA Bone Proxy to the hand, so the trigger's curling finger does not move it — D29 Q14) with a
//                VRCRaycast -> LaserHit (+Z = surface normal, Reticle, MarkSeat) and a Beam; Waypoint follows LaserHit/MarkSeat
//                — floor, wall or ceiling, +Y off the surface (D35) — or WpFeet on desktop (Q19), by a VRC Parent Constraint the FX freezes to the world on a mark — Mark/Vis/Diamond (+ Tag, D61), PulseWatchMark.shader
//     flash    : Plate/LightTab beside the button (D46) and a Spot Light along the forearm, owner only
//     fx/menu  : FX controller (PulseWatchAnimator) via MA Merge Animator, MA Parameters, MA menu (D69)
//   Features switched off in the settings (radar, scan, waypoint, flashlight, voiceRing, fps, coordinates, compass,
//   sessionTime, frost, drip, debugMeter — D32)
//   are left out entirely: no objects, contacts, layers, parameters or menu items
//   Log: Logs/Pyrite9_PulseWatch_build.txt when the project has a Logs folder.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using static Pyrite9.PulseWatch.PulseWatchConst;

[assembly: ExportsPlugin(typeof(Pyrite9.PulseWatch.PulseWatchPlugin))]

namespace Pyrite9.PulseWatch
{
    public class PulseWatchPlugin : Plugin<PulseWatchPlugin>
    {
        public override string QualifiedName => "pyrite9.pulsewatch";
        public override string DisplayName => "Pyrite9 Pulse Watch";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Build pulse watch", Build);
        }

        static void Build(BuildContext ctx)
        {
            var log = new StringBuilder();
            log.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] Pyrite9 Pulse Watch {Version} — {ctx.AvatarRootObject.name}");
            var all = ctx.AvatarRootTransform.GetComponentsInChildren<PulseWatch>(true);
            // a watch whose GameObject is switched off is left out (MA would still merge its animator and menu)
            var watches = all.Where(w => SwitchedOn(w.transform, ctx.AvatarRootTransform)).ToArray();
            if (all.Length > watches.Length) log.AppendLine($"{all.Length - watches.Length} switched-off Pulse Watch(es) — left out");
            if (watches.Length > 1) log.AppendLine($"WARN {watches.Length} Pulse Watches on one avatar — only the first is built");
            var rec = new PwBuildRecord.Entry { avatar = ctx.AvatarRootObject.name, time = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), version = Version };
            // everything the build makes goes under the watch's own object — remember what was there, to undo a half build
            var target = watches.Length > 0 ? watches[0].transform : null;
            var hadKids = target == null ? null : new HashSet<Transform>(target.Cast<Transform>());
            var hadComps = target == null ? null : new HashSet<Component>(target.GetComponents<Component>());
            try
            {
                if (target != null) BuildOne(ctx, watches[0], log, rec);
            }
            catch (System.Exception e)
            {
                log.AppendLine("EXCEPTION " + e);
                Debug.LogException(e);
            }
            // stopped part way (a bone missing, an exception): leave nothing behind — no half watch, no merged FX or menu
            if (target != null && !rec.built)
            {
                int n = 0;
                foreach (var c in target.Cast<Transform>().Where(k => !hadKids.Contains(k)).ToList()) { Object.DestroyImmediate(c.gameObject); n++; }
                foreach (var c in target.GetComponents<Component>().Where(k => !hadComps.Contains(k)).ToList()) { Object.DestroyImmediate(c); n++; }
                if (n > 0) log.AppendLine($"not built — {n} objects / components made so far removed");
            }
            foreach (var w in all) Object.DestroyImmediate(w);
            try
            {
                if (Directory.Exists("Logs"))
                {
                    // past LogLimit the log starts over; the previous one stays as .old (one generation)
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > LogLimit) { File.Copy(LogPath, LogPath + ".old", true); File.WriteAllText(LogPath, ""); }
                    File.AppendAllText(LogPath, log.ToString());
                }
            }
            catch (IOException e) { Debug.LogWarning("[Pyrite9 Pulse Watch] build log not written: " + e.Message); }
            // inspector 'Last build' (PwBuildRecord): WARN lines without the tag, and the first line of an exception
            if (watches.Length > 0)
            {
                foreach (var line in log.ToString().Split('\n').Select(l => l.TrimEnd('\r')))
                {
                    if (line.StartsWith("WARN ")) rec.warnings.Add(line.Substring(5));
                    else if (line.StartsWith("EXCEPTION ")) rec.warnings.Add("Build error: " + line.Substring(10));
                }
                PwBuildRecord.Put(rec);
            }
            foreach (var line in log.ToString().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith("WARN")))
                Debug.LogWarning("[Pyrite9 Pulse Watch] " + line);
        }

        // D72: the zone's daylight saving rule in force today, for the shader to switch by date (PulseWatchTime.cginc PwDst): each
        // transition as (month, week 1-5 with 5 = last or 0 = a fixed date, day of week with 0 = Sunday or the fixed day, minutes into
        // the local day — the start's in standard time, the end's in daylight time, as TimeZoneInfo gives them); shift 0 = none
        internal static (Vector4 start, Vector4 end, int shift) DstRule(System.TimeZoneInfo tz)
        {
            var none = (Vector4.zero, Vector4.zero, 0);
            if (!tz.SupportsDaylightSavingTime) return none;
            Vector4 Tr(System.TimeZoneInfo.TransitionTime t) => new Vector4(t.Month, t.IsFixedDateRule ? 0 : t.Week,
                t.IsFixedDateRule ? t.Day : (int)t.DayOfWeek, (float)System.Math.Round(t.TimeOfDay.TimeOfDay.TotalMinutes));
            var today = System.DateTime.Today;
            foreach (var r in tz.GetAdjustmentRules())
            {
                if (r.DateStart > today || r.DateEnd < today || r.DaylightDelta == System.TimeSpan.Zero) continue;
                var (a, b) = (Tr(r.DaylightTransitionStart), Tr(r.DaylightTransitionEnd));
                return a == b ? none : (a, b, (int)System.Math.Round(r.DaylightDelta.TotalMinutes));
            }
            return none;
        }

        // values typed past the inspector's ranges (or set by a script) are pulled back to the [Range] of their field
        static void Sanitize(PulseWatch pw, StringBuilder log)
        {
            foreach (var f in typeof(PulseWatch).GetFields())
            {
                var r = (RangeAttribute)System.Attribute.GetCustomAttribute(f, typeof(RangeAttribute));
                if (r == null) continue;
                if (f.FieldType == typeof(float))
                {
                    float v = (float)f.GetValue(pw), c = float.IsNaN(v) ? r.min : Mathf.Clamp(v, r.min, r.max);
                    if (c != v) { f.SetValue(pw, c); log.AppendLine($"WARN {f.Name} {v} out of range — {c} used"); }
                }
                else if (f.FieldType == typeof(int))
                {
                    int v = (int)f.GetValue(pw), c = Mathf.Clamp(v, (int)r.min, (int)r.max);
                    if (c != v) { f.SetValue(pw, c); log.AppendLine($"WARN {f.Name} {v} out of range — {c} used"); }
                }
            }
        }

        // active in the hierarchy below the avatar root (the root itself not counted)
        static bool SwitchedOn(Transform t, Transform avatar)
        {
            for (; t != null && t != avatar; t = t.parent) if (!t.gameObject.activeSelf) return false;
            return true;
        }

        // D68: this build's colours (the inspector's), set at the top of BuildOne — the material builders below read them
        static Color accent = Accent, wpColor = WaypointColor;
        static Color Opaque(Color c) { c.a = 1f; return c; }

        static void BuildOne(BuildContext ctx, PulseWatch pw, StringBuilder log, PwBuildRecord.Entry rec)
        {
            var avatar = ctx.AvatarRootTransform;
            var root = pw.transform;
            var anim = avatar.GetComponent<Animator>();
            var desc = avatar.GetComponent<VRCAvatarDescriptor>();
            if (anim == null || !anim.isHuman) { log.AppendLine("WARN not a humanoid avatar — watch not built"); return; }
            bool left = pw.side == PulseWatchSide.Left;   // the default wrist; the menu switches it (D18)
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) { log.AppendLine("WARN head bone missing — watch not built"); return; }
            Sanitize(pw, log);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null && root.IsChildOf(hips))
                log.AppendLine($"WARN the watch is under the armature ({root.parent.name}) — put it right under the avatar root (the floor mark and scan floor take its height)");
            var made = new List<Object>();
            accent = Opaque(pw.accentColor);   // D68
            wpColor = Opaque(pw.waypointColor);
            log.AppendLine($"colours: accent #{ColorUtility.ToHtmlStringRGB(accent)}, waypoint #{ColorUtility.ToHtmlStringRGB(wpColor)}");

            // a seat on each wrist (the watch's pose there); Watch follows one of them (VRC Parent Constraint, menu: right hand)
            var seatL = Seat(root, anim, avatar, pw, true, log);
            var seatR = Seat(root, anim, avatar, pw, false, log);
            if (seatL.seat == null || seatR.seat == null) { log.AppendLine("WARN lower arm / hand bone missing — watch not built"); return; }
            var home = left ? seatL : seatR;
            float s = home.s;
            var watch = Child(root, "Watch", new Pose(home.seat.position, home.seat.rotation));
            var mount = watch.gameObject.AddComponent<VRCParentConstraint>();
            mount.Sources.Add(new VRCConstraintSource(seatL.seat, left ? 1f : 0f));   // source0 = left, back of the wrist (Side clips)
            mount.Sources.Add(new VRCConstraintSource(seatR.seat, left ? 0f : 1f));   // source1 = right, back
            mount.Sources.Add(new VRCConstraintSource(seatL.inside, 0f));            // source2 = left, inside (menu, D34)
            mount.Sources.Add(new VRCConstraintSource(seatR.inside, 0f));            // source3 = right, inside
            mount.IsActive = true;
            mount.Locked = true;

            // visuals: quads in the watch's XZ plane (normal +Y)
            var shader = Shader.Find(ShaderName);
            if (shader == null) log.AppendLine($"WARN shader {ShaderName} not found — the watch is invisible");
            var quad = QuadMesh(made);
            var glyphs = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(GlyphsGuid));
            if (glyphs == null) log.AppendLine("WARN glyph atlas missing (Textures/PulseWatchGlyphs.png) — no clock digits");
            int offset = pw.autoTimeZone ? (int)System.Math.Round(System.TimeZoneInfo.Local.BaseUtcOffset.TotalMinutes) : pw.utcOffsetMinutes;
            (Vector4 start, Vector4 end, int shift) dst = pw.autoTimeZone ? DstRule(System.TimeZoneInfo.Local) : (Vector4.zero, Vector4.zero, 0);   // D72
            Material Clocked(Material m)
            {
                if (m == null) return null;
                m.SetTexture("_Glyphs", glyphs);
                m.SetInteger("_UtcOffsetMin", offset);
                m.SetVector("_DstStart", dst.start);
                m.SetVector("_DstEnd", dst.end);
                m.SetInteger("_DstMin", dst.shift);
                m.SetInteger("_DateLang", (int)pw.menuLanguage);   // D73
                return m;
            }
            // Holo: quads in its XZ plane, X = text right, Z = text up / the disc's 12 o'clock. Rest pose (before the constraint
            // runs): 12 o'clock away from the eyes across the forearm (-X of the watch on the left wrist, +X on the right)
            var holoUp = Child(watch, "HoloUp", new Pose(watch.position + watch.up * 0.1f * s, watch.rotation));
            var holo = Unit(watch, "Holo", Vector3.zero);
            holo.localRotation = Quaternion.LookRotation(new Vector3(left ? -1f : 1f, 0f, 0f), Vector3.up);
            // Face / Panel = the unit-scale objects the FX scales (pop-up / fold, both only while open), the sized quads under them
            var face = Unit(holo, "Face", new Vector3(0f, DiscLift * s, 0f));
            var discMat = Clocked(Mat(shader, 1f, made));
            if (discMat != null)
            {
                discMat.SetFloat("_Radar", pw.radar ? 1f : 0f);
                discMat.SetVector("_Pair", new Vector4(PairProximity(0), PairProximity(1), PairProximity(2), PairProximity(3)));
                discMat.SetFloat("_PairFar", PairProximity(4));
                discMat.SetVector("_CellR", new Vector4(CellRadius(0), CellRadius(1), CellRadius(2), CellRadius(3)));
                discMat.SetFloat("_CellRFar", CellRadius(4));
                discMat.SetVector("_PairK", new Vector4(HeadRadius, PairMargin, 0f, 0f));
                discMat.SetFloat("_Layered", 1f);
            }
            // the disc in layers (D42): six quads stacked up the normal, each drawing one layer of the picture
            Visual(face, "Disc", Vector3.zero, new Vector3(DiscSize, 1f, DiscSize) * s, LayeredDisc(made), discMat, true);
            face.gameObject.SetActive(false);
            var panel = Unit(holo, "Panel", Vector3.zero);
            float plateH = PlateWidth / PlateAspect;
            // the plate's spot on the disc plane, below the centre toward the hand (D18; Side clips flip X) — lifted toward the eyes
            // once they exist (D19, below); Plate follows the lifted point, turned as the disc
            var plateBase = Unit(panel, "PlateBase", new Vector3((left ? 1f : -1f) * PlateOut, DiscLift, -PlateDown) * s);
            var plate = Unit(panel, "Plate", plateBase.localPosition);
            var plateMat = Clocked(Mat(shader, 0f, made));
            if (plateMat != null)
            {
                plateMat.SetFloat("_Aspect", PlateAspect);
                plateMat.SetFloat("_CoordOn", pw.coordinates ? 1f : 0f);
            }
            Visual(plate, "Quad", Vector3.zero, new Vector3(PlateWidth, 1f, plateH) * s, quad, plateMat, true);
            // beams from the plate's corners straight down toward the disc plane, fading (the shader turns them to the camera)
            var beamMat = Mat(shader, 3f, made);
            foreach (var m in new[] { discMat, plateMat, beamMat }) if (m != null) m.SetFloat("_DepthBias", DepthBias * s);
            var up = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            int bi = 0;
            foreach (var (cx, cz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                var corner = new Vector3(cx * PlateWidth, 0f, cz * plateH) * 0.5f * s;
                Visual(plate, "Beam" + bi++, corner - new Vector3(0f, BeamLength * 0.5f * s, 0f), new Vector3(0.0006f * s, 1f, BeamLength * s), quad, beamMat, true).transform.localRotation = up;
            }
            panel.gameObject.SetActive(false);
            var dbgMat = pw.debugMeter ? Mat(shader, 2f, made) : null;
            float[] faceO = Angles(pw.faceOpen), faceC = Angles(pw.faceClose), gazeO = Angles(pw.gazeOpen), gazeC = Angles(pw.gazeClose);
            if (dbgMat != null)
            {
                dbgMat.SetVector("_ThFaceOpen", Prox(faceO));
                dbgMat.SetVector("_ThFaceClose", Prox(faceC));
                dbgMat.SetVector("_ThGazeOpen", Prox(gazeO));
                dbgMat.SetVector("_ThGazeClose", Prox(gazeC));
            }
            if (pw.debugMeter) Visual(watch, "Debug", new Vector3(0f, 0.03f, -0.065f) * s, new Vector3(0.08f, 1f, 0.03f) * s, quad, dbgMat, false);   // toward the elbow, clear of the disc

            // eyes: pivot under the neck at the head joint, copying the head rotation
            var neckBone = anim.GetBoneTransform(HumanBodyBones.Neck) != null ? HumanBodyBones.Neck
                : anim.GetBoneTransform(HumanBodyBones.UpperChest) != null ? HumanBodyBones.UpperChest : HumanBodyBones.Chest;
            var headPivot = Anchor(root, HeadPivot, new Pose(head.position, head.rotation), neckBone);
            var rc = headPivot.gameObject.AddComponent<VRCRotationConstraint>();
            rc.Sources.Add(new VRCConstraintSource(head, 1f));
            rc.IsActive = true;
            rc.Locked = true;
            // ViewPosition is a world-space offset from the root (the SDK's handle draws it as is, NDMF reads it the same way) —
            // TransformPoint would apply the root's scale and rotation a second time (a scaled root put the eyes too high)
            var view = desc != null ? avatar.position + desc.ViewPosition : head.position;
            var fwd = Vector3.ProjectOnPlane(avatar.forward, avatar.up).normalized;
            var eye = Child(headPivot, "EyePoint", new Pose(view, Quaternion.LookRotation(fwd, avatar.up)));

            // Sight: at the eyes, +Z aimed at the watch, +Y as close as it gets to the head's up — its +X is "right" across the line
            // of sight, which is screen right wherever the watch sits in view (the head's own right is off by the head-to-watch yaw:
            // editor test 10-15, arm out diagonally, 12 o'clock 48° off)
            var sightDir = watch.position - eye.position;
            var sight = Child(eye, "Sight", new Pose(eye.position, sightDir.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(sightDir, eye.up) : eye.rotation));
            Aim(sight, watch, Vector3.forward, Vector3.up, eye, log);

            // Holo follows the line of sight (D17): +Y aimed exactly along the face normal (at HoloUp), +X (text right) as close
            // as it gets to Sight's right — so 12 o'clock / text up is "up" as the wearer sees the face, whatever way the forearm
            // points. Right, not up: the face is seen at a slant, and up laid straight onto a face that lies flat points sideways
            // (editor test 10-15: text turned 90°); right stays across the line of sight. Others' copies turn the same way (the
            // wearer's head and arm)
            Aim(holo, holoUp, Vector3.up, Vector3.right, sight, log);

            // D19: the plate lifted from its spot toward the eyes — along the line of sight it stays on that spot on screen at any
            // angle, and in front of the disc (lifted along the face normal it rode up the screen at a slant — v8 VR)
            var plateAim = AimAt(plateBase, "PlateAim", eye, log);
            var plateTip = Child(plateAim, "PlateTip", new Pose(plateAim.position + plateAim.forward * PlateLift * s, plateAim.rotation));
            plate.position = plateTip.position;
            Follow(plate, plateTip, true);
            // D47: source1 = the disc centre, weight 0 — the FX raises it while opening, so the plate starts tucked toward the disc
            // and slides out to its spot (either wrist: the blend is in world space)
            plate.GetComponent<VRCPositionConstraint>().Sources.Add(new VRCConstraintSource(face, 0f));

            int cells = pw.radar ? BuildRadar(root, eye, log) : 0;
            bool scan = pw.scan && BuildScan(root, eye, avatar.position.y, pw.scanDepth, made, log);
            bool depth = scan && pw.scanDepth;
            Vector3 beamL = Vector3.zero, beamR = Vector3.zero;
            // the radar frame (eyes, head yaw only) also places the waypoint on the disc and turns the compass; a frame of their
            // own when the radar is left out
            var frame = pw.radar ? root.Find(RadarRoot) : null;
            if (frame == null && (pw.waypoint || pw.compass)) frame = EyeYaw(root, YawFrame, eye);
            // the tabs under the plate (D46): the waypoint button and the flashlight tab side by side, one alone in the middle
            float tabShift = pw.waypoint && pw.flashlight ? (ButtonW + TabGap) * 0.5f : 0f;
            bool tag = false;
            bool wp = pw.waypoint && BuildWaypoint(root, avatar, anim, plate, face, frame, discMat, left, -tabShift, s, shader, MarkName(pw, log),
                made, log, out beamL, out beamR, out tag);
            bool flash = pw.flashlight && BuildFlash(watch, eye, plate, shader, left, tabShift, s, made, log);
            // compass (D27 2): a quad skinned to the disc whose root bone is the frame — the shader reads the heading from it
            bool compass = pw.compass && discMat != null;
            if (compass) DiscOverlay(face, "Compass", frame, discMat, 6f, CompassLift, made);
            // glass (D41): the blur under the disc and the plate, and the hologram's light running down to the forearm (needs the
            // blur: it reads the screen the blur copied before the hologram)
            var tabs = new[] { wp ? plate.Find("Button") : null, flash ? plate.Find("LightTab") : null }.Where(t => t != null).ToArray();
            bool frost = pw.frost && BuildFrost(face, plate, tabs, s, quad, made, log);
            bool drip = pw.drip && frost && BuildDrip(face, plate, tabs, plateMat, s, made, log);
            if (pw.drip && !frost) log.AppendLine("drip off: it needs the blur (the screen copied before the hologram)");

            // detection pairs (L and the receiver radius scale with the forearm)
            float L = Arm * s;
            float r = 2f * L * Mathf.Sin(MaxAngle * 0.5f * Mathf.Deg2Rad);
            // face: receiver on the face normal, sender aimed at the eyes
            var faceTip = Child(watch, "FaceTip", new Pose(watch.position + watch.up * L, watch.rotation));
            Receiver(faceTip, r, FaceProx, FaceTag);
            var eyeAim = AimAt(watch, "EyeAim", eye, log);
            var eyeTip = Child(eyeAim, "EyeTip", new Pose(eyeAim.position + eyeAim.forward * L, eyeAim.rotation));
            Sender(eyeTip, FaceTag);
            // gaze: receiver ahead of the eyes, sender aimed at the watch
            var gazeTip = Child(eye, "GazeTip", new Pose(eye.position + eye.forward * L, eye.rotation));
            Receiver(gazeTip, r, GazeProx, GazeTag);
            var watchAim = AimAt(eye, "WatchAim", watch, log);
            var watchTip = Child(watchAim, "WatchTip", new Pose(watchAim.position + watchAim.forward * L, watchAim.rotation));
            Sender(watchTip, GazeTag);

            // FX, parameters, menu
            var fx = PulseWatchAnimator.Build(faceO, faceC, gazeO, gazeC, pw.dwell, pw.radar, scan, new Vector3(PlateOut, DiscLift, -PlateDown) * s, wp, tag, beamL, beamR,
                flash, frost, compass, pw.voiceRing, pw.fps, pw.sessionTime, pw.debugMeter, made);
            log.AppendLine($"fx: {fx.layers.Length} layers, {fx.parameters.Length} parameters");
            var merge = root.gameObject.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = fx;
            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.pathMode = MergeAnimatorPathMode.Relative;
            merge.matchAvatarWriteDefaults = true;

            var ps = root.gameObject.AddComponent<ModularAvatarParameters>();
            ps.parameters.Add(Param(Open, ParameterSyncType.Bool, 0f));
            ps.parameters.Add(Param(Manual, ParameterSyncType.Bool, 0f, localOnly: true));
            var sens = Param(Sens, ParameterSyncType.Int, 0f, localOnly: true);
            sens.saved = true;
            ps.parameters.Add(sens);
            if (pw.debugMeter)
            {
                var dbg = Param(DebugOn, ParameterSyncType.Bool, 1f, localOnly: true);
                dbg.saved = true;
                ps.parameters.Add(dbg);
            }
            var right = Param(Right, ParameterSyncType.Bool, left ? 0f : 1f);   // synced: others see the watch on that wrist
            right.saved = true;
            ps.parameters.Add(right);
            var hide = Param(Hide, ParameterSyncType.Bool, 0f);   // synced: others' copies read it
            hide.saved = true;
            ps.parameters.Add(hide);
            var inside = Param(Inside, ParameterSyncType.Bool, 0f);   // synced: others see the watch on that side of the wrist
            inside.saved = true;
            ps.parameters.Add(inside);
            ps.parameters.Add(Param(FaceProx, ParameterSyncType.NotSynced, 0f));
            ps.parameters.Add(Param(GazeProx, ParameterSyncType.NotSynced, 0f));
            ps.parameters.Add(Param(One, ParameterSyncType.NotSynced, 1f));
            ps.parameters.Add(Param(Local, ParameterSyncType.NotSynced, 0f));   // D70
            if (pw.voiceRing)
                foreach (var (p, v) in new[] { (VoiceS, 0f), (VoiceRiseK, VoiceRise), (VoiceRiseK1, 1f - VoiceRise), (VoiceFallK, VoiceFall), (VoiceFallK1, 1f - VoiceFall) })
                    ps.parameters.Add(Param(p, ParameterSyncType.NotSynced, v));
            if (pw.radar)
                foreach (var (k, b, i) in AllSlices()) ps.parameters.Add(Param(Cell(k, b, i), ParameterSyncType.NotSynced, 0f));

            if (wp)
            {
                foreach (var p in new[] { WpOn, WpSeq, WpLaser }) ps.parameters.Add(Param(p, ParameterSyncType.Bool, 0f));
                var show = Param(WpShow, ParameterSyncType.Bool, 0f);
                show.saved = true;
                ps.parameters.Add(show);
                ps.parameters.Add(Param(WpTap, ParameterSyncType.Bool, 0f, localOnly: true));
                ps.parameters.Add(Param(WpClear, ParameterSyncType.Bool, 0f, localOnly: true));
                foreach (var p in new[] { WpPress, WpRayHit, WpRayRatio }) ps.parameters.Add(Param(p, ParameterSyncType.NotSynced, 0f));
            }
            if (flash)   // D46: local — others' copies never turn the light on
            {
                ps.parameters.Add(Param(Flash, ParameterSyncType.Bool, 0f, localOnly: true));
                ps.parameters.Add(Param(FlashPress, ParameterSyncType.NotSynced, 0f));
            }

            BuildMenu(root, (int)pw.menuLanguage, wp, flash, pw.debugMeter, log);

            foreach (var o in made.Where(o => o != null && !EditorUtility.IsPersistent(o))) ctx.AssetSaver.SaveAsset(o);
            log.AppendLine($"wrist: {(left ? "left" : "right")} by default, menu switches (D18); sizes x{s:0.00}, L {L:0.000} m, receiver r {r:0.000} m");
            log.AppendLine($"layout D16: disc {DiscSize * s * 1000f:0} mm at +{DiscLift * s * 1000f:0} mm, clock plate {PlateWidth * s * 1000f:0} x {PlateWidth / PlateAspect * s * 1000f:0} mm on a spot {PlateOut * s * 1000f:0} mm toward the hand and {PlateDown * s * 1000f:0} mm below the centre (D18), lifted {PlateLift * s * 1000f:0} mm toward the eyes (D19), turning with the head, radar on the disc {(pw.radar ? "on" : "off")}");
            log.AppendLine($"thresholds (normal / loose / strict, deg -> proximity): face open {Fmt(faceO)} close {Fmt(faceC)}, gaze open {Fmt(gazeO)} close {Fmt(gazeC)}, dwell {pw.dwell:0.00} s");
            log.AppendLine($"clock: UTC{(offset < 0 ? "-" : "+")}{System.Math.Abs(offset) / 60}:{System.Math.Abs(offset) % 60:00} ({(pw.autoTimeZone ? "this PC, " + System.TimeZoneInfo.Local.Id : "set by hand")}){(dst.shift != 0 ? $", daylight saving +{dst.shift} min from {dst.start} to {dst.end} (D72)" : "")}");
            log.AppendLine($"built: {made.Count} assets, {cells} radar receivers (60 cells); features: radar {On(pw.radar)}, scan {On(scan)} (depth light {On(depth)}), waypoint {On(wp)}, voice ring {On(pw.voiceRing)}, fps {On(pw.fps)}, coordinates {On(pw.coordinates)}, compass {On(compass)}, session time {On(pw.sessionTime)}, blur {On(frost)}, light drip {On(drip)}, flashlight {On(flash)}, debug meter {On(pw.debugMeter)}");

            rec.built = true;
            rec.side = (int)pw.side;
            rec.scale = s;
            rec.utcOffsetMinutes = offset;
            rec.autoTimeZone = pw.autoTimeZone;
            rec.daylightSaving = dst.shift != 0;
            rec.syncedBits = ps.parameters.Where(p => !p.localOnly && p.syncType != ParameterSyncType.NotSynced).Sum(p => p.syncType == ParameterSyncType.Bool ? 1 : 8);
            rec.fxLayers = fx.layers.Length;
            rec.contacts = root.GetComponentsInChildren<VRCContactReceiver>(true).Length + root.GetComponentsInChildren<VRCContactSender>(true).Length;
            rec.featuresOn = new[] { pw.radar, scan, depth, wp, tag, flash, pw.voiceRing, pw.fps, pw.coordinates, compass, pw.sessionTime, frost, drip }.Count(b => b);
            rec.debugMeter = pw.debugMeter;
        }

        // one wrist: WatchPivotL/R on the forearm axis (lower arm), twisting with the hand (aim at the hand, up = back of the hand
        // from DorsalRefL/R under the hand), and its Seat child where the watch face sits (+Y face normal, +Z toward the hand).
        // SeatIn = the inside of the wrist (D34, menu): on the palm side, turned half round the forearm axis — everything that hangs
        // off the face (detection, Holo, plate, button) follows, so opening it means turning the palm up to the eyes
        static (Transform seat, Transform inside, float s) Seat(Transform root, Animator anim, Transform avatar, PulseWatch pw, bool left, StringBuilder log)
        {
            string tag = left ? "L" : "R";
            var lowerArm = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (lowerArm == null || hand == null) return (null, null, 1f);

            // forearm frame: axis elbow -> wrist, back of the hand from the finger direction x the knuckle line
            var forearm = hand.position - lowerArm.position;
            float forearmLen = forearm.magnitude;
            var axis = forearm / forearmLen;
            var fingers = Bone(anim, left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var index = Bone(anim, left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = Bone(anim, left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var fingerDir = fingers != null ? (fingers.position - hand.position).normalized : axis;
            Vector3 dorsal;
            if (index != null && little != null)
            {
                var across = (index.position - little.position).normalized;
                dorsal = Vector3.Cross(fingerDir, across) * (left ? 1f : -1f);
            }
            else
            {
                dorsal = avatar.up;   // T / A pose: back of the hand up
                log.AppendLine($"WARN {tag}: index / little finger bones missing — back of the hand assumed to face up in the rest pose");
            }
            dorsal = Vector3.ProjectOnPlane(dorsal, axis).normalized;
            if (dorsal.sqrMagnitude < 0.5f) { dorsal = Vector3.ProjectOnPlane(avatar.up, axis).normalized; log.AppendLine($"WARN {tag}: back of the hand undefined — using up"); }
            float s = forearmLen / 0.25f;   // 1 = a 25 cm forearm
            float radius = pw.wristRadius * forearmLen;

            var pivotPose = new Pose(lowerArm.position + forearm * pw.alongForearm, Quaternion.LookRotation(axis, dorsal));
            var pivot = Anchor(root, Pivot + tag, pivotPose, left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var dorsalRef = Anchor(root, DorsalRef + tag, new Pose(hand.position, Quaternion.LookRotation(axis, dorsal)),
                left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Aim(pivot, hand, Vector3.forward, Vector3.up, dorsalRef, log);   // the seat follows the hand's twist

            log.AppendLine($"fit {tag}: forearm {forearmLen:0.000} m (x{s:0.00}), watch {pw.alongForearm:0.00} along, face {radius * 1000f:0} mm from the axis on the back of the wrist (SeatIn: the same on the inside)");
            var seat = Child(pivot, "Seat", new Pose(pivotPose.position + dorsal * radius, pivotPose.rotation));
            var inside = Child(pivot, "SeatIn", new Pose(pivotPose.position - dorsal * radius, Quaternion.LookRotation(axis, -dorsal)));
            return (seat, inside, s);
        }

        static string On(bool b) => b ? "on" : "off";

        // at the eyes, turning with the head's yaw only
        static Transform EyeYaw(Transform root, string name, Transform eye)
        {
            var frame = Child(root, name, new Pose(eye.position, eye.rotation));
            var pc = frame.gameObject.AddComponent<VRCPositionConstraint>();
            pc.Sources.Add(new VRCConstraintSource(eye, 1f));
            pc.IsActive = true;
            pc.Locked = true;
            var rc = frame.gameObject.AddComponent<VRCRotationConstraint>();
            rc.Sources.Add(new VRCConstraintSource(eye, 1f));
            var so = new SerializedObject(rc);
            so.FindProperty("AffectsRotationX").boolValue = false;
            so.FindProperty("AffectsRotationZ").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            rc.IsActive = true;
            rc.Locked = true;
            return frame;
        }

        // radar frame at the eyes turning with the head's yaw only (EyePoint: +Z ahead, +Y up at rest), and Local Only Proximity
        // receivers per cell slice (D23, D36) catching other players' built-in Head senders. Sizes in world metres at build time
        static int BuildRadar(Transform root, Transform eye, StringBuilder log)
        {
            var frame = EyeYaw(root, RadarRoot, eye);

            float sc = Mathf.Max(1e-4f, frame.lossyScale.x);
            int n = 0;
            for (int b = 0; b < RadarRings.Length - 1; b++)
            {
                float mid = CellMid(b);
                float radius = CellRadius(b);
                float height = Mathf.Min(Mathf.Max(3f, 2f * radius + 0.2f), 6f);
                for (int k = 0; k < RadarDirs; k++)
                    for (int i = 0; i < RadarSlices[b]; i++)
                    {
                        float a = SliceAngle(k, b, i) * Mathf.Deg2Rad;
                        var cell = new GameObject(RadarSlices[b] == 1 ? $"Cell{k}_{b}" : $"Cell{k}_{b}_{i}").transform;
                        cell.SetParent(frame, false);
                        cell.localPosition = new Vector3(Mathf.Sin(a) * mid, -RadarDrop, Mathf.Cos(a) * mid) / sc;
                        var c = cell.gameObject.AddComponent<VRCContactReceiver>();
                        c.shapeType = ContactBase.ShapeType.Capsule;
                        c.radius = radius / sc;
                        c.height = height / sc;
                        c.parameter = Cell(k, b, i);
                        c.receiverType = ContactReceiver.ReceiverType.Proximity;   // D23: how central, so the shader can pick one slice of the overlap
                        c.allowSelf = false;
                        c.allowOthers = true;
                        c.localOnly = true;
                        c.collisionTags = new List<string> { HeadTag };
                        n++;
                    }
                log.AppendLine($"radar ring {b}: {RadarRings[b]:0.0}-{RadarRings[b + 1]:0.0} m, {RadarSlices[b]} slice(s) a cell, radius {radius:0.00} m (corners need {CoverRadius(b) - CoverMargin:0.00}), height {height:0.0} m, two heads from a neighbour pair sum {PairProximity(b):0.00} (D23)");
            }
            return n;
        }

        // scan wave: unit meshes the shader places around their object's origin (world scale 1 at build, so it follows the
        // avatar's scale like the radar). ScanRoot follows the eyes; ScanFloor stays at the feet height, X / Z under the eyes
        static bool BuildScan(Transform root, Transform eye, float floorY, bool depthLight, List<Object> made, StringBuilder log)
        {
            var shader = Shader.Find(ScanShaderName);
            if (shader == null) { log.AppendLine($"WARN shader {ScanShaderName} not found — no scan wave"); return false; }
            float reach = RadarRings[RadarRings.Length - 1];
            var scanRoot = Child(root, ScanRoot, new Pose(eye.position, Quaternion.identity));
            Follow(scanRoot, eye, true);
            var floor = Child(root, ScanFloor, new Pose(new Vector3(eye.position.x, floorY, eye.position.z), Quaternion.identity));
            Follow(floor, eye, false);

            var bounds = new Bounds(Vector3.zero, Vector3.one * 2f * (reach + 1f));   // the shader moves the vertices out to here
            var sphere = SphereMesh(bounds, made);
            var disc = QuadMesh(made, "PulseWatch Scan Disc", bounds);
            Material M(string name, float mode, float zTest)
            {
                var m = new Material(shader) { name = "PulseWatch Scan " + name };
                m.SetFloat("_Mode", mode);
                m.SetColor("_Color", accent);
                m.SetFloat("_MaxRadius", reach);
                m.SetFloat("_Radius", 0f);
                m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                m.SetFloat("_ZTest", zTest);
                made.Add(m);
                return m;
            }
            float unit = 1f / Mathf.Max(1e-4f, scanRoot.lossyScale.x);
            Visual(scanRoot, "Shell", Vector3.zero, Vector3.one * unit, sphere, M("Shell", 0f, (float)UnityEngine.Rendering.CompareFunction.LessEqual), true);
            Visual(scanRoot, "Surface", Vector3.zero, Vector3.one * unit, sphere, M("Surface", 2f, (float)UnityEngine.Rendering.CompareFunction.Always), true);
            Visual(floor, "Disc", new Vector3(0f, 0.01f * unit, 0f), Vector3.one * unit, disc, M("Floor", 1f, (float)UnityEngine.Rendering.CompareFunction.LessEqual), true);
            if (depthLight)   // 10-96: on with the scan objects (the Scan layer, owner only) — the depth texture for the surface mode
            {
                var dl = Child(scanRoot, "DepthLight", new Pose(scanRoot.position, Quaternion.Euler(90f, 0f, 0f)));
                var light = dl.gameObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = DepthLightIntensity;
                light.color = Color.white;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = 0f;
                light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
                light.cullingMask = 1 << DepthLightLayer;
                light.renderMode = LightRenderMode.Auto;
            }
            scanRoot.gameObject.SetActive(false);
            floor.gameObject.SetActive(false);
            log.AppendLine($"scan: 0-{reach:0} m in {ScanTime:0.0} s, cooldown {ScanCooldown:0} s, owner only{(depthLight ? $"; depth light (directional, shadows strength 0, layer {DepthLightLayer} only, on with the wave — a Light: Performance Very Poor with the flashlight)" : "")}");
            return true;
        }

        // waypoint (D26-D30): button + receivers under the plate, a laser on each hand, the drop ray, the mark. Full beam scales
        // (local, ratio 1) go to the FX, which scales them by the raycast's _Ratio
        static bool BuildWaypoint(Transform root, Transform avatar, Animator anim, Transform plate, Transform face, Transform frame, Material discMat,
            bool left, float tabX, float s, Shader shader, string tagName, List<Object> made, StringBuilder log, out Vector3 beamL, out Vector3 beamR,
            out bool tagged)
        {
            beamL = beamR = Vector3.zero;
            tagged = false;
            var markShader = Shader.Find(MarkShaderName);
            if (markShader == null) { log.AppendLine($"WARN shader {MarkShaderName} not found — no waypoint"); return false; }

            BuildTab(plate, "Button", tabX, s, shader, 0f, WpPress, left, made);

            // where the laser hits (+Z = the surface normal) with the owner's reticle, and the mark's seat there (+Y off the surface:
            // the diamond stands off the floor, wall or ceiling — D35, was a drop ray to the floor below, D29 Q15)
            var hit = Child(root, LaserHit, new Pose(avatar.position, root.rotation));
            var markQuad = QuadMesh(made, "PulseWatch Mark Quad", new Bounds(Vector3.zero, Vector3.one * 2f));
            var ret = Visual(hit, "Reticle", Vector3.zero, Vector3.one * ReticleSize / Lossy(hit), markQuad,
                MarkMat(markShader, 3f, UnityEngine.Rendering.CompareFunction.LessEqual, made), false);
            ret.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // its XZ plane on the surface
            var seat = Child(hit, "MarkSeat", new Pose(hit.position, hit.rotation * Quaternion.Euler(90f, 0f, 0f)));

            // lasers: on the hand bone at the index tip, along the finger in the rest pose
            var laserMat = MarkMat(markShader, 0f, UnityEngine.Rendering.CompareFunction.LessEqual, made);
            var beamQuad = QuadMesh(made, "PulseWatch Laser Quad", new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1.2f)));
            foreach (bool l in new[] { true, false })
            {
                var hand = anim.GetBoneTransform(l ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var prox = Bone(anim, l ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
                var mid = Bone(anim, l ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate);
                var dist = Bone(anim, l ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal);
                Vector3 tip, dir;
                if (prox != null && mid != null && dist != null)
                {
                    tip = dist.position + (dist.position - mid.position);
                    dir = (tip - prox.position).normalized;
                }
                else
                {
                    var m = Bone(anim, l ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                    var lower = anim.GetBoneTransform(l ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    dir = (m != null ? m.position - hand.position : hand.position - lower.position).normalized;
                    tip = hand.position + dir * 0.08f * s;
                    log.AppendLine($"WARN {(l ? "L" : "R")}: index finger bones missing — the laser starts {0.08f * s:0.00} m past the hand");
                }
                var up = Mathf.Abs(Vector3.Dot(dir, avatar.up)) > 0.99f ? avatar.forward : avatar.up;
                var laser = Anchor(root, Laser + (l ? "L" : "R"), new Pose(tip, Quaternion.LookRotation(dir, up)), l ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Raycast(laser, Vector3.forward, LaserReach, hit, true, VRCRaycast.MissBehavior.SnapToEnd, WpRay, log);
                var full = new Vector3(LaserWidth, 1f, LaserReach) / Lossy(laser);
                Visual(laser, "Beam", Vector3.zero, full, beamQuad, laserMat, true);
                laser.gameObject.SetActive(false);
                if (l) beamL = full; else beamR = full;
            }

            // the mark: follows the hit point (VR) or the feet (desktop) until the FX freezes it to the world
            var feet = Child(root, WpFeet, new Pose(avatar.position, root.rotation));
            var wpT = Child(root, Waypoint, new Pose(avatar.position, root.rotation));
            var pc = wpT.gameObject.AddComponent<VRCParentConstraint>();
            pc.Sources.Add(new VRCConstraintSource(seat, 1f));      // source0 = where the laser hits (VR)
            pc.Sources.Add(new VRCConstraintSource(feet, 0f));      // source1 = the feet (desktop)
            pc.IsActive = true;
            pc.Locked = true;
            var mark = Unit(wpT, "Mark", Vector3.zero);
            var vis = Unit(mark, "Vis", Vector3.zero);
            float u = 1f / Lossy(wpT);
            // D63: the diamond alone (the floor ring left out), bobbing up and down in the shader
            var diaMat = MarkMat(markShader, 2f, UnityEngine.Rendering.CompareFunction.Always, made);
            Hover(diaMat);
            Visual(vis, "Diamond", new Vector3(0f, DiamondLift, 0f) * u, Vector3.one * DiamondSize * u, markQuad, diaMat, true);
            // name tag (D61): on the diamond's centre — the shader lifts it straight up over the diamond (a wall mark too) and turns
            // it to the viewer; its own quad with wide bounds, as it grows with the distance
            float aspect = 1f;
            var tagTex = tagName == null ? null : NameTexture(tagName, made, log, out aspect);
            if (tagTex != null)
            {
                var tagMat = MarkMat(markShader, 5f, UnityEngine.Rendering.CompareFunction.Always, made);
                tagMat.SetTexture("_MainTex", tagTex);
                tagMat.SetFloat("_TagAspect", aspect);
                tagMat.SetFloat("_TagLift", DiamondSize * 0.5f + TagGap);
                Hover(tagMat);   // with the diamond
                Visual(vis, "Tag", new Vector3(0f, DiamondLift, 0f) * u, Vector3.one * TagHeight * u,
                    QuadMesh(made, "PulseWatch Tag Quad", new Bounds(Vector3.zero, Vector3.one * 200f)), tagMat, true);
                tagged = true;
            }
            mark.gameObject.SetActive(false);

            // on the disc (D27 3): WpProbe sits on the mark inside the radar frame, so its local position is the mark's offset from
            // the eyes; WpEcho copies that local position under Face; WpDisc is a quad skinned to the disc with WpEcho as its root
            // bone — the shader reads the offset from its vertices (mode 5). The FX shows it with the mark
            var probe = Child(frame, "WpProbe", new Pose(wpT.position, frame.rotation));
            Follow(probe, wpT, true);
            var echo = Unit(face, "WpEcho", probe.localPosition);
            var epc = echo.gameObject.AddComponent<VRCPositionConstraint>();
            epc.Sources.Add(new VRCConstraintSource(probe, 1f));
            epc.SolveInLocalSpace = true;
            epc.IsActive = true;
            epc.Locked = true;
            var (pip, pipMat) = DiscOverlay(face, "WpDisc", echo, discMat, 5f, WaypointLift, made);
            if (pipMat != null)
            {
                pipMat.SetFloat("_WpSpan", face.Find("Disc").localScale.x);
                pipMat.SetFloat("_WpUnit", Lossy(frame));
            }
            pip.SetActive(false);
            log.AppendLine($"waypoint: button {ButtonW * s * 1000f:0} x {ButtonH * s * 1000f:0} mm under the plate, depth receiver r {PressRadius * s * 1000f:0} mm at {PressDepth * s * 1000f:0} mm behind (down {PressOn} / up {PressOff}, hold {HoldTime} s = clear), laser {LaserReach} m (floor, wall or ceiling), mark: diamond bobbing {HoverAmp * 100f:0} cm / {HoverPeriod} s (through walls), on the disc in the {frame.name} frame");
            return true;
        }

        static float Lossy(Transform t) => Mathf.Max(1e-4f, t.lossyScale.x);

        // flashlight (D46, docs/pulse-watch.md 10-60): a tab beside the waypoint button (same depth press, its own receivers) and a
        // Spot Light on the watch, a little off the back of the wrist, aimed where the wearer looks (D49: a VRC Rotation Constraint
        // copying EyePoint — it was along the forearm). With it a short beam from the watch along the gaze (PulseWatchMark 4), so
        // the light reads as the watch's (D50: no lens glint at its start). The FX turns it on from the local Flash only, so it lights
        // the wearer's view alone; the wearer's own body (PlayerLocal, and its mirror copy) is left out of the light so the hand in
        // front of it doesn't burn white
        static bool BuildFlash(Transform watch, Transform eye, Transform plate, Shader shader, bool left, float tabX, float s, List<Object> made, StringBuilder log)
        {
            BuildTab(plate, "LightTab", tabX, s, shader, 1f, FlashPress, left, made);
            var lt = Child(watch, "Flashlight", new Pose(watch.position + watch.up * 0.01f * s + watch.forward * 0.02f * s, eye.rotation));
            var aim = lt.gameObject.AddComponent<VRCRotationConstraint>();
            aim.Sources.Add(new VRCConstraintSource(eye, 1f));
            aim.IsActive = true;
            aim.Locked = true;
            var light = lt.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = FlashRange;
            light.spotAngle = FlashAngle;
            light.innerSpotAngle = FlashAngle * 0.5f;
            light.intensity = FlashIntensity;
            light.color = FlashColor;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.cullingMask = ~((1 << 10) | (1 << 18));   // not PlayerLocal / MirrorReflection: the wearer's own body
            var markShader = Shader.Find(MarkShaderName);
            if (markShader != null)
            {
                var beamQuad = QuadMesh(made, "PulseWatch Flash Beam Quad", new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1.2f)));
                var beam = MarkMat(markShader, 4f, UnityEngine.Rendering.CompareFunction.LessEqual, made);
                beam.SetColor("_Color", FlashColor);
                beam.SetFloat("_Gain", FlashBeamGain);
                beam.SetFloat("_Start", FlashBeamStart);
                Visual(lt, "Beam", Vector3.zero, new Vector3(FlashBeamWidth, 1f, FlashBeamLength) * s / Lossy(lt), beamQuad, beam, true);
            }
            else log.AppendLine($"WARN shader {MarkShaderName} not found — flashlight without its beam");
            lt.gameObject.SetActive(false);
            log.AppendLine($"flashlight: tab {(tabX == 0f ? "alone under the plate" : "beside the waypoint button")}, spot {FlashAngle:0} deg, {FlashRange:0} m, intensity {FlashIntensity}, from the watch along the gaze, beam {FlashBeamLength * s * 100f:0} cm, local only (a Light: Performance Poor)");
            return true;
        }

        // a tab under the plate (the waypoint button D28 B, the flashlight tab D46): a unit object (the receivers stay round), the
        // tab quad under it (_Icon 0 waypoint / 1 flashlight); the depth receivers' centres behind the face (the plate's +Y faces
        // the eyes), one per index — only the one for the other hand is on (Side clips)
        static void BuildTab(Transform plate, string name, float tabX, float s, Shader shader, float icon, string press, bool left, List<Object> made)
        {
            var tab = Unit(plate, name, new Vector3(tabX, 0f, -ButtonDown) * s);
            var m = Mat(shader, 4f, made);
            TabMat(m, s);
            if (m != null) m.SetFloat("_Icon", icon);
            Visual(tab, "Quad", Vector3.zero, new Vector3(ButtonW, 1f, ButtonH) * s, ButtonMesh(made), m, true);
            foreach (var (receiver, tag, on) in new[] { ("PressL", "FingerIndexL", !left), ("PressR", "FingerIndexR", left) })
            {
                var t = Child(tab, receiver, new Pose(tab.position - tab.up * PressDepth * s, tab.rotation));
                Receiver(t, PressRadius * s, press, tag);
                t.gameObject.SetActive(on);
            }
        }

        // frosted glass (D41): a quad just under the disc and one under the plate, both redrawing what is behind them blurred
        // (PulseWatchFrost, one named GrabPass). Children of Face / Plate, so they pop and grow with them and hide with them.
        // The tabs under the plate get one each too (D44 waypoint, D46 flashlight), under their own unit objects
        static bool BuildFrost(Transform face, Transform plate, Transform[] tabs, float s, Mesh quad, List<Object> made, StringBuilder log)
        {
            var frostShader = Shader.Find(FrostShaderName);
            if (frostShader == null) { log.AppendLine($"WARN shader {FrostShaderName} not found — no blur"); return false; }
            float plateH = PlateWidth / PlateAspect;
            // the disc's glass is culled by bounds covering both light drip boards (the disc's, DripHalfWidth across, and the plate's
            // beside it): the drip subtracts the screen this glass grabbed, so where a drip board is on screen the grab must have been
            // taken this frame — with the disc just off screen a stale grab (last frame's, another camera's) streaked the board
            float cull = 2f * (DripHalfWidth + 0.05f) / DiscSize;
            var discQuad = QuadMesh(made, "PulseWatch Frost Disc Quad", new Bounds(Vector3.zero, new Vector3(cull, 0.12f, cull)));
            var under = new List<(Transform, float, Vector3, Mesh)> { (face, 0f, new Vector3(DiscSize, 1f, DiscSize), discQuad), (plate, 1f, new Vector3(PlateWidth, 1f, plateH), quad) };
            foreach (var tab in tabs) under.Add((tab, 2f, new Vector3(ButtonW, 1f, ButtonH), quad));
            foreach (var (parent, shape, size, mesh) in under)
            {
                var m = new Material(frostShader) { name = shape == 0f ? "PulseWatch Frost Disc" : shape == 1f ? "PulseWatch Frost Plate" : "PulseWatch Frost Button" };
                m.SetFloat("_Shape", shape);
                m.SetFloat("_Aspect", shape == 2f ? ButtonW / ButtonH : PlateAspect);
                m.SetFloat("_Blur", FrostBlur * s);
                m.SetFloat("_DepthBias", DepthBias * s);
                made.Add(m);
                Visual(parent, "Frost", new Vector3(0f, -0.0005f * s, 0f), size * s, mesh, m, true);   // just under the disc / plate quad
            }
            return true;
        }

        // light drip (D41, 10-56; the plate's D43): one board on Face's origin and one on the clock plate's, both turned to the camera
        // about Face's normal by their shader (PulseWatchDrip), trailing what the hologram lights down the normal toward the forearm
        // (wrist-vertical, D41), hidden when looked at along it. The disc's: unit-scale sizes x s through its own scale, so it
        // narrows as Face closes. The plate's: four vertices skinned to the plate with Holo (Face's axes) as the root bone — the
        // shader gets Holo's matrix and the plate's origin as the vertex (_AtVertex), so its sizes carry s themselves
        static bool BuildDrip(Transform face, Transform plate, Transform[] tabs, Material plateMat, float s, List<Object> made, StringBuilder log)
        {
            var dripShader = Shader.Find(DripShaderName);
            if (dripShader == null) { log.AppendLine($"WARN shader {DripShaderName} not found — no light drip"); return false; }
            Material DripMat(string name, float halfW, float top, float k)
            {
                var m = new Material(dripShader) { name = name };
                m.SetFloat("_HalfW", halfW * k);
                m.SetFloat("_Top", top * k);
                m.SetFloat("_Len", DripLength * k);
                m.SetFloat("_Bottom", (DripLength + 0.005f) * k);
                m.SetFloat("_Col", m.GetFloat("_Col") * k);
                m.SetFloat("_DepthBias", (DepthBias + DripBias) * k);
                made.Add(m);
                return m;
            }
            // the vertex shader places the corners; the mesh's bounds cover every way the board can turn
            var board = QuadMesh(made, "PulseWatch Drip Board", new Bounds(new Vector3(0f, (DripTop - DripLength) * 0.5f, 0f),
                new Vector3(2f * DripHalfWidth, DripTop + DripLength + 0.005f, 2f * DripHalfWidth)));
            Visual(face, "Drip", Vector3.zero, Vector3.one * s, board, DripMat("PulseWatch Drip", DripHalfWidth, DripTop, 1f), true);

            var pm = DripMat("PulseWatch Drip Plate", PlateDripHalfWidth, PlateDripTop, s);
            pm.SetFloat("_AtVertex", 1f);
            pm.SetVector("_PlateHalf", new Vector4(PlateWidth * 0.5f * s, PlateWidth / PlateAspect * 0.5f * s, 0f, 0f));
            if (plateMat != null) pm.SetFloat("_BackA", plateMat.GetColor("_Back").a);
            if (tabs.Length > 0)   // D44 / D46: the row of tabs runs down too (one rectangle over them, the gap between counted in)
            {
                float lo = tabs.Min(t => t.localPosition.x) - ButtonW * 0.5f * s, hi = tabs.Max(t => t.localPosition.x) + ButtonW * 0.5f * s;
                pm.SetVector("_Button", new Vector4(ButtonDown * s, (hi - lo) * 0.5f, ButtonH * 0.5f * s, (hi + lo) * 0.5f));
            }
            var skin = QuadMesh(made, "PulseWatch Drip Plate Board", new Bounds(Vector3.zero, Vector3.one));
            skin.vertices = new Vector3[4];   // all on the plate's origin; the shader spreads them by uv
            skin.normals = Enumerable.Repeat(Vector3.up, 4).ToArray();   // the plate's normal and X, skinned into Holo's space
            skin.tangents = Enumerable.Repeat(new Vector4(1f, 0f, 0f, 1f), 4).ToArray();
            skin.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, 4).ToArray();
            skin.bindposes = new[] { Matrix4x4.identity };
            var go = new GameObject("Drip");
            go.transform.SetParent(plate, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = skin;
            smr.bones = new[] { plate };
            smr.rootBone = face.parent;   // Holo: Face's axes without its FX scale (0 when closed — no inverse to skin into)
            // in Holo's space: wherever the plate slides (D18 sides, the open pop) and however far the streaks run
            smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 0.3f * s);
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;
            smr.sharedMaterial = pm;
            return true;
        }

        // a quad over the disc, skinned to it, with another transform as its root bone: the shader gets that transform as its
        // object matrix and its vertices in that transform's space (modes 5, 6). Drawn just after the disc
        static (GameObject go, Material mat) DiscOverlay(Transform face, string name, Transform rootBone, Material discMat, float mode, float lift, List<Object> made)
        {
            var discT = face.Find("Disc");
            var skin = QuadMesh(made, $"PulseWatch {name} Quad", new Bounds(Vector3.zero, new Vector3(1f, 0.2f, 1f)));
            skin.vertices = skin.vertices.Select(p => p + Vector3.up * lift).ToArray();   // D53: its own layer over the disc (Disc's Y = m x s)
            skin.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, 4).ToArray();
            skin.bindposes = new[] { Matrix4x4.identity };   // the quad is in Disc's own space
            var go = new GameObject(name);
            go.transform.SetParent(face, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = skin;
            smr.bones = new[] { discT };
            smr.rootBone = rootBone;
            smr.updateWhenOffscreen = true;   // the root bone is away from the disc (far off for the waypoint)
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;
            Material m = null;
            if (discMat != null)
            {
                m = new Material(discMat) { name = "PulseWatch " + name };
                m.SetFloat("_Mode", mode);
                m.renderQueue = discMat.renderQueue + 1;   // over the disc, under the plate
                made.Add(m);
                smr.sharedMaterial = m;
            }
            return (go, m);
        }

        static void Raycast(Transform t, Vector3 dir, float distance, Transform result, bool rotate, VRCRaycast.MissBehavior miss, string param, StringBuilder log)
        {
            var so = new SerializedObject(t.gameObject.AddComponent<VRCRaycast>());   // fields are private serialized (portal gun)
            void Set(string path, System.Action<SerializedProperty> f)
            {
                var p = so.FindProperty(path);
                if (p == null) log.AppendLine($"WARN VRCRaycast.{path} not found"); else f(p);
            }
            Set("raycastDirection", p => p.vector3Value = dir);
            Set("distance", p => p.floatValue = distance);
            Set("applyTransformScale", p => p.boolValue = false);
            Set("collisionMode", p => p.enumValueIndex = (int)VRCRaycast.CollisionMode.HitWorlds);
            Set("resultTransform", p => p.objectReferenceValue = result);
            Set("applyRotation", p => p.boolValue = rotate);
            Set("alignmentAxis", p => p.vector3Value = Vector3.forward);
            Set("behaviorOnMiss", p => p.enumValueIndex = (int)miss);
            Set("parameter", p => p.stringValue = param);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // the name on the waypoint's tag (D61): typed in the settings, else the VRChat SDK's signed-in display name; null = no tag
        static string MarkName(PulseWatch pw, StringBuilder log)
        {
            if (!pw.waypoint || !pw.waypointName) return null;
            string typed = (pw.waypointNameText ?? "").Trim();
            bool cached = false;
            string name = typed.Length > 0 ? typed : SdkUserName(out cached);
            if (string.IsNullOrEmpty(name)) { log.AppendLine("name tag: no name (none typed, the VRChat SDK not signed in yet) — left out"); return null; }
            if (name.Length > TagMaxChars) name = name.Substring(0, TagMaxChars);
            log.AppendLine($"name tag: \"{name}\" ({(typed.Length > 0 ? "typed" : cached ? "SDK account, last seen" : "SDK account")})");
            return name;
        }

        // the display name of the account signed in to the VRChat SDK (VRC.Core.APIUser.CurrentUser), by reflection so this
        // assembly needs no reference to VRC.Core. Remembered in EditorPrefs: entering Play mode signs the SDK out for a moment, so
        // a Play-mode build takes the last name seen (cached = true); "" when it was never signed in on this PC
        internal static string SdkUserName(out bool cached)
        {
            const string Key = "Pyrite9.PulseWatch.SdkName";
            cached = false;
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("VRC.Core.APIUser", false);
                if (t == null) continue;
                var user = t.GetProperty("CurrentUser", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                string live = user == null ? "" : (t.GetProperty("displayName")?.GetValue(user) as string ?? "");
                if (live.Length > 0) { EditorPrefs.SetString(Key, live); return live; }
                break;
            }
            cached = true;
            return EditorPrefs.GetString(Key, "");
        }

        // the name drawn with the OS fonts (TagFonts) into a TagPixels-tall coverage texture laid out as the whole tab: the shader
        // draws the tab, its edge and bar around it (mode 5). aspect = width / height
        static Texture2D NameTexture(string name, List<Object> made, StringBuilder log, out float aspect)
        {
            aspect = 1f;
            const int H = TagPixels;
            int size = Mathf.RoundToInt(H * 0.6f);
            var font = Font.CreateDynamicFontFromOSFont(TagFonts, size);
            font.RequestCharactersInTexture(name, size);
            var chars = new List<(CharacterInfo ci, char c)>();
            float pen = 0f, lo = float.MaxValue, hi = float.MinValue;
            var missing = new StringBuilder();
            foreach (char ch in name)
            {
                if (!font.GetCharacterInfo(ch, out var ci, size)) { missing.Append(ch); continue; }
                chars.Add((ci, ch));
                pen += ci.advance;
                if (ci.maxY > ci.minY) { lo = Mathf.Min(lo, ci.minY); hi = Mathf.Max(hi, ci.maxY); }
            }
            if (missing.Length > 0) log.AppendLine($"WARN name tag: no font has \"{missing}\" — left out of the tag");
            if (chars.Count == 0 || pen <= 0f) { Object.DestroyImmediate(font); log.AppendLine("name tag: nothing to draw — left out"); return null; }
            float left = H * 0.25f, right = H * 0.2f;
            int W = Mathf.CeilToInt(left + pen + right);
            float baseY = (H - (hi + lo)) * 0.5f;   // the ink centred

            var rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            font.material.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, W, 0, H);
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);
            float x = left;
            foreach (var (ci, _) in chars)
            {
                float x0 = x + ci.minX, x1 = x + ci.maxX, y0 = baseY + ci.minY, y1 = baseY + ci.maxY;
                GL.TexCoord(ci.uvBottomLeft); GL.Vertex3(x0, y0, 0f);
                GL.TexCoord(ci.uvTopLeft); GL.Vertex3(x0, y1, 0f);
                GL.TexCoord(ci.uvTopRight); GL.Vertex3(x1, y1, 0f);
                GL.TexCoord(ci.uvBottomRight); GL.Vertex3(x1, y0, 0f);
                x += ci.advance;
            }
            GL.End();
            GL.PopMatrix();
            var read = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
            read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // coverage = red (white text over black, so the text shader's blend leaves the edges' coverage in the colour)
            var px = read.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i].a = px[i].r;
            Object.DestroyImmediate(read);
            var tex = new Texture2D(W, H, TextureFormat.Alpha8, true, true) { name = "PulseWatch Name Tag", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            // VRChat wants mip streaming on every mipmapped texture (NDMF's check reads this field) — a generated texture has no
            // importer, so set it on the texture itself
            var so = new SerializedObject(tex);
            so.FindProperty("m_StreamingMipmaps").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(font);
            made.Add(tex);
            aspect = (float)W / H;
            return tex;
        }

        // D63: the mark's bob (PulseWatchMark modes 2 and 5)
        // D64: the diamond and its tag grow alike past TagNear (or neither) — set on both
        static void Hover(Material m) { m.SetFloat("_Hover", HoverAmp); m.SetFloat("_HoverPeriod", HoverPeriod); m.SetFloat("_TagNear", TagNear); m.SetFloat("_Grow", MarkGrow ? 1f : 0f); }

        static Material MarkMat(Shader shader, float mode, UnityEngine.Rendering.CompareFunction zTest, List<Object> made)
        {
            var m = new Material(shader) { name = $"PulseWatch Mark {(mode == 0f ? "Laser" : mode == 2f ? "Diamond" : mode == 3f ? "Reticle" : mode == 5f ? "Name Tag" : "Flash Beam")}" };
            m.SetFloat("_Mode", mode);
            m.SetColor("_Color", wpColor);
            m.SetFloat("_ZTest", (float)zTest);
            made.Add(m);
            return m;
        }

        static void Follow(Transform t, Transform target, bool y)
        {
            var pc = t.gameObject.AddComponent<VRCPositionConstraint>();
            pc.Sources.Add(new VRCConstraintSource(target, 1f));
            if (!y)
            {
                var so = new SerializedObject(pc);
                so.FindProperty("AffectsPositionY").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            pc.IsActive = true;
            pc.Locked = true;
        }

        // unit sphere (radius 0.5), only the directions matter — the shader sets the distance
        static Mesh SphereMesh(Bounds bounds, List<Object> made)
        {
            const int lon = 48, lat = 24;
            var v = new List<Vector3>();
            for (int j = 0; j <= lat; j++)
            {
                float th = Mathf.PI * j / lat;
                for (int i = 0; i <= lon; i++)
                {
                    float ph = 2f * Mathf.PI * i / lon;
                    v.Add(0.5f * new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph)));
                }
            }
            var tri = new List<int>();
            for (int j = 0; j < lat; j++)
                for (int i = 0; i < lon; i++)
                {
                    int a = j * (lon + 1) + i, b = a + lon + 1;
                    tri.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var m = new Mesh { name = "PulseWatch Scan Sphere" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.bounds = bounds;
            made.Add(m);
            return m;
        }

        // normal, loose (+10), strict (-10)
        static float[] Angles(float normal) => new[] { normal, Mathf.Min(normal + SensStep, MaxAngle - 1f), Mathf.Max(normal - SensStep, 5f) };
        static Vector4 Prox(float[] a) => new Vector4(ProxAt(a[0]), ProxAt(a[1]), ProxAt(a[2]), 0f);
        static string Fmt(float[] a) => string.Join(" / ", a.Select(x => $"{x:0}°={ProxAt(x):0.000}"));

        static Transform Bone(Animator anim, HumanBodyBones b) => anim.GetBoneTransform(b);

        static Transform Anchor(Transform root, string name, Pose world, HumanBodyBones bone)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.SetPositionAndRotation(world.position, world.rotation);
            var bp = t.gameObject.AddComponent<ModularAvatarBoneProxy>();
            bp.boneReference = bone;
            bp.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
            return t;
        }

        static Transform Child(Transform parent, string name, Pose world)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(world.position, world.rotation);
            return t;
        }

        // a child at the parent's origin whose +Z points at target (rest pose already aimed, so the constraint has no jump)
        static Transform AimAt(Transform parent, string name, Transform target, StringBuilder log)
        {
            var dir = target.position - parent.position;
            var rot = dir.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(dir.normalized, Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up) : parent.rotation;
            var t = Child(parent, name, new Pose(parent.position, rot));
            Aim(t, target, Vector3.forward, Vector3.up, null, log);
            return t;
        }

        // a VRC Aim Constraint on t: aimAxis at target, upAxis along upObject's own upAxis (ObjectRotationUp), or the scene up
        // when upObject is null
        static void Aim(Transform t, Transform target, Vector3 aimAxis, Vector3 upAxis, Transform upObject, StringBuilder log)
        {
            var c = t.gameObject.AddComponent<VRCAimConstraint>();
            c.Sources.Add(new VRCConstraintSource(target, 1f));
            var so = new SerializedObject(c);
            so.FindProperty("AimAxis").vector3Value = aimAxis;
            so.FindProperty("UpAxis").vector3Value = upAxis;
            if (upObject == null) SetEnum(so, "WorldUp", "SceneUp", log);
            else
            {
                SetEnum(so, "WorldUp", "ObjectRotationUp", log);
                so.FindProperty("WorldUpVector").vector3Value = upAxis;
                var upT = so.FindProperty("WorldUpTransform");
                if (upT != null) upT.objectReferenceValue = upObject;
                else log.AppendLine($"WARN VRCAimConstraint.WorldUpTransform not found — {t.name} will not turn with {upObject.name}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            c.IsActive = true;
            c.Locked = true;
        }

        static void SetEnum(SerializedObject so, string path, string value, StringBuilder log)
        {
            var p = so.FindProperty(path);
            int i = p != null ? System.Array.IndexOf(p.enumNames, value) : -1;
            if (i < 0) { log.AppendLine($"WARN {so.targetObject.GetType().Name}.{path} = {value} not available"); return; }
            p.enumValueIndex = i;
        }

        static void Receiver(Transform t, float worldRadius, string param, string tag)
        {
            var c = t.gameObject.AddComponent<VRCContactReceiver>();
            c.shapeType = ContactBase.ShapeType.Sphere;
            c.radius = worldRadius / Mathf.Max(1e-4f, t.lossyScale.x);
            c.parameter = param;
            c.receiverType = ContactReceiver.ReceiverType.Proximity;
            c.allowSelf = true;
            c.allowOthers = false;
            c.localOnly = true;
            c.collisionTags = new List<string> { tag };
        }

        static void Sender(Transform t, string tag)
        {
            var c = t.gameObject.AddComponent<VRCContactSender>();
            c.shapeType = ContactBase.ShapeType.Sphere;
            c.radius = 0.001f / Mathf.Max(1e-4f, t.lossyScale.x);   // a point (1 mm)
            c.localOnly = true;
            c.collisionTags = new List<string> { tag };
        }

        static Mesh QuadMesh(List<Object> made) => QuadMesh(made, "PulseWatch Quad", new Bounds(Vector3.zero, new Vector3(1f, 0.2f, 1f)));

        // the radar disc in layers (D42): one quad per layer at DiscLayers[k] up the normal (the Disc object's Y scale is s, so
        // these are unit-scale metres), uv1.x = the layer — emitted from the lowest up, so they blend back to front seen from above
        static Mesh LayeredDisc(List<Object> made)
        {
            var order = Enumerable.Range(0, DiscLayers.Length).OrderBy(k => DiscLayers[k]).ToArray();
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var layer = new List<Vector4>(); var tri = new List<int>();
            foreach (int k in order)
            {
                int b = v.Count;
                float y = DiscLayers[k];
                v.AddRange(new[] { new Vector3(-0.5f, y, -0.5f), new Vector3(0.5f, y, -0.5f), new Vector3(-0.5f, y, 0.5f), new Vector3(0.5f, y, 0.5f) });
                uv.AddRange(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
                layer.AddRange(Enumerable.Repeat(new Vector4(k, 0f, 0f, 0f), 4));
                tri.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            var m = new Mesh { name = "PulseWatch Disc Layers" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetUVs(1, layer); m.SetTriangles(tri, 0);
            m.normals = Enumerable.Repeat(Vector3.up, v.Count).ToArray();
            m.bounds = new Bounds(new Vector3(0f, DiscLayers.Max() * 0.5f, 0f), new Vector3(1f, 0.2f, 1f));
            made.Add(m);
            return m;
        }

        // the tabs under the plate, thick (D52): a top face at ButtonThick (the unit quad's uv, uv1 (0, 1)) over side walls along
        // the shader's cut-corner outline (uv = the outline point, uv1 (1, 0 at the base / 1 at the top)). Walls first, so the
        // top draws over them. Local units: X / Z the unit quad (scaled by the tab size), Y metres x s (the Y scale)
        static Mesh ButtonMesh(List<Object> made)
        {
            float a = ButtonW / ButtonH, t = ButtonThick;
            var outline = new[] { new Vector2(ButtonCut / a, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f - ButtonCut),
                new Vector2((a - ButtonCut) / a, 1f), new Vector2(0f, 1f), new Vector2(0f, ButtonCut) };
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var d1 = new List<Vector4>();
            var tri = new List<int>();
            void Vert(Vector2 q, float y, Vector4 d) { v.Add(new Vector3(q.x - 0.5f, y, q.y - 0.5f)); uv.Add(q); d1.Add(d); }
            for (int i = 0; i < outline.Length; i++)
            {
                var q0 = outline[i];
                var q1 = outline[(i + 1) % outline.Length];
                int b = v.Count;
                Vert(q0, 0f, new Vector4(1f, 0f)); Vert(q1, 0f, new Vector4(1f, 0f));
                Vert(q0, t, new Vector4(1f, 1f)); Vert(q1, t, new Vector4(1f, 1f));
                tri.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            int top = v.Count;
            foreach (var q in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
                Vert(q, t, new Vector4(0f, 1f));
            tri.AddRange(new[] { top, top + 2, top + 1, top + 1, top + 2, top + 3 });
            var m = new Mesh { name = "PulseWatch Button" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetUVs(1, d1);
            m.normals = Enumerable.Repeat(Vector3.up, v.Count).ToArray();
            m.SetTriangles(tri, 0);
            m.bounds = new Bounds(new Vector3(0f, t * 0.5f, 0f), new Vector3(1.2f, t * 2f + 0.01f, 1.2f));
            made.Add(m);
            return m;
        }

        // the tabs' material settings shared by the waypoint button and the flashlight tab (D52: the receiver's geometry for the
        // sinking top, in the tab's local Y units = metres x s)
        static void TabMat(Material m, float s)
        {
            if (m == null) return;
            m.SetFloat("_Aspect", ButtonW / ButtonH);
            m.SetFloat("_PressOn", PressOn);
            m.SetFloat("_DepthBias", DepthBias * s);
            m.SetFloat("_PressR", PressRadius);
            m.SetFloat("_PressD", PressDepth);
        }

        static Mesh QuadMesh(List<Object> made, string name, Bounds bounds)
        {
            var m = new Mesh { name = name };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.normals = Enumerable.Repeat(Vector3.up, 4).ToArray();
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.bounds = bounds;
            made.Add(m);
            return m;
        }

        static Material Mat(Shader shader, float mode, List<Object> made)
        {
            if (shader == null) return null;
            // queue: disc, beams, plate, meter — the plate draws over the disc
            var m = new Material(shader) { name = $"PulseWatch {(mode == 0f ? "Plate" : mode == 1f ? "Disc" : mode == 2f ? "Debug" : mode == 3f ? "Beam" : "Button")}" };
            m.SetFloat("_Mode", mode);
            m.SetColor("_Color", accent);
            m.SetColor("_WpColor", wpColor);   // the waypoint button and the waypoint on the disc
            m.renderQueue = 3010 + (mode == 1f ? 0 : mode == 3f ? 1 : mode == 0f || mode == 4f ? 2 : 3);
            made.Add(m);
            return m;
        }

        static Transform Unit(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        static GameObject Visual(Transform parent, string name, Vector3 local, Vector3 scale, Mesh mesh, Material mat, bool active)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(active);
            return go;
        }

        static ParameterConfig Param(string name, ParameterSyncType type, float def, bool localOnly = false) => new ParameterConfig
        {
            nameOrPrefix = name,
            syncType = type,
            defaultValue = def,
            hasExplicitDefaultValue = true,
            saved = false,
            localOnly = localOnly,
        };

        // Expression Menu (D69), labels in the menu language (PulseWatchMenuLanguage: English / Japanese / Korean), every control
        // with an icon (Textures/MenuIcons, Source/menu_icons.py):
        //   Pulse Watch > Keep open · Waypoint > (Mark · Clear · Show to others) · Flashlight · Hide from others
        //               · Settings > (Sensitivity loose · strict · Wear on right wrist · Wear on inside of wrist · Debug meter)
        // — what is used while playing on the first page, what is set once under Settings; each page well inside VRChat's 8
        static void BuildMenu(Transform root, int lang, bool wp, bool flash, bool debug, StringBuilder log)
        {
            string T(string en, string jp, string ko) => lang == 1 ? jp : lang == 2 ? ko : en;
            string iconDir = AssetDatabase.GUIDToAssetPath(MenuIconsGuid);
            int missing = 0;
            Texture2D Icon(string name)
            {
                var t = string.IsNullOrEmpty(iconDir) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(iconDir + "/" + name + ".png");
                if (t == null) missing++;
                return t;
            }
            Transform Sub(string label, string icon, Transform under)
            {
                var go = new GameObject(label);
                if (under != null) go.transform.SetParent(under, false);
                var mi = go.AddComponent<ModularAvatarMenuItem>();
                mi.Control = new VRCExpressionsMenu.Control { name = label, icon = Icon(icon), type = VRCExpressionsMenu.Control.ControlType.SubMenu };
                mi.MenuSource = SubmenuSource.Children;
                return go.transform;
            }
            var menu = Sub("Pulse Watch", "watch", null);
            menu.SetParent(root, false);
            menu.gameObject.AddComponent<ModularAvatarMenuInstaller>();

            void Item(string label, string icon, string param, float value, bool saved, bool synced = false, bool button = false, Transform under = null)
            {
                var go = new GameObject(label);
                go.transform.SetParent(under != null ? under : menu, false);
                var mi = go.AddComponent<ModularAvatarMenuItem>();
                mi.Control = new VRCExpressionsMenu.Control
                {
                    name = label,
                    icon = Icon(icon),
                    type = button ? VRCExpressionsMenu.Control.ControlType.Button : VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = param },
                    value = value,
                };
                mi.isSynced = synced;
                mi.isSaved = saved;
            }
            Item(T("Keep open", "開いたままにする", "계속 열기"), "keep_open", Manual, 1f, false);
            if (wp)
            {
                var w = Sub(T("Waypoint", "ウェイポイント", "웨이포인트"), "waypoint", menu);
                Item(T("Mark", "置く", "찍기"), "waypoint_mark", WpTap, 1f, false, button: true, under: w);   // desktop: at the feet; VR: laser on (D29 Q19)
                Item(T("Clear", "消す", "지우기"), "waypoint_clear", WpClear, 1f, false, button: true, under: w);
                Item(T("Show to others", "他の人に見せる", "남에게 보이기"), "waypoint_show", WpShow, 1f, true, true, under: w);
            }
            if (flash) Item(T("Flashlight", "ライト", "손전등"), "flashlight", Flash, 1f, false);   // D46: desktop, or without the tab
            Item(T("Hide from others", "他の人に隠す", "남에게 숨기기"), "hide", Hide, 1f, true, true);
            var set = Sub(T("Settings", "設定", "설정"), "settings", menu);
            Item(T("Sensitivity: loose", "感度：広く", "감도: 넓게"), "sens_loose", Sens, 1f, true, under: set);
            Item(T("Sensitivity: strict", "感度：狭く", "감도: 좁게"), "sens_strict", Sens, 2f, true, under: set);
            Item(T("Wear on right wrist", "右手に着ける", "오른손에 차기"), "right_wrist", Right, 1f, true, true, under: set);
            Item(T("Wear on inside of wrist", "手首の内側に着ける", "손목 안쪽에 차기"), "inside", Inside, 1f, true, true, under: set);   // D34
            if (debug) Item(T("Debug meter", "デバッグメーター", "디버그 막대"), "debug", DebugOn, 1f, true, under: set);
            if (missing > 0) log.AppendLine($"WARN {missing} menu icons missing (Textures/MenuIcons) — those controls have none");
        }
    }
}
#endif
