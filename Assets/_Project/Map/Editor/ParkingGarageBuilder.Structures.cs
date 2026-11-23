using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Ramps, stairwells, the service tunnel, lobbies and north service rooms.</summary>
    internal sealed partial class ParkingGarageBuilder
    {
        // ================================================================== ramps

        /// <summary>
        /// A ramp shaft along X. Ramp segments for every pair of levels are stacked in the same footprint
        /// (constant 3 m headroom between them), like a scissor ramp.
        /// </summary>
        private void BuildRamp(RampDef r)
        {
            float w = r.Z1 - r.Z0, zc = (r.Z0 + r.Z1) * 0.5f;
            Vector3 down = r.XBot < r.XTop ? Vector3.left : Vector3.right;
            for (int k = 0; k < Levels - 1; k++)
            {
                var rb = _ramps[k];
                var top = new Vector3(r.XTop, FloorY(k), zc);
                var bot = new Vector3(r.XBot, FloorY(k + 1), zc);
                Vector3 dir = bot - top;
                float len = dir.magnitude;
                var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                Vector3 up = rot * Vector3.up, fwd = rot * Vector3.forward, right = rot * Vector3.right;
                Vector3 mid = (top + bot) * 0.5f;
                rb.Box(mid - up * 0.2f, new Vector3(w, 0.4f, len + 0.04f), M(k == 1 ? "Floor_Concrete_Old" : "Ramp_Concrete"), rot);
                foreach (float s in new[] { -1f, 1f })
                    rb.Box(mid + up * 0.075f + right * (s * (w * 0.5f - 0.15f)), new Vector3(0.3f, 0.15f, len), M("Concrete_Yellow"), rot);
                for (float t = 1.5f; t < len - 1f; t += 3f)
                    rb.FaceUV(top + fwd * t + up * 0.008f, right * 0.06f, fwd * 0.75f, M("Paint_Yellow"), Vector2.zero, Vector2.one);
                foreach (float s in new[] { -1f, 1f })
                foreach (float t in new[] { len * 0.3f, len * 0.7f })
                {
                    Vector3 arrowDir = s > 0 ? fwd : -fwd;
                    Vector3 c = top + fwd * t + right * (s * w * 0.25f) + up * 0.01f;
                    Vector3 ar = Vector3.Cross(up, arrowDir).normalized;
                    rb.FaceUV(c, ar * 0.6f, arrowDir * 1.2f, M("Arrow_White"), Vector2.zero, Vector2.one);
                }
                // rails on both side walls
                foreach (float zr in new[] { r.Z0 + 0.06f, r.Z1 - 0.06f })
                    rb.Cylinder(new Vector3(r.XTop, FloorY(k) + 0.9f, zr), new Vector3(r.XBot, FloorY(k + 1) + 0.9f, zr), 0.04f, 8, M("PM_Yellow"));

                // floor markings at both transitions
                var dTop = _decals[k];
                dTop.Face(new Vector3(r.XTop - down.x * 0.4f, FloorY(k) + 0.01f, zc), new Vector3(0.3f, 0, 0), new Vector3(0, 0, w * 0.5f - 0.3f), HazardMat(k));
                dTop.Text(LevelName(k + 1), new Vector3(r.XTop - down.x * 3.2f, FloorY(k) + DecalY.Mark, zc), Vector3.up, down, 1.0f, M("Text_Floor"));
                dTop.FloorDecal(new Vector3(r.XTop - down.x * 5.5f, FloorY(k) + DecalY.Mark, zc), down, 1.0f, 2.2f, M("Arrow_White"));
                dTop.Text("SLOW", new Vector3(r.XTop - down.x * 7.6f, FloorY(k) + DecalY.Mark, zc), Vector3.up, down, 0.8f, M("Text_FloorYellow"));
                var dBot = _decals[k + 1];
                dBot.Face(new Vector3(r.XBot + down.x * 0.5f, FloorY(k + 1) + 0.01f, zc), new Vector3(0.25f, 0, 0), new Vector3(0, 0, w * 0.5f - 0.3f), M("Grate"));
                dBot.Text(LevelName(k), new Vector3(r.XBot + down.x * 3.2f, FloorY(k + 1) + DecalY.Mark, zc), Vector3.up, -down, 1.0f, M("Text_Floor"));
                dBot.FloorDecal(new Vector3(r.XBot + down.x * 5.5f, FloorY(k + 1) + DecalY.Mark, zc), -down, 1.0f, 2.2f, M("Arrow_White"));
            }

            // side walls on every level; L1 closes the void at the low end, the lowest level under the high end
            for (int lv = 0; lv < Levels; lv++)
            {
                LevelWall(lv, r.XMin, r.XMax, r.Z0 - 0.25f, r.Z0);
                LevelWall(lv, r.XMin, r.XMax, r.Z1, r.Z1 + 0.25f);
                foreach (var (z, n) in new[] { (r.Z0 - 0.25f, Vector3.back), (r.Z1 + 0.25f, Vector3.forward) })
                {
                    float cx = (r.XMin + r.XMax) * 0.5f;
                    WallQuad(_walls[lv], new Vector3(cx, FloorY(lv) + 0.5f, z) + n * 0.01f, n, (r.XMax - r.XMin) * 0.5f, 0.5f, HazardMat(lv));
                    _walls[lv].Text("RAMP " + (lv < Levels - 1 ? LevelName(lv + 1) : ""), new Vector3(cx, FloorY(lv) + 1.8f, z) + n * 0.015f, n, Vector3.up, 0.6f, M("Text_White"));
                }
            }
            float bx0 = down.x < 0 ? r.XBot - 0.25f : r.XBot, bx1 = down.x < 0 ? r.XBot : r.XBot + 0.25f;
            LevelWall(0, bx0, bx1, r.Z0 - 0.25f, r.Z1 + 0.25f);
            float tx0 = down.x < 0 ? r.XTop : r.XTop - 0.25f, tx1 = down.x < 0 ? r.XTop + 0.25f : r.XTop;
            LevelWall(Levels - 1, tx0, tx1, r.Z0 - 0.25f, r.Z1 + 0.25f);
        }

        // ================================================================== stairs

        /// <summary>
        /// Stairwell with straight flights stacked in one footprint. Door on the south face. On each level the
        /// flight down starts at the landing (west half), the corridor back from the bottom runs in the east half.
        /// </summary>
        private void BuildStair(StairDef s)
        {
            float x0 = s.X0, zf = s.Zf, zi = zf + 0.2f, zA = s.ZA, zB = s.ZB, zH = zB - Run;
            float zIn = Mathf.Min(s.ZIn, HZ);
            for (int lv = 0; lv < Levels; lv++)
            {
                float y0 = FloorY(lv);
                float door = lv == 0 ? x0 + 1f : x0 + 3f;
                LevelWall(lv, x0 - 0.2f, x0 + StairWidth + 0.2f, zf, zi, new Opening(door - 0.6f, door + 0.6f, 2.1f));
                LevelWall(lv, x0 - 0.2f, x0, zi, zIn);
                LevelWall(lv, x0 + StairWidth, x0 + StairWidth + 0.2f, zi, zIn);
                if (zIn < HZ - 0.01f) LevelWall(lv, x0 - 0.2f, x0 + StairWidth + 0.2f, zIn, zIn + 0.2f);

                float signX = lv == 0 ? door + 1.3f : door - 1.3f;
                SignPlate(_signs[lv], new Vector3(signX, y0 + 1.6f, zf), Vector3.back, 0.9f, 0.26f, "STAIRS", 0.16f, M("Sign_Blue"), M("Text_White"));
                _signs[lv].Text(LevelName(lv), new Vector3(x0 + StairWidth - 0.015f, y0 + 1.6f, zi + 0.9f), Vector3.left, Vector3.up, 0.5f, M("Text_White"));
                _decals[lv].FloorQuad(door - 1.2f, door + 1.2f, zf - 1.4f, zf, y0 + DecalY.Hatch, M("Hatch_Yellow"));
                PlaceProp(_props.ExtinguisherBox(), $"ExtinguisherBox_Stair_{x0}_{Tag(lv)}", _tPropsLv[lv],
                    new Vector3(x0 + (lv == 0 ? 3.5f : 0.5f), y0 + 1.0f, zf), 180f, null, false);

                if (lv == 0)
                {
                    Wall(_walls[0], x0 + 2f, x0 + 2.15f, zA, zH + 0.15f, y0, y0 + 1.05f, WallMat(0));
                    Wall(_walls[0], x0, x0 + 2f, zH, zH + 0.15f, y0, y0 + 1.05f, WallMat(0));
                    _walls[0].Cylinder(new Vector3(x0 + 2.075f, y0 + 1.1f, zA), new Vector3(x0 + 2.075f, y0 + 1.1f, zH + 0.15f), 0.035f, 8, M("PM_Yellow"));
                    _walls[0].Cylinder(new Vector3(x0, y0 + 1.1f, zH + 0.075f), new Vector3(x0 + 2f, y0 + 1.1f, zH + 0.075f), 0.035f, 8, M("PM_Yellow"));
                }
                else
                {
                    float wz0 = lv == Levels - 1 ? zi : zA;
                    LevelWall(lv, x0 + 2f, x0 + 2.15f, wz0, zB);
                    _signs[lv].Text(LevelName(lv), new Vector3(x0 + 2.165f, y0 + 1.6f, (zA + zB) * 0.5f), Vector3.right, Vector3.up, 0.5f, M("Text_White"));
                }
                if (lv < Levels - 1)
                    _decals[lv].FloorQuad(x0, x0 + 2f, zA - 0.06f, zA, y0 + DecalY.Paint, M("Paint_Yellow"));
            }

            for (int k = 0; k < Levels - 1; k++)
            {
                var sb = _steps[k];
                bool solid = k + 1 == Levels - 1;
                float yTop = FloorY(k), yBot = FloorY(k + 1);
                for (int i = 0; i < Risers - 1; i++)
                {
                    float top = yTop - (i + 1) * Rise;
                    float za = zA + i * Run, zb = za + Run;
                    float bottom = solid ? yBot : top - 0.35f;
                    var faces = MeshBuilder.Faces.PosY | MeshBuilder.Faces.PosZ | MeshBuilder.Faces.NegZ | (solid ? 0 : MeshBuilder.Faces.NegY);
                    sb.BoxMinMax(new Vector3(x0, bottom, za), new Vector3(x0 + 2f, top, zb), M("Stair_Concrete"), faces);
                    sb.Face(new Vector3(x0 + 1f, top + 0.006f, zb - 0.03f), new Vector3(1f, 0, 0), new Vector3(0, 0, 0.03f), M("Paint_Yellow"));
                }
                if (!solid)
                {
                    // smooth soffit under a stacked flight
                    var a = new Vector3(x0 + 1f, yTop - Rise - 0.35f, zA);
                    var b = new Vector3(x0 + 1f, yBot - 0.35f, zB - Run);
                    var srot = Quaternion.LookRotation((b - a).normalized, Vector3.up);
                    sb.Box((a + b) * 0.5f - srot * Vector3.up * 0.06f, new Vector3(2f, 0.12f, (b - a).magnitude), M("Ceiling_Concrete"), srot);
                }
                float rA = yTop + 0.9f, rB = yBot + 0.9f;
                sb.Cylinder(new Vector3(x0 + 0.07f, rA, zA - 0.3f), new Vector3(x0 + 0.07f, rA, zA), 0.03f, 8, M("PM_Yellow"));
                sb.Cylinder(new Vector3(x0 + 0.07f, rA, zA), new Vector3(x0 + 0.07f, rB, zB), 0.03f, 8, M("PM_Yellow"));
                sb.Cylinder(new Vector3(x0 + 0.07f, rB, zB), new Vector3(x0 + 0.07f, rB, zB + 0.4f), 0.03f, 8, M("PM_Yellow"));

                // invisible ramp collider through the step mid-points so a CharacterController glides smoothly
                var pTop = new Vector3(x0 + 1f, yTop, zA);
                var pBot = new Vector3(x0 + 1f, yBot, zB - Run);
                Vector3 dir = pBot - pTop;
                var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                var col = new GameObject($"StairRampCollider_{x0}_{Tag(k)}-{Tag(k + 1)}");
                col.transform.SetParent(_tStairs, false);
                col.transform.SetPositionAndRotation((pTop + pBot) * 0.5f - rot * Vector3.up * 0.15f, rot);
                col.AddComponent<BoxCollider>().size = new Vector3(2f, 0.3f, dir.magnitude + 0.1f);
                GameObjectUtility.SetStaticEditorFlags(col, DetailFlags);
            }
        }

        // ================================================================== service tunnel

        private static readonly float[] TunnelDoors = { -72f, -48f, -24f, 0f, 24f, 48f, 72f };

        private void BuildTunnel(int lv)
        {
            float y0 = FloorY(lv);
            var ops = new Opening[TunnelDoors.Length];
            for (int i = 0; i < TunnelDoors.Length; i++) ops[i] = new Opening(TunnelDoors[i] - 0.6f, TunnelDoors[i] + 0.6f, 2.1f);
            LevelWall(lv, -86.2f, 86.2f, TunnelWall, TunnelWall + 0.2f, ops);
            LevelWall(lv, -86.2f, -86f, -HZ, TunnelWall);
            LevelWall(lv, 86f, 86.2f, -HZ, TunnelWall);
            foreach (float x in TunnelDoors)
            {
                SignPlate(_signs[lv], new Vector3(x + 1.9f, y0 + 1.7f, TunnelWall + 0.2f), Vector3.forward, 2.2f, 0.3f, "SERVICE TUNNEL", 0.15f, M("Sign_Yellow"), M("Text_Black"));
                SignPlate(_signs[lv], new Vector3(x, y0 + 2.45f, TunnelWall), Vector3.back, 0.6f, 0.22f, "T" + (System.Array.IndexOf(TunnelDoors, x) + 1), 0.14f, M("Sign_White"), M("Text_Black"));
            }
            // tunnel floor markings
            for (float x = -84f; x < 84f; x += 4f)
                _decals[lv].FloorQuad(x, x + 2f, -58.15f, -58.05f, y0 + DecalY.Paint, M("Paint_Yellow"));
        }

        // ================================================================== lobbies

        private void ElevatorDoors(int lv, float ex, float zFace, float s, bool lit, bool functional = false)
        {
            // s = +1 when the face looks towards +z, -1 towards -z
            var b = _walls[lv];
            float y0 = FloorY(lv);
            float z1 = zFace + s * 0.15f, zd0 = zFace + s * 0.03f, zd1 = zFace + s * 0.08f;
            Vector3 Mn(float x, float y, float z) => new Vector3(x, y, z);
            void Bx(float xa, float ya, float za, float xb, float yb, float zb, Material m) =>
                b.BoxMinMax(Mn(xa, ya, Mathf.Min(za, zb)), Mn(xb, yb, Mathf.Max(za, zb)), m);
            Bx(ex - 1.0f, y0, zFace, ex - 0.85f, y0 + 2.35f, z1, M("Metal_Steel"));
            Bx(ex + 0.85f, y0, zFace, ex + 1.0f, y0 + 2.35f, z1, M("Metal_Steel"));
            Bx(ex - 1.0f, y0 + 2.2f, zFace, ex + 1.0f, y0 + 2.45f, z1, M("Metal_Steel"));
            if (!functional)
            {
                // Decorative (closed) doors; working elevators get sliding doors at runtime (Game: ElevatorSystem).
                Bx(ex - 0.85f, y0, zd0, ex - 0.006f, y0 + 2.2f, zd1, M("Metal_Steel"));
                Bx(ex + 0.006f, y0, zd0, ex + 0.85f, y0 + 2.2f, zd1, M("Metal_Steel"));
                Bx(ex - 0.006f, y0, zd0, ex + 0.006f, y0 + 2.2f, zd1, M("Plastic_Black"));
            }
            else
            {
                // Lintel between the door header and the ceiling, flush with the shaft face.
                Bx(ex - 1.05f, y0 + 2.2f, zFace - s * 0.12f, ex + 1.05f, CeilY(lv), zFace, M("Metal_Steel"));
            }
            var n = new Vector3(0, 0, s);
            _signs[lv].Box(new Vector3(ex, y0 + 2.62f, zFace + s * 0.03f), new Vector3(0.5f, 0.2f, 0.06f), M("Sign_Dark"));
            if (lit) _signs[lv].Text(LevelName(lv), new Vector3(ex, y0 + 2.62f, zFace + s * 0.065f), n, Vector3.up, 0.14f, M("Text_GlowRed"));
            _decals[lv].FloorQuad(ex - 0.9f, ex + 0.9f, Mathf.Min(zFace, zFace + s * 0.25f), Mathf.Max(zFace, zFace + s * 0.25f), y0 + DecalY.Stain, M("Metal_Steel"));
            // call buttons next to the doors
            b.Box(new Vector3(ex + 1.25f, y0 + 1.15f, zFace + s * 0.03f), new Vector3(0.14f, 0.3f, 0.06f), M("Metal_Steel"));
            b.Box(new Vector3(ex + 1.25f, y0 + 1.15f, zFace + s * 0.065f), new Vector3(0.05f, 0.05f, 0.02f), M("Plastic_Black"));
        }

        /// <summary>Cabin interior (lining, ceiling with light panel, handrails) + marker for the Game elevator system.</summary>
        private void ElevatorCabin(int lv, int car, float ex)
        {
            float y0 = FloorY(lv);
            var b = _walls[lv];
            const float hw = 1.05f, hd = 1.28f;
            // ceiling of the cabin
            b.BoxMinMax(new Vector3(ex - hw, y0 + 2.55f, -1.4f), new Vector3(ex + hw, CeilY(lv), 1.4f), M("Metal_Steel"));
            // brushed steel side linings
            b.BoxMinMax(new Vector3(ex - hw, y0, -hd), new Vector3(ex - hw + 0.04f, y0 + 2.55f, hd), M("Metal_Steel"));
            b.BoxMinMax(new Vector3(ex + hw - 0.04f, y0, -hd), new Vector3(ex + hw, y0 + 2.55f, hd), M("Metal_Steel"));
            // handrails
            b.BoxMinMax(new Vector3(ex - hw + 0.04f, y0 + 0.92f, -0.9f), new Vector3(ex - hw + 0.09f, y0 + 0.97f, 0.9f), M("Metal_Steel"));
            b.BoxMinMax(new Vector3(ex + hw - 0.09f, y0 + 0.92f, -0.9f), new Vector3(ex + hw - 0.04f, y0 + 0.97f, 0.9f), M("Metal_Steel"));
            // light panel
            _signs[lv].Box(new Vector3(ex, y0 + 2.53f, 0f), new Vector3(1.2f, 0.02f, 1.6f), M("Text_White"));
            // floor plate
            _decals[lv].FloorQuad(ex - hw, ex + hw, -1.4f, 1.4f, y0 + DecalY.Stain, M("Metal_Steel"));
            // marker for the runtime elevator (position = cabin floor centre)
            var marker = new GameObject($"ElevatorCar_{car}_L{lv}");
            marker.transform.SetParent(_tPropsLv[lv], false);
            marker.transform.position = new Vector3(ex, y0, 0f);
        }

        private void BuildLobbyNorth(int lv)
        {
            float y0 = FloorY(lv), zf = NorthFront, zi = zf + 0.2f;
            LevelWall(lv, -6.2f, 6.2f, zf, zi, new Opening(-1.6f, 1.6f, 2.5f));
            LevelWall(lv, -6.2f, -6f, zi, HZ);
            LevelWall(lv, 6f, 6.2f, zi, HZ);
            _decals[lv].FloorQuad(-6f, 6f, zi, HZ, y0 + DecalY.Under, M(lv == 2 ? "Floor_Concrete_Old" : "Tile_Floor"));
            _decals[lv].FloorQuad(-1.6f, 1.6f, zf, zi, y0 + DecalY.Stain, M("Metal_Steel"));
            ElevatorDoors(lv, -2.6f, HZ, -1f, lv != 2);
            ElevatorDoors(lv, 2.6f, HZ, -1f, false);
            SignPlate(_signs[lv], new Vector3(0, y0 + 2.75f, zf), Vector3.back, 1.6f, 0.36f, "LIFT N " + LevelName(lv), 0.18f, M("Sign_Blue"), M("Text_White"));
            SignPlate(_signs[lv], new Vector3(-6f, y0 + 1.8f, 55.5f), Vector3.right, 0.9f, 0.7f, LevelName(lv), 0.45f, M("Sign_Blue"), M("Text_White"));
            _decals[lv].FloorQuad(-2f, 2f, zf - 1.4f, zf, y0 + DecalY.Hatch, M("Hatch_Yellow"));
        }

        private void BuildLobbyCentral(int lv)
        {
            float y0 = FloorY(lv);
            LevelWall(lv, -8f, 8f, -5f, -4.8f, new Opening(-2f, 2f, 2.5f));
            LevelWall(lv, -8f, 8f, 4.8f, 5f, new Opening(-2f, 2f, 2.5f));
            LevelWall(lv, -8f, -7.8f, -4.8f, 4.8f);
            LevelWall(lv, 7.8f, 8f, -4.8f, 4.8f);
            // elevator core: two walk-through cabins (doors on both sides), solid blocks between them.
            LevelWall(lv, -5.5f, -3.65f, -1.4f, 1.4f);
            LevelWall(lv, -1.55f, 1.55f, -1.4f, 1.4f);
            LevelWall(lv, 3.65f, 5.5f, -1.4f, 1.4f);
            for (int car = 0; car < 2; car++)
            {
                float ex = car == 0 ? -2.6f : 2.6f;
                ElevatorDoors(lv, ex, -1.4f, -1f, ex < 0 && lv != 2, true);
                ElevatorDoors(lv, ex, 1.4f, 1f, ex > 0, true);
                ElevatorCabin(lv, car, ex);
            }
            var tile = M(lv == 2 ? "Floor_Concrete_Old" : "Tile_Floor");
            _decals[lv].FloorQuad(-7.8f, 7.8f, -4.8f, -1.4f, y0 + DecalY.Under, tile);
            _decals[lv].FloorQuad(-7.8f, 7.8f, 1.4f, 4.8f, y0 + DecalY.Under, tile);
            _decals[lv].FloorQuad(-7.8f, -5.5f, -1.4f, 1.4f, y0 + DecalY.Under, tile);
            _decals[lv].FloorQuad(5.5f, 7.8f, -1.4f, 1.4f, y0 + DecalY.Under, tile);
            foreach (var (z, n) in new[] { (-5f, Vector3.back), (5f, Vector3.forward) })
            {
                SignPlate(_signs[lv], new Vector3(0, y0 + 2.75f, z), n, 1.6f, 0.36f, "LIFT C " + LevelName(lv), 0.18f, M("Sign_Blue"), M("Text_White"));
                _decals[lv].FloorQuad(-2.4f, 2.4f, Mathf.Min(z, z + n.z * 1.4f), Mathf.Max(z, z + n.z * 1.4f), y0 + DecalY.Hatch, M("Hatch_Yellow"));
            }
            var t = _tPropsLv[lv];
            foreach (var (x, z, yaw) in new[] { (-6.9f, -3f, 90f), (6.9f, 3f, -90f) })
            {
                var p = new Vector3(x, y0, z);
                if (!NearSpawn(p, 1.2f)) PlaceProp(_props.Bench(), $"Bench_LobbyC_{Tag(lv)}_{x}", t, p, yaw);
            }
            PlaceProp(_props.WheelieBin("PM_DarkGrey"), $"Bin_LobbyC_{Tag(lv)}", t, new Vector3(7.1f, y0, -4.1f), 200f);
            PlaceProp(_props.PayMachine(), $"PayMachine_LobbyC_{Tag(lv)}", t, new Vector3(-7.4f, y0, 3.8f), 90f);
        }

        // ================================================================== north rooms

        private void BuildNorthRooms(int lv)
        {
            float y0 = FloorY(lv), zf = NorthFront, zi = zf + 0.2f;
            var t = _tPropsLv[lv];

            // technical / generator / pump room
            LevelWall(lv, -32.2f, -11.8f, zf, zi, new Opening(-22.6f, -21.4f, 2.1f));
            LevelWall(lv, -32.2f, -32f, zi, HZ);
            LevelWall(lv, -12f, -11.8f, zi, HZ);
            PlaceProp(_props.Door("PM_DarkGrey", 1.15f), $"Door_TechRoom_{Tag(lv)}", t, new Vector3(-21.45f, y0, zi + 0.05f), -110f);
            string[] title = { "DANGER", "GENERATORS", "PUMP ROOM" };
            string[] sub = { "HIGH VOLTAGE", "STAFF ONLY", "FLOOD RISK" };
            SignPlate(_signs[lv], new Vector3(-25f, y0 + 1.7f, zf), Vector3.back, 1.7f, 0.42f, title[lv], 0.2f, M("Sign_Yellow"), M("Text_Black"));
            SignPlate(_signs[lv], new Vector3(-25f, y0 + 1.33f, zf), Vector3.back, 1.7f, 0.24f, sub[lv], 0.12f, M("Sign_Yellow"), M("Text_Black"));
            _decals[lv].FloorQuad(-23.2f, -20.8f, zf - 1.4f, zf, y0 + DecalY.Hatch, M("Hatch_Yellow"));

            // storage block: cages (L1, L3 damaged) or a workshop (L2)
            if (lv == 1)
            {
                LevelWall(lv, -56.2f, -33.8f, zf, zi, new Opening(-50.6f, -49.4f, 2.1f), new Opening(-40.6f, -39.4f, 2.1f));
                LevelWall(lv, -56.2f, -56f, zi, HZ);
                LevelWall(lv, -34f, -33.8f, zi, HZ);
                SignPlate(_signs[lv], new Vector3(-45f, y0 + 2.2f, zf), Vector3.back, 1.8f, 0.3f, "WORKSHOP", 0.18f, M("Sign_White"), M("Text_Black"));
            }
            else
            {
                Cages(lv, new[] { -56f, -51.6f, -47.2f, -42.8f, -38.4f, -34f }, 55.5f, HZ, lv == 2);
            }

            // maintenance corridor
            LevelWall(lv, 11.8f, 44.2f, 57.2f, 57.4f, new Opening(16f, 17.2f, 2.1f), new Opening(29f, 30.2f, 2.1f), new Opening(39f, 40.2f, 2.1f));
            LevelWall(lv, 11.8f, 12f, 57.4f, HZ);
            LevelWall(lv, 44f, 44.2f, 57.4f, HZ);
            PlaceProp(_props.Door("PM_Grey", 1.15f), $"Door_Corridor_{Tag(lv)}", t, new Vector3(30.15f, y0, 57.45f), 100f);
            SignPlate(_signs[lv], new Vector3(23f, y0 + 2.3f, 57.2f), Vector3.back, 2.2f, 0.3f, "MAINTENANCE", 0.18f, M("Sign_White"), M("Text_Black"));
        }

        /// <summary>Storage cages: wire mesh fronts with an open door per cage and wire dividers to the back wall.</summary>
        private void Cages(int lv, float[] xs, float zFront, float zBack, bool damaged)
        {
            var b = _cages[lv];
            float y0 = FloorY(lv);
            const float h = 2.4f;
            var wire = M("Wire_Mesh");
            var post = M(damaged ? "Rust" : "Metal_Galvanized");
            for (int i = 0; i < xs.Length - 1; i++)
            {
                float a = xs[i], c = xs[i + 1];
                float doorA = a + (c - a) * 0.6f, doorB = doorA + 1f;
                bool missing = damaged && i % 2 == 1;
                if (!missing) b.BoxMinMax(new Vector3(a, y0 + 0.05f, zFront - 0.01f), new Vector3(doorA, y0 + h, zFront + 0.01f), wire);
                b.BoxMinMax(new Vector3(doorB, y0 + 0.05f, zFront - 0.01f), new Vector3(c, y0 + h, zFront + 0.01f), wire);
                b.BoxMinMax(new Vector3(a, y0 + h - 0.05f, zFront - 0.03f), new Vector3(c, y0 + h, zFront + 0.03f), post);
                b.BoxMinMax(new Vector3(a, y0, zFront - 0.03f), new Vector3(c, y0 + 0.05f, zFront + 0.03f), post);
                foreach (float px in new[] { a, doorA, doorB, c })
                    b.BoxMinMax(new Vector3(px - 0.03f, y0, zFront - 0.03f), new Vector3(px + 0.03f, y0 + h, zFront + 0.03f), post);
                var hinge = new Vector3(doorB, y0 + 0.05f, zFront);
                if (damaged && i % 3 == 0)
                {
                    // door torn off, lying on the floor
                    b.Box(new Vector3(doorA + 0.3f, y0 + 0.03f, zFront - 1.4f), new Vector3(1f, 0.02f, h - 0.1f), wire, Quaternion.Euler(0, 20f, 0));
                }
                else
                {
                    var rot = Quaternion.Euler(0, -70f + i * 15f, 0);
                    b.Box(hinge + rot * new Vector3(-0.5f, (h - 0.1f) * 0.5f, 0), new Vector3(1f, h - 0.1f, 0.02f), wire, rot);
                    b.Box(hinge + rot * new Vector3(-0.5f, h - 0.1f, 0), new Vector3(1f, 0.04f, 0.04f), post, rot);
                }
                SignPlate(_signs[lv], new Vector3((a + doorA) * 0.5f, y0 + 1.9f, zFront - 0.02f), Vector3.back, 0.5f, 0.25f, "S" + (i + 1), 0.16f, M("Sign_Yellow"), M("Text_Black"));
            }
            if (zBack < HZ - 0.1f)
            {
                b.BoxMinMax(new Vector3(xs[0], y0 + 0.05f, zBack - 0.01f), new Vector3(xs[xs.Length - 1], y0 + h, zBack + 0.01f), wire);
                b.BoxMinMax(new Vector3(xs[0], y0 + h - 0.05f, zBack - 0.03f), new Vector3(xs[xs.Length - 1], y0 + h, zBack + 0.03f), post);
            }
            foreach (float x in xs)
            {
                b.BoxMinMax(new Vector3(x - 0.01f, y0 + 0.05f, zFront), new Vector3(x + 0.01f, y0 + h, zBack), wire);
                b.BoxMinMax(new Vector3(x - 0.03f, y0 + h - 0.05f, zFront), new Vector3(x + 0.03f, y0 + h, zBack), post);
            }
            // contents
            for (int i = 0; i < xs.Length - 1; i++)
            {
                float cx = (xs[i] + xs[i + 1]) * 0.5f;
                var t = _tPropsLv[lv];
                float zc = (zFront + zBack) * 0.5f;
                PlaceProp(_props.Shelf(1.8f, i + lv), $"Shelf_Cage_{Tag(lv)}_{i}", t, new Vector3(cx - 0.6f, y0, zBack - 0.4f), 180f);
                switch ((i + lv) % 4)
                {
                    case 0:
                        PlaceProp(_props.TireStack(4), $"Tires_Cage_{Tag(lv)}_{i}", t, new Vector3(cx + 1.3f, y0, zBack - 1.2f), 0f);
                        PlaceProp(_props.CardboardBox(2), $"Boxes_Cage_{Tag(lv)}_{i}", t, new Vector3(cx - 1.2f, y0, zc), 15f);
                        break;
                    case 1:
                        PlaceProp(_props.Crate(1.0f), $"Crate_Cage_{Tag(lv)}_{i}", t, new Vector3(cx + 1.0f, y0, zc), 8f);
                        break;
                    case 2:
                        PlaceProp(_props.PalletStack(0), $"PalletStack_Cage_{Tag(lv)}_{i}", t, new Vector3(cx + 0.9f, y0, zc), 90f);
                        PlaceProp(_props.Drum("PM_Red"), $"Drum_Cage_{Tag(lv)}_{i}", t, new Vector3(cx - 1.4f, y0, zc - 0.5f), 0f);
                        break;
                    default:
                        PlaceProp(_props.Crate(0.8f), $"Crate_Cage_{Tag(lv)}_{i}", t, new Vector3(cx + 1.2f, y0, zc), -6f);
                        PlaceProp(_props.Motorbike("CarPaint_Black"), $"Motorbike_Cage_{Tag(lv)}_{i}", t, new Vector3(cx - 1.0f, y0, zc), 10f);
                        break;
                }
            }
        }
    }
}
