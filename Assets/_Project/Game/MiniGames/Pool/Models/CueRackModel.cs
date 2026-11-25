using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Wall cue rack in walnut: two uprights with turned knobs, a butt shelf with felt-lined round cups, a shaft rail with
    /// U notches, a crown shelf with three ball cups and a brass gallery (room for chalk), a pediment board, side brackets.
    /// One extra wider slot on the right holds the bridge, a brass peg on a small plaque on the left takes the triangle.
    /// Space: root on the floor at the wall, +X along the wall, +Z from the wall into the room, the back at z = 0.
    /// </summary>
    public static class CueRackModel
    {
        /// <summary>Distance between two cue slots, and from the last one to the bridge slot.</summary>
        public const float Pitch = 0.075f, BridgeGap = 0.105f;
        /// <summary>Top of the butt shelf, depth of its cups, cup radius and how far the cups are from the wall.</summary>
        public const float ShelfY = 0.30f, CupDepth = 0.02f, CupR = 0.021f, CupZ = 0.072f;
        /// <summary>Top of the shaft rail, its thickness, notch radius and the centre of the notches' round ends.</summary>
        public const float RailY = 1.40f, RailT = 0.022f, NotchR = 0.0115f, NotchZ = 0.048f;
        /// <summary>Top of the crown shelf (above the cue tips).</summary>
        public const float CrownY = 1.84f;
        /// <summary>Radius of a cue where it rests in a notch (about 1.1 m from the butt).</summary>
        const float ShaftR = 0.0088f;

        /// <summary>X of slot <paramref name="i"/> (0..slots-1 cues, slots = the bridge slot).</summary>
        public static float SlotX(int slots, int i)
        {
            float run = (slots - 1) * Pitch + BridgeGap;
            float x = i < slots ? i * Pitch : (slots - 1) * Pitch + BridgeGap;
            return x - run * 0.5f;
        }

        /// <summary>Half width of the space between the uprights.</summary>
        static float InnerHalf(int slots) => ((slots - 1) * Pitch + BridgeGap) * 0.5f + 0.06f;

        /// <summary>
        /// Pose of a cue (built in cue space: butt at z = 0, tip along +Z) standing in slot <paramref name="i"/>, in rack
        /// space: the butt in the cup, the shaft leaning back against the end of its notch. Slot <c>slots</c> = the bridge.
        /// </summary>
        public static void SlotPose(int i, out Vector3 localPos, out Quaternion localRot) => SlotPose(DefaultSlots, i, out localPos, out localRot);

        /// <summary>Slot count assumed by the short <see cref="SlotPose(int, out Vector3, out Quaternion)"/>.</summary>
        public const int DefaultSlots = 8;

        /// <summary><see cref="SlotPose(int, out Vector3, out Quaternion)"/> for a rack with <paramref name="slots"/> slots.</summary>
        public static void SlotPose(int slots, int i, out Vector3 localPos, out Quaternion localRot)
        {
            float x = SlotX(slots, Mathf.Clamp(i, 0, slots));
            localPos = new Vector3(x, ShelfY - CupDepth + 0.0005f, CupZ);
            Vector3 top = new Vector3(x, RailY - RailT * 0.5f, NotchZ - NotchR + 0.0025f + ShaftR);
            localRot = Quaternion.LookRotation((top - localPos).normalized, Vector3.forward);
        }

        /// <summary>Point where the triangle's apex hangs on the peg (rack space).</summary>
        public static Vector3 TrianglePeg(int slots) => new Vector3(-InnerHalf(slots) - 0.05f - 0.17f, 1.2f, 0.065f);

        public static Transform Build(Transform parent, Material template, int slots)
        {
            slots = Mathf.Clamp(slots, 2, 12);
            var root = new GameObject("CueRack").transform;
            root.SetParent(parent, false);
            int layer = parent != null ? parent.gameObject.layer : 0;
            root.gameObject.layer = layer;
            var kit = new TableKit(template, layer, 0);
            var felt = kit.Mat(new Color(0.06f, 0.26f, 0.13f), 0f, 0.08f, BoardArt.Felt);
            Material wood = kit.TableWood, trim = kit.LegWood;

            float ih = InnerHalf(slots), sx = ih + 0.025f, outer = sx + 0.025f;
            var holes = new float[slots + 1];
            for (int i = 0; i <= slots; i++) holes[i] = SlotX(slots, i);
            string k = slots.ToString();

            // ---- uprights
            var stile = BoardMeshes.Extrude("rackstile", CueRackMeshes.StileOutline(0.05f, 0.2f, 1.93f), _ => 0.016f, 0.004f, 3, 4f);
            foreach (float s in new[] { -sx, sx })
            {
                kit.Part("Stile", root, stile, trim, new Vector3(s, 0f, 0.016f), CueRackMeshes.Upright);
                kit.Part("StileFinial", root, Finial(), trim, new Vector3(s, 1.929f, 0.016f));
            }

            // ---- back boards between the uprights
            float bx0 = -sx, bx1 = sx;
            kit.Part("BackLow", root, Upright("rackbacklow" + k, Rect(bx0, bx1, 0.215f, 0.37f)), wood, new Vector3(0f, 0f, 0.008f), CueRackMeshes.Upright);
            kit.Part("BackRail", root, Upright("rackbackrail" + k, Rect(bx0, bx1, 1.325f, 1.43f)), wood, new Vector3(0f, 0f, 0.008f), CueRackMeshes.Upright);
            kit.Part("Pediment", root, Upright("rackpediment" + k, CueRackMeshes.ArchOutline(bx0, bx1, CrownY - 0.005f, CrownY + 0.07f, 0.045f)), wood, new Vector3(0f, 0f, 0.008f), CueRackMeshes.Upright);
            kit.Part("Rosette", root, Rosette(), kit.Brass, new Vector3(0f, CrownY + 0.075f, 0.016f), Quaternion.Euler(90f, 0f, 0f));

            // ---- butt shelf: holed top, body, nosing, brackets, felt in the cups
            float z0 = 0.016f, z1 = 0.126f;
            kit.Part("ShelfTop", root, CueRackMeshes.HoledTop("rackbutt" + k, -outer, outer, z0, z1, holes, CupZ, CupR, CupDepth, 0.028f), wood, new Vector3(0f, ShelfY, 0f));
            kit.Part("ShelfBody", root, BoardMeshes.RoundedBox(new Vector3(outer * 2f - 0.002f, 0.034f, z1 - z0 - 0.002f), 0.003f, 3), wood, new Vector3(0f, ShelfY - CupDepth - 0.0185f, (z0 + z1) * 0.5f));
            kit.Part("ShelfNose", root, BoardMeshes.RoundedBox(new Vector3(outer * 2f + 0.006f, 0.024f, 0.018f), 0.0089f, 4), trim, new Vector3(0f, ShelfY - 0.013f, z1));
            kit.Part("ShelfCove", root, BoardMeshes.RoundedBox(new Vector3(outer * 2f - 0.01f, 0.012f, 0.012f), 0.0059f, 3), trim, new Vector3(0f, ShelfY - 0.057f, z1 - 0.008f));
            var feltDisc = Disc("rackfelt", CupR + 0.0005f);
            foreach (float x in holes) kit.Part("CupFelt", root, feltDisc, felt, new Vector3(x, ShelfY - CupDepth + 0.0003f, CupZ), Quaternion.identity, false);

            // ---- shaft rail with notches
            var rail = BoardMeshes.Extrude("rackrail" + k, CueRackMeshes.NotchedOutline(-outer, outer, z0, 0.106f, holes, NotchZ, NotchR), _ => RailT * 0.5f, 0.0028f, 3, 4f);
            kit.Part("ShaftRail", root, rail, wood, new Vector3(0f, RailY - RailT * 0.5f, 0f), CueRackMeshes.Flat);
            // Leather pads at the back of the notches (where the shafts rest).
            var pad = BoardMeshes.RoundedBox(new Vector3(0.014f, RailT + 0.002f, 0.003f), 0.0012f, 2);
            foreach (float x in holes) kit.Part("NotchPad", root, pad, kit.Leather, new Vector3(x, RailY - RailT * 0.5f, NotchZ - NotchR + 0.001f), Quaternion.identity, false);

            // ---- crown shelf: ball cups on the right, a flat end for chalk, a brass gallery rail
            var ballX = new[] { outer - 0.07f, outer - 0.14f, outer - 0.21f };
            kit.Part("CrownTop", root, CueRackMeshes.HoledTop("rackcrown" + k, -outer, outer, z0, 0.118f, ballX, 0.066f, 0.019f, 0.008f, 0.024f), wood, new Vector3(0f, CrownY, 0f));
            kit.Part("CrownBody", root, BoardMeshes.RoundedBox(new Vector3(outer * 2f - 0.002f, 0.02f, 0.1f), 0.003f, 3), wood, new Vector3(0f, CrownY - 0.024f, 0.067f));
            kit.Part("CrownNose", root, BoardMeshes.RoundedBox(new Vector3(outer * 2f + 0.006f, 0.022f, 0.016f), 0.0079f, 4), trim, new Vector3(0f, CrownY - 0.012f, 0.118f));
            foreach (float x in ballX) kit.Part("BallFelt", root, Disc("rackballfelt", 0.0195f), felt, new Vector3(x, CrownY - 0.0077f, 0.066f), Quaternion.identity, false);
            Gallery(kit, root, outer, CrownY);

            // ---- brackets under both shelves and under the rail
            var corbel = BoardMeshes.Extrude("rackcorbel", CueRackMeshes.CorbelOutline(z0, 0.1f, 0.042f), _ => 0.011f, 0.003f, 3, 4f);
            var small = BoardMeshes.Extrude("rackcorbelsm", CueRackMeshes.CorbelOutline(z0, 0.085f, 0.07f), _ => 0.009f, 0.0025f, 3, 4f);
            foreach (float s in new[] { -sx, sx })
            {
                kit.Part("ShelfBracket", root, corbel, trim, new Vector3(s, ShelfY - 0.0545f, 0f));
                kit.Part("RailBracket", root, small, trim, new Vector3(s, RailY - RailT + 0.001f, 0f));
                kit.Part("CrownBracket", root, small, trim, new Vector3(s, CrownY - 0.033f, 0f));
            }

            // ---- triangle peg (left) on a small oval plaque
            Vector3 peg = TrianglePeg(slots);
            var plaque = BoardMeshes.Extrude("rackplaque", Oval(0.05f, 0.075f), _ => 0.009f, 0.004f, 3, 4f);
            kit.Part("PegPlaque", root, plaque, trim, new Vector3(peg.x, peg.y, 0.009f), CueRackMeshes.Upright);
            kit.Part("Peg", root, Peg(), kit.Brass, new Vector3(peg.x, peg.y - 0.0045f, 0.018f), Quaternion.Euler(90f, 0f, 0f));
            return root;
        }

        static Mesh Upright(string key, List<Vector2> outline) => BoardMeshes.Extrude(key, outline, _ => 0.008f, 0.003f, 3, 4f);

        static List<Vector2> Rect(float x0, float x1, float y0, float y1) => new() { new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1) };

        static List<Vector2> Oval(float rx, float ry)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                o.Add(new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
            }
            return o;
        }

        static Mesh Disc(string key, float r) =>
            BoardMeshes.Lathe(key, new List<Vector2> { new(0f, 0f), new(r, 0f), new(r, 0f), new(r, 0.001f), new(r, 0.001f), new(0f, 0.001f) }, 32, 4f);

        /// <summary>Brass peg along +Y (turned out of the wall): a rose, a shaft and a ball knob.</summary>
        static Mesh Peg()
        {
            var p = new List<Vector2> { new(0f, 0f), new(0.014f, 0f), new(0.014f, 0.003f), new(0.008f, 0.006f), new(0.0045f, 0.01f), new(0.0045f, 0.05f) };
            BoardMeshes.Arc(p, 0f, 0.058f, 0.009f, -60f, 90f, 10);
            return BoardMeshes.Lathe("rackpeg", p, 20, 6f);
        }

        /// <summary>Turned finial on top of an upright: a plinth, a neck, a ball and a small tip.</summary>
        static Mesh Finial()
        {
            var p = new List<Vector2> { new(0f, 0f), new(0.012f, 0f), new(0.0125f, 0.004f), new(0.0105f, 0.008f), new(0.007f, 0.012f), new(0.0065f, 0.016f), new(0.0095f, 0.019f), new(0.0065f, 0.022f) };
            BoardMeshes.Arc(p, 0f, 0.0355f, 0.0145f, -65f, 75f, 12);
            p.Add(new Vector2(0.0025f, 0.054f)); p.Add(new Vector2(0.003f, 0.058f));
            BoardMeshes.Arc(p, 0f, 0.058f, 0.003f, 0f, 90f, 4);
            return BoardMeshes.Lathe("rackfinial", p, 24, 6f);
        }

        static Mesh Rosette()
        {
            var p = new List<Vector2> { new(0f, -0.006f), new(0.022f, -0.006f), new(0.024f, -0.003f), new(0.02f, 0f), new(0.012f, 0.003f), new(0.008f, 0.006f), new(0f, 0.0075f) };
            return BoardMeshes.Lathe("rackrosette", p, 28, 6f);
        }

        /// <summary>Brass gallery along the front of the crown shelf: a rail on small turned posts.</summary>
        static void Gallery(TableKit kit, Transform root, float outer, float y)
        {
            float len = outer * 2f - 0.03f, z = 0.104f, h = 0.03f;
            kit.Part("GalleryRail", root, PoolLampMeshes.Rod(0.003f, len), kit.Brass, new Vector3(-len * 0.5f, y + h, z), Quaternion.Euler(0f, 0f, -90f));
            int posts = Mathf.Max(3, Mathf.RoundToInt(len / 0.14f) + 1);
            var post = PoolLampMeshes.Rod(0.0022f, h);
            var knob = Ball(0.0042f);
            for (int i = 0; i < posts; i++)
            {
                float x = -len * 0.5f + len * i / (posts - 1);
                kit.Part("GalleryPost", root, post, kit.Brass, new Vector3(x, y, z));
                kit.Part("GalleryKnob", root, knob, kit.Brass, new Vector3(x, y + h, z));
            }
        }

        static Mesh Ball(float r)
        {
            var p = new List<Vector2>();
            BoardMeshes.Arc(p, 0f, 0f, r, -90f, 90f, 8);
            return BoardMeshes.Lathe("rackball" + r.ToString("0.0000"), p, 16, 6f);
        }
    }
}
