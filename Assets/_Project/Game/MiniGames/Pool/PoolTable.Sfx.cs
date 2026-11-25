using System.Collections.Generic;
using HideAndSeek.Game.MiniGames.Pool;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Sounds of the table, each where it happens: a small pool of 3D voices placed at the event (ball clicks louder
    /// with the impact speed, cushions, jaws, the cue tip, balls dropping into pockets and landing in the baskets),
    /// at most a few per frame (the break makes dozens at once, the loudest win), and a rolling rumble that follows
    /// the moving balls.
    /// </summary>
    public sealed partial class PoolTable
    {
        const int Voices = 8, MaxPerFrame = 5;

        AudioSource[] _voices;
        int _nextVoice;
        AudioSource _rollSrc;
        readonly List<PoolEvent> _frameEvents = new();
        static readonly System.Comparison<PoolEvent> Louder = (a, b) => Weight(b).CompareTo(Weight(a));

        static float Weight(PoolEvent e) => e.Kind == PoolEvent.Pocket ? 100f + e.Speed : e.Speed;

        static AudioSource NewSource(Transform parent, string name)
        {
            var g = new GameObject(name) { layer = parent.gameObject.layer };
            g.transform.SetParent(parent, false);
            var s = g.AddComponent<AudioSource>();
            s.spatialBlend = 1f;
            s.minDistance = 1.0f;
            s.maxDistance = 14f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            return s;
        }

        void InitSfx()
        {
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) _voices[i] = NewSource(transform, "PoolSfx" + i);
            _rollSrc = NewSource(transform, "PoolRoll");
            _rollSrc.clip = PoolSounds.RollLoop;
            _rollSrc.loop = true;
            _rollSrc.volume = 0f;
        }

        /// <summary>Plays <paramref name="clip"/> at a world point (a free voice, else the oldest one).</summary>
        public void PlayAt(AudioClip clip, Vector3 world, float volume, float pitch = 1f)
        {
            if (clip == null || _voices == null || volume < 0.01f) return;
            int pick = _nextVoice;
            for (int k = 0; k < Voices; k++)
            {
                int idx = (_nextVoice + k) % Voices;
                if (_voices[idx] != null && !_voices[idx].isPlaying) { pick = idx; break; }
            }
            var s = _voices[pick];
            _nextVoice = (pick + 1) % Voices;
            if (s == null) return;
            s.transform.position = world;
            s.clip = clip;
            s.pitch = pitch;
            s.volume = Mathf.Clamp01(volume);
            s.Play();
        }

        void StrikeSound(float power, Vector2 cue) =>
            PlayAt(PoolSounds.Strike, ToWorld(cue), 0.3f + 0.65f * Mathf.Clamp01(power), 0.95f + 0.12f * Mathf.Clamp01(power));

        /// <summary>Events of the replayed shot up to time <paramref name="t"/> (the loudest few of this frame).</summary>
        void PlayEventsUpTo(float t)
        {
            if (_rep == null) return;
            var ev = _rep.Events;
            _frameEvents.Clear();
            while (_repEvent < ev.Count && ev[_repEvent].T <= t) _frameEvents.Add(ev[_repEvent++]);
            if (_frameEvents.Count == 0) return;
            if (_frameEvents.Count > MaxPerFrame) _frameEvents.Sort(Louder);
            for (int k = 0; k < _frameEvents.Count && k < MaxPerFrame; k++) PlayEvent(_frameEvents[k]);
        }

        void PlayEvent(PoolEvent e)
        {
            float sp = Mathf.Max(0f, e.Speed);
            Vector3 at = ToWorld(e.At);
            switch (e.Kind)
            {
                case PoolEvent.BallBall:
                    PlayAt(PoolSounds.Click, at, Mathf.Pow(Mathf.Clamp01(sp / 3f), 0.7f) * 0.9f, Random.Range(0.94f, 1.08f));
                    break;
                case PoolEvent.Cushion:
                    PlayAt(PoolSounds.Cushion, at, Mathf.Pow(Mathf.Clamp01(sp / 2.5f), 0.8f) * 0.8f, Random.Range(0.9f, 1.1f));
                    break;
                case PoolEvent.Jaw:
                    PlayAt(PoolSounds.Jaw, at, Mathf.Pow(Mathf.Clamp01(sp / 2.5f), 0.8f) * 0.75f, Random.Range(0.92f, 1.08f));
                    break;
                case PoolEvent.Pocket:
                    int p = e.B >= 0 && e.B < 6 ? e.B : NearestPocket(e.At);
                    var c = PoolTableModel.PocketCentre(p);
                    PlayAt(PoolSounds.PocketDrop, transform.TransformPoint(c), 0.55f + 0.35f * Mathf.Clamp01(sp / 2f), Random.Range(0.92f, 1.06f));
                    break;
            }
        }

        /// <summary>A dropping ball reached its place in the basket.</summary>
        void BagLanded(int i)
        {
            int p = _bag[i];
            if (p < 0) return;
            Vector3 w = transform.TransformPoint(SlotPos(p, _bagSlot[i]));
            if (_bagSlot[i] > 0) PlayAt(PoolSounds.BagClack, w, 0.45f, Random.Range(0.95f, 1.05f));
            else PlayAt(PoolSounds.BagThud, w, 0.5f, Random.Range(0.92f, 1.05f));
        }

        void TickSfx(float dt)
        {
            if (_rollSrc == null) return;
            float target = Mathf.Clamp01(_rollSpeed / 4f) * 0.28f;
            _rollSrc.volume = Mathf.MoveTowards(_rollSrc.volume, target, dt * 1.5f);
            if (target > 0.004f)
            {
                _rollSrc.transform.position = ToWorld(_rollAt, 0f);
                _rollSrc.pitch = 0.85f + 0.3f * Mathf.Clamp01(_rollSpeed / 4f);
                if (!_rollSrc.isPlaying) _rollSrc.Play();
            }
            else if (_rollSrc.volume <= 0.002f && _rollSrc.isPlaying) _rollSrc.Stop();
        }
    }
}
