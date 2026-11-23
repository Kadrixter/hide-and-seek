using System.Collections.Generic;
using HideAndSeek.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// The selected inventory item in the right hand (third person) - one generic layer for every item described by a
    /// <see cref="HandGrip"/> (medkit, syringe, RC car, rod, phone...). Switching puts the old one away (the fist drops
    /// to the hip and the model shrinks into the pocket), swaps at the bottom and draws the new one. The item is only
    /// out while the hands are free: any one-shot action, healing, sitting, crawling, the RC car, a weapon or another
    /// prop in the hand puts it away first (instantly when the hands are needed at once). Weapons (HeldItem) also go
    /// down before a carried item comes up and the other way round (see <see cref="DrawnItem"/>).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        const float CarryRaiseTime = 0.26f, CarryLowerTime = 0.18f, CarryShowFrom = 0.22f;

        int carryKey, carryShownKey;
        HandGrip carryGrip, carryShownGrip;
        float carryW;
        bool carryFp;
        int carryLayer = -1;
        HeldItem drawnItem;
        readonly Dictionary<int, (Transform pivot, HandGrip grip)> carryModels = new Dictionary<int, (Transform, HandGrip)>();

        /// <summary>
        /// Item wanted in the right hand: <paramref name="key"/> identifies it (0 = nothing, e.g. the item kind), the grip
        /// says how it is held. Call every frame or on change; the layer itself waits until the hands are free.
        /// </summary>
        public void SetCarried(int key, HandGrip grip)
        {
            if (grip == null) key = 0;
            carryKey = key;
            carryGrip = key != 0 ? grip : null;
        }

        /// <summary>Key of the item currently out in the hand (0 = none or being put away / drawn below half way).</summary>
        public int CarriedShown => carryW > 0.5f ? carryShownKey : 0;

        /// <summary>0..1 how far the carried item is out (1 = held).</summary>
        public float CarryWeight => carryW;

        /// <summary>The fist with the carried item while it is shown (null when none).</summary>
        public Transform CarrySocket => carryW > CarryShowFrom && carryShownGrip != null ? socketR : null;

        /// <summary>Hands needed right now by something that cannot wait for the put-away motion.</summary>
        bool CarryHardBlocked =>
            VisualLifeState != LifeState.Alive || pathActive || action != ActionKind.None || shownItem != HeldItem.None ||
            PhoneSmashing || shockT >= 0f || gunT >= 0f || overdoseT >= 0f || flashT >= 0f || rodAttachT >= 0f || stompT >= 0f || Pushing ||
            CrateSawing || vendingOn || slashT < SlashDuration || sawSlashT < SawSlashDuration || dustT < DustDuration ||
            stunT >= 0f || LegThrowHidesCarry;

        /// <summary>Hands busy (or about to be): the item goes back into the pocket with the normal motion.</summary>
        bool CarrySoftBlocked =>
            heldItem != HeldItem.None || toyOn || toyW > 0.01f || sitting || sitW > 0.01f || proneW > 0.3f || rcW > 0.01f ||
            injectW > 0.01f || healW > 0.01f || medW > 0.01f || patientW > 0.01f || reviving || reviveW > 0.01f ||
            leverW > 0.01f || arcadeOn || brickOn || gooW > 0.3f || remoteHeld;

        /// <summary>
        /// The weapon actually in the hand (HeldItem): lags behind <see cref="heldItem"/> so a switch first lowers the old
        /// one (until the hold weight is low), swaps and raises the new one; a carried item is put away before a weapon comes.
        /// </summary>
        HeldItem DrawnItem()
        {
            if (drawnItem == heldItem) return drawnItem;
            bool swap = drawnItem == HeldItem.None ? carryW < 0.05f : itemW < 0.15f;
            if (swap)
            {
                drawnItem = heldItem;
                BeginCrossfade();
            }
            return drawnItem;
        }

        /// <summary>True while the drawn weapon is being lowered to swap it.</summary>
        bool Holstering(HeldItem show) => show != HeldItem.None && show == drawnItem && drawnItem != heldItem;

        void ApplyCarry(ref CharacterPose p, float dt, float lookPitch)
        {
            bool hard = CarryHardBlocked;
            bool want = carryGrip != null && !hard && !CarrySoftBlocked;
            if (hard) carryW = 0f;

            // Swap at the bottom: the shown grip changes only once the old one is away.
            if (carryShownGrip != null && carryW <= 0.001f && (carryShownKey != carryKey || carryShownGrip != carryGrip || !want))
            {
                carryShownGrip = null;
                carryShownKey = 0;
            }
            if (carryShownGrip == null && want)
            {
                carryShownGrip = carryGrip;
                carryShownKey = carryKey;
            }
            bool up = want && carryShownGrip == carryGrip && carryShownKey == carryKey;
            carryW = Mathf.MoveTowards(carryW, up ? 1f : 0f, dt / (up ? CarryRaiseTime : CarryLowerTime));
            if (carryW <= 0.001f || carryShownGrip == null) return;

            var g = carryShownGrip;
            float w = ActionTimeline.SmoothStep01(carryW);
            var q = p;
            if (g.Hang)
            {
                // Hanging arm (keeps the walk swing), a little out from the leg; the item keeps its root-space angle.
                q.ShoulderR = Quaternion.Euler(0f, 0f, 7f) * p.ShoulderR;
                q.ElbowR = p.ElbowR * Quaternion.Euler(-6f, 0f, 0f);
                // Out of / into the pocket: the forearm comes up past the hip in a short arc.
                if (carryW < 1f) q.ElbowR = q.ElbowR * Quaternion.Euler(-40f * Mathf.Sin(Mathf.PI * carryW), 0f, 0f);
                q.PropR = Quaternion.Inverse(q.ForearmRoot(0)) * Quaternion.Euler(g.Rotation);
            }
            else
            {
                float look = Mathf.Clamp(lookPitch, -50f, 50f) * g.Look;
                Quaternion rl = Quaternion.Euler(-look, 0f, 0f);
                Vector3 pivot = new Vector3(0f, 0.5f, 0f);
                Vector3 hand = q.TorsoPoint(pivot + rl * (g.Hand - pivot));
                Quaternion rot = q.TorsoRoot * rl * Quaternion.Euler(g.Rotation);
                q.SolveArm(0, hand, TorsoDir(q, 1f, -1f, -0.4f));
                q.PropR = Quaternion.Inverse(q.ForearmRoot(0)) * rot;
                if (g.Left.HasValue)
                {
                    Vector3 left = q.HandPos(0) + rot * ((g.Left.Value - g.Grip) * g.Scale);
                    q.SolveArm(1, left, TorsoDir(q, -1f, -1f, -0.4f));
                }
            }

            CharacterPose.LerpArm(ref p, q, 0, w);
            if (g.Left.HasValue && !g.Hang) CharacterPose.LerpArm(ref p, q, 1, w);
            if (carryW > CarryShowFrom) p.PropR = q.PropR;
        }

        void UpdateCarryProp()
        {
            bool fp = character.IsFirstPerson;
            int layer = gameObject.layer;
            if (fp != carryFp || layer != carryLayer)
            {
                carryFp = fp;
                carryLayer = layer;
                foreach (var kv in carryModels) SetCarryRender(kv.Value.pivot, layer, fp);
            }

            Transform active = null;
            HandGrip g = carryShownGrip;
            if (g != null && carryW > CarryShowFrom)
            {
                if (!carryModels.TryGetValue(carryShownKey, out var m) || m.pivot == null || m.grip != g)
                {
                    if (m.pivot != null) Destroy(m.pivot.gameObject);
                    m = (g.Build(socketR, g.Grip, layer, g.Scale, fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On, true), g);
                    carryModels[carryShownKey] = m;
                }
                active = m.pivot;
                // Grows out of the fist instead of popping in at full size.
                float s = g.Scale * Mathf.Lerp(0.55f, 1f, ActionTimeline.SmoothStep01((carryW - CarryShowFrom) / 0.35f));
                active.localScale = Vector3.one * s;
                active.localPosition = -(g.Grip * s);
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

        static void SetCarryRender(Transform pivot, int layer, bool fp)
        {
            if (pivot == null) return;
            foreach (var t in pivot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }
    }
}
