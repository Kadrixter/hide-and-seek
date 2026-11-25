// Planar mirror surface (rendered by PlanarMirror). The glass is projected through the mirror camera (_MirrorVP)
// into the part of the reflection texture that camera filled (_ReflRect.xy, half a texel in .zw), so a reflection
// kept for a frame or two stays put on the glass. MIRROR_OFF (the "Lustro" option is off, or nothing rendered yet):
// a static dark glass with a soft gradient and a faint sheen that drifts with the view, no texture at all.
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
            #pragma multi_compile_local __ MIRROR_OFF
            #include "UnityCG.cginc"

            sampler2D _ReflTex;
            fixed4 _Tint;
            float4x4 _MirrorVP;
            float4 _ReflRect;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 refl : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 world : TEXCOORD2;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityWorldToClipPos(world.xyz);
                o.refl = mul(_MirrorVP, world);
                o.uv = v.texcoord.xy;
                o.world = world.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
            #if MIRROR_OFF
                // Dark smoky glass: lighter at the top, a broad diagonal sheen and a thin streak that slide as you move.
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float drift = (view.x + view.z) * 0.35 + view.y * 0.2;
                // Colours picked as they should look on screen (sRGB), converted for a linear-space project.
                float3 bottom = float3(0.085, 0.09, 0.10);
                float3 top = float3(0.20, 0.21, 0.235);
                float3 col = lerp(bottom, top, smoothstep(0.0, 1.0, i.uv.y));
                float band = i.uv.x * 0.8 + i.uv.y * 0.6 + drift;
                col += 0.07 * exp(-pow((frac(band * 0.5) - 0.5) * 4.0, 2.0));
                col += 0.06 * exp(-pow((frac(band * 0.5 + 0.18) - 0.5) * 22.0, 2.0));
            #ifndef UNITY_COLORSPACE_GAMMA
                col = GammaToLinearSpace(col);
            #endif
                return fixed4(col * _Tint.rgb, 1);
            #else
                float2 uv = (i.refl.xy / i.refl.w) * 0.5 + 0.5;
                uv = clamp(uv * _ReflRect.xy, _ReflRect.zw, _ReflRect.xy - _ReflRect.zw);
                return tex2D(_ReflTex, uv) * _Tint;
            #endif
            }
            ENDCG
        }
    }
}
