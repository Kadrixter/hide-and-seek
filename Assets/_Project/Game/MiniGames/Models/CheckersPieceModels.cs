using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Turned wooden checkers discs for the chess table (same boxwood / ebony as the chess pieces): a disc with rounded
    /// edges, a reeded rim and two cut rings on the face; a king ("damka") is two discs stacked, the top one with an
    /// inlaid brass ring. Local space: the disc stands on y = 0.
    /// </summary>
    public static class CheckersPieceModels
    {
        public const float Radius = 0.03f, Thickness = 0.012f;

        /// <summary>Height of a man / a king, for grabbing from the top.</summary>
        public static float Height(bool king) => king ? Thickness * 2f : Thickness;

        public static Mesh Disc
        {
            get
            {
                var p = new List<Vector2> { new(0f, 0f), new(Radius - 0.0018f, 0f) };
                BoardMeshes.Arc(p, Radius - 0.0018f, 0.0018f, 0.0018f, -90f, 0f, 4);
                // Reeded rim: three shallow beads round the edge.
                for (int i = 0; i <= 12; i++)
                {
                    float y = Mathf.Lerp(0.0018f, Thickness - 0.0018f, i / 12f);
                    p.Add(new Vector2(Radius - 0.0006f * (1f - Mathf.Abs(Mathf.Cos(i / 12f * Mathf.PI * 3f))), y));
                }
                BoardMeshes.Arc(p, Radius - 0.0018f, Thickness - 0.0018f, 0.0018f, 0f, 90f, 4);
                // Face: an outer cut ring, a flat band, an inner cut ring, a slightly dished centre.
                p.Add(new Vector2(0.0245f, Thickness));
                p.Add(new Vector2(0.0238f, Thickness - 0.0009f));
                p.Add(new Vector2(0.0231f, Thickness));
                p.Add(new Vector2(0.0150f, Thickness));
                p.Add(new Vector2(0.0144f, Thickness - 0.0008f));
                p.Add(new Vector2(0.0138f, Thickness));
                p.Add(new Vector2(0.006f, Thickness - 0.0005f));
                p.Add(new Vector2(0f, Thickness - 0.0006f));
                return BoardMeshes.Lathe("checkersdisc", p, 40, 6f);
            }
        }

        /// <summary>Brass ring inlaid in the face of the king's top disc.</summary>
        public static Mesh CrownRing
        {
            get
            {
                // Half-buried round bead: outer edge -> over the top -> inner edge (outward normals).
                var q = new List<Vector2>();
                BoardMeshes.Arc(q, 0.0185f, -0.0005f, 0.0016f, 0f, 180f, 8);
                return BoardMeshes.Lathe("checkerscrown", q, 36, 6f);
            }
        }

        /// <summary>Five-point star for the king's face: a thin brass plate made in the (z, y) plane, laid flat with Euler(0, 0, 90).</summary>
        public static Mesh Star
        {
            get
            {
                const string key = "checkersstar";
                if (BoardMeshes.TryGet(key, out var cached)) return cached;
                var outline = new List<Vector2>();
                for (int i = 0; i < 10; i++)
                {
                    float a = (90f + i * 36f) * Mathf.Deg2Rad;
                    float r = i % 2 == 0 ? 0.0105f : 0.0045f;
                    outline.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
                var m = BoardMeshes.Extrude(key, outline, _ => 0.0004f, 0.0002f, 2, 20f);
                return m;
            }
        }
    }
}
