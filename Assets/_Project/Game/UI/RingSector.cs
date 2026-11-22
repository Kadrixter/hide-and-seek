using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// uGUI graphic: a piece of a ring (annular sector) around the centre of its rect, with antialiased edges (a soft
    /// fringe of <see cref="Feather"/> canvas units) and a constant-width gap between neighbouring sectors (the side
    /// edges are moved in by half the gap, so the gap is the same at the inner and the outer radius). A full ring when
    /// the sweep is 360 degrees. Angles are measured clockwise from 12 o'clock.
    /// </summary>
    public class RingSector : MaskableGraphic
    {
        [SerializeField] private float inner = 100f, outer = 200f;
        [SerializeField] private float centreAngle, sweep = 360f;
        [SerializeField] private float gap, feather = 1.2f;

        public float Inner { get => inner; set { if (inner != value) { inner = value; SetVerticesDirty(); } } }
        public float Outer { get => outer; set { if (outer != value) { outer = value; SetVerticesDirty(); } } }
        public float CentreAngle { get => centreAngle; set { if (centreAngle != value) { centreAngle = value; SetVerticesDirty(); } } }
        public float Sweep { get => sweep; set { if (sweep != value) { sweep = value; SetVerticesDirty(); } } }
        public float Gap { get => gap; set { if (gap != value) { gap = value; SetVerticesDirty(); } } }
        public float Feather { get => feather; set { if (feather != value) { feather = value; SetVerticesDirty(); } } }

        public static RingSector Create(string name, Transform parent, float inner, float outer, float centreAngle, float sweep, float gap, Color color, float feather = 1.2f)
        {
            var rt = UiKit.Rect(name, parent);
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (outer + feather) * 2f);
            var g = rt.gameObject.AddComponent<RingSector>();
            g.inner = inner;
            g.outer = outer;
            g.centreAngle = centreAngle;
            g.sweep = sweep;
            g.gap = gap;
            g.feather = feather;
            g.color = color;
            g.raycastTarget = false;
            return g;
        }

        /// <summary>Clockwise from 12 o'clock (degrees) to a unit direction (x right, y up).</summary>
        public static Vector2 Dir(float clockDeg)
        {
            float a = clockDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float f = Mathf.Max(0.01f, feather);
            float rIn = Mathf.Max(0f, inner), rOut = Mathf.Max(rIn + 0.5f, outer);
            bool full = sweep >= 359.99f;
            Color32 c = color;
            Color32 c0 = new Color32(c.r, c.g, c.b, 0);

            // Radial rings: fringe in, inner edge, outer edge, fringe out.
            float[] radii = { Mathf.Max(0f, rIn - f), rIn, rOut, rOut + f };
            bool[] solidR = { rIn - f < 0f && rIn <= 0.001f, true, true, false };

            int segs = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(sweep, 1f) / 4f), 2, 96);
            float half = sweep * 0.5f;
            if (full)
            {
                for (int s = 0; s <= segs; s++)
                {
                    var d = Dir(centreAngle - half + sweep * s / segs);
                    for (int r = 0; r < 4; r++) vh.AddVert(d * radii[r], solidR[r] ? c : c0, Vector4.zero);
                }
                for (int s = 0; s < segs; s++)
                for (int r = 0; r < 3; r++)
                {
                    int a = s * 4 + r, b = (s + 1) * 4 + r;
                    vh.AddTriangle(a, b, b + 1);
                    vh.AddTriangle(a, b + 1, a + 1);
                }
                return;
            }

            // Columns: fringe at the start edge, start edge, interior..., end edge, fringe at the end edge. The side
            // edges are straight lines moved in by gap / 2 (so the angle they sit at depends on the radius).
            int cols = segs + 3;
            float g2 = gap * 0.5f;
            for (int col = 0; col < cols; col++)
            {
                for (int r = 0; r < 4; r++)
                {
                    float rad = Mathf.Max(radii[r], 0.5f);
                    float ang;
                    bool solid = solidR[r];
                    if (col == 0 || col == cols - 1)
                    {
                        float off = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp((g2 - f) / rad, -1f, 1f));
                        ang = col == 0 ? centreAngle - half + off : centreAngle + half - off;
                        solid = false;
                    }
                    else
                    {
                        float off = Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp(g2 / rad, -1f, 1f));
                        float a0 = centreAngle - half + off, a1 = centreAngle + half - off;
                        if (a1 < a0) a0 = a1 = centreAngle;
                        ang = Mathf.Lerp(a0, a1, (col - 1) / (float)segs);
                    }
                    vh.AddVert(Dir(ang) * radii[r], solid ? c : c0, Vector4.zero);
                }
            }
            for (int col = 0; col < cols - 1; col++)
            for (int r = 0; r < 3; r++)
            {
                int a = col * 4 + r, b = (col + 1) * 4 + r;
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
            }
        }
    }
}
