using HideAndSeek.Game.Gfx;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Mirror camera maths: the reflection matrix, the oblique near plane, the crop to the glass's on-screen rectangle
    /// (only that part of the screen is rendered, into a matching corner of the render texture) and the cheaper lighting
    /// used by the lower "Lustro" levels during the reflection render.
    /// </summary>
    public partial class PlanarMirror
    {
        private static readonly Vector3[] QuadCorners =
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };

        /// <summary>
        /// The glass's bounds in the player camera's normalized device coordinates (-1..1), grown by <paramref name="margin"/>
        /// (fraction of its size) so a reflection reused for a few frames still covers it when the view turns.
        /// The whole screen when a corner is behind the camera; false when the glass is off screen.
        /// </summary>
        private bool ScreenRect(Camera src, float margin, out Rect ndc)
        {
            Matrix4x4 vp = src.projectionMatrix * src.worldToCameraMatrix;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var c in QuadCorners)
            {
                Vector3 w = transform.TransformPoint(c);
                Vector4 p = vp * new Vector4(w.x, w.y, w.z, 1f);
                if (p.w < src.nearClipPlane * 0.5f)
                {
                    ndc = new Rect(-1f, -1f, 2f, 2f);
                    return true;
                }
                float x = p.x / p.w, y = p.y / p.w;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }
            float mx = (x1 - x0) * margin + 0.01f, my = (y1 - y0) * margin + 0.01f;
            float lim = 1f + margin * 1.5f; // reused frames may need a strip just off screen
            x0 = Mathf.Max(x0 - mx, -lim); x1 = Mathf.Min(x1 + mx, lim);
            y0 = Mathf.Max(y0 - my, -lim); y1 = Mathf.Min(y1 + my, lim);
            ndc = new Rect(x0, y0, x1 - x0, y1 - y0);
            return x1 > -1f && y1 > -1f && x0 < 1f && y0 < 1f && x1 > x0 && y1 > y0;
        }

        /// <summary>Clip-space transform that stretches the NDC rectangle <paramref name="r"/> over the whole viewport.</summary>
        private static Matrix4x4 Crop(Rect r)
        {
            var m = Matrix4x4.identity;
            m.m00 = 2f / r.width;
            m.m03 = -(r.xMin + r.xMax) / r.width;
            m.m11 = 2f / r.height;
            m.m13 = -(r.yMin + r.yMax) / r.height;
            return m;
        }

        /// <summary>QualitySettings values changed for the reflection render only.</summary>
        private struct LightingScope
        {
            public int Lights;
            public ShadowQuality Shadows;
            public float ShadowDistance, Lod;
        }

        private static LightingScope LowerLighting(in MirrorSettings.Level q)
        {
            var s = new LightingScope
            {
                Lights = QualitySettings.pixelLightCount, Shadows = QualitySettings.shadows,
                ShadowDistance = QualitySettings.shadowDistance, Lod = QualitySettings.lodBias,
            };
            if (q.PixelLights > 0 && q.PixelLights < s.Lights) QualitySettings.pixelLightCount = q.PixelLights;
            if (!q.Shadows) { if (s.Shadows != ShadowQuality.Disable) QualitySettings.shadows = ShadowQuality.Disable; }
            else if (q.ShadowDistance > 0f && q.ShadowDistance < s.ShadowDistance) QualitySettings.shadowDistance = q.ShadowDistance;
            if (q.LodScale > 0f && !Mathf.Approximately(q.LodScale, 1f)) QualitySettings.lodBias = s.Lod * q.LodScale;
            return s;
        }

        private static void RestoreLighting(in LightingScope s)
        {
            if (QualitySettings.pixelLightCount != s.Lights) QualitySettings.pixelLightCount = s.Lights;
            if (QualitySettings.shadows != s.Shadows) QualitySettings.shadows = s.Shadows;
            if (!Mathf.Approximately(QualitySettings.shadowDistance, s.ShadowDistance)) QualitySettings.shadowDistance = s.ShadowDistance;
            if (!Mathf.Approximately(QualitySettings.lodBias, s.Lod)) QualitySettings.lodBias = s.Lod;
        }

        private static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal)
        {
            const float offset = 0.02f;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(pos + normal * offset);
            Vector3 cnormal = m.MultiplyVector(normal).normalized;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        private static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y; m.m02 = -2f * p.x * p.z; m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x; m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z; m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x; m.m21 = -2f * p.z * p.y; m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }
    }
}
