// Planar mirror surface: samples the reflection render texture in screen space (rendered by PlanarMirror).
Shader "HideAndSeek/Mirror"
{
    Properties
    {
        _ReflTex ("Reflection", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.92, 0.94, 0.96, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ReflTex;
            fixed4 _Tint;

            struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2Dproj(_ReflTex, UNITY_PROJ_COORD(i.screen)) * _Tint;
            }
            ENDCG
        }
    }
}
