// Pyrite9 Pulse Watch — frosted glass under the hologram (D41, docs/pulse-watch.md 10-56): a quad just under the disc (circle)
// and one under the clock plate (its shape: _Aspect, bottom right corner cut) redraw what is behind them blurred, so what shows
// through the hologram's backing reads as glass. One named GrabPass (_PulseWatchGrab): the screen is copied once a frame however
// many copies of the watch are in view. Drawn before the disc (queue Transparent+9), with the disc's _DepthBias.
//   _Reveal (D47): the disc's glass opens with the disc — a circle of that radius (1 = the rim, 1.05+ = all)
//   _Shape 2 (D44): under the waypoint button — its tab, top right and bottom left corners cut (PulseWatchProto WpButton)
//   _Blur: blur radius in metres at the quad (turned into screen space per vertex); _Dim darkens the blurred picture a little
Shader "Pyrite9/PulseWatch/Frost"
{
    Properties
    {
        _Shape ("Shape (0 disc, 1 plate, 2 button)", Float) = 0
        _Aspect ("Plate width / height", Float) = 1.8
        _Blur ("Blur radius (m)", Float) = 0.006
        _Dim ("Darken", Range(0, 1)) = 0.15
        _DepthBias ("Draw over things this close in front (m)", Float) = 0
        _Reveal ("Disc: opening radius (D47)", Float) = 2
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+9" "RenderType" = "Transparent" "IgnoreProjector" = "True" "VRCFallback" = "Hidden" }
        GrabPass { "_PulseWatchGrab" }
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

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_PulseWatchGrab);
            float _Shape, _Aspect, _Blur, _Dim, _DepthBias, _Reveal;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 grab : TEXCOORD1; float2 br : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                // depth only, as the hologram: pulled toward the camera so it draws over the wearer's own hand and sleeve
                float3 toCam = _WorldSpaceCameraPos - wp;
                float dist = length(toCam);
                float4 nearP = mul(UNITY_MATRIX_VP, float4(wp + toCam * (min(_DepthBias, dist * 0.8) / max(dist, 1e-5)), 1.0));
                o.pos.z = nearP.z / nearP.w * o.pos.w;
                o.grab = ComputeGrabScreenPos(o.pos);
                // the blur radius in grab uv: _Blur m across the view, projected here (x; y corrected for the aspect)
                float3 right = normalize(UNITY_MATRIX_V[0].xyz);
                float4 side = mul(UNITY_MATRIX_VP, float4(wp + right * _Blur, 1.0));
                float r = length(side.xy / side.w - o.pos.xy / o.pos.w) * 0.5;
                o.br = float2(r, r * _ScreenParams.x / _ScreenParams.y);
                o.uv = v.uv;
                return o;
            }

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
                    // the button's tab (PulseWatchProto WpButton): x 0..A, y 0..1, corners cut where x + y < 0.22 or > A + 0.78
                    float d = i.uv.x * _Aspect + i.uv.y;
                    float fw = max(fwidth(d), 1e-5);
                    inside = saturate((d - 0.22) / fw + 0.5) * saturate(((_Aspect + 0.78) - d) / fw + 0.5);
                }
                else
                {
                    // the plate's outline (PulseWatchProto Plate): x 0..A, y 0..1, bottom right corner cut at 45 deg
                    float2 p = float2(i.uv.x * _Aspect, i.uv.y);
                    float cutD = (_Aspect - p.x) + p.y - 0.13;
                    inside = saturate(cutD / max(fwidth(cutD), 1e-5) + 0.5);
                }
                float2 uv = i.grab.xy / i.grab.w;
                // centre + 3 rings x 8 taps, gaussian-ish weights
                float3 acc = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_PulseWatchGrab, uv).rgb;
                float wsum = 1.0;
                [unroll] for (int r = 1; r <= 3; r++)
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float a = (k + 0.5 * r) * 0.7853982;
                        float w = exp(-r * r / 4.5);
                        acc += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_PulseWatchGrab, uv + float2(cos(a), sin(a)) * i.br * (r / 3.0)).rgb * w;
                        wsum += w;
                    }
                return float4(acc / wsum * (1.0 - _Dim), inside);
            }
            ENDCG
        }
    }
}
