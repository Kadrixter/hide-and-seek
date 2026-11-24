// SMAA 1x (Jimenez et al., MIT licence - see HnsSMAA.hlsl) for the Built-in pipeline, used by GfxPost.
// Pass 0: luma edge detection (on gamma-corrected colour: the garage is dark, linear luma would miss most edges),
// pass 1: blending weights (needs the area / search lookup textures), pass 2: neighbourhood blending.
Shader "Hidden/HnsSmaa"
{
    Properties
    {
        _MainTex ("", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "HnsSafe.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _AreaTex;
    sampler2D _SearchTex;
    sampler2D _BlendTex;

    #define SMAA_RT_METRICS float4(_MainTex_TexelSize.xy, _MainTex_TexelSize.zw)
    #define SMAA_CUSTOM_SL 1
    #define SMAATexture2D(tex) sampler2D tex
    #define SMAATexturePass2D(tex) tex
    #define SMAASampleLevelZero(tex, coord) HnsSafe4(tex2Dlod(tex, float4(coord, 0.0, 0.0)))
    #define SMAASampleLevelZeroPoint(tex, coord) tex2Dlod(tex, float4(coord, 0.0, 0.0))
    #define SMAASampleLevelZeroOffset(tex, coord, offset) tex2Dlod(tex, float4(coord + offset * SMAA_RT_METRICS.xy, 0.0, 0.0))
    #define SMAASample(tex, coord) tex2D(tex, coord)
    #define SMAASampleOffset(tex, coord, offset) tex2D(tex, coord + offset * SMAA_RT_METRICS.xy)
    #define SMAA_FLATTEN UNITY_FLATTEN
    #define SMAA_BRANCH UNITY_BRANCH
    #define SMAA_AREATEX_SELECT(s) s.rg
    #define SMAA_SEARCHTEX_SELECT(s) s.r
    #define SMAA_THRESHOLD 0.07
    #define SMAA_MAX_SEARCH_STEPS 16
    #define SMAA_MAX_SEARCH_STEPS_DIAG 8
    #define SMAA_CORNER_ROUNDING 25

    // Colour as SMAA wants it (perceptual, 0..1): the camera is HDR and linear.
    float4 HnsSmaaGamma(float4 c) { return float4(sqrt(saturate(c.rgb)), c.a); }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: edges
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #define SMAASamplePoint(tex, coord) HnsSmaaGamma(tex2D(tex, coord))
            #include "HnsSMAA.hlsl"

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 off[3] : TEXCOORD1; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                SMAAEdgeDetectionVS(o.uv, o.off);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(SMAALumaEdgeDetectionPS(i.uv, i.off, _MainTex), 0.0, 0.0);
            }
            ENDCG
        }

        // 1: blending weights (_MainTex = edges)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #define SMAASamplePoint(tex, coord) tex2D(tex, coord)
            #include "HnsSMAA.hlsl"

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 pix : TEXCOORD1; float4 off[3] : TEXCOORD2; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                SMAABlendingWeightCalculationVS(o.uv, o.pix, o.off);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return SMAABlendingWeightCalculationPS(i.uv, i.pix, i.off, _MainTex, _AreaTex, _SearchTex, float4(0.0, 0.0, 0.0, 0.0));
            }
            ENDCG
        }

        // 2: neighbourhood blending (_MainTex = colour, _BlendTex = weights)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #define SMAASamplePoint(tex, coord) tex2D(tex, coord)
            #include "HnsSMAA.hlsl"

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 off : TEXCOORD1; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                SMAANeighborhoodBlendingVS(o.uv, o.off);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return SMAANeighborhoodBlendingPS(i.uv, i.off, _MainTex, _BlendTex);
            }
            ENDCG
        }
    }
    Fallback Off
}
