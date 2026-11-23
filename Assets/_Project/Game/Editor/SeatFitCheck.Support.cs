using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// SeatFitCheck, the other way round: does the body actually rest on the furniture? For every pose the gap between
    /// the furniture of the seat and: the hips (the lowest corners of the torso and the thighs at the hips), the back
    /// (the torso's back face) where the pose leans on a backrest / armrest, and the legs where they lie on the
    /// cushions. Gaps above <see cref="SupportMax"/> are problems (the body hovers).
    /// </summary>
    public static partial class SeatFitCheck
    {
        const float SupportMax = 0.03f;
        static readonly System.Text.StringBuilder SupportLog = new System.Text.StringBuilder();

        /// <summary>Poses that lean back on something (sofa backrest or the armrest).</summary>
        static bool LeansBack(int v, SeatShape shape) => shape.Kind == SeatKind.Sofa && (v == 0 || v == 1 || v == 3 || v == 5 || v == 7);

        static IEnumerable<(string, float)> Support(int v, SeatShape shape, PartBox[] boxes, Transform seat, List<Mesh3> meshes, Transform furniture)
        {
            // Hips: the lowest corners of the torso and of the thighs' hip ends.
            var hips = new List<Vector3>();
            var torso = World(boxes[(int)BodyPart.Torso], seat);
            hips.AddRange(Lowest(torso, 4));
            foreach (int th in new[] { (int)BodyPart.ThighR, (int)BodyPart.ThighL })
            {
                var b = World(boxes[th], seat);
                var top = new List<Vector3>();
                for (int c = 0; c < 8; c++)
                    if (((c >> 1) & 1) == 1) top.Add(b.Point((c & 1) == 0 ? -1f : 1f, 1f, (c & 4) == 0 ? -1f : 1f)); // hip end (local +y)
                top.Sort((a, c) => a.y.CompareTo(c.y));
                hips.Add(top[0]);
                hips.Add(top[1]);
            }
            yield return ("hips on the seat", MinGap(hips, meshes, furniture));
            if (LeansBack(v, shape))
            {
                // Back face of the torso (local -z), lower and middle points.
                var back = new List<Vector3>();
                for (float u = -0.8f; u <= 0.81f; u += 0.4f)
                for (float h = -0.8f; h <= 0.41f; h += 0.4f)
                    back.Add(torso.Point(u, h, -1f));
                yield return ("back on the backrest / armrest", MinGap(back, meshes, furniture));
            }
            if (SeatPoses.Lying(v) || v == 7)
            {
                var legs = new List<Vector3>();
                foreach (int part in new[] { (int)BodyPart.ShinR, (int)BodyPart.ShinL, (int)BodyPart.ThighR, (int)BodyPart.ThighL })
                    legs.AddRange(Lowest(World(boxes[part], seat), 2));
                yield return ("legs on the cushions", MinGap(legs, meshes, furniture));
            }
        }

        static List<Vector3> Lowest(PartBox b, int n)
        {
            var pts = new List<Vector3>();
            for (int c = 0; c < 8; c++) pts.Add(b.Point((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
            pts.Sort((a, c) => a.y.CompareTo(c.y));
            return pts.GetRange(0, n);
        }

        static float MinGap(List<Vector3> pts, List<Mesh3> meshes, Transform furniture)
        {
            float best = float.MaxValue;
            foreach (var p in pts) best = Mathf.Min(best, FurnitureDistance(p, meshes, 0.4f, out _, furniture));
            return best > 1f ? 1f : best;
        }
    }
}
