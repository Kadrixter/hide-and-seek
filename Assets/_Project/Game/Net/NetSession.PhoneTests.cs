using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Voice;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Toy phone tests. -testphone on every copy (all with -voicetest: a tone instead of the microphone, always
    /// talking), host -forcehider -autostart -hidetime N, 3 players (host hider, a hider and the seeker):
    /// count + spots, pickup (prompt, HUD), phone voice between levels (frames per channel), ringing in the pocket
    /// (sound everywhere, "E" badge), E = throw + stomp (first person shots on the host, third person from the other
    /// hider), a ringing phone on the floor (jumps, sound), the seeker stomps on it (first person shots, third person
    /// from the host), the dead drop their phone. -phoneshots &lt;dir&gt; = PNGs. -phonecounts logs the count table.
    /// Timing is shared through the server clock (stages at fixed seconds after the seeking phase starts).
    /// </summary>
    public partial class NetSession
    {
        private void StartPhoneTests(Func<string, bool> has)
        {
            if (has("-phonecounts")) LogPhoneCounts();
            if (has("-testphone")) StartCoroutine(PhoneTest());
            if (has("-phonelog")) StartCoroutine(PhoneLog());
            if (ArgOr("-phonewav", "") is string wav && wav.Length > 0) StartCoroutine(PhoneWavs(wav));
        }

        private static string PhoneShots => ArgOr("-phoneshots", "");

        private static void LogPhoneCounts()
        {
            var sb = new StringBuilder("[PhoneTest] count table (2000 draws):");
            for (int n = 2; n <= 8; n++)
            {
                var hist = new SortedDictionary<int, int>();
                for (int i = 0; i < 2000; i++)
                {
                    int c = MatchManager.PhoneCountFor(n);
                    hist[c] = hist.TryGetValue(c, out int k) ? k + 1 : 1;
                }
                sb.Append($"\n[PhoneTest]   {n} players ->");
                foreach (var kv in hist) sb.Append($" {kv.Key} phones x{kv.Value}");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>-phonelog: just logs the phones of the round (count tests with 2 / 4 players).</summary>
        private IEnumerator PhoneLog()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            int players = 0;
            foreach (var p in NetPlayer.All) if (p != null) players++;
            var sb = new StringBuilder($"[PhoneTest] players={players} phones={mm.Phones.Count}:");
            for (int i = 0; i < mm.Phones.Count; i++) sb.Append($" #{mm.Phones[i].Id} level {FloorAcoustics.FloorOf(mm.Phones[i].Pos.y)} {mm.Phones[i].Pos}");
            Debug.Log(sb.ToString());
        }

        /// <summary>-phonewav &lt;dir&gt;: writes the synthesised crush / drop sounds and 4 s of the radio channel (WAV).</summary>
        private IEnumerator PhoneWavs(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            WriteWav(System.IO.Path.Combine(dir, "phone_crush.wav"), PhoneSounds.Crush);
            WriteWav(System.IO.Path.Combine(dir, "phone_drop.wav"), PhoneSounds.Drop);
            // Radio: wait for phone voice on this copy, then capture.
            while (VoiceChat.Instance == null || VoiceChat.Instance.ReceivedPhone < 50) yield return null;
            RadioFilter.TestCaptureN = 0;
            RadioFilter.TestCapture = new float[RadioFilter.CaptureRate * 4];
            while (RadioFilter.TestCaptureN < RadioFilter.TestCapture.Length) yield return null;
            var cap = RadioFilter.TestCapture;
            RadioFilter.TestCapture = null;
            WriteWav(System.IO.Path.Combine(dir, "phone_radio.wav"), cap, RadioFilter.CaptureRate);
            Debug.Log($"[PhoneTest] wavs written to {dir}");
        }

        private static void WriteWav(string path, AudioClip clip)
        {
            var d = new float[clip.samples * clip.channels];
            clip.GetData(d, 0);
            WriteWav(path, d, clip.frequency);
        }

        private static void WriteWav(string path, float[] d, int rate)
        {
            using var w = new System.IO.BinaryWriter(System.IO.File.Create(path));
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + d.Length * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(d.Length * 2);
            foreach (float x in d) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(x * 32767f), -32768, 32767));
        }

        private double _phoneT0;

        private IEnumerator At(float sec)
        {
            while (MatchManager.Instance != null && MatchManager.Instance.Now < _phoneT0 + sec) yield return null;
        }

        private IEnumerator PhoneTest()
        {
            yield return WaitSeeking();
            var mm = MatchManager.Instance;
            _phoneT0 = mm.Now;
            var me = NetPlayer.Local;
            bool host = NetworkManager.Singleton.IsServer;
            string role = me.IsSeeker ? "seeker" : host ? "hostHider" : "hiderB";
            string dir = PhoneShots;
            NetPlayer hostP = NetPlayer.FindByClient(NetworkManager.ServerClientId), seeker = null, hiderB = null;
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                if (p.IsSeeker) seeker = p;
                else if (p != hostP) hiderB = p;
            }
            Debug.Log($"[PhoneTest] {me.Nick} is {role}; phones={mm.Phones.Count} players={NetPlayer.All.Count} ringLength={PhoneTuning.RingLength:0.00}s clip={(PhoneTuning.RingClip != null)}");
            for (int i = 0; i < mm.Phones.Count; i++)
                Debug.Log($"[PhoneTest]   phone {mm.Phones[i].Id} mode={mm.Phones[i].Mode} level={FloorAcoustics.FloorOf(mm.Phones[i].Pos.y)} pos={mm.Phones[i].Pos}");

            // ---- pickup: host takes phone 0, hider B phone 1.
            yield return At(1f);
            int myIdx = role == "hostHider" ? 0 : role == "hiderB" ? 1 : -1;
            if (myIdx >= 0 && myIdx < mm.Phones.Count)
            {
                var ph = mm.Phones[myIdx];
                StandAndLook(me, ph.Pos, 1.0f);
                yield return new WaitForSeconds(1f);
                var lpc = LocalPlayerController.Instance;
                Debug.Log($"[PhoneTest] {role} prompt at phone {ph.Id}: {lpc.Prompt} '{lpc.PromptText}'");
                if (role == "hostHider")
                {
                    Shot(dir, "pickup_prompt");
                    Vector3 side = Quaternion.Euler(0f, ph.Yaw + 50f, 0f) * Vector3.forward;
                    ItemLitShot(dir, "phone_on_floor", ph.Pos + side * 0.75f + Vector3.up * 0.45f, ph.Pos + Vector3.up * 0.04f);
                }
                me.RequestPickupPhoneRpc(ph.Id);
            }
            yield return At(5f);
            Debug.Log($"[PhoneTest] {role} hasPhone={me.HasPhone} links={VoiceRules.LocalPhoneLinks()} hud='{PhoneHud.LinkText}'");

            // ---- different levels, far apart: hider B goes to a hider spawn on another level than the host.
            if (role == "hiderB" && hostP != null)
            {
                int hostLv = FloorAcoustics.FloorOf(hostP.transform.position.y);
                Transform best = null;
                float bestD = 0f;
                foreach (var sp in SpawnPoints.Hider())
                {
                    float d = Vector3.Distance(sp.position, hostP.transform.position);
                    bool other = FloorAcoustics.FloorOf(sp.position.y) != hostLv;
                    float score = d + (other ? 1000f : 0f);
                    if (score > bestD) { bestD = score; best = sp; }
                }
                if (best != null) me.TeleportLocal(best.position + Vector3.up * 0.05f, best.eulerAngles.y);
            }
            if (role == "seeker" && hostP != null)
            {
                // Seeker away from both, but within normal voice range of nobody in particular.
                me.TeleportLocal(me.transform.position, me.transform.eulerAngles.y);
            }
            yield return At(7f);
            if (role == "hostHider") Shot(dir, "hud_link");
            PhoneVoiceSnapshot(role, "reset");
            yield return At(12f);
            PhoneVoiceSnapshot(role, "voice 5 s");

            // ---- ringing in the host's pocket.
            int hostPhone = -1;
            if (hostP != null) { int hi = mm.PhoneOf(hostP); if (hi >= 0) hostPhone = mm.Phones[hi].Id; }
            yield return At(13f);
            if (host && hostPhone >= 0) mm.RingPhone(hostPhone);
            yield return At(15f);
            LogRing(role, hostPhone, hostP);
            if (role == "hostHider")
            {
                Debug.Log($"[PhoneTest] badge shown={(PhoneHud.Instance != null && PhoneHud.Instance.Badge != null && PhoneHud.Instance.Badge.Shown)} offered={PhoneControls.SmashOffered(me)}");
                Shot(dir, "ring_badge");
            }

            // ---- E: throw and stomp (host), filmed in first person (host) and third person (hider B, who walks over
            // to the host first: the map renders only around the own level).
            yield return At(16f);
            if (role == "hiderB" && hostP != null)
                me.TeleportLocal(hostP.transform.position + hostP.transform.right * 2.6f + hostP.transform.forward * 1.2f, hostP.transform.eulerAngles.y - 90f);
            yield return At(17f);
            if (role == "hostHider") PhoneControls.TestE = true;
            yield return null;
            float[] marks = { 0.35f, 0.62f, 0.85f, 1.25f, 1.47f, 1.75f };
            float started = Time.time;
            foreach (float m in marks)
            {
                while (Time.time - started < m) yield return null;
                if (role == "hostHider")
                {
                    // First person on screen, then the own body in third person for one frame, filmed from the side.
                    Shot(dir, $"smash_fpp_{m:0.00}");
                    yield return null;
                    me.Character.SetFirstPersonMode(false);
                    yield return null;
                    Vector3 hp = me.transform.position;
                    ItemLitShot(dir, $"smash_tpp_self_{m:0.00}", hp + me.transform.right * 2.3f + me.transform.forward * 1.1f + Vector3.up * 1.3f,
                        hp + me.transform.forward * 0.35f + Vector3.up * 0.75f);
                    me.Character.SetFirstPersonMode(true);
                }
                else if (role == "hiderB" && hostP != null)
                {
                    Vector3 hp = hostP.transform.position;
                    ItemLitShot(dir, $"smash_tpp_{m:0.00}", hp + hostP.transform.right * 2.3f + hostP.transform.forward * 1.1f + Vector3.up * 1.3f,
                        hp + hostP.transform.forward * 0.35f + Vector3.up * 0.75f);
                }
            }
            yield return At(21f);
            LogRing(role, hostPhone, hostP);
            Debug.Log($"[PhoneTest] {role} after smash: host hasPhone={(hostP != null && hostP.HasPhone)} links={VoiceRules.LocalPhoneLinks()} hostBusy={(hostP != null && hostP.Busy.Value)}");

            // ---- a ringing phone on the floor next to the seeker; the seeker stomps on it.
            int floorId = -1;
            if (host && seeker != null)
            {
                Vector3 p = seeker.transform.position + seeker.transform.forward * 1.3f;
                if (Physics.Raycast(p + Vector3.up * 0.8f, Vector3.down, out var hit, 2f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) p = hit.point;
                floorId = mm.TestAddPhone(p + Vector3.up * 0.004f, seeker.transform.eulerAngles.y + 30f);
            }
            yield return At(22f);
            for (int i = 0; i < mm.Phones.Count; i++) if (mm.Phones[i].Id >= 390) floorId = mm.Phones[i].Id;
            if (host && floorId >= 0) mm.RingPhone(floorId);
            yield return At(24f);
            LogRing(role, floorId, null);
            PhoneState fs = default;
            for (int i = 0; i < mm.Phones.Count; i++) if (mm.Phones[i].Id == floorId) fs = mm.Phones[i];
            if (role == "hiderB" && floorId >= 0)
            {
                ItemLitShot(dir, "floor_ringing_a", fs.Pos + new Vector3(0.6f, 0.35f, 0.5f), fs.Pos + Vector3.up * 0.03f);
                yield return new WaitForSeconds(0.13f);
                ItemLitShot(dir, "floor_ringing_b", fs.Pos + new Vector3(0.6f, 0.35f, 0.5f), fs.Pos + Vector3.up * 0.03f);
                var v = PhoneWorld.Instance != null ? PhoneWorld.Instance.View(floorId) : null;
                Debug.Log($"[PhoneTest] floor phone view ringing={(v != null && v.Ringing)} pos={(v != null ? v.transform.position.ToString("F3") : "-")} state={fs.Pos.ToString("F3")}");
            }
            if (role == "seeker" && floorId >= 0)
            {
                StandAndLook(me, fs.Pos, 1.25f);
                yield return new WaitForSeconds(0.8f);
                var lpc = LocalPlayerController.Instance;
                Debug.Log($"[PhoneTest] seeker prompt: {lpc.Prompt} '{lpc.PromptText}'");
                Shot(dir, "stomp_prompt");
            }
            yield return At(26f);
            if (role == "seeker" && floorId >= 0) me.RequestPhoneStompRpc(floorId);
            float[] sm = { 0.3f, 0.55f, 0.65f, 0.9f };
            started = Time.time;
            foreach (float m in sm)
            {
                while (Time.time - started < m) yield return null;
                if (role == "seeker") Shot(dir, $"stomp_fpp_{m:0.00}");
                else if (role == "hostHider" && seeker != null)
                {
                    Vector3 sp = seeker.transform.position;
                    ItemLitShot(dir, $"stomp_tpp_{m:0.00}", sp + seeker.transform.right * 2.2f + seeker.transform.forward * 1.0f + Vector3.up * 1.2f,
                        sp + seeker.transform.forward * 0.4f + Vector3.up * 0.6f);
                }
            }
            yield return At(29f);
            for (int i = 0; i < mm.Phones.Count; i++) if (mm.Phones[i].Id == floorId) fs = mm.Phones[i];
            Debug.Log($"[PhoneTest] {role} floor phone after stomp: mode={fs.Mode} ringAt={fs.RingAt:0.0}");
            LogRing(role, floorId, null);

            // ---- wrecks stay, the same on every copy.
            foreach (int wid in new[] { hostPhone, floorId })
            {
                var wr = PhoneWorld.Instance != null ? PhoneWorld.Instance.Wreck(wid) : null;
                float sig = 0f;
                if (wr != null) foreach (Transform c in wr.transform) sig += c.localPosition.x * 3.1f + c.localPosition.z * 7.3f + c.localEulerAngles.y * 0.01f;
                Debug.Log($"[PhoneTest] {role} wreck of {wid}: {(wr != null ? $"pieces={wr.transform.childCount} at={wr.transform.position.ToString("F2")} signature={sig:0.0000} settling={(wr.GetComponent<PhoneWreckSettle>() != null)}" : "none")}");
                if (wr != null && role == "hostHider")
                    ItemLitShot(dir, $"wreck_{wid}", wr.transform.position + new Vector3(0.55f, 0.6f, -0.45f), wr.transform.position + Vector3.up * 0.02f);
            }

            // ---- the dead drop their phone; the dead never talk through it.
            if (host && hiderB != null) { mm.TestKill(hiderB); Debug.Log($"[PhoneTest] host killed {hiderB.Nick}"); }
            yield return At(31f);
            var sb = new StringBuilder($"[PhoneTest] {role} after the kill:");
            for (int i = 0; i < mm.Phones.Count; i++) sb.Append($" #{mm.Phones[i].Id}={mm.Phones[i].Mode}");
            Debug.Log(sb.ToString());
            Debug.Log($"[PhoneTest] {role} done");
        }

        private static void StandAndLook(NetPlayer me, Vector3 target, float dist)
        {
            Vector3 best = Vector3.forward;
            float bestFree = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                float free = Physics.SphereCast(target + Vector3.up * 0.9f, 0.3f, d, out var h, dist + 0.6f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore) ? h.distance : dist + 0.6f;
                if (free > bestFree) { bestFree = free; best = d; }
            }
            Vector3 stand = target + best * dist;
            stand.y = target.y + 0.05f;
            Vector3 eye = stand + Vector3.up * 1.62f;
            Vector3 to = target + Vector3.up * 0.05f - eye;
            me.TeleportLocal(stand, YawTo(to));
            me.Motor.SetLook(YawTo(to), PitchTo(to));
        }

        private static void LogRing(string role, int id, NetPlayer holder)
        {
            var pw = PhoneWorld.Instance;
            var src = pw != null ? pw.RingSource(id) : null;
            var cam = Camera.main;
            float dist = src != null && cam != null ? Vector3.Distance(src.transform.position, cam.transform.position) : -1f;
            Debug.Log($"[PhoneTest] {role} ring of phone {id}: playing={(pw != null && pw.RingPlaying(id))} time={(src != null ? src.time : -1f):0.00}s " +
                      $"vol={(src != null ? src.volume : -1f):0.00} dist={dist:0.0}m holderRinging={(holder != null && holder.PhoneRinging)} " +
                      $"listener={(HideAndSeek.Audio.GameAudio.Listener != null ? HideAndSeek.Audio.GameAudio.Listener.position.ToString("F1") : "-")} " +
                      $"src={(src != null ? src.transform.position.ToString("F1") : "-")} otherFloor={(src != null && HideAndSeek.Audio.GameAudio.Listener != null && FloorAcoustics.DifferentFloor(HideAndSeek.Audio.GameAudio.Listener.position, src.transform.position))}");
        }

        private readonly Dictionary<string, (int near, int phone)> _phonePkts = new();

        private void PhoneVoiceSnapshot(string role, string label)
        {
            var sb = new StringBuilder($"[PhoneTest] {role} {label}: links={VoiceRules.LocalPhoneLinks()} rxPhone={VoiceChat.Instance?.ReceivedPhone}");
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.IsOwner) continue;
                var vp = p.GetComponentInChildren<VoicePlayback>();
                var t = p.transform.Find("PhoneVoice");
                var pv = t != null ? t.GetComponent<PhoneVoice>() : null;
                int near = vp != null ? vp.Packets : 0, phone = pv != null ? pv.Packets : 0;
                _phonePkts.TryGetValue(p.Nick, out var before);
                _phonePkts[p.Nick] = (near, phone);
                if (label == "reset") continue;
                float dist = NetPlayer.Local != null ? Vector3.Distance(NetPlayer.Local.transform.position, p.transform.position) : -1f;
                sb.Append($"\n[PhoneTest]    from {p.Nick} ({(p.IsSeeker ? "seeker" : "hider")}, {dist:0}m, level {FloorAcoustics.FloorOf(p.transform.position.y)}): " +
                          $"near={near - before.near} phone={phone - before.phone} floorGain={(vp != null ? vp.FloorGain : -1f):0.00} " +
                          $"radioLevel={(pv != null ? pv.Radio.OutputLevel : 0f):0.000} radioOpen={(pv != null && pv.Radio.IsOpen)}");
            }
            if (label != "reset") Debug.Log(sb.ToString());
        }
    }
}
