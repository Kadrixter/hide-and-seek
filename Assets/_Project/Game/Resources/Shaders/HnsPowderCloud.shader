// Lit, alpha-blended particles for extinguisher powder (jet and the cloud hanging in the air). Half-Lambert wrap
// lighting with the normal turned towards the viewer, so a flashlight behind the player lights the cloud softly and
// the dark garage keeps it dark; a little self-illumination so it never turns pure black; fogged.
Shader "HideAndSeek/PowderCloud"
{
    Properties
    {
        _MainTex ("Puff", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glow ("Self light", Range(0,1)) = 0.06
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Wrap alpha:fade vertex:vert noshadow nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glow;

        struct Input { float2 uv_MainTex; fixed4 color : COLOR; };

        void vert(inout appdata_full v)
        {
            // Billboards: light them as if they faced the camera.
            float3 toCam = normalize(ObjSpaceViewDir(v.vertex));
            v.normal = toCam;
        }

        half4 LightingWrap(SurfaceOutput s, half3 lightDir, half atten)
        {
            half d = dot(s.Normal, lightDir) * 0.5 + 0.5;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (d * atten);
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 t = tex2D(_MainTex, IN.uv_MainTex) * IN.color * _Color;
            o.Albedo = t.rgb;
            o.Emission = t.rgb * _Glow;
            o.Alpha = t.a;
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Particles/Alpha Blended"
}
