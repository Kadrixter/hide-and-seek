using System;
using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Client side of the lobby mini interactions: the E prompt (<see cref="TryPrompt"/>, called by
    /// LocalPlayerController), the server's "used" broadcast (<see cref="OnUsed"/>: the user's arm reaches in third person,
    /// the local player's first-person hand follows the same curve) and the toggle state for the props' visuals.
    /// </summary>
    public sealed class LobbyPropDriver : MonoBehaviour
    {
        public static LobbyPropDriver Instance { get; private set; }

        /// <summary>Fired on every peer when somebody used a prop: kind, user (network object id), argument from the server.</summary>
        public static event Action<LobbyPropKind, ulong, int> Used;

        /// <summary>Current toggle bits (MatchManager.PropBits, 0 outside the lobby).</summary>
        public static int Bits => MatchManager.Instance != null ? MatchManager.Instance.PropBits.Value : 0;
        public static bool On(LobbyPropKind k) => LobbyPropBits.On(Bits, k);

        // first-person reach of the local player
        private float _fpT = 99f;
        private ReachStyle _fpStyle;
        private Vector3 _fpTarget;
        private bool _fpOn;

        public static void Ensure()
        {
            if (Instance != null) return;
            Instance = new GameObject("LobbyPropDriver").AddComponent<LobbyPropDriver>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>E prompt for the nearest usable prop in view.</summary>
        public static bool TryPrompt(NetPlayer me, Vector3 eye, Vector3 fwd, Func<Vector3, Transform, bool> inView,
            out int id, out Vector3 world, out string text, out float dist)
        {
            id = -1; world = default; text = null; dist = float.MaxValue;
            if (me == null) return false;
            int bits = Bits;
            foreach (var p in LobbyProp.All)
            {
                if (p == null) continue;
                Vector3 a = p.Aim;
                float d = Vector3.Distance(eye, a);
                if (d > p.range || d >= dist) continue;
                if (p.kind == LobbyPropKind.Darts && !LobbyDarts.CanThrowFrom(me.transform.position)) continue;
                if (!inView(a, p.transform)) continue;
                id = (int)p.kind;
                world = a + Vector3.up * 0.12f;
                text = p.PromptText(bits);
                dist = d;
            }
            return id >= 0;
        }

        /// <summary>From MatchManager.PropUsedRpc on every peer.</summary>
        public static void OnUsed(LobbyPropKind kind, ulong user, int arg)
        {
            var prop = LobbyProp.Of(kind);
            var np = NetPlayer.Find(user);
            if (prop != null && np != null)
            {
                Vector3 target = kind == LobbyPropKind.Darts ? LobbyDarts.BoardCentre : prop.Aim;
                (np.Actions as BlockyCharacterActions)?.PlayReach(target, prop.Style);
                if (np.IsOwner && Instance != null) Instance.StartFirstPerson(prop.Style, target);
            }
            try { Used?.Invoke(kind, user, arg); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void StartFirstPerson(ReachStyle style, Vector3 target)
        {
            _fpT = 0f;
            _fpStyle = style;
            _fpTarget = target;
        }

        private void LateUpdate()
        {
            var me = NetPlayer.Local;
            var vm = me != null ? me.ViewModel as FirstPersonViewModel : null;
            float dur = BlockyCharacterActions.ReachDuration(_fpStyle);
            if (vm == null || me.Camera == null || _fpT >= dur)
            {
                if (_fpOn && vm != null) vm.SetOverrideHands(false, Vector3.zero, Quaternion.identity);
                _fpOn = false;
                return;
            }
            _fpT += Time.deltaTime;
            // Same curve as the body, in camera space (shoulder below-right of the eye), hand shown while the weight is up.
            var cam = me.Camera.transform;
            Vector3 shoulder = new Vector3(0.2f, -0.3f, 0f);
            Vector3 target = cam.InverseTransformPoint(_fpTarget);
            BlockyCharacterActions.ReachCurve(_fpStyle, _fpT, target, shoulder, out var hand, out float w);
            Vector3 rest = new Vector3(0.28f, -0.45f, 0.55f);
            Vector3 local = Vector3.Lerp(rest, hand + new Vector3(0f, 0f, 0.1f), w);
            vm.SetOverrideHands(true, cam.TransformPoint(local), cam.rotation);
            _fpOn = true;
        }
    }
}
