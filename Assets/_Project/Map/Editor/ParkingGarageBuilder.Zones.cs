using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Distinct zones per floor. Level -1: loading dock, car wash, motorbike/bicycle area, security booth with
    /// barrier gates. Level -2: EV charging bays, storage cages, electrical room, pallet storage, recycling.
    /// Level -3 (old, wet, damaged): construction zones with scaffolding and tarp-covered cars, collapsed area,
    /// flooded quarter.
    /// </summary>
    internal sealed partial class ParkingGarageBuilder
    {
        private int _propIndex;

        private void P(int lv, PropMesh pm, string name, float x, float z, float yaw, Material[] mats = null)
        {
            var p = new Vector3(x, FloorY(lv), z);
            if (NearSpawn(p, 1.4f)) return;
            PlaceProp(pm, $"{name}_{Tag(lv)}_{++_propIndex:000}", _tPropsLv[lv], p, yaw, mats);
        }

        private void BuildZones(int lv)
        {
            switch (lv)
            {
                case 0:
                    CarWash(lv);
                    MotoArea(lv);
                    SecurityGate(lv);
                    LoadingDock(lv);
                    break;
                case 1:
                    Cages(lv, new[] { -29f, -24.75f, -20.5f, -16.25f, -12f }, -4.2f, 4.2f, false);
                    ElectricalRoom(lv);
                    break;
                default:
                    ConstructionCentral(lv);
                    CollapsedArea(lv);
                    ConstructionRow(lv);
                    break;
            }
            NorthZones(lv);
        }

        // ------------------------------------------------------------------ level -1

        private void CarWash(int lv)
        {
            float y0 = FloorY(lv);
            LevelWall(lv, -26.2f, -26f, -6f, 6f);
            LevelWall(lv, -14f, -13.8f, -6f, 6f);
            _decals[lv].FloorQuad(-26f, -14f, -6f, 6f, y0 + DecalY.Under, M("Tile_Wash"));
            _decals[lv].FloorQuad(-20.2f, -19.8f, -5.5f, 5.5f, y0 + DecalY.Stain, M("Grate"));
            foreach (var (x, n) in new[] { (-26f, Vector3.right), (-14f, Vector3.left) })
            {
                WallQuad(_walls[lv], new Vector3(x, y0 + 1.3f, 0) + n * 0.012f, n, 5.9f, 1.25f, M("Tile_Wash"));
                _walls[lv].Cylinder(new Vector3(x + n.x * 0.06f, y0 + 1.6f, -4f), new Vector3(x + n.x * 0.06f, y0 + 1.6f, 4f), 0.03f, 6, M("Plastic_Black"));
            }
            foreach (var (x, n) in new[] { (-26.2f, Vector3.left), (-13.8f, Vector3.right) })
                SignPlate(_signs[lv], new Vector3(x, y0 + 2.2f, 0), n, 2.4f, 0.4f, "CAR WASH", 0.24f, M("Sign_Blue"), M("Text_White"));
            _decals[lv].Text("WASH", new Vector3(-20f, y0 + DecalY.Mark, -7.6f), Vector3.up, Vector3.forward, 0.9f, M("Text_Floor"));
            P(lv, _props.CarWashGantry(), "CarWashGantry", -20f, 1.8f, 0f);
            PlaceCar(lv, ParkedModel("Estate_1"), new Vector3(-20f, y0, 0f), Vector3.forward, 6f, "WASH", true, "CarPaint_Silver");
            for (int i = 0; i < 5; i++)
                _decals[lv].FloorDecal(new Vector3(R(-25f, -15f), y0 + DecalY.Puddle, R(-5.5f, 5.5f)), new Vector3(R(-1, 1), 0, 1), R(1.5f, 3f), R(2f, 4f), M("Puddle"));
            P(lv, _props.PayMachine(), "WashTerminal", -27.2f, -6.8f, 180f);
            P(lv, _props.Drum("PM_Blue"), "Detergent", -12.8f, 6.5f, 0f);
            P(lv, _props.Drum("PM_Yellow"), "Detergent", -12.2f, 7.2f, 0f);
            P(lv, _props.Drum("PM_Blue"), "Detergent", -27.6f, 6.8f, 0f);
            P(lv, _props.Cone(), "Cone", -23f, -7.2f, 0f);
            P(lv, _props.Cone(), "Cone", -17f, -7.2f, 30f);
        }

        private void MotoArea(int lv)
        {
            float y0 = FloorY(lv);
            var d = _decals[lv];
            var bikes = new[] { "CarPaint_Red", "CarPaint_Black", "CarPaint_Blue", "CarPaint_Yellow", "CarPaint_Green", "CarPaint_Orange" };
            for (int row = 0; row < 2; row++)
            {
                float zA = row == 0 ? -8.4f : 4.4f, zB = zA + 4f;
                Vector3 inward = row == 0 ? Vector3.forward : Vector3.back;
                for (int i = 0; i < 16; i++)
                {
                    float x = 9.5f + i * 1.2f;
                    d.FloorQuad(x - 0.04f, x + 0.04f, zA, zB, y0 + DecalY.Paint, M("Paint_Yellow"));
                    if (_rng.NextDouble() < 0.55)
                    {
                        float bx = x + 0.6f;
                        float bz = (zA + zB) * 0.5f;
                        P(lv, _props.Motorbike(bikes[_rng.Next(bikes.Length)]), "Motorbike", bx, bz, (inward.z > 0 ? 0f : 180f) + R(-6f, 6f));
                    }
                }
                d.FloorQuad(9.5f - 0.04f + 16 * 1.2f, 9.5f + 0.04f + 16 * 1.2f, zA, zB, y0 + DecalY.Paint, M("Paint_Yellow"));
                d.Text("MOTO", new Vector3(19.1f, y0 + DecalY.Mark, row == 0 ? -9.2f + 0.0f : 9.2f - 0.0f), Vector3.up, inward, 0.6f, M("Text_FloorYellow"));
            }
            for (int r = 0; r < 4; r++)
            {
                float x = 11.5f + r * 5f;
                P(lv, _props.BikeRack(5), "BikeRack", x, 0f, 0f);
                for (int k = 0; k < 5; k++)
                    if (_rng.NextDouble() < 0.6)
                        P(lv, _props.Bicycle(bikes[_rng.Next(bikes.Length)]), "Bicycle", x + (k - 2) * 0.75f, -0.9f + (k % 2) * 1.8f, k % 2 == 0 ? 0f : 180f);
            }
            SignPlate(_signs[lv], new Vector3(8.6f, y0 + 2.2f, -5f), Vector3.left, 1.6f, 0.3f, "BIKES", 0.18f, M("Sign_Blue"), M("Text_White"));
        }

        private void SecurityGate(int lv)
        {
            float y0 = FloorY(lv);
            P(lv, _props.SecurityBooth(), "SecurityBooth", 80.5f, -9.5f, 0f);
            P(lv, _props.BarrierGate(), "BarrierGate", 75.6f, -2.5f, 0f);
            P(lv, _props.BarrierGate(), "BarrierGate", 83.6f, 2.5f, 180f);
            var d = _decals[lv];
            d.FloorQuad(75.5f, 80.5f, -3.1f, -2.9f, y0 + DecalY.Paint, M("Paint_White"));
            d.FloorQuad(78.6f, 83.6f, 2.9f, 3.1f, y0 + DecalY.Paint, M("Paint_White"));
            d.Text("STOP", new Vector3(78f, y0 + DecalY.Mark, -4.5f), Vector3.up, Vector3.forward, 0.8f, M("Text_Floor"));
            d.Text("STOP", new Vector3(81f, y0 + DecalY.Mark, 4.5f), Vector3.up, Vector3.back, 0.8f, M("Text_Floor"));
            d.FloorQuad(86.5f, 89.9f, -3.5f, 3.5f, y0 + DecalY.Hatch, M("Hatch_Yellow"));
            d.Text("EXIT", new Vector3(84f, y0 + DecalY.Mark, 0f), Vector3.up, Vector3.right, 0.9f, M("Text_Floor"));
            foreach (float z in new[] { -6f, -5f, 5f, 6f })
                P(lv, _props.Bollard(), "Bollard", 86f, z, 0f);
        }

        private void LoadingDock(int lv)
        {
            float y0 = FloorY(lv);
            var b = _walls[lv];
            const float h = 1.1f, zFront = 54.5f;
            b.BoxMinMax(new Vector3(-HX, y0, zFront), new Vector3(-66f, y0 + h, HZ), WallMat(lv));
            b.BoxMinMax(new Vector3(-HX, y0 + h, zFront), new Vector3(-66f, y0 + h + 0.01f, zFront + 0.35f), HazardMat(lv), MeshBuilder.Faces.PosY);
            for (int i = 0; i < 4; i++)
            {
                float x = -86f + i * 5.5f;
                b.BoxMinMax(new Vector3(x - 0.2f, y0 + 0.25f, zFront - 0.18f), new Vector3(x + 0.2f, y0 + 0.95f, zFront), M("Rubber"));
                // dock shutter in the north wall above the platform
                b.BoxMinMax(new Vector3(x + 1.2f, y0 + h, HZ - 0.08f), new Vector3(x + 4.0f, y0 + 2.85f, HZ), M("Shutter"));
                SignPlate(_signs[lv], new Vector3(x + 2.6f, y0 + 2.92f, HZ - 0.08f), Vector3.back, 0.6f, 0.14f, "DOCK " + (i + 1), 0.09f, M("Sign_Yellow"), M("Text_Black"));
            }
            // steps at the east end + invisible ramp collider
            for (int s = 0; s < 4; s++)
            {
                float xa = -66f + s * 0.55f;
                b.BoxMinMax(new Vector3(xa, y0, zFront + 0.5f), new Vector3(xa + 0.55f, y0 + h - s * 0.275f, zFront + 2.5f), WallMat(lv));
            }
            var pTop = new Vector3(-66f, y0 + h, zFront + 1.5f);
            var pBot = new Vector3(-63.8f, y0, zFront + 1.5f);
            var rot = Quaternion.LookRotation((pBot - pTop).normalized, Vector3.up);
            var col = new GameObject("DockStepsCollider_L1");
            col.transform.SetParent(_tStairs, false);
            col.transform.SetPositionAndRotation((pTop + pBot) * 0.5f - rot * Vector3.up * 0.15f, rot);
            col.AddComponent<BoxCollider>().size = new Vector3(2f, 0.3f, (pBot - pTop).magnitude + 0.1f);
            GameObjectUtility.SetStaticEditorFlags(col, DetailFlags);
            _decals[lv].FloorQuad(-84f, -66f, zFront - 2.5f, zFront - 0.2f, y0 + DecalY.Hatch, M("Hatch_Yellow"));
            SignPlate(_signs[lv], new Vector3(-75f, y0 + 0.65f, zFront), Vector3.back, 2.6f, 0.3f, "LOADING DOCK", 0.18f, M("Sign_Yellow"), M("Text_Black"));
            float top = y0 + h;
            void D(PropMesh pm, string n, float x, float z, float yaw) =>
                PlaceProp(pm, $"{n}_{Tag(lv)}_{++_propIndex:000}", _tPropsLv[lv], new Vector3(x, top, z), yaw);
            D(_props.PalletStack(0), "PalletStack", -86.5f, 57.5f, 0f);
            D(_props.PalletStack(1), "PalletStack", -84.8f, 57.5f, 90f);
            D(_props.PalletStack(2), "PalletPile", -80f, 58.8f, 0f);
            D(_props.Crate(1.0f), "Crate", -77f, 56.5f, 12f);
            D(_props.Crate(0.8f), "Crate", -76.8f, 57.8f, -8f);
            D(_props.SackStack(), "Sacks", -71.5f, 58.5f, 0f);
            D(_props.CardboardBox(4), "Boxes", -69f, 56f, 20f);
            P(lv, _props.StackedChairs(), "Barrier", -70f, 52.5f, 0f);
        }

        // ------------------------------------------------------------------ level -2

        private void ElectricalRoom(int lv)
        {
            float y0 = FloorY(lv);
            LevelWall(lv, 9f, 29f, -5f, -4.8f, new Opening(18.4f, 19.6f, 2.1f));
            LevelWall(lv, 9f, 29f, 4.8f, 5f);
            LevelWall(lv, 9f, 9.2f, -4.8f, 4.8f);
            LevelWall(lv, 28.8f, 29f, -4.8f, 4.8f);
            SignPlate(_signs[lv], new Vector3(14f, y0 + 1.7f, -5f), Vector3.back, 1.8f, 0.4f, "ELECTRICAL", 0.2f, M("Sign_Yellow"), M("Text_Black"));
            PlaceProp(_props.Door("PM_DarkGrey", 1.15f), $"Door_Electrical_{Tag(lv)}", _tPropsLv[lv], new Vector3(19.55f, y0, -4.75f), -110f);
            for (int i = 0; i < 18; i++)
                P(lv, _props.ElectricalCabinet(i), "ElectricalCabinet", 9.8f + i * 1.02f, 4.4f, 180f);
            P(lv, _props.Transformer(), "Transformer", 12f, -1f, 90f);
            P(lv, _props.Transformer(), "Transformer", 25.5f, -1f, 90f);
            P(lv, _props.Generator(), "Generator", 21f, -2.6f, 0f);
            _decals[lv].FloorQuad(18f, 20f, -6.4f, -5f, y0 + DecalY.Hatch, M("Hatch_Yellow"));
        }

        private void PalletStorage(int lv, float x0, float x1)
        {
            for (float x = x0 + 1.5f; x < x1 - 1.2f; x += 2.85f)
                P(lv, _props.PalletRack((int)(x * 10) & 7), "PalletRack", x, 59.35f, 180f);
            for (float x = x0 + 1.5f; x < x1 - 4f; x += 2.85f)
                P(lv, _props.PalletRack(((int)(x * 10) + 3) & 7), "PalletRack", x, 55.2f, 0f);
            P(lv, _props.PalletStack(1), "PalletStack", x1 - 2f, 52.5f, 15f);
            P(lv, _props.PalletStack(2), "PalletPile", x0 + 2f, 52.6f, 90f);
        }

        // ------------------------------------------------------------------ level -3

        private void FenceLine(int lv, Vector3 a, Vector3 b, float gapEvery = 0f)
        {
            Vector3 d = b - a;
            int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 3.5f));
            float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                if (gapEvery > 0 && i % (int)gapEvery == (int)gapEvery - 1) continue;
                Vector3 p = a + d * ((i + 0.5f) / n);
                P(lv, _props.TempFence(), "TempFence", p.x, p.z, yaw + R(-4f, 4f));
            }
        }

        private void TarpCar(int lv, float x, float z, float yaw)
        {
            var type = _rng.NextDouble() < 0.6 ? CarType.Sedan : _rng.NextDouble() < 0.5 ? CarType.Suv : CarType.Van;
            P(lv, _props.TarpCar(type, _rng.NextDouble() < 0.5 ? "Tarp_Blue" : "Tarp_Grey"), "TarpCar", x, z, yaw);
            _carCount++;
        }

        private void ConstructionCentral(int lv)
        {
            float y0 = FloorY(lv);
            FenceLine(lv, new Vector3(-29.4f, 0, -7.6f), new Vector3(-8.6f, 0, -7.6f), 3);
            FenceLine(lv, new Vector3(-29.4f, 0, 7.6f), new Vector3(-8.6f, 0, 7.6f), 4);
            P(lv, _props.Scaffold(3), "Scaffold", -19f, 5.6f, 0f);
            P(lv, _props.Scaffold(2), "Scaffold", -26.5f, -3f, 90f);
            P(lv, _props.SackStack(), "Sacks", -15f, -4.5f, 10f);
            P(lv, _props.SackStack(), "Sacks", -13.5f, -4.4f, -5f);
            P(lv, _props.Rubble(2), "Rubble", -17.5f, 1.5f, 30f);
            P(lv, _props.Rubble(1), "Rubble", -11f, 2f, 0f);
            P(lv, _props.Drum("Rust"), "Drum", -10f, -5.5f, 0f);
            TarpCar(lv, -22f, -2.5f, 90f);
            P(lv, _props.Cone(), "Cone", -9.5f, -8.2f, 0f);
            SignPlate(_signs[lv], new Vector3(-27f, y0 + 2.1f, -7.5f), Vector3.back, 2.4f, 0.4f, "CLOSED AREA", 0.2f, M("Sign_Yellow"), M("Text_Black"));
        }

        private void CollapsedArea(int lv)
        {
            float y0 = FloorY(lv);
            FenceLine(lv, new Vector3(8.6f, 0, -7.6f), new Vector3(29.4f, 0, -7.6f), 3);
            FenceLine(lv, new Vector3(8.6f, 0, 7.6f), new Vector3(29.4f, 0, 7.6f), 0);
            for (int i = 0; i < 7; i++)
                P(lv, _props.Rubble(i % 4), "Rubble", R(10f, 28f), R(-5.5f, 5.5f), R(0f, 360f));
            // fallen ceiling slabs
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector3(13f + i * 6f, y0 + 0.5f, R(-3f, 3f));
                _walls[lv].Box(c, new Vector3(R(2.5f, 4f), 0.35f, R(2f, 3f)), CeilMat(lv), Quaternion.Euler(R(-18f, 18f), R(0f, 90f), R(10f, 25f)));
            }
            TarpCar(lv, 25f, 2.5f, 80f);
            SignPlate(_signs[lv], new Vector3(19f, y0 + 2.1f, -7.5f), Vector3.back, 2.4f, 0.4f, "DANGER", 0.24f, M("Sign_Red"), M("Text_White"));
            _decals[lv].FloorDecal(new Vector3(19f, y0 + DecalY.Flood, 0f), Vector3.forward, 14f, 10f, M("Flood_Water"));
        }

        private void ConstructionRow(int lv)
        {
            float y0 = FloorY(lv);
            FenceLine(lv, new Vector3(-75f, 0, 14.8f), new Vector3(-45f, 0, 14.8f), 3);
            FenceLine(lv, new Vector3(-75f, 0, 25.2f), new Vector3(-45f, 0, 25.2f), 4);
            foreach (float x in new[] { -63.75f, -56.25f, -48.75f })
                P(lv, _props.Scaffold(2), "Scaffold", x, 20f, 0f);
            foreach (var (x, z) in new[] { (-72.5f, 17.3f), (-69f, 22.7f), (-60f, 17.3f), (-60f, 22.7f), (-52.5f, 22.7f) })
                TarpCar(lv, x, z, z < 20f ? 0f : 180f);
            P(lv, _props.SackStack(), "Sacks", -70f, 17f, 0f);
            P(lv, _props.Rubble(3), "Rubble", -65.5f, 23f, 40f);
            P(lv, _props.Rubble(0), "Rubble", -50f, 16.8f, 0f);
            _decals[lv].Text("CLOSED", new Vector3(-60f, y0 + DecalY.Mark, 13.6f), Vector3.up, Vector3.forward, 0.9f, M("Text_FloorYellow"));
        }

        // ------------------------------------------------------------------ north zones (all levels)

        private void NorthZones(int lv)
        {
            float y0 = FloorY(lv);
            // north-west block
            if (lv == 1) PalletStorage(lv, -84f, -64f);
            if (lv == 2)
            {
                TarpCar(lv, -80f, 56f, 90f);
                TarpCar(lv, -72f, 56.2f, 95f);
                P(lv, _props.Rubble(1), "Rubble", -67f, 58f, 0f);
                P(lv, _props.Crate(1.0f), "Crate", -86.5f, 58.6f, 5f);
                _decals[lv].FloorDecal(new Vector3(-76f, y0 + DecalY.Flood, 54f), Vector3.right, 12f, 8f, M("Flood_Water"));
            }
            // waste corner between the north lobby and the corridor
            P(lv, _props.WheelieBin("PM_Green"), "Bin", 7.2f, 58.9f, 10f);
            P(lv, _props.WheelieBin("PM_Blue"), "Bin", 8.1f, 59f, -8f);
            P(lv, _props.TrashBags(lv), "TrashBags", 10.4f, 58.6f, 30f);
            // in front of the maintenance corridor
            switch (lv)
            {
                case 0:
                    // Racks stand clear of the three corridor doors (x 16-17.2, 29-30.2, 39-40.2) and of the corridor wall
                    // (z 57.2): bicycles are 1.7 m long, so the back row ends at z = 55.0 + 0.9 + 0.85 = 56.75.
                    foreach (float x in new[] { 13.6f, 22.5f, 34.5f, 43.2f })
                    {
                        P(lv, _props.BikeRack(5), "BikeRack", x, 55.0f, 0f);
                        for (int k = 0; k < 5; k++)
                            if (_rng.NextDouble() < 0.5)
                                P(lv, _props.Bicycle(MaterialLibrary.CarPaints[_rng.Next(MaterialLibrary.CarPaints.Length)]), "Bicycle", x + (k - 2) * 0.75f, 54.1f + (k % 2) * 1.8f, k % 2 == 0 ? 0f : 180f);
                    }
                    break;
                case 1:
                    P(lv, _props.Dumpster("PM_Green"), "Dumpster", 16f, 55.8f, 0f);
                    P(lv, _props.Dumpster("PM_Blue"), "Dumpster", 19f, 55.8f, 0f);
                    P(lv, _props.PalletStack(0), "BaledCardboard", 24f, 55.5f, 0f);
                    P(lv, _props.PalletStack(0), "BaledCardboard", 25.6f, 55.5f, 0f);
                    P(lv, _props.TrashBags(4), "TrashBags", 21.5f, 55.2f, 0f);
                    break;
                default:
                    P(lv, _props.Rubble(2), "Rubble", 18f, 55f, 0f);
                    P(lv, _props.Scaffold(2), "Scaffold", 33f, 55.6f, 0f);
                    P(lv, _props.SackStack(), "Sacks", 25f, 55.4f, 0f);
                    break;
            }
            // maintenance corridor contents
            P(lv, _props.WaterHeater(), "WaterHeater", 13.6f, 59.5f, 0f);
            P(lv, _props.Shelf(2.0f, 3 + lv), "Shelf", 22f, 59.6f, 180f);
            P(lv, _props.Crate(0.8f), "Crate", 34.5f, 59.4f, 4f);
            P(lv, _props.MopBucket(), "MopBucket", 36.8f, 58.4f, 30f);
            P(lv, _props.Drum(lv == 0 ? "PM_Blue" : "PM_Red"), "Drum", 43.3f, 59.4f, 0f);
            P(lv, _props.CardboardBox(6 + lv), "Boxes", 41.5f, 59.5f, 0f);
            // tech / generator / pump room contents
            if (lv == 0)
            {
                for (int i = 0; i < 20; i++)
                    P(lv, _props.ElectricalCabinet(i), "ElectricalCabinet", -31.4f + i * 0.98f, 59.7f, 180f);
                P(lv, _props.Transformer(), "Transformer", -28f, 55f, 90f);
                P(lv, _props.Transformer(), "Transformer", -15f, 55f, 90f);
            }
            else if (lv == 1)
            {
                P(lv, _props.Generator(), "Generator", -27f, 57.5f, 0f);
                P(lv, _props.Generator(), "Generator", -16.5f, 57.5f, 0f);
                P(lv, _props.Tank(), "Tank", -29.5f, 53.5f, 0f);
                for (int i = 0; i < 6; i++)
                    P(lv, _props.Workbench(), "Workbench", -55f + i * 3.6f, 59.4f, 180f);
                P(lv, _props.Shelf(2.0f, 7), "Shelf", -52f, 53.3f, 0f);
                P(lv, _props.TireStack(5), "Tires", -36f, 53.5f, 0f);
            }
            else
            {
                P(lv, _props.Tank(), "Tank", -28f, 56f, 0f);
                P(lv, _props.Tank(), "Tank", -24f, 58.6f, 40f);
                P(lv, _props.Pump(), "Pump", -17f, 58.9f, 0f);
                P(lv, _props.Pump(), "Pump", -14f, 55f, 90f);
                _decals[lv].FloorDecal(new Vector3(-22f, y0 + DecalY.Flood, 55.5f), Vector3.right, 16f, 8f, M("Flood_Water"));
            }
            // north-east open area
            if (lv == 0)
            {
                P(lv, _props.Dumpster("PM_Green"), "Dumpster", 56f, 59f, 0f);
                P(lv, _props.Dumpster("PM_Blue"), "Dumpster", 59.5f, 59f, 180f);
                P(lv, _props.PalletStack(2), "PalletPile", 64f, 58.8f, 0f);
                P(lv, _props.TrashBags(5), "TrashBags", 62f, 57f, 0f);
            }
            else if (lv == 1) PalletStorage(lv, 52f, 84f);
            else
            {
                P(lv, _props.Scaffold(3), "Scaffold", 66f, 58.8f, 0f);
                P(lv, _props.SackStack(), "Sacks", 58f, 57f, 0f);
                P(lv, _props.SackStack(), "Sacks", 59.3f, 57.2f, 6f);
                P(lv, _props.Rubble(3), "Rubble", 74f, 56f, 0f);
                TarpCar(lv, 80f, 56f, 90f);
            }
        }

        // ------------------------------------------------------------------ props everywhere

        private void BuildCommonProps(int lv)
        {
            float y0 = FloorY(lv);
            // extinguisher boxes on columns
            for (int j = 0; j < ColZ.Length; j++)
            for (int i = 0; i < ColX.Length; i++)
            {
                if ((i + j + lv) % 3 != 0 || ColumnBlocked(ColX[i], ColZ[j])) continue;
                bool north = j % 2 == 0;
                var pos = new Vector3(ColX[i], y0 + 1.0f, ColZ[j] + (north ? 0.3f : -0.3f));
                PlaceProp(_props.ExtinguisherBox(), $"ExtinguisherBox_{Tag(lv)}_{++_propIndex:000}", _tPropsLv[lv], pos, north ? 0f : 180f, null, false);
            }
            // corner pockets
            foreach (var (x, z) in new[] { (-87f, -50f), (87f, -50f), (-87f, 49f), (87f, 49f) })
            {
                if (lv == 0 && z > 0 && x < 0) continue; // loading dock stairs side
                int pick = _rng.Next(3);
                if (pick == 0) P(lv, _props.Dumpster(lv == 2 ? "PM_Orange" : "PM_Green"), "Dumpster", x, z, 90f);
                else if (pick == 1) P(lv, _props.PalletStack(_rng.Next(3)), "PalletStack", x, z, 0f);
                else P(lv, _props.Crate(1.0f), "Crate", x, z, R(0, 30));
                P(lv, _props.Drum(_rng.NextDouble() < 0.5 ? "PM_Blue" : "PM_Red"), "Drum", x + (x < 0 ? 1.6f : -1.6f), z + 1.4f, 0f);
            }
            // service tunnel clutter
            for (float x = -80f; x < 80f; x += 13f)
            {
                float px = x + R(-3f, 3f);
                if (TunnelDoors.Any(dx => Mathf.Abs(dx - px) < 1.5f)) continue;
                int pick = _rng.Next(4);
                PropMesh pm = pick == 0 ? _props.Crate(0.8f) : pick == 1 ? _props.Drum("PM_Blue") : pick == 2 ? _props.TireStack(3) : _props.CardboardBox(_rng.Next(8));
                P(lv, pm, "TunnelClutter", px, -58.9f, R(0, 360));
            }
            // cones and barriers at the ramp approaches
            foreach (var r in Ramps)
            {
                float zc = (r.Z0 + r.Z1) * 0.5f;
                float dx = r.XBot < r.XTop ? 1f : -1f;
                if (lv < Levels - 1)
                {
                    P(lv, _props.Cone(), "Cone", r.XTop + dx * 1.2f, r.Z0 + 0.6f, 0f);
                    P(lv, _props.Cone(), "Cone", r.XTop + dx * 1.2f, r.Z1 - 0.6f, 40f);
                }
                if (lv > 0)
                {
                    P(lv, _props.Cone(), "Cone", r.XBot - dx * 1.5f, r.Z0 + 0.6f, 10f);
                    P(lv, _props.Cone(), "Cone", r.XBot - dx * 1.5f, r.Z1 - 0.6f, 70f);
                }
            }
            // walkway clutter along the tunnel wall
            for (int i = 0; i < 6; i++)
            {
                float x = R(-80f, 80f);
                if (TunnelDoors.Any(dx => Mathf.Abs(dx - x) < 2f)) continue;
                P(lv, _rng.NextDouble() < 0.5 ? _props.Drum(lv == 2 ? "Rust" : "PM_Red") : _props.Crate(0.8f), "Clutter", x, -55.5f, R(0, 360));
            }
        }
    }
}
