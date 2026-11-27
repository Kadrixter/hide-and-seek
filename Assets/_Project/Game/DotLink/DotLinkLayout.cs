using HideAndSeek.Game.Cinematics;
using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Where the dot link screen is: inside the electrical box of the exit gate (<see cref="ExitGate"/>, the one whose
    /// lever the killer tears off in the intro), behind its door, left of the lever stub. Screen centre, its outward
    /// normal, where the user stands (facing the box) and the close-up camera in front of it. Same numbers on the
    /// server (distance checks) and on every client (panel, prompt, camera).
    /// </summary>
    public struct DotLinkLayout
    {
        /// <summary>Screen centre in the box (box local; the front of the box body is at z = 0.14).</summary>
        public static readonly Vector3 ScreenInBox = new Vector3(-0.03f, -0.04f, 0.1415f);
        public const float ScreenSize = 0.25f;

        public Transform Box;
        public Vector3 Centre, Normal, Up, Stand;
        public float Yaw;
        /// <summary>0 = east gate, 1 = west gate (both boxes have the same panel, one shared puzzle).</summary>
        public int Gate;

        /// <summary>The east gate's panel (tests).</summary>
        public static bool TryGet(out DotLinkLayout l) => TryGet(0, out l);

        /// <summary>The panel of the gate nearer to <paramref name="world"/>.</summary>
        public static bool TryNearest(Vector3 world, out DotLinkLayout l)
        {
            l = default;
            bool any = false;
            float best = float.MaxValue;
            for (int i = 0; i < ExitGate.Count; i++)
            {
                if (!TryGet(i, out var c)) continue;
                float d = (c.Stand - world).sqrMagnitude;
                if (d < best) { best = d; l = c; any = true; }
            }
            return any;
        }

        public static bool TryGet(int gateIndex, out DotLinkLayout l)
        {
            l = default;
            var gate = ExitGate.Get(gateIndex);
            if (gate == null) return false;
            l.Gate = gateIndex;
            var box = gate.transform.Find("ElectricBox");
            if (box == null) return false;
            l.Box = box;
            l.Centre = box.TransformPoint(ScreenInBox);
            l.Normal = box.forward;
            l.Up = box.up;
            Vector3 flat = new Vector3(l.Normal.x, 0f, l.Normal.z).normalized;
            l.Stand = new Vector3(l.Centre.x, gate.transform.position.y + 0.02f, l.Centre.z) + flat * 0.62f;
            l.Yaw = Mathf.Atan2(-flat.x, -flat.z) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>Close-up camera straight in front of the screen so it fills most of the view (vertical fov in degrees).</summary>
        public void CameraPose(float fov, float zoom, out Vector3 pos, out Quaternion rot)
        {
            float d = ScreenSize / 0.8f / (2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad)) * zoom;
            pos = Centre + Normal * d;
            rot = Quaternion.LookRotation(-Normal, Up);
        }
    }
}
