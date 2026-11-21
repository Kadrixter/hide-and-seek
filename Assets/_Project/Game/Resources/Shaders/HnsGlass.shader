// Thin display glass (vending machine door): mostly clear, darkened a little, brighter at grazing angles
// (fresnel), with faint diagonal wipe streaks and fingerprints from _MainTex (R = streaks, G = smudges).
Shader "Hns/Glass"
{
    Properties
    {
        _MainTex ("Streaks (R) smudges (G)", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.55, 0.65, 0.7, 0.08)
        _Edge ("Fresnel colour", Color) = (0.7, 0.8, 0.9, 0.45)
        _Streak ("Streak strength", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Tint, _Edge;
            half _Streak;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 n : TEXCOORD1;
                float3 v : TEXCOORD2;
            };

            v2f vert (appdata_base i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.uv = TRANSFORM_TEX(i.texcoord, _MainTex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = normalize(UnityWorldSpaceViewDir(mul(unity_ObjectToWorld, i.vertex).xyz));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed2 s = tex2D(_MainTex, i.uv).rg;
                half f = pow(1.0 - saturate(abs(dot(normalize(i.n), normalize(i.v)))), 4.0);
                half a = saturate(_Tint.a + f * _Edge.a + s.r * _Streak + s.g * _Streak * 0.6);
                fixed3 c = _Tint.rgb * _Tint.a + _Edge.rgb * f * _Edge.a + (s.r + s.g * 0.6) * _Streak * fixed3(0.85, 0.9, 0.95);
                return fixed4(c, a);
            }
            ENDCG
        }
    }
}
