// Dust motes for the flashlight: additive soft dots that are only visible inside the flashlight cone.
// _FlashPos / _FlashDir / _FlashCone (x = cos outer, y = cos inner, z = range) are set per renderer by Flashlight.cs.
Shader "HideAndSeek/FlashlightDust"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.93, 0.82, 1)
        _Intensity ("Intensity (driven by script)", Float) = 1
        _Strength ("Strength", Range(0, 8)) = 1.6
        _Attenuation ("Distance Attenuation", Float) = 0.12
        _NearFade ("Near Fade (m)", Float) = 0.35
        _FlashPos ("Flash Position", Vector) = (0, 0, 0, 0)
        _FlashDir ("Flash Direction", Vector) = (0, 0, 1, 0)
        _FlashCone ("Cone (cosOuter, cosInner, range)", Vector) = (0.88, 0.98, 30, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            half4 _Color;
            half _Intensity;
            half _Strength;
            float _Attenuation;
            float _NearFade;
            float4 _FlashPos;
            float4 _FlashDir;
            float4 _FlashCone;

            struct appdata
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                half dotMask = saturate(1.0 - dot(d, d));
                dotMask *= dotMask;

                float3 L = i.worldPos - _FlashPos.xyz;
                float dist = length(L);
                float cosA = dot(L / max(dist, 1e-4), _FlashDir.xyz);
                half cone = smoothstep(_FlashCone.x, _FlashCone.y, cosA);
                half atten = 1.0 / (1.0 + dist * dist * _Attenuation) * saturate(1.0 - dist / max(_FlashCone.z, 0.01));

                float camDist = distance(_WorldSpaceCameraPos, i.worldPos);
                half near = saturate((camDist - 0.08) / max(_NearFade, 1e-3));

                half a = _Strength * _Intensity * i.color.a * dotMask * cone * atten * near;
                return half4(_Color.rgb * i.color.rgb * a, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
