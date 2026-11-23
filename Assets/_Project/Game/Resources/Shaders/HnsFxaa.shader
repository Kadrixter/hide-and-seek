// FXAA 3.11 "quality" (after Timothy Lottes' public algorithm), written for the Built-in pipeline (GfxPost).
// Same neighbour naming as the original (N = -y). Luma is taken from the gamma-corrected colour on the fly (the camera is HDR + linear), 12 search steps.
Shader "Hidden/HnsFxaa"
{
    Properties
    {
        _MainTex ("", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            #define SUBPIX 0.75
            #define EDGE_THRESHOLD 0.125
            #define EDGE_THRESHOLD_MIN 0.0312

            float Luma(float2 uv)
            {
                float3 c = saturate(tex2Dlod(_MainTex, float4(uv, 0, 0)).rgb);
                return sqrt(dot(c, float3(0.299, 0.587, 0.114)));
            }

            static const float Steps[12] = { 1.0, 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0, 8.0 };

            float4 frag(v2f_img i) : SV_Target
            {
                float2 rcp = _MainTex_TexelSize.xy;
                float2 pos = i.uv;
                float4 rgbM = tex2Dlod(_MainTex, float4(pos, 0, 0));
                float lumaM = sqrt(dot(saturate(rgbM.rgb), float3(0.299, 0.587, 0.114)));
                float lumaS = Luma(pos + float2(0, rcp.y));
                float lumaE = Luma(pos + float2(rcp.x, 0));
                float lumaN = Luma(pos + float2(0, -rcp.y));
                float lumaW = Luma(pos + float2(-rcp.x, 0));

                float maxL = max(max(lumaN, lumaW), max(max(lumaE, lumaS), lumaM));
                float minL = min(min(lumaN, lumaW), min(min(lumaE, lumaS), lumaM));
                float range = maxL - minL;
                if (range < max(EDGE_THRESHOLD_MIN, maxL * EDGE_THRESHOLD)) return rgbM;

                float lumaNW = Luma(pos + float2(-rcp.x, -rcp.y));
                float lumaSE = Luma(pos + float2(rcp.x, rcp.y));
                float lumaNE = Luma(pos + float2(rcp.x, -rcp.y));
                float lumaSW = Luma(pos + float2(-rcp.x, rcp.y));

                float lumaNS = lumaN + lumaS;
                float lumaWE = lumaW + lumaE;
                float edgeHorz1 = -2.0 * lumaM + lumaNS;
                float edgeVert1 = -2.0 * lumaM + lumaWE;
                float lumaNESE = lumaNE + lumaSE;
                float lumaNWNE = lumaNW + lumaNE;
                float edgeHorz2 = -2.0 * lumaE + lumaNESE;
                float edgeVert2 = -2.0 * lumaN + lumaNWNE;
                float lumaNWSW = lumaNW + lumaSW;
                float lumaSWSE = lumaSW + lumaSE;
                float edgeHorz4 = abs(edgeHorz1) * 2.0 + abs(edgeHorz2);
                float edgeVert4 = abs(edgeVert1) * 2.0 + abs(edgeVert2);
                float edgeHorz3 = -2.0 * lumaW + lumaNWSW;
                float edgeVert3 = -2.0 * lumaS + lumaSWSE;
                float edgeHorz = abs(edgeHorz3) + edgeHorz4;
                float edgeVert = abs(edgeVert3) + edgeVert4;

                bool horzSpan = edgeHorz >= edgeVert;
                float lengthSign = horzSpan ? rcp.y : rcp.x;
                float subpixA = (lumaNS + lumaWE) * 2.0 + (lumaNWSW + lumaNESE);
                if (!horzSpan) { lumaN = lumaW; lumaS = lumaE; }
                float subpixB = subpixA * (1.0 / 12.0) - lumaM;
                float gradientN = lumaN - lumaM;
                float gradientS = lumaS - lumaM;
                float lumaNN = lumaN + lumaM;
                float lumaSS = lumaS + lumaM;
                bool pairN = abs(gradientN) >= abs(gradientS);
                float gradient = max(abs(gradientN), abs(gradientS));
                if (pairN) lengthSign = -lengthSign;
                float subpixC = saturate(abs(subpixB) / range);

                float2 posB = pos;
                float2 offNP = horzSpan ? float2(rcp.x, 0) : float2(0, rcp.y);
                if (horzSpan) posB.y += lengthSign * 0.5; else posB.x += lengthSign * 0.5;

                float2 posN = posB - offNP * Steps[0];
                float2 posP = posB + offNP * Steps[0];
                float subpixD = -2.0 * subpixC + 3.0;
                float lumaEndN = Luma(posN);
                float subpixE = subpixC * subpixC;
                float lumaEndP = Luma(posP);
                if (!pairN) lumaNN = lumaSS;
                float gradientScaled = gradient * 0.25;
                float lumaMM = lumaM - lumaNN * 0.5;
                float subpixF = subpixD * subpixE;
                bool lumaMLTZero = lumaMM < 0.0;
                lumaEndN -= lumaNN * 0.5;
                lumaEndP -= lumaNN * 0.5;
                bool doneN = abs(lumaEndN) >= gradientScaled;
                bool doneP = abs(lumaEndP) >= gradientScaled;
                if (!doneN) posN -= offNP * Steps[1];
                if (!doneP) posP += offNP * Steps[1];

                [loop]
                for (int s = 2; s < 12 && !(doneN && doneP); s++)
                {
                    if (!doneN) lumaEndN = Luma(posN) - lumaNN * 0.5;
                    if (!doneP) lumaEndP = Luma(posP) - lumaNN * 0.5;
                    doneN = doneN || abs(lumaEndN) >= gradientScaled;
                    doneP = doneP || abs(lumaEndP) >= gradientScaled;
                    if (!doneN) posN -= offNP * Steps[s];
                    if (!doneP) posP += offNP * Steps[s];
                }

                float dstN = horzSpan ? pos.x - posN.x : pos.y - posN.y;
                float dstP = horzSpan ? posP.x - pos.x : posP.y - pos.y;
                bool goodSpanN = (lumaEndN < 0.0) != lumaMLTZero;
                bool goodSpanP = (lumaEndP < 0.0) != lumaMLTZero;
                float spanLength = dstP + dstN;
                bool directionN = dstN < dstP;
                float dst = min(dstN, dstP);
                bool goodSpan = directionN ? goodSpanN : goodSpanP;
                float subpixG = subpixF * subpixF;
                float pixelOffset = dst * (-1.0 / spanLength) + 0.5;
                float subpixH = subpixG * SUBPIX;
                float pixelOffsetGood = goodSpan ? pixelOffset : 0.0;
                float pixelOffsetSubpix = max(pixelOffsetGood, subpixH);
                if (horzSpan) pos.y += pixelOffsetSubpix * lengthSign; else pos.x += pixelOffsetSubpix * lengthSign;
                return float4(tex2Dlod(_MainTex, float4(pos, 0, 0)).rgb, rgbM.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
