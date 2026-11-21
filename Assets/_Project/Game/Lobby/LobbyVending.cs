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
    /// Lobby vending machine: dresses the scene model at runtime (branded cans on the shelves, pickup tray with a
    /// swinging flap, keypad) and plays the purchase for everyone from the server clock in <see cref="MatchManager.VendingCans"/>:
    /// button, can out into the tray, picked up (held prop on the character), opened, drunk, crushed and tossed
    /// over the shoulder, where the server simulates the empty can.
    /// Machine space = world axes, front facing -X (see MultiplayerBuilder.LobbyDecor).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class LobbyVending : MonoBehaviour
    {
        public static LobbyVending Instance { get; private set; }

        static readonly Vector3 TrayCanLocal = new Vector3(-0.5f, 0.236f, 0.06f);
        static readonly Vector3 ChuteLocal = new Vector3(-0.33f, 0.236f, 0.06f);
        const float WorldCanScale = BlockyProps.CanScale, ShelfCanScale = 1.15f;

        Transform flap;
        readonly List<Transform> keys = new();
        readonly Dictionary<int, SodaCan> cans = new();
        readonly Dictionary<int, Rigidbody> bodies = new();
        readonly Dictionary<int, double> restSince = new();
        readonly Dictionary<int, float> lastT = new();
        readonly Dictionary<int, byte> lastBounce = new();
        readonly HashSet<NetPlayer> posing = new();
        float nextSync;

        /// <summary>Where the prompt sits (the keypad).</summary>
        public Vector3 AimPoint => transform.TransformPoint(new Vector3(-0.43f, 1.25f, 0.35f));
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

        // ================================================================ dressing

        void Awake()
        {
            Instance = this;
            var body = transform.Find("Body");
            var template = (body != null ? body.GetComponent<Renderer>() : GetComponentInChildren<Renderer>())?.sharedMaterial;
            if (SodaCan.Template == null && template != null) SodaCan.Template = template;
            var metal = new Material(template) { color = new Color(0.62f, 0.65f, 0.68f) };
            metal.SetFloat("_Metallic", 0.85f); metal.SetFloat("_Glossiness", 0.7f);
            var dark = new Material(template) { color = new Color(0.03f, 0.03f, 0.035f) };

            // Shelf cans: every one a different brand, logo out, slightly turned like real stacked cans.
            int i = 0;
            foreach (var r in new List<Transform>(GetComponentsInChildren<Transform>()))
            {
                if (r.name == "Key") keys.Add(r);
                if (r.name != "Can") continue;
                var c = SodaCan.Create(transform, i * 3 + i / 4, gameObject.layer, ShelfCanScale);
                c.transform.SetPositionAndRotation(r.position, Quaternion.Euler(0f, 90f + ((i * 37) % 21 - 10), 0f));
                Destroy(r.gameObject);
                i++;
            }

            // Pickup tray: dark recess behind a hinged flap, floor ledge, side cheeks and a front lip.
            var tray = transform.Find("Tray");
            if (tray != null) tray.gameObject.SetActive(false);
            Part("TrayRecess", new Vector3(-0.402f, 0.25f, -0.12f), new Vector3(0.01f, 0.2f, 0.55f), dark);
            Part("TrayFloor", new Vector3(-0.49f, 0.152f, -0.12f), new Vector3(0.18f, 0.02f, 0.58f), metal);
            Part("TrayCheekL", new Vector3(-0.49f, 0.2f, -0.405f), new Vector3(0.18f, 0.1f, 0.012f), metal);
            Part("TrayCheekR", new Vector3(-0.49f, 0.2f, 0.165f), new Vector3(0.18f, 0.1f, 0.012f), metal);
            Part("TrayLip", new Vector3(-0.575f, 0.185f, -0.12f), new Vector3(0.012f, 0.05f, 0.58f), metal);
            flap = new GameObject("TrayFlapHinge").transform;
            flap.SetParent(transform, false);
            flap.position = transform.TransformPoint(new Vector3(-0.415f, 0.35f, -0.12f));
            var f = Part("TrayFlap", Vector3.zero, new Vector3(0.012f, 0.19f, 0.54f), dark, flap);
            f.localPosition = new Vector3(0f, -0.095f, 0f);
            Part("TrayFlapGrip", Vector3.zero, new Vector3(0.016f, 0.02f, 0.2f), metal, flap).localPosition = new Vector3(-0.01f, -0.17f, 0f);
        }

        Transform Part(string n, Vector3 local, Vector3 size, Material m, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent != null ? parent : transform, false);
            if (parent == null) g.transform.position = transform.TransformPoint(local);
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g.transform;
        }

        Vector3 KeyPoint(int brand)
        {
            if (keys.Count == 0) return AimPoint;
            var k = keys[brand % keys.Count];
            return k.position;
        }

        // ================================================================ per frame

        void Update()
        {
            var mm = MatchManager.Instance;
            if (!mm || !mm.IsSpawned) return;
            var alive = new HashSet<int>();
            var active = new HashSet<NetPlayer>();
            float flapOpen = 0f;
            for (int i = mm.VendingCans.Count - 1; i >= 0; i--)
            {
                var s = mm.VendingCans[i];
                alive.Add(s.Id);
                if (!cans.TryGetValue(s.Id, out var can))
                {
                    can = SodaCan.Create(transform, s.Brand, 0, WorldCanScale);
                    can.name = "BoughtCan_" + s.Id;
                    cans.Add(s.Id, can);
                }
                float age = (float)(mm.Now - s.Started);
                var player = NetPlayer.Find(s.Player);
                if (s.Stage == 0)
                {
                    if (!player) { if (mm.IsServer) mm.VendingCans.RemoveAt(i); continue; }
                    active.Add(player);
                    flapOpen = Mathf.Max(flapOpen, VendingTimeline.S(age, 1.15f, 1.35f) * (1f - VendingTimeline.S(age, 1.6f, 1.95f)));
                    Pose(can, player, s, age);
                    Sounds(s.Id, player, age);
                    if (mm.IsServer && age >= VendingTimeline.Release) Throw(mm, i, s, can, player);
                }
                else
                {
                    can.gameObject.SetActive(true);
                    can.SetOpen(1f);
                    can.SetCrush(1f);
                    if (mm.IsServer) { if (!Simulate(mm, i, s, can)) continue; }
                    else can.transform.SetPositionAndRotation(
                        Vector3.Lerp(can.transform.position, s.Position, 0.5f), Quaternion.Slerp(can.transform.rotation, s.Rotation, 0.5f));
                    if (lastBounce.TryGetValue(s.Id, out var b) && b != s.Bounces)
                        GameAudio.PlayAt(GameSfx.CanBounce, can.transform.position, 1f / (1 + b));
                    lastBounce[s.Id] = s.Bounces;
                }
            }
            if (Time.unscaledTime >= nextSync) nextSync = Time.unscaledTime + 0.05f;
            foreach (var id in new List<int>(cans.Keys))
                if (!alive.Contains(id)) { Destroy(cans[id].gameObject); cans.Remove(id); bodies.Remove(id); restSince.Remove(id); lastT.Remove(id); lastBounce.Remove(id); }
            foreach (var p in new List<NetPlayer>(posing))
                if (!p || !active.Contains(p)) { ResetPose(p); posing.Remove(p); }
            if (flap != null) flap.localRotation = Quaternion.Euler(0f, 0f, -62f * flapOpen);
        }

        void Pose(SodaCan can, NetPlayer p, in VendingState s, float t)
        {
            Vector3 trayCan = transform.TransformPoint(TrayCanLocal);
            Vector3 button = KeyPoint(s.Brand);
            // World can: pushed out through the flap into the tray, waits there, hidden while it is in the hand.
            bool inTray = t >= VendingTimeline.Drop + 0.15f && t < VendingTimeline.Grip;
            can.gameObject.SetActive(inTray);
            if (inTray)
            {
                float slide = VendingTimeline.S(t, 1.15f, VendingTimeline.Land);
                float bump = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01((t - VendingTimeline.Land) / 0.25f) * Mathf.PI)) * 0.015f;
                can.transform.position = Vector3.Lerp(transform.TransformPoint(ChuteLocal), trayCan, slide) + Vector3.up * bump;
                can.transform.rotation = p.transform.rotation * VendingTimeline.GripRotation * Quaternion.Euler(0f, 90f, 0f);
                can.SetOpen(0f);
                can.SetCrush(0f);
            }
            // Key goes in while pressed.
            if (keys.Count > 0)
            {
                var k = keys[s.Brand % keys.Count];
                float press = VendingTimeline.S(t, 0.38f, 0.45f) * (1f - VendingTimeline.S(t, 0.5f, 0.62f));
                k.localPosition = new Vector3(KeyRestX(k), k.localPosition.y, k.localPosition.z) + Vector3.right * 0.008f * press;
            }

            (p.Actions as BlockyCharacterActions)?.SetVending(t, button, trayCan, s.Brand);
            posing.Add(p);
            if (!p.IsOwner) return;
            (p.ViewModel as FirstPersonViewModel)?.SetVending(t, button, trayCan, p.transform.rotation, s.Brand);
            var bob = p.GetComponentInChildren<HeadBob>();
            if (bob != null)
            {
                float crouch = VendingTimeline.Crouch(t), drink = VendingTimeline.Drink(t);
                float look = 8f * VendingTimeline.S(t, 0.05f, 0.3f) * (1f - VendingTimeline.S(t, 0.9f, 1.3f)) + 30f * crouch
                             + 16f * VendingTimeline.S(t, 2.9f, 3.6f) * (1f - VendingTimeline.S(t, 4.85f, 5.15f)) - 24f * drink;
                bob.EyeOverride = 0.85f;
                bob.EyeOverrideWeight = crouch;
                bob.ExtraEuler = new Vector3(look, -4f * VendingTimeline.S(t, 7.2f, 7.6f), 0f);
            }
        }

        readonly Dictionary<Transform, float> keyRest = new();
        float KeyRestX(Transform k)
        {
            if (!keyRest.TryGetValue(k, out var x)) keyRest[k] = x = k.localPosition.x;
            return x;
        }

        void Sounds(int id, NetPlayer p, float t)
        {
            float prev = lastT.TryGetValue(id, out var v) ? v : t;
            lastT[id] = t;
            bool Hit(float mark) => prev < mark && t >= mark;
            Vector3 head = p.transform.position + Vector3.up * 1.5f;
            if (Hit(0.4f)) GameAudio.PlayAt(GameSfx.VendButton, AimPoint, 1f);
            if (Hit(VendingTimeline.Drop - 0.35f)) GameAudio.PlayAt(GameSfx.VendDrop, transform.TransformPoint(TrayCanLocal), 1f);
            if (Hit(VendingTimeline.TabPop - 0.1f)) GameAudio.PlayAt(GameSfx.CanOpen, head - Vector3.up * 0.3f, 1f);
            if (Hit(VendingTimeline.Gulps[0] - 0.1f)) GameAudio.PlayAt(GameSfx.CanGulp, head, 1f);
            if (Hit(6.75f)) GameAudio.PlayAt(GameSfx.CanAhh, head, 1f);
            if (Hit(VendingTimeline.CrushAt - 0.05f)) GameAudio.PlayAt(GameSfx.CanCrush, head - Vector3.up * 0.35f, 1f);
        }

        // ================================================================ server physics

        void Throw(MatchManager mm, int i, VendingState s, SodaCan can, NetPlayer player)
        {
            var tr = player.transform;
            can.gameObject.SetActive(true);
            can.SetCrush(1f);
            can.transform.SetPositionAndRotation(tr.TransformPoint(new Vector3(0.22f, 1.78f, -0.3f)), tr.rotation * Quaternion.Euler(70f, 0f, 20f));
            var col = can.gameObject.AddComponent<CapsuleCollider>();
            col.radius = SodaCan.Radius * 0.9f;
            col.height = SodaCan.Height * 0.7f;
            var rb = can.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.03f;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var c in player.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c);
            rb.linearVelocity = -tr.forward * 4.2f + Vector3.up * 2.6f + tr.right * 0.5f;
            rb.angularVelocity = tr.right * 14f + tr.up * 5f;
            can.gameObject.AddComponent<CanImpact>();
            bodies[s.Id] = rb;
            s.Stage = 1;
            s.Position = can.transform.position;
            s.Rotation = can.transform.rotation;
            s.Expires = mm.Now + 45;
            player.Busy.Value = false;
            mm.VendingCans[i] = s;
        }

        bool Simulate(MatchManager mm, int i, VendingState s, SodaCan can)
        {
            if (mm.Now >= s.Expires) { mm.VendingCans.RemoveAt(i); return false; }
            if (s.Stage != 1 || !bodies.TryGetValue(s.Id, out var rb)) return true;
            var impact = can.GetComponent<CanImpact>();
            bool changed = false;
            if (impact != null && impact.Hits != s.Bounces) { s.Bounces = (byte)Mathf.Min(impact.Hits, 250); changed = true; }
            bool grounded = Physics.Raycast(can.transform.position, Vector3.down, 0.12f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            if (grounded && rb.linearVelocity.sqrMagnitude < 0.05f)
            {
                if (!restSince.ContainsKey(s.Id)) restSince[s.Id] = mm.Now;
                if (mm.Now - restSince[s.Id] > 0.5) { rb.isKinematic = true; s.Stage = 2; s.Expires = mm.Now + 30; changed = true; }
            }
            else restSince.Remove(s.Id);
            s.Position = can.transform.position;
            s.Rotation = can.transform.rotation;
            if (changed || Time.unscaledTime >= nextSync) mm.VendingCans[i] = s;
            return true;
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

    /// <summary>Counts hard hits of the thrown can (server); the count is replicated so everyone hears the bounces.</summary>
    sealed class CanImpact : MonoBehaviour
    {
        public int Hits;
        float last;
        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude < 1.2f || Time.time - last < 0.15f) return;
            last = Time.time;
            Hits++;
        }
    }
}
