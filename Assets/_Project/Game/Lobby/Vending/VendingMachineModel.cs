using HideAndSeek.Characters;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Detailed lobby vending machine built at runtime under the scene's "VendingMachine" root (local space = world
    /// axes, front facing -X, footprint x [-0.4, 0.4], z [-0.5, 0.5], 1.96 m high; the scene keeps only an invisible
    /// collider box). Cabinet, printed sides, lit header, glazed door with a light box, 4 x 4 spiral slots with two
    /// cans each, control column (display, keypad, coin / note / card), delivery bin with a swinging flap and tray.
    /// </summary>
    public sealed partial class VendingMachineModel : MonoBehaviour
    {
        public const int Rows = 4, Cols = 4;
        public const float CanScale = VendingTimeline.MachineCanScale;
        /// <summary>Slot spacing along the spiral (one turn pushes the cans forward by this much).</summary>
        public const float Pitch = 0.14f;
        public const float ShelfFrontX = -0.245f, DropX = -0.31f, BinY = 0.215f;

        public SodaCan[,] Front { get; } = new SodaCan[Rows, Cols];
        public SodaCan[,] Rear { get; } = new SodaCan[Rows, Cols];
        public Transform[,] Spirals { get; } = new Transform[Rows, Cols];
        public Transform[] Keys { get; } = new Transform[12];
        public Transform Flap { get; private set; }

        Material paint, charcoal, chrome, black, rubber, inner, shelfMat, lightBox, led, headerMat, glassMat, readerMat, displayMat, sideArt;
        Texture2D displayTex;
        string displayText;
        int layer;

        public static float RowY(int r) => 0.52f + r * 0.29f;
        public static float ColZ(int c) => -0.37f + c * 0.145f;
        /// <summary>Centre of the front can of a slot (standing on the shelf).</summary>
        public static Vector3 SlotFront(int r, int c) => new Vector3(-0.19f, RowY(r) + 0.012f + SodaCan.Height * CanScale * 0.5f, ColZ(c));
        /// <summary>Brand of the cans in a slot: every brand twice, mixed across the shelves.</summary>
        public static int SlotBrand(int r, int c) => (r * 3 + c * 5 + r / 2) % SodaCanArt.BrandCount;
        /// <summary>Key pressed to buy from a slot.</summary>
        public static int SlotKey(int r, int c) => (r * 3 + c) % 12;

        public static VendingMachineModel Build(Transform root, Material template)
        {
            var m = root.gameObject.AddComponent<VendingMachineModel>();
            m.layer = root.gameObject.layer;
            m.Materials(template);
            m.BuildCabinet();
            m.BuildDoor();
            m.BuildShelves();
            m.BuildPanel();
            m.BuildBin();
            m.SetDisplay("2.50");
            return m;
        }

        // ================================================================ materials

        Material Mat(Material template, Color c, float metallic, float gloss, Texture tex = null)
        {
            var m = new Material(template) { color = c, mainTexture = tex };
            m.DisableKeyword("_EMISSION");
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        Material Glow(Material template, Color c, Texture tex = null, float intensity = 1f)
        {
            var m = Mat(template, Color.black, 0f, 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            if (tex != null) m.SetTexture("_EmissionMap", tex);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        void Materials(Material t)
        {
            paint = Mat(t, new Color(0.66f, 0.06f, 0.08f), 0.35f, 0.62f, VendingMachineArt.Paint);
            charcoal = Mat(t, new Color(0.13f, 0.135f, 0.15f), 0.6f, 0.55f);
            chrome = Mat(t, new Color(0.82f, 0.84f, 0.87f), 0.95f, 0.88f);
            black = Mat(t, new Color(0.035f, 0.035f, 0.04f), 0.1f, 0.55f);
            rubber = Mat(t, new Color(0.02f, 0.02f, 0.022f), 0f, 0.2f);
            inner = Mat(t, new Color(0.2f, 0.21f, 0.23f), 0.5f, 0.4f);
            shelfMat = Mat(t, new Color(0.55f, 0.57f, 0.6f), 0.8f, 0.6f);
            sideArt = Mat(t, Color.white, 0.3f, 0.6f, VendingMachineArt.Side);
            lightBox = Glow(t, new Color(0.75f, 0.85f, 0.95f), VendingMachineArt.BackWall, 1.4f);
            led = Glow(t, new Color(0.85f, 0.95f, 1f), null, 2.2f);
            headerMat = Glow(t, Color.white, VendingMachineArt.Header, 1.15f);
            readerMat = Mat(t, Color.white, 0.2f, 0.85f, VendingMachineArt.Reader);
            displayTex = new Texture2D(96, 24, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "VendDisplay" };
            displayMat = Glow(t, new Color(0.3f, 1f, 0.45f), displayTex, 1.6f);
            var glassShader = Resources.Load<Shader>("Shaders/HnsGlass");
            glassMat = glassShader != null ? new Material(glassShader) { mainTexture = VendingMachineArt.Glass } : Mat(t, new Color(0.6f, 0.7f, 0.75f), 0.5f, 0.95f);
            if (glassShader != null) { glassMat.SetFloat("_Streak", 0.06f); glassMat.SetColor("_Edge", new Color(0.7f, 0.8f, 0.9f, 0.3f)); }
        }

        /// <summary>Shows up to 4 digits on the price display ("2.50", "13", "----").</summary>
        public void SetDisplay(string text)
        {
            if (text == displayText) return;
            displayText = text;
            VendingMachineArt.DrawDisplay(displayTex, text, new Color(0.35f, 1f, 0.5f));
        }

        // ================================================================ helpers

        Transform Rb(string n, Vector3 centre, Vector3 size, float radius, Material m, Transform parent = null)
        {
            var g = new GameObject(n) { layer = layer };
            g.transform.SetParent(parent != null ? parent : transform, false);
            g.transform.localPosition = centre;
            g.AddComponent<MeshFilter>().sharedMesh = VendingMeshes.RoundedBox(size, radius);
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        Transform Quad(string n, Vector3 centre, Vector2 size, Quaternion rot, Material m, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = n;
            g.layer = layer;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent != null ? parent : transform, false);
            g.transform.localPosition = centre;
            g.transform.localRotation = rot;
            g.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g.transform;
        }

        Transform Cyl(string n, Vector3 centre, float diameter, float length, Quaternion rot, Material m, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = n;
            g.layer = layer;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent != null ? parent : transform, false);
            g.transform.localPosition = centre;
            g.transform.localRotation = rot;
            g.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        static readonly Quaternion FaceFront = Quaternion.Euler(0f, 90f, 0f);   // quad normal -> -X
        static readonly Quaternion AlongX = Quaternion.Euler(0f, 0f, 90f);       // cylinder axis -> X

        // ================================================================ cabinet

        void BuildCabinet()
        {
            // Shell: sides, back, top cap and plinth (the front is made of the door, header, panel and bin).
            Rb("SideL", new Vector3(0.01f, 1.0f, 0.48f), new Vector3(0.78f, 1.82f, 0.04f), 0.015f, paint);
            Rb("SideR", new Vector3(0.01f, 1.0f, -0.48f), new Vector3(0.78f, 1.82f, 0.04f), 0.015f, paint);
            Quad("SideArtL", new Vector3(0.0f, 1.0f, 0.5005f), new Vector2(0.72f, 1.66f), Quaternion.Euler(0f, 180f, 0f), sideArt);
            Quad("SideArtR", new Vector3(0.0f, 1.0f, -0.5005f), new Vector2(0.72f, 1.66f), Quaternion.identity, sideArt);
            Rb("Back", new Vector3(0.38f, 1.0f, 0f), new Vector3(0.04f, 1.82f, 0.94f), 0.01f, paint);
            Rb("TopCap", new Vector3(0.005f, 1.935f, 0f), new Vector3(0.81f, 0.05f, 1.02f), 0.02f, charcoal);
            Rb("Plinth", new Vector3(0.02f, 0.05f, 0f), new Vector3(0.76f, 0.1f, 0.98f), 0.012f, charcoal);
            for (int i = 0; i < 10; i++)
                Rb("PlinthVent", new Vector3(-0.361f, 0.05f, -0.4f + i * 0.09f), new Vector3(0.004f, 0.05f, 0.06f), 0.002f, black);
            for (int i = 0; i < 4; i++)
                Cyl("Foot", new Vector3(i < 2 ? -0.3f : 0.32f, 0.006f, i % 2 == 0 ? -0.42f : 0.42f), 0.05f, 0.012f, Quaternion.identity, rubber);
            // Lit header with a chrome trim and a dark bezel.
            Rb("HeaderBezel", new Vector3(-0.37f, 1.8f, 0f), new Vector3(0.06f, 0.2f, 0.98f), 0.015f, charcoal);
            Quad("HeaderArt", new Vector3(-0.4005f, 1.8f, 0f), new Vector2(0.92f, 0.155f), FaceFront, headerMat);
            Rb("HeaderTrimTop", new Vector3(-0.403f, 1.882f, 0f), new Vector3(0.008f, 0.008f, 0.94f), 0.003f, chrome);
            Rb("HeaderTrimBot", new Vector3(-0.403f, 1.718f, 0f), new Vector3(0.008f, 0.008f, 0.94f), 0.003f, chrome);
            // Lights: glow in front of the machine, and inside the cabinet so the cans are lit.
            var front = new GameObject("VendingGlow").AddComponent<Light>();
            front.transform.SetParent(transform, false);
            front.transform.localPosition = new Vector3(-0.75f, 1.25f, -0.1f);
            front.type = LightType.Point; front.range = 3.2f; front.intensity = 0.9f; front.color = new Color(0.65f, 0.85f, 1f);
            var inside = new GameObject("VendingInnerLight").AddComponent<Light>();
            inside.transform.SetParent(transform, false);
            inside.transform.localPosition = new Vector3(-0.33f, 1.1f, -0.15f);
            inside.type = LightType.Point; inside.range = 1.1f; inside.intensity = 1.3f; inside.color = new Color(0.85f, 0.93f, 1f);
        }

        void BuildDoor()
        {
            // Frame (left / right stiles, top / bottom rails), rubber seal, handle, lock, glass.
            Rb("StileL", new Vector3(-0.385f, 1.07f, 0.1625f), new Vector3(0.05f, 1.3f, 0.045f), 0.012f, charcoal);
            Rb("StileR", new Vector3(-0.385f, 1.07f, -0.4625f), new Vector3(0.05f, 1.3f, 0.045f), 0.012f, charcoal);
            Rb("RailTop", new Vector3(-0.385f, 1.6975f, -0.15f), new Vector3(0.05f, 0.045f, 0.67f), 0.012f, charcoal);
            Rb("RailBot", new Vector3(-0.385f, 0.4425f, -0.15f), new Vector3(0.05f, 0.045f, 0.67f), 0.012f, charcoal);
            Rb("SealTop", new Vector3(-0.401f, 1.672f, -0.15f), new Vector3(0.006f, 0.008f, 0.59f), 0.003f, rubber);
            Rb("SealBot", new Vector3(-0.401f, 0.468f, -0.15f), new Vector3(0.006f, 0.008f, 0.59f), 0.003f, rubber);
            Rb("Handle", new Vector3(-0.432f, 1.08f, 0.163f), new Vector3(0.022f, 0.34f, 0.026f), 0.01f, chrome);
            Rb("HandleFootTop", new Vector3(-0.418f, 1.23f, 0.163f), new Vector3(0.03f, 0.02f, 0.02f), 0.006f, chrome);
            Rb("HandleFootBot", new Vector3(-0.418f, 0.93f, 0.163f), new Vector3(0.03f, 0.02f, 0.02f), 0.006f, chrome);
            Cyl("Lock", new Vector3(-0.412f, 1.34f, 0.163f), 0.022f, 0.012f, AlongX, chrome);
            Rb("LockSlot", new Vector3(-0.418f, 1.34f, 0.163f), new Vector3(0.002f, 0.012f, 0.003f), 0.0005f, black);
            for (int i = 0; i < 3; i++)
                Cyl("Hinge", new Vector3(-0.395f, 0.55f + i * 0.5f, -0.488f), 0.016f, 0.06f, Quaternion.identity, chrome);
            Quad("Glass", new Vector3(-0.398f, 1.07f, -0.15f), new Vector2(0.6f, 1.215f), FaceFront, glassMat);
        }

        void BuildShelves()
        {
            // Inner cavity: dark side walls, ceiling with an LED strip, the light box at the back.
            Rb("InnerL", new Vector3(-0.07f, 1.07f, 0.145f), new Vector3(0.6f, 1.26f, 0.01f), 0.003f, inner);
            Rb("InnerR", new Vector3(-0.07f, 1.07f, -0.445f), new Vector3(0.6f, 1.26f, 0.01f), 0.003f, inner);
            Rb("InnerTop", new Vector3(-0.07f, 1.695f, -0.15f), new Vector3(0.6f, 0.01f, 0.6f), 0.003f, inner);
            Rb("LedStrip", new Vector3(-0.33f, 1.685f, -0.15f), new Vector3(0.025f, 0.008f, 0.56f), 0.003f, led);
            Quad("LightBox", new Vector3(0.225f, 1.07f, -0.15f), new Vector2(0.59f, 1.25f), FaceFront, lightBox);
            Rb("Chute", new Vector3(-0.31f, 0.3f, -0.15f), new Vector3(0.16f, 0.01f, 0.58f), 0.003f, inner).localRotation = Quaternion.Euler(0f, 0f, -8f);
            for (int r = 0; r < Rows; r++)
            {
                float y = RowY(r);
                Rb("Shelf", new Vector3(-0.015f, y, -0.15f), new Vector3(0.46f, 0.012f, 0.585f), 0.004f, shelfMat);
                Rb("ShelfLip", new Vector3(-0.243f, y - 0.018f, -0.15f), new Vector3(0.008f, 0.03f, 0.585f), 0.003f, shelfMat);
                for (int c = 0; c < Cols; c++)
                {
                    float z = ColZ(c);
                    // Price tab on the lip (colour of the brand, no print), divider between slots.
                    Rb("PriceTab", new Vector3(-0.249f, y - 0.018f, z), new Vector3(0.003f, 0.022f, 0.05f), 0.002f,
                        Mat(paint, SodaCanArt.BrandColor(SlotBrand(r, c)), 0.1f, 0.5f));
                    if (c < Cols - 1) Rb("Divider", new Vector3(-0.02f, y + 0.07f, z + 0.0725f), new Vector3(0.44f, 0.12f, 0.004f), 0.002f, inner);
                    // Spiral (axis along X, sits on the shelf) with its motor at the back.
                    var sp = new GameObject("Spiral") { layer = layer }.transform;
                    sp.SetParent(transform, false);
                    sp.localPosition = new Vector3(0.2f, y + 0.012f + 0.062f, z);
                    var coil = new GameObject("Coil") { layer = layer }.transform;
                    coil.SetParent(sp, false);
                    coil.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    coil.gameObject.AddComponent<MeshFilter>().sharedMesh = VendingMeshes.Spiral(0.44f, 0.062f, 0.0025f, 3);
                    coil.gameObject.AddComponent<MeshRenderer>().sharedMaterial = chrome;
                    Spirals[r, c] = sp;
                    Rb("Motor", new Vector3(0.205f, y + 0.07f, z), new Vector3(0.03f, 0.07f, 0.07f), 0.006f, black);
                    // Two cans in the spiral, logo out.
                    int brand = SlotBrand(r, c);
                    Front[r, c] = SodaCan.Create(transform, brand, layer, CanScale);
                    Front[r, c].transform.localPosition = SlotFront(r, c);
                    Front[r, c].transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    Rear[r, c] = SodaCan.Create(transform, brand, layer, CanScale);
                    Rear[r, c].transform.localPosition = SlotFront(r, c) + Vector3.right * Pitch;
                    Rear[r, c].transform.localRotation = Quaternion.Euler(0f, 90f + (r * 31 + c * 17) % 40 - 20, 0f);
                }
            }
        }
    }
}
