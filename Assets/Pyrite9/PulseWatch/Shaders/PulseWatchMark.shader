// Pyrite9 Pulse Watch — waypoint in the world (D27-D29, prototype). Glow in the waypoint colour, five looks by _Mode:
//   0 laser  : a line from the object's origin along its +Z (length = Z scale, width = X scale) turned to face the camera,
//              a bright core in a soft glow, fading in at the hand and out toward the far end
//   1 (ring) : left out (D63) — the number kept
//   2 diamond: an outlined diamond with a filled heart, standing upright and turned to the camera about the world up (object
//              origin = its centre, X scale = its size). D63: bobs _Hover (m) up and down every _HoverPeriod (s), the tag with it.
//              D64 _Grow: past _TagNear (m) the diamond grows with the distance like its tag, the tag's lift with it (the two keep
//              their sizes to each other); off, both keep their size in the world
//   3 reticle: where the laser hits (owner only) — a small ring with four ticks, lying in the object's XZ plane
//   4 flash  : (D49) the flashlight's short beam — like the laser, but a cone: widening from _Start x the X scale at the origin
//              to the X scale at the far end, soft across and fading out along it (the across / along position from the world
//              point, so the tapered quad does not skew it). Faint at the watch, growing over its first third (D50: no bright
//              point where it starts — the v27 lens glint was dropped)
//   5 tag    : (D61) the name tag above the diamond — a dark see-through tab with a cut corner, a cyan edge, a bar and the name
//              (_MainTex: the name's coverage laid out as the whole tab, _TagAspect = width / height). Object origin = the diamond's
//              centre, X scale = its height: lifted _TagLift straight up, turned to the camera about the world up, and past
//              _TagNear (m) from the camera grown with the distance (about the same size on screen) when _Grow. Alpha blended — the other
//              looks return alpha 0, so the shared blend (One, OneMinusSrcAlpha) still adds them
//   _ZTest: Always for the mark (seen through walls, Q13), LessEqual for the laser and the reticle
//   _Pend (D60, the wearer's side): a held clear waiting to be sent — the diamond goes hollow, dimmed and blinking slowly
//   with its tag (a tap on the button keeps the waypoint)
Shader "Pyrite9/PulseWatch/Mark"
{
    Properties
    {
        _Color ("Color", Color) = (0.30, 0.85, 1, 1)
        _Mode ("Mode (0 laser, 2 diamond, 3 reticle, 4 flash beam, 5 name tag)", Float) = 0
        _Gain ("Brightness", Float) = 1
        _Start ("Beam width at the origin (mode 4, of the X scale)", Float) = 0.15
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8
        _Pend ("Clear pending (mode 2, 5)", Float) = 0
        _Hover ("Bob up and down, m (mode 2, 5)", Float) = 0
        _HoverPeriod ("Bob period, s (mode 2, 5)", Float) = 3
        _MainTex ("Name coverage (mode 5)", 2D) = "black" {}
        _TagAspect ("Tag width / height (mode 5)", Float) = 4
        _TagLift ("Tag lift over the diamond's centre, m (mode 5)", Float) = 0.14
        _TagNear ("Grows past this distance, m (mode 2, 5)", Float) = 4
        _Grow ("Grow with the distance (mode 2, 5)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" "VRCFallback" = "Hidden" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            float _Mode, _Gain, _Start, _Pend, _TagAspect, _TagLift, _TagNear, _Hover, _HoverPeriod, _Grow;
            sampler2D _MainTex;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 origin = unity_ObjectToWorld._m03_m13_m23;
                if (_Mode > 1.5 && (_Mode < 2.5 || _Mode > 4.5)) origin.y += _Hover * sin(_Time.y * 6.2832 / max(_HoverPeriod, 0.01));
                float3 wp;
                o.uv = v.uv;
                if (_Mode < 0.5 || (_Mode > 3.5 && _Mode < 4.5))
                {
                    float3 axis = unity_ObjectToWorld._m02_m12_m22;
                    float width = length(unity_ObjectToWorld._m00_m10_m20);
                    float t = v.vertex.z + 0.5;
                    if (_Mode > 3.5) width *= lerp(_Start, 1.0, t);
                    float3 p = origin + axis * t;
                    float3 side = cross(axis, _WorldSpaceCameraPos - p);
                    side = dot(side, side) > 1e-12 ? normalize(side) : float3(1, 0, 0);
                    wp = p + side * v.vertex.x * width;
                }
                else if (_Mode > 1.5 && _Mode < 2.5)
                {
                    float3 toCam = _WorldSpaceCameraPos - origin;
                    float size = length(unity_ObjectToWorld._m00_m10_m20) * (_Grow > 0.5 ? max(1.0, length(toCam) / _TagNear) : 1.0);
                    float3 right = cross(float3(0, 1, 0), toCam);
                    right = dot(right, right) > 1e-12 ? normalize(right) : float3(1, 0, 0);
                    wp = origin + (right * v.vertex.x + float3(0, 1, 0) * v.vertex.z) * size;
                }
                else if (_Mode > 4.5)
                {
                    float h = length(unity_ObjectToWorld._m00_m10_m20);
                    float k = _Grow > 0.5 ? max(1.0, length(_WorldSpaceCameraPos - origin) / _TagNear) : 1.0;   // as the diamond's
                    float3 base = origin + float3(0, _TagLift * k, 0);
                    float3 toCam = _WorldSpaceCameraPos - base;
                    float3 right = cross(toCam, float3(0, 1, 0));   // the viewer's right (the diamond's cross is mirrored, it does not show)
                    right = dot(right, right) > 1e-12 ? normalize(right) : float3(1, 0, 0);
                    wp = base + (right * v.vertex.x * _TagAspect + float3(0, 1, 0) * (v.vertex.z + 0.5)) * h * k;
                }
                else wp = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                o.wp = wp;
                return o;
            }

            float Line (float d, float w) { float aa = max(fwidth(d), 1e-5); return saturate((w - abs(d)) / aa + 0.5); }
            float Fill (float d) { return saturate(-d / max(fwidth(d), 1e-5) + 0.5); }

            float Laser (float2 uv)
            {
                float d = abs(uv.x - 0.5) * 2.0;
                float core = exp(-pow(d / 0.22, 2.0)) + 0.35 * exp(-pow(d / 0.7, 2.0));
                return core * smoothstep(0.0, 0.02, uv.y) * lerp(1.0, 0.45, uv.y);
            }

            // across 0 on the axis .. 1 at the cone's edge, along 0 at the watch .. 1 at the far end
            float FlashBeam (float3 wp)
            {
                float3 origin = unity_ObjectToWorld._m03_m13_m23;
                float3 axis = unity_ObjectToWorld._m02_m12_m22;
                float len = max(length(axis), 1e-5);
                float3 q = wp - origin;
                float t = saturate(dot(q, axis) / (len * len));
                float hw = 0.5 * length(unity_ObjectToWorld._m00_m10_m20) * lerp(_Start, 1.0, t);
                float a = length(q - axis * t) / max(hw, 1e-5);
                float across = pow(saturate(1.0 - a * a), 2.0) * (0.55 + 0.45 * exp(-pow(a / 0.3, 2.0)));
                return across * smoothstep(0.0, 0.35, t) * pow(1.0 - t, 1.6);
            }

            float Diamond (float2 uv)
            {
                float2 p = (uv - 0.5) * 2.0;
                float d = (abs(p.x) + abs(p.y)) * 0.7071;
                float edge = Line(d - 0.52, 0.045);
                float heart = Fill(d - 0.30) * (1.0 - step(0.5, _Pend));
                float glow = exp(-pow((d - 0.52) / 0.12, 2.0)) * 0.45;
                return max(edge, heart) + glow;
            }

            float Reticle (float2 uv)
            {
                float2 p = (uv - 0.5) * 2.0;
                float r = length(p);
                float ring = Line(r - 0.62, 0.06);
                float2 a = abs(p);
                float ticks = (Line(p.x, 0.05) * step(0.75, a.y) + Line(p.y, 0.05) * step(0.75, a.x)) * step(max(a.x, a.y), 1.0);
                return max(ring, ticks) + Fill(r - 0.08);
            }

            // premultiplied colour over o
            float4 Over (float4 o, float3 c, float a) { return float4(c * a, a) + o * (1.0 - a); }

            float4 Tag (float2 uv)
            {
                float A = _TagAspect;
                float2 p = float2(uv.x * A, uv.y);   // tag units: 1 = its height
                const float cut = 0.22;
                float box = max(max(-p.x, p.x - A), max(-p.y, p.y - 1.0));
                float d = max(box, max((cut - (p.x + 1.0 - p.y)) * 0.7071, (cut - (A - p.x + p.y)) * 0.7071));   // top-left / bottom-right cut
                float4 o = Over(0, float3(0.07, 0.07, 0.08), Fill(d) * 0.8);
                o = Over(o, _Color.rgb, Line(d + 0.035, 0.022));
                o = Over(o, _Color.rgb, Fill(max(abs(p.x - 0.125) - 0.03, abs(p.y - 0.5) - 0.24)));
                o = Over(o, _Color.rgb, tex2D(_MainTex, uv).a * Fill(d));
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (_Mode > 4.5) return Tag(i.uv) * (_Pend > 0.5 ? 0.4 + 0.15 * sin(_Time.y * 6.2832) : 1.0);
                float k = _Mode < 0.5 ? Laser(i.uv) : (_Mode < 2.5 ? Diamond(i.uv) : (_Mode < 3.5 ? Reticle(i.uv) : FlashBeam(i.wp)));
                if (_Pend > 0.5 && _Mode > 1.5 && _Mode < 2.5) k *= 0.4 + 0.15 * sin(_Time.y * 6.2832);
                return float4(_Color.rgb * k * _Gain, 0.0);
            }
            ENDCG
        }
    }
}
