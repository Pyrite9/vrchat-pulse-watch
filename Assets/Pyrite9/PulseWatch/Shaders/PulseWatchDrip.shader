// Pyrite9 Pulse Watch — the hologram's light running straight down to the wrist (D41, docs/pulse-watch.md 10-56): seen from the
// side, every lit thing on the disc and the plate (radar cells, lines, digits) trails a fading streak down the face normal to the
// forearm; looked at along the normal (the wearer reading the watch) it is gone, so the radar never smears.
// One board through Face's origin, spanning the normal (+Y) and the line across the view (turned to the camera about the normal in
// the vertex shader). Each of its pixels looks up the normal (_Len, a different length per 2 mm column) and adds what the hologram
// put on screen there: the screen after the hologram (named GrabPass _PulseWatchHolo, Transparent+13) less the screen before it
// (_PulseWatchGrab, taken by the blur under the disc — so this needs the blur, and the disc's glass is culled by bounds covering
// these boards so that copy is always this frame's). Background, sleeve and skin cancel out.
// Sizes in metres at unit scale, multiplied by the object's own scale: the board narrows to nothing as Face closes (FX scale x).
// _AtVertex 1 (the clock plate's board, D43): the board sits on the mesh's vertex instead of the object's origin — a mesh skinned
// to the plate with Holo (Face's axes, unscaled) as its root bone, so the board follows the plate but runs down Face's normal like
// the disc's. Over the plate the screen-difference reads almost nothing where it hangs over bright skin or sleeve (its dark
// backing hides as much light as its text adds), so this board also takes the plate's plane (the skinned normal and tangent) and
// half size (_PlateHalf): a sample whose ray hits the plate counts the screen after less only what the backing let through
// (1 - _BackA) of the screen before. _Button (D44): the row of tabs under the plate (waypoint, flashlight — D46), in the same
// plane — down the plate, half width, half height, centre across (m) — counts the same way, so their icons run down too (0 = none).
Shader "Pyrite9/PulseWatch/Drip"
{
    Properties
    {
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
        _PlateHalf ("Plate half width, half height (m, _AtVertex)", Vector) = (0, 0, 0, 0)
        _BackA ("Plate backing cover (_AtVertex)", Float) = 0.8
        _Button ("Tabs: down the plate, half width, half height, centre across (m, _AtVertex)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+13" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" "VRCFallback" = "Hidden" }
        GrabPass { "_PulseWatchHolo" }
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
            #include "UnityCG.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_PulseWatchHolo);
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_PulseWatchGrab);
            float _HalfW, _Top, _Bottom, _Len, _Col, _K, _FadeIn, _FadeOut, _DepthBias, _AtVertex, _BackA;
            float4 _PlateHalf, _Button;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 up : TEXCOORD1; float x : TEXCOORD2; float unit : TEXCOORD3;
                float3 pc : TEXCOORD4; float3 pn : TEXCOORD5; float3 pt : TEXCOORD6; UNITY_VERTEX_OUTPUT_STEREO };

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
                // depth only, pulled toward the camera as the hologram's, so the streaks draw over the wearer's own sleeve
                float3 toCam = _WorldSpaceCameraPos - wp;
                float dist = length(toCam);
                float4 nearP = UnityWorldToClipPos(wp + toCam * (min(_DepthBias * sy, dist * 0.8) / max(dist, 1e-5)));
                o.pos.z = nearP.z / nearP.w * o.pos.w;
                o.wp = wp; o.up = up; o.x = x / max(sy, 1e-6); o.unit = sy;
                // the plate's centre and axes (world), its half size scaled as the board
                o.pc = c; o.pn = float3(0.0, 1.0, 0.0); o.pt = float3(1.0, 0.0, 0.0);
                if (_AtVertex > 0.5)
                {
                    o.pn = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                    o.pt = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
                }
                return o;
            }

            float Hash (float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float Noise (float x) { float i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f); return lerp(Hash(i), Hash(i + 1.0), f); }

            // the light the hologram added at this point on screen; on the plate (_AtVertex, the ray through wp hits it) what showed
            // through its backing is all that is taken off
            float3 Added (float3 wp, v2f i)
            {
                float4 g = ComputeGrabScreenPos(UnityWorldToClipPos(wp));
                float2 uv = g.xy / g.w;
                // off screen (or behind the eye) the copies clamp to their edge — nothing the hologram added there
                if (g.w <= 0.0 || any(uv < 0.0) || any(uv > 1.0)) return 0.0;
                float through = 1.0;
                if (_AtVertex > 0.5)
                {
                    float3 d = wp - _WorldSpaceCameraPos;
                    float dn = dot(d, i.pn);
                    float3 q = _WorldSpaceCameraPos + d * (dot(i.pc - _WorldSpaceCameraPos, i.pn) / (abs(dn) > 1e-6 ? dn : 1e-6)) - i.pc;
                    float2 p = float2(dot(q, i.pt), dot(q, cross(i.pn, i.pt)));   // across the plate, down it
                    float2 l = abs(p) / max(_PlateHalf.xy * i.unit, 1e-6);
                    float2 b = abs(p - float2(_Button.w, _Button.x) * i.unit) / max(_Button.yz * i.unit, 1e-6);
                    through = min(max(l.x, l.y), max(b.x, b.y)) < 1.0 ? 1.0 - _BackA : 1.0;
                }
                return max(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_PulseWatchHolo, uv).rgb - UNITY_SAMPLE_SCREENSPACE_TEXTURE(_PulseWatchGrab, uv).rgb * through, 0.0);
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float cosA = abs(dot(normalize(_WorldSpaceCameraPos - i.wp), i.up));
                float fade = smoothstep(_FadeIn, _FadeOut, cosA);
                if (fade <= 0.0) return float4(0.0, 0.0, 0.0, 0.0);
                float col = i.x / _Col;
                float len = _Len * i.unit * (0.25 + 0.75 * (0.6 * Noise(col * 0.35) + 0.4 * Noise(col * 1.3 + 7.0)));
                // per-pixel dither on the sample spots: the steps between samples blur into one smooth streak
                float dz = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                float3 acc = 0.0;
                float wsum = 0.0;
                [unroll] for (int j = 0; j < 20; j++)
                {
                    float t = (j + dz) / 20.0;
                    float w = pow(1.0 - t, 1.3);
                    acc += Added(i.wp + i.up * (len * t), i) * w;
                    wsum += w;
                }
                return float4(acc / wsum * _K * fade, 0.0);
            }
            ENDCG
        }
    }
}
