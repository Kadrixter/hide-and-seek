using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Wall fuse box of the escape objective: a grey steel cabinet with an open door (hazard stripes outside, a wiring
    /// sheet inside), a galvanised back plate with a DIN rail and 4 black bakelite sockets in a row, the socket's mark
    /// painted on a black plate above each one, a signal lamp on the front frame, a steel locking bar that swings down
    /// over the fuses when the box is locked and a conduit pipe up to the ceiling. All three boxes are built the same;
    /// only the marks differ.
    /// Box space: origin = wall surface at floor height, +Z = out of the wall, +Y up; socket 0 is on the left of a player
    /// facing the box (box +X), socket 3 on the right.
    /// </summary>
    public static class FuseBoxModel
    {
        public const float W = 0.92f, H = 0.86f, D = 0.34f, CenterY = 1.3f;
        public const float SocketY = 1.2f, SocketSpacing = 0.2f, SocketFace = 0.12f, PlateZ = 0.035f;
        /// <summary>How deep a seated fuse sits in its socket's mouth.</summary>
        public const float SeatDepth = 0.07f;
        /// <summary>Fuse centre (box space z) when it sits fully in its socket.</summary>
        public const float FuseHomeZ = SocketFace - SeatDepth + FuseModel.HalfLengthShown;
        public const float LabelY = 1.37f, LabelHalf = 0.05f;
        public static readonly Vector3 LampLocal = new Vector3(-0.3f, CenterY + H * 0.5f - 0.045f, D + 0.012f);
        /// <summary>The locking bar slides in two side rails: up out of the way (open) / down across the fuse caps (locked).</summary>
        public const float BarUpY = 1.62f, BarDownY = SocketY, BarZ = D - 0.018f;
        /// <summary>Where a player stands to work on the box (floor, box space).</summary>
        public static readonly Vector3 StandLocal = new Vector3(0f, 0f, 0.86f);
        /// <summary>Where the worker stands for socket <paramref name="i"/>: his right shoulder in front of it, close enough to reach.</summary>
        public static Vector3 WorkStand(int i) => new Vector3(SocketX(i) + 0.27f, 0f, 0.8f);

        public static float SocketX(int i) => (1.5f - i) * SocketSpacing;
        public static Vector3 SocketLocal(int i) => new Vector3(SocketX(i), SocketY, SocketFace);
        /// <summary>Fuse centre in socket <paramref name="i"/> pulled <paramref name="out"/> m out of it (box space).</summary>
        public static Vector3 FuseLocal(int i, float @out) => new Vector3(SocketX(i), SocketY, FuseHomeZ + @out);

        private const int SubPaint = 0, SubGalv = 1, SubBakelite = 2, SubBrass = 3, SubDark = 4, SubRail = 5;
        private static Mesh _body, _door, _bar, _lamp, _quad;
        private static Material[] _mats;
        private static Material _hazard, _sheet, _lampGlass;

        public sealed class Parts
        {
            public Transform Root, Door, Bar;
            public Renderer Lamp;
            public Transform[] Labels;
        }

        public static Parts Create(Transform parent, int s0, int s1, int s2, int s3)
        {
            Build();
            var root = new GameObject("FuseBox").transform;
            root.SetParent(parent, false);
            var parts = new Parts { Root = root, Labels = new Transform[4] };
            Renderer(root, "Cabinet", _body, _mats);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, CenterY, D * 0.5f);
            col.size = new Vector3(W, H, D);

            // Door on the right hinge (seen from the front), swung open to the side: the worker stands left of it
            // (the door mesh runs along -X from its hinge, mirrored here to run along +X).
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(root, false);
            hinge.localPosition = new Vector3(-W * 0.5f - 0.005f, CenterY, D);
            hinge.localRotation = Quaternion.Euler(0f, -112f, 0f);
            hinge.localScale = new Vector3(-1f, 1f, 1f);
            Renderer(hinge, "Door", _door, new[] { _mats[SubPaint], _hazard, _sheet, _mats[SubDark] });
            parts.Door = hinge;

            int[] sym = { s0, s1, s2, s3 };
            for (int i = 0; i < 4; i++)
            {
                var l = new GameObject("Mark" + i).transform;
                l.SetParent(root, false);
                l.localPosition = new Vector3(SocketX(i), LabelY, PlateZ + 0.0015f);
                l.localScale = Vector3.one * (LabelHalf * 2f);
                Renderer(l, "Plate", _quad, new[] { FuseModel.LabelMat(sym[i]) });
                parts.Labels[i] = l;
            }

            var bar = new GameObject("LockBar").transform;
            bar.SetParent(root, false);
            bar.localPosition = new Vector3(0f, BarUpY, BarZ);
            Renderer(bar, "Bar", _bar, new[] { _mats[SubRail], _mats[SubDark] });
            parts.Bar = bar;

            var lamp = new GameObject("Lamp").transform;
            lamp.SetParent(root, false);
            lamp.localPosition = LampLocal;
            parts.Lamp = Renderer(lamp, "Dome", _lamp, new[] { _mats[SubBrass], new Material(_lampGlass) });
            return parts;
        }

        /// <summary>Builds the shared meshes / materials now (lobby warm-up).</summary>
        public static void WarmUp() => Build();

        private static Renderer Renderer(Transform parent, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            return r;
        }

        /// <summary>Lamp colours: off (dark), standby amber, green (box working), red (wrong fuses).</summary>
        public static void SetLamp(Renderer lamp, Color c, float glow)
        {
            if (lamp == null) return;
            var m = lamp.sharedMaterials.Length > 1 ? lamp.sharedMaterials[1] : null;
            if (m == null) return;
            m.color = Color.Lerp(new Color(0.12f, 0.1f, 0.08f), c, 0.35f + 0.65f * Mathf.Clamp01(glow));
            m.SetColor("_EmissionColor", c * (glow * 2.2f));
        }

        // ---------------------------------------------------------------- meshes

        private static void Build()
        {
            if (_body != null) return;
            _mats = new[]
            {
                PaMesh.Mat("FuseBoxPaint", new Color(0.46f, 0.5f, 0.5f), 0.32f, 0.35f),
                PaMesh.Mat("FuseBoxGalv", new Color(0.62f, 0.64f, 0.65f), 0.45f, 0.7f),
                PaMesh.Mat("FuseBoxBakelite", new Color(0.06f, 0.055f, 0.05f), 0.5f),
                PaMesh.Mat("FuseBoxBrass", new Color(0.76f, 0.6f, 0.32f), 0.65f, 0.8f),
                PaMesh.Mat("FuseBoxDark", new Color(0.03f, 0.03f, 0.032f), 0.2f),
                PaMesh.Mat("FuseBoxRail", new Color(0.7f, 0.72f, 0.74f), 0.6f, 0.9f),
            };
            _hazard = PaMesh.Mat("FuseBoxHazard", Color.white, 0.3f);
            _hazard.mainTexture = FuseBoxTextures.Hazard();
            _sheet = PaMesh.Mat("FuseBoxSheet", Color.white, 0.1f);
            _sheet.mainTexture = FuseBoxTextures.Sheet();
            _lampGlass = PaMesh.Mat("FuseBoxLamp", new Color(0.3f, 0.25f, 0.15f), 0.85f, 0f, true);
            _quad = FuseBoxTextures.Quad();

            var m = new PaMesh(6);
            Quaternion id = Quaternion.identity;
            float t = 0.02f, x0 = -W * 0.5f, x1 = W * 0.5f, y0 = CenterY - H * 0.5f, y1 = CenterY + H * 0.5f;
            // Shell: back, sides, top, bottom; a rolled front lip.
            m.Box(new Vector3(0f, CenterY, t * 0.5f), new Vector3(W, H, t), id, SubPaint);
            m.Box(new Vector3(x0 + t * 0.5f, CenterY, D * 0.5f), new Vector3(t, H, D), id, SubPaint);
            m.Box(new Vector3(x1 - t * 0.5f, CenterY, D * 0.5f), new Vector3(t, H, D), id, SubPaint);
            m.Box(new Vector3(0f, y1 - t * 0.5f, D * 0.5f), new Vector3(W, t, D), id, SubPaint);
            m.Box(new Vector3(0f, y0 + t * 0.5f, D * 0.5f), new Vector3(W, t, D), id, SubPaint);
            m.Box(new Vector3(0f, y1 - 0.03f, D - 0.006f), new Vector3(W, 0.06f, 0.012f), id, SubPaint);
            m.Box(new Vector3(0f, y0 + 0.03f, D - 0.006f), new Vector3(W, 0.06f, 0.012f), id, SubPaint);
            m.Box(new Vector3(x0 + 0.03f, CenterY, D - 0.006f), new Vector3(0.06f, H, 0.012f), id, SubPaint);
            m.Box(new Vector3(x1 - 0.03f, CenterY, D - 0.006f), new Vector3(0.06f, H, 0.012f), id, SubPaint);
            // Hinges on the right frame (seen from the front = box -X).
            for (int k = 0; k < 2; k++)
                m.Box(new Vector3(x0 - 0.008f, CenterY + (k == 0 ? 0.28f : -0.28f), D - 0.004f), new Vector3(0.018f, 0.07f, 0.018f), id, SubRail);
            // Galvanised mounting plate, DIN rail, cable duct at the bottom.
            m.Box(new Vector3(0f, CenterY - 0.02f, t + 0.0075f), new Vector3(W - 0.08f, H - 0.1f, 0.015f), id, SubGalv);
            m.Box(new Vector3(0f, SocketY, PlateZ + 0.004f), new Vector3(W - 0.14f, 0.035f, 0.008f), id, SubRail);
            m.Box(new Vector3(0f, y0 + 0.09f, PlateZ + 0.025f), new Vector3(W - 0.14f, 0.06f, 0.05f), id, SubDark);
            for (int i = 0; i < 4; i++)
            {
                float x = SocketX(i);
                // Socket block with a dark square mouth and two brass terminal screws; a wire down into the duct.
                m.Box(new Vector3(x, SocketY, (PlateZ + SocketFace) * 0.5f), new Vector3(0.162f, 0.175f, SocketFace - PlateZ), id, SubBakelite);
                m.Box(new Vector3(x, SocketY + 0.004f, SocketFace + 0.0008f), new Vector3(0.122f, 0.122f, 0.0015f), id, SubDark);
                m.Box(new Vector3(x, SocketY + 0.004f, SocketFace + 0.002f), new Vector3(0.134f, 0.006f, 0.004f), id, SubBakelite);
                m.Box(new Vector3(x - 0.06f, SocketY - 0.074f, SocketFace + 0.002f), new Vector3(0.016f, 0.016f, 0.005f), id, SubBrass);
                m.Box(new Vector3(x + 0.06f, SocketY - 0.074f, SocketFace + 0.002f), new Vector3(0.016f, 0.016f, 0.005f), id, SubBrass);
                m.Tube(new[] { new Vector3(x, SocketY - 0.088f, PlateZ + 0.03f), new Vector3(x, y0 + 0.14f, PlateZ + 0.03f) }, 0.007f, 6, SubBakelite);
                // Frame round the painted mark.
                m.Box(new Vector3(x, LabelY, PlateZ + 0.0007f), new Vector3(LabelHalf * 2f + 0.012f, LabelHalf * 2f + 0.012f, 0.0012f), id, SubRail);
            }
            // Side rails of the locking bar (behind the front lip).
            foreach (float x in new[] { -(W * 0.5f - 0.05f), W * 0.5f - 0.05f })
            {
                m.Box(new Vector3(x, (BarDownY + BarUpY) * 0.5f, BarZ - 0.016f), new Vector3(0.03f, BarUpY - BarDownY + 0.12f, 0.008f), id, SubRail);
                m.Box(new Vector3(x + Mathf.Sign(x) * 0.012f, (BarDownY + BarUpY) * 0.5f, BarZ), new Vector3(0.008f, BarUpY - BarDownY + 0.12f, 0.03f), id, SubRail);
            }
            // Conduit up to the ceiling with a gland on the top.
            m.Lathe(new Vector3(0.25f, y1, 0.12f), Vector3.up, new[] { new Vector2(0.035f, 0f), new Vector2(0.035f, 0.04f), new Vector2(0.028f, 0.05f), new Vector2(0.028f, 0.06f) }, 10, SubDark);
            m.Tube(new[] { new Vector3(0.25f, y1 + 0.05f, 0.12f), new Vector3(0.25f, 3.05f, 0.12f) }, 0.024f, 10, SubGalv);
            for (float y = y1 + 0.45f; y < 2.95f; y += 0.7f)
                m.Box(new Vector3(0.25f, y, 0.075f), new Vector3(0.07f, 0.03f, 0.11f), id, SubRail);
            _body = m.ToMesh("FuseBoxBody");

            // Door (hinge space: hinge at the origin, the door along -X, outer face +Z).
            var d = new PaMesh(4);
            float dw = W - 0.01f;
            d.Box(new Vector3(-dw * 0.5f, 0f, 0.012f), new Vector3(dw, H - 0.01f, 0.018f), id, 0);
            d.Quad(new Vector3(-dw + 0.06f, -0.3f, 0.0225f), new Vector3(-0.06f, -0.3f, 0.0225f), new Vector3(-0.06f, -0.38f, 0.0225f), new Vector3(-dw + 0.06f, -0.38f, 0.0225f), Vector3.forward, 1);
            d.Quad(new Vector3(-dw + 0.12f, 0.3f, 0.0025f), new Vector3(-0.12f, 0.3f, 0.0025f), new Vector3(-0.12f, -0.15f, 0.0025f), new Vector3(-dw + 0.12f, -0.15f, 0.0025f), Vector3.back, 2);
            d.Box(new Vector3(-dw + 0.05f, 0f, 0.03f), new Vector3(0.03f, 0.12f, 0.025f), id, 3);
            _door = d.ToMesh("FuseBoxDoor");
            FuseBoxTextures.MapDoorUvs(_door, dw);

            // Locking bar (its centre at the origin): a flat steel bar across the box, shoes in the rails, a padlock tab.
            var b = new PaMesh(2);
            b.Box(Vector3.zero, new Vector3(W - 0.09f, 0.05f, 0.018f), id, 0);
            b.Box(new Vector3(0f, -0.027f, -0.004f), new Vector3(W - 0.11f, 0.006f, 0.012f), id, 1);
            foreach (float x in new[] { -(W * 0.5f - 0.05f), W * 0.5f - 0.05f })
                b.Box(new Vector3(x, 0f, -0.004f), new Vector3(0.04f, 0.075f, 0.026f), id, 1);
            b.Box(new Vector3(0f, -0.005f, 0.016f), new Vector3(0.06f, 0.08f, 0.016f), id, 0);
            b.Lathe(new Vector3(0f, -0.03f, 0.03f), Vector3.forward, new[] { new Vector2(0.012f, -0.006f), new Vector2(0.012f, 0.006f), new Vector2(0f, 0.008f) }, 8, 1);
            _bar = b.ToMesh("FuseBoxBar");

            var l = new PaMesh(2);
            l.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0.034f, -0.012f), new Vector2(0.034f, 0.004f), new Vector2(0.027f, 0.006f) }, 12, 0);
            l.Lathe(Vector3.zero, Vector3.forward, new[] { new Vector2(0.026f, 0.004f), new Vector2(0.024f, 0.016f), new Vector2(0.016f, 0.026f), new Vector2(0f, 0.029f) }, 12, 1);
            _lamp = l.ToMesh("FuseBoxLamp");
        }
    }
}
