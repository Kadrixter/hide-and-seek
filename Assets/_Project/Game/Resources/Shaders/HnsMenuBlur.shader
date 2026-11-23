// Menu background blur (Esc menu, confirm dialog): 4-tap downsample, separable 9-tap gauss, composite with fade.
Shader "Hidden/HnsMenuBlur"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D _BlurTex;
    float2 _Dir;
    float _Amount;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    half4 fragDown (v2f i) : SV_Target
    {
        float2 t = _MainTex_TexelSize.xy;
        half4 c = tex2D(_MainTex, i.uv + t * float2(-0.5, -0.5));
        c += tex2D(_MainTex, i.uv + t * float2( 0.5, -0.5));
        c += tex2D(_MainTex, i.uv + t * float2(-0.5,  0.5));
        c += tex2D(_MainTex, i.uv + t * float2( 0.5,  0.5));
        return c * 0.25;
    }

    half4 fragBlur (v2f i) : SV_Target
    {
        float2 d = _Dir * _MainTex_TexelSize.xy;
        half4 c = tex2D(_MainTex, i.uv) * 0.2270270270;
        c += (tex2D(_MainTex, i.uv + d * 1.3846153846) + tex2D(_MainTex, i.uv - d * 1.3846153846)) * 0.3162162162;
        c += (tex2D(_MainTex, i.uv + d * 3.2307692308) + tex2D(_MainTex, i.uv - d * 3.2307692308)) * 0.0702702703;
        return c;
    }

    half4 fragComp (v2f i) : SV_Target
    {
        half4 s = tex2D(_MainTex, i.uv);
        half4 b = tex2D(_BlurTex, i.uv);
        // slightly darker and less saturated behind the menu
        half l = dot(b.rgb, half3(0.299, 0.587, 0.114));
        b.rgb = lerp(b.rgb, l.xxx, 0.15) * 0.92;
        return lerp(s, b, _Amount);
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComp
            ENDCG }
    }
    Fallback Off
}
