using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Lobby: small table by the south wall (between the TV and the mirror) with an open wooden crate full of shoulder
    /// flashlights and a sign above it. E takes a lamp (= ready), E again puts it back (= not ready); the server side is
    /// in <c>MatchManager.FlashBox</c>. Built at runtime like the boombox (no scene / builder changes). The crate shows
    /// as many lamps as are not carried by somebody (or in a hand during the animation).
    /// </summary>
    public class FlashlightBox : MonoBehaviour
    {
        public static FlashlightBox Instance { get; private set; }

        public static readonly Vector3 TablePos = new Vector3(-1.35f, 0f, -5.78f);
        public static readonly Vector3 CratePos = new Vector3(-1.35f, 0.74f, -5.74f);
        /// <summary>Where a player stands to use the box (facing it, yaw 180) so the hand meets the lamps.</summary>
        public static readonly Vector3 StandPos = CratePos + new Vector3(FlashTakeTimeline.GrabLocal.x, -CratePos.y, FlashTakeTimeline.GrabLocal.z);
        public const float StandYaw = 180f;
        public static Vector3 AimPoint => CratePos + Vector3.up * 0.18f;
        public static Vector3 GrabWorld => StandPos + Quaternion.Euler(0f, StandYaw, 0f) * FlashTakeTimeline.GrabLocal;

        private const int Slots = 10;
        private readonly Transform[] _lamps = new Transform[Slots];
        private int _shown = -1;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("FlashlightBox");
            Instance = go.AddComponent<FlashlightBox>();
            Instance.Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static Material M(string n, Color c, float gloss = 0.3f)
        {
            var m = new Material(Shader.Find("Standard")) { color = c, name = n };
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        private static Transform Box(Transform parent, string n, Vector3 pos, Vector3 size, Material m, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }

        private void Build()
        {
            var t = transform;
            var wood = M("FlashTableWood", new Color(0.36f, 0.22f, 0.12f), 0.35f);
            var crate = M("FlashCrate", new Color(0.66f, 0.5f, 0.3f), 0.15f);
            var crateDark = M("FlashCrateDark", new Color(0.45f, 0.32f, 0.18f), 0.1f);
            var stripe = M("FlashSignStripe", new Color(0.95f, 0.75f, 0.1f), 0.4f);

            // Table: top, apron, four legs (one collider for the whole table).
            Vector3 tp = TablePos;
            Box(t, "Top", tp + new Vector3(0f, 0.72f, 0f), new Vector3(0.92f, 0.04f, 0.42f), wood);
            Box(t, "Apron", tp + new Vector3(0f, 0.66f, 0.19f), new Vector3(0.84f, 0.08f, 0.02f), wood);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.4f : 0.4f, z = i < 2 ? -0.16f : 0.16f;
                Box(t, "Leg", tp + new Vector3(x, 0.35f, z), new Vector3(0.045f, 0.7f, 0.045f), wood);
            }
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = tp + new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(0.92f, 0.9f, 0.42f);

            // Open crate: bottom, slatted walls, a darker gap between slats, rope handles.
            Vector3 c = CratePos;
            const float W = 0.62f, D = 0.36f;
            Box(t, "CrateBottom", c + new Vector3(0f, 0.01f, 0f), new Vector3(W, 0.02f, D), crateDark);
            for (int s = 0; s < 2; s++)
            {
                float y = 0.02f + 0.035f + s * 0.07f;
                Box(t, "CrateFront", c + new Vector3(0f, y, D * 0.5f), new Vector3(W, 0.06f, 0.016f), crate);
                Box(t, "CrateBack", c + new Vector3(0f, y, -D * 0.5f), new Vector3(W, 0.06f, 0.016f), crate);
                Box(t, "CrateL", c + new Vector3(-W * 0.5f, y, 0f), new Vector3(0.016f, 0.06f, D), crate);
                Box(t, "CrateR", c + new Vector3(W * 0.5f, y, 0f), new Vector3(0.016f, 0.06f, D), crate);
            }
            Box(t, "Gap", c + new Vector3(0f, 0.09f, D * 0.5f - 0.002f), new Vector3(W - 0.01f, 0.01f, 0.012f), crateDark);
            Box(t, "HandleL", c + new Vector3(-W * 0.5f - 0.012f, 0.12f, 0f), new Vector3(0.012f, 0.02f, 0.1f), crateDark);
            Box(t, "HandleR", c + new Vector3(W * 0.5f + 0.012f, 0.12f, 0f), new Vector3(0.012f, 0.02f, 0.1f), crateDark);
            // Stencil-like label strip on the front.
            Box(t, "Label", c + new Vector3(0f, 0.105f, D * 0.5f + 0.01f), new Vector3(0.3f, 0.045f, 0.004f), stripe);
            Text(t, "LabelText", c + new Vector3(0f, 0.105f, D * 0.5f + 0.0125f), "LATARKI", 0.0055f, new Color(0.1f, 0.08f, 0.05f), new Vector2(0.28f, 0.04f));

            // Riser in the back half so the back row shows over the front one.
            Box(t, "Riser", c + new Vector3(0f, 0.045f, -D * 0.25f + 0.004f), new Vector3(W - 0.034f, 0.05f, D * 0.5f - 0.02f), crateDark);

            // Lamps lying in two rows inside the walls (inner size 0.6 x 0.34 m), lenses towards the room, tipped up.
            var rnd = new System.Random(7);
            for (int i = 0; i < Slots; i++)
            {
                int row = i / 5, col2 = i % 5;
                var lamp = new GameObject("Lamp" + i).transform;
                lamp.SetParent(t, false);
                lamp.position = c + new Vector3(-0.23f + col2 * 0.115f, row == 0 ? 0.068f : 0.118f, row == 0 ? 0.052f : -0.112f);
                lamp.rotation = Quaternion.Euler(-15f, (float)(rnd.NextDouble() * 8 - 4), (float)(rnd.NextDouble() * 10 - 5));
                LampModel.Build(lamp, false, 0);
                _lamps[i] = lamp;
            }

            // Warm little lamp light so the box reads in the room.
            var lg = new GameObject("BoxLight");
            lg.transform.SetParent(t, false);
            lg.transform.position = c + new Vector3(0f, 0.75f, 0.35f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.6f);
            l.range = 1.8f;
            l.intensity = 0.7f;
            l.shadows = LightShadows.None;
        }

        private static void Text(Transform parent, string n, Vector3 pos, string text, float size, Color color, Vector2 fit)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            WorldText.Setup(tm, fit);
        }

        private void Update()
        {
            int carried = 0;
            foreach (var p in NetPlayer.All)
                if (p != null && p.LampWithPlayer) carried++;
            int shown = Mathf.Clamp(Slots - carried, 2, Slots);
            if (shown == _shown) return;
            _shown = shown;
            for (int i = 0; i < Slots; i++)
                if (_lamps[i] != null) _lamps[i].gameObject.SetActive(i < shown);
        }

        /// <summary>E prompt when looking at the box from close by.</summary>
        public static bool TryPrompt(NetPlayer me, Vector3 eye, Vector3 fwd, out Vector3 pos, out string text, out float dist)
        {
            pos = AimPoint;
            text = null;
            dist = Vector3.Distance(eye, pos);
            if (Instance == null || me == null || dist > 2.3f || Vector3.Angle(fwd, pos - eye) > 38f) return false;
            text = me.Ready.Value ? "E  odłóż latarkę (nie gotowy)" : "E  weź latarkę (gotowy)";
            return true;
        }
    }
}
