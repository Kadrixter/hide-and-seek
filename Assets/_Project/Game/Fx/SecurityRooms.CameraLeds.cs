using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Extra garage cameras (both ends of every aisle, the second ends of the cross aisles, the two ramps), the LED
    /// blinking and the idle cycling of the monitors. LED: a camera that is on a monitor while somebody is in a
    /// security room blinks clearly red (1.25 Hz, bright, halo, a faint red light on the bracket); every other camera
    /// only gives a short dim standby blip every few seconds. More cameras than monitors: a monitor without motion
    /// switches to the camera that has not been shown for the longest time (one monitor every few seconds, on the
    /// shared match clock so every client shows the same cameras and the LEDs agree).
    /// </summary>
    public partial class SecurityRooms
    {
        private const float CycleDwell = 14f, CycleGap = 4.5f;
        private const float WatchedHz = 1.25f, IdlePeriod = 2.8f;

        /// <summary>Test switch: LEDs behave as if somebody watched the monitors.</summary>
        public static bool TestWatched;

        private readonly double[] _cycleSince = new double[Monitors];
        private long _cycleTick = -1;

        private void AddExtraCams(int lv, float y0, float y, float[] aisleZ)
        {
            for (int i = 0; i < 6; i++)
            {
                bool fromWest = i % 2 == 0;   // the first camera of the aisle hangs at this end; this one at the other
                AddCam(lv, "ALEJA " + (i + 1) + "B", new Vector3(fromWest ? 72f : -72f, y, aisleZ[i] + 0.6f), new Vector3(fromWest ? -1f : 1f, -0.07f, 0f));
            }
            AddCam(lv, "WSCHÓD 2", new Vector3(79.5f, y, 44f), new Vector3(0f, -0.07f, -1f));
            AddCam(lv, "ZACHÓD 2", new Vector3(-79.5f, y, -44f), new Vector3(0f, -0.07f, 1f));
            // ramps (Map ParkingGarageBuilder.Ramps: x -44 -> -66 at z -7.5..-0.5 and 44 -> 66 at z 0.5..7.5)
            AddCam(lv, "RAMPA 1", new Vector3(-38f, y0 + 2.4f, -4f), new Vector3(-1f, -0.16f, 0f));
            AddCam(lv, "RAMPA 2", new Vector3(38f, y0 + 2.4f, 4f), new Vector3(1f, -0.16f, 0f));
        }

        /// <summary>Per-level camera number for the on-screen display (CAM01..CAM24).</summary>
        private int CamNumber(int ci) => _levelCams[_cams[ci].Level].IndexOf(ci) + 1;

        private static float Blink(float phase, float on)
        {
            // quick rise, hold, quick fall: a crisp but not harsh blink
            return Mathf.Clamp01(phase / 0.03f) * Mathf.Clamp01((on - phase) / 0.05f);
        }

        private void UpdateLeds()
        {
            var mm = MatchManager.Instance;
            bool watched = TestWatched || (mm != null && mm.SecurityWatched.Value);
            float t = Time.time;
            for (int i = 0; i < _cams.Count; i++)
            {
                var c = _cams[i];
                if (c.Led == null) continue;
                bool on = false;
                if (watched)
                    foreach (var s in _slots) if (s.CamIndex == i) { on = true; break; }
                float b, emit, halo, light;
                if (on)
                {
                    b = Blink(Mathf.Repeat(t * WatchedHz, 1f), 0.42f);
                    emit = 0.35f + 6f * b; halo = 0.2f + 1.15f * b; light = 0.55f * b;
                }
                else
                {
                    b = Blink(Mathf.Repeat(t + i * 0.731f, IdlePeriod), 0.13f);
                    emit = 0.06f + 1.4f * b; halo = 0.03f + 0.3f * b; light = 0f;
                }
                _ledMpb.Clear();
                _ledMpb.SetColor("_EmissionColor", new Color(1f, 0.06f, 0.03f) * emit);
                c.Led.SetPropertyBlock(_ledMpb);
                if (c.Halo != null)
                {
                    _ledMpb.Clear();
                    _ledMpb.SetColor("_Color", new Color(1f, 0.09f, 0.05f));
                    _ledMpb.SetFloat("_Intensity", halo);
                    c.Halo.SetPropertyBlock(_ledMpb);
                }
                if (light > 0.001f && c.Glow == null)
                {
                    c.Glow = new GameObject("LedGlow").AddComponent<Light>();
                    c.Glow.transform.SetParent(c.Led.transform, false);
                    c.Glow.transform.localPosition = CctvModel.LedLocal + new Vector3(0f, 0.02f, 0.08f);
                    c.Glow.type = LightType.Point;
                    c.Glow.range = 1.1f;
                    c.Glow.color = new Color(1f, 0.08f, 0.04f);
                    c.Glow.shadows = LightShadows.None;
                    _cams[i] = c;
                }
                if (c.Glow != null)
                {
                    c.Glow.enabled = light > 0.001f;
                    c.Glow.intensity = light;
                }
            }
        }

        /// <summary>
        /// Idle cycling: on every shared clock tick one monitor without motion that has shown its camera for at least
        /// <see cref="CycleDwell"/> s switches to the camera of this level that was not shown for the longest time.
        /// </summary>
        private void CycleIdle()
        {
            var mm = MatchManager.Instance;
            double now = mm != null ? mm.Now : Time.timeAsDouble;
            for (int i = 0; i < Monitors; i++)
                if (_slots[i].CamIndex >= 0)
                {
                    var c = _cams[_slots[i].CamIndex];
                    c.LastShown = Time.time;
                    _cams[_slots[i].CamIndex] = c;
                }
            long tick = (long)(now / CycleGap);
            if (tick == _cycleTick) return;
            bool first = _cycleTick < 0;
            _cycleTick = tick;
            if (first) { for (int i = 0; i < Monitors; i++) _cycleSince[i] = now; return; }
            var list = _levelCams[_floor];
            if (list.Count <= Monitors) return;
            int best = -1;
            for (int i = 0; i < Monitors; i++)
            {
                var s = _slots[i];
                if (s.Motion || s.CamIndex < 0 || now - _cycleSince[i] < CycleDwell || Time.time - s.Since < CycleDwell) continue;
                if (best < 0 || _cycleSince[i] < _cycleSince[best]) best = i;
            }
            if (best < 0) return;
            int pick = -1;
            foreach (int ci in list)
            {
                bool shown = false;
                foreach (var s in _slots) if (s.CamIndex == ci) { shown = true; break; }
                if (shown) continue;
                if (pick < 0 || _cams[ci].LastShown < _cams[pick].LastShown) pick = ci;
            }
            if (pick < 0) return;
            var slot = _slots[best];
            slot.CamIndex = pick;
            slot.Since = Time.time;
            slot.NextRender = 0f;
            _cycleSince[best] = now;
            PlayFeedSound(best, 0.45f);
            UpdateLabels();
        }

        /// <summary>Test helper: names of the cameras on the six monitors.</summary>
        public string MonitorCameras()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in _slots) sb.Append(s.CamIndex >= 0 ? _cams[s.CamIndex].Name : "-").Append(" | ");
            return sb.ToString();
        }

        public int CameraCount(int level) => _levelCams[level].Count;

        /// <summary>Test helper: rotation of a camera.</summary>
        public Quaternion CameraRotation(int level, int index) => _cams[_levelCams[level][index]].Rot;

        /// <summary>Test helper: (level, index in level) of the camera on a monitor, or (-1, -1).</summary>
        public (int level, int index) SlotCamera(int slot)
        {
            int ci = _slots[slot].CamIndex;
            return ci < 0 ? (-1, -1) : (_cams[ci].Level, _levelCams[_cams[ci].Level].IndexOf(ci));
        }

        /// <summary>Test helper: the standby blip of an unwatched camera is lit right now.</summary>
        public bool IdleBlipOn(int level, int index) =>
            Blink(Mathf.Repeat(Time.time + _levelCams[level][index] * 0.731f, IdlePeriod), 0.13f) > 0.8f;
    }
}
