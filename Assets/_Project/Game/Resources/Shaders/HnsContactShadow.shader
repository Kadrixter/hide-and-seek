// Soft contact shadow under small props (trash bin, papers on the floor, crushed can): multiplies the floor by a
// radial blob (texture alpha x vertex alpha = strength). Unlit, no depth write, drawn just above the floor.
Shader "HideAndSeek/ContactShadow"
{
    Properties
    {
        _MainTex ("Blob (A)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        ZWrite Off
        Blend DstColor Zero
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed a : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.a = v.color.a;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed k = 1 - tex2D(_MainTex, i.uv).a * i.a;
                return fixed4(k, k, k, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
