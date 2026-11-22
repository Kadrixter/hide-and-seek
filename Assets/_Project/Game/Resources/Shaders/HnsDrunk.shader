Shader "Hidden/HideAndSeek/Drunk"
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
            sampler2D _MainTex;
            float4 _Ghost;     // xy: uv offset of the second image
            float _GhostMix;   // 0..0.5 weight of the second image
            float _Blur;       // blur radius (uv) at the centre, grows toward the edges
            float _Vignette;
            float _Aspect;

            fixed3 Soft(float2 uv, float2 r)
            {
                // Five taps: centre plus a rotated cross, cheap and smooth enough for a slight blur.
                fixed3 c = tex2D(_MainTex, uv).rgb * 0.36;
                c += tex2D(_MainTex, uv + float2( r.x,  0.38 * r.y)).rgb * 0.16;
                c += tex2D(_MainTex, uv + float2(-r.x, -0.38 * r.y)).rgb * 0.16;
                c += tex2D(_MainTex, uv + float2(-0.38 * r.x,  r.y)).rgb * 0.16;
                c += tex2D(_MainTex, uv + float2( 0.38 * r.x, -r.y)).rgb * 0.16;
                return c;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 d = uv - 0.5;
                d.x *= _Aspect;
                float r2 = dot(d, d);
                float br = _Blur * (0.4 + 2.2 * r2);
                float2 r = float2(br, br * _Aspect);
                fixed3 c = Soft(uv, r);
                // Double vision: a faint copy beside the picture (blurred a bit more, so it reads as out of focus).
                fixed3 g = Soft(uv + _Ghost.xy, r * 1.6);
                c = lerp(c, g, _GhostMix);
                // Light vignette, the edges sink a little.
                c *= 1.0 - _Vignette * saturate(r2 * 1.5);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
