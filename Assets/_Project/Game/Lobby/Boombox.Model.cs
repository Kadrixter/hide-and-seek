using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Boombox model: rounded black body, brushed silver face, two woofers (chrome trim, rubber surround, cone, dust
    /// cap - the moving part is <see cref="_diaL"/> / <see cref="_diaR"/>), cassette deck with turning reels, LCD, VU
    /// LEDs, piano keys and knobs on top, a bent chrome handle with a rubber grip, a telescopic antenna, rubber feet.
    /// All smooth procedural meshes (<see cref="BoomboxMeshes"/>, <see cref="VendingMeshes.RoundedBox"/>).
    /// </summary>
    public partial class Boombox
    {
        const float W = 0.56f, H = 0.27f, D = 0.15f;
        const float SpkX = 0.168f, SpkY = 0.133f, FaceZ = D * 0.5f + 0.007f;

        private Transform _visual;               // everything visible (shakes on the bass; the collider stays put)
        private Transform _diaL, _diaR;          // cone + dust cap
        private Transform _surL, _surR, _capL, _capR;
        private Transform _reelL, _reelR, _knobVol, _knobBass, _playKey;
        private TextMesh _lcd;
        private Renderer[] _leds;
        private MaterialPropertyBlock _mpb;

        private static Material Mat(Color c, float gloss, float metal = 0f)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        private static Material Glow(Color c) => new Material(Shader.Find("Unlit/Color")) { color = c };

        private Transform Mesh(string n, Transform parent, Mesh mesh, Material m, Vector3 lp, Quaternion? rot = null, Vector3? scale = null)
        {
            var g = new GameObject(n);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.transform.localScale = scale ?? Vector3.one;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            return g.transform;
        }

        private Transform Box(string n, Transform parent, Vector3 lp, Vector3 size, float radius, Material m, Quaternion? rot = null)
            => Mesh(n, parent, VendingMeshes.RoundedBox(size, radius, 6), m, lp, rot);

        private void Build()
        {
            var body = Mat(new Color(0.055f, 0.055f, 0.065f), 0.55f);
            var silver = Mat(new Color(0.66f, 0.68f, 0.72f), 0.62f, 0.35f);
            var chrome = Mat(new Color(0.86f, 0.87f, 0.9f), 0.9f, 0.6f);
            var rubber = Mat(new Color(0.04f, 0.04f, 0.045f), 0.12f);
            var coneMat = Mat(new Color(0.2f, 0.2f, 0.22f), 0.42f);
            var capMat = Mat(new Color(0.09f, 0.09f, 0.1f), 0.8f, 0.2f);
            var dark = Mat(new Color(0.03f, 0.03f, 0.035f), 0.25f);
            var red = Mat(new Color(0.8f, 0.08f, 0.06f), 0.5f);
            var label = Mat(new Color(0.93f, 0.86f, 0.62f), 0.25f);

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            var v = _visual;

            // Body + face plate (slightly proud of the body, softly rounded).
            Box("Body", v, new Vector3(0f, H * 0.5f, 0f), new Vector3(W, H, D), 0.028f, body);
            Box("Face", v, new Vector3(0f, H * 0.5f, D * 0.5f), new Vector3(W - 0.034f, H - 0.036f, 0.014f), 0.006f, silver);
            // Chrome pin-stripes along the top and the bottom of the face.
            foreach (float y in new[] { 0.026f, H - 0.026f })
                Mesh("Stripe", v, BoomboxMeshes.Tube("stripe", new List<Vector3> { new(-W * 0.45f, 0f, 0f), new(W * 0.45f, 0f, 0f) }, 0.0022f, 10), chrome,
                    new Vector3(0f, y, FaceZ + 0.0005f));

            BuildSpeakers(v, chrome, rubber, coneMat, capMat, dark);
            BuildCentre(v, dark, silver, chrome, label, body);
            BuildTop(v, silver, red, chrome, rubber, body);

            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, H * 0.5f, 0f);
            col.size = new Vector3(W, H, D);
        }

        private void BuildSpeakers(Transform v, Material chrome, Material rubber, Material coneMat, Material capMat, Material dark)
        {
            // Profiles in (radius, z) around the speaker axis (+Z = out of the face). Each woofer sits in a pod that
            // stands 2.5 cm proud of the face: black collar, chrome trim ring with four screws on its lip.
            var collar = new List<Vector2> { new(0.0795f, 0.002f), new(0.0795f, 0.024f), new(0.0795f, 0.024f) };
            BoomboxMeshes.Round(collar, new Vector2(0.0795f, 0.024f), new Vector2(0.101f, 0f), 8, true);
            var trim = new List<Vector2>();
            BoomboxMeshes.Round(trim, new Vector2(0.0785f, 0.022f), new Vector2(0.0855f, 0.031f), 6, false);
            BoomboxMeshes.Round(trim, new Vector2(0.0855f, 0.031f), new Vector2(0.0935f, 0.0235f), 6, true);
            var surround = new List<Vector2>();
            BoomboxMeshes.Round(surround, new Vector2(0.066f, 0f), new Vector2(0.0725f, 0.0065f), 6, false);
            BoomboxMeshes.Round(surround, new Vector2(0.0725f, 0.0065f), new Vector2(0.0797f, 0f), 6, true);
            var cone = new List<Vector2>();
            for (int i = 0; i <= 40; i++)
            {
                float k = i / 40f;
                // Slightly curved (exponential horn) dish with two pressed rings, so the moving cone catches the light.
                float ring = 0.0011f * (Mathf.Exp(-Mathf.Pow((k - 0.38f) / 0.035f, 2f)) + Mathf.Exp(-Mathf.Pow((k - 0.7f) / 0.035f, 2f)));
                cone.Add(new Vector2(Mathf.Lerp(0.024f, 0.0665f, k), -0.016f + 0.016f * Mathf.Pow(k, 1.35f) + ring));
            }
            var cap = new List<Vector2>();
            BoomboxMeshes.Round(cap, new Vector2(0f, 0.011f), new Vector2(0.0255f, 0f), 8, true);
            var back = new List<Vector2> { new(0f, 0.001f), new(0.08f, 0.001f) };

            foreach (float sx in new[] { -1f, 1f })
            {
                var spk = new GameObject(sx < 0 ? "SpeakerL" : "SpeakerR").transform;
                spk.SetParent(v, false);
                spk.localPosition = new Vector3(sx * SpkX, SpkY, FaceZ);
                Mesh("Back", spk, BoomboxMeshes.Lathe("spkback", back, 48), dark, Vector3.zero);
                Mesh("Collar", spk, BoomboxMeshes.Lathe("spkcollar", collar, 72), dark, Vector3.zero);
                Mesh("Trim", spk, BoomboxMeshes.Lathe("spktrim", trim, 72), chrome, Vector3.zero);
                var screw = new List<Vector2>();
                BoomboxMeshes.Round(screw, new Vector2(0f, 0.0028f), new Vector2(0.0036f, 0f), 4, true);
                for (int i = 0; i < 4; i++)
                {
                    float a = (45f + 90f * i) * Mathf.Deg2Rad;
                    Mesh("Screw", spk, BoomboxMeshes.Lathe("screw", screw, 16), rubber, new Vector3(Mathf.Cos(a) * 0.0858f, Mathf.Sin(a) * 0.0858f, 0.0295f));
                }
                // Surround (pivot = its base ring) is stretched in depth as the cone moves.
                var sur = Mesh("Surround", spk, BoomboxMeshes.Lathe("spksur", surround, 72), rubber, new Vector3(0f, 0f, 0.019f));
                var dia = new GameObject("Diaphragm").transform;
                dia.SetParent(spk, false);
                dia.localPosition = new Vector3(0f, 0f, 0.019f);
                Mesh("Cone", dia, BoomboxMeshes.Lathe("spkcone", cone, 72), coneMat, Vector3.zero);
                var capT = Mesh("DustCap", dia, BoomboxMeshes.Lathe("spkcap", cap, 48), capMat, new Vector3(0f, 0f, -0.016f));
                if (sx < 0) { _diaL = dia; _surL = sur; _capL = capT; } else { _diaR = dia; _surR = sur; _capR = capT; }
            }
        }

        private void BuildCentre(Transform v, Material dark, Material silver, Material chrome, Material label, Material body)
        {
            // LCD in a black bezel, VU LEDs under it.
            Box("LcdBezel", v, new Vector3(0f, 0.207f, FaceZ), new Vector3(0.142f, 0.052f, 0.008f), 0.004f, dark);
            Box("LcdGlass", v, new Vector3(0f, 0.207f, FaceZ + 0.0035f), new Vector3(0.128f, 0.04f, 0.003f), 0.0012f, Glow(new Color(0.05f, 0.13f, 0.09f)));
            var lcdGo = new GameObject("LcdText");
            lcdGo.transform.SetParent(v, false);
            lcdGo.transform.localPosition = new Vector3(0f, 0.207f, FaceZ + 0.0056f);
            lcdGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _lcd = lcdGo.AddComponent<TextMesh>();
            _lcd.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _lcd.GetComponent<MeshRenderer>().sharedMaterial = _lcd.font.material;
            _lcd.fontSize = 48;
            _lcd.characterSize = 0.00105f;
            _lcd.anchor = TextAnchor.MiddleCenter;
            _lcd.color = new Color(0.5f, 1f, 0.62f);
            _lcd.text = "RADIO";
            _leds = new Renderer[8];
            var ledMesh = VendingMeshes.RoundedBox(new Vector3(0.011f, 0.0065f, 0.004f), 0.0018f, 4);
            var ledMat = Glow(Color.white);
            for (int i = 0; i < _leds.Length; i++)
                _leds[i] = Mesh("Led", v, ledMesh, ledMat, new Vector3(0.0525f - i * 0.015f, 0.17f, FaceZ + 0.001f)).GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();

            // Cassette deck: recessed door, cassette with a label, two reels that turn while the music plays.
            Box("DeckDoor", v, new Vector3(0f, 0.093f, FaceZ), new Vector3(0.134f, 0.09f, 0.008f), 0.006f, dark);
            Box("Cassette", v, new Vector3(0f, 0.095f, FaceZ + 0.003f), new Vector3(0.106f, 0.068f, 0.006f), 0.003f, Mat(new Color(0.16f, 0.16f, 0.18f), 0.5f));
            Box("Label", v, new Vector3(0f, 0.111f, FaceZ + 0.0062f), new Vector3(0.094f, 0.025f, 0.001f), 0.0006f, label);
            Box("Window", v, new Vector3(0f, 0.087f, FaceZ + 0.0062f), new Vector3(0.07f, 0.022f, 0.001f), 0.004f, Mat(new Color(0.05f, 0.05f, 0.06f), 0.95f));
            var reel = new List<Vector2>();
            BoomboxMeshes.Round(reel, new Vector2(0.0035f, 0.0018f), new Vector2(0.0085f, 0f), 4, true);
            foreach (float sx in new[] { -1f, 1f })
            {
                var r = Mesh("Reel", v, BoomboxMeshes.Lathe("reel", reel, 24), Mat(new Color(0.92f, 0.92f, 0.92f), 0.4f), new Vector3(sx * 0.022f, 0.087f, FaceZ + 0.0068f));
                for (int k = 0; k < 3; k++)
                    Box("Tooth", r, Quaternion.Euler(0f, 0f, k * 120f) * new Vector3(0f, 0.0028f, 0.001f), new Vector3(0.0012f, 0.0026f, 0.0016f), 0.0003f, dark, Quaternion.Euler(0f, 0f, k * 120f));
                if (sx < 0) _reelL = r; else _reelR = r;
            }
            Box("Eject", v, new Vector3(0f, 0.04f, FaceZ + 0.001f), new Vector3(0.04f, 0.009f, 0.008f), 0.003f, silver);
            Box("Mic", v, new Vector3(0.05f, 0.04f, FaceZ + 0.001f), new Vector3(0.012f, 0.009f, 0.006f), 0.003f, dark);
            Box("Mic2", v, new Vector3(-0.05f, 0.04f, FaceZ + 0.001f), new Vector3(0.012f, 0.009f, 0.006f), 0.003f, dark);
        }

        private void BuildTop(Transform v, Material silver, Material red, Material chrome, Material rubber, Material body)
        {
            // Piano keys (the second one = PLAY, pressed while the music plays).
            for (int i = 0; i < 6; i++)
            {
                var k = Box("Key", v, new Vector3(0.115f - i * 0.026f, H + 0.004f, 0.022f), new Vector3(0.022f, 0.012f, 0.05f), 0.004f, i == 0 ? red : silver);
                if (i == 1) _playKey = k;
            }
            // Knobs: volume and bass, turned by the settings.
            var knob = new List<Vector2> { new(0f, 0.016f) };
            BoomboxMeshes.Round(knob, new Vector2(0f, 0.016f), new Vector2(0.0125f, 0.012f), 5, true);
            knob.Add(new Vector2(0.0125f, 0.012f));
            knob.Add(new Vector2(0.0125f, 0f));
            var knobMesh = BoomboxMeshes.Lathe("knob", knob, 32);
            Transform Knob(float x)
            {
                var k = Mesh("Knob", v, knobMesh, silver, new Vector3(x, H, 0.022f), Quaternion.Euler(-90f, 0f, 0f));
                Box("Mark", k, new Vector3(0f, 0.008f, 0.0163f), new Vector3(0.0022f, 0.008f, 0.0012f), 0.0005f, red);
                return k;
            }
            _knobVol = Knob(-0.065f);
            _knobBass = Knob(-0.1f);

            // Handle: chrome bar bent down into two posts, black rubber grip in the middle.
            var path = new List<Vector3>();
            const float hx = 0.2f, hy = 0.075f, br = 0.028f;
            path.Add(new Vector3(-hx, 0f, 0f));
            BoomboxMeshes.Arc(path, new Vector3(-hx + br, hy - br, 0f), br, 180f, 90f, 8);
            BoomboxMeshes.Arc(path, new Vector3(hx - br, hy - br, 0f), br, 90f, 0f, 8);
            path.Add(new Vector3(hx, 0f, 0f));
            Mesh("Handle", v, BoomboxMeshes.Tube("handle", path, 0.0085f, 16), chrome, new Vector3(0f, H, -0.01f));
            Mesh("Grip", v, BoomboxMeshes.Tube("grip", new List<Vector3> { new(-0.11f, 0f, 0f), new(0.11f, 0f, 0f) }, 0.0135f, 18), rubber, new Vector3(0f, H + hy, -0.01f));
            foreach (float sx in new[] { -1f, 1f })
                Box("Post", v, new Vector3(sx * hx, H + 0.008f, -0.01f), new Vector3(0.034f, 0.02f, 0.036f), 0.007f, body);

            // Telescopic antenna on a swivel: three thinner sections and a ball tip.
            var ant = new GameObject("Antenna").transform;
            ant.SetParent(v, false);
            ant.localPosition = new Vector3(-0.245f, H + 0.006f, -0.05f);
            ant.localRotation = Quaternion.Euler(-10f, 0f, 26f);
            var swivel = new List<Vector2>();
            BoomboxMeshes.Round(swivel, new Vector2(0f, 0.008f), new Vector2(0.008f, 0f), 6, true);
            Mesh("Swivel", ant, BoomboxMeshes.Lathe("swivel", swivel, 24), chrome, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
            float[] rad = { 0.0042f, 0.0033f, 0.0025f };
            float y0 = 0f;
            for (int i = 0; i < rad.Length; i++)
            {
                Mesh("Section", ant, BoomboxMeshes.Tube("ant" + i, new List<Vector3> { new(0f, 0f, 0f), new(0f, 0.15f, 0f) }, rad[i], 10), chrome, new Vector3(0f, y0, 0f));
                y0 += 0.135f;
            }
            var tip = new List<Vector2>();
            BoomboxMeshes.Round(tip, new Vector2(0f, 0.005f), new Vector2(0.005f, 0f), 5, true);
            BoomboxMeshes.Round(tip, new Vector2(0.005f, 0f), new Vector2(0f, -0.005f), 5, false);
            Mesh("Tip", ant, BoomboxMeshes.Lathe("anttip", tip, 16), chrome, new Vector3(0f, y0 + 0.017f, 0f), Quaternion.Euler(-90f, 0f, 0f));

            // Rubber feet.
            var foot = new List<Vector2>();
            BoomboxMeshes.Round(foot, new Vector2(0f, 0.006f), new Vector2(0.014f, 0f), 4, true);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Mesh("Foot", v, BoomboxMeshes.Lathe("foot", foot, 20), rubber, new Vector3(sx * (W * 0.5f - 0.05f), 0.0055f, sz * 0.045f), Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
