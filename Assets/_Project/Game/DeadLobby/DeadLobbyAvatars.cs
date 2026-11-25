using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The other dead players in this peer's lobby copy: one blocky character per player whose
    /// <see cref="NetPlayer.LobbyGhost"/> is on (same skin, nick over the head, walks / turns / sits from the synced pose,
    /// smoothed between updates). Only peers in the lobby copy build them; the living never see a ghost.
    /// </summary>
    public sealed class DeadLobbyAvatars : MonoBehaviour
    {
        public static DeadLobbyAvatars Instance { get; private set; }

        private readonly Dictionary<ulong, DeadLobbyAvatar> _byId = new();
        private readonly List<ulong> _gone = new();

        public static void Create(DeadLobbyWorld world)
        {
            DestroyInstance();
            var go = new GameObject("DeadLobbyAvatars");
            SceneManager.MoveGameObjectToScene(go, world.Scene);
            Instance = go.AddComponent<DeadLobbyAvatars>();
        }

        public static void DestroyInstance()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        /// <summary>The ghost of player <paramref name="id"/> (network object id) reaches for a lobby prop.</summary>
        public static void Reach(ulong id, Vector3 target, ReachStyle style)
        {
            if (Instance != null && Instance._byId.TryGetValue(id, out var a) && a != null) a.Reach(target, style);
        }

        /// <summary>Avatar of a player (null when he has no ghost in this copy).</summary>
        public static DeadLobbyAvatar Of(NetPlayer p) => Instance != null && p != null && Instance._byId.TryGetValue(p.NetworkObjectId, out var a) ? a : null;

        private void Update()
        {
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.IsOwner || !p.IsSpawned) continue;
                bool on = p.IsObserver && p.LobbyGhost.Value.Mode != LobbyGhostState.Off && p.LobbyGhost.Value.Mode != LobbyGhostState.WalkingIn;
                _byId.TryGetValue(p.NetworkObjectId, out var a);
                if (on && a == null)
                {
                    a = DeadLobbyAvatar.Build(transform, p);
                    _byId[p.NetworkObjectId] = a;
                    Debug.Log($"[DeadLobby] ghost of {p.Nick} shown at {p.LobbyGhost.Value.Pos} (lobby-local)");
                }
                else if (!on && a != null)
                {
                    Destroy(a.gameObject);
                    _byId.Remove(p.NetworkObjectId);
                }
            }
            _gone.Clear();
            foreach (var kv in _byId) if (kv.Value == null || NetPlayer.Find(kv.Key) == null) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                if (_byId[id] != null) Destroy(_byId[id].gameObject);
                _byId.Remove(id);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
