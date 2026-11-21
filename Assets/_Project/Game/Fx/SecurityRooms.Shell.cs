using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Walls, floor, the monitor wall, the control console with the level buttons, the room lights, the door sign.</summary>
    public partial class SecurityRooms
    {
        private static readonly Color WallPaint = new Color(0.62f, 0.66f, 0.6f);     // tired institutional green-grey
        private static readonly Color DarkPlastic = new Color(0.07f, 0.07f, 0.08f);
        private static readonly Color SteelGrey = new Color(0.42f, 0.44f, 0.46f);

        private void BuildShell(Transform root, int lv, float cx, float cz, float y0)
        {
            var wall = Surf.T(SecurityTextures.Kind.Concrete, 2.4f, 0.05f);
            Box(root, "WallW", new Vector3(X0, y0 + Height * 0.5f, (Z0 + GarageWallZ) * 0.5f), new Vector3(Wall, Height, GarageWallZ - Z0), WallPaint, true, null, wall);
            Box(root, "WallE", new Vector3(X1, y0 + Height * 0.5f, (Z0 + GarageWallZ) * 0.5f), new Vector3(Wall, Height, GarageWallZ - Z0), WallPaint, true, null, wall);
            Box(root, "WallS1", new Vector3((X0 + DoorA) * 0.5f, y0 + Height * 0.5f, Z0), new Vector3(DoorA - X0, Height, Wall), WallPaint, true, null, wall);
            Box(root, "WallS2", new Vector3((DoorB + X1) * 0.5f, y0 + Height * 0.5f, Z0), new Vector3(X1 - DoorB, Height, Wall), WallPaint, true, null, wall);
            Box(root, "Lintel", new Vector3((DoorA + DoorB) * 0.5f, y0 + 2.1f + (Height - 2.1f) * 0.5f, Z0), new Vector3(DoorB - DoorA, Height - 2.1f, Wall), WallPaint, true, null, wall);
            Box(root, "NorthLining", new Vector3(cx, y0 + Height * 0.5f, Z1 - 0.05f), new Vector3(X1 - X0, Height, 0.1f), new Color(0.3f, 0.32f, 0.3f), true, null, wall);
            Box(root, "Floor", new Vector3(cx, y0 + 0.02f, cz), new Vector3(X1 - X0, 0.04f, Z1 - Z0), new Color(0.5f, 0.52f, 0.5f), true, null, Surf.T(SecurityTextures.Kind.Linoleum, 1.6f, 0.25f));
            Box(root, "Baffle", new Vector3(DoorA - 0.1f, y0 + Height * 0.5f, Z0 + 1.4f), new Vector3(Wall, Height, 2.4f), WallPaint, true, null, wall);

            // Door frame (steel), skirting boards, a lower band of darker paint (scuffed by chairs).
            var frame = new Color(0.25f, 0.27f, 0.28f);
            Box(root, "DoorFrameL", new Vector3(DoorA + 0.03f, y0 + 1.05f, Z0), new Vector3(0.06f, 2.1f, Wall + 0.04f), frame, false, null, Surf.Shiny(0.3f, 0.6f));
            Box(root, "DoorFrameR", new Vector3(DoorB - 0.03f, y0 + 1.05f, Z0), new Vector3(0.06f, 2.1f, Wall + 0.04f), frame, false, null, Surf.Shiny(0.3f, 0.6f));
            Box(root, "DoorFrameT", new Vector3((DoorA + DoorB) * 0.5f, y0 + 2.08f, Z0), new Vector3(DoorB - DoorA, 0.06f, Wall + 0.04f), frame, false, null, Surf.Shiny(0.3f, 0.6f));
            var skirt = new Color(0.16f, 0.17f, 0.17f);
            Box(root, "SkirtW", new Vector3(X0 + 0.11f, y0 + 0.06f, cz), new Vector3(0.02f, 0.1f, Z1 - Z0 - 0.2f), skirt, false);
            Box(root, "SkirtE", new Vector3(X1 - 0.11f, y0 + 0.06f, cz), new Vector3(0.02f, 0.1f, Z1 - Z0 - 0.2f), skirt, false);
            Box(root, "SkirtS", new Vector3((X0 + DoorA) * 0.5f, y0 + 0.06f, Z0 + 0.11f), new Vector3(DoorA - X0 - 0.2f, 0.1f, 0.02f), skirt, false);
            var band = new Color(0.34f, 0.4f, 0.36f);
            Box(root, "BandW", new Vector3(X0 + 0.105f, y0 + 0.55f, cz), new Vector3(0.012f, 0.9f, Z1 - Z0 - 0.2f), band, false, null, wall);
            Box(root, "BandS", new Vector3((X0 + DoorA) * 0.5f, y0 + 0.55f, Z0 + 0.105f), new Vector3(DoorA - X0 - 0.2f, 0.9f, 0.012f), band, false, null, wall);

            // Fluorescent ceiling fittings (switched off: grey tubes, no light).
            for (int i = 0; i < 2; i++)
            {
                Vector3 f = new Vector3(cx - 2.5f + i * 5f, y0 + Height - 0.04f, cz - 0.4f);
                Box(root, "Fitting", f, new Vector3(1.3f, 0.06f, 0.3f), new Color(0.75f, 0.76f, 0.74f), false, null, Surf.Shiny(0.3f, 0.3f));
                for (int k = 0; k < 2; k++)
                    Prim(root, PrimitiveType.Cylinder, "Tube", f + new Vector3(0f, -0.05f, -0.07f + k * 0.14f), new Vector3(0.035f, 0.6f, 0.035f), new Color(0.55f, 0.56f, 0.55f), new Vector3(0f, 0f, 90f), false, Surf.Shiny(0.6f));
            }
        }

        // ================================================================ monitor wall

        private void BuildMonitorWall(Transform root, int lv, float cx, float y0)
        {
            // Steel rails holding the screens.
            foreach (float ry in new[] { 0.98f, 1.71f, 2.44f })
                Box(root, "Rail", new Vector3(cx + 0.3f, y0 + ry, Z1 - 0.115f), new Vector3(5.6f, 0.06f, 0.03f), SteelGrey, false, null, Surf.T(SecurityTextures.Kind.Metal, 1f, 0.35f, 0.7f));
            foreach (float px in new[] { -2.75f, 2.75f })
                Box(root, "Post", new Vector3(cx + 0.3f + px, y0 + 1.7f, Z1 - 0.115f), new Vector3(0.06f, 1.9f, 0.03f), SteelGrey, false, null, Surf.T(SecurityTextures.Kind.Metal, 1f, 0.35f, 0.7f));

            int m = 0;
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++, m++)
            {
                Vector3 c = new Vector3(cx - 1.8f + col * 1.8f + 0.3f, y0 + (row == 0 ? 1.28f : 2.14f), Z1 - 0.2f);
                // Housing: back box + a slightly larger front bezel with a chin.
                Box(root, "MonitorBack", c + new Vector3(0f, 0f, 0.06f), new Vector3(1.5f, 0.78f, 0.1f), DarkPlastic, false, null, Surf.Shiny(0.25f));
                Box(root, "Bezel", c + new Vector3(0f, 0f, 0.0f), new Vector3(1.62f, 0.9f, 0.035f), DarkPlastic, false, null, Surf.Shiny(0.4f));
                Box(root, "Chin", c + new Vector3(0f, -0.43f, -0.012f), new Vector3(1.62f, 0.04f, 0.02f), new Color(0.12f, 0.12f, 0.13f), false, null, Surf.Shiny(0.5f));
                Led(root, "PowerLed", c + new Vector3(0.72f, -0.43f, -0.024f), new Vector3(0.012f, 0.008f, 0.004f), new Color(0.1f, 1.2f, 0.3f));

                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Screen" + m;
                Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(root, false);
                q.transform.position = c + new Vector3(0f, 0.01f, -0.022f);
                q.transform.localScale = new Vector3(1.5f, 0.8f, 1f);
                var r = q.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                var mat = _monitorShader != null ? new Material(_monitorShader) : new Material(Shader.Find("Unlit/Texture"));
                mat.mainTexture = _slots[m].Rt;
                mat.SetTexture("_Osd", _slots[m].Osd);
                r.sharedMaterial = mat;
                _slots[m].Mats.Add(mat);
                _slots[m].ScreenPos[lv] = c;
                // Sticky notes on some bezels.
                if ((m + lv) % 3 == 0)
                    Box(root, "StickyNote", c + new Vector3(0.7f, 0.37f, -0.02f), new Vector3(0.08f, 0.08f, 0.003f), (m % 2 == 0) ? new Color(0.95f, 0.85f, 0.3f) : new Color(0.95f, 0.5f, 0.7f), false, new Vector3(0f, 0f, 8f));
            }
        }

        // ================================================================ desk console + level buttons

        private Texture2D _panelTex;

        private void BuildConsole(Transform root, int lv, float cx, float y0)
        {
            float top = y0 + 0.77f, dz = Z1 - 0.65f;
            var lam = Surf.T(SecurityTextures.Kind.Laminate, 1.2f, 0.3f);
            Box(root, "Desk", new Vector3(cx, y0 + 0.745f, dz), new Vector3(9.4f, 0.05f, 1.0f), new Color(0.85f, 0.8f, 0.72f), true, null, lam);
            Box(root, "DeskEdge", new Vector3(cx, y0 + 0.745f, dz - 0.505f), new Vector3(9.42f, 0.055f, 0.015f), new Color(0.1f, 0.1f, 0.1f), false, null, Surf.Shiny(0.3f));
            Box(root, "Modesty", new Vector3(cx, y0 + 0.42f, dz + 0.47f), new Vector3(9.2f, 0.6f, 0.02f), new Color(0.3f, 0.3f, 0.32f), false, null, lam);
            // Drawer pedestals at both ends, steel legs between.
            foreach (float px in new[] { -4.2f, 4.2f })
            {
                Vector3 pp = new Vector3(cx + px, y0 + 0.36f, dz);
                Box(root, "Pedestal", pp, new Vector3(0.9f, 0.72f, 0.9f), new Color(0.32f, 0.33f, 0.35f), true, null, Surf.T(SecurityTextures.Kind.Metal, 0.8f, 0.3f, 0.4f));
                for (int d = 0; d < 3; d++)
                {
                    Vector3 dp = pp + new Vector3(0f, 0.22f - d * 0.23f, -0.455f);
                    Box(root, "Drawer", dp, new Vector3(0.84f, 0.2f, 0.012f), new Color(0.36f, 0.37f, 0.39f), false, null, Surf.Shiny(0.3f, 0.4f));
                    Box(root, "Handle", dp + new Vector3(0f, 0.05f, -0.015f), new Vector3(0.22f, 0.02f, 0.02f), new Color(0.6f, 0.62f, 0.64f), false, null, Surf.Shiny(0.6f, 0.9f));
                }
            }
            foreach (float px in new[] { -1.5f, 1.5f })
                Box(root, "DeskLeg", new Vector3(cx + px, y0 + 0.36f, dz - 0.42f), new Vector3(0.06f, 0.72f, 0.06f), SteelGrey, true, null, Surf.Shiny(0.4f, 0.8f));

            // Control panel lying on the desk: baked labels, three big buttons choosing the camera level.
            if (_panelTex == null) _panelTex = MakePanelTexture();
            Vector3 pc = new Vector3(cx, top + 0.03f, Z1 - 0.97f);
            Box(root, "ButtonPanel", pc, new Vector3(2.6f, 0.06f, 0.36f), new Color(0.2f, 0.21f, 0.22f), false, null, Surf.T(SecurityTextures.Kind.Metal, 1f, 0.35f, 0.5f));
            Sign(root, "PanelFace", pc + Vector3.up * 0.031f, new Vector2(2.56f, 0.34f), Vector3.up, _panelTex, 0.3f);
            for (int f = 0; f < Levels; f++)
            {
                Vector3 bp = new Vector3(cx - 1.0f + f * 1.0f, y0 + 0.8f, Z1 - 1.0f);
                Prim(root, PrimitiveType.Cylinder, "ButtonRing", new Vector3(bp.x, top + 0.07f, bp.z + 0.05f), new Vector3(0.2f, 0.012f, 0.2f), new Color(0.55f, 0.56f, 0.58f), null, false, Surf.Shiny(0.6f, 0.9f));
                var b = Prim(root, PrimitiveType.Cylinder, "Button" + f, new Vector3(bp.x, top + 0.09f, bp.z + 0.05f), new Vector3(0.15f, 0.025f, 0.15f), new Color(0.3f, 0.05f, 0.04f), null, true, Surf.Glow(new Color(0.01f, 0f, 0f)));
                _buttons.Add((bp + Vector3.up * 0.05f, f));
                _buttonRenderers.Add(b.GetComponent<Renderer>());
            }
        }

        private static Texture2D MakePanelTexture()
        {
            var ink = new Color(0.85f, 0.87f, 0.85f);
            var tb = new TextureBaker(768, 102, new Color(0.13f, 0.14f, 0.15f));
            tb.Frame(0.5f, 0.5f, 0.985f, 0.9f, 0.03f, new Color(0.35f, 0.36f, 0.37f));
            for (int f = 0; f < 3; f++)
            {
                float x = 0.5f + (f - 1) * (1f / 2.6f);
                tb.Text("POZIOM " + LevelName(f), x, 0.17f, 0.16f, ink);
            }
            tb.Text("KAMERY", 0.06f, 0.75f, 0.13f, new Color(0.95f, 0.75f, 0.2f), TextAnchor.MiddleLeft);
            tb.Text("ZASILANIE", 0.94f, 0.75f, 0.11f, ink, TextAnchor.MiddleRight);
            foreach (float sx in new[] { 0.015f, 0.985f })
            foreach (float sy in new[] { 0.12f, 0.88f })
                tb.Circle(sx, sy, 0.04f, new Color(0.5f, 0.5f, 0.52f));
            return tb.Finish("SecPanel", 0.15f, 5);
        }

        private void PaintButton(int i)
        {
            if (i < 0 || i >= _buttonRenderers.Count || _buttonRenderers[i] == null) return;
            bool on = i % Levels == _floor;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", on ? new Color(0.15f, 0.7f, 0.25f) : new Color(0.35f, 0.06f, 0.05f));
            mpb.SetColor("_EmissionColor", on ? new Color(0.25f, 1.6f, 0.45f) : new Color(0.25f, 0.02f, 0.01f));
            _buttonRenderers[i].SetPropertyBlock(mpb);
        }

        // ================================================================ lights

        private void BuildLights(Transform root, int lv, float cx, float y0)
        {
            // Light thrown by the screens: a wide soft spot into the room (chair and desk cast shadows)...
            var spill = AddLight(root, "ScreenSpill", new Vector3(cx + 0.3f, y0 + 1.7f, Z1 - 0.3f), LightType.Spot, 6.5f, 1.25f, new Color(0.62f, 0.9f, 0.8f), LightShadows.Soft);
            spill.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.42f, -1f));
            spill.spotAngle = 140f;
            spill.innerSpotAngle = 60f;
            spill.shadowStrength = 0.85f;
            spill.shadowNearPlane = 0.2f;
            _screenLightBase.Add((spill, spill.intensity, spill.color));
            // ...and the glow on the wall and the desk right under the monitors.
            var glow = AddLight(root, "ScreenGlow", new Vector3(cx + 0.3f, y0 + 1.5f, Z1 - 0.75f), LightType.Point, 2.3f, 0.45f, new Color(0.6f, 0.95f, 0.8f));
            _screenLightBase.Add((glow, glow.intensity, glow.color));
        }

        // ================================================================ sign over the door (outside)

        private Texture2D _signTex;

        private void BuildDoorSign(Transform root, int lv, float y0)
        {
            if (_signTex == null)
            {
                var tb = new TextureBaker(512, 128, new Color(0.07f, 0.13f, 0.34f));
                tb.Frame(0.5f, 0.5f, 0.97f, 0.88f, 0.05f, new Color(0.85f, 0.86f, 0.9f));
                tb.Text("SECURITY", 0.5f, 0.6f, 0.34f, Color.white);
                tb.Text("TYLKO PERSONEL", 0.5f, 0.22f, 0.12f, new Color(0.8f, 0.85f, 1f));
                _signTex = tb.Finish("SecSign", 0.35f, 2);
            }
            float doorX = (DoorA + DoorB) * 0.5f;
            Vector3 sp = new Vector3(doorX, y0 + 2.42f, Z0 - Wall * 0.5f - 0.025f);
            Box(root, "SignPlate", sp + new Vector3(0f, 0f, 0.008f), new Vector3(1.74f, 0.46f, 0.03f), new Color(0.75f, 0.76f, 0.8f), false, null, Surf.Shiny(0.4f, 0.6f));
            Sign(root, "SignFace", sp + new Vector3(0f, 0f, -0.009f), new Vector2(1.7f, 0.42f), Vector3.back, _signTex, 0.35f);
            // Small caged bulb over the sign, lighting just the sign and the crate.
            Vector3 lamp = sp + new Vector3(0f, 0.36f, -0.12f);
            Box(root, "SignLampArm", lamp + new Vector3(0f, 0f, 0.06f), new Vector3(0.04f, 0.04f, 0.12f), DarkPlastic, false);
            Led(root, "SignBulb", lamp, new Vector3(0.09f, 0.06f, 0.09f), new Color(1.6f, 1.5f, 1.2f), PrimitiveType.Sphere);
            var l = AddLight(root, "SignLamp", lamp + new Vector3(0f, -0.05f, -0.03f), LightType.Spot, 4.2f, 1.5f, new Color(1f, 0.92f, 0.78f));
            l.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -1f, -0.15f));
            l.spotAngle = 135f;
        }
    }
}
