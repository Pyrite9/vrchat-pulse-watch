// Pyrite9 Pulse Watch — light drip (D41 / D43, docs/pulse-watch.md 10-56, 10-119): a board about the face's normal draws streaks
// down toward the forearm of what the hologram lights. No GrabPass — every streak tap meets the disc / plate plane along the line
// of sight and reads a picture baked at build time from this avatar's own watch (_BakeTex, PulseWatchBake), so a streak falls
// under the line that really glows. What changes is added live:
//   disc (_Live, the disc renderer's second slot so the FX's _Rad* / _ScanR / _Muted / _Voice / _CountOn reach it): sweep, occupied
//     cells (decided per vertex, PulseWatchCells.cginc), the outer ring / ticks / + with mute and voice, the mic and the count tab
//   plate (_Glyph): the time, seconds and second gauge from the Proto's glyph atlas (PulseWatchTime.cginc)
Shader "Pyrite9/PulseWatch/Drip"
{
    Properties
    {
        _BakeTex ("Baked face (built)", 2D) = "black" {}
        _BakeRect ("Baked rect: min X, min Z, size X, size Z (board units)", Vector) = (0, 0, 1, 1)
        _BakeH ("Baked plane height up the normal (board units)", Float) = 0
        _HalfW ("Half width across the view (m)", Float) = 0.1
        _Top ("Board top above the origin (m)", Float) = 0.04
        _Bottom ("Board bottom below the origin (m)", Float) = 0.03
        _Len ("Streak length (m, the longest column)", Float) = 0.02
        _Col ("Streak column width (m)", Float) = 0.002
        _K ("Strength", Float) = 1.2
        _FadeIn ("Hidden within this cos of the normal", Range(0, 1)) = 0.85
        _FadeOut ("Full past this cos of the normal", Range(0, 1)) = 0.6
        _DepthBias ("Draw over things this close in front (m)", Float) = 0
        _AtVertex ("Board on the vertex, not the origin (1: the plate's)", Float) = 0
        _Live ("Disc: add the sweep and the occupied cells", Float) = 0
        _Acc ("Disc: accent", Color) = (0.24, 1, 0.69, 1)
        _LiveK ("Disc: sweep / cell gain", Float) = 1
        _LiveBits ("Disc: cells decided per vertex (1) / per tap (0, v48)", Float) = 1
        _Taps ("Samples per streak (the original: 20)", Float) = 20
        _SkipTop ("Skip the board above the face", Float) = 0
        _Discard ("Discard pixels that add nothing", Float) = 0
        _Glyph ("Plate: add the live time, seconds and gauge from the glyph atlas", Float) = 0
        [NoScaleOffset] _Glyphs ("Glyph atlas (SDF, the Proto's)", 2D) = "black" {}
        _PlateM ("Plate: board units -> text units, rows (m00, m01, m10, m11)", Vector) = (1, 0, 0, 1)
        _PlateT ("Plate: offset x, y, aspect, gauge right end", Vector) = (0, 0, 1.8, 1.6)
        _PlatePx ("Plate: one baked pixel in text units", Float) = 0.004
        _InkBake ("Plate: the time's colour as baked", Color) = (1, 1, 1, 1)
        _AccBake ("Plate: the seconds' colour as baked", Color) = (0, 1, 0.6, 1)
        _UtcOffsetMin ("Wearer UTC offset (minutes)", Integer) = 540
        _DstStart ("Daylight saving start", Vector) = (0, 0, 0, 0)
        _DstEnd ("Daylight saving end", Vector) = (0, 0, 0, 0)
        _DstMin ("Daylight saving shift (minutes)", Integer) = 0
        _Ink ("Disc: ink (the Proto's line colour)", Color) = (0.9, 0.9, 0.86, 1)
        _BackC ("Disc: backing (the Proto's back colour)", Color) = (0, 0.05, 0.04, 0.6)
        _TopH ("Disc: mic / count tab layer height (board units, DiscLayers 5)", Float) = 0.004
        _DiscPx ("Disc: one baked pixel in disc units", Float) = 0.004
        _Muted ("Disc: muted (built-in MuteSelf, from the disc renderer's block)", Float) = 0
        _Voice ("Disc: voice level (from the disc renderer's block)", Float) = 0
        _CountOn ("Disc: count tab on (the wearer's copy)", Float) = 0
        _DynH ("Disc: sweep / cell layer height (board units)", Float) = 0.0027
        _DiscR ("Disc radius (board units)", Float) = 0.0475
        _ScanR ("Scan wave radius (m)", Float) = 100
        _Pair ("Pair proximity, rings 0-3", Vector) = (0, 0, 0, 0)
        _PairFar ("Pair proximity, far ring", Float) = 0
        _CellR ("Cell receiver radius (m), rings 0-3", Vector) = (0.58, 0.79, 1.09, 1.77)
        _CellRFar ("Same, far ring", Float) = 3
        _PairK ("Head radius, pair margin", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad0_0 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad0_1 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad0_2 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad1_0 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad1_1 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad1_2 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_0 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_1 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_2 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_3 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_4 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad2_5 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_0 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_1 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_2 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_3 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_4 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad3_5 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_0 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_1 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_2 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_3 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_4 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_5 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_6 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_7 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_8 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_9 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_10 ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Rad4_11 ("", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+13" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" "VRCFallback" = "Hidden" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma shader_feature_local __ PW_PERTAP
            #include "UnityCG.cginc"

            sampler2D _BakeTex;
            float4 _BakeRect;
            float _BakeH, _HalfW, _Top, _Bottom, _Len, _Col, _K, _FadeIn, _FadeOut, _DepthBias, _AtVertex;
            float _Taps, _SkipTop, _Discard;
            float _Live, _LiveBits, _LiveK, _DynH, _DiscR, _ScanR, _PairFar, _CellRFar;
            float4 _Acc, _Pair, _CellR, _PairK;
            float _Glyph, _PlatePx;
            float _TopH, _DiscPx, _Muted, _Voice, _CountOn;
            float4 _Ink, _BackC;
            static const float3 Red = float3(1.0, 0.24, 0.18);   // the Proto's
            float4 _PlateM, _PlateT, _InkBake, _AccBake;
            float4 _Rad0_0, _Rad0_1, _Rad0_2, _Rad1_0, _Rad1_1, _Rad1_2, _Rad2_0, _Rad2_1, _Rad2_2, _Rad2_3, _Rad2_4, _Rad2_5, _Rad3_0, _Rad3_1, _Rad3_2, _Rad3_3, _Rad3_4, _Rad3_5, _Rad4_0, _Rad4_1, _Rad4_2, _Rad4_3, _Rad4_4, _Rad4_5, _Rad4_6, _Rad4_7, _Rad4_8, _Rad4_9, _Rad4_10, _Rad4_11;

            // ---- copied from PulseWatchProto (radar cells D23 / D36): the same occupied-cell decision ----
            #include "PulseWatchCells.cginc"
            #include "PulseWatchTime.cginc"
            static const float RadarR = 0.80;
            static const float Edge[6] = { 0.12, 0.31, 0.50, 0.69, 0.86, 1.0 };
            static const float Metres[6] = { 0.4, 1.5, 3.0, 5.0, 8.0, 14.0 };

            // the disc's moving light at q (disc radius 1, y = 12 o'clock), as the Proto disc draws it — the sweep and its trail, the
            // scan front and the occupied cells (hatching averaged, the bright outer edge kept); lines a little wider: the taps are sparse
            float Live (float2 q, uint2 cells)
            {
                float r = length(q);
                if (r >= 0.995) return 0.0;
                float rr = r / RadarR;
                float deg = atan2(q.x, q.y) * 57.29578;
                deg = deg < 0.0 ? deg + 360.0 : deg;
                float arcK = 0.0174533 * r;
                float inGrid = step(Edge[0], rr) * step(rr, 1.0);
                float sw = frac(_Time.y / 4.0) * 360.0;
                float behind = frac((sw - deg) / 360.0);
                float sd = abs(frac((deg - sw) / 360.0 + 0.5) - 0.5) * 360.0 * arcK;
                float a = (exp(-behind * 360.0 / 22.0) * 0.16 + saturate(1.0 - sd / 0.008) * 0.6) * inGrid;
                uint dir = (uint)floor(frac(deg / 360.0 + 1.0 / 24.0) * 12.0) % 12u;
                float band = -1.0, lo = 0.0, hi = 0.0, reached = 0.0, sweepR = 0.0;
                [unroll] for (int i = 0; i < 5; i++)
                {
                    if (rr >= Edge[i] && rr < Edge[i + 1]) { band = i; lo = Edge[i] * RadarR; hi = Edge[i + 1] * RadarR; reached = step(Metres[i], _ScanR); }
                    if (_ScanR >= Metres[i] && _ScanR < Metres[i + 1]) sweepR = lerp(Edge[i], Edge[i + 1], (_ScanR - Metres[i]) / (Metres[i + 1] - Metres[i])) * RadarR;
                }
                a += saturate(1.0 - abs(r - sweepR) / 0.01) * step(0.001, sweepR) * 0.9;
                if (band > -0.5)
                {
                    // the cells come decided from the vertex (60 bits). v48 ran CellShown here on every tap: +1.06 ms side-on (RTX 5080,
                    // 4128x2208 MSAA 4) against +0.21 — and its slice arrays in the fragment slowed every pixel even when unused
                    #if defined(PW_PERTAP)   // v48 as uploaded, kept only to measure it (keyword set by PulseWatchDripCost)
                    LoadRad(); float on = CellShown(dir, (uint)band) * reached;
                    #else
                    uint k = (uint)band * 12u + dir;
                    float on = (float)(((k < 32u ? cells.x : cells.y) >> (k & 31u)) & 1u);
                    #endif
                    float cdeg = abs(frac(deg / 30.0 + 0.5) - 0.5) * 30.0 * arcK;
                    float side = step(cdeg, 15.0 * arcK - 0.012);
                    float inset = side * step(lo + 0.012, r) * step(r, hi - 0.012);
                    a += on * (inset * 0.43 + saturate(1.0 - abs(r - (hi - 0.014)) / 0.01) * side * 1.1);
                }
                return a;
            }

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 up : TEXCOORD1; float x : TEXCOORD2; float unit : TEXCOORD3;
                float3 pc : TEXCOORD4; float3 pn : TEXCOORD5; float3 pt : TEXCOORD6; nointerpolation uint2 cells : TEXCOORD7; nointerpolation uint4 hms : TEXCOORD8; nointerpolation uint cnt : TEXCOORD9; UNITY_VERTEX_OUTPUT_STEREO };

            // the 60 cells' on / off (scan reached included), decided once per vertex (the board has four): bit band x 12 + dir
            uint Pop (uint x) { x = x - ((x >> 1) & 0x55555555u); x = (x & 0x33333333u) + ((x >> 2) & 0x33333333u); return (((x + (x >> 4)) & 0x0F0F0F0Fu) * 0x01010101u) >> 24; }

            // cnt = every occupied cell (the Proto's count tab: CountCells, scan or not)
            uint2 CellBits (out uint cnt)
            {
                LoadRad();
                uint2 bits = 0u, all = 0u;
                [loop] for (uint b = 0u; b < 5u; b++)
                {
                    bool reached = _ScanR >= Metres[b];
                    [loop] for (uint d = 0u; d < 12u; d++)
                        if (CellShown(d, b) > 0.5) { uint k = b * 12u + d; uint2 m = k < 32u ? uint2(1u << k, 0u) : uint2(0u, 1u << (k - 32u)); all |= m; if (reached) bits |= m; }
                }
                cnt = Pop(all.x) + Pop(all.y);
                return bits;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 c = mul(unity_ObjectToWorld, float4(_AtVertex > 0.5 ? v.vertex.xyz : float3(0.0, 0.0, 0.0), 1.0)).xyz;
                float3 ax = unity_ObjectToWorld._m00_m10_m20, ay = unity_ObjectToWorld._m01_m11_m21;
                float sx = length(ax), sy = length(ay);
                float3 up = ay / max(sy, 1e-6);
                float3 cr = cross(up, _WorldSpaceCameraPos - c);
                float3 right = dot(cr, cr) > 1e-10 ? normalize(cr) : ax / max(sx, 1e-6);
                float x = (v.uv.x - 0.5) * 2.0 * _HalfW * sx;
                float3 wp = c + right * x + up * (lerp(-_Bottom, _Top, v.uv.y) * sy);
                o.pos = UnityWorldToClipPos(wp);
                float3 toCam = _WorldSpaceCameraPos - wp;
                float dist = length(toCam);
                float4 nearP = UnityWorldToClipPos(wp + toCam * (min(_DepthBias * sy, dist * 0.8) / max(dist, 1e-5)));
                o.pos.z = nearP.z / nearP.w * o.pos.w;
                o.wp = wp; o.up = up; o.x = x / max(sy, 1e-6); o.unit = sy;
                // the baked picture's plane: the disc's = the board object's own axes (Face's), the plate's = its skinned normal / tangent
                o.pc = c; o.pn = up; o.pt = ax / max(sx, 1e-6);
                o.cells = uint2(0u, 0u); o.cnt = 0u;
                if (_Live > 0.5 && _LiveBits > 0.5) { uint c; o.cells = CellBits(c); o.cnt = c; }
                o.hms = uint4(0u, 0u, 0u, 0u);
                if (_Glyph > 0.5) { PwTime t = PwGetTime(); o.hms = uint4(t.hour, t.minute, t.second, t.live ? 1u : 0u); }   // once per vertex
                if (_AtVertex > 0.5)
                {
                    o.pn = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                    o.pt = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
                }
                return o;
            }

            float Hash (float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float Noise (float x) { float i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f); return lerp(Hash(i), Hash(i + 1.0), f); }

            // coverage of glyph g (cell centred at cx, baseline by, Proto's PwGlyph layout) at q, edges one baked pixel wide
            float GlyphCov (float2 q, float cx, float by, float size, uint g, float px)
            {
                float2 c = (q - float2(cx - 0.5 * size, by - PW_BASELINE * size)) / size;
                if (any(c < 0.0) || any(c > 1.0)) return 0.0;
                float2 uv = float2(((float)(g % 8u) + c.x) / PW_ATLAS_COLS, 1.0 - ((float)(g / 8u) + 1.0 - c.y) / PW_ATLAS_ROWS);
                float d = (0.5 - tex2Dlod(_Glyphs, float4(uv, 0.0, 0.0)).r) * 2.0 * PW_SPREAD * size;
                return saturate(0.5 - d / px);
            }

            // the plate's changing text the bake left out (ProtoBake _BakeHide): big HH:MM, the seconds, the gauge fill — at b (board units
            // on the plate plane), laid out as PulseWatchProto Plate does (digits monospaced, so each slot is found without a loop)
            float3 PlateLive (float2 b, uint4 hms)
            {
                if (hms.w == 0u) return 0.0;
                float2 q = float2(dot(_PlateM.xy, b), dot(_PlateM.zw, b)) + _PlateT.xy;
                float3 r = 0.0;
                const float big = 0.52, by = 0.30, x0 = 0.07;
                float rel = (q.x - x0) / big;
                if (q.y > by - 0.25 * big && q.y < by + 0.85 * big && rel > -0.25 && rel < 2.6)
                {
                    uint s = rel < 0.52 ? 0u : (rel < 1.04 ? 1u : (rel < 1.32 ? 2u : (rel < 1.84 ? 3u : 4u)));
                    float start = s == 0u ? 0.0 : (s == 1u ? 0.52 : (s == 2u ? 1.04 : (s == 3u ? 1.32 : 1.84)));
                    float adv = s == 2u ? 0.28 : 0.52;
                    uint g = s == 0u ? hms.x / 10u : (s == 1u ? hms.x % 10u : (s == 2u ? (uint)PW_COLON : (s == 3u ? hms.y / 10u : hms.y % 10u)));
                    r += _InkBake.rgb * GlyphCov(q, x0 + (start + 0.5 * adv) * big, by, big, g, _PlatePx);
                }
                const float ss = 0.24;
                float sx0 = x0 + 2.36 * big + 0.04;
                rel = (q.x - sx0) / ss;
                if (q.y > by - 0.25 * ss && q.y < by + 0.85 * ss && rel > -0.25 && rel < 1.3)
                {
                    uint s = rel < 0.52 ? 0u : 1u;
                    r += _AccBake.rgb * GlyphCov(q, sx0 + ((float)s * 0.52 + 0.26) * ss, by, ss, s == 0u ? hms.z / 10u : hms.z % 10u, _PlatePx);
                }
                float u = (q.x - x0) / (_PlateT.w - x0);
                if (q.y > 0.12 && q.y < 0.175 && u > 0.0 && u < 1.0 && frac(u * 60.0) > 0.25 && floor(u * 60.0) < (float)hms.z - 0.5)
                    r += _AccBake.rgb * 0.95;
                return r;
            }

            float Line1 (float d, float w) { return saturate((w - abs(d)) / _DiscPx + 0.5); }
            float Fill1 (float d) { return saturate(-d / _DiscPx + 0.5); }
            float Box1 (float2 p, float2 lo, float2 hi) { float2 a = saturate((p - lo) / _DiscPx + 0.5) * saturate((hi - p) / _DiscPx + 0.5); return a.x * a.y; }
            float Blink () { return _Muted > 0.5 ? 0.675 + 0.325 * cos(_Time.y * 6.2832 / 1.6) : 1.0; }

            // the outer ring layer as PulseWatchProto Disc draws it (DiscLayers 4 = the baked plane, so the bake leaves it out):
            // ring, "+" in the diagonal gaps, ticks — red and blinking when muted — and the voice glow (D20 / D58)
            float3 DiscRing (float2 q)
            {
                float r = length(q);
                if (r < 0.80 || r > 1.0) return 0.0;
                float deg = atan2(q.x, q.y) * 57.29578;
                deg = deg < 0.0 ? deg + 360.0 : deg;
                float arcK = 0.0174533 * r;
                float gapDeg = abs(frac((deg - 45.0) / 90.0 + 0.5) - 0.5) * 90.0;
                bool muted = _Muted > 0.5;
                float blink = Blink();
                float3 ringC = muted ? Red * blink : _Ink.rgb, plusC = muted ? Red * blink : _Acc.rgb;
                float3 o = ringC * Line1(r - 0.965, 0.007) * step(7.0, gapDeg) * 0.85;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float a = radians(45.0 + 90.0 * k);
                    float2 d = abs(q - 0.965 * float2(sin(a), cos(a)));
                    o += plusC * max(Box1(d, -0.004, float2(0.026, 0.004)), Box1(d, -0.004, float2(0.004, 0.026))) * 0.95;
                }
                float t5 = abs(frac(deg / 5.0 + 0.5) - 0.5) * 5.0 * arcK;
                float t30 = abs(frac(deg / 30.0 + 0.5) - 0.5) * 30.0 * arcK;
                float tk = Line1(t5, 0.0025) * step(0.905, r) * step(r, 0.935);
                float tl = Line1(t30, 0.004) * step(0.875, r) * step(r, 0.94);
                o += ringC * (tk * 0.45 + tl * 0.9);
                if (_Voice > 0.001)
                {
                    float v = saturate(_Voice);
                    float w = 0.007 * (1.0 + 3.0 * v);
                    o += _Acc.rgb * Line1(r - 0.965, w) * step(7.0, gapDeg) * saturate(v * 1.8);
                    o += _Acc.rgb * exp(-abs(r - 0.965) / (0.02 + 0.03 * v)) * v * 0.85;
                    o += _Acc.rgb * (tk * 0.5 + tl) * v * 0.7;
                }
                return o;
            }

            // the top layer (DiscLayers 5): the slashed mic at 6 o'clock when muted, the count tab (the wearer's copy) with its number
            float Person (float2 p, float2 o, float h)
            {
                float2 q = (p - o) / h;
                float head = length(q - float2(0.32, 0.70)) - 0.17;
                float body = max(length((q - float2(0.32, 0.0)) / float2(1.0, 1.15)) - 0.32, -q.y);
                return min(head, body) * h;
            }
            float MicD (float2 p, float2 c, float h)
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
            float3 DiscTop (float2 q, uint cnt)
            {
                float3 o = 0.0;
                if (_Muted > 0.5 && abs(q.x) < 0.12 && q.y < -0.85 && q.y > -1.06) o += Red * Blink() * Fill1(MicD(q, float2(0.0, -0.995), 0.12));
                if (_CountOn > 0.5 && q.x < -0.6 && q.y > 0.72 && length(q) >= 1.0)
                {
                    o += _BackC.rgb * _BackC.a * Box1(q, float2(-1.0, 0.74), float2(-0.62, 1.0));
                    o += _Acc.rgb * Box1(q, float2(-1.0, 0.74), float2(-0.98, 1.0)) * 0.95;
                    o += _Ink.rgb * Fill1(Person(q, float2(-0.95, 0.80), 0.105)) * 0.9;
                    // PwCount: one or two digits from x -0.865, baseline 0.80, size 0.17 (digits monospaced 0.52)
                    float rel = (q.x + 0.865) / 0.17;
                    uint n = cnt >= 10u ? 2u : 1u;
                    if (rel > -0.25 && rel < 0.52 * n + 0.25)
                    {
                        uint s = rel < 0.52 || n == 1u ? 0u : 1u;
                        uint g = n == 2u && s == 0u ? (cnt / 10u) % 10u : cnt % 10u;
                        o += _Acc.rgb * GlyphCov(q, -0.865 + ((float)s * 0.52 + 0.26) * 0.17, 0.80, 0.17, g, _DiscPx);
                    }
                }
                return o;
            }

            // what the hologram shows on screen at this point: the line of sight through it meets the baked plane there
            float3 Added (float3 wp, v2f i)
            {
                float3 d = wp - _WorldSpaceCameraPos;
                float dn = dot(d, i.pn);
                if (abs(dn) < 1e-7) return 0.0;
                float3 az = cross(i.pt, i.pn);
                float3 res = 0.0;
                float lam = dot(i.pc + i.pn * (_BakeH * i.unit) - _WorldSpaceCameraPos, i.pn) / dn;
                if (lam > 0.0)
                {
                    float3 q = _WorldSpaceCameraPos + d * lam - i.pc;
                    float2 p = float2(dot(q, i.pt), dot(q, az)) / max(i.unit, 1e-6);   // X, Z in board units
                    float2 uv = (p - _BakeRect.xy) / _BakeRect.zw;
                    if (all(uv >= 0.0) && all(uv <= 1.0)) res = tex2Dlod(_BakeTex, float4(uv, 0.0, 0.0)).rgb;
                    if (_Glyph > 0.5) res += PlateLive(p, i.hms);
                    if (_Live > 0.5) res += DiscRing(p / _DiscR);
                }
                if (_Live > 0.5)   // the sweep and the cells sit a little higher (DiscLayers 2 / 3)
                {
                    lam = dot(i.pc + i.pn * (_DynH * i.unit) - _WorldSpaceCameraPos, i.pn) / dn;
                    if (lam > 0.0)
                    {
                        float3 q = _WorldSpaceCameraPos + d * lam - i.pc;
                        res += _Acc.rgb * _LiveK * Live(float2(dot(q, i.pt), dot(q, az)) / (max(i.unit, 1e-6) * _DiscR), i.cells);
                    }
                    lam = dot(i.pc + i.pn * (_TopH * i.unit) - _WorldSpaceCameraPos, i.pn) / dn;
                    if (lam > 0.0)
                    {
                        float3 q = _WorldSpaceCameraPos + d * lam - i.pc;
                        res += DiscTop(float2(dot(q, i.pt), dot(q, az)) / (max(i.unit, 1e-6) * _DiscR), i.cnt);
                    }
                }
                return res;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float cosA = abs(dot(normalize(_WorldSpaceCameraPos - i.wp), i.up));
                float fade = smoothstep(_FadeIn, _FadeOut, cosA);
                if (fade <= 0.0) { if (_Discard > 0.5) discard; return float4(0.0, 0.0, 0.0, 0.0); }
                // _SkipTop: the board above the face (it only lays the hologram's own light back over itself)
                if (_SkipTop > 0.5 && dot(i.wp - i.pc, i.up) > 0.0) { if (_Discard > 0.5) discard; return float4(0.0, 0.0, 0.0, 0.0); }
                float col = i.x / _Col;
                float len = _Len * i.unit * (0.25 + 0.75 * (0.6 * Noise(col * 0.35) + 0.4 * Noise(col * 1.3 + 7.0)));
                float dz = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                float3 acc = 0.0;
                float wsum = 0.0;
                int taps = (int)_Taps;
                [loop] for (int j = 0; j < taps; j++)   // not unrolled: the cell decision inside is too large twenty times over
                {
                    float t = (j + dz) / taps;
                    float w = pow(1.0 - t, 1.3);
                    acc += Added(i.wp + i.up * (len * t), i) * w;
                    wsum += w;
                }
                if (_Discard > 0.5 && max(acc.r, max(acc.g, acc.b)) < 1e-4) discard;   // nothing to add: skip the blend
                return float4(acc / wsum * _K * fade, 0.0);
            }
            ENDCG
        }
    }
}
