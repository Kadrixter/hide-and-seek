using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Dereliction pass of 26.11, run after the cars, the map detail and every chunk are in place (so physics can be
    /// asked what is free): low collider-less wheel stops, the open ends of the ceiling service runs taken into the
    /// walls / slab, broken dripping water pipes (<see cref="BuildBrokenPipes"/>), spalled concrete above every rubble
    /// pile (<see cref="BuildRubbleSpalls"/>) and a few things left lying around: shopping carts, knocked-over cones, a
    /// fallen fluorescent tube, empty bottles, a squatter's corner, cables sagging out of the trays, torn notices on the
    /// columns, leaves blown in at the street exits. Every random choice uses its own System.Random (never _rng), and
    /// nothing with a collider goes into the aisles, near spawns or into occupied space.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        // ------------------------------------------------------------------ wheel stops

        /// <summary>
        /// Height of the yellow wheel stops at the back of the bays (was 12 cm). They have no collider any more: the
        /// player capsule snagged on them when its step offset was cut down under a car's body (crouching).
        /// </summary>
        private const float WheelStopHeight = 0.06f;

        private readonly ChunkedBuilder[] _stops = new ChunkedBuilder[Levels];

        private ChunkedBuilder Stops(int lv) => _stops[lv] ??= new ChunkedBuilder(_lib);

        /// <summary>Wheel stop of one bay: a low yellow concrete kerb, rendered only (see <see cref="WheelStopHeight"/>).</summary>
        private void WheelStop(int lv, Vector3 at, Quaternion rot)
        {
            Stops(lv).Box(new Vector3(at.x, at.y + WheelStopHeight * 0.5f, at.z), new Vector3(1.6f, WheelStopHeight, 0.16f),
                M("Concrete_Yellow"), rot, MeshBuilder.Faces.All & ~MeshBuilder.Faces.NegY);
        }

        // ------------------------------------------------------------------ entry

        /// <summary>The whole dereliction pass (see the class summary). Call after BuildMapDetail.</summary>
        private void BuildDereliction()
        {
            _lib.EnsureDerelictionMaterials();
            Physics.SyncTransforms();
            var drips = new List<CeilingDrips.Spot>();
            var dripGo = new GameObject("CeilingDrips");
            dripGo.transform.SetParent(_root, false);
            int carts = 0, spalls = 0;
            for (int lv = 0; lv < Levels; lv++)
            {
                string t = Tag(lv);
                var ceil = new ChunkedBuilder(_lib);   // pipes, rebar, cables: rendered, shadows, no collider
                var stains = new ChunkedBuilder(_lib); // stains / cracks / notices / floor decals: no shadows
                var loose = new ChunkedBuilder(_lib);  // small things on the floor: shadows, no collider
                var rnd = new System.Random(9241 + lv * 613);

                CapServiceRuns(lv, ceil);
                BuildBrokenPipes(lv, ceil, stains, loose, drips, dripGo.transform);
                spalls += BuildRubbleSpalls(lv, ceil, stains);
                HangingCables(lv, ceil, rnd);
                TornNotices(lv, stains, rnd);
                carts += FloorClutter(lv, loose, stains, rnd);

                if (_stops[lv] != null) EmitChunks(_stops[lv], "WheelStops_" + t, _tPropsLv[lv], false, false);
                // "CeilingServices_Lx" prefix: hidden with the other ceiling services in the overview renders
                EmitChunks(ceil, "CeilingServices_" + t + "_Decay", _tPropsLv[lv], false, false);
                EmitChunks(loose, "FloorClutter_" + t, _tPropsLv[lv], false, false);
                EmitChunks(stains, "DecayDecals_" + t, Group(_tDecals, "Level-" + (lv + 1)), false, false, ShadowCastingMode.Off);
            }

            var cd = dripGo.AddComponent<CeilingDrips>();
            cd.Spots = drips.ToArray();
            cd.DropMesh = DropMesh();
            cd.RingMesh = RingMesh();
            cd.DropMaterial = M("Drip_Water");
            cd.RingMaterial = M("Drip_Ring");
            Debug.Log($"[ParkingGarage] Dereliction: {drips.Count} dripping pipes ({string.Join("/", Enumerable.Range(0, Levels).Select(l => drips.Count(d => d.Level == l)))} per level), {spalls} spalled patches over rubble, {carts} shopping carts, wheel stops {WheelStopHeight * 100f:0} cm without collider");
        }

        // ------------------------------------------------------------------ service runs

        /// <summary>
        /// The ceiling service runs of BuildCeilingServices stopped short of the walls (sprinkler lines, cable trays and
        /// ducts 0.5-1 m before the side walls, the red mains and drain pipes in mid-air at their north ends, the tunnel
        /// and corridor bundles a few cm off the end walls). Extends each into the wall with a flange, or turns it up into
        /// the slab with an elbow and a sleeve.
        /// </summary>
        private void CapServiceRuns(int lv, ChunkedBuilder b)
        {
            float c = CeilY(lv), y0 = FloorY(lv);
            bool old = lv == 2;
            var dark = M("Metal_Dark");
            const float wallX = HX + 0.03f;
            foreach (float sx in new[] { -1f, 1f })
            {
                // sprinkler lines (x +-89, c - 0.15)
                float sprY = c - 0.15f;
                foreach (float z in SprinklerZ)
                {
                    b.Cylinder(new Vector3(sx * 88.95f, sprY, z), new Vector3(sx * wallX, sprY, z), 0.035f, 8, M(old ? "Rust" : "PM_Red"), false);
                    b.Cylinder(new Vector3(sx * (HX - 0.03f), sprY, z), new Vector3(sx * wallX, sprY, z), 0.07f, 10, dark);
                }
                // cable trays (x +-89, c - 0.3)
                foreach (float z in new[] { -39.6f, 39.6f })
                {
                    float yt = c - 0.3f;
                    b.Box(new Vector3(sx * 89.515f, yt, z), new Vector3(1.03f, 0.02f, 0.45f), M("Metal_Galvanized"));
                    b.Box(new Vector3(sx * 89.515f, yt + 0.04f, z - 0.225f), new Vector3(1.03f, 0.08f, 0.015f), M("Metal_Galvanized"));
                    b.Box(new Vector3(sx * 89.515f, yt + 0.04f, z + 0.225f), new Vector3(1.03f, 0.08f, 0.015f), M("Metal_Galvanized"));
                    for (int k = 0; k < 4; k++)
                        b.Cylinder(new Vector3(sx * 88.95f, yt + 0.035f, z - 0.15f + k * 0.1f), new Vector3(sx * wallX, yt + 0.035f, z - 0.15f + k * 0.1f), 0.025f, 6, M(k == 2 ? "PM_Orange" : "Plastic_Black"), false);
                    b.Box(new Vector3(sx * (HX - 0.02f), yt + 0.03f, z), new Vector3(0.04f, 0.16f, 0.56f), dark);
                }
                // ventilation ducts (x +-89.5): the last half metre and a collar on the wall
                foreach (float z in new[] { -24f, 24f })
                {
                    const float h = 0.45f, w = 0.9f;
                    float y = c - 0.06f - h * 0.5f;
                    b.Box(new Vector3(sx * 89.765f, y, z), new Vector3(0.53f, h, w), M("Metal_Galvanized"));
                    b.Box(new Vector3(sx * (HX - 0.03f), y, z), new Vector3(0.06f, h + 0.1f, w + 0.1f), M("Metal_Galvanized"));
                }
            }
            // red mains (x +-78, c - 0.6) and the big drain pipes (x +-72.6, c - 0.75): into the tunnel wall at the
            // south end, up into the slab at the north end
            foreach (var (x, y, r, mat) in new[] { (78f, c - 0.6f, 0.07f, "PM_Red"), (-78f, c - 0.6f, 0.07f, "PM_Red"), (72.6f, c - 0.75f, 0.1f, "PM_Grey"), (-72.6f, c - 0.75f, 0.1f, "PM_Grey") })
            {
                float zs = mat == "PM_Red" ? -55.5f : -55.8f;
                b.Cylinder(new Vector3(x, y, zs + 0.05f), new Vector3(x, y, TunnelWall - 0.03f), r, 12, M(mat), false);
                b.Cylinder(new Vector3(x, y, TunnelWall + 0.03f), new Vector3(x, y, TunnelWall - 0.01f), r * 1.9f, 12, dark);
                SlabRiser(b, new Vector3(x, y, 47.5f), c, r, M(mat));
            }
            // tunnel bundle (x +-85.8) and the corridor pipes (x 12.2 .. 43.8): the last few cm into the end walls
            foreach (var (yy, mat, r) in new[] { (2.0f, "PM_Green", 0.12f), (2.35f, "PM_Blue", 0.08f), (2.62f, "PM_Grey", 0.09f), (0.5f, "Metal_Dark", 0.06f) })
                foreach (float sx in new[] { -1f, 1f })
                {
                    var m = M(old && mat != "Metal_Dark" ? "Rust" : mat);
                    var a = new Vector3(sx * 85.75f, y0 + yy, -HZ + 0.25f);
                    var e = new Vector3(sx * 86.03f, y0 + yy, -HZ + 0.25f);
                    b.Cylinder(a, e, r, 10, m, false);
                    b.Cylinder(new Vector3(sx * 85.97f, y0 + yy, -HZ + 0.25f), new Vector3(sx * 86.01f, y0 + yy, -HZ + 0.25f), r * 1.7f, 10, dark);
                }
            foreach (var (yy, mat, r) in new[] { (2.2f, "PM_Green", 0.09f), (2.45f, "PM_Blue", 0.06f), (2.65f, "PM_Grey", 0.07f) })
                foreach (var (from, to) in new[] { (12.25f, 11.97f), (43.75f, 44.03f) })
                {
                    var m = M(old ? "Rust" : mat);
                    b.Cylinder(new Vector3(from, y0 + yy, HZ - 0.2f), new Vector3(to, y0 + yy, HZ - 0.2f), r, 10, m, false);
                    float f = to < 20f ? 12.03f : 43.97f;
                    b.Cylinder(new Vector3(f, y0 + yy, HZ - 0.2f), new Vector3(f + (to < 20f ? -0.04f : 0.04f), y0 + yy, HZ - 0.2f), r * 1.7f, 10, dark);
                }
        }

        /// <summary>
        /// A horizontal pipe ending at <paramref name="p"/> turns up into the slab: elbow fitting, riser, steel sleeve
        /// and flange on the ceiling.
        /// </summary>
        private void SlabRiser(ChunkedBuilder b, Vector3 p, float ceilY, float r, Material m)
        {
            b.Cylinder(new Vector3(p.x, p.y - r * 1.05f, p.z), new Vector3(p.x, p.y + r * 1.6f, p.z), r * 1.18f, 12, m);
            b.Cylinder(new Vector3(p.x, p.y + r * 1.6f, p.z), new Vector3(p.x, ceilY, p.z), r, 12, m, false);
            b.Cylinder(new Vector3(p.x, ceilY - 0.12f, p.z), new Vector3(p.x, ceilY, p.z), r * 1.35f, 12, M("Metal_Dark"), false);
            b.Cylinder(new Vector3(p.x, ceilY - 0.015f, p.z), new Vector3(p.x, ceilY, p.z), r * 2.2f, 12, M("Metal_Dark"));
        }

        // ------------------------------------------------------------------ cables

        /// <summary>Two cables per level slipped out of the cable trays and sagging in a loop (lowest ~1.95 m above the floor).</summary>
        private void HangingCables(int lv, ChunkedBuilder b, System.Random rnd)
        {
            float c = CeilY(lv);
            float yt = c - 0.3f + 0.035f;
            for (int i = 0; i < 2; i++)
            {
                float sz = i == 0 ? -1f : 1f;
                float z = sz * 39.6f;
                float x0 = -80f + (float)rnd.NextDouble() * 155f;
                float len = 2.6f + (float)rnd.NextDouble() * 1.6f;
                float drop = 0.65f + (float)rnd.NextDouble() * 0.2f;
                float lane = -0.15f + 0.1f * rnd.Next(4);
                float outward = (rnd.NextDouble() < 0.5 ? -1f : 1f);
                var mat = M(rnd.NextDouble() < 0.3 ? "PM_Orange" : "Plastic_Black");
                const int seg = 14;
                Vector3 prev = default;
                for (int k = 0; k <= seg; k++)
                {
                    float tt = k / (float)seg;
                    float s = Mathf.Sin(Mathf.PI * tt);
                    // out over the tray edge first, then down in a sagging loop
                    var p = new Vector3(x0 + len * tt, yt - drop * Mathf.Pow(s, 1.4f) + 0.05f * s, z + lane + outward * (0.4f - Mathf.Abs(lane)) * Mathf.Min(1f, s * 3f));
                    if (k > 0) b.At(p).Cylinder(prev, p, 0.012f, 6, mat, false);
                    prev = p;
                }
            }
        }

        // ------------------------------------------------------------------ notices

        private static readonly string[][] NoticeTexts =
        {
            new[] { "UWAGA", "PARKING", "NIECZYNNY" },
            new[] { "UWAGA", "GROZI", "ZAWALENIEM" },
            new[] { "SPRZEDAM", "OPONY", "TANIO" },
            new[] { "REMONT", "DO ODWOLANIA" },
            new[] { "ZGUBIONO", "KOTA", "NAGRODA" },
        };

        /// <summary>
        /// Three yellowed paper notices per level taped on columns (the face towards the nearest aisle, between the
        /// level band and the column sign), each with a corner torn off. Polish text without diacritics (pixel font).
        /// </summary>
        private void TornNotices(int lv, ChunkedBuilder d, System.Random rnd)
        {
            float y0 = FloorY(lv);
            var paper = M("Paper_Old");
            var ink = M("Text_Black");
            var used = new List<Vector3>();
            int placed = 0;
            for (int tries = 0; tries < 60 && placed < 3; tries++)
            {
                int i = rnd.Next(ColX.Length), j = rnd.Next(ColZ.Length);
                float x = ColX[i], z = ColZ[j];
                if (ColumnBlocked(x, z) || (i + j + lv) % 3 == 0) continue; // no column / extinguisher box on it
                if (used.Any(u => Mathf.Abs(u.x - x) < 20f && Mathf.Abs(u.z - z) < 20f)) continue;
                float nearest = AisleZ.OrderBy(a => Mathf.Abs(a - z)).First();
                var n = nearest > z ? Vector3.forward : Vector3.back;
                var center = new Vector3(x, y0 + 1.35f, z) + n * 0.302f;
                float rot = ((float)rnd.NextDouble() - 0.5f) * 12f;
                var up = Quaternion.AngleAxis(rot, n) * Vector3.up;
                var right = Vector3.Cross(n, up).normalized;
                const float hw = 0.13f, hh = 0.18f;
                int torn = rnd.Next(4);
                // outline: rectangle with one corner replaced by a ragged cut
                var pts = new List<Vector2>();
                var corners = new[] { new Vector2(-hw, -hh), new Vector2(hw, -hh), new Vector2(hw, hh), new Vector2(-hw, hh) };
                for (int k = 0; k < 4; k++)
                {
                    if (k != torn) { pts.Add(corners[k]); continue; }
                    Vector2 prevC = corners[(k + 3) % 4], nextC = corners[(k + 1) % 4], cc = corners[k];
                    float a = 0.35f + 0.3f * (float)rnd.NextDouble(), bb = 0.3f + 0.35f * (float)rnd.NextDouble();
                    Vector2 p0 = Vector2.Lerp(cc, prevC, a * 0.6f), p1 = Vector2.Lerp(cc, nextC, bb * 0.6f);
                    pts.Add(p0);
                    pts.Add(Vector2.Lerp(p0, p1, 0.5f) + (Vector2.zero - cc).normalized * 0.012f);
                    pts.Add(p1);
                }
                Vector3 W(Vector2 v, float off) => center + right * v.x + up * v.y + n * off;
                var mb = d.At(center);
                for (int k = 0; k < pts.Count; k++)
                    mb.Tri(W(Vector2.zero, 0f), W(pts[k], 0f), W(pts[(k + 1) % pts.Count], 0f), n, n, n, paper);
                // strips of tape at the top corners
                foreach (float sx in new[] { -1f, 1f })
                    d.FaceUV(W(new Vector2(sx * (hw - 0.02f), hh - 0.01f), 0.0008f), right * 0.025f, up * 0.012f, M("Sign_White"), Vector2.zero, Vector2.one);
                var lines = NoticeTexts[rnd.Next(NoticeTexts.Length)];
                for (int l = 0; l < lines.Length; l++)
                {
                    float h = Mathf.Min(l == 0 ? 0.045f : 0.032f, 0.22f / (lines[l].Length * PixelFont.Aspect));
                    d.Text(lines[l], W(new Vector2(0f, hh - 0.06f - l * 0.06f), 0.0012f), n, up, h, ink);
                }
                used.Add(new Vector3(x, 0f, z));
                placed++;
            }
        }

        // ------------------------------------------------------------------ floor clutter

        /// <summary>
        /// Things left lying in empty bays (checked free with physics, away from spawns): a shopping cart (tipped over on
        /// P -2), two knocked-over cones, a few empty bottles and cans, a squatter's corner on P -1; a fallen fluorescent
        /// tube under one of the aisle lamps; dry leaves blown in at the street exits on P 0. Only the cart has a
        /// collider. Returns the number of carts placed.
        /// </summary>
        private int FloorClutter(int lv, ChunkedBuilder loose, ChunkedBuilder decals, System.Random rnd)
        {
            float y0 = FloorY(lv);
            var empty = _bays.Where(b => b.Level == lv && !b.NoCars && b.Label != "WASH" && !HeroReserved(lv, b.Label) &&
                                         !_parked.Any(p => p.Level == lv && p.Label == b.Label)).ToList();
            for (int i = empty.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (empty[i], empty[j]) = (empty[j], empty[i]);
            }
            int next = 0;
            var usedSpots = new List<Vector3>();

            // next free spot at the back of an empty bay (box of half size `half` must be empty, 18 m from the others)
            bool Take(Vector3 half, out Vector3 pos, out Bay bay)
            {
                while (next < empty.Count)
                {
                    bay = empty[next++];
                    pos = bay.Center + bay.Inward * (bay.Depth * 0.5f - 1.4f);
                    pos.y = y0;
                    if (NearSpawn(pos + Vector3.up * 0.05f, 4f)) continue;
                    var at = pos;
                    if (usedSpots.Any(u => (u - at).sqrMagnitude < 18f * 18f)) continue;
                    var hits = Physics.OverlapBox(pos + Vector3.up * (0.12f + half.y), half, Quaternion.LookRotation(bay.Inward), ~0, QueryTriggerInteraction.Ignore);
                    if (hits.Length > 0) continue;
                    usedSpots.Add(pos);
                    return true;
                }
                pos = default;
                bay = default;
                return false;
            }
            float Rn(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            int carts = 0;
            // shopping cart: the box covers the upright and the tipped footprint
            if (Take(new Vector3(0.75f, 0.5f, 0.75f), out var cp, out var cb))
            {
                float yaw = Quaternion.LookRotation(cb.Inward).eulerAngles.y + Rn(-35f, 35f);
                bool tipped = lv == 2;
                var go = PlaceProp(ShoppingCart(), $"ShoppingCart_{Tag(lv)}", _tPropsLv[lv], cp, yaw);
                if (tipped)
                {
                    var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 90f);
                    go.transform.SetPositionAndRotation(cp + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.5f, 0.3f, 0f), rot);
                }
                carts++;
            }
            // two knocked-over cones (no collider: a player kicks through them rather than snagging)
            for (int k = 0; k < 2; k++)
            {
                if (!Take(new Vector3(0.45f, 0.25f, 0.45f), out var p, out _)) break;
                float yaw = Rn(0f, 360f);
                var go = PlaceProp(_props.Cone(), $"FallenCone_{Tag(lv)}_{k + 1}", _tPropsLv[lv], p, yaw, null, false);
                // on its side, resting on the base edge and the tip
                go.transform.SetPositionAndRotation(p + Vector3.up * 0.195f, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 104f));
            }
            // empty bottles and cans
            if (Take(new Vector3(0.6f, 0.2f, 0.6f), out var bp, out _))
                Bottles(loose, bp, rnd, 3 + rnd.Next(3));
            // squatter's corner: flattened boxes, an old blanket, bottles
            if (lv == 1 && Take(new Vector3(0.8f, 0.3f, 1.1f), out var sp, out var sb))
            {
                var fwd = sb.Inward;
                var side = Vector3.Cross(Vector3.up, fwd);
                var q = Quaternion.LookRotation(fwd);
                var mb = loose.At(sp);
                mb.Box(sp + Vector3.up * 0.006f, new Vector3(0.95f, 0.012f, 1.9f), M("Cardboard"), q * Quaternion.Euler(0f, 4f, 0f));
                mb.Box(sp + side * 0.35f + Vector3.up * 0.016f, new Vector3(0.7f, 0.012f, 1.0f), M("Cardboard"), q * Quaternion.Euler(0f, -11f, 0f));
                mb.Box(sp + Vector3.up * 0.04f + fwd * 0.2f, new Vector3(0.85f, 0.05f, 1.3f), M("Blanket_Old"), q * Quaternion.Euler(1.5f, -6f, 2f));
                mb.Box(sp + Vector3.up * 0.09f + fwd * 0.55f + side * 0.15f, new Vector3(0.5f, 0.09f, 0.35f), M("Blanket_Old"), q * Quaternion.Euler(4f, 20f, -6f));
                Bottles(loose, sp - fwd * 1.15f + side * 0.3f, rnd, 4);
            }
            // fluorescent tube that fell from an aisle lamp
            for (int tries = 0; tries < 20; tries++)
            {
                float x = -71.25f + 7.5f * rnd.Next(20);
                float z = AisleZ[rnd.Next(AisleZ.Length)] + Rn(-0.7f, 0.7f);
                var p = new Vector3(x + Rn(-0.5f, 0.5f), y0, z);
                if (NearSpawn(p + Vector3.up * 0.05f, 3f)) continue;
                if (Physics.OverlapBox(p + Vector3.up * 0.3f, new Vector3(0.8f, 0.2f, 0.4f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Length > 0) continue;
                BrokenTube(loose, decals, p, rnd);
                break;
            }
            // dry leaves blown in at the street exits (P 0)
            if (lv == 0)
                foreach (float sx in new[] { -1f, 1f })
                    for (int k = 0; k < 14; k++)
                    {
                        float dx = Rn(0.3f, 7f);
                        var c = new Vector3(sx * (HX - dx), y0 + DecalY.Mark + 0.0045f + k * 0.00002f, Rn(-3.2f, 3.2f) * (1f - dx / 12f));
                        float s = Rn(0.5f, 1.1f);
                        decals.FloorDecal(c, new Vector3(Rn(-1f, 1f), 0f, Rn(-1f, 1f)) + Vector3.forward * 0.01f, s, s, M("Litter"));
                    }
            return carts;
        }

        /// <summary>A few empty bottles and cans around <paramref name="p"/>, most of them lying on their side.</summary>
        private void Bottles(ChunkedBuilder b, Vector3 p, System.Random rnd, int count)
        {
            var mb = b.At(p);
            for (int i = 0; i < count; i++)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2.0);
                var at = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.1f + 0.35f * (float)rnd.NextDouble());
                bool can = rnd.NextDouble() < 0.4;
                bool lying = rnd.NextDouble() < 0.75;
                float b2 = (float)(rnd.NextDouble() * Mathf.PI * 2.0);
                var dir = lying ? new Vector3(Mathf.Cos(b2), 0f, Mathf.Sin(b2)) : Vector3.up;
                if (can)
                {
                    float r = 0.033f;
                    var c0 = at + (lying ? Vector3.up * r : Vector3.zero);
                    mb.Cylinder(c0, c0 + dir * 0.12f, r, 10, M(rnd.NextDouble() < 0.5 ? "PM_Red" : "Metal_Steel"));
                }
                else
                {
                    float r = 0.034f;
                    var glass = M(rnd.NextDouble() < 0.5 ? "Bottle_Green" : "Bottle_Brown");
                    var c0 = at + (lying ? Vector3.up * r : Vector3.zero);
                    mb.Cylinder(c0, c0 + dir * 0.2f, r, 10, glass);
                    mb.Cylinder(c0 + dir * 0.2f, c0 + dir * 0.27f, r, 10, glass, false, 0.012f);
                    mb.Cylinder(c0 + dir * 0.27f, c0 + dir * 0.31f, 0.012f, 8, glass);
                }
            }
        }

        /// <summary>A 1.2 m fluorescent tube broken in two on the floor, glass crumbs around the break.</summary>
        private void BrokenTube(ChunkedBuilder b, ChunkedBuilder decals, Vector3 p, System.Random rnd)
        {
            float a = (float)(rnd.NextDouble() * 360.0);
            var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            var side = Vector3.Cross(Vector3.up, dir);
            const float r = 0.013f;
            var tube = M("PM_White");
            var cap = M("Metal_Steel");
            var mb = b.At(p);
            var c = p + Vector3.up * r;
            // long piece and the short piece, kinked apart at the break
            var a0 = c - dir * 0.6f;
            var a1 = c + dir * 0.12f;
            var b0 = c + dir * 0.2f + side * 0.06f;
            var b1 = b0 + (Quaternion.Euler(0f, 25f + (float)rnd.NextDouble() * 30f, 0f) * dir) * 0.35f;
            mb.Cylinder(a0, a1, r, 8, tube);
            mb.Cylinder(b0, b1, r, 8, tube);
            mb.Cylinder(a0 - dir * 0.03f, a0, r * 1.15f, 8, cap);
            mb.Cylinder(b1, b1 + (b1 - b0).normalized * 0.03f, r * 1.15f, 8, cap);
            // two pins on each end cap
            foreach (var (e, d) in new[] { (a0 - dir * 0.03f, -dir), (b1 + (b1 - b0).normalized * 0.03f, (b1 - b0).normalized) })
                foreach (float s in new[] { -0.005f, 0.005f })
                {
                    var q = e + Vector3.Cross(Vector3.up, d) * s;
                    mb.Cylinder(q, q + d * 0.012f, 0.0015f, 4, cap, false);
                }
            decals.FloorDecal(new Vector3(c.x, p.y + DecalY.Mark + 0.004f, c.z) + dir * 0.16f,
                new Vector3(dir.z, 0f, -dir.x) + Vector3.forward * 0.01f, 0.55f, 0.45f, M("Glass_Shards"));
        }

        /// <summary>Old wire shopping cart, length along Z (handle at -Z), pivot on the floor; one box collider.</summary>
        private PropMesh ShoppingCart() => _props.Get("ShoppingCart", (mb, pm) =>
        {
            var steel = M("Metal_Steel");
            var mesh = M("Wire_Mesh");
            var black = M("Plastic_Black");
            // castors
            foreach (float x in new[] { -0.2f, 0.2f })
                foreach (float z in new[] { -0.32f, 0.36f })
                {
                    mb.Cylinder(new Vector3(x - 0.015f, 0.05f, z), new Vector3(x + 0.015f, 0.05f, z), 0.05f, 10, black);
                    mb.Box(new Vector3(x, 0.105f, z), new Vector3(0.05f, 0.03f, 0.06f), steel);
                }
            // chassis rails and legs
            foreach (float x in new[] { -0.2f, 0.2f })
            {
                mb.Cylinder(new Vector3(x, 0.13f, -0.4f), new Vector3(x, 0.13f, 0.42f), 0.012f, 6, steel);
                mb.Cylinder(new Vector3(x, 0.13f, -0.4f), new Vector3(x * 1.3f, 0.46f, -0.42f), 0.012f, 6, steel, false);
                mb.Cylinder(new Vector3(x, 0.13f, 0.4f), new Vector3(x, 0.5f, 0.4f), 0.012f, 6, steel, false);
            }
            mb.Cylinder(new Vector3(-0.2f, 0.13f, 0.42f), new Vector3(0.2f, 0.13f, 0.42f), 0.012f, 6, steel);
            // basket: wider and lower at the back
            Vector3 bbl = new Vector3(-0.26f, 0.46f, -0.42f), bbr = new Vector3(0.26f, 0.46f, -0.42f);
            Vector3 btl = new Vector3(-0.28f, 0.96f, -0.44f), btr = new Vector3(0.28f, 0.96f, -0.44f);
            Vector3 fbl = new Vector3(-0.2f, 0.5f, 0.42f), fbr = new Vector3(0.2f, 0.5f, 0.42f);
            Vector3 ftl = new Vector3(-0.24f, 0.96f, 0.46f), ftr = new Vector3(0.24f, 0.96f, 0.46f);
            void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var n = Vector3.Cross(b - a, d - a).normalized;
                mb.Quad(a, b, c, d, n, n, n, n, mesh);
                mb.Quad(a, b, c, d, -n, -n, -n, -n, mesh);
            }
            Panel(bbl, bbr, fbr, fbl);  // bottom
            Panel(bbl, fbl, ftl, btl);  // left
            Panel(bbr, fbr, ftr, btr);  // right
            Panel(fbl, fbr, ftr, ftl);  // front
            Panel(bbl, bbr, btr, btl);  // back (the flap)
            foreach (var (a, b) in new[] { (btl, btr), (btr, ftr), (ftr, ftl), (ftl, btl), (bbl, fbl), (bbr, fbr), (fbl, fbr), (bbl, bbr), (bbl, btl), (bbr, btr), (fbl, ftl), (fbr, ftr) })
                mb.Cylinder(a, b, 0.008f, 5, steel, false);
            // handle with its red plastic grip
            mb.Cylinder(btl, new Vector3(-0.28f, 1.0f, -0.55f), 0.01f, 6, steel, false);
            mb.Cylinder(btr, new Vector3(0.28f, 1.0f, -0.55f), 0.01f, 6, steel, false);
            mb.Cylinder(new Vector3(-0.29f, 1.0f, -0.55f), new Vector3(0.29f, 1.0f, -0.55f), 0.016f, 8, M("PM_Red"));
            pm.Colliders.Add(new Bounds(new Vector3(0f, 0.52f, -0.04f), new Vector3(0.6f, 1.0f, 1.04f)));
        });
    }
}
