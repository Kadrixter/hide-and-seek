using System.Collections.Generic;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Plays the round outro on this client (<see cref="OutroTimeline"/>): a lit stage in front of the closed exit
    /// gate, the winners as puppets (their skins, the dead ones too, no wounds) walking into a row and turning three
    /// quarters to the camera, the camera travelling along the row, nicks under the figures, the title, then the award
    /// cards. The players' own emotes (K wheel, also pair emotes with a row neighbour) play on their figures.
    /// </summary>
    public sealed partial class OutroDirector : MonoBehaviour
    {
        public static OutroDirector Current { get; private set; }
        /// <summary>The row of the outro being played (also on the server, for the pair emote checks).</summary>
        public static readonly List<ulong> Row = new();
        public static bool Recording;

        public const float Spacing = 1.0f, RowZ = 2.35f;

        OutroPack _pack;
        System.Func<double> _clock;
        float _t, _prevT = -10f;
        ExitGate _gate;
        CineCamera _cam;
        readonly List<CinePuppet> _puppets = new();
        readonly List<float> _autoAt = new();
        readonly List<EmoteId> _autoEmote = new();
        bool _localWinner;

        public float T => _t;

        public static OutroDirector Play(OutroPack pack, System.Func<double> clock)
        {
            if (Current != null) Destroy(Current.gameObject);
            SetRow(pack.Row);
            var go = new GameObject("OutroDirector");
            var d = go.AddComponent<OutroDirector>();
            d._pack = pack;
            d._clock = clock;
            d._t = (float)(clock() - pack.StartAt);
            Current = d;
            d.Begin();
            return d;
        }

        public static void SetRow(ulong[] ids)
        {
            Row.Clear();
            if (ids != null) Row.AddRange(ids);
        }

        void Begin()
        {
            _gate = ExitGate.Ensure();
            _gate.SetOpen(0f);
            _gate.SetLever(1f);
            var me = NetPlayer.Local;
            _localWinner = me != null && Row.Contains(me.NetworkObjectId);
            BuildStage();
            BuildRow();
            BuildOverlay();
            Debug.Log($"[Cine] outro start t={_t:0.00} winner={(_pack.SeekersWon ? "seekers" : "hiders")} row={Row.Count} awards={_pack.Awards?.Length ?? 0}");
        }

        /// <summary>Slot <paramref name="i"/> of <paramref name="n"/> in gate-local space.</summary>
        static Vector3 Slot(int i, int n) => new Vector3((i - (n - 1) * 0.5f) * Spacing, 0f, RowZ);

        /// <summary>Facing of the row: towards the garage, turned a little to the side the camera travels from.</summary>
        float RowYaw(int i, int n)
        {
            Vector3 f = _gate.LocalDir(Quaternion.Euler(0f, -24f + (i - (n - 1) * 0.5f) * 3f, 0f) * Vector3.forward);
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        void BuildRow()
        {
            int n = Row.Count;
            int layer = HideAndSeek.Map.LevelLighting.LayerOf(0);
            var rng = new System.Random((int)(_pack.StartAt * 10) & 0x7fffffff);
            EmoteId[] cheer = _pack.SeekersWon
                ? new[] { EmoteId.Laugh, EmoteId.Point, EmoteId.Salute, EmoteId.Clap }
                : new[] { EmoteId.Wave, EmoteId.Clap, EmoteId.Salute, EmoteId.Wave };
            for (int i = 0; i < n; i++)
            {
                var pl = NetPlayer.Find(Row[i]);
                if (pl == null) continue;
                Vector3 slot = _gate.Local(Slot(i, n));
                // Enter from the left of the frame, one after another.
                Vector3 from = _gate.Local(Slot(i, n) + new Vector3(-3.4f - i * 0.25f, 0f, 0.35f));
                float walkYaw = Mathf.Atan2((slot - from).x, (slot - from).z) * Mathf.Rad2Deg;
                var p = CinePuppet.Of(pl, transform, from, walkYaw, layer);
                if (pl.IsSeeker) p.Actions.SetHeldItem(i % 3 == 1 && pl.HasChainsaw.Value ? HeldItem.Chainsaw : HeldItem.Knife);
                p.gameObject.SetActive(false);
                _puppets.Add(p);
                _autoAt.Add(OutroTimeline.Travel + 0.6f + (float)rng.NextDouble() * 3.2f);
                _autoEmote.Add(cheer[rng.Next(cheer.Length)]);
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (!Recording && _clock != null)
            {
                float server = (float)(_clock() - _pack.StartAt);
                float err = server - _t;
                if (Mathf.Abs(err) > 0.35f) _t = server;
                else _t += err * Mathf.Min(1f, dt * 2f);
            }
            float t = _t;
            Events(_prevT, t);
            _prevT = t;
            UpdateRow(t, dt);
            UpdateStage(t);
            UpdateCamera(t);
            UpdateOverlay(t);
            if (t >= OutroTimeline.End + 2f) Destroy(gameObject); // the lobby normally loads before
        }

        void UpdateRow(float t, float dt)
        {
            int n = _puppets.Count;
            for (int i = 0; i < n; i++)
            {
                var p = _puppets[i];
                if (p == null) continue;
                float go = OutroTimeline.WalkIn + i * OutroTimeline.WalkStagger;
                if (t >= OutroTimeline.StageIn && !p.gameObject.activeSelf) p.gameObject.SetActive(true);
                if (t >= go && !_walked.Contains(i))
                {
                    _walked.Add(i);
                    p.WalkTo(_gate.Local(Slot(i, n)), 1.55f, RowYaw(i, n));
                }
                MirrorEmote(p, i, t);
                p.Tick(dt);
            }
        }

        readonly HashSet<int> _walked = new();

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                Row.Clear();
            }
            if (_cam != null) _cam.Release();
            HudHider.KeepEmotes = false;
        }
    }
}
