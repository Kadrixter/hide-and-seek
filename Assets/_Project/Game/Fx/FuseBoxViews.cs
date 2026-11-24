using HideAndSeek.Game.Net;
using HideAndSeek.Map;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The three fuse boxes on every peer, built from <see cref="MatchManager.FuseBoxes"/> (late joiners too): seated
    /// fuses with their marks, the signal lamp (amber standby, green = working and locked, red blinking = wrong fuses),
    /// the locking bar, the hum of a working box, the sparks / click when the right box comes alive.
    /// A socket somebody is working on right now is drawn by that player's <see cref="FuseWorkDriver"/> instead.
    /// </summary>
    public static class FuseBoxViews
    {
        private static readonly FuseBoxView[] Views = new FuseBoxView[FuseTuning.Boxes];

        public static FuseBoxView Get(int i) => i >= 0 && i < Views.Length ? Views[i] : null;

        /// <summary>Every frame (MatchManager.Fuses): builds / updates / removes the views.</summary>
        public static void Sync(FuseBoxSet set, bool show)
        {
            for (int i = 0; i < Views.Length; i++)
            {
                var b = set[i];
                bool want = show && b.Placed;
                var v = Views[i];
                if (v != null && (!want || !v.Matches(b, set.Round)))
                {
                    Object.Destroy(v.gameObject);
                    Views[i] = v = null;
                }
                if (want && v == null) Views[i] = v = FuseBoxView.Create(i, b, set.Round);
                if (v != null) v.Apply(b);
            }
        }

        public static void PlayEvent(int box, bool right) => Get(box)?.PlayEvent(right);
    }

    public sealed class FuseBoxView : MonoBehaviour
    {
        private int _index, _round;
        private FuseBox _box;
        private FuseBoxModel.Parts _parts;
        private readonly Transform[] _fuses = new Transform[FuseTuning.Sockets];
        private AudioSource _hum;
        private float _bar = 0f, _flash, _born;

        public FuseBox Box => _box;

        public static FuseBoxView Create(int index, FuseBox b, int round)
        {
            var go = new GameObject($"FuseBox_{index}");
            go.transform.SetPositionAndRotation(b.Pos, b.Rot);
            var v = go.AddComponent<FuseBoxView>();
            v._index = index;
            v._round = round;
            v._box = b;
            v._born = Time.time;
            v._parts = FuseBoxModel.Create(go.transform, b.S0, b.S1, b.S2, b.S3);
            LevelLighting.SetLevelLayer(go.transform, b.Level);
            for (int i = 0; i < FuseTuning.Sockets; i++)
            {
                var f = FuseModel.Create(go.transform, b.Symbol(i));
                f.localPosition = FuseBoxModel.FuseLocal(i, 0f);
                f.localRotation = Quaternion.identity;
                LevelLighting.SetLevelLayer(f, b.Level);
                f.gameObject.SetActive(false);
                v._fuses[i] = f;
            }
            Debug.Log($"[Fuses] box view {index} built at {b.Pos} (level {b.Level})");
            return v;
        }

        public bool Matches(in FuseBox b, int round) => round == _round && b.Pos == _box.Pos && b.S0 == _box.S0 && b.S1 == _box.S1 && b.S2 == _box.S2 && b.S3 == _box.S3;

        /// <summary>World pose of socket <paramref name="i"/>'s seated fuse and the socket's outward axis.</summary>
        public Vector3 SeatedWorld(int i) => transform.TransformPoint(FuseBoxModel.FuseLocal(i, 0f));
        public Vector3 Axis => transform.forward;
        /// <summary>Point on the frame (left of a player facing the box) for the steadying hand.</summary>
        public Vector3 BraceWorld => transform.TransformPoint(new Vector3(FuseBoxModel.W * 0.5f - 0.02f, FuseBoxModel.SocketY + 0.12f, FuseBoxModel.D + 0.02f));

        public void Apply(FuseBox b)
        {
            _box = b;
            for (int i = 0; i < FuseTuning.Sockets; i++)
            {
                bool show = b.Has(i) && !Worked(i);
                if (_fuses[i].gameObject.activeSelf != show) _fuses[i].gameObject.SetActive(show);
            }

            // Lamp.
            float t = Time.time;
            _flash = Mathf.Max(0f, _flash - Time.deltaTime);
            if (b.Locked) FuseBoxModel.SetLamp(_parts.Lamp, new Color(0.25f, 1f, 0.35f), 0.85f + 0.15f * Mathf.Sin(t * 3f) + _flash * 2f);
            else if (b.Lit) FuseBoxModel.SetLamp(_parts.Lamp, new Color(1f, 0.15f, 0.08f), Mathf.Repeat(t * 2f, 1f) < 0.55f ? 1f : 0.08f);
            else FuseBoxModel.SetLamp(_parts.Lamp, new Color(1f, 0.6f, 0.15f), 0.3f + 0.05f * Mathf.Sin(t * 1.3f + _index));

            // Locking bar swings down over the fuses once the box is locked (a late joiner sees it down at once).
            float want = b.Locked ? 1f : 0f;
            _bar = Time.time - _born < 0.5f ? want : Mathf.MoveTowards(_bar, want, Time.deltaTime / 0.45f);
            float k = _bar < 1f ? _bar * _bar : 1f; // drops in its rails, then sits on the caps
            _parts.Bar.localPosition = new Vector3(0f, Mathf.Lerp(FuseBoxModel.BarUpY, FuseBoxModel.BarDownY, k), FuseBoxModel.BarZ);

            // Hum of a working box.
            if (b.Locked && _hum == null)
            {
                _hum = gameObject.AddComponent<AudioSource>();
                _hum.clip = FuseSounds.Hum;
                _hum.loop = true;
                _hum.spatialBlend = 1f;
                _hum.rolloffMode = AudioRolloffMode.Linear;
                _hum.minDistance = 1f;
                _hum.maxDistance = 9f;
                _hum.volume = 0.22f;
                _hum.dopplerLevel = 0f;
                _hum.Play();
            }
            else if (!b.Locked && _hum != null) { Destroy(_hum); _hum = null; }
        }

        private bool Worked(int socket)
        {
            foreach (var p in NetPlayer.All)
            {
                if (p == null) continue;
                var w = p.FuseWork.Value;
                if (w.Active && w.Box == _index && w.Socket == socket && w.Remove) return true;
            }
            return false;
        }

        /// <summary>The right box: sparks out of the sockets, click, bright lamp. A wrong one: the loud power-on (lights: FloorLights).</summary>
        public void PlayEvent(bool right)
        {
            Vector3 mid = transform.TransformPoint(new Vector3(0f, FuseBoxModel.SocketY, FuseBoxModel.FuseHomeZ + 0.1f));
            if (right)
            {
                _flash = 1f;
                FuseSounds.Click(mid);
                FuseSounds.Spark(mid);
                FuseSparks.Burst(mid, transform.forward, 26);
            }
            else FuseSounds.PowerOn(mid, 1f, 60f);
        }
    }
}
