using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Real panel gaps and recessed glass of the parked cars, cut into the lofts themselves (no painted lines):
    /// every shut line gets four extra stations (edge, floor, floor, edge); the floor stations are pushed into the
    /// panel and meshed dark, the walls stay paint with crisp normals, so the gap catches light like a real one.
    /// The side glass sits a few millimetres inside its seal and frame.
    /// </summary>
    internal abstract partial class ParkedCar
    {
        /// <summary>Half width of a shut line (edge to edge), half width of its dark floor, depth into the panel.</summary>
        private const float GapHalf = 0.0055f, GapFloor = 0.0018f, GapDepth = 0.0045f;
        /// <summary>How far the side glass sits inside the belt seal and the window frame.</summary>
        private const float GlassRecess = 0.007f;

        /// <summary>Front edge of the front door (behind the front wheel arch).</summary>
        protected float FrontDoorZ => Mathf.Min(ScreenZ - 0.06f, FrontAxle - TireR - 0.08f);
        /// <summary>Rear edge of the rear door (van: rear edge of the sliding door).</summary>
        protected float RearDoorZ => Kind == RearKind.Van ? BPillar - 1.0f : Mathf.Max(CPillar - 0.02f, RearAxle + TireR + 0.06f);

        /// <summary>Vertical door shut lines on the flanks (z).</summary>
        private float[] SideGaps => new[] { FrontDoorZ, BPillar, RearDoorZ };
        /// <summary>Shut lines across the top: bonnet rear edge, boot lid front edge of the saloon (z).</summary>
        private float[] TopGaps => Kind == RearKind.Notch ? new[] { ScreenZ + 0.03f, GlassEndZ - 0.04f } : new[] { ScreenZ + 0.03f };

        /// <summary>Outer radius of the wheel arch lip / cladding (door gaps stop short of it).</summary>
        private float ArchTrimReach => Cladding ? 0.075f : 0.035f;

        private readonly HashSet<(int j, int i)> _gapFloor = new HashSet<(int, int)>();

        protected override float[] BodyBreaks
        {
            get
            {
                var b = new List<float> { ScreenZ, BPillar, CPillar };
                foreach (float g in SideGaps) b.AddRange(GapStations(g));
                foreach (float g in TopGaps) b.AddRange(GapStations(g));
                return b.ToArray();
            }
        }

        private static IEnumerable<float> GapStations(float z) => new[] { z - GapHalf, z - GapFloor, z + GapFloor, z + GapHalf };

        protected override void ShapeBody()
        {
            _gapFloor.Clear();
            var arches = new[] { FrontArch, RearArch };
            for (int j = 0; j < Body.Stations; j++)
            {
                float z = Body.Z[j];
                var s = Body.S[j];
                foreach (float g in SideGaps)
                {
                    float d = Mathf.Abs(z - g);
                    if (d > GapHalf + 0.0005f) continue;
                    Body.SharpStation.Add(j);
                    if (d > GapFloor + 0.0007f) continue;
                    for (int i = CarShapes.BRocker; i <= CarShapes.BShoulder; i++)
                    {
                        // the door cut ends at the arch lip instead of running through it
                        bool nearArch = false;
                        foreach (var a in arches)
                            if (new Vector2(z - a.Z, s[i].y - a.Y).magnitude < a.R + ArchTrimReach + 0.008f) nearArch = true;
                        if (nearArch) continue;
                        s[i].x -= GapDepth;
                        _gapFloor.Add((j, i));
                    }
                }
                foreach (float g in TopGaps)
                {
                    float d = Mathf.Abs(z - g);
                    if (d > GapHalf + 0.0005f || TubAt(z) > 0.02f) continue;
                    Body.SharpStation.Add(j);
                    if (d > GapFloor + 0.0007f) continue;
                    for (int i = CarShapes.BRound; i <= CarShapes.BCentre; i++)
                    {
                        s[i].y -= GapDepth;
                        _gapFloor.Add((j, i));
                    }
                }
            }
        }

        /// <summary>Paint everywhere, but the floors of the shut lines are dark.</summary>
        protected override Material BodyMat(int seg, int j)
        {
            var m = base.BodyMat(seg, j);
            if (m == Paint && _gapFloor.Contains((j, seg)) && _gapFloor.Contains((j, seg + 1))
                && _gapFloor.Contains((j + 1, seg)) && _gapFloor.Contains((j + 1, seg + 1)))
                return Seam;
            return m;
        }

        /// <summary>Side glass (and the pillars between the panes) pushed in behind the seal and the frame.</summary>
        protected override void ShapeCabin()
        {
            for (int j = 0; j < Cabin.Stations; j++)
            {
                if (!SideGlass(Cabin.Z[j])) continue;
                var s = Cabin.S[j];
                for (int i = CarShapes.CSeal; i <= CarShapes.CGlassTop; i++) s[i].x -= GlassRecess;
            }
        }
    }
}
