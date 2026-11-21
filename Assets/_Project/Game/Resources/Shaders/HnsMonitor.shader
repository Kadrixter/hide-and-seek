Shader "HideAndSeek/Monitor"
{
    Properties
    {
        _MainTex ("Feed", 2D) = "black" {}
        _Gain ("Gain", Float) = 2.6
        _Tint ("Tint", Color) = (0.75, 1, 0.8, 1)
        _Glitch ("Glitch", Float) = 0
        _Alarm ("Alarm", Float) = 0
        _Snow ("Snow", Float) = 0
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
            sampler2D _MainTex;
            float _Gain, _Glitch, _Alarm, _Snow;
            fixed4 _Tint;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float band = step(0.97, hash(float2(floor(uv.y * 30.0), floor(_Time.y * 8.0)))) * _Glitch;
                uv.x += band * 0.05;
                fixed3 c = tex2D(_MainTex, uv).rgb;
                float l = dot(c, float3(0.299, 0.587, 0.114));
                l = saturate(pow(max(l, 0.0), 0.6) * _Gain);
                l *= 0.88 + 0.12 * sin(uv.y * 360.0 + _Time.y * 6.0);            // scanlines
                l += (hash(uv * 400.0 + _Time.y) - 0.5) * (0.08 + 0.5 * _Glitch); // noise
                // Old-TV snow while the monitor switches: coarse flickering noise with a rolling bright band.
                float snow = hash(floor(i.uv * float2(240.0, 135.0)) + floor(_Time.y * 40.0));
                snow *= 0.75 + 0.35 * step(0.9, frac(i.uv.y * 1.3 - _Time.y * 1.7));
                l = lerp(l, snow, _Snow);
                float2 d = uv - 0.5;
                l *= saturate(1.2 - dot(d, d) * 1.4);
                fixed3 col = l * _Tint.rgb;
                col = lerp(col, col * float3(1.4, 0.5, 0.5), _Alarm * (0.5 + 0.5 * sin(_Time.y * 9.0)) * 0.5);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
