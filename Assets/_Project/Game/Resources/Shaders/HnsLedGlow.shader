// Soft additive halo around a small lamp (CCTV LED): a quad turned to the camera in the vertex shader, radial
// falloff with a bright core, fades out in the dark fog like the rest of the garage. Colour and strength per
// renderer via MaterialPropertyBlock (_Color, _Intensity).
Shader "HideAndSeek/LedGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1,0.1,0.05,1)
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            fixed4 _Color;
            float _Intensity;

            v2f vert (appdata v)
            {
                v2f o;
                // billboard: object centre in view space, quad corners offset in view space (object scale = size)
                float3 centre = UnityObjectToViewPos(float3(0, 0, 0));
                float sx = length(unity_ObjectToWorld._m00_m10_m20);
                float sy = length(unity_ObjectToWorld._m01_m11_m21);
                // pulled a little towards the camera so the halo is not cut by the housing it sits on
                float3 toCam = -normalize(centre);
                float3 vp = centre + toCam * 0.03 + float3(v.vertex.x * sx, v.vertex.y * sy, 0);
                o.pos = mul(UNITY_MATRIX_P, float4(vp, 1));
                o.uv = v.uv * 2 - 1;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float halo = saturate(1 - r);
                float glow = halo * halo * 0.55 + pow(halo, 10) * 1.6;
                fixed4 col = _Color * glow * _Intensity;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }
}
