Shader "Hidden/HideAndSeek/NightVision"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "HnsSafe.cginc"
            sampler2D _MainTex;
            float _Gain;
            float _Noise;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed3 c = HnsSafe3(tex2D(_MainTex, i.uv).rgb);
                float l = dot(c, float3(0.299, 0.587, 0.114));
                // Lift the shadows (gamma) and amplify, keep it grey.
                l = saturate(pow(max(l, 0.0), 0.62) * _Gain);
                float n = frac(sin(dot(i.uv * (_Time.y + 1.0), float2(12.9898, 78.233))) * 43758.5453);
                l = saturate(l + (n - 0.5) * _Noise);
                // Soft vignette.
                float2 d = i.uv - 0.5;
                l *= saturate(1.15 - dot(d, d) * 1.6);
                return fixed4(l, l, l, 1);
            }
            ENDCG
        }
    }
}
