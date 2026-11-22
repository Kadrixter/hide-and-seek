using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Big double start lever (knife-switch style) built at runtime under the scene's "StartLever" root.
    /// Local space: +Z out of the wall into the room, +Y up, origin on the floor at the wall.
    /// Steel wall plate with a hazard border, two bearing blocks and an axle, two parallel steel arms with copper
    /// blades tied together and joined at the end by a horizontal red bar, upper (STOP) and lower (START) copper
    /// jaws on ceramic posts, a lock-out solenoid whose bolt goes through the right arm while not everyone is
    /// ready, a junction box with a caged status lamp, conduits and label plates.
    /// </summary>
    public sealed class StartLeverModel
    {
        public const float ArmLength = 0.62f, ArmX = 0.31f, JawS = 0.38f;
        public const float UpAngle = 35f, DownAngle = 145f;
        public static readonly Vector3 PivotPos = new Vector3(0f, 1.12f, 0.18f);
        /// <summary>Lock bolt centre (x) when shot through the arm / pulled back into the solenoid.</summary>
        public const float BoltIn = 0.33f, BoltOut = 0.44f;

        public Transform Root, Pivot, Bar, Bolt;
        public Renderer Lamp;
        public Light Glow;
        /// <summary>Points where the blades meet the lower jaws (local), for the sparks.</summary>
        public Vector3[] LowerJaws = new Vector3[2];

        Material steel, plate, darkSteel, copper, red, black, ceramic, yellow, hazard, cable, lampGlass;
        int layer;

        /// <summary>Point at distance <paramref name="s"/> along the arms at throw angle <paramref name="deg"/> (local).</summary>
        public static Vector3 ArmPoint(float deg, float s, float x = 0f)
        {
            float a = deg * Mathf.Deg2Rad;
            return PivotPos + new Vector3(x, Mathf.Cos(a) * s, Mathf.Sin(a) * s);
        }

        public static StartLeverModel Build(Transform root, Material template)
        {
            var m = new StartLeverModel { Root = root, layer = root.gameObject.layer };
            m.Materials(template);
            m.BuildPlate();
            m.BuildBearings();
            m.BuildArms();
            m.BuildJaws(UpAngle, "Upper");
            m.BuildJaws(DownAngle, "Lower");
            m.BuildLock();
            m.BuildTop();
            m.BuildLabels();
            return m;
        }

        // ================================================================ materials

        static Material Mat(Material t, Color c, float metallic, float gloss, Texture tex = null)
        {
            var m = new Material(t) { color = c, mainTexture = tex, mainTextureScale = Vector2.one };
            m.DisableKeyword("_EMISSION");
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        void Materials(Material t)
        {
            plate = Mat(t, new Color(0.2f, 0.215f, 0.23f), 0.55f, 0.42f);
            darkSteel = Mat(t, new Color(0.1f, 0.105f, 0.115f), 0.6f, 0.5f);
            steel = Mat(t, new Color(0.62f, 0.64f, 0.67f), 0.9f, 0.7f);
            copper = Mat(t, new Color(0.86f, 0.47f, 0.26f), 0.95f, 0.78f);
            red = Mat(t, new Color(0.78f, 0.05f, 0.04f), 0.15f, 0.82f);
            black = Mat(t, new Color(0.03f, 0.03f, 0.035f), 0.05f, 0.35f);
            ceramic = Mat(t, new Color(0.9f, 0.88f, 0.82f), 0f, 0.88f);
            yellow = Mat(t, new Color(0.95f, 0.7f, 0.07f), 0.2f, 0.55f);
            hazard = Mat(t, Color.white, 0.15f, 0.45f, StartLeverArt.Hazard);
            cable = Mat(t, new Color(0.04f, 0.04f, 0.045f), 0f, 0.6f);
            lampGlass = Mat(t, new Color(1f, 0.2f, 0.15f), 0f, 0.9f);
            lampGlass.EnableKeyword("_EMISSION");
            lampGlass.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.15f));
            lampGlass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        // ================================================================ helpers

        Transform Rb(string n, Vector3 centre, Vector3 size, float radius, Material mat, Transform parent = null, Quaternion? rot = null)
        {
            var g = new GameObject(n) { layer = layer };
            g.transform.SetParent(parent != null ? parent : Root, false);
            g.transform.localPosition = centre;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.AddComponent<MeshFilter>().sharedMesh = VendingMeshes.RoundedBox(size, radius);
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g.transform;
        }

        /// <summary>Cylinder along local <paramref name="axis"/> (0 = X, 1 = Y, 2 = Z).</summary>
        Transform Cyl(string n, Vector3 centre, float diameter, float length, int axis, Material mat, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = n;
            g.layer = layer;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent != null ? parent : Root, false);
            g.transform.localPosition = centre;
            g.transform.localRotation = axis == 0 ? Quaternion.Euler(0f, 0f, 90f) : axis == 2 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            g.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return g.transform;
        }

        /// <summary>Flat quad facing +Z (out of the wall).</summary>
        Transform Quad(string n, Vector3 centre, Vector2 size, Material mat, Vector2? tiling = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = n;
            g.layer = layer;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(Root, false);
            g.transform.localPosition = centre;
            g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            g.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (tiling.HasValue)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector("_MainTex_ST", new Vector4(tiling.Value.x, tiling.Value.y, 0f, 0f));
                r.SetPropertyBlock(mpb);
            }
            return g.transform;
        }

        // ================================================================ parts

        void BuildPlate()
        {
            // Thick wall plate (1.04 x 1.62 m) with a raised frame.
            Rb("Plate", new Vector3(0f, 1.12f, 0.03f), new Vector3(1.04f, 1.62f, 0.06f), 0.015f, plate);
            Rb("FrameTop", new Vector3(0f, 1.9f, 0.065f), new Vector3(1.04f, 0.08f, 0.02f), 0.006f, darkSteel);
            Rb("FrameBottom", new Vector3(0f, 0.34f, 0.065f), new Vector3(1.04f, 0.08f, 0.02f), 0.006f, darkSteel);
            Rb("FrameL", new Vector3(-0.48f, 1.12f, 0.065f), new Vector3(0.08f, 1.48f, 0.02f), 0.006f, darkSteel);
            Rb("FrameR", new Vector3(0.48f, 1.12f, 0.065f), new Vector3(0.08f, 1.48f, 0.02f), 0.006f, darkSteel);
            // Hazard stripes on the frame.
            Quad("HazardTop", new Vector3(0f, 1.9f, 0.0762f), new Vector2(1.0f, 0.06f), hazard, new Vector2(16f, 1f));
            Quad("HazardBottom", new Vector3(0f, 0.34f, 0.0762f), new Vector2(1.0f, 0.06f), hazard, new Vector2(16f, 1f));
            Quad("HazardL", new Vector3(-0.48f, 1.12f, 0.0762f), new Vector2(0.06f, 1.42f), hazard, new Vector2(1f, 23f));
            Quad("HazardR", new Vector3(0.48f, 1.12f, 0.0762f), new Vector2(0.06f, 1.42f), hazard, new Vector2(1f, 23f));
            // Bolts through the frame corners and sides.
            foreach (var b in new[] { new Vector2(-0.48f, 1.9f), new Vector2(0.48f, 1.9f), new Vector2(-0.48f, 0.34f), new Vector2(0.48f, 0.34f),
                                      new Vector2(-0.48f, 1.12f), new Vector2(0.48f, 1.12f) })
            {
                Cyl("BoltHead", new Vector3(b.x, b.y, 0.082f), 0.042f, 0.016f, 2, steel);
                Cyl("BoltNut", new Vector3(b.x, b.y, 0.094f), 0.022f, 0.012f, 2, darkSteel);
            }
        }

        void BuildBearings()
        {
            Vector3 c = PivotPos;
            foreach (float sx in new[] { -1f, 1f })
            {
                // Base flange on the plate and a pillow block outside each arm.
                Rb("BearingFlange", new Vector3(sx * 0.385f, c.y, 0.07f), new Vector3(0.12f, 0.3f, 0.02f), 0.006f, darkSteel);
                Rb("Bearing", new Vector3(sx * 0.385f, c.y, 0.135f), new Vector3(0.075f, 0.18f, 0.13f), 0.015f, darkSteel);
                Cyl("BearingCap", new Vector3(sx * 0.385f, c.y, c.z), 0.12f, 0.08f, 0, darkSteel);
                foreach (float sy in new[] { -1f, 1f })
                    Cyl("FlangeBolt", new Vector3(sx * 0.385f, c.y + sy * 0.12f, 0.085f), 0.03f, 0.014f, 2, steel);
            }
            // Axle through both arms, with nuts.
            Cyl("Axle", c, 0.045f, 0.9f, 0, steel);
            Cyl("AxleNutL", c + new Vector3(-0.445f, 0f, 0f), 0.07f, 0.025f, 0, steel);
            Cyl("AxleNutR", c + new Vector3(0.445f, 0f, 0f), 0.07f, 0.025f, 0, steel);
        }

        void BuildArms()
        {
            Pivot = new GameObject("LeverPivot") { layer = layer }.transform;
            Pivot.SetParent(Root, false);
            Pivot.localPosition = PivotPos;
            Pivot.localRotation = Quaternion.Euler(UpAngle, 0f, 0f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * ArmX;
                Cyl("Hub", new Vector3(x, 0f, 0f), 0.11f, 0.06f, 0, steel, Pivot);
                Rb("Arm", new Vector3(x, ArmLength * 0.5f, 0f), new Vector3(0.036f, ArmLength + 0.06f, 0.07f), 0.01f, steel, Pivot);
                // Copper knife blade riveted to the arm where it enters the jaws.
                Rb("Blade", new Vector3(x, JawS, 0f), new Vector3(0.026f, 0.16f, 0.085f), 0.006f, copper, Pivot);
                Cyl("Rivet", new Vector3(x + sx * 0.02f, JawS - 0.05f, 0f), 0.018f, 0.01f, 0, steel, Pivot);
                Cyl("Rivet", new Vector3(x + sx * 0.02f, JawS + 0.05f, 0f), 0.018f, 0.01f, 0, steel, Pivot);
            }
            // Insulated tie bar between the arms.
            Rb("TieBar", new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.05f, 0.05f), 0.012f, black, Pivot);
            // Horizontal red bar joining the arm ends, black end caps.
            Bar = Cyl("RedBar", new Vector3(0f, ArmLength, 0f), 0.062f, 0.74f, 0, red, Pivot);
            Cyl("BarCapL", new Vector3(-0.37f, ArmLength, 0f), 0.078f, 0.04f, 0, black, Pivot);
            Cyl("BarCapR", new Vector3(0.37f, ArmLength, 0f), 0.078f, 0.04f, 0, black, Pivot);
            foreach (float sx in new[] { -1f, 1f })
                Cyl("BarCollar", new Vector3(sx * ArmX, ArmLength, 0f), 0.08f, 0.05f, 0, steel, Pivot);
        }

        void BuildJaws(float deg, string tag)
        {
            Vector3 j = ArmPoint(deg, JawS);
            var rot = Quaternion.Euler(deg, 0f, 0f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * ArmX;
                // Ceramic stand-off post with ribs, a copper base and two spring plates the blade slides between.
                float postEnd = j.z - 0.06f;
                Cyl(tag + "Post", new Vector3(x, j.y, (0.06f + postEnd) * 0.5f), 0.06f, postEnd - 0.06f, 2, ceramic);
                for (int r = 0; r < 3; r++)
                    Cyl(tag + "Rib", new Vector3(x, j.y, 0.09f + r * 0.065f), 0.082f, 0.014f, 2, ceramic);
                Rb(tag + "JawBase", new Vector3(x, j.y, postEnd + 0.01f), new Vector3(0.09f, 0.06f, 0.03f), 0.006f, copper);
                foreach (float side in new[] { -1f, 1f })
                    Rb(tag + "JawPlate", new Vector3(x + side * 0.024f, j.y, j.z), new Vector3(0.01f, 0.13f, 0.09f), 0.003f, copper, null, rot);
                Cyl(tag + "JawBolt", new Vector3(x, j.y, j.z), 0.016f, 0.07f, 0, steel);
                // Heavy cable from the jaw to the frame (up / down).
                float edge = deg < 90f ? 1.86f : 0.38f;
                Rb(tag + "Cable", new Vector3(x + sx * 0.06f, (j.y + edge) * 0.5f, 0.085f), new Vector3(0.03f, Mathf.Abs(j.y - edge), 0.03f), 0.014f, cable);
            }
            if (deg > 90f)
            {
                LowerJaws[0] = new Vector3(-ArmX, j.y, j.z);
                LowerJaws[1] = new Vector3(ArmX, j.y, j.z);
            }
        }

        void BuildLock()
        {
            // Yellow solenoid on a bracket beside the +X arm (the puller's left), its bolt crosses the arm below the
            // bar while the lever is locked.
            Vector3 at = ArmPoint(UpAngle, 0.24f);
            Rb("LockBracket", new Vector3(0.455f, at.y, (0.06f + at.z) * 0.5f), new Vector3(0.05f, 0.08f, at.z - 0.06f), 0.006f, darkSteel);
            Rb("LockSolenoid", new Vector3(0.455f, at.y, at.z), new Vector3(0.1f, 0.095f, 0.095f), 0.016f, yellow);
            Quad("LockMark", new Vector3(0.455f, at.y, at.z + 0.0485f), new Vector2(0.07f, 0.03f), hazard, new Vector2(3f, 1f));
            Bolt = Cyl("LockBolt", new Vector3(BoltIn, at.y, at.z), 0.028f, 0.16f, 0, steel);
        }

        void BuildTop()
        {
            // Junction box above the plate, conduit to the ceiling and down to the floor.
            Rb("JunctionBox", new Vector3(0f, 2.06f, 0.08f), new Vector3(0.36f, 0.2f, 0.16f), 0.02f, darkSteel);
            Rb("JunctionLid", new Vector3(0f, 2.06f, 0.163f), new Vector3(0.32f, 0.16f, 0.012f), 0.005f, plate);
            Cyl("ConduitUp", new Vector3(0f, 2.86f, 0.06f), 0.06f, 1.42f, 1, darkSteel);
            foreach (float sx in new[] { -1f, 1f })
                Cyl("ConduitDown", new Vector3(sx * 0.31f, 0.165f, 0.05f), 0.05f, 0.33f, 1, darkSteel);
            // Caged status lamp on the lid.
            Cyl("LampBase", new Vector3(0f, 2.06f, 0.18f), 0.11f, 0.025f, 2, black);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "StatusLamp";
            dome.layer = layer;
            Object.Destroy(dome.GetComponent<Collider>());
            dome.transform.SetParent(Root, false);
            dome.transform.localPosition = new Vector3(0f, 2.06f, 0.2f);
            dome.transform.localScale = new Vector3(0.09f, 0.09f, 0.1f);
            dome.GetComponent<MeshRenderer>().sharedMaterial = lampGlass;
            Lamp = dome.GetComponent<Renderer>();
            for (int i = 0; i < 4; i++)
            {
                float a = i * 45f;
                Rb("LampCage", new Vector3(0f, 2.06f, 0.215f), new Vector3(0.008f, 0.115f, 0.008f), 0.003f, black, null, Quaternion.Euler(0f, 0f, a));
            }
            Cyl("LampRing", new Vector3(0f, 2.06f, 0.196f), 0.112f, 0.008f, 2, black);
            Glow = new GameObject("Glow").AddComponent<Light>();
            Glow.transform.SetParent(Root, false);
            Glow.transform.localPosition = new Vector3(0f, 2.06f, 0.45f);
            Glow.type = LightType.Point;
            Glow.range = 3f;
            Glow.intensity = 1.2f;
            Glow.shadows = LightShadows.None;
        }

        void BuildLabels()
        {
            var paper = new Color(0.95f, 0.72f, 0.08f);
            var ink = new Color(0.06f, 0.05f, 0.04f);
            var start = Mat(plate, Color.white, 0.1f, 0.5f, StartLeverArt.Label(new[] { "START", "RUNDY" }, 256, 128, 5, paper, ink));
            Quad("LabelStart", new Vector3(0f, 0.5f, 0.0605f), new Vector2(0.4f, 0.2f), start);
            var arrow = Mat(plate, Color.white, 0.1f, 0.5f, StartLeverArt.ArrowDown(new Color(0.2f, 0.215f, 0.23f), new Color(0.92f, 0.9f, 0.85f)));
            Quad("ArrowDown", new Vector3(0f, 0.85f, 0.0605f), new Vector2(0.12f, 0.18f), arrow);
            var white = new Color(0.92f, 0.9f, 0.85f);
            var stop = Mat(plate, Color.white, 0.1f, 0.5f, StartLeverArt.Label(new[] { "STOP" }, 128, 48, 4, white, ink));
            Vector3 up = ArmPoint(UpAngle, JawS);
            Quad("LabelStop", new Vector3(0f, up.y + 0.02f, 0.0605f), new Vector2(0.2f, 0.075f), stop);
            var warn = Mat(plate, Color.white, 0.1f, 0.5f, StartLeverArt.Label(new[] { "UWAGA", "WSZYSCY GOTOWI?" }, 320, 96, 3, paper, ink));
            Quad("LabelWarn", new Vector3(0f, 1.74f, 0.0605f), new Vector2(0.36f, 0.135f), warn);
        }
    }
}
