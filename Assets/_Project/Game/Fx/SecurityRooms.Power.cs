using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Camera power of the rooms (MatchManager.CamPower): on the east wall next to the desk end a steel power box
    /// "ZASILANIE KAMER 12V" with a lever switch and a 5-segment charge gauge, under it on the floor a battery tray with
    /// a red and a black cable and their clamps. A battery in the tray shows with the clamps on its terminals. Rooms
    /// without power: the monitors are black with a faint "BRAK ZASILANIA" (an empty battery: "AKUMULATOR
    /// ROZŁADOWANY"), no screen light, and with no room powered the feeds are not rendered and the garage cameras'
    /// LEDs stay dark.
    /// </summary>
    public partial class SecurityRooms
    {
        private const float PowerZ = Z1 - 0.75f;              // between the sideboard and the north lining, next to the desk pedestal
        private const float PowerWallX = X1 - Wall * 0.5f;   // inner face of the east wall
        private const int GaugeSegments = 5;

        private sealed class PowerParts
        {
            public Transform Lever, Battery, ClampPlus, ClampMinus, CablePlus, CableMinus;
            public Vector3 CableFromPlus, CableFromMinus, RestPlus, RestMinus;
            public readonly Renderer[] Gauge = new Renderer[GaugeSegments];
            public float LeverAngle = 999f;
            public int Shown = -1;          // screens: -1 unknown, 0 no power, 1 powered, 2 battery empty
            public bool BatteryShown = true;
            public int GaugeKey = -1;
        }

        private readonly PowerParts[] _power = new PowerParts[Levels];
        private static Texture2D _powerLabel, _osdNoPower, _osdEmpty;
        private readonly MaterialPropertyBlock _gaugeMpb = new();
        private bool _ledsDark;

        /// <summary>Where the battery tray of room <paramref name="lv"/> is (interaction point, a little above it).</summary>
        public static Vector3 PowerSlotPoint(int lv) => new Vector3(PowerWallX - 0.3f, FloorY(lv) + 0.3f, PowerZ);

        /// <summary>Where the camera power switch of room <paramref name="lv"/> is.</summary>
        public static Vector3 PowerSwitchPoint(int lv) => new Vector3(PowerWallX - 0.15f, FloorY(lv) + 1.2f, PowerZ - 0.05f);

        /// <summary>A point inside security room <paramref name="lv"/>.</summary>
        public static bool InRoom(Vector3 v, int lv) =>
            v.x >= X0 && v.x <= X1 && v.z >= Z0 && v.z <= Z1 && v.y > FloorY(lv) - 0.5f && v.y < FloorY(lv) + 2.5f;

        /// <summary>The monitors of room <paramref name="lv"/> have power.</summary>
        public static bool RoomPowered(int lv)
        {
            var mm = MatchManager.Instance;
            return mm != null && mm.CamPower.Value.Powered(lv);
        }

        private static bool AnyRoomPowered
        {
            get
            {
                var mm = MatchManager.Instance;
                return mm != null && mm.CamPower.Value.AnyPowered;
            }
        }

        // ================================================================ build

        private void BuildPower(Transform root, int lv, float y0)
        {
            var pp = new PowerParts();
            _power[lv] = pp;
            var steel = new Color(0.36f, 0.4f, 0.44f);
            var metal = Surf.T(SecurityTextures.Kind.Metal, 0.5f, 0.35f, 0.6f);
            float wx = PowerWallX;

            // Power box on the wall, its label, the lever switch and the gauge.
            Vector3 box = new Vector3(wx - 0.06f, y0 + 1.22f, PowerZ);
            Box(root, "PowerBox", box, new Vector3(0.12f, 0.42f, 0.32f), steel, false, null, metal);
            Box(root, "PowerBoxLid", box + new Vector3(-0.061f, 0f, 0f), new Vector3(0.004f, 0.4f, 0.3f), steel * 1.08f, false, null, Surf.Shiny(0.35f, 0.5f));
            if (_powerLabel == null) _powerLabel = MakePowerLabel();
            Sign(root, "PowerLabel", box + new Vector3(-0.0645f, 0.15f, 0f), new Vector2(0.27f, 0.075f), Vector3.left, _powerLabel, 0.2f);
            Vector3 lever = PowerSwitchPoint(lv);
            Box(root, "SwitchPlate", new Vector3(wx - 0.124f, lever.y, lever.z), new Vector3(0.006f, 0.12f, 0.07f), new Color(0.12f, 0.12f, 0.13f), false, null, Surf.Shiny(0.4f));
            var pivot = new GameObject("PowerLever").transform;
            pivot.SetParent(root, false);
            pivot.position = new Vector3(wx - 0.128f, lever.y, lever.z);
            var handle = Prim(pivot, PrimitiveType.Cube, "LeverArm", pivot.position + new Vector3(-0.045f, 0f, 0f), new Vector3(0.09f, 0.018f, 0.018f), new Color(0.55f, 0.56f, 0.58f), null, false, Surf.Shiny(0.6f, 0.9f));
            handle.transform.SetParent(pivot, true);
            var knob = Prim(pivot, PrimitiveType.Sphere, "LeverKnob", pivot.position + new Vector3(-0.095f, 0f, 0f), Vector3.one * 0.032f, new Color(0.75f, 0.08f, 0.05f), null, false, Surf.Shiny(0.5f));
            knob.transform.SetParent(pivot, true);
            pp.Lever = pivot;
            for (int k = 0; k < GaugeSegments; k++)
            {
                var g = Led(root, "Gauge" + k, new Vector3(wx - 0.123f, y0 + 1.1f + k * 0.032f, PowerZ + 0.09f), new Vector3(0.004f, 0.022f, 0.04f), new Color(0.05f, 0.3f, 0.08f));
                pp.Gauge[k] = g.GetComponent<Renderer>();
            }

            // Battery tray on the floor under it, the cables down the wall, the clamps.
            float fy = y0 + 0.04f;
            Vector3 tray = new Vector3(wx - 0.3f, fy, PowerZ);
            Box(root, "BatteryTray", tray + new Vector3(0f, 0.012f, 0f), new Vector3(0.3f, 0.024f, 0.38f), new Color(0.2f, 0.21f, 0.22f), false, null, metal);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(root, "TrayRim", tray + new Vector3(s * 0.145f, 0.035f, 0f), new Vector3(0.01f, 0.05f, 0.38f), new Color(0.24f, 0.25f, 0.26f), false, null, metal);
                Box(root, "TrayRim", tray + new Vector3(0f, 0.035f, s * 0.185f), new Vector3(0.3f, 0.05f, 0.01f), new Color(0.24f, 0.25f, 0.26f), false, null, metal);
            }
            var bat = CarBatteryModel.Create(root);
            bat.position = tray + new Vector3(0f, 0.024f + CarBatteryModel.HalfH, 0f);
            bat.rotation = Quaternion.Euler(0f, 180f, 0f); // terminals to the room side
            SetupRenderer(bat.GetComponent<Renderer>());
            pp.Battery = bat;
            var red = new Color(0.7f, 0.08f, 0.05f);
            var black = new Color(0.05f, 0.05f, 0.05f);
            foreach (var (s, col) in new[] { (1f, red), (-1f, black) })
            {
                Vector3 top = new Vector3(wx - 0.03f, box.y - 0.21f, PowerZ + s * 0.06f);
                Vector3 bottom = new Vector3(wx - 0.03f, fy + 0.04f, PowerZ + s * 0.06f);
                Prim(root, PrimitiveType.Cylinder, "PowerCable", (top + bottom) * 0.5f, new Vector3(0.012f, (top.y - bottom.y) * 0.5f, 0.012f), col);
                var cable = Prim(root, PrimitiveType.Cylinder, "PowerLead", bottom, new Vector3(0.012f, 0.1f, 0.012f), col).transform;
                var clamp = Box(root, "Clamp", bottom, new Vector3(0.05f, 0.022f, 0.028f), col, false, null, Surf.Shiny(0.45f)).transform;
                Vector3 rest = tray + new Vector3(0.1f, 0.06f, s * 0.16f);
                if (s > 0) { pp.CablePlus = cable; pp.ClampPlus = clamp; pp.CableFromPlus = bottom; pp.RestPlus = rest; }
                else { pp.CableMinus = cable; pp.ClampMinus = clamp; pp.CableFromMinus = bottom; pp.RestMinus = rest; }
            }
        }

        private static Texture2D MakePowerLabel()
        {
            var tb = new TextureBaker(256, 72, new Color(0.92f, 0.78f, 0.12f));
            tb.Frame(0.5f, 0.5f, 0.96f, 0.86f, 0.05f, new Color(0.1f, 0.1f, 0.1f));
            tb.Text("ZASILANIE KAMER", 0.5f, 0.62f, 0.3f, new Color(0.08f, 0.08f, 0.08f));
            tb.Text("AKUMULATOR 12V", 0.5f, 0.27f, 0.22f, new Color(0.15f, 0.15f, 0.15f));
            return tb.Finish("SecPowerLabel", 0.2f, 31);
        }

        private static Texture2D MakeOsd(string name, string line1, string line2)
        {
            var px = new Color32[OsdW * OsdH];
            var faint = new Color32(96, 104, 98, 255);
            if (line2 == null) OsdText(px, line1, (OsdW - CanFont.Width(line1, 2)) / 2, OsdH / 2 - 7, 2, faint);
            else
            {
                OsdText(px, line1, (OsdW - CanFont.Width(line1, 2)) / 2, OsdH / 2 + 4, 2, faint);
                OsdText(px, line2, (OsdW - CanFont.Width(line2, 2)) / 2, OsdH / 2 - 16, 2, faint);
            }
            var tex = new Texture2D(OsdW, OsdH, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false);
            return Keep(tex);
        }

        // ================================================================ runtime

        /// <summary>Per frame after the screens / lights were updated: switches the rooms without power off.</summary>
        private void UpdatePower()
        {
            var mm = MatchManager.Instance;
            var s = mm != null ? mm.CamPower.Value : default;
            if (_osdNoPower == null) _osdNoPower = MakeOsd("OsdNoPower", "BRAK ZASILANIA", null);
            if (_osdEmpty == null) _osdEmpty = MakeOsd("OsdEmpty", "AKUMULATOR", "ROZŁADOWANY");
            bool lightsPerRoom = _screenLightBase.Count == Levels * 2;
            for (int lv = 0; lv < Levels; lv++)
            {
                var pp = _power[lv];
                bool powered = s.Powered(lv);
                int shown = powered ? 1 : s.HasBattery(lv) && s.IsOn(lv) ? 2 : 0;
                if (pp != null && shown != pp.Shown)
                {
                    pp.Shown = shown;
                    foreach (var sl in _slots)
                    {
                        if (sl == null || lv >= sl.Mats.Count || sl.Mats[lv] == null) continue;
                        sl.Mats[lv].mainTexture = powered ? sl.Rt : Texture2D.blackTexture;
                        sl.Mats[lv].SetTexture("_Osd", powered ? sl.Osd : shown == 2 ? _osdEmpty : _osdNoPower);
                    }
                }
                if (!powered)
                {
                    foreach (var sl in _slots)
                    {
                        if (sl == null || lv >= sl.Mats.Count || sl.Mats[lv] == null) continue;
                        var m = sl.Mats[lv];
                        m.SetFloat("_Snow", 0f);
                        m.SetFloat("_Glitch", 0f);
                        m.SetFloat("_Alarm", 0f);
                    }
                }
                if (lightsPerRoom)
                    for (int k = 0; k < 2; k++)
                    {
                        var l = _screenLightBase[lv * 2 + k].light;
                        if (l != null && l.enabled != powered) l.enabled = powered;
                    }
                if (pp != null) UpdatePowerParts(pp, s, lv);
            }
            bool any = s.AnyPowered;
            if (!any && !_ledsDark) LedsDark();
            _ledsDark = !any;
        }

        private void UpdatePowerParts(PowerParts pp, in CamPowerState s, int lv)
        {
            // lever: up = on
            float want = s.IsOn(lv) ? -38f : 38f;
            if (Mathf.Abs(pp.LeverAngle - want) > 0.01f && pp.Lever != null)
            {
                pp.LeverAngle = pp.LeverAngle > 900f ? want : Mathf.MoveTowards(pp.LeverAngle, want, 600f * Time.deltaTime);
                pp.Lever.localRotation = Quaternion.Euler(0f, 0f, pp.LeverAngle);
            }
            // battery + clamps
            bool bat = s.HasBattery(lv);
            if (bat != pp.BatteryShown && pp.Battery != null)
            {
                pp.BatteryShown = bat;
                pp.Battery.gameObject.SetActive(bat);
                Vector3 plus = bat ? pp.Battery.TransformPoint(CarBatteryModel.PlusPost) + Vector3.up * 0.008f : pp.RestPlus;
                Vector3 minus = bat ? pp.Battery.TransformPoint(CarBatteryModel.MinusPost) + Vector3.up * 0.008f : pp.RestMinus;
                PlaceLead(pp.ClampPlus, pp.CablePlus, pp.CableFromPlus, plus);
                PlaceLead(pp.ClampMinus, pp.CableMinus, pp.CableFromMinus, minus);
            }
            // gauge: green bars for the charge (any battery in the tray), the last one blinks red when low / empty with the switch on
            int pct = bat ? s.Percent(lv) : 0;
            int lit = bat ? Mathf.CeilToInt(pct / (100f / GaugeSegments)) : 0;
            bool warn = s.IsOn(lv) && (!bat || pct <= 20);
            bool blink = warn && Time.time % 0.8f < 0.4f;
            int key = lit * 4 + (warn ? 2 : 0) + (blink ? 1 : 0);
            if (key == pp.GaugeKey) return;
            pp.GaugeKey = key;
            for (int k = 0; k < GaugeSegments; k++)
            {
                var r = pp.Gauge[k];
                if (r == null) continue;
                Color e;
                if (k == 0 && warn) e = blink ? new Color(2.2f, 0.15f, 0.05f) : new Color(0.15f, 0.01f, 0f);
                else e = k < lit ? (lit <= 1 ? new Color(2f, 0.9f, 0.05f) : new Color(0.25f, 1.8f, 0.35f)) : new Color(0.01f, 0.03f, 0.01f);
                _gaugeMpb.Clear();
                _gaugeMpb.SetColor("_Color", new Color(0.05f, 0.08f, 0.05f));
                _gaugeMpb.SetColor("_EmissionColor", e);
                r.SetPropertyBlock(_gaugeMpb);
            }
        }

        /// <summary>Clamp on its point, the lead from the bottom of the wall cable to it.</summary>
        private static void PlaceLead(Transform clamp, Transform cable, Vector3 from, Vector3 to)
        {
            if (clamp != null) clamp.position = to;
            if (cable == null) return;
            Vector3 d = to - from;
            cable.position = (from + to) * 0.5f;
            cable.rotation = d.sqrMagnitude > 1e-6f ? Quaternion.FromToRotation(Vector3.up, d.normalized) : Quaternion.identity;
            cable.localScale = new Vector3(0.012f, d.magnitude * 0.5f, 0.012f);
        }

        /// <summary>No room has power: every camera LED out (once; UpdateLeds skips while nothing is powered).</summary>
        private void LedsDark()
        {
            for (int i = 0; i < _cams.Count; i++)
            {
                var c = _cams[i];
                if (c.Led != null)
                {
                    _ledMpb.Clear();
                    _ledMpb.SetColor("_EmissionColor", Color.black);
                    c.Led.SetPropertyBlock(_ledMpb);
                }
                if (c.Halo != null)
                {
                    _ledMpb.Clear();
                    _ledMpb.SetColor("_Color", new Color(1f, 0.09f, 0.05f));
                    _ledMpb.SetFloat("_Intensity", 0f);
                    c.Halo.SetPropertyBlock(_ledMpb);
                }
                if (c.Glow != null) c.Glow.enabled = false;
            }
        }

        // ================================================================ interaction

        /// <summary>Power switch or battery tray in reach and in view (prompt). <paramref name="isSwitch"/> tells which.</summary>
        public bool TryFindPower(Vector3 eye, Vector3 fwd, out bool isSwitch, out int room, out Vector3 world, out float dist)
        {
            isSwitch = false; room = -1; world = default; dist = float.MaxValue;
            for (int lv = 0; lv < Levels; lv++)
            {
                if (Mathf.Abs(eye.y - (FloorY(lv) + 1.5f)) > 1.5f) continue;
                Vector3 sw = PowerSwitchPoint(lv), slot = PowerSlotPoint(lv);
                float ds = Vector3.Distance(eye, sw), dt = Vector3.Distance(eye, slot);
                if (ds <= 2.2f && ds < dist && Vector3.Angle(fwd, sw - eye) <= 22f) { isSwitch = true; room = lv; world = sw; dist = ds; }
                if (dt <= 2.3f && dt < dist && Vector3.Angle(fwd, slot - eye) <= 30f) { isSwitch = false; room = lv; world = slot; dist = dt; }
            }
            return room >= 0;
        }
    }
}
