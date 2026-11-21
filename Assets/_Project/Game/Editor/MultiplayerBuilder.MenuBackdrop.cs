using HideAndSeek.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Main menu backdrop: dark dungeon room with a wet tiled floor, cold top spotlight with a visible beam and dust,
    /// brick walls, a doorway to a red-lit corridor with a shadow figure (glowing red eyes), a red wall lantern,
    /// crates, a covered table and hanging cables.
    /// Character stands at (0.9, 0, 0.6); the camera looks at it from (-0.6, 1.35, -3.4).
    /// </summary>
    public static partial class MultiplayerBuilder
    {
        private static void BuildMenuBackdrop(Transform character)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.055f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.055f;
            RenderSettings.fogColor = new Color(0.01f, 0.012f, 0.018f);

            var root = new GameObject("Backdrop").transform;

            // ---- materials
            var tiles = Mat("MenuTiles", new Color(0.42f, 0.44f, 0.48f), Tex("menu_tiles", TexKind.Tiles), 0.55f, new Vector2(10, 10));
            var bricks = Mat("MenuBricks", new Color(0.46f, 0.42f, 0.42f), Tex("menu_bricks", TexKind.Bricks), 0.25f, new Vector2(6, 2.2f));
            var bricksSide = Mat("MenuBricksSide", new Color(0.46f, 0.42f, 0.42f), Tex("menu_bricks", TexKind.Bricks), 0.25f, new Vector2(3, 2.2f));
            var stone = Mat("MenuStone", new Color(0.2f, 0.2f, 0.22f), Tex("menu_concrete", TexKind.Concrete), 0.3f, new Vector2(1, 2));
            var crate = Mat("MenuCrate", new Color(0.72f, 0.62f, 0.55f), Tex("menu_crate", TexKind.Crate), 0.18f, Vector2.one);
            var cloth = Mat("MenuCloth", new Color(0.16f, 0.13f, 0.17f), Tex("menu_cloth", TexKind.Fabric), 0.1f, new Vector2(3, 3));
            var dark = Mat("MenuShadowFigure", new Color(0.012f, 0.012f, 0.014f), null, 0.05f, Vector2.one);
            var cable = Mat("MenuCable", new Color(0.03f, 0.03f, 0.035f), null, 0.5f, Vector2.one);
            var metal = Mat("MenuMetal", new Color(0.08f, 0.08f, 0.085f), null, 0.6f, Vector2.one, 0.7f);
            var redGlass = Mat("MenuRedGlass", new Color(1f, 0.2f, 0.12f), null, 0.9f, Vector2.one);
            redGlass.EnableKeyword("_EMISSION");
            redGlass.SetColor("_EmissionColor", new Color(1f, 0.12f, 0.06f) * 3f);
            var eyeMat = Mat("MenuEyes", new Color(1f, 0.1f, 0.05f), null, 0.5f, Vector2.one);
            eyeMat.EnableKeyword("_EMISSION");
            eyeMat.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.02f) * 6f);

            // ---- room: floor, back wall with a doorway, side walls, ceiling
            // Floor on its own layer: the red lights skip it (their glossy highlights looked like red blobs).
            Box("Floor", root, new Vector3(0, -0.05f, 2f), new Vector3(24, 0.1f, 24), tiles).layer = MenuFloorLayer;
            const float wallZ = 4.2f, doorX0 = 3.35f, doorX1 = 4.55f, doorH = 2.5f;
            float doorC = (doorX0 + doorX1) / 2f;
            Box("BackWallL", root, new Vector3((-8f + doorX0) / 2f, 3f, wallZ), new Vector3(doorX0 + 8f, 6f, 0.4f), bricks);
            Box("BackWallR", root, new Vector3((doorX1 + 9f) / 2f, 3f, wallZ), new Vector3(9f - doorX1, 6f, 0.4f), bricks);
            Box("Lintel", root, new Vector3((doorX0 + doorX1) / 2f, (doorH + 6f) / 2f, wallZ), new Vector3(doorX1 - doorX0, 6f - doorH, 0.4f), bricks);
            Box("DoorFrameL", root, new Vector3(doorX0 - 0.1f, doorH / 2f, wallZ - 0.25f), new Vector3(0.22f, doorH + 0.2f, 0.18f), stone);
            Box("DoorFrameR", root, new Vector3(doorX1 + 0.1f, doorH / 2f, wallZ - 0.25f), new Vector3(0.22f, doorH + 0.2f, 0.18f), stone);
            Box("DoorFrameTop", root, new Vector3((doorX0 + doorX1) / 2f, doorH + 0.1f, wallZ - 0.25f), new Vector3(doorX1 - doorX0 + 0.42f, 0.22f, 0.18f), stone);
            Box("RightWall", root, new Vector3(6.4f, 3f, 0.5f), new Vector3(0.4f, 6f, 8f), bricksSide);
            Box("LeftWall", root, new Vector3(-3.6f, 3f, 0.5f), new Vector3(0.4f, 6f, 8f), bricksSide);
            Box("Ceiling", root, new Vector3(0, 4.6f, 1f), new Vector3(24, 0.2f, 12), stone);
            Box("Beam", root, new Vector3(0.5f, 4.35f, 2.8f), new Vector3(10f, 0.3f, 0.35f), metal);

            // corridor behind the door, lit red
            Box("CorrFloor", root, new Vector3(doorC, -0.05f, 7.2f), new Vector3(1.4f, 0.1f, 6f), tiles);
            Box("CorrWallL", root, new Vector3(doorX0 - 0.2f, 1.6f, 7.2f), new Vector3(0.3f, 3.2f, 6f), bricksSide);
            Box("CorrWallR", root, new Vector3(doorX1 + 0.2f, 1.6f, 7.2f), new Vector3(0.3f, 3.2f, 6f), bricksSide);
            Box("CorrEnd", root, new Vector3(doorC, 1.6f, 10.1f), new Vector3(1.8f, 3.2f, 0.3f), bricks);
            Box("CorrCeil", root, new Vector3(doorC, 3.1f, 7.2f), new Vector3(1.8f, 0.2f, 6f), stone);
            var corrLight = AddLight("CorridorRed", root, new Vector3(doorC, 2.3f, 8.2f), LightType.Point, new Color(1f, 0.08f, 0.04f), 3.2f, 7f);

            // shadow figure in the corridor (blocky silhouette), facing the room
            var fig = new GameObject("ShadowFigure").transform;
            fig.SetParent(root, false);
            fig.position = new Vector3(doorC, 0f, 5.5f);
            Box("Legs", fig, fig.position + new Vector3(0, 0.38f, 0), new Vector3(0.46f, 0.76f, 0.24f), dark);
            Box("Body", fig, fig.position + new Vector3(0, 1.1f, 0), new Vector3(0.5f, 0.72f, 0.26f), dark);
            Box("ArmL", fig, fig.position + new Vector3(-0.36f, 1.07f, 0), new Vector3(0.22f, 0.74f, 0.24f), dark);
            Box("ArmR", fig, fig.position + new Vector3(0.36f, 1.07f, 0), new Vector3(0.22f, 0.74f, 0.24f), dark);
            Box("Head", fig, fig.position + new Vector3(0, 1.7f, 0), new Vector3(0.46f, 0.46f, 0.46f), dark);
            var eyeL = Box("EyeL", fig, fig.position + new Vector3(-0.11f, 1.73f, -0.235f), new Vector3(0.11f, 0.055f, 0.02f), eyeMat);
            var eyeR = Box("EyeR", fig, fig.position + new Vector3(0.11f, 1.73f, -0.235f), new Vector3(0.11f, 0.055f, 0.02f), eyeMat);
            corrLight.cullingMask = ~(1 << MenuFloorLayer);
            var eyeGlow = AddLight("EyeGlow", fig, fig.position + new Vector3(0, 1.73f, -0.5f), LightType.Point, new Color(1f, 0.05f, 0.02f), 1.2f, 1.6f);

            // red lantern on the back wall, right of the door
            var lantern = new GameObject("Lantern").transform;
            lantern.SetParent(root, false);
            lantern.position = new Vector3(5.2f, 2.35f, wallZ - 0.32f);
            Box("Bracket", lantern, lantern.position + new Vector3(0, 0.36f, 0.12f), new Vector3(0.05f, 0.05f, 0.3f), metal);
            Box("Cap", lantern, lantern.position + new Vector3(0, 0.27f, 0), new Vector3(0.26f, 0.06f, 0.26f), metal);
            Box("Glass", lantern, lantern.position, new Vector3(0.2f, 0.42f, 0.2f), redGlass);
            Box("Bottom", lantern, lantern.position + new Vector3(0, -0.24f, 0), new Vector3(0.24f, 0.06f, 0.24f), metal);
            foreach (var cx in new[] { -0.105f, 0.105f })
            foreach (var cz in new[] { -0.105f, 0.105f })
                Box("Bar", lantern, lantern.position + new Vector3(cx, 0, cz), new Vector3(0.025f, 0.44f, 0.025f), metal);
            var lanternLight = AddLight("LanternLight", root, lantern.position + new Vector3(0, 0, -0.35f), LightType.Point, new Color(1f, 0.12f, 0.06f), 2.6f, 7.5f);
            lanternLight.shadows = LightShadows.Soft;
            lanternLight.shadowStrength = 0.7f;
            lanternLight.cullingMask = ~(1 << MenuFloorLayer);

            // crates, covered table, foreground crates
            void Crate(string n, Vector3 p, float s, float yaw)
            {
                var c = Box(n, root, p + Vector3.up * s / 2f, Vector3.one * s, crate);
                c.transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
            Crate("CrateL1", new Vector3(-1.9f, 0, 3.2f), 0.9f, 8f);
            Crate("CrateL2", new Vector3(-1.75f, 0.9f, 3.25f), 0.7f, -12f);
            Crate("CrateL3", new Vector3(-0.85f, 0, 3.4f), 0.75f, 20f);
            Crate("CrateR1", new Vector3(5.4f, 0, 2.2f), 0.8f, -15f);
            Crate("CrateR2", new Vector3(2.3f, 0, 3.5f), 0.6f, 35f);
            Box("TableTop", root, new Vector3(-1.1f, 0.72f, 2.0f), new Vector3(1.1f, 0.06f, 0.75f), crate);
            var clothTop = Box("Cloth", root, new Vector3(-1.1f, 0.5f, 2.0f), new Vector3(1.2f, 0.5f, 0.85f), cloth);
            clothTop.transform.rotation = Quaternion.Euler(0, 6f, 0);
            Box("ClothDrape", root, new Vector3(-1.05f, 0.25f, 1.6f), new Vector3(1.0f, 0.5f, 0.05f), cloth).transform.rotation = Quaternion.Euler(-8f, 6f, 0);

            // hanging cables (top left)
            for (int i = 0; i < 3; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                c.name = "Cable";
                c.transform.SetParent(root, false);
                c.transform.position = new Vector3(-0.6f + i * 0.35f, 3.7f - i * 0.15f, 2.4f + i * 0.2f);
                c.transform.localScale = new Vector3(0.035f, 1.3f + i * 0.2f, 0.035f);
                c.transform.rotation = Quaternion.Euler(10f * i - 8f, 0, 60f + i * 18f);
                c.GetComponent<MeshRenderer>().sharedMaterial = cable;
                Object.DestroyImmediate(c.GetComponent<Collider>());
            }

            // ---- lights on the character
            var spot = AddLight("TopSpot", root, new Vector3(0.75f, 4.3f, 0.15f), LightType.Spot, new Color(0.62f, 0.78f, 1f), 3.4f, 9f);
            spot.transform.LookAt(character.position + new Vector3(0, 0.2f, 0.1f));
            spot.spotAngle = 44f;
            spot.innerSpotAngle = 20f;
            spot.shadows = LightShadows.Soft;
            spot.shadowResolution = LightShadowResolution.VeryHigh;
            spot.shadowStrength = 0.85f;
            spot.shadowBias = 0.03f;
            spot.shadowNormalBias = 0.3f;
            spot.shadowNearPlane = 0.6f;

            // soft frontal key (no shadows) so the face is readable
            AddLight("FrontKey", root, new Vector3(-0.4f, 1.9f, -1.6f), LightType.Point, new Color(0.75f, 0.82f, 1f), 1.1f, 5f);
            // red rim from the lantern side, green-ish fill from the left (like the reference)
            AddLight("RimRed", root, new Vector3(2.4f, 1.5f, 2.0f), LightType.Point, new Color(1f, 0.15f, 0.08f), 1.4f, 4f).cullingMask = ~(1 << MenuFloorLayer);
            AddLight("FillGreen", root, new Vector3(-1.2f, 1.2f, 1.6f), LightType.Point, new Color(0.25f, 0.9f, 0.45f), 0.35f, 3f);

            // visible beam cone (flashlight beam shader / mesh)
            var beamMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Player/Generated/FlashlightBeamMesh.asset");
            var beamShader = Shader.Find("HideAndSeek/FlashlightBeam");
            MeshRenderer beamR = null;
            if (beamMesh != null && beamShader != null)
            {
                string bp = $"{LobbyArtDir}/MenuBeam.mat";
                var bm = AssetDatabase.LoadAssetAtPath<Material>(bp);
                if (bm == null)
                {
                    bm = new Material(beamShader);
                    AssetDatabase.CreateAsset(bm, bp);
                }
                bm.SetColor("_Color", new Color(0.55f, 0.72f, 1f));
                bm.SetFloat("_Intensity", 1f);
                bm.SetFloat("_Strength", 0.13f);
                bm.SetFloat("_LengthFalloff", 0.8f);
                bm.SetFloat("_EdgePower", 1.3f);
                bm.SetFloat("_SoftDepth", 0.6f);
                bm.SetFloat("_NearFadeStart", 0.2f);
                bm.SetFloat("_NearFadeRange", 1.0f);
                EditorUtility.SetDirty(bm);
                var beam = new GameObject("SpotBeam");
                beam.transform.SetParent(spot.transform, false);
                // Mesh: 13 m long along +Z, 58 degrees wide; scaled to a narrower ~4.6 m shaft inside the light cone.
                beam.transform.localScale = new Vector3(0.19f, 0.19f, 4.6f / 13f); // mesh is 58 deg / 13 m -> ~17 deg shaft
                beam.AddComponent<MeshFilter>().sharedMesh = beamMesh;
                beamR = beam.AddComponent<MeshRenderer>();
                beamR.sharedMaterial = bm;
                beamR.shadowCastingMode = ShadowCastingMode.Off;
                beamR.receiveShadows = false;
            }

            // dust motes floating in the beam
            var dustGo = new GameObject("Dust");
            dustGo.transform.SetParent(root, false);
            dustGo.transform.position = new Vector3(0.85f, 2.2f, 0.4f);
            var ps = dustGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0.02f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.022f);
            main.startColor = new Color(0.75f, 0.85f, 1f, 0.35f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 40f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(1.1f, 4.0f, 1.1f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.3f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var psr = dustGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            psr.shadowCastingMode = ShadowCastingMode.Off;

            // reflections for the wet floor (rendered once at runtime)
            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(root, false);
            probeGo.transform.position = new Vector3(1f, 1.2f, 1f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(14f, 6f, 14f);
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.hdr = true;
            probe.intensity = 0.55f;
            probe.cullingMask = ~(1 << MenuFloorLayer);

            // runtime life: eye blink, lantern flicker, probe render
            var amb = root.gameObject.AddComponent<MenuAmbience>();
            var so = new SerializedObject(amb);
            so.FindProperty("eyes").arraySize = 2;
            so.FindProperty("eyes").GetArrayElementAtIndex(0).objectReferenceValue = eyeL.GetComponent<Renderer>();
            so.FindProperty("eyes").GetArrayElementAtIndex(1).objectReferenceValue = eyeR.GetComponent<Renderer>();
            so.FindProperty("eyeGlow").objectReferenceValue = eyeGlow;
            so.FindProperty("lantern").objectReferenceValue = lanternLight;
            so.FindProperty("corridor").objectReferenceValue = corrLight;
            so.FindProperty("probe").objectReferenceValue = probe;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private const int MenuFloorLayer = 10;

        private static Light AddLight(string name, Transform parent, Vector3 pos, LightType type, Color color, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
