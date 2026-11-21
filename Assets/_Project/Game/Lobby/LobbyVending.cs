using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using HideAndSeek.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby vending machine: builds the detailed model (<see cref="VendingMachineModel"/>) on the scene's
    /// "VendingMachine" root and plays the purchase for everyone from the server clock in
    /// <see cref="MatchManager.VendingCans"/>: key press, the spiral of a random slot turns, the can tips off the shelf,
    /// falls into the bin and rolls out through the flap; the buyer picks it up (held prop), opens, drinks, crushes and
    /// tosses it over the shoulder, where the server simulates the empty can.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class LobbyVending : MonoBehaviour
    {
        public static LobbyVending Instance { get; private set; }

        const float ThrownScale = 1.9f;

        VendingMachineModel model;
        readonly Dictionary<int, SodaCan> cans = new();
        readonly Dictionary<int, Rigidbody> bodies = new();
        readonly Dictionary<int, double> restSince = new();
        readonly Dictionary<int, float> lastT = new();
        readonly Dictionary<int, byte> lastBounce = new();
        readonly HashSet<NetPlayer> posing = new();
        float nextSync;

        /// <summary>Where the prompt sits (the keypad).</summary>
        public Vector3 AimPoint => transform.TransformPoint(VendingMachineModel.KeypadLocal);
        /// <summary>No purchase is running right now.</summary>
        public bool Free
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm == null || !mm.IsSpawned) return false;
                foreach (var s in mm.VendingCans) if (s.Stage == 0) return false;
                return true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; Install(); }
        static void Loaded(Scene s, LoadSceneMode m) => Install();
        static void Install()
        {
            foreach (var t in FindObjectsByType<Transform>())
                if (t.name == "VendingMachine" && !t.GetComponent<LobbyVending>()) t.gameObject.AddComponent<LobbyVending>();
        }

        void Awake()
        {
            Instance = this;
            // The scene keeps the collider box ("Body", renderer off); its Standard material is the template.
            var body = transform.Find("Body");
            var template = body != null ? body.GetComponent<Renderer>().sharedMaterial : GetComponentInChildren<Renderer>(true).sharedMaterial;
            if (SodaCan.Template == null) SodaCan.Template = template;
            // Older scenes still carry the previous dressing: hide everything but the collider.
            foreach (Transform c in transform)
                if (c != body) c.gameObject.SetActive(false);
            if (body != null) body.GetComponent<Renderer>().enabled = false;
            model = VendingMachineModel.Build(transform, template);
        }

        // ================================================================ per frame

        void Update()
        {
            var mm = MatchManager.Instance;
            if (!mm || !mm.IsSpawned || model == null) return;
            var alive = new HashSet<int>();
            var active = new HashSet<NetPlayer>();
            float flapOpen = 0f;
            bool running = false;
            for (int i = mm.VendingCans.Count - 1; i >= 0; i--)
            {
                var s = mm.VendingCans[i];
                alive.Add(s.Id);
                if (!cans.TryGetValue(s.Id, out var can))
                {
                    can = SodaCan.Create(transform, s.Brand, 0, VendingMachineModel.CanScale);
                    can.name = "BoughtCan_" + s.Id;
                    can.gameObject.SetActive(false);
                    cans.Add(s.Id, can);
                }
                float age = (float)(mm.Now - s.Started);
                var player = NetPlayer.Find(s.Player);
                if (s.Stage == 0)
                {
                    if (!player) { if (mm.IsServer) mm.VendingCans.RemoveAt(i); continue; }
                    running = true;
                    active.Add(player);
                    flapOpen = Mathf.Max(flapOpen, Dispense(can, player, s, age));
                    PosePlayer(player, s, age);
                    Sounds(s, player, age);
                    if (mm.IsServer && age >= VendingTimeline.Release) Throw(mm, i, s, can, player);
                }
                else
                {
                    can.gameObject.SetActive(true);
                    can.SetOpen(1f);
                    can.Scale = ThrownScale;
                    can.SetCrush(1f);
                    if (mm.IsServer) { if (!Simulate(mm, i, s, can)) continue; }
                    else can.transform.SetPositionAndRotation(
                        Vector3.Lerp(can.transform.position, s.Position, 0.5f), Quaternion.Slerp(can.transform.rotation, s.Rotation, 0.5f));
                    if (lastBounce.TryGetValue(s.Id, out var b) && b != s.Bounces)
                        GameAudio.PlayAt(GameSfx.CanBounce, can.transform.position, 1f / (1 + b));
                    lastBounce[s.Id] = s.Bounces;
                }
            }
            if (!running) { ResetSlots(); model.SetDisplay("2.50"); }
            if (Time.unscaledTime >= nextSync) nextSync = Time.unscaledTime + 0.05f;
            foreach (var id in new List<int>(cans.Keys))
                if (!alive.Contains(id)) { Destroy(cans[id].gameObject); cans.Remove(id); bodies.Remove(id); restSince.Remove(id); lastT.Remove(id); lastBounce.Remove(id); }
            foreach (var p in new List<NetPlayer>(posing))
                if (!p || !active.Contains(p)) { ResetPose(p); posing.Remove(p); }
            model.Flap.localRotation = Quaternion.Euler(0f, 0f, -62f * flapOpen);
        }

        void PosePlayer(NetPlayer p, in VendingState s, float t)
        {
            Vector3 trayCan = transform.TransformPoint(VendingMachineModel.TrayCanLocal);
            int r = s.Slot / VendingMachineModel.Cols, c = s.Slot % VendingMachineModel.Cols;
            Vector3 button = model.Keys[VendingMachineModel.SlotKey(r, c)].position;
            (p.Actions as BlockyCharacterActions)?.SetVending(t, button, trayCan, s.Brand);
            posing.Add(p);
            if (!p.IsOwner) return;
            (p.ViewModel as FirstPersonViewModel)?.SetVending(t, button, trayCan, p.transform.rotation, s.Brand);
            var bob = p.GetComponentInChildren<HeadBob>();
            if (bob == null) return;
            float crouch = VendingTimeline.Crouch(t), drink = VendingTimeline.Drink(t);
            // Look at the keypad, follow the can down the machine, the tray, the can in the hand, head back to drink.
            float watch = 6f * VendingTimeline.S(t, 0.7f, 1.1f) * (1f - VendingTimeline.S(t, 1.3f, 1.6f));
            float look = 8f * VendingTimeline.S(t, 0.05f, 0.3f) * (1f - VendingTimeline.S(t, 0.7f, 1.1f)) + watch + 30f * crouch
                         + 16f * VendingTimeline.S(t, 2.9f, 3.6f) * (1f - VendingTimeline.S(t, 4.85f, 5.15f)) - 24f * drink;
            bob.EyeOverride = 0.85f;
            bob.EyeOverrideWeight = crouch;
            bob.ExtraEuler = new Vector3(look, -4f * VendingTimeline.S(t, 7.2f, 7.6f), 0f);
        }

        void Sounds(in VendingState s, NetPlayer p, float t)
        {
            float prev = lastT.TryGetValue(s.Id, out var v) ? v : t;
            lastT[s.Id] = t;
            bool Hit(float mark) => prev < mark && t >= mark;
            Vector3 head = p.transform.position + Vector3.up * 1.5f;
            Vector3 bin = transform.TransformPoint(VendingMachineModel.ChuteLocal);
            int r = s.Slot / VendingMachineModel.Cols;
            if (Hit(0.4f)) GameAudio.PlayAt(GameSfx.VendButton, AimPoint, 1f);
            if (Hit(SpiralStart)) GameAudio.PlayAt(GameSfx.VendDrop, bin, 1f);
            if (Hit(ImpactTime(r))) GameAudio.PlayAt(GameSfx.CanBounce, bin, 0.35f);
            if (Hit(VendingTimeline.TabPop - 0.1f)) GameAudio.PlayAt(GameSfx.CanOpen, head - Vector3.up * 0.3f, 1f);
            if (Hit(VendingTimeline.Gulps[0] - 0.1f)) GameAudio.PlayAt(GameSfx.CanGulp, head, 1f);
            if (Hit(6.75f)) GameAudio.PlayAt(GameSfx.CanAhh, head, 1f);
            if (Hit(VendingTimeline.CrushAt - 0.05f)) GameAudio.PlayAt(GameSfx.CanCrush, head - Vector3.up * 0.35f, 1f);
        }

        void ResetPose(NetPlayer p)
        {
            if (!p) return;
            (p.Actions as BlockyCharacterActions)?.SetVending(-1f, Vector3.zero, Vector3.zero, 0);
            if (!p.IsOwner) return;
            (p.ViewModel as FirstPersonViewModel)?.SetVending(-1f, Vector3.zero, Vector3.zero, Quaternion.identity, 0);
            var bob = p.GetComponentInChildren<HeadBob>();
            if (bob != null) { bob.EyeOverrideWeight = 0f; bob.ExtraEuler = Vector3.zero; }
        }

        void OnDestroy()
        {
            foreach (var p in posing) ResetPose(p);
            if (Instance == this) Instance = null;
        }
    }
}
