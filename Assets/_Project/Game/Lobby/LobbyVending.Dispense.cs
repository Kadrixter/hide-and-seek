using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The machine side of a purchase (same on every client, driven by the server clock): key press, slot number on
    /// the display, the slot's spiral turns once, the front can slides over the shelf edge, tips, tumbles down the
    /// drop column, bounces in the bin and rolls out through the flap into the tray; the next can moves up.
    /// </summary>
    public sealed partial class LobbyVending
    {
        const float SpiralStart = 0.6f, SpiralEnd = 1.3f, TipStart = 0.93f, FallStart = 1.05f;
        const float Gravity = 9.81f, FallV0 = 0.3f, TipAngle = 70f;

        static float Lin(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

        /// <summary>Where the can is when it leaves the shelf (centre, local) and its tilt.</summary>
        static Vector3 TipCentre(int r, int c, float angle)
        {
            Vector3 front = VendingMachineModel.SlotFront(r, c);
            float shelfTop = VendingMachineModel.RowY(r) + 0.012f;
            Vector2 pivot = new Vector2(VendingMachineModel.ShelfFrontX, shelfTop);
            Vector2 d = new Vector2(front.x - 0.066f - pivot.x, front.y - shelfTop);
            float a = angle * Mathf.Deg2Rad;
            Vector2 rot = new Vector2(d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a), d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a));
            return new Vector3(pivot.x + rot.x, pivot.y + rot.y, front.z);
        }

        static float FallTime(int r)
        {
            float h = TipCentre(r, 0, TipAngle).y - VendingMachineModel.BinY;
            return (-FallV0 + Mathf.Sqrt(FallV0 * FallV0 + 2f * Gravity * Mathf.Max(0.01f, h))) / Gravity;
        }

        /// <summary>Server-clock time the can hits the bin floor (depends on the shelf row).</summary>
        static float ImpactTime(int r) => FallStart + FallTime(r);

        /// <summary>Animates the bought can and the slot; returns how far the flap is open (0..1).</summary>
        float Dispense(SodaCan can, NetPlayer p, in VendingState s, float t)
        {
            int r = s.Slot / VendingMachineModel.Cols, c = s.Slot % VendingMachineModel.Cols;
            Vector3 front = VendingMachineModel.SlotFront(r, c);
            Quaternion logoOut = Quaternion.Euler(0f, 90f, 0f);

            // Keypad and display.
            var key = model.Keys[VendingMachineModel.SlotKey(r, c)];
            float press = VendingTimeline.S(t, 0.38f, 0.45f) * (1f - VendingTimeline.S(t, 0.5f, 0.62f));
            key.localPosition = new Vector3(-0.411f + 0.008f * press, key.localPosition.y, key.localPosition.z);
            model.SetDisplay(t < 0.35f || t > 2.4f ? "2.50" : (t < 1.3f || Mathf.Repeat(t, 0.4f) < 0.25f ? "  " + (r + 1) + (c + 1) : "    "));

            // Slot: one spiral turn; the rear can moves up into the front place, the front can is the one we animate.
            float turn = Lin(t, SpiralStart, SpiralEnd);
            bool turning = t >= SpiralStart && t < SpiralEnd;
            model.Spirals[r, c].localRotation = Quaternion.Euler(-360f * turn * (turning ? 1f : 0f), 0f, 0f);
            model.Front[r, c].gameObject.SetActive(!turning);
            model.Rear[r, c].transform.localPosition = front + Vector3.right * VendingMachineModel.Pitch * (1f - (turning ? turn : 0f));

            // The bought can (a separate object, same brand).
            bool shown = t >= SpiralStart && t < VendingTimeline.Grip;
            can.gameObject.SetActive(shown);
            can.Scale = VendingMachineModel.CanScale;
            can.SetCrush(0f);
            can.SetOpen(0f);
            if (!shown) return 0f;

            float impact = ImpactTime(r);
            Vector3 pos;
            Quaternion rot;
            if (t < TipStart)
            {
                pos = front + Vector3.left * VendingMachineModel.Pitch * Lin(t, SpiralStart, SpiralEnd);
                rot = logoOut;
            }
            else if (t < FallStart)
            {
                float k = Lin(t, TipStart, FallStart);
                float ang = TipAngle * k * k;
                pos = TipCentre(r, c, ang);
                rot = Quaternion.Euler(0f, 0f, ang) * logoOut;
            }
            else if (t < impact)
            {
                // Free fall down the drop column with a tumble; drifts back from the glass towards the middle.
                float dt = t - FallStart, k = Lin(t, FallStart, impact);
                Vector3 start = TipCentre(r, c, TipAngle);
                pos = new Vector3(Mathf.Lerp(start.x, VendingMachineModel.DropX, k),
                    start.y - FallV0 * dt - 0.5f * Gravity * dt * dt, start.z);
                float ang = Mathf.Lerp(TipAngle, 270f, k);
                rot = Quaternion.Euler(8f * Mathf.Sin(k * 9f), 14f * k, ang) * logoOut;
            }
            else
            {
                // Bounce, settle, then roll forward through the flap into the tray, turning across the tray.
                Vector3 bin = new Vector3(VendingMachineModel.DropX, VendingMachineModel.BinY, front.z);
                Quaternion lying = Quaternion.Euler(0f, 14f, 270f) * logoOut;
                Quaternion inTray = Quaternion.Inverse(transform.rotation) * p.transform.rotation * VendingTimeline.GripRotation * logoOut;
                float bounce = Mathf.Sin(Mathf.PI * Lin(t, impact, impact + 0.16f)) * (t < impact + 0.16f ? 0.03f : 0f);
                float roll = VendingTimeline.S(t, impact + 0.25f, impact + 0.7f);
                Vector3 chute = VendingMachineModel.ChuteLocal, tray = VendingMachineModel.TrayCanLocal;
                pos = roll < 0.45f ? Vector3.Lerp(bin, chute, roll / 0.45f) : Vector3.Lerp(chute, tray, (roll - 0.45f) / 0.55f);
                pos.y += bounce;
                rot = Quaternion.Slerp(lying, inTray, roll) * Quaternion.Euler(0f, 0f, 0f);
            }
            can.transform.localPosition = pos;
            can.transform.localRotation = rot;

            // The flap swings out while the can passes it.
            float pass = Lin(t, impact + 0.25f, impact + 0.7f);
            return VendingTimeline.S(pass, 0.35f, 0.55f) * (1f - VendingTimeline.S(pass, 0.8f, 1f));
        }

        /// <summary>Puts every slot back to its resting look (both cans in place, spirals still, keys out).</summary>
        void ResetSlots()
        {
            for (int r = 0; r < VendingMachineModel.Rows; r++)
            for (int c = 0; c < VendingMachineModel.Cols; c++)
            {
                if (!model.Front[r, c].gameObject.activeSelf) model.Front[r, c].gameObject.SetActive(true);
                model.Rear[r, c].transform.localPosition = VendingMachineModel.SlotFront(r, c) + Vector3.right * VendingMachineModel.Pitch;
                model.Spirals[r, c].localRotation = Quaternion.identity;
            }
            foreach (var k in model.Keys) k.localPosition = new Vector3(-0.411f, k.localPosition.y, k.localPosition.z);
        }
    }
}
