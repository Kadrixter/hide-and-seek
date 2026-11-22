using System.Collections.Generic;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// First-person side of the selected inventory item (<see cref="HandGrip"/>): the right fist holds it low on the
    /// right of the view (never over the crosshair), the left hand joins for two-handed items. A change goes through
    /// the same lower / swap at the bottom / raise as the weapons (<see cref="BeginSwitch"/>), and every hold animation
    /// (healing, injection, phone, vending, toy gun...) and every weapon takes the hands first.
    /// </summary>
    public partial class FirstPersonViewModel
    {
        int carryKey, carryShownKey;
        HandGrip carryGrip, carryShownGrip;
        bool carryHands;
        readonly Dictionary<int, (Transform pivot, HandGrip grip)> carryModels = new Dictionary<int, (Transform, HandGrip)>();

        /// <summary>Item wanted in the right hand (0 / null = none); switching lowers the rig, swaps and raises it again.</summary>
        public void SetCarried(int key, HandGrip grip)
        {
            if (grip == null) key = 0;
            if (key == carryKey && grip == carryGrip) return;
            if (key == 0 && carryKey == 0) return;
            carryKey = key;
            carryGrip = key != 0 ? grip : null;
            BeginSwitch();
        }

        /// <summary>Key of the item shown in the first-person hand (0 = none).</summary>
        public int CarriedShown => carryHands ? carryShownKey : 0;

        /// <summary>
        /// Starts (or continues) the lower / swap / raise motion: never jumps up - while already going down the swap at
        /// the bottom simply takes the newest state; while coming up it turns round at the current height.
        /// </summary>
        void BeginSwitch()
        {
            if (!visible)
            {
                switchTime = 99f;
                shownItem = heldItem;
                carryShownKey = carryKey;
                carryShownGrip = carryGrip;
                return;
            }
            if (switchTime < LowerTime) return;
            float lower = switchTime < LowerTime + RaiseTime ? 1f - ActionTimeline.SmoothStep01((switchTime - LowerTime) / RaiseTime) : 0f;
            float a = 0f, b = 1f;
            for (int i = 0; i < 12; i++)
            {
                float m = (a + b) * 0.5f;
                if (ActionTimeline.SmoothStep01(m) < lower) a = m;
                else b = m;
            }
            switchTime = (a + b) * 0.5f * LowerTime;
        }

        /// <summary>Swap point of the switch (bottom of the lowering).</summary>
        void SwapCarried()
        {
            carryShownKey = carryKey;
            carryShownGrip = carryGrip;
        }

        /// <summary>Hands for the carried item (rig space), when nothing else is in the hand.</summary>
        bool CarryHands(HeldItem show, out Vector3 rightT, out Quaternion propRot, ref Vector3 leftT, ref float wantLeft)
        {
            rightT = default;
            propRot = Quaternion.identity;
            var g = carryShownGrip;
            if (show != HeldItem.None || g == null) return false;
            rightT = g.FpHand;
            propRot = Quaternion.Euler(g.FpRotation);
            SolveArm(shR, elR, ShoulderR, rightT, new Vector3(1f, -1f, -0.4f));
            if (g.FpLeft.HasValue)
            {
                leftT = HandPos(shR, elR) + propRot * ((g.FpLeft.Value - g.FpGripPoint) * (g.Scale * g.FpScale));
                wantLeft = 1f;
            }
            carryHands = true;
            return true;
        }

        void UpdateCarryFp()
        {
            Transform active = null;
            var g = carryShownGrip;
            if (carryHands && g != null)
            {
                if (!carryModels.TryGetValue(carryShownKey, out var m) || m.pivot == null || m.grip != g)
                {
                    if (m.pivot != null) Destroy(m.pivot.gameObject);
                    m = (g.Build(socketR, g.FpGripPoint, Layer, g.Scale * g.FpScale, ShadowCastingMode.Off, false), g);
                    carryModels[carryShownKey] = m;
                }
                active = m.pivot;
                float s = g.Scale * g.FpScale;
                active.localScale = Vector3.one * s;
                active.localPosition = -(g.FpGripPoint * s);
                active.localRotation = Quaternion.identity;
            }
            foreach (var kv in carryModels)
            {
                var t = kv.Value.pivot;
                if (t == null) continue;
                bool on = t == active;
                if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            }
        }
    }
}
