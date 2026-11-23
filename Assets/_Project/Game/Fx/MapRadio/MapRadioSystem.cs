using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Client side of the radios found on the parking (<see cref="MatchManager.MapRadios"/>): one <see cref="Boombox"/>
    /// per radio (the lobby model and music player on its own channel, a little worn), placed every frame after the
    /// characters are posed: lying on its spot, on the carrier's shoulder (<see cref="BlockyCharacterActions.ShoulderRadioPose"/>),
    /// in the hands while it is lifted / put down, or tumbling off the shoulder when the carrier goes down. Drives every
    /// player's shoulder-radio layer from the server clock, slows the local carrier down (frozen while lifting /
    /// putting down, slower running), and filters the music between floors like every other 3D sound
    /// (<c>MapRadioSystem.Audio.cs</c>). First person: <c>MapRadioSystem.Fpp.cs</c>; hint line: <c>MapRadioSystem.Hud.cs</c>.
    /// Lives on the MatchManager object (added by <c>MatchManager.MapRadio.cs</c>).
    /// </summary>
    [DefaultExecutionOrder(300)]
    public partial class MapRadioSystem : MonoBehaviour
    {
        public const float WalkScale = 0.9f, RunScale = 0.7f;

        public static MapRadioSystem Instance { get; private set; }

        private sealed class View
        {
            public Boombox Box;
            public Renderer[] Renderers;
            public Vector3 Pos;
            public Quaternion Rot = Quaternion.identity;
            public bool HasPose, Hidden;
            public byte Mode, Move;
            public double MoveAt;
            public Vector3 FallFrom;
            public Quaternion FallFromRot = Quaternion.identity;
            public bool Landed = true;
            public float Fold;
            public float FloorGain = 1f, FloorTarget = 1f, Cutoff = 22000f, Occlusion, NextAudio;
            public AudioLowPassFilter LowPass;
        }

        private readonly List<View> _views = new();
        private readonly HashSet<NetPlayer> _driven = new();

        /// <summary>Radio index whose view <paramref name="b"/> is (-1 = not a map radio).</summary>
        public int IndexOf(Boombox b)
        {
            for (int i = 0; i < _views.Count; i++) if (_views[i].Box == b) return i;
            return -1;
        }

        public Boombox ViewOf(int i) => i >= 0 && i < _views.Count ? _views[i].Box : null;

        /// <summary>World pose of radio <paramref name="i"/> as shown now.</summary>
        public bool PoseOf(int i, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;
            if (i < 0 || i >= _views.Count || !_views[i].HasPose) return false;
            pos = _views[i].Pos;
            rot = _views[i].Rot;
            return true;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var v in _views) if (v.Box != null) Destroy(v.Box.gameObject);
            DestroyFpp();
        }

        private static bool InGame => SceneManager.GetActiveScene().name == NetSession.GameScene;

        // ================================================================ per frame

        private void Update()
        {
            var mm = MatchManager.Instance;
            int n = mm != null && mm.MapRadios != null && InGame ? mm.MapRadios.Count : 0;
            while (_views.Count < n) _views.Add(new View());
            for (int i = _views.Count - 1; i >= n; i--)
            {
                if (_views[i].Box != null) Destroy(_views[i].Box.gameObject);
                _views.RemoveAt(i);
            }
            for (int i = 0; i < n; i++)
            {
                var v = _views[i];
                if (v.Box == null) CreateView(v, i);
                TrackState(v, mm.MapRadios[i]);
            }
            DriveCharacters(mm, n);
            UpdateLocal(mm, n);
            UpdateAudio(mm, n);
            UpdateHud(mm, n);
            UpdateFpp();
        }

        private void CreateView(View v, int i)
        {
            int idx = i;
            var b = Boombox.Create($"MapRadio{i}", () => MatchManager.Instance?.MapChannel(idx));
            b.PanelTitle = "RADIO";
            b.PanelAllowed = () => LocalMayUse(idx);
            MapRadioLook.Wear(b);
            v.Box = b;
            v.Renderers = b.GetComponentsInChildren<Renderer>(true);
            v.HasPose = false;
            v.Hidden = false;
            SetupAudio(v);
        }

        /// <summary>Notices a fall (carried -> lying) to animate it from the last shown pose.</summary>
        private static void TrackState(View v, in MapRadioState s)
        {
            if (s.Mode == v.Mode && s.Move == v.Move && s.MoveAt == v.MoveAt) return;
            if (s.Mode == MapRadioState.Lying && s.Move == MapRadioState.Fell && v.HasPose)
            {
                v.FallFrom = v.Pos;
                v.FallFromRot = v.Rot;
                v.Landed = false;
            }
            else v.Landed = s.Move != MapRadioState.Fell && s.Move != MapRadioState.PutDown;
            v.Mode = s.Mode;
            v.Move = s.Move;
            v.MoveAt = s.MoveAt;
        }

        private float Since(MatchManager mm, in MapRadioState s) => (float)(mm.Now - s.MoveAt);

        /// <summary>Every player's shoulder-radio layer (carrying, lifting, putting down).</summary>
        private void DriveCharacters(MatchManager mm, int n)
        {
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !(p.Actions is BlockyCharacterActions bca)) continue;
                int found = -1;
                bool carried = false, putting = false;
                for (int i = 0; i < n; i++)
                {
                    var s = mm.MapRadios[i];
                    if (s.Mode == MapRadioState.Carried && s.Carrier == p.NetworkObjectId) { found = i; carried = true; break; }
                    if (s.Mode == MapRadioState.Lying && s.Move == MapRadioState.PutDown && s.LastCarrier == p.NetworkObjectId &&
                        Since(mm, s) < ShoulderRadioRig.PutTime) { found = i; putting = true; break; }
                }
                if (found < 0)
                {
                    if (_driven.Remove(p)) bca.SetShoulderRadio(false, false, 99f, Vector3.zero, Quaternion.identity, 0f, false);
                    continue;
                }
                var st = mm.MapRadios[found];
                var box = _views[found].Box;
                bool music = box != null && box.MusicAudible;
                float beat = music && box.Output != null ? box.Output.Kick : 0f;
                bca.SetShoulderRadio(carried, putting, Since(mm, st), st.Pos, st.Rot, beat, music);
                _driven.Add(p);
            }
            _driven.RemoveWhere(p => p == null);
        }

        /// <summary>Local carrier: speed (frozen while lifting / putting down), no toy gun out, panel rules.</summary>
        private void UpdateLocal(MatchManager mm, int n)
        {
            var me = NetPlayer.Local;
            if (me == null || mm == null) return;
            float scale = 1f;
            for (int i = 0; i < n; i++)
            {
                var s = mm.MapRadios[i];
                float since = Since(mm, s);
                if (s.Mode == MapRadioState.Carried && s.Carrier == me.NetworkObjectId)
                {
                    var kb = Keyboard.current;
                    bool run = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                    scale = since < ShoulderRadioRig.LiftTime ? 0f : run ? RunScale : WalkScale;
                    if (me.ToyGunOut.Value) me.ToyGunOut.Value = false;
                }
                else if (s.Mode == MapRadioState.Lying && s.Move == MapRadioState.PutDown && s.LastCarrier == me.NetworkObjectId && since < ShoulderRadioRig.PutRelease + 0.15f)
                    scale = 0f;
            }
            // NetPlayer sets the motor's scale every frame before this (execution order); the motor uses it next frame.
            if (scale < 1f && me.Motor != null) me.Motor.SpeedScale *= scale;
        }

        /// <summary>The local player may open / keep open radio <paramref name="i"/>'s panel.</summary>
        public static bool LocalMayUse(int i)
        {
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            if (mm == null || me == null || !me.IsAliveHider || mm.MapRadios == null || i >= mm.MapRadios.Count) return false;
            var s = mm.MapRadios[i];
            if (s.Mode == MapRadioState.Carried) return s.Carrier == me.NetworkObjectId || Vector3.Distance(me.EyePosition, mm.MapRadioWhere(i)) <= MatchManager.MapRadioPanelReach;
            return Vector3.Distance(me.EyePosition, s.Pos + Vector3.up * 0.15f) <= MatchManager.MapRadioPanelReach;
        }

        // ================================================================ placement (after the characters are posed)

        private void LateUpdate()
        {
            var mm = MatchManager.Instance;
            for (int i = 0; i < _views.Count; i++)
            {
                var v = _views[i];
                if (v.Box == null || mm == null || i >= mm.MapRadios.Count) continue;
                var s = mm.MapRadios[i];
                Place(mm, v, s, out bool held, out NetPlayer holder);
                var t = v.Box.transform;
                t.SetPositionAndRotation(v.Pos + FppNudge(mm, s, holder), v.Rot);
                if (v.Box.Collider != null) v.Box.Collider.enabled = !held && v.Landed;
                float fold = s.Mode == MapRadioState.Carried || s.Move != MapRadioState.Placed ? 1f : 0f;
                if (fold != v.Fold)
                {
                    v.Fold = Mathf.MoveTowards(v.Fold, fold, Time.deltaTime * 1.6f);
                    MapRadioLook.FoldAntenna(v.Box, v.Fold);
                }
                // The local carrier sees the real radio too (it sits beside his head: its end shows at the right edge).
            }
        }

        /// <summary>
        /// Local carrier in first person only: the radio sits a few cm further out and back, so from the eyes only its end
        /// shows at the edge of the view (the others see it exactly on the shoulder). Faded in / out with the lift / put.
        /// </summary>
        private Vector3 FppNudge(MatchManager mm, in MapRadioState s, NetPlayer holder)
        {
            if (holder == null || !holder.IsOwner || !(holder.Actions is BlockyCharacterActions b) || !IsFirstPerson(b)) return Vector3.zero;
            float since = Since(mm, s);
            float k = s.Mode == MapRadioState.Carried ? Mathf.Clamp01((since - ShoulderRadioRig.LiftTime + 0.35f) / 0.35f)
                : s.Move == MapRadioState.PutDown ? 1f - Mathf.Clamp01(since / 0.3f) : 0f;
            k = Mathf.SmoothStep(0f, 1f, k);
            return (holder.transform.right * 0.06f - holder.transform.forward * 0.09f) * k;
        }

        private static bool IsFirstPerson(BlockyCharacterActions b)
        {
            var c = b.GetComponent<BlockyCharacter>();
            return c != null && c.IsFirstPerson;
        }

        private void Place(MatchManager mm, View v, in MapRadioState s, out bool held, out NetPlayer holder)
        {
            held = false;
            holder = null;
            float since = Since(mm, s);
            if (s.Mode == MapRadioState.Carried)
            {
                held = true;
                holder = NetPlayer.Find(s.Carrier);
                if (holder != null && holder.Actions is BlockyCharacterActions bca && bca.ShoulderRadioPose(out var p, out var r))
                {
                    v.Pos = p;
                    v.Rot = r;
                    v.HasPose = true;
                }
                else if (!v.HasPose)
                {
                    v.Pos = s.Pos;
                    v.Rot = s.Rot;
                    v.HasPose = true;
                }
                return;
            }
            if (s.Move == MapRadioState.PutDown && since < ShoulderRadioRig.PutRelease)
            {
                holder = NetPlayer.Find(s.LastCarrier);
                if (holder != null && holder.Actions is BlockyCharacterActions bca && bca.ShoulderRadioPose(out var p, out var r))
                {
                    held = true;
                    v.Pos = p;
                    v.Rot = r;
                    v.HasPose = true;
                    return;
                }
            }
            if (s.Move == MapRadioState.Fell && !v.Landed)
            {
                float k = Mathf.Clamp01(since / ShoulderRadioRig.FallTime);
                Fall(v, s, k);
                if (k >= 1f)
                {
                    v.Landed = true;
                    PlayClunk(s.Pos, 1f);
                }
                v.HasPose = true;
                return;
            }
            if (s.Move == MapRadioState.PutDown && !v.Landed && since < ShoulderRadioRig.PutRelease + 0.5f) PlayClunk(s.Pos, 0.45f);
            v.Landed = true;
            v.Pos = s.Pos;
            v.Rot = s.Rot;
            v.HasPose = true;
        }

        /// <summary>Off the shoulder: drops in an arc (gravity), tips over a little in the air, lands with a small bounce.</summary>
        private static void Fall(View v, in MapRadioState s, float k)
        {
            const float land = 0.72f;
            Vector3 to = s.Pos;
            if (k < land)
            {
                float u = k / land;
                Vector3 flat = Vector3.Lerp(v.FallFrom, to, 1f - (1f - u) * (1f - u) * 0.3f - 0.7f * (1f - u));
                float y = Mathf.Lerp(v.FallFrom.y, to.y, u * u) + 0.12f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.6f)) * (1f - u);
                v.Pos = new Vector3(flat.x, y, flat.z);
                v.Rot = Quaternion.Slerp(v.FallFromRot, s.Rot, u) * Quaternion.Euler(28f * Mathf.Sin(Mathf.PI * u), 0f, -18f * Mathf.Sin(Mathf.PI * u));
            }
            else
            {
                float u = (k - land) / (1f - land);
                float hop = Mathf.Sin(Mathf.PI * u) * 0.035f * (1f - u);
                v.Pos = to + Vector3.up * hop;
                v.Rot = s.Rot * Quaternion.Euler(6f * Mathf.Sin(Mathf.PI * u) * (1f - u), 0f, 0f);
            }
        }
    }
}
