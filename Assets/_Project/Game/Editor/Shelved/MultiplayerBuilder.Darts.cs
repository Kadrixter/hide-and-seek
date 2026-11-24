using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// SHELVED (2026-11-24): dartboard with a throw line (<see cref="LobbyDarts"/>), taken out of the lobby on the
    /// user's request but kept for later. Runtime code: Game/Shelved/Darts (LobbyDarts, DartboardArt); the prop kind
    /// <see cref="LobbyPropKind.Darts"/>, the throw reach animation and the network handling stay where they were.
    /// To bring it back: call <c>Dartboard(f, darkWood);</c> again in MultiplayerBuilder.LobbyFurniture (after
    /// CoatRack) and rebuild the scenes.
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void Dartboard(Transform parent, Material darkWood)
        {
            // east wall, facing -X; board root at the wall surface, local +Z = into the room
            var t = Frame(parent, "Dartboard", LobbyLayout.Dartboard, -90f);
            RBox(t, "Backboard", new Vector3(0f, 0f, 0.01f), new Vector3(0.64f, 0.64f, 0.02f), darkWood, 0.012f);
            var surround = Mat("LobbyDartSurround", new Color(0.06f, 0.06f, 0.06f), null, 0.2f, Vector2.one);
            MeshObj("Board", t, LatheSaved("DartBoardBody", new[] { new Vector2(LobbyDarts.BoardR, 0f), new Vector2(LobbyDarts.BoardR + 0.004f, 0.006f), new Vector2(LobbyDarts.BoardR + 0.004f, 0.018f), new Vector2(LobbyDarts.BoardR, 0.024f) }, 64, false, true),
                surround, new Vector3(0f, 0f, 0.02f), Quaternion.Euler(90f, 0f, 0f));
            var face = QuadAt("Face", t, t.TransformPoint(new Vector3(0f, 0f, LobbyDarts.FaceZ)), t.rotation * Quaternion.Euler(0f, 180f, 0f), Vector2.one * LobbyDarts.BoardR * 2f,
                Mat("LobbyDartFace", Color.white, null, 0.15f, Vector2.one));
            var faceMat = face.GetComponent<MeshRenderer>().sharedMaterial;
            faceMat.SetFloat("_Mode", 1f);
            faceMat.EnableKeyword("_ALPHATEST_ON");
            faceMat.SetFloat("_Cutoff", 0.5f);
            faceMat.renderQueue = 2450;
            // small brass lamp above lighting the board
            MeshObj("LampArm", t, LatheSaved("DartLampArm", new[] { new Vector2(0.008f, 0f), new Vector2(0.008f, 0.22f) }, 8, false, true), Brass, new Vector3(0f, 0.42f, 0f), Quaternion.Euler(90f, 0f, 0f));
            MeshObj("LampHood", t, ShadeMesh("DartLampHood", 0.06f, 0.025f, 0.07f), Brass, new Vector3(0f, 0.42f, 0.24f), Quaternion.Euler(150f, 0f, 0f));
            var spot = new GameObject("DartLight").AddComponent<Light>();
            spot.transform.SetParent(t, false);
            spot.transform.localPosition = new Vector3(0f, 0.38f, 0.26f);
            spot.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.38f, -0.2f));
            spot.type = LightType.Spot;
            spot.spotAngle = 70f;
            spot.range = 1.6f;
            spot.intensity = 1.6f;
            spot.color = new Color(1f, 0.88f, 0.7f);
            t.gameObject.AddComponent<LobbyDarts>();
            AddProp(t.gameObject, LobbyPropKind.Darts, new Vector3(0f, 0f, 0.06f), 3.4f);
            // throw line (oche) on the floor
            Vector3 line = LobbyDarts.ThrowLine;
            Deco("Oche", parent, new Vector3(line.x, 0.003f, line.z), new Vector3(0.05f, 0.006f, 0.7f), Mat("LobbyOche", new Color(0.75f, 0.6f, 0.25f), null, 0.6f, Vector2.one, 0.6f));
        }
    }
}
