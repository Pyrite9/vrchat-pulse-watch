// Pyrite9 Pulse Watch — glass under the hologram (D41, docs/pulse-watch.md 10-56, 10-119): a quad just under the disc (circle),
// the clock plate (_Aspect, bottom right corner cut) and the waypoint button (_Shape 2). No GrabPass (the screen copy was most of
// the watch's GPU cost): the reflection probe behind, blurred (high mip) + smoke + fine grain + a fresnel edge + a faint sheen
// band sliding with the view (_Style 2). Drawn before the disc (queue Transparent+9), with the disc's _DepthBias.
//   _Reveal (D47): the disc's glass opens with the disc — a circle of that radius (1 = the rim, 1.05+ = all)
Shader "Pyrite9/PulseWatch/Frost"
{
    Properties
    {
        _Style ("Style (0 smoke, 1 probe, 2 probe + sheen)", Float) = 2
        _Shape ("Shape (0 disc, 1 plate, 2 button)", Float) = 0
        _Aspect ("Plate width / height", Float) = 1.8
        _Blur ("(unused) Blur radius (m)", Float) = 0.006
        _Dim ("Darken", Range(0, 1)) = 0.15
        _DepthBias ("Draw over things this close in front (m)", Float) = 0
        _Reveal ("Disc: opening radius (D47)", Float) = 2
        _Tint ("Smoke tint", Color) = (0.05, 0.065, 0.085, 1)
        _Smoke ("Smoke cover", Range(0, 1)) = 0.55
        _ProbeMip ("Probe blur (mip)", Float) = 6
        _ProbeK ("Probe share", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+9" "RenderType" = "Transparent" "IgnoreProjector" = "True" "VRCFallback" = "Hidden" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float _Style, _Shape, _Aspect, _Dim, _DepthBias, _Reveal, _Smoke, _ProbeMip, _ProbeK;
            float4 _Tint;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; float3 n : TEXCOORD2; float3 t : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                float3 toCam = _WorldSpaceCameraPos - wp;
                float dist = length(toCam);
                float4 nearP = mul(UNITY_MATRIX_VP, float4(wp + toCam * (min(_DepthBias, dist * 0.8) / max(dist, 1e-5)), 1.0));
                o.pos.z = nearP.z / nearP.w * o.pos.w;
                o.uv = v.uv; o.wp = wp;
                o.n = normalize(unity_ObjectToWorld._m01_m11_m21);   // the quad lies in its object's XZ plane: +Y is its normal
                o.t = normalize(unity_ObjectToWorld._m00_m10_m20);
                return o;
            }

            float Hash (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float inside;
                if (_Shape < 0.5)
                {
                    float d = length(i.uv - 0.5) - 0.5;
                    inside = saturate(-d / max(fwidth(d), 1e-5) + 0.5);
                    float r = length(i.uv - 0.5) * 2.0;
                    if (_Reveal < 1.045) inside *= saturate((_Reveal - r) / max(fwidth(r), 1e-4) + 0.5);
                }
                else if (_Shape > 1.5)
                {
                    float d = i.uv.x * _Aspect + i.uv.y;
                    float fw = max(fwidth(d), 1e-5);
                    inside = saturate((d - 0.22) / fw + 0.5) * saturate(((_Aspect + 0.78) - d) / fw + 0.5);
                }
                else
                {
                    float2 p = float2(i.uv.x * _Aspect, i.uv.y);
                    float cutD = (_Aspect - p.x) + p.y - 0.13;
                    inside = saturate(cutD / max(fwidth(cutD), 1e-5) + 0.5);
                }
                float3 v = normalize(i.wp - _WorldSpaceCameraPos);   // eye → glass
                float3 col = _Tint.rgb;
                float a = _Smoke;
                if (_Style > 0.5)
                {
                    // what is behind, blurred: the reflection probe looked up straight through the glass, at a high mip
                    float4 env = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, v, _ProbeMip);
                    float3 behind = DecodeHDR(env, unity_SpecCube0_HDR) * (1.0 - _Dim);
                    col = lerp(_Tint.rgb, behind, _ProbeK);
                    a = lerp(_Smoke, 0.85, _ProbeK);
                }
                if (_Style > 1.5)
                {
                    float cosV = abs(dot(v, i.n));
                    float fres = pow(1.0 - cosV, 4.0);
                    float band = dot(v, i.t) * 3.0 + i.uv.x * 2.0 - i.uv.y * 1.2;
                    float sheen = exp(-pow(frac(band * 0.5) - 0.5, 2.0) * 60.0) * 0.06;
                    float grain = (Hash(floor(i.uv * 420.0)) - 0.5) * 0.035;
                    col += fres * 0.18 + sheen + grain;
                }
                return float4(max(col, 0.0), inside * a);
            }
            ENDCG
        }
    }
}
