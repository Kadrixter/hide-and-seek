using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Sideboard with a record player (south wall): plinth, platter, record with a label, tone arm on a pivot
    /// (<see cref="LobbyRecordPlayer"/>), records in the open shelf, two small speakers. The aquarium: LobbyAquariumTank.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        // ================================================================ sideboard with the record player (south wall)

        private static void RecordCabinet(Transform parent, Vector3 pos, Material darkWood)
        {
            var t = Frame(parent, "RecordCabinet", pos, 0f);
            var teak = Mat("LobbyTeak", new Color(0.62f, 0.4f, 0.24f), Tex("lobby_wood", TexKind.Wood), 0.45f, new Vector2(1f, 0.5f));
            var black = Mat("LobbyFireBlack", new Color(0.04f, 0.04f, 0.045f), null, 0.45f, Vector2.one, 0.4f);
            const float w = 1.0f, d = 0.45f, y0 = 0.16f, h = 0.44f;
            RBox(t, "Body", new Vector3(0f, y0 + h * 0.5f, 0f), new Vector3(w, h, d), teak, 0.012f);
            LocalBox(t, "ShelfHole", new Vector3(-0.24f, y0 + h * 0.5f, d * 0.5f - 0.16f), new Vector3(0.44f, h - 0.06f, 0.33f), Mat("LobbyCabinetInside", new Color(0.18f, 0.11f, 0.07f), null, 0.2f, Vector2.one));
            RBox(t, "Door", new Vector3(0.25f, y0 + h * 0.5f, d * 0.5f + 0.006f), new Vector3(0.46f, h - 0.04f, 0.012f), teak, 0.005f);
            RBox(t, "Pull", new Vector3(0.06f, y0 + h * 0.5f, d * 0.5f + 0.016f), new Vector3(0.014f, 0.14f, 0.012f), Brass, 0.005f);
            foreach (var (x, z) in new[] { (-0.44f, -0.17f), (0.44f, -0.17f), (-0.44f, 0.17f), (0.44f, 0.17f) })
                MeshObj("Leg", t, LatheSaved("SideboardLeg", new[] { new Vector2(0.012f, 0f), new Vector2(0.022f, y0) }, 12, true, true), teak, new Vector3(x, 0f, z), Quaternion.Euler(z > 0 ? 6f : -6f, 0f, x > 0 ? -6f : 6f));
            // records standing in the open shelf
            var rnd = new System.Random(9);
            float rx = -0.44f;
            int sleeve = 0;
            while (rx < -0.04f)
            {
                float thick = 0.006f + (float)rnd.NextDouble() * 0.006f;
                var c = Color.HSVToRGB((float)rnd.NextDouble(), 0.45f + 0.3f * (float)rnd.NextDouble(), 0.35f + 0.45f * (float)rnd.NextDouble());
                LocalBox(t, "Record", new Vector3(rx + thick * 0.5f, y0 + 0.03f + 0.155f, 0.03f), new Vector3(thick, 0.31f, 0.31f), Mat("LobbySleeve" + (sleeve++ % 12), c, null, 0.3f, Vector2.one))
                    .transform.localRotation = Quaternion.Euler(0f, 0f, rx > -0.12f ? -8f : 0f);
                rx += thick + 0.002f;
            }
            // speakers
            foreach (float sx in new[] { -0.4f, -0.16f })
            {
                RBox(t, "Speaker", new Vector3(sx, y0 + h + 0.12f, -0.03f), new Vector3(0.15f, 0.24f, 0.17f), teak, 0.01f);
                MeshObj("Cone", t, LatheSaved("SpeakerCone", new[] { new Vector2(0.05f, 0f), new Vector2(0.045f, 0.004f), new Vector2(0.015f, -0.01f), new Vector2(0f, -0.008f) }, 24, false, false),
                    black, new Vector3(sx, y0 + h + 0.1f, 0.056f), Quaternion.Euler(-90f, 0f, 0f));
            }
            // turntable
            var tt = new GameObject("Turntable").transform;
            tt.SetParent(t, false);
            tt.localPosition = new Vector3(0.22f, y0 + h, 0f);
            RBox(tt, "Plinth", new Vector3(0f, 0.045f, 0f), new Vector3(0.46f, 0.09f, 0.36f), Mat("LobbyWalnut", new Color(0.4f, 0.26f, 0.16f), Tex("lobby_wood", TexKind.Wood), 0.6f, new Vector2(0.5f, 0.5f)), 0.012f);
            var platter = new GameObject("Platter").transform;
            platter.SetParent(tt, false);
            platter.localPosition = new Vector3(-0.04f, 0.09f, 0f);
            MeshObj("Disc", platter, LatheSaved("Platter", new[] { new Vector2(0.15f, 0f), new Vector2(0.15f, 0.018f) }, 48, false, true), metalSilver.Value, Vector3.zero);
            var vinyl = Mat("LobbyVinyl", Color.white, SmoothTex("lobby2_vinyl", 256, 256, (x, y) =>
            {
                float u = x / 128f - 1f, v = y / 128f - 1f, r = Mathf.Sqrt(u * u + v * v);
                if (r < 0.33f) return r < 0.03f ? Color.black : Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.95f, 0.85f, 0.6f), r > 0.3f ? 1f : 0f);
                float groove = 0.06f + 0.05f * Mathf.Sin(r * 380f) + (Mathf.Abs(r - 0.6f) < 0.006f ? -0.04f : 0f);
                return new Color(groove, groove, groove);
            }), 0.85f, Vector2.one);
            QuadAt("Record", platter, platter.TransformPoint(new Vector3(0f, 0.0195f, 0f)), Quaternion.Euler(90f, 0f, 0f), new Vector2(0.298f, 0.298f), vinyl).transform.SetParent(platter, true);
            Cyl("Spindle", platter, platter.TransformPoint(new Vector3(0f, 0.03f, 0f)), 0.008f, 0.025f, metalSilver.Value);
            // tone arm: pivot at the back right, arm along +Z (rest beside the platter)
            var arm = new GameObject("ToneArm").transform;
            arm.SetParent(tt, false);
            arm.localPosition = new Vector3(0.17f, 0.13f, -0.11f);
            MeshObj("ArmBase", tt, LatheSaved("ArmBase", new[] { new Vector2(0.025f, 0f), new Vector2(0.022f, 0.03f), new Vector2(0.012f, 0.04f) }, 20, false, true), metalSilver.Value, new Vector3(0.17f, 0.09f, -0.11f));
            LocalBox(arm, "Tube", new Vector3(0f, 0f, 0.115f), new Vector3(0.007f, 0.007f, 0.23f), metalSilver.Value);
            LocalBox(arm, "Counterweight", new Vector3(0f, 0f, -0.035f), new Vector3(0.026f, 0.026f, 0.04f), black);
            LocalBox(arm, "Headshell", new Vector3(-0.006f, -0.008f, 0.235f), new Vector3(0.018f, 0.01f, 0.04f), black);
            LocalBox(arm, "Rest", new Vector3(0.02f, -0.02f, 0.16f), new Vector3(0.012f, 0.03f, 0.012f), black);
            var rp = tt.gameObject.AddComponent<LobbyRecordPlayer>();
            rp.platter = platter;
            rp.arm = arm;
            rp.armRestYaw = 0f;
            rp.armPlayYaw = -28f;
            BoxCollider(t, new Vector3(0f, 0.35f, 0f), new Vector3(w, 0.7f, d));
            AddProp(tt.gameObject, LobbyPropKind.RecordPlayer, new Vector3(0f, 0.12f, 0.05f), 1.9f);
        }
    }
}
