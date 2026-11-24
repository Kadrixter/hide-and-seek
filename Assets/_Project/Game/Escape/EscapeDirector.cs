using HideAndSeek.Game.Cinematics;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game.Escape
{
    /// <summary>
    /// Runs the escape run every frame without hooks in the match code: created once at start-up (kept across
    /// scenes), it attaches <see cref="EscapeGateDriver"/> to the exit gate when the gate exists, ticks the server
    /// rules (<c>MatchManager.EscapeServerTick</c>), the minimap pin and every player's escaped state
    /// (<see cref="NetPlayer.EscapeTick"/>). Each part is guarded: an exception is logged (at most every 5 s) and
    /// never stops the others.
    /// </summary>
    public sealed class EscapeDirector : MonoBehaviour
    {
        static EscapeDirector _instance;
        readonly float[] _errAt = new float[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("EscapeDirector");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<EscapeDirector>();
        }

        MatchManager _mm;
        System.Action _server, _players, _gate, _pin;

        void Update()
        {
            _mm = MatchManager.Instance;
            if (_mm == null || !_mm.IsSpawned) return;
            Guard(0, "server", _server ??= () => _mm.EscapeServerTick());
            Guard(1, "players", _players ??= TickPlayers);
            Guard(2, "gate", _gate ??= TickGate);
            Guard(3, "pin", _pin ??= () => _mm.EscapeClientTick());
        }

        static void TickPlayers()
        {
            var all = NetPlayer.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].IsSpawned) all[i].EscapeTick();
        }

        void TickGate()
        {
            var gate = ExitGate.Instance;
            if (gate == null || _mm.RaceMode || SceneManager.GetActiveScene().name != NetSession.GameScene) return;
            EscapeGateDriver.Attach(gate).Tick(_mm);
        }

        void Guard(int k, string what, System.Action a)
        {
            try { a(); }
            catch (System.Exception e)
            {
                if (Time.unscaledTime - _errAt[k] < 5f && _errAt[k] > 0f) return;
                _errAt[k] = Time.unscaledTime;
                Debug.LogWarning($"[Escape] {what} tick failed: {e.GetType().Name}");
                Debug.LogException(e);
            }
        }
    }
}
