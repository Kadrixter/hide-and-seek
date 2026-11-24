using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.Cinematics
{
    public sealed partial class ExitGate
    {
        const float StreetLight = 4.2f;
        Material _steel, _slat, _dark, _rubber, _hazard, _boxPaint, _chrome, _knobRed, _label, _asphalt, _curb, _concrete, _facade, _window, _lampHead;

        void Materials()
        {
            _steel = CineKit.Mat("GateSteel", new Color(0.46f, 0.48f, 0.5f), 0.75f, 0.42f);
            _slat = CineKit.Mat("GateSlat", new Color(0.55f, 0.57f, 0.58f), 0.7f, 0.38f);
            _dark = CineKit.Mat("GateDark", new Color(0.09f, 0.095f, 0.1f), 0.65f, 0.45f);
            _rubber = CineKit.Mat("GateRubber", new Color(0.03f, 0.03f, 0.03f), 0f, 0.25f);
            _hazard = CineKit.Mat("GateHazard", Color.white, 0.1f, 0.4f, StartLeverArt.Hazard);
            _hazard.mainTextureScale = new Vector2(14f, 1f);
            _boxPaint = CineKit.Mat("ElecBox", new Color(0.62f, 0.63f, 0.6f), 0.35f, 0.5f);
            _chrome = CineKit.Mat("LeverChrome", new Color(0.8f, 0.82f, 0.85f), 0.95f, 0.85f);
            _knobRed = CineKit.Mat("LeverKnob", new Color(0.8f, 0.06f, 0.04f), 0.1f, 0.85f);
            _label = CineKit.Mat("ElecLabel", Color.white, 0f, 0.3f,
                StartLeverArt.Label(new[] { "BRAMA", "400 V" }, 96, 64, 2, new Color(0.96f, 0.76f, 0.1f), new Color(0.06f, 0.06f, 0.06f)));
            _lampMat = CineKit.Glow("ElecLamp", new Color(1f, 0.12f, 0.08f), 2.2f);
            _asphalt = CineKit.Mat("StreetAsphalt", new Color(0.06f, 0.062f, 0.068f), 0f, 0.72f);
            _curb = CineKit.Mat("StreetCurb", new Color(0.3f, 0.3f, 0.29f), 0f, 0.15f);
            _concrete = CineKit.Mat("StreetWall", new Color(0.17f, 0.17f, 0.18f), 0f, 0.1f);
            _facade = CineKit.Mat("StreetFacade", new Color(0.07f, 0.07f, 0.08f), 0f, 0.15f);
            _window = CineKit.Glow("StreetWindow", new Color(1f, 0.72f, 0.38f), 1.4f);
            _lampHead = CineKit.Glow("StreetLampHead", new Color(1f, 0.7f, 0.35f), 3f);
        }

        void Build()
        {
            Materials();
            BuildShutter();
            BuildBox();
            if (StreetVisible) BuildStreet();
        }

        void BuildShutter()
        {
            var t = transform;
            // Guide rails in the jambs (U channels), the curtain ends run inside them.
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (HalfW + 0.02f);
                CineKit.Box("Rail", t, new Vector3(x, OpenH * 0.5f, CurtainZ), new Vector3(0.06f, OpenH, 0.14f), _dark, 0.008f);
                CineKit.Box("RailLip", t, new Vector3(x - s * 0.035f, OpenH * 0.5f, CurtainZ + 0.06f), new Vector3(0.025f, OpenH, 0.02f), _steel, 0.005f);
                CineKit.Box("RailLipBack", t, new Vector3(x - s * 0.035f, OpenH * 0.5f, CurtainZ - 0.06f), new Vector3(0.025f, OpenH, 0.02f), _steel, 0.005f);
                // Bolted foot plates.
                CineKit.Box("RailFoot", t, new Vector3(x, 0.04f, CurtainZ), new Vector3(0.12f, 0.08f, 0.2f), _dark, 0.01f);
            }
            // Steel threshold with a hazard strip.
            CineKit.Box("Threshold", t, new Vector3(0f, 0.006f, -0.15f), new Vector3(HalfW * 2f, 0.012f, 0.5f), _steel, 0.004f);
            CineKit.Quad("ThresholdHazard", t, new Vector3(0f, 0.0125f, 0.04f), new Vector2(HalfW * 2f, 0.12f), _hazard, Quaternion.Euler(-90f, 0f, 0f));

            // Curtain: slats that disappear into the lintel above the opening, and a heavy bottom bar.
            _slatH = 0.09f;
            int n = Mathf.CeilToInt(OpenH / _slatH) + 1;
            _slats = new Transform[n];
            var slatMesh = VendingMeshes.RoundedBox(new Vector3(HalfW * 2f + 0.06f, _slatH, 0.028f), 0.012f);
            for (int i = 0; i < n; i++)
            {
                var s = CineKit.Node("Slat" + i, t, Vector3.zero);
                s.gameObject.AddComponent<MeshFilter>().sharedMesh = slatMesh;
                s.gameObject.AddComponent<MeshRenderer>().sharedMaterial = i % 7 == 3 ? _steel : _slat;
                _slats[i] = s;
            }
            _bottomBar = CineKit.Node("BottomBar", t, Vector3.zero);
            CineKit.Box("Bar", _bottomBar, Vector3.zero, new Vector3(HalfW * 2f + 0.06f, 0.09f, 0.07f), _dark, 0.012f);
            CineKit.Box("Seal", _bottomBar, new Vector3(0f, -0.05f, 0f), new Vector3(HalfW * 2f, 0.025f, 0.05f), _rubber, 0.008f);
            CineKit.Quad("BarHazard", _bottomBar, new Vector3(0f, 0f, 0.0355f), new Vector2(HalfW * 2f, 0.06f), _hazard);
            for (int s = -1; s <= 1; s += 2)
                CineKit.Box("Handle", _bottomBar, new Vector3(s * 1.4f, 0.0f, 0.06f), new Vector3(0.2f, 0.035f, 0.03f), _steel, 0.01f);

            var block = CineKit.Node("GateBlock", t, new Vector3(0f, 1.6f, CurtainZ));
            _block = block.gameObject.AddComponent<BoxCollider>();
            _block.size = new Vector3(HalfW * 2f, 3.2f, 0.3f);
        }

        void BuildBox()
        {
            var t = CineKit.Node("ElectricBox", transform, BoxPos);
            const float w = 0.34f, h = 0.46f, d = 0.14f;
            CineKit.Box("Back", t, new Vector3(0f, 0f, 0.006f), new Vector3(w + 0.04f, h + 0.04f, 0.012f), _dark, 0.004f);
            CineKit.Box("Body", t, new Vector3(0f, 0f, d * 0.5f), new Vector3(w, h, d), _boxPaint, 0.012f);
            // Door with a seam, hinges on the right, a quarter-turn lock.
            CineKit.Box("Door", t, new Vector3(-0.005f, 0f, d + 0.004f), new Vector3(w - 0.03f, h - 0.03f, 0.008f), _boxPaint, 0.004f);
            for (int k = -1; k <= 1; k += 2)
                CineKit.Cyl("Hinge", t, new Vector3(-w * 0.5f + 0.004f, k * 0.15f, d + 0.004f), 0.014f, 0.05f, 1, _chrome);
            CineKit.Cyl("Lock", t, new Vector3(w * 0.5f - 0.05f, -0.02f, d + 0.01f), 0.022f, 0.012f, 2, _dark);
            CineKit.Quad("Label", t, new Vector3(-0.02f, 0.13f, d + 0.0095f), new Vector2(0.15f, 0.1f), _label);
            // Status lamp, top left, with a little light of its own.
            CineKit.Cyl("LampBezel", t, new Vector3(-0.1f, 0.17f, d + 0.012f), 0.034f, 0.012f, 2, _dark);
            CineKit.Sphere("LampGlass", t, new Vector3(-0.1f, 0.17f, d + 0.02f), 0.024f, _lampMat);
            _boxLamp = CineKit.Node("LampLight", t, new Vector3(-0.1f, 0.17f, d + 0.08f)).gameObject.AddComponent<Light>();
            _boxLamp.type = LightType.Point;
            _boxLamp.range = 0.9f;
            _boxLamp.shadows = LightShadows.None;
            // Conduit up to the ceiling, cable gland at the top of the box.
            CineKit.Cyl("Gland", t, new Vector3(0.06f, h * 0.5f + 0.02f, d * 0.5f), 0.04f, 0.04f, 1, _dark);
            CineKit.Cyl("Conduit", t, new Vector3(0.06f, (3.0f - BoxPos.y + h * 0.5f) * 0.5f, d * 0.5f), 0.028f, 3.0f - BoxPos.y - h * 0.5f, 1, _steel);
            for (float y = 0.55f; y < 1.7f; y += 0.55f)
                CineKit.Box("Clip", t, new Vector3(0.06f, h * 0.5f + y, 0.03f), new Vector3(0.05f, 0.02f, 0.05f), _dark, 0.004f);

            // The small lever on the left edge of the front (the killer stands to its left, right hand on it): a
            // bracket, the pivot, a chrome rod and a red ball, thrown from up-and-out to down-and-out (and torn off).
            Vector3 pv = LeverPivotInBox;
            CineKit.Box("LeverPlate", t, new Vector3(pv.x, pv.y, d + 0.006f), new Vector3(0.05f, 0.16f, 0.012f), _dark, 0.004f);
            CineKit.Box("LeverBracket", t, new Vector3(pv.x, pv.y, d + 0.02f), new Vector3(0.036f, 0.04f, 0.026f), _steel, 0.004f);
            _leverPivot = CineKit.Node("LeverPivot", t, pv);
            CineKit.Cyl("Axle", _leverPivot, Vector3.zero, 0.02f, 0.05f, 0, _chrome);
            _rod = CineKit.Cyl("Rod", _leverPivot, new Vector3(0f, 0.065f, 0f), 0.012f, 0.13f, 1, _chrome);
            _knob = CineKit.Sphere("Knob", _leverPivot, new Vector3(0f, 0.135f, 0f), 0.034f, _knobRed);
            // What is left after the killer tears the handle off in the intro: a short stub with a bent, torn end.
            _stub = CineKit.Node("Stub", _leverPivot, Vector3.zero);
            CineKit.Cyl("StubRod", _stub, new Vector3(0f, 0.017f, 0f), 0.012f, 0.034f, 1, _chrome);
            CineKit.Box("StubTear", _stub, new Vector3(0.002f, 0.036f, 0.001f), new Vector3(0.014f, 0.01f, 0.012f), _dark, 0.002f,
                Quaternion.Euler(24f, 0f, -18f));
            // Up / down marks next to the lever.
            CineKit.Box("MarkUp", t, new Vector3(pv.x + 0.032f, pv.y + 0.07f, d + 0.0085f), new Vector3(0.012f, 0.012f, 0.002f), _lampMat, 0.001f);
            CineKit.Box("MarkDown", t, new Vector3(pv.x + 0.032f, pv.y - 0.07f, d + 0.0085f), new Vector3(0.012f, 0.012f, 0.002f), _knobRed, 0.001f);
        }

        /// <summary>
        /// The torn-off handle (rod with the red ball, ragged end) as a loose prop under <paramref name="parent"/>:
        /// the ball at the origin, the rod along local +Z.
        /// </summary>
        public Transform MakeLooseHandle(Transform parent)
        {
            var h = CineKit.Node("TornHandle", parent, Vector3.zero);
            CineKit.Sphere("Knob", h, Vector3.zero, 0.034f, _knobRed);
            CineKit.Cyl("Rod", h, new Vector3(0f, 0f, 0.065f), 0.012f, 0.13f, 2, _chrome);
            CineKit.Box("Tear", h, new Vector3(0.001f, -0.001f, 0.132f), new Vector3(0.015f, 0.012f, 0.012f), _dark, 0.002f,
                Quaternion.Euler(-20f, 30f, 0f));
            return h;
        }

        void BuildStreet()
        {
            var t = CineKit.Node("Street", transform, Vector3.zero);
            // Short driveway between retaining walls, a pavement, the street and a dark building across it.
            CineKit.BoxMinMax("Drive", t, new Vector3(-HalfW - 0.4f, -0.2f, -12f), new Vector3(HalfW + 0.4f, 0.0f, -0.4f), _asphalt, 0.002f);
            CineKit.BoxMinMax("Street", t, new Vector3(-24f, -0.2f, -24f), new Vector3(24f, -0.02f, -12f), _asphalt, 0.002f);
            CineKit.BoxMinMax("CurbL", t, new Vector3(-24f, 0f, -12.4f), new Vector3(-HalfW - 0.4f, 0.14f, -12f), _curb, 0.02f);
            CineKit.BoxMinMax("CurbR", t, new Vector3(HalfW + 0.4f, 0f, -12.4f), new Vector3(24f, 0.14f, -12f), _curb, 0.02f);
            for (int s = -1; s <= 1; s += 2)
                CineKit.BoxMinMax("Retaining" + s, t, new Vector3(s * (HalfW + 0.4f), 0f, -12f), new Vector3(s * (HalfW + 0.7f), 3.2f, -0.4f), _concrete, 0.02f);
            CineKit.BoxMinMax("Facade", t, new Vector3(-24f, -0.02f, -26f), new Vector3(24f, 14f, -24f), _facade, 0.01f);
            // Lit windows on the building across the street (a few dark, a few warm).
            var rng = new System.Random(1123);
            for (int row = 0; row < 3; row++)
            for (int col = -5; col <= 5; col++)
            {
                if (rng.NextDouble() < 0.55) continue;
                var q = CineKit.Quad("Window", t, new Vector3(col * 2.6f + 0.6f, 3.4f + row * 3.1f, -23.98f), new Vector2(1.1f, 1.3f), _window);
                q.gameObject.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            // Street lamp on the far pavement, orange, shining back into the driveway.
            Vector3 pole = new Vector3(2.6f, 0f, -13.2f);
            CineKit.Cyl("LampPole", t, pole + Vector3.up * 2.6f, 0.12f, 5.2f, 1, _dark);
            CineKit.Box("LampArm", t, pole + new Vector3(0f, 5.15f, 0.55f), new Vector3(0.08f, 0.08f, 1.2f), _dark, 0.01f);
            CineKit.Box("LampHead", t, pole + new Vector3(0f, 5.05f, 1.1f), new Vector3(0.36f, 0.12f, 0.5f), _lampHead, 0.03f);
            _street = CineKit.Node("StreetLight", t, pole + new Vector3(0f, 4.9f, 1.1f), Quaternion.Euler(62f, 0f, 0f)).gameObject.AddComponent<Light>();
            _street.type = LightType.Spot;
            _street.spotAngle = 105f;
            _street.range = 26f;
            _street.color = new Color(1f, 0.66f, 0.32f);
            _street.shadows = LightShadows.Soft;
            // Spill on the driveway asphalt (cheap fake bounce).
            var fill = CineKit.Node("StreetFill", t, new Vector3(0f, 1.6f, -5f)).gameObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 7f;
            fill.intensity = 0.8f;
            fill.color = new Color(1f, 0.6f, 0.35f);
            fill.shadows = LightShadows.None;
            _streetFill = fill;
        }

        Light _streetFill;
    }
}
