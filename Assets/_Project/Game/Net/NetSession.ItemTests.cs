using System;
using System.Collections;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Tests for the hotbar, the metal rod (peg leg) and the minimap item markers:
    ///  -testrod (host, -forcehider, + a seeker client): rods on the map, pickup, hotbar, leg cut off, fitting the rod
    ///      (first person shots), walking on it, minimap markers, a second rod on the other leg.
    ///  -rodwatch (any other instance): lit side camera films the hider fitting the rod and walking on it.
    ///  -itemshots &lt;folder&gt;: where both write their PNGs.
    /// </summary>
    public partial class NetSession
    {
        private void StartItemTests(Func<string, bool> has)
        {
            if (has("-testrod")) StartCoroutine(RodTest());
            if (has("-rodwatch")) { StartCoroutine(RodWatch()); StartCoroutine(SeekerHotbarTest()); }
            StartInvTests(has);
        }

        private static string ItemShots => ArgOr("-itemshots", "");

        private IEnumerator RodTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            var me = NetPlayer.Local;
            string dir = ItemShots;
            NetPlayer seeker = null;
            foreach (var p in NetPlayer.All) if (p != null && p.IsSeeker) seeker = p;
            if (!me.IsHider || seeker == null) { Debug.Log("[RodTest] need host hider (-forcehider) + seeker"); yield break; }
            Debug.Log($"[RodTest] rods on the map: {mm.Rods.Count}, candidates {RodSpots.Candidates().Count}");
            if (mm.Rods.Count == 0) mm.SpawnRods(3);
            Shot(dir, "hotbar_start");

            // Walk up to the first rod and look at it.
            var rod = mm.Rods[0];
            Vector3 away = rod.Base - rod.Top;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : Vector3.forward;
            Vector3 stand = rod.Base + away * 1.3f;
            Vector3 look = rod.Middle - (stand + Vector3.up * 1.6f);
            me.TeleportLocal(stand, Quaternion.LookRotation(new Vector3(look.x, 0f, look.z)).eulerAngles.y);
            me.Motor.SetLook(me.Motor.LookYaw, -Mathf.Atan2(-look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(1.2f);
            var lpc = LocalPlayerController.Instance;
            Debug.Log($"[RodTest] prompt at the rod: {lpc.Prompt} '{lpc.PromptText}'");
            Shot(dir, "rod_prompt");
            ItemLitShot(dir, "rod_on_map", rod.Middle + away * 1.6f + Vector3.Cross(Vector3.up, away) * 0.9f + Vector3.up * 0.5f, rod.Middle);
            me.RequestPickupRodRpc(rod.Id);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[RodTest] picked up: rods={me.RodCount.Value} taken={mm.Rods[0].Taken}");

            // Hotbar with items; LMB on the rod with both legs = hint only.
            me.MedkitCount.Value = 2;
            me.SyringeCount.Value = 1;
            yield return new WaitForSeconds(0.3f); // slot layout follows the counts (server)
            Debug.Log($"[RodTest] slots {me.Inv.Value}");
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Rod);
            yield return null;
            Hotbar.TestClick = true;
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[RodTest] click with both legs: rods={me.RodCount.Value} legs={me.RodLegs.Value}");
            Shot(dir, "hotbar_items");
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Medkit);
            yield return new WaitForSeconds(0.3f);
            Shot(dir, "hotbar_medkit");

            // Medkit from the hotbar: hold LMB (same path as G).
            me.Limping.Value = true;
            yield return new WaitForSeconds(0.5f);
            Hotbar.TestHold = true;
            yield return new WaitForSeconds(Tuning.HealHoldTime + 0.8f);
            Hotbar.TestHold = false;
            Debug.Log($"[RodTest] medkit via LMB: limping={me.Limping.Value} medkits={me.MedkitCount.Value}");

            // Leg cut off -> hopping (in an open spot, so the other instance can film it from the side).
            if (OpenSpot(out var open)) { me.TeleportLocal(open, 0f); yield return new WaitForSeconds(0.8f); }
            seeker.HasChainsaw.Value = true;
            seeker.Held.Value = HeldItem.Chainsaw;
            seeker.AttackReadyAt.Value = 0;
            seeker.TeleportRpc(me.transform.position + me.transform.forward * 1.1f, me.transform.eulerAngles.y + 180f);
            yield return new WaitForSeconds(0.5f);
            mm.ServerChainsaw(seeker, me);
            yield return new WaitForSeconds(2.5f);
            seeker.TeleportRpc(me.transform.position + me.transform.forward * 6f + Vector3.up * 0.1f, 0f);
            Debug.Log($"[RodTest] after saw: severed={me.Severed.Value} free={me.FreeStump} scale={me.Motor.SpeedScale:0.00}");
            yield return new WaitForSeconds(1.5f);

            // Fit the rod from the hotbar (its slot + LMB), first-person shots along the way.
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Rod);
            yield return null;
            Shot(dir, "hotbar_stump");
            Hotbar.TestClick = true;
            float t0 = Time.time;
            float[] at = { 0.7f, 1.25f, 1.55f, 2.2f, 2.6f, 3.2f };
            foreach (float a in at)
            {
                while (Time.time - t0 < a) yield return null;
                Shot(dir, $"fp_attach_{a:0.00}");
            }
            yield return new WaitForSeconds(1.2f);
            var drv = me.GetComponent<RodLegDriver>();
            Debug.Log($"[RodTest] fitted: legs={me.RodLegs.Value} rods={me.RodCount.Value} walking={drv.RodWalking} scale={me.Motor.SpeedScale:0.00} (rod {ItemTuning.RodSpeedScale}, hop {Tuning.HopSpeedScale})");

            // Walk on the rod (moved here, the other instance films the proxy).
            yield return WalkFree(me, 5f, me.Motor.WalkSpeed * ItemTuning.RodSpeedScale);
            Debug.Log($"[RodTest] walked; steps={RodSounds.Steps} scrapes={RodSounds.Scrapes}");

            // Minimap: stand ~20 m from a medkit on the same level, widest zoom.
            if (mm.Medkits.Count > 0)
            {
                var m = mm.Medkits[0];
                me.TeleportLocal(m.Position + new Vector3(12f, 0.05f, 9f), 0f);
                yield return new WaitForSeconds(1.5f);
                Shot(dir, "minimap_items");
            }

            // Second leg: a second rod (both legs on rods).
            me.RodCount.Value = 1;
            seeker.TeleportRpc(me.transform.position + me.transform.forward * 1.1f, me.transform.eulerAngles.y + 180f);
            me.Severed.Value = (byte)(me.Severed.Value | Limb.Legs);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[RodTest] both legs cut: free={me.FreeStump} walking={drv.RodWalking}");
            yield return new WaitForSeconds(0.3f);
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Rod);
            yield return null;
            Hotbar.TestClick = true;
            yield return new WaitForSeconds(RodTimeline.Duration + 1f);
            Debug.Log($"[RodTest] second rod: legs={me.RodLegs.Value} walking={drv.RodWalking} scale={me.Motor.SpeedScale:0.00}");
            yield return WalkFree(me, 3f, me.Motor.WalkSpeed * ItemTuning.RodSpeedScale);
            Debug.Log("[RodTest] done");
        }

        /// <summary>A hider spawn point with at least 3.5 m of free floor all round.</summary>
        private static bool OpenSpot(out Vector3 pos)
        {
            pos = default;
            foreach (var sp in SpawnPoints.Hider())
            {
                bool ok = true;
                for (int i = 0; i < 8 && ok; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                    if (Physics.SphereCast(sp.position + Vector3.up * 0.9f, 0.3f, d, out _, 3.5f, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore)) ok = false;
                }
                if (ok) { pos = sp.position + Vector3.up * 0.05f; return true; }
            }
            return false;
        }

        /// <summary>Moves the local player along the freest horizontal direction (no input needed).</summary>
        private static IEnumerator WalkFree(NetPlayer me, float seconds, float speed)
        {
            Vector3 best = me.transform.forward;
            float bestD = 0f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                float free = Physics.SphereCast(me.transform.position + Vector3.up * 0.9f, 0.35f, d, out var h, 12f, ~(1 << PlayerAvatarBinder.LocalBodyLayer), QueryTriggerInteraction.Ignore) ? h.distance : 12f;
                if (free > bestD) { bestD = free; best = d; }
            }
            me.TeleportLocal(me.transform.position, Quaternion.LookRotation(best).eulerAngles.y);
            yield return new WaitForSeconds(0.6f);
            var cc = me.GetComponent<CharacterController>();
            float end = Time.time + Mathf.Min(seconds, (bestD - 1f) / Mathf.Max(0.5f, speed));
            while (Time.time < end)
            {
                if (cc != null && cc.enabled) cc.Move(best * speed * Time.deltaTime);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Seeker side of the hotbar: knife in slot 1, an empty slot = empty hands, the saw's slot takes the saw.</summary>
        private IEnumerator SeekerHotbarTest()
        {
            yield return WaitSeeking();
            var me = NetPlayer.Local;
            if (!me.IsSeeker) yield break;
            yield return new WaitForSeconds(0.5f);
            Shot(ItemShots, "seeker_hotbar_knife");
            Hotbar.TestSelect = 1;
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[SeekerHotbar] empty slot 2: held={me.Held.Value} slots={me.Inv.Value}");
            float end = Time.time + 40f;
            while (!me.HasChainsaw.Value && Time.time < end) yield return null;
            yield return new WaitForSeconds(6f);
            Hotbar.TestSelect = 0;
            yield return new WaitForSeconds(1f);
            Hotbar.TestSelect = Hotbar.SlotOf(me, HotbarItem.Chainsaw);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[SeekerHotbar] saw slot: held={me.Held.Value} selected={Hotbar.Selected(me) + 1} slots={me.Inv.Value}");
            Shot(ItemShots, "seeker_hotbar_saw");
        }

        /// <summary>Other instance: lit side views of the hider fitting the rod and walking on it.</summary>
        private IEnumerator RodWatch()
        {
            yield return WaitSeeking();
            string dir = ItemShots;
            int n = 0, walk = 0;
            Vector3 lastPos = Vector3.zero;
            while (n < 90)
            {
                yield return new WaitForSeconds(0.12f);
                NetPlayer h = null;
                foreach (var p in NetPlayer.All) if (p != null && p != NetPlayer.Local && p.IsHider) h = p;
                if (h == null) continue;
                var bca = h.Actions as BlockyCharacterActions;
                var drv = h.GetComponent<RodLegDriver>();
                Vector3 pos = h.transform.position;
                bool moving = (pos - lastPos).magnitude > 0.1f;
                lastPos = pos;
                if (bca == null) continue;
                // Camera on the side of the cut-off leg (the rod).
                int stumpLeg = bca.RodAttaching ? bca.RodAttachLegIndex : (h.RodLegs.Value & Limb.LegL) != 0 && (h.RodLegs.Value & Limb.LegR) == 0 ? 1 : 0;
                Vector3 side = h.transform.right * (stumpLeg == 0 ? 1f : -1f);
                if (bca.RodAttaching)
                {
                    Vector3 at = pos + Vector3.up * 0.4f;
                    ItemLitShot(dir, $"tp_attach_{n++:000}_{bca.RodAttachTime:0.00}", FreeCam(at, side * 2.4f + h.transform.forward * 1.5f + Vector3.up * 0.85f), at);
                }
                else if (drv != null && drv.RodWalking && moving && walk < 30)
                {
                    walk++;
                    Vector3 at = pos + Vector3.up * 0.75f;
                    ItemLitShot(dir, $"tp_walk_{n++:000}", FreeCam(at, side * 3.2f + Vector3.up * 0.1f), at);
                }
            }
            Debug.Log($"[RodWatch] done, {n} shots, steps heard={RodSounds.Steps} scrapes={RodSounds.Scrapes}");
        }

        /// <summary>Camera spot <paramref name="offset"/> away from <paramref name="at"/>, mirrored / pulled in when a wall is in the way.</summary>
        private static Vector3 FreeCam(Vector3 at, Vector3 offset)
        {
            float best = -1f;
            Vector3 pick = at + offset;
            foreach (var o in new[] { offset, new Vector3(-offset.x, offset.y, -offset.z) })
            {
                float d = o.magnitude;
                float free = Physics.SphereCast(at, 0.2f, o / d, out var hit, d, ~((1 << 9) | (1 << PlayerAvatarBinder.LocalBodyLayer)), QueryTriggerInteraction.Ignore) ? hit.distance - 0.15f : d;
                if (free > best) { best = free; pick = at + o / d * Mathf.Max(0.8f, free); }
            }
            return pick;
        }

        /// <summary>Temporary camera with its own key and fill light (the garage is dark) rendered into a PNG.</summary>
        private static void ItemLitShot(string dir, string name, Vector3 pos, Vector3 at)
        {
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.Directory.CreateDirectory(dir);
            var go = new GameObject("TestLitShot");
            go.transform.position = pos;
            go.transform.LookAt(at);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.03f;
            cam.cullingMask = ~(1 << 9);
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(go.transform, false);
            key.transform.localPosition = new Vector3(0.6f, 0.5f, 0f);
            key.type = LightType.Spot;
            key.spotAngle = 70f;
            key.range = 10f;
            key.intensity = 2.4f;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.position = at + Vector3.up * 1.8f;
            fill.type = LightType.Point;
            fill.range = 5f;
            fill.intensity = 1.1f;
            var rt = new RenderTexture(960, 540, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(fill.gameObject);
            UnityEngine.Object.Destroy(go);
        }
    }
}
