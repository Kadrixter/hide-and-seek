using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>The bar of the pool lamp (walnut beam / steel tube) and what hangs it from the ceiling.</summary>
    public sealed partial class PoolLampModel
    {
        /// <summary>Height of the bar (beam with its boards / tube diameter).</summary>
        float BarHeight => _style == 1 ? 0.044f : 0.099f;

        /// <summary>Builds the bar; returns the height of its underside.</summary>
        float BuildBar(Transform root)
        {
            float y0 = RimY + PoolLampMeshes.ShadeHeight(_style) + (_style == 1 ? 0.11f : 0.095f);
            var bar = new GameObject("Bar") { layer = root.gameObject.layer }.transform;
            bar.SetParent(root, false);
            if (_style == 1)
            {
                var steel = _glass;
                _kit.Part("Tube", bar, PoolLampMeshes.Rod(0.022f, 1.72f), steel, new Vector3(-0.86f, y0 + 0.022f, 0f), Quaternion.Euler(0f, 0f, -90f));
                _kit.Part("KnobR", bar, PoolLampMeshes.EndKnob(), _kit.Brass, new Vector3(0.858f, y0 + 0.022f, 0f), Quaternion.Euler(0f, 0f, -90f));
                _kit.Part("KnobL", bar, PoolLampMeshes.EndKnob(), _kit.Brass, new Vector3(-0.858f, y0 + 0.022f, 0f), Quaternion.Euler(0f, 0f, 90f));
                foreach (float x in ShadeX)
                    _kit.Part("Collar", bar, PoolLampMeshes.Rod(0.0245f, 0.022f), _kit.Brass, new Vector3(x - 0.011f, y0 + 0.022f, 0f), Quaternion.Euler(0f, 0f, -90f));
                foreach (float x in new[] { -HangX, HangX })
                    _kit.Part("Collar", bar, PoolLampMeshes.Rod(0.0245f, 0.03f), _kit.Brass, new Vector3(x - 0.015f, y0 + 0.022f, 0f), Quaternion.Euler(0f, 0f, -90f));
                return y0;
            }
            // Walnut beam: a bullnosed bottom board, the body with a brass inlay line on both faces, a bead and a wider top board.
            var flat = Quaternion.LookRotation(Vector3.right, Vector3.forward);
            _kit.Part("BottomBoard", bar, Board("lampbot", 1.74f, 0.092f, 0.016f, 0.03f, 0.0065f), _kit.TableWood, new Vector3(0f, y0 + 0.008f, 0f), flat);
            _kit.Part("Body", bar, BoardMeshes.RoundedBox(new Vector3(1.68f, 0.068f, 0.066f), 0.005f, 4), _kit.LegWood, new Vector3(0f, y0 + 0.048f, 0f));
            foreach (float z in new[] { -0.0335f, 0.0335f })
                _kit.Part("Inlay", bar, BoardMeshes.RoundedBox(new Vector3(1.6f, 0.005f, 0.003f), 0.0012f, 2), _kit.Brass, new Vector3(0f, y0 + 0.05f, z));
            _kit.Part("Bead", bar, BoardMeshes.RoundedBox(new Vector3(1.71f, 0.009f, 0.076f), 0.0044f, 3), _kit.TableWood, new Vector3(0f, y0 + 0.0795f, 0f));
            _kit.Part("TopBoard", bar, Board("lamptop", 1.77f, 0.102f, 0.018f, 0.035f, 0.007f), _kit.TableWood, new Vector3(0f, y0 + 0.09f, 0f), flat);
            foreach (float sx in new[] { -1f, 1f })
            {
                _kit.Part("Finial", bar, PoolLampMeshes.Finial(), _kit.LegWood, new Vector3(sx * 0.83f, y0 + 0.001f, 0f));
                _kit.Part("EndMedal", bar, EndMedal(), _kit.Brass, new Vector3(sx * 0.8395f, y0 + 0.048f, 0f), Quaternion.Euler(0f, 0f, -90f * sx));
            }
            return y0;
        }

        /// <summary>Flat board (rounded corners, bullnosed edges) lying in the XZ plane, centred, thickness along Y (use the "flat" rotation).</summary>
        static Mesh Board(string key, float len, float width, float thick, float corner, float bevel)
        {
            var o = new List<Vector2>();
            float hx = len * 0.5f - corner, hz = width * 0.5f - corner;
            Vector2[] c = { new(hx, hz), new(-hx, hz), new(-hx, -hz), new(hx, -hz) };
            for (int k = 0; k < 4; k++)
                for (int i = 0; i <= 6; i++)
                {
                    float a = (k * 90f + i * 15f) * Mathf.Deg2Rad;
                    o.Add(c[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * corner);
                }
            return BoardMeshes.Extrude(key, o, _ => thick * 0.5f, bevel, 3, 4f);
        }

        static Mesh EndMedal()
        {
            var p = new List<Vector2> { new(0f, 0.004f), new(0.012f, 0.0035f), new(0.017f, 0.0025f), new(0.02f, 0.001f), new(0.021f, -0.002f), new(0f, -0.002f) };
            return BoardMeshes.Lathe("poolendmedal", p, 28, 6f);
        }

        /// <summary>Two hangers from the bar top (<paramref name="barTop"/>) to canopies on the ceiling (<paramref name="ceilingY"/>).</summary>
        void BuildHangers(Transform root, float barTop, float ceilingY)
        {
            var h = new GameObject("Hangers") { layer = root.gameObject.layer }.transform;
            h.SetParent(root, false);
            foreach (float x in new[] { -HangX, HangX })
            {
                _kit.Part("Canopy", h, PoolLampMeshes.Canopy(), _style == 1 ? _glass : _kit.Brass, new Vector3(x, ceilingY, 0f));
                if (_style == 1)
                {
                    // Black down-rod on a brass ball swivel; the cable runs inside.
                    float top = ceilingY - 0.05f;
                    _kit.Part("Swivel", h, Ball(0.013f), _kit.Brass, new Vector3(x, top, 0f));
                    _kit.Part("DownRod", h, PoolLampMeshes.Rod(0.0075f, top - barTop + 0.01f), _glass, new Vector3(x, barTop - 0.01f, 0f));
                    _kit.Part("RodNut", h, PoolLampMeshes.Rod(0.011f, 0.014f), _kit.Brass, new Vector3(x, barTop - 0.004f, 0f));
                    continue;
                }
                // Hook loop under the canopy, eye bolt on the beam, brass chain in between.
                float hookC = ceilingY - 0.05f - 0.0085f;
                float eyeC = barTop + 0.008f + 0.011f;
                float chainTop = hookC - 0.009f - 0.0028f, chainBottom = eyeC + 0.011f + 0.0028f;
                float span = chainTop - chainBottom;
                int count = PoolLampMeshes.LinkCount(span);
                var chain = _kit.Part("Chain", h, PoolLampMeshes.Chain(span), _kit.Brass, new Vector3(x, chainTop, 0f));
                chain.transform.localScale = new Vector3(1f, span / PoolLampMeshes.ChainSpan(span), 1f);
                _kit.Part("Hook", h, PoolLampMeshes.Ring(0.009f, 0.0028f), _kit.Brass, new Vector3(x, hookC, 0f), Quaternion.Euler(0f, 90f, 0f));
                _kit.Part("HookStem", h, PoolLampMeshes.Rod(0.004f, 0.012f), _kit.Brass, new Vector3(x, hookC + 0.006f, 0f));
                _kit.Part("Eye", h, PoolLampMeshes.Ring(0.011f, 0.003f), _kit.Brass, new Vector3(x, eyeC, 0f), Quaternion.Euler(0f, ((count - 1) & 1) == 1 ? 0f : 90f, 0f));
                _kit.Part("EyeShank", h, PoolLampMeshes.Rod(0.0042f, 0.012f), _kit.Brass, new Vector3(x, barTop - 0.003f, 0f));
                _kit.Part("EyeWasher", h, PoolLampMeshes.Rod(0.01f, 0.003f), _kit.Brass, new Vector3(x, barTop - 0.0015f, 0f));
            }
            if (_style == 1) return;
            // Fabric cable wound loosely round the left chain, into the beam through a brass grommet.
            var cableMat = _kit.Mat(new Color(0.16f, 0.11f, 0.07f), 0f, 0.25f, BoardArt.GrainFine);
            float xc = -HangX, yTop = ceilingY - 0.04f, yBot = barTop;
            Vector3 grommet = new Vector3(xc + 0.065f, yBot + 0.004f, 0f);
            float length = yTop - yBot;
            System.Func<float, Vector3> path = t =>
            {
                float y = Mathf.Lerp(yTop, yBot + 0.07f, Mathf.Clamp01(t / 0.9f));
                float a = (yTop - y) / 0.16f * Mathf.PI * 2f;
                float r = 0.013f * Mathf.Clamp01((yTop - y) / 0.03f) + 0.006f;
                var spiral = new Vector3(xc + Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                if (t <= 0.9f) return spiral;
                float w = (t - 0.9f) / 0.1f;
                Vector3 mid = new Vector3(grommet.x - 0.01f, yBot + 0.05f, spiral.z * 0.3f);
                return Vector3.Lerp(Vector3.Lerp(spiral, mid, w), Vector3.Lerp(mid, grommet, w), w);
            };
            _kit.Part("Cable", h, PoolLampMeshes.Sweep("lampcable" + length.ToString("0.000"), path, false, 0.0026f, Mathf.Max(40, (int)(length / 0.008f)), 8), cableMat, Vector3.zero);
            _kit.Part("Grommet", h, PoolLampMeshes.Rod(0.0055f, 0.006f), _kit.Brass, new Vector3(grommet.x, yBot - 0.002f, 0f));
        }

        static Mesh Ball(float r)
        {
            var p = new List<Vector2>();
            BoardMeshes.Arc(p, 0f, 0f, r, -90f, 90f, 10);
            return BoardMeshes.Lathe("poolball" + r.ToString("0.000"), p, 20, 6f);
        }
    }
}
