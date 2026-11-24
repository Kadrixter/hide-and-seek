using System.Collections;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Client side of the smashed RC transmitters (every instance): the swish and the crash of the throw, and the wreck
    /// on the floor (<see cref="RcRemoteModel.CreateWreck"/>, seeded by the car slot and the landing spot, so it is the
    /// same for everybody) until the round ends. Seen live: the wreck bursts out when the transmitter hits the floor
    /// (local clock of the throw); coming in late: it is just lying there.
    /// </summary>
    public class RcRemoteFx : MonoBehaviour
    {
        public static RcRemoteFx Instance { get; private set; }

        private readonly GameObject[] _wrecks = new GameObject[RcCar.Count];
        private readonly float[] _landAt = { -1f, -1f, -1f, -1f };
        private readonly float[] _seen = { -1f, -1f, -1f, -1f };

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[RcRemoteFx]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RcRemoteFx>();
        }

        /// <summary>The wreck of the transmitter of car <paramref name="slot"/> (tests), null = none.</summary>
        public GameObject Wreck(int slot) => slot >= 0 && slot < RcCar.Count ? _wrecks[slot] : null;

        /// <summary>A smash starts on <paramref name="by"/>: sounds on the throw clock, the wreck appears at the impact.</summary>
        public void OnThrow(NetPlayer by, int slot, Vector3 land)
        {
            if (slot < 0 || slot >= RcCar.Count) return;
            _landAt[slot] = Time.time + RcRemoteTimeline.Land;
            if (_wrecks[slot] != null) Destroy(_wrecks[slot]);
            _wrecks[slot] = null;
            StartCoroutine(ThrowSounds(by, land));
        }

        private IEnumerator ThrowSounds(NetPlayer by, Vector3 land)
        {
            float t = 0f;
            IEnumerator Until(float at)
            {
                while (t < at) { t += Time.deltaTime; yield return null; }
            }
            yield return Until(0.1f);
            if (by != null) GameAudio.PlayAttached(GameSfx.RcPlace, by.transform, 0.35f); // grabbed hard: plastic creak
            yield return Until(RcRemoteTimeline.ThrowStart - 0.02f);
            if (by != null) GameAudio.PlayAttached(GameSfx.KnifeSwing, by.transform, 0.5f);
            yield return Until(RcRemoteTimeline.Land);
            RcRemoteSounds.PlayCrash(land);
        }

        private void Update()
        {
            var mm = MatchManager.Instance;
            var ph = mm != null ? mm.Phase.Value : MatchPhase.Lobby;
            bool inGame = mm != null && SceneManager.GetActiveScene().name == NetSession.GameScene &&
                          (ph == MatchPhase.Hiding || ph == MatchPhase.Seeking || ph == MatchPhase.Ended);
            for (int i = 0; i < RcCar.Count; i++)
            {
                bool want = false;
                RcSlotState s = default;
                if (inGame)
                {
                    s = mm.RcSlot(i);
                    want = s.Mode is RcMode.Wreck or RcMode.Shot && s.Remote == RcRemoteFate.Thrown;
                }
                if (!want)
                {
                    if (_wrecks[i] != null) Destroy(_wrecks[i]);
                    _wrecks[i] = null;
                    _seen[i] = -1f;
                    if (!inGame) _landAt[i] = -1f;
                    continue;
                }
                if (_wrecks[i] != null) continue;
                bool burst;
                if (_landAt[i] >= 0f)
                {
                    if (Time.time < _landAt[i]) continue;
                    _landAt[i] = -1f;
                    burst = true;
                }
                else
                {
                    // Already smashed when we got here (late join): after a short grace for the throw to arrive.
                    if (_seen[i] < 0f) { _seen[i] = Time.time; continue; }
                    if (Time.time - _seen[i] < 0.3f) continue;
                    burst = false;
                }
                _wrecks[i] = RcRemoteModel.CreateWreck(s.RemotePos, s.RemoteYaw, Seed(i, s.RemotePos), burst);
            }
        }

        /// <summary>Same seed on every peer: the slot and the landing spot (server-written, identical everywhere).</summary>
        private static int Seed(int slot, Vector3 pos) =>
            slot * 7919 + Mathf.RoundToInt(pos.x * 100f) * 31 + Mathf.RoundToInt(pos.z * 100f) * 17 + Mathf.RoundToInt(pos.y * 100f);
    }
}
