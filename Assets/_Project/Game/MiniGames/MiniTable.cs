using HideAndSeek.Characters;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Common part of a board-game table in the lobby: seats (A at local -Z, B at +Z), where a seated player is put,
    /// the seated pose of both players every frame (reach / think / look come from the concrete table), a 3D sound
    /// source, the camera spot above the board for each side.
    /// </summary>
    public abstract class MiniTable : MonoBehaviour
    {
        public abstract byte Game { get; }
        public abstract string Title { get; }
        protected abstract float StoolZ { get; }
        protected abstract float TopY { get; }
        protected abstract float TableHalf { get; }
        /// <summary>Board surface height (local), for the camera and the cursor plane.</summary>
        public abstract float SurfaceY { get; }
        public abstract ulong SeatPlayer(int seat);
        /// <summary>Short seat name for the prompt ("białe", "X").</summary>
        public abstract string SeatName(int seat);
        public abstract bool Playing { get; }
        /// <summary>Prompt verb ("usiądź" at a table, "podejdź" to a wall board).</summary>
        public virtual string JoinVerb => "usiądź";

        public static float Side(int seat) => seat == 0 ? -1f : 1f;

        /// <summary>Where a seated player's root stands (on the stool centre; the controller is off while seated).</summary>
        public virtual Vector3 SeatStand(int seat) => transform.TransformPoint(new Vector3(0f, 0.02f, Side(seat) * StoolZ));
        public virtual float SeatYaw(int seat) => transform.eulerAngles.y + (seat == 0 ? 0f : 180f);
        /// <summary>Where a player stands after getting up (behind the stool).</summary>
        public virtual Vector3 LeavePoint(int seat) => transform.TransformPoint(new Vector3(0f, 0.02f, Side(seat) * (StoolZ + 0.62f)));
        public Vector3 SeatTop(int seat) => transform.TransformPoint(new Vector3(0f, TableKit.StoolHeight + 0.034f, Side(seat) * StoolZ));
        public Vector3 TableEdge(int seat) => transform.TransformPoint(new Vector3(0f, TopY, Side(seat) * TableHalf));
        public float FootRestY => transform.position.y + TableKit.FootRing + 0.015f;
        public virtual Vector3 Centre => transform.TransformPoint(new Vector3(0f, SurfaceY, 0f));

        /// <summary>Camera above the board seen from a seat (pitched down at the board from behind the player's head).</summary>
        public virtual void CameraPose(int seat, float zoom, out Vector3 pos, out Quaternion rot)
        {
            float s = Side(seat);
            Vector3 local = new Vector3(0f, SurfaceY + 0.78f * zoom, s * 0.52f * zoom);
            Vector3 at = new Vector3(0f, SurfaceY, s * 0.035f);
            pos = transform.TransformPoint(local);
            rot = Quaternion.LookRotation(transform.TransformPoint(at) - pos, Vector3.up);
        }

        // ================================================================ seated poses

        readonly NetPlayer[] _posed = new NetPlayer[2];
        protected AudioSource Audio;

        protected virtual void Awake()
        {
            Audio = gameObject.AddComponent<AudioSource>();
            Audio.spatialBlend = 1f;
            Audio.minDistance = 1.2f;
            Audio.maxDistance = 14f;
            Audio.rolloffMode = AudioRolloffMode.Linear;
            Audio.playOnAwake = false;
            Audio.dopplerLevel = 0f;
        }

        public void PlaySfx(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null || Audio == null) return;
            Audio.pitch = pitch;
            Audio.PlayOneShot(clip, volume);
        }

        /// <summary>Reach target / weight, thinking weight, look target and extra lean of the player in a seat.</summary>
        protected abstract void PoseInputs(int seat, out Vector3 reach, out float reachW, out float think, out Vector3 look, out float lean);

        protected void DrivePoses()
        {
            for (int seat = 0; seat < 2; seat++)
            {
                ulong id = SeatPlayer(seat);
                var pl = id == ulong.MaxValue ? null : NetPlayer.Find(id);
                if (_posed[seat] != pl)
                {
                    StopPose(_posed[seat]);
                    _posed[seat] = pl;
                }
                if (pl == null) continue;
                var act = pl.Actions as BlockyCharacterActions;
                if (act == null) continue;
                float reachW = ApplyPose(act, seat, (id % 97) * 0.37f);
                if (pl.IsOwner) pl.BoardBodyTick(reachW > 0.08f);
            }
        }

        /// <summary>Poses one seated player for this frame; returns the reach weight (the own arm shows while reaching).</summary>
        protected virtual float ApplyPose(BlockyCharacterActions act, int seat, float seed)
        {
            PoseInputs(seat, out var reach, out float reachW, out float think, out var look, out float lean);
            act.SetBoardGame(true, SeatTop(seat), TableEdge(seat), FootRestY, reach, reachW, think, look, lean, seed);
            return reachW;
        }

        static void StopPose(NetPlayer pl)
        {
            if (pl == null) return;
            pl.SetGhostBody(false);
            (pl.Actions as BlockyCharacterActions)?.SetBoardGame(false, default, default, 0f, default, 0f, 0f, default, 0f, 0f);
        }

        protected virtual void OnDestroy()
        {
            StopPose(_posed[0]);
            StopPose(_posed[1]);
        }

        /// <summary>Right-hand rest point of a seat (fist on the table edge), used as the start of a reach.</summary>
        protected Vector3 HandRest(int seat) => transform.TransformPoint(new Vector3(-Side(seat) * 0.19f, TopY + 0.05f, Side(seat) * (TableHalf - 0.04f)));

        /// <summary>Nick of the player in a seat ("" when empty).</summary>
        public string SeatNick(int seat)
        {
            ulong id = SeatPlayer(seat);
            var p = id == ulong.MaxValue ? null : NetPlayer.Find(id);
            return p != null ? p.Nick : "";
        }

        /// <summary>Seat of a player at this table, -1 = none.</summary>
        public int SeatOf(ulong id) => id == ulong.MaxValue ? -1 : SeatPlayer(0) == id ? 0 : SeatPlayer(1) == id ? 1 : -1;
    }
}
