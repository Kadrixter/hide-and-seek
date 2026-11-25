using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using HideAndSeek.Game.Voice;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>One dead player's ghost in the lobby copy; motion source of its character animator.</summary>
    public sealed class DeadLobbyAvatar : MonoBehaviour, ICharacterMotionSource
    {
        private NetPlayer _p;
        private BlockyCharacter _ch;
        private BlockyCharacterActions _act;
        private TextMesh _tag;
        private Vector3 _vel;
        private int _seat = -1;
        private bool _placed;

        public NetPlayer Player => _p;
        public Vector3 Head => transform.position + Vector3.up * (_seat >= 0 ? 1.1f : 1.6f);

        public static DeadLobbyAvatar Build(Transform parent, NetPlayer p)
        {
            var go = new GameObject("Ghost_" + p.Nick);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<DeadLobbyAvatar>();
            a._p = p;
            a._ch = BlockyCharacterFactory.Create(go.transform, p.SkinVar.Value, p.Nick);
            if (p.Character != null && p.Character.CustomSkin != null) a._ch.SetCustomSkin(p.Character.CustomSkin);
            a._act = a._ch.GetComponent<BlockyCharacterActions>();
            var tagGo = new GameObject("Nick");
            tagGo.transform.SetParent(go.transform, false);
            a._tag = tagGo.AddComponent<TextMesh>();
            a._tag.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            a._tag.GetComponent<MeshRenderer>().sharedMaterial = a._tag.font.material;
            a._tag.fontSize = 48;
            a._tag.characterSize = 0.022f;
            a._tag.anchor = TextAnchor.LowerCenter;
            a._tag.alignment = TextAlignment.Center;
            a._tag.richText = true;
            a.Apply(true);
            return a;
        }

        public void Reach(Vector3 target, ReachStyle style) => _act?.PlayReach(target, style);

        private void Update() => Apply(false);

        private void Apply(bool snap)
        {
            if (_p == null) return;
            var g = _p.LobbyGhost.Value;
            Vector3 target = DeadLobbyWorld.ToWorld(g.Pos);
            Quaternion rot = Quaternion.Euler(0f, g.Yaw, 0f);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (g.Seat != _seat) ChangeSeat(g.Seat);
            if (_seat >= 0)
            {
                var s = LobbySeat.Get(_seat);
                if (s != null) { target = s.transform.position; rot = s.transform.rotation; }
                _vel = Vector3.zero;
            }
            if (snap || !_placed || (target - transform.position).sqrMagnitude > 9f)
            {
                transform.SetPositionAndRotation(target, rot);
                _placed = true;
                _vel = Vector3.zero;
            }
            else
            {
                Vector3 next = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-dt * 12f));
                _vel = Vector3.Lerp(_vel, (next - transform.position) / dt, 1f - Mathf.Exp(-dt * 10f));
                transform.position = next;
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-dt * 14f));
            }
            _pitch = g.Pitch;
            if (_tag != null)
            {
                _tag.transform.position = transform.position + Vector3.up * (_seat >= 0 ? 1.45f : 2.12f);
                var cam = Camera.main;
                if (cam != null) _tag.transform.rotation = Quaternion.LookRotation(_tag.transform.position - cam.transform.position);
                string mic = VoiceChat.IsSpeaking(_p) ? "<color=#7dff8a>((</color> " : "";
                string watching = g.Mode == LobbyGhostState.Watching ? "  <color=#9aa0a8>(ogląda)</color>" : "";
                _tag.text = $"{mic}<color=#c8c8cc>{_p.Nick}</color>{watching}";
            }
        }

        private void ChangeSeat(int seat)
        {
            _seat = seat;
            if (_act == null) return;
            var s = LobbySeat.Get(seat);
            if (seat >= 0 && s != null)
            {
                _act.SetSitSeat(s.transform, s.Shape);
                _act.BeginSitFrom(_ch.transform.position, _ch.transform.rotation);
                _act.SetSitting(true);
            }
            else _act.SetSitting(false);
        }

        // ---- ICharacterMotionSource
        private float _pitch;
        public Vector3 Velocity => _vel;
        public float HorizontalSpeed => new Vector2(_vel.x, _vel.z).magnitude;
        public bool IsGrounded => true;
        public bool IsCrouching => false;
        public bool IsRunning => HorizontalSpeed > 4f;
        public float CrouchAmount => 0f;
        public float LookPitch => _pitch;
        public LocomotionState State => HorizontalSpeed < 0.15f ? LocomotionState.Idle : IsRunning ? LocomotionState.Run : LocomotionState.Walk;
    }
}
