using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// The passage from the lobby arch to the drift track (<see cref="DriftTrackLayout"/>): brick walls, a concrete
    /// ceiling, an asphalt floor, a cage lamp and the roller shutter at the track end
    /// (closed; <see cref="DriftTrack"/> lifts it in the real lobby). The asphalt material is saved here with its
    /// normal / gloss / detail keywords and small placeholder maps, so the player build keeps those Standard shader
    /// variants; the runtime swaps in the generated maps for the graphics profile (<see cref="AsphaltMaps"/>).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        public const string TrackAsphaltPath = LobbyArtDir + "/TrackAsphalt.mat";

        private static Material TrackAsphaltMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(TrackAsphaltPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, TrackAsphaltPath);
            }
            m.color = Color.white;
            m.mainTexture = SmoothTex("track_asphalt_ph", 64, 64, (x, y) => new Color(0.22f, 0.22f, 0.23f, 1f));
            m.SetTexture("_BumpMap", SmoothTex("track_asphalt_ph_n", 64, 64, (x, y) => new Color(0.5f, 0.5f, 1f, 1f), true));
            m.SetTexture("_MetallicGlossMap", SmoothTex("track_asphalt_ph_g", 64, 64, (x, y) => new Color(0f, 0f, 0f, 0.25f)));
            m.SetTexture("_DetailAlbedoMap", SmoothTex("track_asphalt_ph_d", 64, 64, (x, y) => new Color(0.5f, 0.5f, 0.5f, 1f)));
            m.SetTexture("_DetailNormalMap", SmoothTex("track_asphalt_ph_dn", 64, 64, (x, y) => new Color(0.5f, 0.5f, 1f, 1f), true));
            m.SetFloat("_BumpScale", 1f);
            m.SetFloat("_GlossMapScale", 1f);
            m.SetFloat("_DetailNormalMapScale", 0.6f);
            m.SetFloat("_Metallic", 0f);
            m.mainTextureScale = Vector2.one;
            m.EnableKeyword("_NORMALMAP");
            m.EnableKeyword("_METALLICGLOSSMAP");
            m.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void BuildPassage(Transform root, Material brick, Material concrete, Material steel)
        {
            var p = new GameObject("TrackPassage").transform;
            p.SetParent(root, false);
            float x0 = DriftTrackLayout.PassageStart, x1 = DriftTrackLayout.PadEast, z = DriftTrackLayout.ArchZ;
            float len = x0 - x1, w = DriftTrackLayout.PassageWidth, h = DriftTrackLayout.PassageHeight, xm = (x0 + x1) * 0.5f;

            // Floor: world-mapped quad (6 m per texture repeat, like the track) on a box collider.
            var fm = new ProcMesh();
            fm.At(Vector3.zero).Box(new Vector3(xm, -0.05f, z), new Vector3(len, 0.1f, w + 0.4f));
            var floorMesh = fm.ToMesh("TrackPassageFloor");
            var uvs = new Vector2[floorMesh.vertexCount];
            var vs = floorMesh.vertices;
            for (int i = 0; i < vs.Length; i++) uvs[i] = new Vector2(vs[i].x / 6f, vs[i].z / 6f);
            floorMesh.uv = uvs;
            floorMesh.RecalculateTangents();
            var floor = new GameObject("TrackPassageFloor");
            floor.transform.SetParent(p, false);
            floor.AddComponent<MeshFilter>().sharedMesh = SaveMesh(floorMesh, "TrackPassageFloor");
            floor.AddComponent<MeshRenderer>().sharedMaterial = TrackAsphaltMaterial();
            var bc = floor.AddComponent<BoxCollider>();
            bc.center = new Vector3(xm, -0.05f, z);
            bc.size = new Vector3(len, 0.1f, w + 0.4f);
            floor.AddComponent<HideAndSeek.Player.FootstepSurface>().Type = HideAndSeek.Player.SurfaceType.Concrete;
            floor.isStatic = true;

            for (int s = -1; s <= 1; s += 2)
                Box("PassageWall", p, new Vector3(xm, h * 0.5f, z + s * (w * 0.5f + 0.1f)), new Vector3(len, h, 0.2f), brick);
            Box("PassageCeiling", p, new Vector3(xm, h + 0.1f, z), new Vector3(len, 0.2f, w + 0.4f), concrete);
            // Lintel over the shutter (the track's wall goes on above it at runtime).
            Box("PassageLintel", p, new Vector3(x1 - 0.15f, h + 0.25f, z), new Vector3(0.3f, 0.5f, w + 0.4f), concrete);

            // Cage lamp on the ceiling.
            var lampMat = Mat("TrackLampGlass", new Color(1f, 0.9f, 0.7f), null, 0.6f, Vector2.one);
            lampMat.EnableKeyword("_EMISSION");
            lampMat.SetColor("_EmissionColor", new Color(1.6f, 1.25f, 0.8f));
            EditorUtility.SetDirty(lampMat);
            Deco("PassageLampBase", p, new Vector3(xm, h - 0.03f, z), new Vector3(0.22f, 0.05f, 0.22f), steel);
            Deco("PassageLampGlass", p, new Vector3(xm, h - 0.13f, z), new Vector3(0.14f, 0.16f, 0.14f), lampMat);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Deco("PassageLampCage", p, new Vector3(xm + Mathf.Cos(a) * 0.08f, h - 0.13f, z + Mathf.Sin(a) * 0.08f), new Vector3(0.012f, 0.18f, 0.012f), steel);
            }
            var l = new GameObject("PassageLight").AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.position = new Vector3(xm, h - 0.3f, z);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.82f, 0.6f);
            l.range = 5f;
            l.intensity = 1.3f;
            l.shadows = LightShadows.None;

            // Roller shutter (closed) in a steel frame at the track end.
            var shutterMat = Mat("TrackShutter", new Color(0.5f, 0.52f, 0.55f), Tex("track_concrete", TexKind.Concrete), 0.35f, new Vector2(1f, 6f), 0.4f);
            var sh = Box("TrackShutter", p, new Vector3(x1 + 0.06f, h * 0.5f, z), new Vector3(0.05f, h, w), shutterMat);
            sh.isStatic = false; // lifted at runtime
            for (int i = 1; i < 12; i++)
            {
                var rib = Deco("ShutterRib", p, new Vector3(x1 + 0.03f, i * h / 12f, z), new Vector3(0.02f, 0.025f, w - 0.02f), steel);
                rib.transform.SetParent(sh.transform, true);
            }
            for (int s = -1; s <= 1; s += 2)
                Box("ShutterRail", p, new Vector3(x1 + 0.06f, h * 0.5f, z + s * (w * 0.5f - 0.04f)), new Vector3(0.12f, h, 0.08f), steel);
        }
    }
}
