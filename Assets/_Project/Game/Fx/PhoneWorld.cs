using System.Collections;
using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Client side of the toy phones (every instance): floor views (<see cref="PhoneView"/>) and the ringtone. A ringing
    /// phone on the floor plays the song in 3D from itself (and jumps); a ringing phone in a pocket plays it from the
    /// hider (it gives them away). The song starts at the right offset when you come in late (server ring time) and
    /// moves with the phone when somebody picks a ringing one up. Smashing stops it at once; the wreck then stays on the
    /// floor until the round ends (<see cref="PhoneModel.CreateWreck"/>, seeded by the phone id).
    /// </summary>
    public class PhoneWorld : MonoBehaviour
    {
        public static PhoneWorld Instance { get; private set; }

        private readonly Dictionary<int, PhoneView> _views = new();
        private readonly Dictionary<int, AudioSource> _ring = new();
        private readonly Dictionary<int, Transform> _ringAnchor = new();
        private readonly Dictionary<int, double> _silenced = new();   // id -> RingAt stopped locally (E pressed)
        private readonly HashSet<int> _hidden = new();                // floor views taken over by a stomp animation
        private readonly Dictionary<int, GameObject> _wrecks = new();   // smashed phones, until the round ends
        private readonly Dictionary<int, float> _wreckAt = new();       // id -> Time.time the sole lands (smash RPC)
        private readonly Dictionary<int, float> _smashSeen = new();     // id -> first seen smashed without an RPC
        private readonly List<int> _tmp = new();

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[PhoneWorld]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<PhoneWorld>();
        }

        /// <summary>True while the ringtone of phone <paramref name="id"/> is audible here (tests / HUD).</summary>
        public bool RingPlaying(int id) => _ring.TryGetValue(id, out var s) && s != null && s.isPlaying && s.clip == PhoneTuning.RingClip;
        public AudioSource RingSource(int id) => _ring.TryGetValue(id, out var s) ? s : null;
        public PhoneView View(int id) => _views.TryGetValue(id, out var v) ? v : null;

        /// <summary>Stops the ringtone of the phone carried by <paramref name="holder"/> right now (before the server answers).</summary>
        public void SilenceLocal(NetPlayer holder)
        {
            var mm = MatchManager.Instance;
            int i = mm != null ? mm.PhoneOf(holder) : -1;
            if (i < 0) return;
            var s = mm.Phones[i];
            _silenced[s.Id] = s.RingAt;
            StopRing(s.Id);
        }

        /// <summary>A smash animation starts on <paramref name="by"/>: the floor view goes (the animation shows the phone).</summary>
        public void OnSmash(NetPlayer by, int id, Vector3 land, bool throwFirst)
        {
            _hidden.Add(id);
            _wreckAt[id] = Time.time + PhoneTimeline.StompHit - (throwFirst ? 0f : PhoneTimeline.SeekerStart);
            if (_views.TryGetValue(id, out var v) && v != null) v.gameObject.SetActive(false);
            StopRing(id);
            StartCoroutine(SmashSounds(by, land, throwFirst));
        }

        private IEnumerator SmashSounds(NetPlayer by, Vector3 land, bool throwFirst)
        {
            float t = throwFirst ? 0f : PhoneTimeline.SeekerStart;
            IEnumerator Until(float at)
            {
                while (t < at) { t += Time.deltaTime; yield return null; }
            }
            if (throwFirst)
            {
                yield return Until(PhoneTimeline.Release - 0.08f);
                if (by != null) GameAudio.PlayAttached(GameSfx.KnifeSwing, by.transform, 0.4f);
                yield return Until(PhoneTimeline.Land);
                PhoneSounds.PlayDrop(land);
            }
            yield return Until(PhoneTimeline.StompStart + 0.15f);
            if (by != null) GameAudio.PlayAttached(GameSfx.KnifeSwing, by.transform, 0.3f);
            yield return Until(PhoneTimeline.StompHit);
            PhoneSounds.PlayCrush(land);
        }

        private void Update()
        {
            var mm = MatchManager.Instance;
            var ph = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            bool inGame = mm != null && mm.Phones != null && SceneManager.GetActiveScene().name == NetSession.GameScene &&
                          (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended);
            if (!inGame)
            {
                if (_views.Count > 0 || _ring.Count > 0 || _wrecks.Count > 0) Clear();
                return;
            }
            double now = mm.Now;
            _tmp.Clear();
            for (int i = 0; i < mm.Phones.Count; i++)
            {
                var s = mm.Phones[i];
                _tmp.Add(s.Id);
                bool floor = s.Mode == PhoneMode.Floor && !_hidden.Contains(s.Id);
                if (s.Mode != PhoneMode.Floor) _hidden.Remove(s.Id);
                if (floor)
                {
                    if (!_views.TryGetValue(s.Id, out var v) || v == null) _views[s.Id] = v = PhoneView.Create(s.Pos, s.Yaw);
                    if (!v.gameObject.activeSelf) v.gameObject.SetActive(true);
                    v.Place(s.Pos, s.Yaw);
                    v.Ringing = MatchManager.PhoneRinging(s, now);
                }
                else if (_views.TryGetValue(s.Id, out var gone))
                {
                    if (gone != null) Destroy(gone.gameObject);
                    _views.Remove(s.Id);
                }
                UpdateRing(s, now);
                if (s.Mode == PhoneMode.Smashed) UpdateWreck(s);
            }
            // Phones no longer in the list.
            List<int> drop = null;
            foreach (var kv in _views) if (!_tmp.Contains(kv.Key)) (drop ??= new List<int>()).Add(kv.Key);
            foreach (var kv in _ring) if (!_tmp.Contains(kv.Key)) (drop ??= new List<int>()).Add(kv.Key);
            foreach (var kv in _wrecks) if (!_tmp.Contains(kv.Key)) (drop ??= new List<int>()).Add(kv.Key);
            if (drop != null)
                foreach (int id in drop)
                {
                    if (_views.TryGetValue(id, out var v) && v != null) Destroy(v.gameObject);
                    _views.Remove(id);
                    StopRing(id);
                    if (_wrecks.TryGetValue(id, out var w) && w != null) Destroy(w);
                    _wrecks.Remove(id);
                }
        }

        /// <summary>
        /// The wreck appears when the sole lands (smash RPC clock), with the pieces flying out; a phone that was already
        /// smashed (no RPC seen here) gets the settled wreck after a short grace for the RPC to arrive.
        /// </summary>
        private void UpdateWreck(in PhoneState s)
        {
            if (_wrecks.ContainsKey(s.Id)) return;
            bool burst;
            if (_wreckAt.TryGetValue(s.Id, out float at))
            {
                if (Time.time < at) return;
                _wreckAt.Remove(s.Id);
                burst = true;
            }
            else
            {
                if (!_smashSeen.TryGetValue(s.Id, out float seen)) { _smashSeen[s.Id] = Time.time; return; }
                if (Time.time - seen < 0.5f) return;
                burst = false;
            }
            _wrecks[s.Id] = PhoneModel.CreateWreck(s.Pos, s.Yaw, s.Id, burst);
        }

        /// <summary>The wreck of phone <paramref name="id"/> (tests).</summary>
        public GameObject Wreck(int id) => _wrecks.TryGetValue(id, out var w) ? w : null;

        private void UpdateRing(in PhoneState s, double now)
        {
            bool want = MatchManager.PhoneRinging(s, now) && !(_silenced.TryGetValue(s.Id, out double sil) && sil == s.RingAt);
            Transform anchor = null;
            if (want)
            {
                if (s.Mode == PhoneMode.Floor && _views.TryGetValue(s.Id, out var v) && v != null && v.gameObject.activeSelf) anchor = v.transform;
                else if (s.Mode == PhoneMode.Carried) anchor = Speaker(NetPlayer.Find(s.Holder));
                want = anchor != null;
            }
            if (!want) { StopRing(s.Id); return; }
            _ring.TryGetValue(s.Id, out var src);
            _ringAnchor.TryGetValue(s.Id, out var was);
            bool mine = src != null && src.isPlaying && src.clip == PhoneTuning.RingClip;
            if (mine && was == anchor) return;
            if (mine) StopRing(s.Id);
            float at = (float)(now - s.RingAt);
            var clip = PhoneTuning.RingClip;
            if (clip == null || at >= clip.length - 0.1f) return;
            src = GameAudio.PlayClipAttached(clip, anchor, PhoneTuning.RingVolume, PhoneTuning.RingRange, GameSfx.None, 0.5f);
            if (src == null) return;
            if (at > 0.05f) src.time = at;
            _ring[s.Id] = src;
            _ringAnchor[s.Id] = anchor;
        }

        private void StopRing(int id)
        {
            if (_ring.TryGetValue(id, out var src) && src != null && src.isPlaying && src.clip == PhoneTuning.RingClip)
                GameAudio.StopLoop(src, 0.03f);
            _ring.Remove(id);
            _ringAnchor.Remove(id);
        }

        /// <summary>Point on the hider where a pocket phone rings from (right hip).</summary>
        private static Transform Speaker(NetPlayer p)
        {
            if (p == null) return null;
            var t = p.transform.Find("PhoneSpeaker");
            if (t != null) return t;
            t = new GameObject("PhoneSpeaker").transform;
            t.SetParent(p.transform, false);
            t.localPosition = new Vector3(0.2f, 0.95f, 0.05f);
            return t;
        }

        private void Clear()
        {
            foreach (var v in _views.Values) if (v != null) Destroy(v.gameObject);
            _views.Clear();
            foreach (int id in new List<int>(_ring.Keys)) StopRing(id);
            _hidden.Clear();
            _silenced.Clear();
            foreach (var w in _wrecks.Values) if (w != null) Destroy(w);
            _wrecks.Clear();
            _wreckAt.Clear();
            _smashSeen.Clear();
        }
    }
}
