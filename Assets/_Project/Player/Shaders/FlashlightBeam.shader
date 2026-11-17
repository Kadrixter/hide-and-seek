// Volumetric-looking flashlight beam for the Built-in Render Pipeline.
// Drawn on nested open cone shells (vertex colour alpha = shell weight, uv.y = 0 at lens .. 1 at beam end).
// Additive; brightness falls off along the length and toward the silhouette edges (view-angle fresnel),
// soft-fades where it intersects geometry (_CameraDepthTexture) and near the camera, with subtle animated noise.
Shader "HideAndSeek/FlashlightBeam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.93, 0.82, 1)
        _Intensity ("Intensity (driven by script)", Float) = 1
        _Strength ("Strength", Range(0, 2)) = 0.11
        _LengthFalloff ("Length Falloff (power)", Range(0.2, 6)) = 1.6
        _LensFade ("Lens Fade-in (uv.y)", Range(0.001, 0.3)) = 0.035
        _EdgePower ("Edge Softness (fresnel power)", Range(0.2, 8)) = 1.7
        _SoftDepth ("Soft Depth Distance (m)", Range(0.01, 4)) = 0.9
        _DepthFadeEnabled ("Depth Fade Enabled", Float) = 1
        _NearFadeStart ("Near Fade Start (m)", Float) = 0.3
        _NearFadeRange ("Near Fade Range (m)", Float) = 2.2
        _NoiseScale ("Noise Scale", Float) = 1.6
        _NoiseSpeed ("Noise Drift (m/s)", Vector) = (0.06, 0.035, 0.05, 0)
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            half4 _Color;
            half _Intensity;
            half _Strength;
            half _LengthFalloff;
            half _LensFade;
            half _EdgePower;
            float _SoftDepth;
            half _DepthFadeEnabled;
            float _NearFadeStart;
            float _NearFadeRange;
            float _NoiseScale;
            float4 _NoiseSpeed;
            half _NoiseStrength;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                half weight : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.screenPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screenPos.z);
                o.weight = v.color.a;
                return o;
            }

            // Cheap 3D value noise.
            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i);
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            half4 frag (v2f i, half facing : VFACE) : SV_Target
            {
                float3 toCam = _WorldSpaceCameraPos - i.worldPos;
                float camDist = length(toCam);
                float3 V = toCam / max(camDist, 1e-4);
                float3 N = normalize(i.worldNormal);

                // Fresnel-style thickness: faces seen head-on are "thick", silhouette edges fade out.
                half ndv = saturate(abs(dot(N, V)));
                half edge = pow(ndv, _EdgePower);

                // Along the beam: quick fade-in at the lens, then falloff toward the end.
                half along = saturate(i.uv.y);
                half lengthFade = pow(1.0 - along, _LengthFalloff) * smoothstep(0.0, _LensFade, along);

                // Avoid a hard wall of fog right in front of the camera.
                half nearFade = saturate((camDist - _NearFadeStart) / max(_NearFadeRange, 1e-3));

                // Soft intersection with scene geometry.
                half depthFade = 1.0;
                if (_DepthFadeEnabled > 0.5)
                {
                    float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                    float partZ = i.screenPos.z;
                    depthFade = saturate((sceneZ - partZ) / _SoftDepth);
                }

                // Two octaves of slowly drifting world-space noise (dust / air density).
                float3 np = i.worldPos * _NoiseScale + _Time.y * _NoiseSpeed.xyz;
                float n = valueNoise(np) * 0.65 + valueNoise(np * 2.7 + 11.3) * 0.35;
                half noise = lerp(1.0, saturate(0.45 + n * 1.1), _NoiseStrength);

                half a = _Strength * _Intensity * i.weight * edge * lengthFade * nearFade * depthFade * noise;
                return half4(_Color.rgb * a, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
