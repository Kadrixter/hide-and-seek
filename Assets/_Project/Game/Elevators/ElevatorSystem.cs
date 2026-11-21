using System;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Networked state of one elevator car (lives in MatchManager.Elevators).</summary>
    public struct ElevatorState : INetworkSerializable, IEquatable<ElevatorState>
    {
        public byte Car;
        public sbyte Floor;     // index into ElevatorSystem.Car.Floors (0 = top level)
        public sbyte Target;
        public byte Mode;       // 0 closed, 1 opening/open, 2 closing, 3 moving
        public double ModeStart;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Car);
            s.SerializeValue(ref Floor);
            s.SerializeValue(ref Target);
            s.SerializeValue(ref Mode);
            s.SerializeValue(ref ModeStart);
        }

        public bool Equals(ElevatorState o) => Car == o.Car && Floor == o.Floor && Target == o.Target && Mode == o.Mode && ModeStart == o.ModeStart;
    }

    /// <summary>
    /// Working elevators in the central core of the garage. The map provides cabins (one per car per level) and
    /// markers "ElevatorCar_{car}_L{level}"; this builds sliding doors, call buttons and the cabin panel, runs the
    /// door / travel timeline from the networked state and plays the sounds. Riding = doors close, the car "travels",
    /// and everyone inside is moved to the identical cabin on the target level.
    /// </summary>
    public class ElevatorSystem : MonoBehaviour
    {
        public const float DoorTime = 0.9f;
        public const float HoldOpen = 5f;
        public const float TravelPerFloor = 2.2f;
        public const float TravelBase = 0.8f;
        private const float HalfWidth = 1.05f, HalfDepth = 1.4f, FaceZ = 1.4f;
        private const float DoorHalf = 0.44f, DoorH = 2.23f; // each leaf 0.88 m wide (3 cm behind the jamb), up into the header

        public static ElevatorSystem Instance { get; private set; }

        public class Floor
        {
            public float Y;
            public string Name;
            public Transform[] DoorL = new Transform[2], DoorR = new Transform[2]; // per face (0 = -z, 1 = +z)
            public Renderer[] CallLamp = new Renderer[2];
            public TextMesh[] Display = new TextMesh[2];
        }

        public class Car
        {
            public int Index;
            public float X;
            public readonly List<Floor> Floors = new();
            public AudioSource MoveLoop;
            public ElevatorState Last;
            public bool HasLast;
        }

        public readonly List<Car> Cars = new();
        private static Material _doorMat, _btnMat, _lampMat, _panelMat;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            Instance = this;
            _mpb = new MaterialPropertyBlock();
            Scan();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================ build

        private void Scan()
        {
            var byCar = new SortedDictionary<int, List<Transform>>();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.name.StartsWith("ElevatorCar_")) continue;
                string[] p = t.name.Split('_');
                if (p.Length < 3 || !int.TryParse(p[1], out int car)) continue;
                if (!byCar.TryGetValue(car, out var list)) byCar[car] = list = new List<Transform>();
                list.Add(t);
            }
            EnsureMaterials();
            foreach (var kv in byCar)
            {
                var car = new Car { Index = kv.Key, X = kv.Value[0].position.x };
                kv.Value.Sort((a, b) => b.position.y.CompareTo(a.position.y)); // top level first
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    float y = kv.Value[i].position.y;
                    var f = new Floor { Y = y, Name = i == 0 ? "0" : "-" + i };
                    BuildFloor(car, f, i, kv.Value.Count);
                    car.Floors.Add(f);
                }
                Cars.Add(car);
            }
            Debug.Log($"[Elevators] {Cars.Count} cars");
        }

        private static void EnsureMaterials()
        {
            if (_doorMat != null) return;
            _doorMat = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.57f, 0.6f) };
            _doorMat.SetFloat("_Metallic", 0.8f);
            _doorMat.SetFloat("_Glossiness", 0.55f);
            _panelMat = new Material(Shader.Find("Standard")) { color = new Color(0.12f, 0.12f, 0.13f) };
            _panelMat.SetFloat("_Metallic", 0.6f);
            _btnMat = new Material(Shader.Find("Standard")) { color = new Color(0.8f, 0.8f, 0.78f) };
            _btnMat.EnableKeyword("_EMISSION");
            _lampMat = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.6f, 0.1f) };
            _lampMat.EnableKeyword("_EMISSION");
        }

        private void BuildFloor(Car car, Floor f, int floorIndex, int floorCount)
        {
            var root = new GameObject($"Elevator{car.Index}_Floor{floorIndex}").transform;
            root.SetParent(transform, false);
            float x = car.X, y = f.Y;
            for (int face = 0; face < 2; face++)
            {
                float s = face == 0 ? -1f : 1f;
                float z = s * FaceZ + s * 0.055f;
                // Leaves overlap the jambs / header by 3 cm (no light gap at the edges when closed).
                bool oldDoors = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nojambfix") >= 0;
                float dh = oldDoors ? 0.425f : DoorHalf, dH = oldDoors ? 2.2f : DoorH;
                f.DoorL[face] = Box(root, "DoorL", new Vector3(x - dh, y + dH / 2f, z), new Vector3(dh * 2f, dH, 0.05f), _doorMat, true);
                f.DoorR[face] = Box(root, "DoorR", new Vector3(x + dh, y + dH / 2f, z), new Vector3(dh * 2f, dH, 0.05f), _doorMat, true);
                // The map's jambs end at +-1.00 m but the shaft opening is +-1.05 m: fill that 5 cm slot (full height,
                // flush with the core wall), otherwise the cabin is visible next to the frame with the doors closed.
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nojambfix") < 0)
                for (int side = -1; side <= 1; side += 2)
                    Box(root, "JambFill", new Vector3(x + side * 1.022f, y + 1.24f, s * (FaceZ + 0.07f)), new Vector3(0.075f, 2.48f, 0.2f), _doorMat, true);
                // Call-button lamp on the existing wall plate.
                f.CallLamp[face] = Box(root, "CallLamp", CallButtonPos(car, f, face), new Vector3(0.045f, 0.045f, 0.02f), _lampMat, false).GetComponent<Renderer>();
                // Floor display above the doors (inside face of the header, readable from the lobby).
                var disp = new GameObject("Display").AddComponent<TextMesh>();
                disp.transform.SetParent(root, false);
                disp.transform.position = new Vector3(x + 0.6f, y + 2.62f, s * FaceZ + s * 0.07f);
                disp.transform.rotation = Quaternion.Euler(0f, s > 0 ? 180f : 0f, 0f);
                disp.characterSize = 0.022f;
                disp.fontSize = 60;
                disp.anchor = TextAnchor.MiddleCenter;
                disp.color = new Color(1f, 0.35f, 0.2f);
                disp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                WorldText.Setup(disp);
                f.Display[face] = disp;
            }
            // Dim cabin light under the ceiling panel.
            var light = new GameObject("CabinLight").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = new Vector3(x, y + 2.35f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.92f, 0.78f);
            light.range = 3.6f;
            light.intensity = 0.7f;
            light.shadows = LightShadows.None;

            // Cabin panel on the +x wall, buttons for every level (top first).
            var panelPos = new Vector3(x + HalfWidth - 0.06f, y + 1.25f, 0f);
            Box(root, "Panel", panelPos, new Vector3(0.03f, 0.55f, 0.3f), _panelMat, false);
            for (int t = 0; t < floorCount; t++)
            {
                Vector3 bp = PanelButtonPos(car, f, t);
                Box(root, "Button", bp, new Vector3(0.025f, 0.08f, 0.08f), _btnMat, false);
                var label = new GameObject("ButtonLabel").AddComponent<TextMesh>();
                label.transform.SetParent(root, false);
                label.transform.position = bp + new Vector3(-0.016f, 0f, -0.1f);
                label.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                label.text = t == 0 ? "0" : "-" + t;
                label.characterSize = 0.006f;
                label.fontSize = 60;
                label.anchor = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                WorldText.Setup(label);
            }
        }

        public Vector3 CallButtonPos(Car car, Floor f, int face)
        {
            float s = face == 0 ? -1f : 1f;
            return new Vector3(car.X + 1.25f, f.Y + 1.15f, s * FaceZ + s * 0.08f);
        }

        /// <summary>World position of the cabin panel button for <paramref name="target"/> level, in the cabin on <paramref name="f"/>.</summary>
        public Vector3 PanelButtonPos(Car car, Floor f, int target)
        {
            return new Vector3(car.X + HalfWidth - 0.08f, f.Y + 1.42f - target * 0.15f, 0f);
        }

        private Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }

        /// <summary>True if the point is inside the cabin of <paramref name="car"/> on level <paramref name="floor"/>.</summary>
        public bool InCabin(Car car, int floor, Vector3 p)
        {
            var f = car.Floors[floor];
            return Mathf.Abs(p.x - car.X) < HalfWidth - 0.05f && Mathf.Abs(p.z) < HalfDepth + 0.05f && p.y > f.Y - 0.6f && p.y < f.Y + 2.2f;
        }

        public static float DoorOpen(in ElevatorState s, double now)
        {
            switch (s.Mode)
            {
                case 1: return Mathf.Clamp01((float)(now - s.ModeStart) / DoorTime);
                case 2: return 1f - Mathf.Clamp01((float)(now - s.ModeStart) / DoorTime);
                default: return 0f;
            }
        }

        // ================================================================ visuals + sounds (every client)

        private void Update()
        {
            var mm = MatchManager.Instance;
            if (mm == null) return;
            double now = mm.Now;
            for (int i = 0; i < mm.Elevators.Count; i++)
            {
                var st = mm.Elevators[i];
                if (st.Car >= Cars.Count) continue;
                var car = Cars[st.Car];
                float open = DoorOpen(st, now);
                for (int fi = 0; fi < car.Floors.Count; fi++)
                {
                    var f = car.Floors[fi];
                    float o = fi == st.Floor ? open : 0f;
                    float slide = Mathf.SmoothStep(0f, 1f, o) * 0.8f;
                    for (int face = 0; face < 2; face++)
                    {
                        float s = face == 0 ? -1f : 1f;
                        float z = s * FaceZ + s * 0.055f;
                        f.DoorL[face].position = new Vector3(car.X - DoorHalf - slide, f.Y + DoorH / 2f, z);
                        f.DoorR[face].position = new Vector3(car.X + DoorHalf + slide, f.Y + DoorH / 2f, z);
                        bool called = st.Target == fi && st.Floor != fi;
                        f.CallLamp[face].GetPropertyBlock(_mpb);
                        _mpb.SetColor("_EmissionColor", called ? new Color(1f, 0.55f, 0.1f) * 2.5f : Color.black);
                        f.CallLamp[face].SetPropertyBlock(_mpb);
                        f.Display[face].text = st.Mode == 3 ? (st.Target < st.Floor ? "^ " : "v ") + car.Floors[st.Target].Name : car.Floors[st.Floor].Name;
                    }
                }
                PlaySounds(car, st);
            }
        }

        private void PlaySounds(Car car, in ElevatorState st)
        {
            if (!car.HasLast)
            {
                car.Last = st;
                car.HasLast = true;
                return;
            }
            var prev = car.Last;
            car.Last = st;
            if (prev.Mode == st.Mode && prev.Floor == st.Floor && prev.ModeStart == st.ModeStart) return;

            Vector3 cabin = new Vector3(car.X, car.Floors[Mathf.Clamp(st.Floor, 0, car.Floors.Count - 1)].Y + 1.4f, 0f);
            if (st.Mode == 1 && prev.Mode != 1)
            {
                GameAudio.PlayAt(GameSfx.ElevatorDing, cabin);
                GameAudio.PlayAt(GameSfx.ElevatorDoor, cabin, 0.8f);
            }
            if (st.Mode == 2 && prev.Mode != 2) GameAudio.PlayAt(GameSfx.ElevatorDoor, cabin, 0.8f);
            if (st.Mode == 3 && prev.Mode != 3)
            {
                // Hum in the departure cabin; it follows the riders (they are moved to the same spot on arrival).
                var from = new Vector3(car.X, car.Floors[st.Floor].Y + 1.4f, 0f);
                car.MoveLoop = GameAudio.StartLoop(GameSfx.ElevatorMove, MoveAnchor(car, from), 1f);
            }
            if (st.Mode != 3 && car.MoveLoop != null)
            {
                GameAudio.StopLoop(car.MoveLoop, 0.4f);
                car.MoveLoop = null;
            }
        }

        private Transform MoveAnchor(Car car, Vector3 pos)
        {
            var t = new GameObject("ElevatorHum").transform;
            t.SetParent(transform, false);
            t.position = pos;
            Destroy(t.gameObject, 30f);
            return t;
        }

        // ================================================================ server logic (MatchManager.Update)

        public void ServerTick(MatchManager mm, double now)
        {
            for (int i = 0; i < mm.Elevators.Count; i++)
            {
                var st = mm.Elevators[i];
                var before = st;
                float t = (float)(now - st.ModeStart);
                switch (st.Mode)
                {
                    case 0:
                        if (st.Target != st.Floor) { st.Mode = 3; st.ModeStart = now; }
                        break;
                    case 1:
                        if (st.Target != st.Floor && t > DoorTime + 1.2f) { st.Mode = 2; st.ModeStart = now; }
                        else if (t > DoorTime + HoldOpen) { st.Mode = 2; st.ModeStart = now; }
                        break;
                    case 2:
                        if (t > DoorTime) { st.Mode = (byte)(st.Target != st.Floor ? 3 : 0); st.ModeStart = now; }
                        break;
                    case 3:
                        float travel = TravelBase + TravelPerFloor * Mathf.Abs(st.Target - st.Floor);
                        if (t > travel)
                        {
                            MoveRiders(Cars[st.Car], st.Floor, st.Target);
                            st.Floor = st.Target;
                            st.Mode = 1;
                            st.ModeStart = now;
                        }
                        break;
                }
                if (!st.Equals(before)) mm.Elevators[i] = st;
            }
        }

        public void ServerRequest(MatchManager mm, int carIndex, int floor, double now)
        {
            for (int i = 0; i < mm.Elevators.Count; i++)
            {
                var st = mm.Elevators[i];
                if (st.Car != carIndex) continue;
                if (floor < 0 || floor >= Cars[carIndex].Floors.Count) return;
                if (st.Mode == 3) return; // already travelling
                st.Target = (sbyte)floor;
                if (floor == st.Floor)
                {
                    if (st.Mode == 0 || st.Mode == 2)
                    {
                        // Re-open (from closing: continue from the current door position).
                        double back = st.Mode == 2 ? DoorTime * DoorOpen(st, now) : 0f;
                        st.Mode = 1;
                        st.ModeStart = now - back;
                    }
                    else if (st.Mode == 1 && now - st.ModeStart > DoorTime) st.ModeStart = now - DoorTime; // keep open
                }
                mm.Elevators[i] = st;
                return;
            }
        }

        private void MoveRiders(Car car, int from, int to)
        {
            float dy = car.Floors[to].Y - car.Floors[from].Y;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !InCabin(car, from, p.transform.position)) continue;
                p.TeleportRpc(p.transform.position + new Vector3(0f, dy, 0f), p.transform.eulerAngles.y);
            }
        }

        public List<ElevatorState> InitialStates()
        {
            var list = new List<ElevatorState>();
            foreach (var c in Cars)
                list.Add(new ElevatorState { Car = (byte)c.Index, Floor = (sbyte)(c.Index % c.Floors.Count), Target = (sbyte)(c.Index % c.Floors.Count), Mode = 0 });
            return list;
        }
    }
}
