using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.Voice;
using HideAndSeek.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Parking public-address system, built at runtime in the garage (no map rebuild): a wall station with the hand
    /// microphone on every level next to the elevators and horn speakers on the columns of every level
    /// (<see cref="PaLayout"/>). Each frame it drives the hold / hang-back animation of every player from the server
    /// state (<see cref="MatchManager.PaHolder"/>), shows the mic on its hook (or flying back to it on the cord and
    /// swinging out), lays the coiled cord to the hand, and lights the station lamps (green = free, red = in use;
    /// one system, so all stations at once). The voice through the speakers: <see cref="PaVoice"/>.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class PaSystem : MonoBehaviour
    {
        public static PaSystem Instance { get; private set; }

        private sealed class Station
        {
            public int Level;
            public Transform Root;
            public MeshRenderer Box;
            public PaMicModel.Parts Mic;
            public PaCord Cord;
            public float FlyT = 99f, Swing, SwingVel;
            public Vector3 FlyFrom;
            public Quaternion FlyFromRot = Quaternion.identity;
            public bool WasInHand;
            public Vector3 LastHandPos;
            public Quaternion LastHandRot = Quaternion.identity;
        }

        private const float FlyTime = 0.34f;

        private readonly Station[] _stations = new Station[PaLayout.Levels];
        private readonly List<(Vector3 pos, int floor)> _mouths = new();
        private readonly Dictionary<ulong, int> _lastStation = new();
        private MaterialPropertyBlock _mpb;
        private int _lampState = -1;
        private PaVoice _voice;

        public int SpeakerCount => _mouths.Count;
        public int SkippedSpeakers { get; private set; }

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("PaSystem");
            Instance = go.AddComponent<PaSystem>();
            Instance.Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                (p.Actions as BlockyCharacterActions)?.ClearPaMic();
                if (p.IsOwner) (p.ViewModel as FirstPersonViewModel)?.SetPaMic(false, Vector3.zero, Quaternion.identity, false, false);
            }
        }

        // ================================================================ build

        private void Build()
        {
            _mpb = new MaterialPropertyBlock();
            var cordMat = PaMesh.Mat("PaCord", new Color(0.03f, 0.03f, 0.032f), 0.55f);
            for (int lv = 0; lv < PaLayout.Levels; lv++)
            {
                _stations[lv] = BuildStation(lv, cordMat);
                BuildSpeakers(lv);
            }
            _voice = PaVoice.Create(transform);
            _voice.SetSpeakers(_mouths);
            UpdateLamps(true);
            Debug.Log($"[PA] built: {PaLayout.Levels} stations, {_mouths.Count} speakers ({SkippedSpeakers} spots skipped)");
        }

        private Station BuildStation(int lv, Material cordMat)
        {
            var root = new GameObject("PaStation_L" + MatchManager.PaLevelName(lv)).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(PaLayout.StationOrigin(lv), PaLayout.StationRot);

            // The wall must be where the map builder put it (south face of the elevator core).
            Vector3 probe = root.position + root.forward * 0.6f;
            if (!Physics.Raycast(probe, -root.forward, out var hit, 1.2f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.distance - 0.6f) > 0.05f)
                Debug.LogWarning($"[PA] level {lv}: no wall at the station spot (hit {(hit.collider != null ? hit.collider.name + " " + hit.distance.ToString("0.00") : "none")})");

            var box = new GameObject("Box");
            box.transform.SetParent(root, false);
            box.AddComponent<MeshFilter>().sharedMesh = PaStationModel.Mesh(PaLayout.Ceiling - PaLayout.BoxY);
            var r = box.AddComponent<MeshRenderer>();
            r.sharedMaterials = PaStationModel.Materials();
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var col = box.AddComponent<BoxCollider>();
            col.center = new Vector3(-0.095f, -0.005f, 0.05f);
            col.size = new Vector3(0.45f, 0.35f, 0.1f);

            // "NAGŁOŚNIENIE" on the red plate.
            var label = new GameObject("Label").AddComponent<TextMesh>();
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(0f, 0.1075f, 0.0942f);
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            label.text = "NAGŁOŚNIENIE";
            label.characterSize = 0.0032f;
            label.fontSize = 60;
            label.fontStyle = FontStyle.Bold;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.97f, 0.95f, 0.9f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            WorldText.Setup(label, new Vector2(0.142f, 0.026f));

            var st = new Station { Level = lv, Root = root, Box = r };
            st.Mic = PaMicModel.Create(root, 0);
            st.Mic.Root.SetPositionAndRotation(PaLayout.MicHangPos(lv), PaLayout.MicHangRot);
            st.Cord = PaCord.Create(transform, 0, cordMat);
            LevelLighting.SetLevelLayer(root, lv);
            LevelLighting.SetLevelLayer(st.Cord.transform, lv);
            return st;
        }

        private void BuildSpeakers(int lv)
        {
            var root = new GameObject("PaSpeakers_L" + MatchManager.PaLevelName(lv)).transform;
            root.SetParent(transform, false);
            var mesh = PaSpeakerModel.Mesh(PaLayout.Ceiling - PaLayout.SpeakerY);
            var mats = PaSpeakerModel.Materials();
            int mask = ~((1 << 8) | (1 << 9));
            var overlap = new Collider[8];
            foreach (var (centre, n) in PaLayout.SpeakerSpots(lv))
            {
                // Column face where the builder put the column; anything else in the way (a parked van, a pipe): skip.
                Vector3 from = centre + n * 1.2f;
                if (!Physics.Raycast(from, -n, out var hit, 1.2f, mask, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.distance - (1.2f - PaLayout.ColumnHalf)) > 0.06f)
                {
                    SkippedSpeakers++;
                    Debug.Log($"[PA] level {lv}: no column face at {centre} ({(hit.collider != null ? hit.collider.name + " at " + hit.distance.ToString("0.00") : "nothing")}) - speaker skipped");
                    continue;
                }
                Vector3 face = new Vector3(hit.point.x, centre.y, hit.point.z);
                var rot = Quaternion.LookRotation(n, Vector3.up);
                int c = Physics.OverlapBoxNonAlloc(face + rot * PaSpeakerModel.BoundsCentre, PaSpeakerModel.BoundsSize * 0.5f, overlap, rot, mask, QueryTriggerInteraction.Ignore);
                string blocker = null;
                for (int i = 0; i < c; i++)
                    if (overlap[i] != hit.collider && !overlap[i].transform.IsChildOf(transform)) { blocker = overlap[i].name; break; }
                if (blocker != null)
                {
                    SkippedSpeakers++;
                    Debug.Log($"[PA] level {lv}: speaker at {face} blocked by {blocker} - skipped");
                    continue;
                }
                var go = new GameObject("PaSpeaker");
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(face, rot);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                // no shadow: under a torch held below it the horn threw a big dark copy of itself on the ceiling
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var bc = go.AddComponent<BoxCollider>();
                bc.center = PaSpeakerModel.BoundsCentre;
                bc.size = PaSpeakerModel.BoundsSize;
                _mouths.Add((face + rot * PaSpeakerModel.MouthLocal, lv));
            }
            LevelLighting.SetLevelLayer(root, lv);
        }

        // ================================================================ per frame

        private void Update()
        {
            var mm = MatchManager.Instance;
            ulong holder = mm != null ? mm.PaHolder.Value : MatchManager.NoPaHolder;
            int station = mm != null ? Mathf.Clamp(mm.PaStation.Value, 0, PaLayout.Levels - 1) : 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                ulong id = p.NetworkObjectId;
                bool hold = id == holder;
                if (hold) _lastStation[id] = station;
                int s = hold ? station : _lastStation.TryGetValue(id, out int ls) ? ls : 0;
                Vector3 hp = PaLayout.MicHangPos(s);
                Quaternion hr = PaLayout.MicHangRot;
                bool talking = hold && (p.IsOwner ? p.PaTransmitting : PaVoice.IsTalking(p));
                (p.Actions as BlockyCharacterActions)?.SetPaMic(hold, hp, hr, talking);
                if (p.IsOwner)
                    (p.ViewModel as FirstPersonViewModel)?.SetPaMic(hold, hp, hr, talking, p.Life.Value == LifeState.Alive && !p.Busy.Value);
            }
            UpdateLamps(false);
        }

        private void LateUpdate()
        {
            var mm = MatchManager.Instance;
            var holder = mm != null ? mm.PaHolderPlayer : null;
            int station = mm != null ? Mathf.Clamp(mm.PaStation.Value, 0, PaLayout.Levels - 1) : 0;
            float dt = Time.deltaTime;
            foreach (var st in _stations)
            {
                if (st == null) continue;
                var bca = holder != null && st.Level == station ? holder.Actions as BlockyCharacterActions : null;
                Transform handMic = bca != null && bca.PaMicInHand ? bca.PaMicTransform : null;
                Vector3 gland = PaLayout.GlandPos(st.Level);
                Vector3 end, endDir;
                if (handMic != null)
                {
                    if (st.Mic.Root.gameObject.activeSelf) st.Mic.Root.gameObject.SetActive(false);
                    st.WasInHand = true;
                    st.LastHandPos = handMic.position;
                    st.LastHandRot = handMic.rotation;
                    var fpv = holder.IsOwner ? holder.ViewModel as FirstPersonViewModel : null;
                    if (fpv != null && fpv.PaMicVisible && holder.Character != null && holder.Character.IsFirstPerson && holder.Camera != null)
                    {
                        end = fpv.PaMicCordScreenWorld;
                        endDir = holder.Camera.transform.rotation * new Vector3(0.1f, -1f, -0.2f);
                    }
                    else
                    {
                        end = bca.PaMicCordWorld;
                        endDir = handMic.rotation * Vector3.down;
                    }
                }
                else
                {
                    if (!st.Mic.Root.gameObject.activeSelf) st.Mic.Root.gameObject.SetActive(true);
                    if (st.WasInHand)
                    {
                        // Back on the hook: hung back by hand (it only swings a little) or pulled back by the cord.
                        st.WasInHand = false;
                        bool far = Vector3.Distance(st.LastHandPos, PaLayout.MicHangPos(st.Level)) > 0.08f;
                        st.FlyT = far ? 0f : FlyTime;
                        st.FlyFrom = st.LastHandPos;
                        st.FlyFromRot = st.LastHandRot;
                        st.SwingVel = (far ? 95f : 22f) * (Random.value < 0.5f ? -1f : 1f);
                    }
                    PoseHookMic(st, dt);
                    end = st.Mic.Root.TransformPoint(PaMicModel.CordLocal);
                    endDir = st.Mic.Root.rotation * Vector3.down;
                }
                st.Cord.Set(gland, Vector3.down, end, endDir, PaLayout.FloorY(st.Level), st.Root.position, st.Root.forward);
            }
        }

        /// <summary>The mic on its hook: flying back on the cord, then a damped swing around the hang tab.</summary>
        private void PoseHookMic(Station st, float dt)
        {
            Vector3 hang = PaLayout.MicHangPos(st.Level);
            Quaternion hangRot = PaLayout.MicHangRot;
            var mic = st.Mic.Root;
            if (st.FlyT < FlyTime)
            {
                st.FlyT += dt;
                float k = Mathf.Clamp01(st.FlyT / FlyTime);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                Vector3 pos = Vector3.Lerp(st.FlyFrom, hang, e) + Vector3.down * 0.18f * Mathf.Sin(Mathf.PI * k) * (1f - k);
                mic.SetPositionAndRotation(pos, Quaternion.Slerp(st.FlyFromRot, hangRot, e));
                mic.localScale = Vector3.one;
                return;
            }
            // pendulum about the hang tab (in the wall plane), damped
            st.SwingVel += (-140f * st.Swing - 6.5f * st.SwingVel) * dt;
            st.Swing += st.SwingVel * dt;
            if (Mathf.Abs(st.Swing) < 0.02f && Mathf.Abs(st.SwingVel) < 0.2f) { st.Swing = 0f; st.SwingVel = 0f; }
            Quaternion rot = hangRot * Quaternion.Euler(0f, 0f, Mathf.Clamp(st.Swing, -20f, 20f));
            Vector3 tab = hang + hangRot * PaMicModel.HangLocal;
            mic.SetPositionAndRotation(tab - rot * PaMicModel.HangLocal, rot);
            mic.localScale = Vector3.one;
        }

        /// <summary>Station lamps: green = free, red = somebody holds a microphone, brighter while he talks.</summary>
        private void UpdateLamps(bool force)
        {
            var mm = MatchManager.Instance;
            var holder = mm != null ? mm.PaHolderPlayer : null;
            bool talking = holder != null && (holder.IsOwner ? holder.PaTransmitting : PaVoice.IsTalking(holder));
            int state = holder == null ? 0 : talking ? 2 : 1;
            if (!force && state == _lampState) return;
            _lampState = state;
            Color lens = state == 0 ? new Color(0.25f, 1f, 0.35f) : new Color(1f, 0.16f, 0.1f);
            Color glow = state == 0 ? new Color(0.12f, 0.9f, 0.25f) * 1.3f : new Color(1f, 0.1f, 0.06f) * (state == 2 ? 2.6f : 1.5f);
            _mpb.Clear();
            _mpb.SetColor("_Color", lens);
            _mpb.SetColor("_EmissionColor", glow);
            foreach (var st in _stations)
                if (st != null) st.Box.SetPropertyBlock(_mpb, PaStationModel.SubLamp);
        }
    }
}
