// Pyrite9 Pulse Watch — prototype hologram (stages 1-5, layout D16: radar disc on the wrist, clock floating over its far side —
// docs/pulse-watch.md 10-13). Premultiplied alpha: backings cover with alpha, lines and glow add with alpha 0. Looks by _Mode:
//   0 plate: the clock — accent tab with MM.DD (요일), the plate's world position "X -23.4 Y 1.2 Z 105.8" right of it (D22,
//            shrinks to fit), big HH:MM + seconds, 60-segment seconds gauge, cut corner, corner brackets, code bars; with _FpsOn
//            (FX: the wearer's copy only) this client's frames per second above the seconds, "141 FPS" (D25)
//   1 disc : outer ring (four arcs broken at the diagonals, "+" in the gaps, 5 / 30 deg ticks, accent wedge at 12) and, with
//            _Radar, the radar inside: heads around the wearer — 12 directions (12 o'clock = where the head faces, clockwise)
//            x 5 rings (0.4-1.5 / 1.5-3 / 3-5 / 5-8 / 8-14 m), a hatched sector per occupied cell, ring distances, a turning sweep
//            and the wearer's arrow at the centre. The outer rings split each cell into slices (1 / 1 / 2 / 2 / 4, D36), a receiver
//            each; a slice's value is the proximity of the nearest head to its receiver (ring b's slice m in _Rad{b}_{m / 4}.{m % 4},
//            m = dir x slices + slice). The receivers overlap their neighbours in the ring, so a slice yields to a higher neighbour
//            unless the two readings add up past what one head could give (_Pair / _PairFar: then two heads); across rings in the
//            same direction the nearer centre wins (distances from _CellR, _PairK.x head radius) unless two heads (D23). A cell is
//            occupied when any of its slices is.
//            While the scan wave runs (_ScanR, m, animated with it) only the rings it has passed show their cells and a line
//            marks the front. With _CountOn (FX: the wearer's copy only) a tab outside the ring at the top left shows a person
//            mark and how many cells are occupied (D22 — a lower bound on the heads: two in one cell count once).
//            The sweep line turns clockwise once in 4 s by the shader's own clock (decoration since D55 left out the beyond-14 m
//            arm it used to show). Muted (_Muted, built-in MuteSelf — D27): ring, ticks and "+"
//            turn red and a slashed mic sits in the ring's gap at 6 o'clock, all blinking slowly (1.6 s, D7)
//            Plate with _SesOn (FX: the wearer's copy only): session time H:MM:SS (_SessionSec, animated from the avatar's load)
//            right of a shorter seconds gauge, the code bars dropped (D27 1)
//            Layered (D42, _Layered): the disc mesh is six quads stacked up the normal, uv1.x = layer — 0 backing, 1 grid + ring
//            distances, 2 occupied cells, 3 sweep + scan front, 4 outer ring / ticks / heading / voice, 5 centre arrow,
//            count tab, mic; each quad draws only its layer (heights: PulseWatchConst.DiscLayers)
//   2 debug: two bars (top = face toward the eyes, bottom = watch near the middle of the view) filled to the proximity,
//            bright tick = open mark, dim tick = close mark for the current sensitivity; bar colour = detect state
//            (white closed, yellow arming, accent open)
//   3 beam : a thin line along the object's +Z (length = Z scale, width = X scale) turned to face the camera; brighter at +Z
//            (DisableBatching: the beams share one material and batching would bake their transforms away)
//   Opening (D47, B): _Reveal (animated 0 -> RevealEnd by the FX) cuts the disc, its compass and waypoint overlays to a circle of
//            that radius (1 = the rim) with a warm ring on its edge (the grid + ring layer only); >= RevealEnd = all of it
//   4 button: the waypoint button under the plate (D28 B) — cut-corner tab with a cyan strip and a "mark here" icon (diamond over
//            a floor line). _Press (the depth receiver, D30) brightens the edge as the finger goes in and fills the tab past
//            _PressOn; _Hold (0-1, animated while held) runs a red bar along the bottom (clear at 1); _Laser = on (the laser /
//            the light): the tab inverts — filled cyan (waypoint) or warm white (flashlight) with a dark icon (D51, was an
//            outline). _Aspect = width / height. _Icon 1 (D46): the flashlight tab beside it - warm strip and a lamp-with-rays
//            icon, no hold bar. Thick (D52): the tab mesh has a raised top (uv1.y 1) and side walls (uv1.x 1, uv1.y 0 at the
//            base .. 1 at the top); the top follows the fingertip down — its height over the base is the finger's
//            ((1 - _Press) x _PressR - _PressD, the receiver's geometry) clamped to a quarter of its rest height .. the rest
//   5 waypoint on the disc (D27 3): a skinned quad over the disc (its vertices ride Face/Disc, its root bone is Face/WpEcho = the
//            mark's offset in the radar frame, copied there in local space) — the vertex shader gets the offset from its own
//            coordinates (the disc corner from the UV minus the vertex), the fragment draws a cyan diamond where the mark lies on
//            the radar's rings (outline only past 14 m, at the edge) and its distance
//   6 compass on the disc (D27 2): the same kind of skinned quad with the radar frame (eyes, head yaw only) as its root bone —
//            the object matrix is that frame, so its +Z gives the wearer's heading (deg clockwise from the world's +Z = north);
//            an accent triangle on the ring and an "N" inside it point north
//   Quads lie in their XZ plane: u = X = text right, v = Z = text up / disc 12 o'clock (the build turns them per wrist)
//   _DepthBias: the depth test is made as if the hologram sat that much closer to the camera — it draws over the wearer's own
//   hand and sleeve just in front of it (v7 VR: the hand bent up cut the plate) but not over things farther in front; where it
//   shows on screen and how deep it looks stay the same
Shader "Pyrite9/PulseWatch/Proto"
{
    Properties
    {
        _Color ("Accent", Color) = (1, 0.88, 0.05, 1)
        _WpColor ("Waypoint (button, the waypoint on the disc)", Color) = (0.30, 0.85, 1, 1)
        _LineColor ("Lines and text", Color) = (0.96, 0.96, 0.94, 1)
        _Back ("Backing (alpha = cover)", Color) = (0.03, 0.035, 0.045, 0.8)
        _Mode ("Mode (0 plate, 1 disc, 2 debug, 3 beam, 4 button, 5 waypoint on the disc, 6 compass on the disc)", Float) = 0
        _Press ("Button: finger depth (receiver proximity)", Range(0, 1)) = 0
        _PressOn ("Button: pressed past this", Float) = 0.6
        _Hold ("Button: hold progress (clear at 1)", Range(0, 1)) = 0
        _Laser ("Button: laser on", Float) = 0
        _Icon ("Button: icon (0 waypoint, 1 flashlight)", Float) = 0
        _PressR ("Button: depth receiver radius (local units)", Float) = 0.03
        _PressD ("Button: depth receiver behind the base (local units)", Float) = 0.015
        _Reveal ("Disc: opening radius (1 = rim, 1.05+ = all — D47)", Float) = 2
        [NoScaleOffset] _Glyphs ("Glyph atlas (SDF)", 2D) = "black" {}
        _UtcOffsetMin ("Wearer UTC offset (minutes)", Integer) = 540
        _DstStart ("Daylight saving start (month, week, weekday / day, minutes — D72)", Vector) = (0, 0, 0, 0)
        _DstEnd ("Daylight saving end (month, week, weekday / day, minutes)", Vector) = (0, 0, 0, 0)
        _DstMin ("Daylight saving shift (minutes, 0 = none)", Integer) = 0
        _DateLang ("Date language (0 English, 1 Japanese, 2 Korean — D73)", Integer) = 2
        _Aspect ("Plate width / height", Float) = 1.8
        _Radar ("Disc: radar inside", Float) = 1
        _Layered ("Disc: in layers (the mesh's quads carry their layer in uv1.x — D42)", Float) = 0
        _Voice ("Disc: wearer's voice level (built-in Voice, 0-1) — the outer ring glows", Range(0, 1)) = 0
        _CountOn ("Disc: occupied-cell count tab (the wearer's copy only)", Float) = 0
        _FpsOn ("Plate: this client's FPS above the seconds (the wearer's copy only)", Float) = 0
        _CoordOn ("Plate: the watch's world position right of the date (D22)", Float) = 1
        _SesOn ("Plate: session time right of the gauge (the wearer's copy only, D27)", Float) = 0
        _Muted ("Disc: the wearer is muted (built-in MuteSelf) — red ring, mic at 6 o'clock (D27)", Float) = 0
        _WpSpan ("Waypoint on the disc: the disc's width in Face units (mode 5)", Float) = 1
        _WpUnit ("Waypoint on the disc: metres per radar-frame unit (mode 5)", Float) = 1
        _Pend ("Waypoint on the disc: clear pending — dimmed, blinking (mode 5, D60)", Float) = 0
        _SessionSec ("Plate: session time (s, animated from the avatar's load)", Float) = 0
        _DepthBias ("Draw over things this close in front (m)", Float) = 0
        _DbgFace ("Debug face", Range(0, 1)) = 0
        _DbgGaze ("Debug gaze", Range(0, 1)) = 0
        _DbgState ("Debug state", Range(0, 1)) = 0
        _DbgSens ("Debug sensitivity", Range(0, 2)) = 0
        _ThFaceOpen ("Face open marks (normal, loose, strict)", Vector) = (0.63, 0.75, 0.52, 0)
        _ThFaceClose ("Face close marks", Vector) = (0.46, 0.58, 0.35, 0)
        _ThGazeOpen ("Gaze open marks", Vector) = (0.58, 0.69, 0.46, 0)
        _ThGazeClose ("Gaze close marks", Vector) = (0.40, 0.52, 0.29, 0)
        [HideInInspector] _Rad0_0 ("Radar ring 0, slices 0-3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad0_1 ("Radar ring 0, slices 4-7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad0_2 ("Radar ring 0, slices 8-11", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad1_0 ("Radar ring 1, slices 0-3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad1_1 ("Radar ring 1, slices 4-7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad1_2 ("Radar ring 1, slices 8-11", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_0 ("Radar ring 2, slices 0-3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_1 ("Radar ring 2, slices 4-7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_2 ("Radar ring 2, slices 8-11", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_3 ("Radar ring 2, slices 12-15", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_4 ("Radar ring 2, slices 16-19", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_5 ("Radar ring 2, slices 20-23", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_0 ("Radar ring 3, slices 0-3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_1 ("Radar ring 3, slices 4-7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_2 ("Radar ring 3, slices 8-11", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_3 ("Radar ring 3, slices 12-15", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_4 ("Radar ring 3, slices 16-19", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_5 ("Radar ring 3, slices 20-23", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_0 ("Radar ring 4, slices 0-3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_1 ("Radar ring 4, slices 4-7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_2 ("Radar ring 4, slices 8-11", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_3 ("Radar ring 4, slices 12-15", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_4 ("Radar ring 4, slices 16-19", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_5 ("Radar ring 4, slices 20-23", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_6 ("Radar ring 4, slices 24-27", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_7 ("Radar ring 4, slices 28-31", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_8 ("Radar ring 4, slices 32-35", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_9 ("Radar ring 4, slices 36-39", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_10 ("Radar ring 4, slices 40-43", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_11 ("Radar ring 4, slices 44-47", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ScanR ("Radar: scan wave radius (m, 100 = no scan)", Float) = 100
        _Pair ("Radar: neighbour pair sum above which two heads, rings 0-3 (xyzw)", Vector) = (1.65, 0.92, 0.40, 0.32)
        _PairFar ("Radar: same, far ring", Float) = 0.26
        _CellR ("Radar: cell receiver radius (m), rings 0-3", Vector) = (0.58, 0.79, 1.09, 1.77)
        _CellRFar ("Radar: same, far ring", Float) = 3
        _PairK ("Radar: head radius (m), pair margin", Vector) = (0.12, 0.08, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" "VRCFallback" = "Hidden" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            // PW_BAKE: only on the build-time bake's temporary copies (PulseWatchBake) — hides what changes (plate text and gauge
            // fill; disc sweep, scan front, outer ring, ticks, +, count tab), which the light drip draws live. No saved material
            // has it, so avatar builds strip the variant and ship only the PW_LIVE 1 code
            #pragma shader_feature_local _ PW_BAKE
            #ifdef PW_BAKE
                #define PW_LIVE 0
            #else
                #define PW_LIVE 1
            #endif
            #include "UnityCG.cginc"
            #include "PulseWatchTime.cginc"

            float4 _Color, _WpColor, _LineColor, _Back;
            float _Mode, _Aspect, _Radar, _DepthBias, _Voice, _CountOn, _FpsOn, _CoordOn;
            float _Press, _PressOn, _Hold, _Laser, _Icon, _Reveal, _PressR, _PressD;
            float _Muted, _WpSpan, _WpUnit, _Pend, _SesOn, _SessionSec;
            static const float3 Red = float3(1.0, 0.24, 0.18);
            static const float3 Warm = float3(1.0, 0.86, 0.38);
            static const float3 Paper = float3(1.0, 0.95, 0.86);   // D51: the lit flashlight tab (the light's colour)
            static const float3 Ink = float3(0.07, 0.07, 0.08);    // and the icon on a lit tab
            float _DbgFace, _DbgGaze, _DbgState, _DbgSens;
            float _Layered;
            // the disc layer being drawn (D42): -1 = all (a flat disc); set per fragment from the layered mesh
            static float gLayer = -1.0;
            float Lay (float k) { return gLayer < -0.5 || abs(gLayer - k) < 0.5 ? 1.0 : 0.0; }
            float4 _ThFaceOpen, _ThFaceClose, _ThGazeOpen, _ThGazeClose;
            float4 _Rad0_0, _Rad0_1, _Rad0_2, _Rad1_0, _Rad1_1, _Rad1_2, _Rad2_0, _Rad2_1, _Rad2_2, _Rad2_3, _Rad2_4, _Rad2_5, _Rad3_0, _Rad3_1, _Rad3_2, _Rad3_3, _Rad3_4, _Rad3_5, _Rad4_0, _Rad4_1, _Rad4_2, _Rad4_3, _Rad4_4, _Rad4_5, _Rad4_6, _Rad4_7, _Rad4_8, _Rad4_9, _Rad4_10, _Rad4_11;
            float _ScanR;
            float4 _Pair, _CellR, _PairK;
            float _PairFar, _CellRFar;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 d1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 wp : TEXCOORD1; nointerpolation uint2 cells : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            static uint2 gCells = uint2(0u, 0u);   // B: the occupied cells, decided once per vertex (bit = band x 12 + dir)

            // 1 on a line of half-width w around d = 0, with a soft edge in screen pixels
            float Line (float d, float w) { float aa = max(fwidth(d), 1e-5); return saturate((w - abs(d)) / aa + 0.5); }
            float Box (float2 p, float2 lo, float2 hi) { float2 aa = max(fwidth(p), 1e-5); float2 a = saturate((p - lo) / aa + 0.5) * saturate((hi - p) / aa + 0.5); return a.x * a.y; }
            float Fill (float d) { return saturate(-d / max(fwidth(d), 1e-5) + 0.5); }
            float4 Over (float4 dst, float3 rgb, float a) { return float4(rgb * a + dst.rgb * (1.0 - a), a + dst.a * (1.0 - a)); }
            float4 Add (float4 dst, float3 rgb, float k) { return float4(dst.rgb + rgb * k, dst.a); }
            float Hash (float x) { return frac(sin(x * 91.345) * 47453.5453); }
            float Pick (float4 v, float i) { return i < 0.5 ? v.x : (i < 1.5 ? v.y : v.z); }

            // ---- position and head count (D22) ----

            // ---- radar cells (D23) ----

            #include "PulseWatchCells.cginc"

            uint2 CellBitsRaw ()
            {
                LoadRad();
                uint2 bits = uint2(0u, 0u);
                [loop] for (uint d = 0u; d < 12u; d++)
                    [loop] for (uint b = 0u; b < 5u; b++)
                        if (CellShown(d, b) > 0.5) { uint k = b * 12u + d; if (k < 32u) bits.x |= 1u << k; else bits.y |= 1u << (k - 32u); }
                return bits;
            }
            float CellBit (uint k) { return (float)(((k < 32u ? gCells.x : gCells.y) >> (k & 31u)) & 1u); }
            uint Pop (uint x) { x = x - ((x >> 1) & 0x55555555u); x = (x & 0x33333333u) + ((x >> 2) & 0x33333333u); return (((x + (x >> 4)) & 0x0F0F0F0Fu) * 0x01010101u) >> 24; }   // countbits is SM5 only
            uint CountCells () { return Pop(gCells.x) + Pop(gCells.y); }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp;
                float4 vtx = v.vertex;
                // 4 (D52): the raised top sinks with the fingertip
                if (abs(_Mode - 4.0) < 0.5 && v.d1.y > 0.5) vtx.y = clamp((1.0 - _Press) * _PressR - _PressD, vtx.y * 0.25, vtx.y);
                if (abs(_Mode - 3.0) < 0.5)
                {
                    // beam: along the object's Z, widened across the view
                    float3 axis = unity_ObjectToWorld._m02_m12_m22;
                    float width = length(unity_ObjectToWorld._m00_m10_m20);
                    float3 mid = unity_ObjectToWorld._m03_m13_m23 + axis * v.vertex.z;
                    float3 side = cross(axis, _WorldSpaceCameraPos - mid);
                    side = dot(side, side) > 1e-12 ? normalize(side) : float3(1, 0, 0);
                    wp = mid + side * v.vertex.x * width;
                }
                else wp = mul(unity_ObjectToWorld, vtx).xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                // depth only: the same point pulled toward the camera, its depth put back at the real w (x, y untouched)
                float3 toCam = _WorldSpaceCameraPos - wp;
                float dist = length(toCam);
                float4 near = mul(UNITY_MATRIX_VP, float4(wp + toCam * (min(_DepthBias, dist * 0.8) / max(dist, 1e-5)), 1.0));
                o.pos.z = near.z / near.w * o.pos.w;
                o.uv = v.uv;
                // 5: the vertex is in WpEcho's space (the skinned quad's root bone, Face's axes and units moved by the offset)
                if (abs(_Mode - 5.0) < 0.5) o.wp = (float3((v.uv.x - 0.5) * _WpSpan, 0.0, (v.uv.y - 0.5) * _WpSpan) - v.vertex.xyz).xz * _WpUnit;
                // 6: the object matrix is the radar frame's — its +Z is where the wearer faces
                if (abs(_Mode - 6.0) < 0.5) { float3 f = unity_ObjectToWorld._m02_m12_m22; o.wp = float2(atan2(f.x, f.z) * 57.29578, 0.0); }
                if (abs(_Mode - 4.0) < 0.5) o.wp = v.d1.xy;
                // 1: the layer of this quad of the layered disc (D42)
                if (abs(_Mode - 1.0) < 0.5) o.wp = float2(_Layered > 0.5 ? v.d1.x : -1.0, 0.0);
                // B: the disc decides its 60 cells here (24 vertices) instead of on every pixel
                if (abs(_Mode - 1.0) < 0.5 && _Radar > 0.5 && (_Layered < 0.5 || abs(v.d1.x - 2.0) < 0.5 || abs(v.d1.x - 5.0) < 0.5)) o.cells = CellBitsRaw();   // the cells' layer and the count tab's
                return o;
            }


            // a small head-and-shoulders mark, size h, its bottom left at o
            float Person (float2 p, float2 o, float h)
            {
                float2 q = (p - o) / h;
                float head = length(q - float2(0.32, 0.70)) - 0.17;
                float body = max(length((q - float2(0.32, 0.0)) / float2(1.0, 1.15)) - 0.32, -q.y);
                return min(head, body) * h;
            }

            // one axis: the letter in accent (0.8 size) then the value; moves x past it
            float4 Axis (float4 o, float2 p, float2 dpx, float2 dpy, inout float x, float by, float size, uint axis, uint gs[PW_MAX_GLYPHS], uint n)
            {
                uint L[PW_MAX_GLYPHS];
                [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) L[i] = PW_SPACE;
                L[0] = PW_X + axis;
                o = Over(o, _Color.rgb, PwFill(PwText(p, dpx, dpy, x, by, size * 0.8, L, 1u), dpx, dpy));
                x += (PwAdvance(PW_X) * 0.8 + 0.12) * size;
                o = Over(o, _LineColor.rgb, PwFill(PwText(p, dpx, dpy, x, by, size, gs, n), dpx, dpy));
                x += PwWidth(gs, n, size);
                return o;
            }

            // "X -23.4  Y 1.2  Z 105.8" (metres to 0.1) of this renderer from x0, at most `size`, shrunk to end by x1
            float4 Coords (float4 o, float2 p, float2 dpx, float2 dpy, float x0, float x1, float by, float size)
            {
                float3 w = unity_ObjectToWorld._m03_m13_m23;
                uint gx[PW_MAX_GLYPHS], gy[PW_MAX_GLYPHS], gz[PW_MAX_GLYPHS]; uint nx, ny, nz;
                PwMetres(w.x, gx, nx); PwMetres(w.y, gy, ny); PwMetres(w.z, gz, nz);
                const float gap = 0.57;
                float unit = 3.0 * (PwAdvance(PW_X) * 0.8 + 0.12) + PwWidth(gx, nx, 1.0) + PwWidth(gy, ny, 1.0) + PwWidth(gz, nz, 1.0) + 2.0 * gap;
                size = min(size, (x1 - x0) / unit);
                float x = x0;
                o = Axis(o, p, dpx, dpy, x, by, size, 0u, gx, nx); x += gap * size;
                o = Axis(o, p, dpx, dpy, x, by, size, 1u, gy, ny); x += gap * size;
                return Axis(o, p, dpx, dpy, x, by, size, 2u, gz, nz);
            }

            // ---- D26 proto marks (docs/pulse-watch.md 10-29) ----
            float Diamond (float2 p, float2 c, float s) { return (abs(p.x - c.x) + abs(p.y - c.y) - s) * 0.7071; }
            // microphone with a slash, height h, its bottom centre at c
            float Mic (float2 p, float2 c, float h)
            {
                float2 q = (p - c) / h;
                float body = length(float2(q.x, q.y - clamp(q.y, 0.42, 0.72))) - 0.17;
                float cup = max(abs(length(q - float2(0.0, 0.50)) - 0.29) - 0.045, q.y - 0.50);
                float stem = max(abs(q.x) - 0.045, abs(q.y - 0.12) - 0.10);
                float base = max(abs(q.x) - 0.20, abs(q.y - 0.03) - 0.045);
                float2 a = float2(-0.36, 0.0), b = float2(0.36, 1.0);
                float2 pa = q - a, ba = b - a;
                float slash = length(pa - ba * saturate(dot(pa, ba) / dot(ba, ba))) - 0.05;
                return min(min(min(body, cup), min(stem, base)), slash) * h;
            }
            // hourglass, height h, centre c
            float Hourglass (float2 p, float2 c, float h)
            {
                float2 q = (p - c) / h;
                float glass = max(abs(q.y) - 0.42, abs(q.x) - 0.36 * abs(q.y) - 0.05);
                float caps = max(abs(q.x) - 0.24, abs(abs(q.y) - 0.46) - 0.05);
                return min(glass, caps) * h;
            }
            // one glyph centred at c (caps centre), size s
            float4 Letter (float4 o, float2 p, float2 dpx, float2 dpy, float2 c, float s, uint g, float3 col)
            {
                uint L[PW_MAX_GLYPHS];
                [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) L[i] = PW_SPACE;
                L[0] = g;
                return Over(o, col, PwFill(PwText(p, dpx, dpy, c.x - 0.5 * PwAdvance(g) * s, c.y - 0.285 * s, s, L, 1u), dpx, dpy));
            }
            float2 OnRing (float deg, float r) { float a = radians(deg); return r * float2(sin(a), cos(a)); }

            // this client's frames per second: the number in ink, "FPS" after it in accent (0.62 size), from x at baseline by
            float4 Fps (float4 o, float2 p, float2 dpx, float2 dpy, float x, float by, float size)
            {
                uint gs[PW_MAX_GLYPHS]; uint n;
                PwFps(gs, n);
                o = Over(o, _LineColor.rgb, PwFill(PwText(p, dpx, dpy, x, by, size, gs, n), dpx, dpy));
                x += PwWidth(gs, n, size) + 0.10 * size;
                uint L[PW_MAX_GLYPHS];
                [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) L[i] = PW_SPACE;
                L[0] = PW_F; L[1] = PW_F + 1u; L[2] = PW_F + 2u;
                return Over(o, _Color.rgb, PwFill(PwText(p, dpx, dpy, x, by, size * 0.62, L, 3u), dpx, dpy));
            }

            float4 Plate (float2 uv)
            {
                float A = _Aspect;
                float2 p = float2(uv.x * A, uv.y);                 // x right 0..A, y up 0..1
                float2 dpx = ddx(p), dpy = ddy(p);
                float3 ink = _LineColor.rgb, acc = _Color.rgb;
                // bottom right corner cut at 45 deg
                float cutD = (A - p.x) + p.y - 0.13;
                float inside = Box(p, 0.0, float2(A, 1.0)) * saturate(cutD / max(fwidth(cutD), 1e-5) + 0.5);
                float4 o = 0;
                o = Over(o, _Back.rgb, inside * _Back.a);
                float fr = max(max(Line(p.x - 0.005, 0.0035), Line(p.y - 0.995, 0.0035)), max(Line(p.x - (A - 0.005), 0.0035) * step(0.13, p.y), Line(p.y - 0.005, 0.0035) * step(p.x, A - 0.13)));
                fr = max(fr, Line(cutD / 1.414, 0.0035) * step(A - 0.14, p.x) * step(p.y, 0.14));
                o = Over(o, ink, fr * 0.45 * inside);
                o = Over(o, acc, Fill((A - p.x) + p.y - 0.085) * inside * 0.95);   // accent in the cut corner

                PwTime t = PwGetTime();
                uint gs[PW_MAX_GLYPHS]; uint n;
                // tab (top left): accent block with the date in dark
                PwDate(t, gs, n);
                float ts = 0.13;
                float tw = PwWidth(gs, n, ts);
                o = Over(o, acc, Box(p, float2(0.0, 0.82), float2(0.11 + tw, 1.0)) * inside);
                if (PW_LIVE) o = Over(o, _Back.rgb, PwFill(PwText(p, dpx, dpy, 0.06, 0.86, ts, gs, n), dpx, dpy));
                float cx0 = 0.14 + tw;
                // the wearer's position right of the tab (D22: the plate's own world position)
                if (_CoordOn > 0.5 && PW_LIVE) o = Coords(o, p, dpx, dpy, cx0, A - 0.12, 0.865, 0.105);
                // big HH:MM, seconds in accent
                float big = 0.52, by = 0.30, x0 = 0.07;
                PwHourMinute(t, gs, n);
                float wBig = PwWidth(gs, n, big);
                if (PW_LIVE) o = Over(o, ink, PwFill(PwText(p, dpx, dpy, x0, by, big, gs, n), dpx, dpy));
                uint ss[PW_MAX_GLYPHS]; uint sn;
                PwSeconds(t, ss, sn);
                if (PW_LIVE) o = Over(o, acc, PwFill(PwText(p, dpx, dpy, x0 + wBig + 0.04, by, 0.24, ss, sn), dpx, dpy));
                // this client's FPS above the seconds (D25), the wearer's copy only — on others' copies it would be the viewer's
                if (_FpsOn > 0.5 && PW_LIVE) o = Fps(o, p, dpx, dpy, x0 + wBig + 0.04, 0.60, 0.13);
                // seconds gauge: 60 segments under the time, lit up to now
                // session time right of it (D27 1), the wearer's copy only — the gauge gets shorter
                float gx0 = x0, gx1 = _SesOn > 0.5 ? A - 0.62 : A - 0.20;
                if (_SesOn > 0.5)
                {
                    uint es[PW_MAX_GLYPHS]; uint en;
                    PwElapsed(_SessionSec, es, en);
                    o = Over(o, acc, Fill(Hourglass(p, float2(A - 0.55, 0.155), 0.10)));
                    o = Over(o, ink, PwFill(PwText(p, dpx, dpy, A - 0.48, 0.115, 0.12, es, en), dpx, dpy));
                }
                float u = (p.x - gx0) / (gx1 - gx0);
                float inGauge = Box(p, float2(gx0, 0.12), float2(gx1, 0.175)) * step(0.25, frac(u * 60.0));
                float lit = step(floor(u * 60.0), t.live && PW_LIVE ? (float)t.second - 0.5 : -1.0);
                o = Over(o, acc, inGauge * lit * 0.95);
                o = Over(o, ink, inGauge * (1.0 - lit) * 0.18);
                o = Over(o, ink, Line(p.x - gx0 - (gx1 - gx0) * 0.25, 0.003) * step(0.10, p.y) * step(p.y, 0.195) * 0.5);
                // corner brackets (top right, bottom left) and code bars (right of the gauge)
                float br = max(Box(p, float2(A - 0.10, 0.985), float2(A - 0.015, 0.993)), Box(p, float2(A - 0.023, 0.90), float2(A - 0.015, 0.993)));
                br = max(br, max(Box(p, float2(0.015, 0.007), float2(0.10, 0.015)), Box(p, float2(0.015, 0.007), float2(0.023, 0.10))));
                o = Over(o, ink, br * 0.8);
                float bx = (p.x - (A - 0.17)) / 0.11;
                if (_SesOn < 0.5)
                    o = Over(o, ink, step(0.0, bx) * step(bx, 1.0) * step(0.12, p.y) * step(p.y, 0.24) * step(0.45, Hash(floor(bx * 14.0) + 3.0)) * 0.35);
                return o;
            }

            // radar part of the disc: inside this radius (disc radius 1); band edges as a share of it
            static const float RadarR = 0.80;
            static const float Edge[6] = { 0.12, 0.31, 0.50, 0.69, 0.86, 1.0 };
            static const float Metres[6] = { 0.4, 1.5, 3.0, 5.0, 8.0, 14.0 };

            float4 Disc (float2 uv)
            {
                float2 q = (uv - 0.5) * 2.0;                       // x right, y = 12 o'clock
                float2 dqx = ddx(q), dqy = ddy(q);
                float r = length(q);
                float deg = atan2(q.x, q.y) * 57.29578;
                deg = deg < 0.0 ? deg + 360.0 : deg;
                float arcK = 0.0174533 * r;                        // degrees -> length at this radius
                float3 ink = _LineColor.rgb, acc = _Color.rgb;
                float disc = saturate((0.995 - r) / max(fwidth(r), 1e-5) + 0.5);
                float4 o = 0;
                o = Over(o, _Back.rgb, disc * _Back.a * (0.85 + 0.15 * smoothstep(0.3, 0.99, r)) * Lay(0.0));

                // outer ring: four arcs broken at the diagonals, "+" in each gap; ticks every 5 deg (short) and 30 deg (long)
                float gapDeg = abs(frac((deg - 45.0) / 90.0 + 0.5) - 0.5) * 90.0;
                bool muted = _Muted > 0.5;
                // muted blinks slowly (D7's proposal, taken 10-51): the red dims to 35% and back every 1.6 s, so it reads without the colour
                float blink = muted ? 0.675 + 0.325 * cos(_Time.y * 6.2832 / 1.6) : 1.0;
                // the ring layer only: the layer is constant over each of the disc's quads, so the branch keeps fwidth whole
                if (Lay(4.0) > 0.5 && PW_LIVE)   // bake: the ring layer comes live from the drip (mute / voice)
                {
                    float3 ringC = muted ? Red * blink : ink, plusC = muted ? Red * blink : acc;
                    o = Add(o, ringC, Line(r - 0.965, 0.007) * step(7.0, gapDeg) * 0.85);
                    [unroll] for (int k = 0; k < 4; k++)
                    {
                        float a = radians(45.0 + 90.0 * k);
                        float2 d = abs(q - 0.965 * float2(sin(a), cos(a)));
                        o = Add(o, plusC, max(Box(d, -0.004, float2(0.026, 0.004)), Box(d, -0.004, float2(0.004, 0.026))) * 0.95);
                    }
                    float t5 = abs(frac(deg / 5.0 + 0.5) - 0.5) * 5.0 * arcK;
                    float t30 = abs(frac(deg / 30.0 + 0.5) - 0.5) * 30.0 * arcK;
                    o = Add(o, ringC, Line(t5, 0.0025) * step(0.905, r) * step(r, 0.935) * 0.45);
                    o = Add(o, ringC, Line(t30, 0.004) * step(0.875, r) * step(r, 0.94) * 0.9);
                    // the wearer's voice (D20): the outer ring turns to the accent, thickens and glows with the built-in Voice. D58: a
                    // solid accent line over the white one (it was added onto it, so the white still showed), thicker, a wider glow and
                    // the ticks lit too — the FX maps Voice on a steeper curve and fades it out
                    if (_Voice > 0.001)
                    {
                        float v = saturate(_Voice);
                        float w = 0.007 * (1.0 + 3.0 * v);
                        o = Over(o, acc, Line(r - 0.965, w) * step(7.0, gapDeg) * saturate(v * 1.8));
                        o = Add(o, acc, exp(-abs(r - 0.965) / (0.02 + 0.03 * v)) * step(r, 1.0) * v * 0.85);
                        o = Add(o, acc, (Line(t5, 0.0025) * step(0.905, r) * step(r, 0.935) * 0.5 + Line(t30, 0.004) * step(0.875, r) * step(r, 0.94)) * v * 0.7);
                    }
                }
                if (Lay(4.0) > 0.5)
                {
                    // heading: accent wedge at 12 on the ring
                    float2 hq = q - float2(0.0, 0.975);
                    o = Over(o, acc, Fill(max(abs(hq.x) * 1.7 + hq.y - 0.02, -hq.y)));
                }

                // the radar draws on layers 1-3 (grid, cells, sweep); the backing, the outer ring and the centre layers skip it
                if (_Radar > 0.5 && (gLayer < -0.5 || (gLayer > 0.5 && gLayer < 3.5)))
                {
                    float rr = r / RadarR;
                    float inGrid = step(Edge[0], rr) * step(rr, 1.0);
                    // band rings (dashed, the last solid) and cell borders (faint spokes between the directions)
                    [unroll] for (int b = 1; b < 6; b++)
                    {
                        float dash = b == 5 ? 1.0 : step(0.45, frac(deg / 3.0));
                        o = Add(o, ink, Line(r - Edge[b] * RadarR, 0.0028) * dash * (b == 5 ? 0.7 : 0.35) * Lay(1.0));
                    }
                    float spoke = abs(frac((deg - 15.0) / 30.0 + 0.5) - 0.5) * 30.0 * arcK;
                    o = Add(o, ink, Line(spoke, 0.002) * inGrid * 0.16 * Lay(1.0));

                    // the cell under this pixel (dir 0 centred on 12 o'clock)
                    uint dir = (uint)floor(frac(deg / 360.0 + 1.0 / 24.0) * 12.0) % 12u;
                    float band = -1.0, lo = 0.0, hi = 0.0, reached = 0.0, sweepR = 0.0;
                    [unroll] for (int i = 0; i < 5; i++)
                    {
                        if (rr >= Edge[i] && rr < Edge[i + 1]) { band = i; lo = Edge[i] * RadarR; hi = Edge[i + 1] * RadarR; reached = step(Metres[i], _ScanR); }
                        if (_ScanR >= Metres[i] && _ScanR < Metres[i + 1]) sweepR = lerp(Edge[i], Edge[i + 1], (_ScanR - Metres[i]) / (Metres[i + 1] - Metres[i])) * RadarR;
                    }
                    // the sweep's layer (3) never reads it: skip the slice lookups there
                    float on = band < -0.5 || abs(gLayer - 3.0) < 0.5 ? 0.0 : CellBit((uint)band * 12u + dir) * reached;
                    // occupied cell: hatched fill inset from its borders + a bright outer edge
                    float cdeg = abs(frac(deg / 30.0 + 0.5) - 0.5) * 30.0;
                    float inset = Box(float2(cdeg * arcK, r), float2(-1.0, lo + 0.012), float2(15.0 * arcK - 0.012, hi - 0.012));
                    float hatch = step(0.5, frac((q.x + q.y) * 34.0));
                    o = Add(o, acc, inset * on * (0.25 + 0.35 * hatch) * Lay(2.0));
                    o = Add(o, acc, Line(r - (hi - 0.014), 0.006) * step(cdeg * arcK, 15.0 * arcK - 0.012) * on * 1.1 * Lay(2.0));
                    // scan front
                    if (PW_LIVE) o = Add(o, acc, Line(r - sweepR, 0.006) * step(0.001, sweepR) * 0.9 * Lay(3.0));
                    // turning sweep with a fading trail: decoration, one turn in 4 s clockwise by the shader's clock (D55)
                    float sw = frac(_Time.y / 4.0) * 360.0;
                    float behind = frac((sw - deg) / 360.0);
                    float sd = abs(frac((deg - sw) / 360.0 + 0.5) - 0.5) * 360.0 * arcK;
                    if (PW_LIVE) o = Add(o, acc, exp(-behind * 360.0 / 22.0) * inGrid * 0.16 * Lay(3.0));
                    if (PW_LIVE) o = Add(o, acc, Line(sd, 0.003) * inGrid * 0.6 * Lay(3.0));
                    // ring distances (m) on the 1 o'clock spoke, small and dim, hidden under a lit cell (the grid's layer)
                    if (Lay(1.0) > 0.5)
                    {
                    uint gs[PW_MAX_GLYPHS];
                    [unroll] for (uint g = 0; g < PW_MAX_GLYPHS; g++) gs[g] = PW_SPACE;
                    float ls = 0.055, dl = PW_SPREAD * ls, lx = 0.035;
                    gs[0] = 3u;             dl = min(dl, PwText(q, dqx, dqy, lx, Edge[2] * RadarR - 0.05, ls, gs, 1u));
                    gs[0] = 5u;             dl = min(dl, PwText(q, dqx, dqy, lx, Edge[3] * RadarR - 0.05, ls, gs, 1u));
                    gs[0] = 8u;             dl = min(dl, PwText(q, dqx, dqy, lx, Edge[4] * RadarR - 0.05, ls, gs, 1u));
                    gs[0] = 1u; gs[1] = 4u; dl = min(dl, PwText(q, dqx, dqy, lx, Edge[5] * RadarR - 0.05, ls, gs, 2u));
                    o = Add(o, ink, PwFill(dl, dqx, dqy) * 0.5 * (1.0 - on));
                    }
                }

                // muted: a slashed mic at 6 o'clock over the ring (D27; D57: no dark disc behind it)
                if (muted && Lay(5.0) > 0.5)
                    o = Over(o, Red * blink, Fill(Mic(q, float2(0.0, -0.995), 0.12)));
                // the wearer: accent arrow at the centre pointing at 12
                float2 sp = q - float2(0.0, -0.02);
                o = Over(o, acc, Fill(max(abs(sp.x) * 1.9 + sp.y, -sp.y * 2.2 - 0.06) - 0.07) * Lay(5.0));
                o *= disc;
                // occupied-cell count: a tab outside the ring at the top left (D22), the wearer's copy only
                if (_CountOn > 0.5 && Lay(5.0) > 0.5 && PW_LIVE)
                {
                    float outer = step(1.0, r);
                    o = Over(o, _Back.rgb, Box(q, float2(-1.0, 0.74), float2(-0.62, 1.0)) * outer * _Back.a);
                    o = Over(o, acc, Box(q, float2(-1.0, 0.74), float2(-0.98, 1.0)) * outer * 0.95);
                    o = Over(o, ink, Fill(Person(q, float2(-0.95, 0.80), 0.105)) * 0.9);
                    uint cnt = 0u;
                    if (q.x < -0.55 && q.y > 0.68) cnt = CountCells();      // 120 slices x 3-4 lookups: only under the tab
                    uint cs[PW_MAX_GLYPHS]; uint cn;
                    PwCount(cnt, cs, cn);
                    o = Over(o, acc, PwFill(PwText(q, dqx, dqy, -0.865, 0.80, 0.17, cs, cn), dqx, dqy));
                }
                return o;
            }

            // 5: the waypoint on the disc — off = the mark's offset from the eyes in the radar frame (m; x right, y = ahead = 12 o'clock)
            float4 WpDisc (float2 uv, float2 off)
            {
                float2 q = (uv - 0.5) * 2.0;
                float2 dqx = ddx(q), dqy = ddy(q);
                float r = length(q);
                float dist = length(off);
                float2 wd = dist > 1e-4 ? off / dist : float2(0.0, 1.0);
                // the radar's own ring scale: inside 0.4 m on the first edge, past 14 m on the last (outline only)
                float rr = RadarR;
                [unroll] for (int wi = 0; wi < 5; wi++)
                    if (dist >= Metres[wi] && dist < Metres[wi + 1]) rr = lerp(Edge[wi], Edge[wi + 1], (dist - Metres[wi]) / (Metres[wi + 1] - Metres[wi])) * RadarR;
                rr = dist < Metres[0] ? Edge[0] * RadarR : rr;
                uint ds[PW_MAX_GLYPHS]; uint dn;
                PwDistance(dist, ds, dn);
                float dw = PwWidth(ds, dn, 0.075);
                float2 c = wd * rr;
                float dmd = Diamond(q, c, 0.055);
                float4 o = 0;
                o = Over(o, _Back.rgb, Fill(dmd - 0.015));
                o = Over(o, _WpColor.rgb, Fill(dist >= Metres[5] ? abs(dmd) - 0.008 : dmd));
                // the distance beside it, on the side toward the centre near the right edge
                float2 lc = c + (c.x > 0.35 ? float2(-0.08 - dw, -0.03) : float2(0.08, -0.03));
                o = Over(o, _Back.rgb, Box(q, lc - float2(0.015, 0.035), lc + float2(dw + 0.015, 0.075)) * 0.9);
                o = Over(o, _WpColor.rgb, PwFill(PwText(q, dqx, dqy, lc.x, lc.y, 0.075, ds, dn), dqx, dqy));
                return o * saturate((0.995 - r) / max(fwidth(r), 1e-5) + 0.5);
            }

            // 6: the compass on the disc — north is -heading from the wearer's front (clockwise degrees on the disc)
            float4 Compass (float2 uv, float heading)
            {
                float2 q = (uv - 0.5) * 2.0;
                float2 dqx = ddx(q), dqy = ddy(q);
                float r = length(q);
                float3 acc = _Color.rgb;
                float2 c = OnRing(-heading, 0.965);
                float2 dirN = normalize(c);
                float2 t = q - c;
                float tri = max(abs(dot(t, float2(dirN.y, -dirN.x))) * 1.6 + dot(t, dirN), -dot(t, dirN) - 0.05) - 0.03;
                float4 o = 0;
                o = Over(o, acc, Fill(tri));
                o = Letter(o, q, dqx, dqy, OnRing(-heading, 0.86), 0.09, PW_N, acc);
                return o * saturate((0.995 - r) / max(fwidth(r), 1e-5) + 0.5);
            }

            float4 Meter (float2 uv)
            {
                bool top = uv.y > 0.5;
                float y = frac(uv.y * 2.0);
                float v = top ? _DbgFace : _DbgGaze;
                float mo = Pick(top ? _ThFaceOpen : _ThGazeOpen, _DbgSens);
                float mc = Pick(top ? _ThFaceClose : _ThGazeClose, _DbgSens);
                float inBar = step(0.2, y) * step(y, 0.8);
                float bar = inBar * step(uv.x, v);
                float3 col = _DbgState > 0.75 ? _Color.rgb : (_DbgState > 0.25 ? float3(1, 0.6, 0.2) : float3(0.85, 0.85, 0.85));
                float tickO = Line(uv.x - mo, 0.008);
                float tickC = Line(uv.x - mc, 0.006);
                float frame = inBar * max(Line(y - 0.2, 0.02), Line(y - 0.8, 0.02));
                float3 rgb = col * bar + float3(1, 1, 1) * tickO + float3(0.5, 0.5, 0.5) * tickC + col * 0.4 * frame;
                float a = saturate(max(max(bar * 0.85, tickO), max(tickC * 0.7, frame * 0.4)));
                return float4(rgb * a, a);
            }

            float4 Beam (float2 uv)
            {
                return float4(_LineColor.rgb * 0.55 * smoothstep(0.0, 0.6, uv.y), 0.0);
            }

            float4 WpButton (float2 uv)
            {
                float A = _Aspect;
                float2 p = float2(uv.x * A, uv.y);
                float2 dpx = ddx(p), dpy = ddy(p);
                float cut = 0.22;
                // cut-corner tab: top right and bottom left corners clipped
                float body = Box(p, float2(0.0, 0.0), float2(A, 1.0));
                body *= saturate(((A + 1.0 - cut) - (p.x + p.y)) / max(fwidth(p.x + p.y), 1e-5) + 0.5);
                body *= saturate(((p.x + p.y) - cut) / max(fwidth(p.x + p.y), 1e-5) + 0.5);
                bool down = _Press > _PressOn;
                bool lamp = _Icon > 0.5;
                bool on = _Laser > 0.5;
                float3 tint = lamp ? Warm : _WpColor.rgb;
                float3 lit = lamp ? Paper : _WpColor.rgb;
                float hover = saturate(_Press / max(_PressOn, 1e-3));
                float4 o = 0;
                // on: inverted (D51) — filled, a shade darker as the finger goes in and pressed
                float3 fill = on ? lit * (down ? 0.6 : lerp(1.0, 0.8, hover)) : (down ? tint : _Back.rgb);
                o = Over(o, fill, body * (on || down ? 0.95 : _Back.a));
                // edge: brighter as the finger goes in (hover), a pulsing cyan outline while the laser is on
                float inner = Box(p, float2(0.05, 0.08), float2(A - 0.05, 0.92));
                inner *= saturate(((A + 1.0 - cut - 0.08) - (p.x + p.y)) / max(fwidth(p.x + p.y), 1e-5) + 0.5);
                inner *= saturate(((p.x + p.y) - cut - 0.08) / max(fwidth(p.x + p.y), 1e-5) + 0.5);
                float edge = saturate(body - inner);
                if (!down && !on) o = Add(o, _LineColor.rgb, edge * (0.35 + 0.65 * hover));
                if (!down && !on) o = Over(o, tint, Box(p, float2(0.0, 0.0), float2(0.07, 1.0 - cut)) * body);
                float3 ic = on ? Ink : (down ? _Back.rgb : tint);
                if (lamp)
                {
                    // icon: a lamp with eight rays, dim while the light is off
                    float2 lc = float2(A * 0.5, 0.5), q = p - lc;
                    float rr = length(q);
                    float spoke = abs(frac(atan2(q.y, q.x) / 6.2831853 * 8.0 + 0.5) - 0.5);   // 0 on a ray
                    float fr = max(fwidth(rr), 1e-5);
                    float ray = saturate((0.12 - spoke) / max(fwidth(spoke), 1e-4) + 0.5) * saturate((rr - 0.21) / fr + 0.5) * saturate((0.33 - rr) / fr + 0.5);
                    float k = down || on ? 1.0 : 0.55;
                    o = Over(o, ic, Fill(rr - 0.13) * k);
                    o = Over(o, ic, ray * k);
                }
                else
                {
                    // icon: a diamond over a floor line
                    float2 c = float2(A * 0.5, 0.56);
                    o = Over(o, ic, Fill(Diamond(p, c, 0.20)));
                    o = Over(o, ic, Box(p, float2(c.x - 0.30, 0.22), float2(c.x + 0.30, 0.27)));
                    // hold: red bar along the bottom
                    o = Over(o, Red, Box(p, float2(0.0, 0.0), float2(A * saturate(_Hold), 0.09)) * body * step(0.001, _Hold));
                }
                return o;
            }

            // opening (D47): only the circle of radius _Reveal, a warm ring fading out on its edge as it reaches the rim
            float4 Reveal (float4 c, float2 uv, bool ring)
            {
                if (_Reveal > 1.045) return c;
                float r = length(uv - 0.5) * 2.0;
                c *= saturate((_Reveal - r) / max(fwidth(r), 1e-4) + 0.5);
                if (ring) c.rgb += Warm * 0.9 * exp(-(r - _Reveal) * (r - _Reveal) / (0.03 * 0.03)) * (1.0 - smoothstep(0.95, 1.045, _Reveal));
                return c;
            }

            // the tab's side walls (D52): its face colour, darker toward the base, a lit lip along the top
            float4 ButtonSide (float h)
            {
                bool down = _Press > _PressOn, on = _Laser > 0.5;
                float3 tint = _Icon > 0.5 ? Warm : _WpColor.rgb;
                float3 face = on ? (_Icon > 0.5 ? Paper : _WpColor.rgb) : (down ? tint : _Back.rgb * 2.5);
                float a = on || down ? 0.92 : _Back.a;
                float4 o = float4(face * lerp(0.3, 0.65, h) * a, a);
                float hover = saturate(_Press / max(_PressOn, 1e-3));
                return Add(o, on || down ? face : _LineColor.rgb, smoothstep(0.7, 1.0, h) * (on || down ? 0.2 : 0.15 + 0.3 * hover));
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (_Mode < 0.5) return Plate(i.uv);
                if (_Mode > 5.5) return Reveal(Compass(i.uv, i.wp.x), i.uv, false);
                if (_Mode > 4.5) return Reveal(WpDisc(i.uv, i.wp) * (_Pend > 0.5 ? 0.4 + 0.15 * sin(_Time.y * 6.2832) : 1.0), i.uv, false);
                if (_Mode > 3.5) return i.wp.x > 0.5 ? ButtonSide(i.wp.y) : WpButton(i.uv);
                if (abs(_Mode - 1.0) < 0.5) { gLayer = i.wp.x; gCells = i.cells; return Reveal(Disc(i.uv), i.uv, gLayer < -0.5 || abs(gLayer - 1.0) < 0.5); }
                return _Mode < 2.5 ? Meter(i.uv) : Beam(i.uv);   // A: modes 0 / 1 returned above — the old ternary inlined Plate and Disc a second time
            }
            ENDCG
        }
    }
}
