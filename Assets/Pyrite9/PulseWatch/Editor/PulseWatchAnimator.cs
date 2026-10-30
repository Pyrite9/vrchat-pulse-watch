// Pyrite9 Pulse Watch — FX controller (built by PulseWatchPlugin, merged with MA Merge Animator; Write Defaults off).
//   Detect   (owner only): Closed -> Arming (both proximities past the open marks for `dwell` s) -> Open, back to Closed when
//            either drops below its close mark (hysteresis). Marks per sensitivity (Sens 0 normal / 1 loose / 2 strict).
//            Drives the synced Open bool; the Manual toggle keeps it open (desktop). Remote copies sit in Remote.
//   Display  (everyone): Open -> the disc opens from its centre, then the plate slides out from it to its corner (D47),
//            not Open -> plate folds down to the wrist (its height first), disc shrinks (FoldTime), both hide.
//            Hide (synced) keeps others' copies hidden; the wearer's own copy ignores it.
//   Voice    : built-in Voice, smoothed to VoiceS (D58) -> the disc's _Voice (outer ring glows while the wearer talks, D20);
//            MuteSelf -> Muted: _Muted (ring red, mic at 6 o'clock — D27)
//   Session  : the wearer's copy counts the plate's _SessionSec up from the avatar's load (_SesOn, D27 1) — others' copies would
//            count from when they loaded it
//   Side     (everyone): Right -> the watch's Parent Constraint on the right Seat and the plate (+ beams) mirrored to the lower
//            left; else the left Seat, plate lower right (D18)
//   DebugVis (owner only, Debug toggle): shows the meter
//   Values   (D70, everyone): one Direct blend tree for every "a value straight onto a material / transform" — the meter's
//            proximities, the radar's 120 slice params (60 cells, D36, weighted by Local: the receivers are local only, so
//            others' copies skip them), the wearer's FPS readout (_FpsOn, D25) and occupied-cell count tab (_CountOn, D22) by
//            Local, the tabs' press depth, the laser beam's length
//   Scan     (owner only): Idle -> Wave on Open (ScanTime: scan objects on, _Radius 0 -> reach, the radar's _ScanR with it)
//            -> Cooldown (to ScanCooldown from the start) -> Wait (until closed) -> Idle
//   Waypoint (D26-D30): WpButton (owner) — depth press under the plate: tap = laser on / off, hold = clear; WpPress -> the
//            tab's _Press; WpLaser (owner) — the other hand's trigger while the laser hits = mark (WpOn, flip WpSeq), timeout,
//            menu (WpTap: VR laser on / desktop mark at the feet, WpClear); WpLaserVis (everyone) — the laser on the free hand
//            while WpLaser, beam to the wearer and with WpShow to others (its length from the raycast's _Ratio in Values);
//            WpMark (everyone) — on each WpSeq flip the mark follows its source briefly and freezes to the world, the disc's
//            WpDisc shown with it (D27); WpMarkVis — the mark's Vis and the disc's WpDisc renderer for the wearer, and for others
//            with WpShow (10-95)
//   Flash    (D46): FlashButton (owner) — the flashlight tab beside the waypoint button, a press flips Flash; Flash (owner) —
//            the Spot Light and the tab's lit look
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDKBase;
using static Pyrite9.PulseWatch.PulseWatchConst;

namespace Pyrite9.PulseWatch
{
    static class PulseWatchAnimator
    {
        public static AnimatorController Build(float[] faceOpen, float[] faceClose, float[] gazeOpen, float[] gazeClose, float dwell, bool radar, bool scan,
            Vector3 plateSpot, bool waypoint, bool tag, Vector3 beamL, Vector3 beamR, bool flash, bool frost, bool compass, bool voice, bool fps, bool session, bool debug, List<Object> made)
        {
            var c = new AnimatorController { name = "Pyrite9 PulseWatch FX" };
            made.Add(c);
            c.AddParameter(IsLocal, AnimatorControllerParameterType.Bool);
            c.AddParameter(Open, AnimatorControllerParameterType.Bool);
            c.AddParameter(Manual, AnimatorControllerParameterType.Bool);
            c.AddParameter(Sens, AnimatorControllerParameterType.Int);
            if (debug) c.AddParameter(DebugOn, AnimatorControllerParameterType.Bool);
            c.AddParameter(Right, AnimatorControllerParameterType.Bool);
            c.AddParameter(Inside, AnimatorControllerParameterType.Bool);
            c.AddParameter(Hide, AnimatorControllerParameterType.Bool);
            if (voice)
            {
                c.AddParameter(Voice, AnimatorControllerParameterType.Float);
                c.AddParameter(MuteSelf, AnimatorControllerParameterType.Bool);
                c.AddParameter(VoiceS, AnimatorControllerParameterType.Float);
                foreach (var (p, v) in new[] { (VoiceRiseK, VoiceRise), (VoiceRiseK1, 1f - VoiceRise), (VoiceFallK, VoiceFall), (VoiceFallK1, 1f - VoiceFall) })
                    c.AddParameter(new AnimatorControllerParameter { name = p, type = AnimatorControllerParameterType.Float, defaultFloat = v });
            }
            c.AddParameter(FaceProx, AnimatorControllerParameterType.Float);
            c.AddParameter(GazeProx, AnimatorControllerParameterType.Float);
            c.AddParameter(new AnimatorControllerParameter { name = One, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            c.AddParameter(Local, AnimatorControllerParameterType.Float);
            if (radar)
                foreach (var (k, b, i) in AllSlices()) c.AddParameter(Cell(k, b, i), AnimatorControllerParameterType.Float);

            if (waypoint) WaypointParams(c);
            if (flash)
            {
                c.AddParameter(Flash, AnimatorControllerParameterType.Bool);
                c.AddParameter(FlashPress, AnimatorControllerParameterType.Float);
                if (!waypoint)   // D67: the point gesture (WaypointParams adds them otherwise)
                {
                    c.AddParameter(GestureLeft, AnimatorControllerParameterType.Int);
                    c.AddParameter(GestureRight, AnimatorControllerParameterType.Int);
                }
            }

            // D70: the value layers' trees, gathered into one Direct layer (Values) at the end
            var values = new List<(Motion motion, string weight)>();
            DetectLayer(c, faceOpen, faceClose, gazeOpen, gazeClose, dwell, debug, made);
            DisplayLayer(c, waypoint, frost, compass, made);
            SideLayer(c, plateSpot, waypoint, flash, made);
            if (debug) DebugLayers(c, values, made);
            if (voice) VoiceLayer(c, made);
            if (fps) values.Add((Drive(Local, PlateQuad, "_FpsOn", 1f, made), One));
            if (session) SessionLayer(c, made);
            if (radar) RadarValues(values, made);
            if (scan) ScanLayer(c, radar, made);
            if (waypoint)
            {
                ButtonLayers(c, flash, tag, values, made);
                LaserLayer(c, made);
                LaserVisLayers(c, beamL, beamR, values, made);
                MarkLayers(c, made);
            }
            if (flash) FlashLayers(c, waypoint, values, made);
            if (values.Count > 0) State(Layer(c, "PulseWatch Values", made), "Values", Direct("PW Values", made, values.ToArray()), made);
            return c;
        }

        static void DetectLayer(AnimatorController c, float[] faceOpen, float[] faceClose, float[] gazeOpen, float[] gazeClose, float dwell, bool debug, List<Object> made)
        {
            var sm = Layer(c, "PulseWatch Detect", made);
            Key[] D(float v) => debug ? new[] { Dbg("_DbgState", v) } : new Key[0];   // the debug meter's state colour, when it is built
            var remote = State(sm, "Remote", Clip("PW Idle", made), made);
            var closed = State(sm, "Closed", Clip("PW Dbg Closed", made, D(0f)), made);
            var arming = State(sm, "Arming", Clip("PW Dbg Arming", made, Mathf.Max(dwell, 1f / 60f), D(0.5f)), made);
            var open = State(sm, "Open", Clip("PW Dbg Open", made, D(1f)), made);
            sm.defaultState = remote;
            Driver(closed, made, Set(Open, 0f), Set(Local, 1f));   // D70: Local for the Values layer (Remote -> Closed is the wearer's copy)
            Driver(open, made, Set(Open, 1f));

            To(remote, closed, made).AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
            To(closed, open, made).AddCondition(AnimatorConditionMode.If, 0f, Manual);
            To(arming, open, made).AddCondition(AnimatorConditionMode.If, 0f, Manual);
            for (int k = 0; k < 3; k++)
            {
                float fo = ProxAt(faceOpen[k]), fc = ProxAt(faceClose[k]), go = ProxAt(gazeOpen[k]), gc = ProxAt(gazeClose[k]);
                var t = To(closed, arming, made);
                t.AddCondition(AnimatorConditionMode.Equals, k, Sens);
                t.AddCondition(AnimatorConditionMode.Greater, fo, FaceProx);
                t.AddCondition(AnimatorConditionMode.Greater, go, GazeProx);
                // while arming the open marks still apply — falling back below them cancels
                var fail1 = To(arming, closed, made);
                fail1.AddCondition(AnimatorConditionMode.Equals, k, Sens);
                fail1.AddCondition(AnimatorConditionMode.Less, fo, FaceProx);
                var fail2 = To(arming, closed, made);
                fail2.AddCondition(AnimatorConditionMode.Equals, k, Sens);
                fail2.AddCondition(AnimatorConditionMode.Less, go, GazeProx);
                foreach (var (p, mark) in new[] { (FaceProx, fc), (GazeProx, gc) })
                {
                    var shut = To(open, closed, made);
                    shut.AddCondition(AnimatorConditionMode.IfNot, 0f, Manual);
                    shut.AddCondition(AnimatorConditionMode.Equals, k, Sens);
                    shut.AddCondition(AnimatorConditionMode.Less, mark, p);
                }
            }
            // a Sens outside 0-2 (old saved value) behaves as normal
            var oddS = To(closed, arming, made);
            oddS.AddCondition(AnimatorConditionMode.Greater, 2, Sens);
            oddS.AddCondition(AnimatorConditionMode.Greater, ProxAt(faceOpen[0]), FaceProx);
            oddS.AddCondition(AnimatorConditionMode.Greater, ProxAt(gazeOpen[0]), GazeProx);
            var armed = To(arming, open, made);   // last: only after the dwell with nothing failing
            armed.hasExitTime = true;
            armed.exitTime = 1f;
        }

        // Opening (D47, B): the disc opens from its centre with a warm ring (_Reveal on the disc, its glass and overlays, RevealTime,
        // easing out); from PlateOutAt the plate slides out from the disc's centre side to its spot, growing from nothing
        // (PlateOutTime) — its Position Constraint's source1 (the disc centre) weight falls from PlateTuck's to 0. Closing: the
        // fold as before (D43), every value the opening moves written back to rest
        static void DisplayLayer(AnimatorController c, bool waypoint, bool frost, bool compass, List<Object> made)
        {
            var reveal = new List<(string path, System.Type type)> { (Radar, typeof(MeshRenderer)) };
            if (frost) reveal.Add((Face + "/Frost", typeof(MeshRenderer)));
            if (compass) reveal.Add((PulseWatchConst.Compass, typeof(SkinnedMeshRenderer)));
            if (waypoint) reveal.Add((WpDisc, typeof(SkinnedMeshRenderer)));
            var tuckW = PlateTuck / (1f - PlateTuck);   // weights blend as w / (1 + w) toward the disc centre
            var plateW = Bind(Plate, typeof(VRCPositionConstraint), "Sources.source1.Weight");
            var sm = Layer(c, "PulseWatch Display", made);
            var hidden = State(sm, "Hidden", Clip("PW Panel Hidden", made,
                Curve(Panel, typeof(GameObject), "m_IsActive", 0f), Curve(Face, typeof(GameObject), "m_IsActive", 0f)), made);
            var open = new AnimationClip { name = "PW Panel Open" };
            made.Add(open);
            float end = PlateOutAt + PlateOutTime;
            foreach (var (path, go) in new[] { (Panel, true), (Face, true) })
            {
                AnimationUtility.SetEditorCurve(open, Bind(path, typeof(GameObject), "m_IsActive"), AnimationCurve.Constant(0f, end, 1f));
                foreach (var ax in new[] { "x", "y", "z" })
                    AnimationUtility.SetEditorCurve(open, Bind(path, typeof(Transform), "m_LocalScale." + ax), AnimationCurve.Constant(0f, end, 1f));
            }
            var r = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.2f * RevealEnd / RevealTime), new Keyframe(RevealTime, RevealEnd, 0f, 0f), new Keyframe(end, RevealEnd, 0f, 0f));
            foreach (var (path, type) in reveal)
                AnimationUtility.SetEditorCurve(open, Bind(path, type, "material._Reveal"), r);
            // the plate: nothing and tucked until PlateOutAt, then out (easing out)
            AnimationCurve Out(float from, float to) => new AnimationCurve(new Keyframe(0f, from, 0f, 0f), new Keyframe(PlateOutAt, from, 0f, 2f * (to - from) / PlateOutTime),
                new Keyframe(end, to, 0f, 0f));
            AnimationUtility.SetEditorCurve(open, Bind(Plate, typeof(Transform), "m_LocalScale.x"), Out(0f, 1f));
            AnimationUtility.SetEditorCurve(open, Bind(Plate, typeof(Transform), "m_LocalScale.y"), AnimationCurve.Constant(0f, end, 1f));
            AnimationUtility.SetEditorCurve(open, Bind(Plate, typeof(Transform), "m_LocalScale.z"), Out(0f, 1f));
            AnimationUtility.SetEditorCurve(open, plateW, Out(tuckW, 0f));
            var opening = State(sm, "Opening", open, made);
            var fold = Ramp("PW Panel Fold", FoldTime, made,
                (Bind(Panel, typeof(GameObject), "m_IsActive"), 1f, 1f),
                (Bind(Panel, typeof(Transform), "m_LocalScale.x"), 1f, 1f),
                (Bind(Panel, typeof(Transform), "m_LocalScale.y"), 1f, 1f),
                (Bind(Panel, typeof(Transform), "m_LocalScale.z"), 1f, 0f),
                (Bind(Face, typeof(GameObject), "m_IsActive"), 1f, 1f),
                (Bind(Face, typeof(Transform), "m_LocalScale.x"), 1f, 0f),
                (Bind(Face, typeof(Transform), "m_LocalScale.y"), 1f, 1f),
                (Bind(Face, typeof(Transform), "m_LocalScale.z"), 1f, 0f));
            foreach (var (path, type) in reveal)
                AnimationUtility.SetEditorCurve(fold, Bind(path, type, "material._Reveal"), AnimationCurve.Constant(0f, FoldTime, RevealEnd));
            foreach (var ax in new[] { "x", "y", "z" })
                AnimationUtility.SetEditorCurve(fold, Bind(Plate, typeof(Transform), "m_LocalScale." + ax), AnimationCurve.Constant(0f, FoldTime, 1f));
            AnimationUtility.SetEditorCurve(fold, plateW, AnimationCurve.Constant(0f, FoldTime, 0f));
            var closing = State(sm, "Closing", fold, made);
            sm.defaultState = hidden;
            // shown = Open and (the wearer or not Hide); transitions are AND-only, so each OR gets two
            foreach (var from in new[] { hidden, closing })
            {
                var own = To(from, opening, made);
                own.AddCondition(AnimatorConditionMode.If, 0f, Open);
                own.AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
                var shown = To(from, opening, made);
                shown.AddCondition(AnimatorConditionMode.If, 0f, Open);
                shown.AddCondition(AnimatorConditionMode.IfNot, 0f, Hide);
            }
            To(opening, closing, made).AddCondition(AnimatorConditionMode.IfNot, 0f, Open);
            var hide = To(opening, closing, made);
            hide.AddCondition(AnimatorConditionMode.If, 0f, Hide);
            hide.AddCondition(AnimatorConditionMode.IfNot, 0f, IsLocal);
            var done = To(closing, hidden, made);
            done.hasExitTime = true;
            done.exitTime = 1f;
        }

        // plateSpot = the plate spot's local position on the left wrist (toward the hand = +X there); the right wrist mirrors X.
        // The plate and its beams follow the spot (D19). All three components are written: a component left out of a
        // Transform curve is set to 0 in game (v6-v8: the plate sat on the disc's right end, level with the face) — editor
        // Play never runs the FX, so only an in-game test or SampleAnimation shows it
        static void SideLayer(AnimatorController c, Vector3 plateSpot, bool waypoint, bool flash, List<Object> made)
        {
            // the watch's Parent Constraint: source0 left back, 1 right back, 2 left inside, 3 right inside (D34). The plate spot
            // and the button stay as on the back: they are laid out in Holo, which turns to the eyes either way
            Key[] Keys(bool right, bool inside)
            {
                float x = right ? -plateSpot.x : plateSpot.x;
                var k = new List<Key>
                {
                    Curve(Watch, typeof(VRCParentConstraint), "Sources.source0.Weight", !right && !inside ? 1f : 0f),
                    Curve(Watch, typeof(VRCParentConstraint), "Sources.source1.Weight", right && !inside ? 1f : 0f),
                    Curve(Watch, typeof(VRCParentConstraint), "Sources.source2.Weight", !right && inside ? 1f : 0f),
                    Curve(Watch, typeof(VRCParentConstraint), "Sources.source3.Weight", right && inside ? 1f : 0f),
                    Curve(PlateBase, typeof(Transform), "m_LocalPosition.x", x),
                    Curve(PlateBase, typeof(Transform), "m_LocalPosition.y", plateSpot.y),
                    Curve(PlateBase, typeof(Transform), "m_LocalPosition.z", plateSpot.z),
                };
                if (waypoint)   // the button takes the other hand's index (D28)
                {
                    k.Add(Curve(PressL, typeof(GameObject), "m_IsActive", right ? 1f : 0f));
                    k.Add(Curve(PressR, typeof(GameObject), "m_IsActive", right ? 0f : 1f));
                }
                if (flash)   // the flashlight tab too (D46)
                {
                    k.Add(Curve(LightPressL, typeof(GameObject), "m_IsActive", right ? 1f : 0f));
                    k.Add(Curve(LightPressR, typeof(GameObject), "m_IsActive", right ? 0f : 1f));
                }
                return k.ToArray();
            }
            var sm = Layer(c, "PulseWatch Side", made);
            foreach (var inside in new[] { false, true })
                foreach (var right in new[] { false, true })
                {
                    string name = (right ? "Right" : "Left") + (inside ? " Inside" : "");
                    var st = State(sm, name, Clip("PW Side " + name, made, Keys(right, inside)), made);
                    if (!right && !inside) sm.defaultState = st;
                    var t = sm.AddAnyStateTransition(st);
                    made.Add(t);
                    t.hasExitTime = false;
                    t.duration = 0f;
                    t.canTransitionToSelf = false;
                    t.AddCondition(right ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, Right);
                    t.AddCondition(inside ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, Inside);
                }
        }

        static void DebugLayers(AnimatorController c, List<(Motion, string)> values, List<Object> made)
        {
            var vis = Layer(c, "PulseWatch DebugVis", made);
            var off = State(vis, "Off", Clip("PW Debug Off", made, Curve(Meter, typeof(GameObject), "m_IsActive", 0f)), made);
            var on = State(vis, "On", Clip("PW Debug On", made, Curve(Meter, typeof(GameObject), "m_IsActive", 1f)), made);
            vis.defaultState = off;
            var t = To(off, on, made);
            t.AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
            t.AddCondition(AnimatorConditionMode.If, 0f, DebugOn);
            To(on, off, made).AddCondition(AnimatorConditionMode.IfNot, 0f, DebugOn);

            foreach (var (p, prop, top) in new[] { (FaceProx, "_DbgFace", 1f), (GazeProx, "_DbgGaze", 1f), (Sens, "_DbgSens", 2f) })
                values.Add((Drive(p, Meter, prop, top, made), One));
        }

        // talking glows the ring (D20); muted turns it red (D27) — Write Defaults off, so each state sets _Muted.
        // D58: the ring follows VoiceS, Voice smoothed (a quick rise, a slow fade after speech). D70 tried one layer (smoother and
        // ring side by side in one Direct state) — Face and Panel's scale doubled again (WD off, as in 10-50), so it stays two layers
        static void VoiceLayer(AnimatorController c, List<Object> made)
        {
            var smooth = Layer(c, "PulseWatch Voice Smooth", made);
            var rise = State(smooth, "Rise", Smoother("PW VoiceS Rise", VoiceRiseK, VoiceRiseK1, made), made);
            var fall = State(smooth, "Fall", Smoother("PW VoiceS Fall", VoiceFallK, VoiceFallK1, made), made);
            smooth.defaultState = fall;
            To(fall, rise, made).AddCondition(AnimatorConditionMode.Greater, VoiceOn, Voice);
            To(rise, fall, made).AddCondition(AnimatorConditionMode.Less, VoiceOn, Voice);

            var sm = Layer(c, "PulseWatch Voice", made);
            // a plain 1D tree, _Muted 0 in both clips: a bare clip child in a Direct tree (0.2.5-0.2.6) doubled Face and Panel's
            // scale while unmuted (WD off) — the watch grew on unmuting in game (10-50)
            var tree = new BlendTree { name = "PW Voice", blendType = BlendTreeType.Simple1D, blendParameter = VoiceS, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            made.Add(tree);
            foreach (float v in new[] { 0f, 1f })
                tree.AddChild(Clip($"PW _Voice {v}", made, Curve(Radar, typeof(MeshRenderer), "material._Voice", v), Curve(Radar, typeof(MeshRenderer), "material._Muted", 0f)), v);
            var talk = State(sm, "Voice", tree, made);
            var muted = State(sm, "Muted", Clip("PW Muted", made,
                Curve(Radar, typeof(MeshRenderer), "material._Muted", 1f), Curve(Radar, typeof(MeshRenderer), "material._Voice", 0f)), made);
            sm.defaultState = talk;
            To(talk, muted, made).AddCondition(AnimatorConditionMode.If, 0f, MuteSelf);
            To(muted, talk, made).AddCondition(AnimatorConditionMode.IfNot, 0f, MuteSelf);
        }

        // VoiceS <- k * VoiceS + (1 - k) * curve(Voice): a Direct tree of two 1D children that both animate VoiceS, weights k and 1 - k
        // (sum 1, so it is exact with write defaults off — the same rule as Direct)
        static BlendTree Smoother(string name, string k, string k1, List<Object> made)
        {
            var tree = new BlendTree { name = name, blendType = BlendTreeType.Direct, hideFlags = HideFlags.HideInHierarchy };
            made.Add(tree);
            var hold = new BlendTree { name = name + " Hold", blendType = BlendTreeType.Simple1D, blendParameter = VoiceS, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            made.Add(hold);
            foreach (float v in new[] { 0f, 1f })
                hold.AddChild(Clip($"PW VoiceS {v}", made, Curve("", typeof(Animator), VoiceS, v)), v);
            var target = new BlendTree { name = name + " Voice", blendType = BlendTreeType.Simple1D, blendParameter = Voice, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            made.Add(target);
            for (int i = 0; i < VoiceIn.Length; i++)   // the steeper curve
                target.AddChild(Clip($"PW VoiceS {VoiceOut[i]}", made, Curve("", typeof(Animator), VoiceS, VoiceOut[i])), VoiceIn[i]);
            tree.AddChild(hold);
            tree.AddChild(target);
            var children = tree.children;
            children[0].directBlendParameter = k;
            children[1].directBlendParameter = k1;
            tree.children = children;
            return tree;
        }

        // one linear clip from the avatar's load, SessionMax long (then it holds); a world change reloads the avatar = 0 again
        static void SessionLayer(AnimatorController c, List<Object> made)
        {
            var sm = Layer(c, "PulseWatch Session", made);
            var off = State(sm, "Remote", Clip("PW Session Off", made, Curve(PlateQuad, typeof(MeshRenderer), "material._SesOn", 0f)), made);
            var count = new AnimationClip { name = "PW Session" };
            made.Add(count);
            AnimationUtility.SetEditorCurve(count, Bind(PlateQuad, typeof(MeshRenderer), "material._SesOn"), new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(SessionMax, 1f)));
            AnimationUtility.SetEditorCurve(count, Bind(PlateQuad, typeof(MeshRenderer), "material._SessionSec"), AnimationCurve.Linear(0f, 0f, SessionMax, SessionMax));
            var on = State(sm, "Local", count, made);
            sm.defaultState = off;
            To(off, on, made).AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
        }

        // the count tab by Local; the slices in a tree weighted by Local — weight 0 on others' copies, where the slices are 0
        // anyway (local receivers) and the disc keeps the material's 0s
        static void RadarValues(List<(Motion, string)> values, List<Object> made)
        {
            values.Add((Drive(Local, Radar, "_CountOn", 1f, made), One));
            var cells = new List<(Motion, string)>();
            foreach (var (k, b, i) in AllSlices()) cells.Add((Drive(Cell(k, b, i), Radar, CellProp(k, b, i), 1f, made), One));
            values.Add((Direct("PW Radar", made, cells.ToArray()), Local));
        }

        static void ScanLayer(AnimatorController c, bool radar, List<Object> made)
        {
            float reach = RadarRings[RadarRings.Length - 1];
            Key[] Hide() => new[] { Curve(ScanRoot, typeof(GameObject), "m_IsActive", 0f), Curve(ScanFloor, typeof(GameObject), "m_IsActive", 0f) };
            var sm = Layer(c, "PulseWatch Scan", made);
            var idle = State(sm, "Idle", Clip("PW Scan Idle", made, Hide()), made);
            // fast start easing out (Hermite slopes 1.8x and 0.3x the average speed — monotonic)
            var r = new AnimationCurve(new Keyframe(0f, 0f, 0f, 1.8f * reach / ScanTime), new Keyframe(ScanTime, reach, 0.3f * reach / ScanTime, 0f));
            var wave = new AnimationClip { name = "PW Scan Wave" };
            made.Add(wave);
            foreach (var path in new[] { ScanRoot, ScanFloor })
                AnimationUtility.SetEditorCurve(wave, Bind(path, typeof(GameObject), "m_IsActive"), AnimationCurve.Constant(0f, ScanTime, 1f));
            foreach (var path in new[] { ScanShell, ScanSurface, ScanFloor + "/Disc" })
                AnimationUtility.SetEditorCurve(wave, Bind(path, typeof(MeshRenderer), "material._Radius"), r);
            if (radar) AnimationUtility.SetEditorCurve(wave, Bind(Radar, typeof(MeshRenderer), "material._ScanR"), r);
            var waveS = State(sm, "Wave", wave, made);
            var cool = State(sm, "Cooldown", Clip("PW Scan Cooldown", made, ScanCooldown - ScanTime, Hide()), made);
            var wait = State(sm, "Wait", Clip("PW Scan Wait", made, Hide()), made);
            sm.defaultState = idle;
            var go = To(idle, waveS, made);
            go.AddCondition(AnimatorConditionMode.If, 0f, Open);
            go.AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
            var t1 = To(waveS, cool, made);
            t1.hasExitTime = true;
            t1.exitTime = 1f;
            var t2 = To(cool, wait, made);
            t2.hasExitTime = true;
            t2.exitTime = 1f;
            To(wait, idle, made).AddCondition(AnimatorConditionMode.IfNot, 0f, Open);
        }

        // ---- waypoint (D26-D30, docs/pulse-watch.md 4-5-1) --------------------------------------------------------------

        static void WaypointParams(AnimatorController c)
        {
            c.AddParameter(GestureLeft, AnimatorControllerParameterType.Int);
            c.AddParameter(GestureRight, AnimatorControllerParameterType.Int);
            c.AddParameter(VRMode, AnimatorControllerParameterType.Int);
            foreach (var p in new[] { WpOn, WpSeq, WpLaser, WpShow, WpTap, WpClear, WpRayHit })
                c.AddParameter(p, AnimatorControllerParameterType.Bool);
            c.AddParameter(WpPress, AnimatorControllerParameterType.Float);
            c.AddParameter(WpRayRatio, AnimatorControllerParameterType.Float);
        }

        // Button (owner): Closed -> Arm (ArmTime after opening, and the finger out) -> Idle -> Pre (in past PressOn, the other hand pointing — D67; D60: out
        // again before PressMin = a brush, back to Idle) -> Down: out again (below PressOff) before HoldTime = laser on / off
        // (D29 Q16: tap again cancels; then Refractory before the next press), held HoldTime = clear. D60: with a waypoint set
        // the clear waits — PendUp (finger still in) -> Pend (UndoTime, the red bar running back, the mark dimmed and dashed on
        // the wearer's side): a press keeps it (Undo), the time running out or the watch closing sends it (Clear). The other
        // tab pressed (flash on, past PressOn) blocks Pre and cancels Pre / Down (WaitUp). The hold bar fills while Down.
        // Closing the watch drops back to Closed. Press: the depth -> the tab's edge / fill
        static void ButtonLayers(AnimatorController c, bool flash, bool tag, List<(Motion, string)> values, List<Object> made)
        {
            // every state writes _Hold and the pending look (write defaults off) — the name tag's too when it is built (D61)
            Key[] K(float hold, float pend)
            {
                var keys = new List<Key>
                {
                    Curve(ButtonQuad, typeof(MeshRenderer), "material._Hold", hold),
                    Curve(Waypoint + "/Mark/Vis/Diamond", typeof(MeshRenderer), "material._Pend", pend),
                    Curve(WpDisc, typeof(SkinnedMeshRenderer), "material._Pend", pend),
                };
                if (tag) keys.Add(Curve(Waypoint + "/Mark/Vis/Tag", typeof(MeshRenderer), "material._Pend", pend));
                return keys.ToArray();
            }
            AnimationClip Run(string name, float length, float from, float to, float pend)
            {
                var clip = Clip(name, made, length, K(0f, pend));
                AnimationUtility.SetEditorCurve(clip, Bind(ButtonQuad, typeof(MeshRenderer), "material._Hold"), AnimationCurve.Linear(0f, from, length, to));
                return clip;
            }
            var sm = Layer(c, "PulseWatch WpButton", made);
            var remote = State(sm, "Remote", Clip("PW Btn Remote", made, K(0f, 0f)), made);
            var closed = State(sm, "Closed", Clip("PW Btn Closed", made, K(0f, 0f)), made);
            var arm = State(sm, "Arm", Clip("PW Btn Arm", made, ArmTime, K(0f, 0f)), made);
            var idle = State(sm, "Idle", Clip("PW Btn Idle", made, K(0f, 0f)), made);
            var pre = State(sm, "Pre", Clip("PW Btn Pre", made, PressMin, K(0f, 0f)), made);
            var down = State(sm, "Down", Run("PW Btn Hold", HoldTime, 0f, 1f, 0f), made);
            var on = State(sm, "LaserOn", Clip("PW Btn Laser On", made, Refractory, K(0f, 0f)), made);
            var off = State(sm, "LaserOff", Clip("PW Btn Laser Off", made, Refractory, K(0f, 0f)), made);
            var pendUp = State(sm, "PendUp", Clip("PW Btn Pend Up", made, K(1f, 1f)), made);
            var pend = State(sm, "Pend", Run("PW Btn Pend", UndoTime, 1f, 0f, 1f), made);
            var undo = State(sm, "Undo", Clip("PW Btn Undo", made, K(0f, 0f)), made);
            var clear = State(sm, "Clear", Clip("PW Btn Clear", made, K(0f, 0f)), made);
            var wait = State(sm, "WaitUp", Clip("PW Btn Wait", made, K(0f, 0f)), made);
            sm.defaultState = remote;
            Driver(on, made, Set(WpLaser, 1f));
            Driver(off, made, Set(WpLaser, 0f));
            Driver(pendUp, made, Set(WpLaser, 0f));
            Driver(clear, made, Set(WpOn, 0f), Set(WpLaser, 0f));

            To(remote, closed, made).AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
            To(closed, arm, made).AddCondition(AnimatorConditionMode.If, 0f, Open);
            var armed = To(arm, idle, made);
            armed.hasExitTime = true;
            armed.exitTime = 1f;
            armed.AddCondition(AnimatorConditionMode.Less, PressOff, WpPress);
            foreach (bool right in new[] { false, true })
            {
                var press = To(idle, pre, made);
                press.AddCondition(AnimatorConditionMode.Greater, PressOn, WpPress);
                if (flash) press.AddCondition(AnimatorConditionMode.Less, PressOn, FlashPress);
                PointHand(press, right);
            }
            To(pre, idle, made).AddCondition(AnimatorConditionMode.Less, PressOff, WpPress);
            if (flash)
                foreach (var s in new[] { pre, down }) To(s, wait, made).AddCondition(AnimatorConditionMode.Greater, PressOn, FlashPress);
            After(pre, down, made);
            var tapOn = To(down, on, made);
            tapOn.AddCondition(AnimatorConditionMode.Less, PressOff, WpPress);
            tapOn.AddCondition(AnimatorConditionMode.IfNot, 0f, WpLaser);
            var tapOff = To(down, off, made);
            tapOff.AddCondition(AnimatorConditionMode.Less, PressOff, WpPress);
            tapOff.AddCondition(AnimatorConditionMode.If, 0f, WpLaser);
            var held = To(down, pendUp, made);
            held.hasExitTime = true;
            held.exitTime = 1f;
            held.AddCondition(AnimatorConditionMode.If, 0f, WpOn);
            var heldEmpty = To(down, clear, made);   // nothing to keep: the laser off at once
            heldEmpty.hasExitTime = true;
            heldEmpty.exitTime = 1f;
            heldEmpty.AddCondition(AnimatorConditionMode.IfNot, 0f, WpOn);
            To(pendUp, pend, made).AddCondition(AnimatorConditionMode.Less, PressOff, WpPress);
            To(pend, undo, made).AddCondition(AnimatorConditionMode.Greater, PressOn, WpPress);
            After(pend, clear, made);
            foreach (var s in new[] { pendUp, pend }) To(s, clear, made).AddCondition(AnimatorConditionMode.IfNot, 0f, Open);
            After(on, idle, made);
            After(off, idle, made);
            After(undo, wait, made);
            After(clear, wait, made);
            To(wait, idle, made).AddCondition(AnimatorConditionMode.Less, PressOff, WpPress);
            foreach (var s in new[] { arm, idle, pre, down, wait }) To(s, closed, made).AddCondition(AnimatorConditionMode.IfNot, 0f, Open);

            values.Add((Drive(WpPress, ButtonQuad, "_Press", 1f, made), One));
        }

        // ---- flashlight (D46, docs/pulse-watch.md 10-60) -----------------------------------------------------------------

        // FlashButton (owner): Closed -> Arm (ArmTime after opening, and the finger out) -> Idle -> Pre (in past PressOn, the other hand pointing — D67; out
        // before PressMin = a brush — D60) -> Down -> out again (below PressOff) flips Flash, then Refractory. The waypoint
        // button pressed (past PressOn) blocks Pre and cancels Pre / Down (WaitUp, D60). Closing the watch drops back to Closed;
        // the light stays as it is. FlashPress: the depth -> the tab's edge / fill. Flash (owner): the light and the tab's lit
        // look; Flash is local, so others stay Off
        static void FlashLayers(AnimatorController c, bool waypoint, List<(Motion, string)> values, List<Object> made)
        {
            var sm = Layer(c, "PulseWatch FlashButton", made);
            var empty = Clip("PW Flash Btn Idle", made);
            var remote = State(sm, "Remote", empty, made);
            var closed = State(sm, "Closed", empty, made);
            var arm = State(sm, "Arm", Clip("PW Flash Btn Arm", made, ArmTime), made);
            var idle = State(sm, "Idle", empty, made);
            var pre = State(sm, "Pre", Clip("PW Flash Btn Pre", made, PressMin), made);
            var down = State(sm, "Down", empty, made);
            var settle = Clip("PW Flash Btn Settle", made, Refractory);
            var on = State(sm, "TurnOn", settle, made);
            var off = State(sm, "TurnOff", settle, made);
            var wait = State(sm, "WaitUp", empty, made);
            sm.defaultState = remote;
            Driver(on, made, Set(Flash, 1f));
            Driver(off, made, Set(Flash, 0f));
            To(remote, closed, made).AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
            To(closed, arm, made).AddCondition(AnimatorConditionMode.If, 0f, Open);
            var armed = To(arm, idle, made);
            armed.hasExitTime = true;
            armed.exitTime = 1f;
            armed.AddCondition(AnimatorConditionMode.Less, PressOff, FlashPress);
            foreach (bool right in new[] { false, true })
            {
                var press = To(idle, pre, made);
                press.AddCondition(AnimatorConditionMode.Greater, PressOn, FlashPress);
                if (waypoint) press.AddCondition(AnimatorConditionMode.Less, PressOn, WpPress);
                PointHand(press, right);
            }
            To(pre, idle, made).AddCondition(AnimatorConditionMode.Less, PressOff, FlashPress);
            if (waypoint)
                foreach (var s in new[] { pre, down }) To(s, wait, made).AddCondition(AnimatorConditionMode.Greater, PressOn, WpPress);
            After(pre, down, made);
            To(wait, idle, made).AddCondition(AnimatorConditionMode.Less, PressOff, FlashPress);
            var tapOn = To(down, on, made);
            tapOn.AddCondition(AnimatorConditionMode.Less, PressOff, FlashPress);
            tapOn.AddCondition(AnimatorConditionMode.IfNot, 0f, Flash);
            var tapOff = To(down, off, made);
            tapOff.AddCondition(AnimatorConditionMode.Less, PressOff, FlashPress);
            tapOff.AddCondition(AnimatorConditionMode.If, 0f, Flash);
            After(on, idle, made);
            After(off, idle, made);
            foreach (var s in new[] { arm, idle, pre, down, wait }) To(s, closed, made).AddCondition(AnimatorConditionMode.IfNot, 0f, Open);

            values.Add((Drive(FlashPress, LightTabQuad, "_Press", 1f, made), One));

            var vis = Layer(c, "PulseWatch Flash", made);
            var dark = State(vis, "Off", Clip("PW Flash Off", made, Curve(Flashlight, typeof(GameObject), "m_IsActive", 0f),
                Curve(LightTabQuad, typeof(MeshRenderer), "material._Laser", 0f)), made);
            var lit = State(vis, "On", Clip("PW Flash On", made, Curve(Flashlight, typeof(GameObject), "m_IsActive", 1f),
                Curve(LightTabQuad, typeof(MeshRenderer), "material._Laser", 1f)), made);
            vis.defaultState = dark;
            var t = To(dark, lit, made);
            t.AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
            t.AddCondition(AnimatorConditionMode.If, 0f, Flash);
            To(lit, dark, made).AddCondition(AnimatorConditionMode.IfNot, 0f, Flash);
        }

        // Laser (owner): Off -> Release (laser on: wait for the other hand's trigger to be let go) -> Ready (LaserTimeout) -> a
        // trigger (the other hand's Gesture 5 trigger only, or 1 fist — D29 Q18) while the laser and the drop both hit = Fire:
        // WpOn, flip WpSeq -> Fired (FireHold: others' copies keep casting until the flip reaches them) -> laser off.
        // WpLaser off (button tap) cancels; Ready running out = off. Menu: WpTap in VR = laser on, on desktop = mark at the
        // feet (the Mark layer takes the feet source when VRMode is 0); WpClear = clear
        static void LaserLayer(AnimatorController c, List<Object> made)
        {
            var empty = Clip("PW Laser Idle", made);
            var sm = Layer(c, "PulseWatch WpLaser", made);
            var remote = State(sm, "Remote", empty, made);
            var off = State(sm, "Off", empty, made);
            var release = State(sm, "Release", empty, made);
            var ready = State(sm, "Ready", Clip("PW Laser Ready", made, LaserTimeout), made);
            var timeout = State(sm, "Timeout", empty, made);
            var fireOn = State(sm, "FireSeqOn", empty, made);
            var fireOff = State(sm, "FireSeqOff", empty, made);
            var fired = State(sm, "Fired", Clip("PW Laser Fired", made, FireHold), made);
            var done = State(sm, "Done", empty, made);
            var menuLaser = State(sm, "MenuLaser", empty, made);
            var feetOn = State(sm, "FeetSeqOn", empty, made);
            var feetOff = State(sm, "FeetSeqOff", empty, made);
            var menuWait = State(sm, "MenuWait", empty, made);
            var menuClear = State(sm, "MenuClear", empty, made);
            sm.defaultState = remote;
            Driver(timeout, made, Set(WpLaser, 0f));
            Driver(fireOn, made, Set(WpOn, 1f), Set(WpSeq, 1f));
            Driver(fireOff, made, Set(WpOn, 1f), Set(WpSeq, 0f));
            Driver(done, made, Set(WpLaser, 0f));
            Driver(menuLaser, made, Set(WpLaser, 1f));
            Driver(feetOn, made, Set(WpOn, 1f), Set(WpSeq, 1f));
            Driver(feetOff, made, Set(WpOn, 1f), Set(WpSeq, 0f));
            Driver(menuClear, made, Set(WpOn, 0f), Set(WpLaser, 0f));

            To(remote, off, made).AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
            foreach (var s in new[] { off, release, ready })
                To(s, menuClear, made).AddCondition(AnimatorConditionMode.If, 0f, WpClear);
            To(menuClear, off, made).AddCondition(AnimatorConditionMode.IfNot, 0f, WpClear);
            var vr = To(off, menuLaser, made);
            vr.AddCondition(AnimatorConditionMode.If, 0f, WpTap);
            vr.AddCondition(AnimatorConditionMode.Equals, 1f, VRMode);
            foreach (var (to, seq) in new[] { (feetOn, false), (feetOff, true) })
            {
                var t = To(off, to, made);
                t.AddCondition(AnimatorConditionMode.If, 0f, WpTap);
                t.AddCondition(AnimatorConditionMode.NotEqual, 1f, VRMode);
                t.AddCondition(seq ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, WpSeq);
                After(to, menuWait, made);
            }
            To(menuLaser, menuWait, made).AddCondition(AnimatorConditionMode.IfNot, 0f, WpTap);
            To(menuWait, off, made).AddCondition(AnimatorConditionMode.IfNot, 0f, WpTap);
            To(off, release, made).AddCondition(AnimatorConditionMode.If, 0f, WpLaser);
            foreach (var s in new[] { release, ready }) To(s, off, made).AddCondition(AnimatorConditionMode.IfNot, 0f, WpLaser);
            // the trigger hand = the hand without the watch: GestureRight on the left wrist (Right off), GestureLeft on the right
            foreach (bool right in new[] { false, true })
            {
                string g = right ? GestureLeft : GestureRight;
                var let = To(release, ready, made);
                let.AddCondition(right ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, Right);
                let.AddCondition(AnimatorConditionMode.NotEqual, 1f, g);
                let.AddCondition(AnimatorConditionMode.NotEqual, 5f, g);
                foreach (float gv in new[] { 5f, 1f })
                    foreach (var (to, seq) in new[] { (fireOn, false), (fireOff, true) })
                    {
                        var t = To(ready, to, made);
                        t.AddCondition(right ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, Right);
                        t.AddCondition(AnimatorConditionMode.Equals, gv, g);
                        t.AddCondition(AnimatorConditionMode.If, 0f, WpRayHit);
                        t.AddCondition(seq ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, WpSeq);
                    }
            }
            After(ready, timeout, made);
            After(timeout, off, made);
            After(fireOn, fired, made);
            After(fireOff, fired, made);
            After(fired, done, made);
            After(done, off, made);
        }

        // LaserVis (everyone): the laser on the hand without the watch is on while WpLaser — on others' copies too, so their mark
        // lands where the wearer's does. Its beam shows to the wearer, and to others with WpShow (D29 Q17); the reticle and the
        // button's laser outline to the wearer. Beam: the beam's length follows the raycast's _Ratio (miss = full length)
        static void LaserVisLayers(AnimatorController c, Vector3 beamL, Vector3 beamR, List<(Motion, string)> values, List<Object> made)
        {
            Key[] Keys(bool l, bool r, bool beam, bool own) => new[]
            {
                Curve(Laser + "L", typeof(GameObject), "m_IsActive", l ? 1f : 0f),
                Curve(Laser + "R", typeof(GameObject), "m_IsActive", r ? 1f : 0f),
                Curve(Laser + "L/Beam", typeof(GameObject), "m_IsActive", beam ? 1f : 0f),
                Curve(Laser + "R/Beam", typeof(GameObject), "m_IsActive", beam ? 1f : 0f),
                Curve(Reticle, typeof(GameObject), "m_IsActive", own ? 1f : 0f),
                Curve(ButtonQuad, typeof(MeshRenderer), "material._Laser", own ? 1f : 0f),
            };
            var sm = Layer(c, "PulseWatch WpLaserVis", made);
            var off = State(sm, "Off", Clip("PW Laser Off", made, Keys(false, false, false, false)), made);
            sm.defaultState = off;
            AnyTo(sm, off, made).AddCondition(AnimatorConditionMode.IfNot, 0f, WpLaser);
            foreach (bool right in new[] { false, true })   // watch on the right wrist -> the laser on the left hand
            {
                string hand = right ? "L" : "R";
                foreach (var (name, local, show) in new[] { ("Own", true, true), ("Shown", false, true), ("Silent", false, false) })
                {
                    var s = State(sm, $"{hand} {name}", Clip($"PW Laser {hand} {name}", made, Keys(right, !right, show, local)), made);
                    var t = AnyTo(sm, s, made);
                    t.AddCondition(AnimatorConditionMode.If, 0f, WpLaser);
                    t.AddCondition(right ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, Right);
                    t.AddCondition(local ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, IsLocal);
                    if (!local) t.AddCondition(show ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, WpShow);
                }
            }

            // beam length: all three scale components written (a Transform curve left out goes to 0 in game — SideLayer)
            Key[] Len(float k) => new[]
            {
                Curve(Laser + "L/Beam", typeof(Transform), "m_LocalScale.x", beamL.x), Curve(Laser + "L/Beam", typeof(Transform), "m_LocalScale.y", beamL.y),
                Curve(Laser + "L/Beam", typeof(Transform), "m_LocalScale.z", beamL.z * k),
                Curve(Laser + "R/Beam", typeof(Transform), "m_LocalScale.x", beamR.x), Curve(Laser + "R/Beam", typeof(Transform), "m_LocalScale.y", beamR.y),
                Curve(Laser + "R/Beam", typeof(Transform), "m_LocalScale.z", beamR.z * k),
            };
            var tree = new BlendTree { name = "PW Laser Length", blendType = BlendTreeType.Simple1D, blendParameter = WpRayRatio, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            made.Add(tree);
            tree.AddChild(Clip("PW Laser Len 0", made, Len(0f)), 0f);
            tree.AddChild(Clip("PW Laser Len 1", made, Len(1f)), 1f);
            values.Add((tree, One));
        }

        // Mark (everyone): a mark is the flip of WpSeq — WaitA / ShowA while it is off, WaitB / ShowB while on. On a flip the mark
        // follows its source for TrackTime (LaserHit/MarkSeat in VR, WpFeet on desktop: the wearer's VRMode) and freezes to the
        // world there (each client at its own copy's hit point — the portal gun's way). WpOn off hides it. Others' copies start in
        // Init for InitTime so the synced values arrive first: a late joiner shows nothing until the next mark (the old spot
        // is not known on their side). VisLayer: Mark/Vis and WpDisc's renderer (10-95: the disc's waypoint too — the Mark layer
        // keeps its GameObject) for the wearer, and for others with WpShow
        static void MarkLayers(AnimatorController c, List<Object> made)
        {
            Key[] Keys(bool shown, bool frozen) => new[]
            {
                Curve(Waypoint + "/Mark", typeof(GameObject), "m_IsActive", shown ? 1f : 0f),
                Curve(WpDisc, typeof(GameObject), "m_IsActive", shown ? 1f : 0f),
                Curve(Waypoint, typeof(VRCParentConstraint), "FreezeToWorld", frozen ? 1f : 0f),
            };
            Key[] Track(bool feet) => new[]
            {
                Curve(Waypoint + "/Mark", typeof(GameObject), "m_IsActive", 0f),
                Curve(WpDisc, typeof(GameObject), "m_IsActive", 0f),
                Curve(Waypoint, typeof(VRCParentConstraint), "FreezeToWorld", 0f),
                Curve(Waypoint, typeof(VRCParentConstraint), "Sources.source0.Weight", feet ? 0f : 1f),
                Curve(Waypoint, typeof(VRCParentConstraint), "Sources.source1.Weight", feet ? 1f : 0f),
            };
            const float InitTime = 2f;
            var sm = Layer(c, "PulseWatch WpMark", made);
            var init = State(sm, "Init", Clip("PW Mark Init", made, InitTime, Keys(false, false)), made);
            sm.defaultState = init;
            var wait = new[] { State(sm, "WaitA", Clip("PW Mark Wait", made, Keys(false, false)), made), null };
            wait[1] = State(sm, "WaitB", wait[0].motion, made);
            var show = new[] { State(sm, "ShowA", Clip("PW Mark Show", made, Keys(true, true)), made), null };
            show[1] = State(sm, "ShowB", show[0].motion, made);
            var trackLaser = Clip("PW Mark Track Laser", made, TrackTime, Track(false));
            var trackFeet = Clip("PW Mark Track Feet", made, TrackTime, Track(true));
            for (int k = 0; k < 2; k++)   // k = the WpSeq value the state belongs to
            {
                var seqIs = k == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
                var local = To(init, wait[k], made);
                local.AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
                local.AddCondition(seqIs, 0f, WpSeq);
                var late = To(init, wait[k], made);
                late.hasExitTime = true;
                late.exitTime = 1f;
                late.AddCondition(seqIs, 0f, WpSeq);
                foreach (var (clip, vr) in new[] { (trackLaser, true), (trackFeet, false) })
                {
                    // a flip to k from the other value's states
                    var tr = State(sm, $"Track{(k == 1 ? "B" : "A")} {(vr ? "Laser" : "Feet")}", clip, made);
                    foreach (var from in new[] { wait[1 - k], show[1 - k] })
                    {
                        var t = To(from, tr, made);
                        t.AddCondition(seqIs, 0f, WpSeq);
                        t.AddCondition(vr ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual, 1f, VRMode);
                    }
                    var placed = To(tr, show[k], made);
                    placed.hasExitTime = true;
                    placed.exitTime = 1f;
                    placed.AddCondition(AnimatorConditionMode.If, 0f, WpOn);
                    To(tr, wait[k], made).AddCondition(AnimatorConditionMode.IfNot, 0f, WpOn);
                }
                To(show[k], wait[k], made).AddCondition(AnimatorConditionMode.IfNot, 0f, WpOn);
            }

            var vis = Layer(c, "PulseWatch WpMarkVis", made);
            var hidden = State(vis, "Hidden", Clip("PW Mark Vis Off", made, Curve(Waypoint + "/Mark/Vis", typeof(GameObject), "m_IsActive", 0f),
                Curve(WpDisc, typeof(SkinnedMeshRenderer), "m_Enabled", 0f)), made);
            var shown = State(vis, "Shown", Clip("PW Mark Vis On", made, Curve(Waypoint + "/Mark/Vis", typeof(GameObject), "m_IsActive", 1f),
                Curve(WpDisc, typeof(SkinnedMeshRenderer), "m_Enabled", 1f)), made);
            vis.defaultState = hidden;
            To(hidden, shown, made).AddCondition(AnimatorConditionMode.If, 0f, IsLocal);
            To(hidden, shown, made).AddCondition(AnimatorConditionMode.If, 0f, WpShow);
            var back = To(shown, hidden, made);
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, IsLocal);
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, WpShow);
        }

        // D67: a press starts only with the pressing hand — the one without the watch (GestureRight on the left wrist, Right off;
        // GestureLeft on the right) — in the point gesture; one transition per wrist. Held presses don't check it again
        static void PointHand(AnimatorStateTransition t, bool right)
        {
            t.AddCondition(right ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, Right);
            t.AddCondition(AnimatorConditionMode.Equals, GesturePoint, right ? GestureLeft : GestureRight);
        }

        static void After(AnimatorState from, AnimatorState to, List<Object> made)
        {
            var t = To(from, to, made);
            t.hasExitTime = true;
            t.exitTime = 1f;
        }

        static AnimatorStateTransition AnyTo(AnimatorStateMachine sm, AnimatorState to, List<Object> made)
        {
            var t = sm.AddAnyStateTransition(to);
            made.Add(t);
            t.hasExitTime = false;
            t.duration = 0f;
            t.canTransitionToSelf = false;
            return t;
        }

        // 1D tree: param drives one material property (0 -> 0, top -> top) on the renderer at path
        static BlendTree Drive(string param, string path, string prop, float top, List<Object> made)
        {
            var sub = new BlendTree { name = "PW " + prop, blendType = BlendTreeType.Simple1D, blendParameter = param, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            made.Add(sub);
            sub.AddChild(Clip($"PW {prop} 0", made, Curve(path, typeof(MeshRenderer), "material." + prop, 0f)), 0f);
            sub.AddChild(Clip($"PW {prop} {top}", made, Curve(path, typeof(MeshRenderer), "material." + prop, top)), top);
            return sub;
        }

        // Direct tree of the given children and their weight parameters. With write defaults off it is exact only while every
        // property sits in one child and that child's weight is 0 or 1 (One, Local) — no bare clips (VoiceLayer), no property
        // in two children or in another layer
        static BlendTree Direct(string name, List<Object> made, params (Motion motion, string weight)[] items)
        {
            var tree = new BlendTree { name = name, blendType = BlendTreeType.Direct, hideFlags = HideFlags.HideInHierarchy };
            made.Add(tree);
            foreach (var (m, _) in items) tree.AddChild(m);
            var children = tree.children;
            for (int i = 0; i < children.Length; i++) children[i].directBlendParameter = items[i].weight;
            tree.children = children;
            return tree;
        }

        // ---- clips ------------------------------------------------------------------------------------------------

        struct Key
        {
            public EditorCurveBinding binding;
            public float value;
        }

        static Key Curve(string path, System.Type type, string prop, float value) => new Key { binding = Bind(path, type, prop), value = value };
        static Key Dbg(string prop, float value) => Curve(Meter, typeof(MeshRenderer), "material." + prop, value);
        static EditorCurveBinding Bind(string path, System.Type type, string prop) => EditorCurveBinding.FloatCurve(path, type, prop);

        static AnimationClip Clip(string name, List<Object> made, params Key[] keys) => Clip(name, made, 1f / 60f, keys);

        static AnimationClip Clip(string name, List<Object> made, float length, params Key[] keys)
        {
            var clip = new AnimationClip { name = name };
            made.Add(clip);
            foreach (var k in keys)
                AnimationUtility.SetEditorCurve(clip, k.binding, new AnimationCurve(new Keyframe(0f, k.value), new Keyframe(length, k.value)));
            if (keys.Length == 0)   // an empty clip still needs a length for exit times
                AnimationUtility.SetEditorCurve(clip, Bind("_PulseWatchNone", typeof(GameObject), "m_IsActive"), new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(length, 1f)));
            return clip;
        }

        static AnimationClip Ramp(string name, float length, List<Object> made, params (EditorCurveBinding b, float from, float to)[] curves)
        {
            var clip = new AnimationClip { name = name };
            made.Add(clip);
            foreach (var (b, from, to) in curves)
                AnimationUtility.SetEditorCurve(clip, b, AnimationCurve.EaseInOut(0f, from, length, to));
            return clip;
        }

        // ---- graph helpers ------------------------------------------------------------------------------------

        static AnimatorStateMachine Layer(AnimatorController c, string name, List<Object> made)
        {
            var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            made.Add(sm);
            c.AddLayer(new AnimatorControllerLayer { name = name, stateMachine = sm, defaultWeight = 1f });
            return sm;
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, Motion motion, List<Object> made)
        {
            var s = sm.AddState(name);
            made.Add(s);
            s.motion = motion;
            s.writeDefaultValues = false;
            return s;
        }

        static AnimatorStateTransition To(AnimatorState from, AnimatorState to, List<Object> made)
        {
            var t = from.AddTransition(to);
            made.Add(t);
            t.hasExitTime = false;
            t.duration = 0f;
            return t;
        }

        static VRC_AvatarParameterDriver.Parameter Set(string name, float value) =>
            new VRC_AvatarParameterDriver.Parameter { type = VRC_AvatarParameterDriver.ChangeType.Set, name = name, value = value };

        static void Driver(AnimatorState s, List<Object> made, params VRC_AvatarParameterDriver.Parameter[] ps)
        {
            var d = s.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            made.Add(d);
            d.localOnly = true;
            d.parameters = new List<VRC_AvatarParameterDriver.Parameter>(ps);
        }
    }
}
#endif
