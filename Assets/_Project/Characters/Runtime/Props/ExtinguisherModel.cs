using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Procedural 6 kg powder fire extinguisher: red tank with a rounded foot and a domed shoulder, brass neck, chrome
    /// valve head with the pressure gauge (moving needle) on the front, black carry handle and squeeze lever pointing
    /// back, safety pin with a pull ring and a plastic seal tag, rubber hose with a nozzle (or a horn) and the wrap-around
    /// instruction label. Model space: origin on the floor under the tank, +Y up, +Z front (label, gauge), the handles
    /// point to -Z, the hose leaves the head on the +X side. Moving parts are separate transforms (<see cref="Parts"/>):
    /// the lever (squeeze), the needle (charge), the pin and seal (pulled out once), the hose (re-laid every frame
    /// while held) and the nozzle. Shared by the wall copy (Game), the third-person hands and the first-person view.
    /// </summary>
    public static partial class ExtinguisherModel
    {
        public sealed class Style
        {
            public string Name;
            public float TankR = 0.078f, TankTop = 0.44f, NeckY = 0.5f, Head = 1f, HoseR = 0.0085f, GaugeR = 0.019f;
            public bool Horn;
            public Color Paint = new(0.74f, 0.05f, 0.03f);
        }

        /// <summary>Variants for the preview: A classic slim with a short nozzle, B squat with a horn, C chunky toy-like.</summary>
        public static readonly Style[] Styles =
        {
            new Style { Name = "A" },
            new Style { Name = "B", TankR = 0.088f, TankTop = 0.4f, NeckY = 0.465f, Horn = true },
            new Style { Name = "C", TankR = 0.092f, TankTop = 0.42f, NeckY = 0.49f, Head = 1.3f, HoseR = 0.012f, GaugeR = 0.027f, Paint = new Color(0.82f, 0.08f, 0.04f) },
        };

        /// <summary>Style used by the game.</summary>
        public static int GameStyle = 0;

        public sealed class Parts
        {
            public Transform Root, Lever, Needle, Pin, Seal, Nozzle;
            public MeshFilter Hose;
            public Mesh HoseMesh;
            public Style Style;
            public readonly List<Renderer> Renderers = new();
            public Vector3 LastHoseEnd = new(9f, 9f, 9f), LastHoseDir;
            public float Charge = 1f, Squeeze;
        }

        /// <summary>Frame values of a style (model space).</summary>
        public static float HeadY(Style s) => s.NeckY + 0.05f * s.Head;
        /// <summary>Point of the carry handle in the middle of the fist (lever squeezed onto it by the palm).</summary>
        public static Vector3 GripLocal(Style s) => new(0f, HeadY(s) + 0.012f * s.Head, -0.1f * s.Head);
        /// <summary>Where the hose leaves the valve head and its direction.</summary>
        public static Vector3 HoseOutLocal(Style s) => new(0.03f * s.Head, HeadY(s) - 0.012f * s.Head, 0.004f);
        /// <summary>Hose clip on the tank side: the nozzle rests here (pointing down) on the wall.</summary>
        public static Vector3 ClipLocal(Style s) => new(s.TankR + 0.012f, s.TankTop * 0.72f, 0.0f);
        /// <summary>Middle of the pull ring of the safety pin.</summary>
        public static Vector3 RingLocal(Style s) => new(0.052f * s.Head, HeadY(s) + 0.01f * s.Head, -0.04f * s.Head);
        /// <summary>Length of the nozzle from the hose coupling to its tip (nozzle space +Z).</summary>
        public static float NozzleLength(Style s) => s.Horn ? 0.15f : 0.105f;
        /// <summary>Top of the whole model (handles included).</summary>
        public static float Height(Style s) => HeadY(s) + 0.07f * s.Head;

        public static Parts Create(Transform parent, int layer, ShadowCastingMode shadows = ShadowCastingMode.On, int style = -1)
        {
            var s = Styles[Mathf.Clamp(style < 0 ? GameStyle : style, 0, Styles.Length - 1)];
            var set = MeshesFor(s);
            var p = new Parts { Style = s };
            p.Root = new GameObject("Extinguisher").transform;
            p.Root.SetParent(parent, false);
            Add(p, "Tank", p.Root, set.Body, set.BodyMats);
            Add(p, "Label", p.Root, set.Label, new[] { set.LabelMat });

            p.Lever = new GameObject("Lever").transform;
            p.Lever.SetParent(p.Root, false);
            p.Lever.localPosition = LeverPivot(s);
            Add(p, "LeverMesh", p.Lever, set.Lever, new[] { set.BodyMats[SubBlack], set.BodyMats[SubChrome] });

            var dial = new GameObject("Dial").transform;
            dial.SetParent(p.Root, false);
            dial.localPosition = GaugeCentre(s) + Vector3.forward * (0.0105f * s.Head);
            Add(p, "Face", dial, set.Dial, new[] { set.DialMat });
            p.Needle = new GameObject("Needle").transform;
            p.Needle.SetParent(dial, false);
            p.Needle.localPosition = new Vector3(0f, 0f, 0.0016f);
            Add(p, "NeedleMesh", p.Needle, set.Needle, new[] { set.NeedleMat });

            p.Pin = new GameObject("Pin").transform;
            p.Pin.SetParent(p.Root, false);
            Add(p, "PinMesh", p.Pin, set.Pin, new[] { set.BodyMats[SubChrome] });
            p.Seal = new GameObject("Seal").transform;
            p.Seal.SetParent(p.Root, false);
            Add(p, "SealMesh", p.Seal, set.Seal, new[] { set.SealMat });

            var hose = new GameObject("Hose");
            hose.transform.SetParent(p.Root, false);
            p.Hose = hose.AddComponent<MeshFilter>();
            p.HoseMesh = new Mesh { name = "ExtHose" };
            p.HoseMesh.MarkDynamic();
            p.Hose.sharedMesh = p.HoseMesh;
            var hr = hose.AddComponent<MeshRenderer>();
            hr.sharedMaterial = set.BodyMats[SubRubber];
            p.Renderers.Add(hr);

            p.Nozzle = new GameObject("Nozzle").transform;
            p.Nozzle.SetParent(p.Root, false);
            Add(p, "NozzleMesh", p.Nozzle, set.Nozzle, new[] { set.BodyMats[SubBlack], set.BodyMats[SubChrome], set.BodyMats[SubRubber] });

            SetRender(p, layer, shadows);
            RestHose(p);
            SetCharge(p, 1f);
            SetSqueeze(p, 0f);
            return p;
        }

        static void Add(Parts p, string name, Transform parent, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            p.Renderers.Add(r);
        }

        public static void SetRender(Parts p, int layer, ShadowCastingMode shadows)
        {
            if (p == null || p.Root == null) return;
            foreach (var t in p.Root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in p.Renderers)
            {
                if (r == null) continue;
                r.shadowCastingMode = shadows;
                r.receiveShadows = shadows != ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        /// <summary>Gauge needle for a charge of 0..1 (falls as the extinguisher empties).</summary>
        public static void SetCharge(Parts p, float charge)
        {
            if (p == null || p.Needle == null) return;
            p.Charge = Mathf.Clamp01(charge);
            p.Needle.localRotation = Quaternion.Euler(0f, 0f, -ExtinguisherArt.NeedleAngle(p.Charge));
        }

        /// <summary>Lever pressed down onto the carry handle (0 = open, 1 = squeezed).</summary>
        public static void SetSqueeze(Parts p, float squeeze)
        {
            if (p == null || p.Lever == null) return;
            p.Squeeze = Mathf.Clamp01(squeeze);
            p.Lever.localRotation = Quaternion.Euler(LeverOpen - 11f * p.Squeeze, 0f, 0f);
        }

        /// <summary>Pin and seal still in the valve (hidden once pulled; the game throws a loose copy).</summary>
        public static void SetPinIn(Parts p, bool pinIn)
        {
            if (p == null || p.Pin == null) return;
            if (p.Pin.gameObject.activeSelf != pinIn) p.Pin.gameObject.SetActive(pinIn);
            if (p.Seal.gameObject.activeSelf != pinIn) p.Seal.gameObject.SetActive(pinIn);
        }

        /// <summary>Hose and nozzle resting in the clip on the tank side (wall pose).</summary>
        public static void RestHose(Parts p)
        {
            var s = p.Style;
            Vector3 clip = ClipLocal(s);
            // Nozzle hangs in the clip pointing down, its coupling a bit above the clip.
            Vector3 end = clip + new Vector3(0.006f, 0.05f, 0.0f);
            LayHose(p, end, Vector3.down);
        }

        /// <summary>
        /// Puts the nozzle with its coupling at <paramref name="end"/> (model space) pointing along <paramref name="dir"/>
        /// and re-lays the hose from the valve head to it (only when it moved noticeably).
        /// </summary>
        public static void LayHose(Parts p, Vector3 end, Vector3 dir)
        {
            if (p == null || p.Nozzle == null) return;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.down;
            Vector3 up = Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up;
            p.Nozzle.localPosition = end;
            p.Nozzle.localRotation = Quaternion.LookRotation(dir, up);
            if ((end - p.LastHoseEnd).sqrMagnitude < 0.0015f * 0.0015f && Vector3.Dot(dir, p.LastHoseDir) > 0.9995f) return;
            p.LastHoseEnd = end;
            p.LastHoseDir = dir;
            BuildHose(p, end, dir);
        }

        /// <summary>A loose safety pin with its ring and torn seal (origin = middle of the ring, same axes as the model),
        /// with a small box collider - the game gives it a rigidbody.</summary>
        public static GameObject CreateLoosePin(Transform parent, int layer)
        {
            var s = Styles[GameStyle];
            var set = MeshesFor(s);
            var root = new GameObject("ExtPin") { layer = layer };
            root.transform.SetParent(parent, false);
            var off = -RingLocal(s);
            foreach (var (mesh, mat) in new[] { (set.Pin, set.BodyMats[SubChrome]), (set.Seal, set.SealMat) })
            {
                var go = new GameObject("Part") { layer = layer };
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = off;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(-0.035f * s.Head, 0f, 0f);
            bc.size = new Vector3(0.1f * s.Head, 0.03f, 0.012f);
            return root;
        }

        /// <summary>Tip of the nozzle in world space (spray origin) and the spray direction.</summary>
        public static Vector3 TipWorld(Parts p) => p.Nozzle.TransformPoint(new Vector3(0f, 0f, NozzleLength(p.Style)));
    }
}
