// Pyrite9 Pulse Watch — scan wave on opening (stage 5, owner only — docs/pulse-watch.md 4-4 / 10-11). Three looks by _Mode:
//   0 shell  : the wave front, a sphere of _Radius around the eyes — a wall of light near eye level (lat-long lines fading
//              above / below, a brighter eye-level line). Dimmed when the depth texture exists (the surfaces carry the effect)
//   1 floor  : a disc at the feet — bright front, a fading 0.5 m grid behind it, the radar ring distances. Only without depth
//   2 surface: an inside-out sphere drawn over everything (ZTest Always); the depth texture gives each pixel's world position,
//              so the front and the grid run over floors, walls, props and people. Nothing without depth, in mirrors or
//              orthographic cameras
//   Meshes are unit (sphere / XZ quad) and placed in the vertex shader around the object's origin; sizes are world metres at
//   build time, times the object's scale (follows the avatar's scale). _Radius is animated by the FX (0 -> _MaxRadius)
//   Depth exists only in worlds whose directional light casts realtime shadows; without it Unity leaves a tiny default
//   texture, so _CameraDepthTexture_TexelSize.z > 16 tells them apart (VRChat: to be confirmed in game)
Shader "Pyrite9/PulseWatch/Scan"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.45, 0.06, 1)
        _Mode ("Mode (0 shell, 1 floor, 2 surface)", Float) = 0
        _Radius ("Wave radius (m)", Float) = 0
        _MaxRadius ("Max radius (m)", Float) = 14
        _Band ("Front width (m)", Float) = 0.35
        _Trail ("Trail length (m)", Float) = 1.6
        _Cell ("Grid cell (m)", Float) = 0.5
        _Near ("Fade near the eyes (m)", Float) = 0.9
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" "VRCFallback" = "Hidden" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull [_Cull]
            ZTest [_ZTest]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            float _Mode, _Radius, _MaxRadius, _Band, _Trail, _Cell, _Near;
            float _VRChatMirrorMode;   // VRChat global: 0 = not a mirror
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float4 spos : TEXCOORD1;
                float3 centre : TEXCOORD2;
                float scale : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 centre = unity_ObjectToWorld._m03_m13_m23;
                float scale = length(unity_ObjectToWorld._m00_m10_m20);
                float3 dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz) / max(scale, 1e-5);
                float3 w;
                if (_Mode < 0.5)      w = centre + normalize(dir) * _Radius * scale;              // shell: the front
                else if (_Mode < 1.5) w = centre + dir * 2.0 * _Radius * scale;                   // floor: unit quad -> disc of _Radius
                else                  w = centre + normalize(dir) * (_MaxRadius + 1.0) * scale;   // surface: beyond the reach
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1.0));
                o.wpos = w;
                o.spos = ComputeScreenPos(o.pos);
                o.centre = centre;
                o.scale = scale;
                return o;
            }

            // 1 on a grid line of spacing cell (half width w, same unit), antialiased
            float GridLine (float x, float cell, float w)
            {
                float f = abs(frac(x / cell + 0.5) - 0.5) * cell;
                float aa = fwidth(f) * 1.5 + 1e-5;
                return 1.0 - smoothstep(w, w + aa, f);
            }

            // world grid on any surface: only the axes lying in the surface
            float Grid3 (float3 p, float3 n, float cell, float w)
            {
                float3 an = abs(n);
                float gx = GridLine(p.x, cell, w), gy = GridLine(p.y, cell, w), gz = GridLine(p.z, cell, w);
                return max(max(gx * saturate(1.2 - an.x * 1.2), gy * saturate(1.2 - an.y * 1.2)), gz * saturate(1.2 - an.z * 1.2));
            }

            float HasDepth () { return _CameraDepthTexture_TexelSize.z > 16.0 ? 1.0 : 0.0; }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float s = max(i.scale, 1e-5);
                float R = _Radius * s, band = _Band * s, trailLen = _Trail * s, cell = _Cell * s;
                float life = (1.0 - smoothstep(_MaxRadius * 0.7, _MaxRadius, _Radius)) * step(0.01, _Radius);
                float3 c = _Color.rgb;

                if (_Mode < 0.5)
                {
                    float3 d = normalize(i.wpos - i.centre);
                    float lon = atan2(d.x, d.z) / 6.2831853 * 24.0;                 // 15 deg
                    float latDeg = asin(clamp(d.y, -1.0, 1.0)) * 57.29578;
                    float g = max(GridLine(lon, 1.0, 0.01), GridLine(latDeg / 15.0, 1.0, 0.01));
                    float wall = exp(-latDeg * latDeg / (16.0 * 16.0));
                    float eq = GridLine(latDeg, 360.0, 0.25);
                    float a = g * 0.16 * wall + eq * 0.5;
                    return float4(c * a * life * 0.6 * lerp(1.0, 0.4, HasDepth()), 0);
                }

                if (_Mode < 1.5)
                {
                    float d = length(i.wpos.xz - i.centre.xz);
                    float behind = R - d;
                    float inside = step(0.0, behind);
                    float front = exp(-behind * behind / (band * band));
                    float trail = exp(-max(behind, 0.0) / trailLen) * inside;
                    float g = max(GridLine(i.wpos.x, cell, 0.012 * s), GridLine(i.wpos.z, cell, 0.012 * s));
                    float rings = 0.0;
                    float rr[5] = { 1.5, 3.0, 5.0, 8.0, 14.0 };   // radar ring distances (PulseWatchConst.RadarRings)
                    [unroll] for (int k = 0; k < 5; k++) rings = max(rings, GridLine(d - rr[k] * s, 100.0 * s, 0.025 * s) * step(rr[k] * s, R));
                    float a = front * 0.9 + g * trail * 0.25 + rings * 0.35 * (0.3 + 0.7 * trail) * inside;
                    return float4(c * a * life * (1.0 - HasDepth()), 0);
                }

                // surface: the world position behind this pixel from the depth texture. Not in a mirror (oblique projection, and
                // the depth texture is the main camera's) nor an orthographic camera (no ray rebuild) — it would smear the screen
                if (_VRChatMirrorMode > 0.5 || unity_OrthoParams.w > 0.5) discard;
                float raw = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.spos));
                float eyeZ = LinearEyeDepth(raw);
                float3 ray = i.wpos - _WorldSpaceCameraPos;
                float3 fwd = -UNITY_MATRIX_V[2].xyz;
                float3 p = _WorldSpaceCameraPos + ray / dot(ray, fwd) * eyeZ;
                float3 n = normalize(cross(ddy(p), ddx(p)));
                float g = Grid3(p, n, cell, 0.012 * s);
                float d = distance(p, i.centre);
                float behind = R - d;
                float front = exp(-behind * behind / (band * band));
                float trail = exp(-max(behind, 0.0) / trailLen) * step(0.0, behind);
                float sky = step(0.999, Linear01Depth(raw));
                float near = smoothstep(_Near * s * 0.6, _Near * s, d);          // not on the wearer's own arms
                float a = (front + g * trail * 0.3 + trail * 0.02) * (1.0 - sky) * near * HasDepth();
                return float4(c * a * life, 0);
            }
            ENDCG
        }
    }
}
