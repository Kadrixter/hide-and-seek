using HideAndSeek.Audio;
using HideAndSeek.Characters;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// "Why this dealer": the Yaris on the floor (parked or driven) squashes flat and wide on every beat, springs
    /// back taller and narrower with a little hop and a lean, wobbles out and hits the next beat - exaggerated, rubbery.
    /// Its music plays in 3D from the car (loop) while it is on the floor; the squash follows the music's own clock
    /// (or local time without music), so every peer sees it in time with what it hears. In the hand / on the shelf it
    /// stands still. Crushed: the music winds down like a stopped tape.
    /// </summary>
    [DefaultExecutionOrder(200)] // after RcCar.Update
    public class YarisCarFx : MonoBehaviour
    {
        private RcCar _car;
        private RcCarSkin _skin;
        private float _w, _danceStart, _phaseClock;
        private AudioSource _music;
        private AudioLowPassFilter _lp;
        private float _musicFade, _tapeStop = -1f, _nextAcoustics, _floorGain = 1f, _floorTarget = 1f, _cutoff = 22000f, _occ;
        private bool _wasGround, _shardsPainted;
        private static readonly Collider[] Near = new Collider[48];

        /// <summary>0..1 how much the car is dancing right now (tests).</summary>
        public float Dance => _w;
        public bool MusicPlaying => _music != null && _music.isPlaying;

        private void Awake()
        {
            _car = GetComponent<RcCar>();
        }

        private void LateUpdate()
        {
            if (_car == null || _car.Parts == null) return;
            if (_skin == null) _skin = _car.Parts.Root.GetComponent<RcCarSkin>();
            if (_skin == null || _skin.Yaris == null) return;
            var y = _skin.Yaris;
            float dt = Time.deltaTime;
            var v = _car.View;
            bool yaris = _skin.IsYaris;
            bool ground = yaris && (v == RcCar.ViewMode.Parked || v == RcCar.ViewMode.Local || v == RcCar.ViewMode.Remote);
            bool musicOn = yaris && (ground || v == RcCar.ViewMode.Frozen);

            if (ground && !_wasGround) _danceStart = Time.time;
            _wasGround = ground;
            UpdateMusic(musicOn, v == RcCar.ViewMode.Wreck, dt);

            _w = Mathf.MoveTowards(_w, ground ? 1f : 0f, dt / (ground ? 0.4f : 0.18f));
            if (v == RcCar.ViewMode.Wreck || !yaris)
            {
                y.Squash.localScale = Vector3.one;
                y.Squash.localPosition = Vector3.zero;
                y.Squash.localRotation = Quaternion.identity;
                if (v == RcCar.ViewMode.Wreck && yaris && y.Antenna != null)
                    y.Antenna.localPosition = new Vector3(y.AntennaBase.x, y.AntennaBase.y * 0.42f - 0.002f, y.AntennaBase.z);
                if (v == RcCar.ViewMode.Wreck && yaris && !_shardsPainted) PaintShards();
                return;
            }

            // Beat clock: the music's own playhead when it plays, otherwise local time since the car went down.
            float bpm = YarisMusic.Bpm;
            float beatLen = 60f / Mathf.Max(30f, bpm);
            if (_music != null && _music.isPlaying && _music.clip != null)
                _phaseClock = (_music.timeSamples / (float)_music.clip.frequency - YarisMusic.Offset) / beatLen;
            else
                _phaseClock = (Time.time - _danceStart) / beatLen;

            Pose(_phaseClock, _w, v == RcCar.ViewMode.Local, out var scale, out float hop, out float roll);
            y.Squash.localScale = scale;
            y.Squash.localPosition = new Vector3(0f, hop, 0f);
            y.Squash.localRotation = Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>
        /// Squash-and-stretch pose for <paramref name="beats"/> (beat count; the squash peaks on every whole beat),
        /// blended in by <paramref name="w"/>. Local driver: no hop (the camera sits right above the roof).
        /// </summary>
        public static void Pose(float beats, float w, bool driver, out Vector3 scale, out float hop, out float roll)
        {
            float q = Mathf.Repeat(beats + 0.1f, 1f); // q = 0.1 on the beat
            float d;
            if (q < 0.1f) d = Mathf.Sin(q / 0.1f * Mathf.PI * 0.5f);            // slam down onto the beat
            else
            {
                float t = (q - 0.1f) / 0.9f;
                d = Mathf.Cos(t * Mathf.PI * 2f * 1.75f) * Mathf.Exp(-t * 2.1f); // spring back: stretch, squish, stretch, settle
            }
            // a tiny anticipation stretch just before the hit
            if (q > 0.9f) d -= 0.12f * Mathf.Sin((q - 0.9f) / 0.1f * Mathf.PI);
            d *= w;
            float sy = d > 0f ? 1f - 0.37f * d : 1f - 0.36f * d;
            float sx = 1f + 0.32f * d;
            float sz = 1f + 0.12f * d;
            scale = new Vector3(sx, sy, sz);
            float tHop = (q - 0.14f) / 0.34f;
            hop = tHop > 0f && tHop < 1f ? Mathf.Sin(tHop * Mathf.PI) * (driver ? 0f : 0.02f) * w : 0f;
            // lean left / right on alternate beats while in the air
            int beat = Mathf.FloorToInt(beats + 0.1f);
            float side = (beat & 1) == 0 ? 1f : -1f;
            roll = tHop > 0f && tHop < 1f ? side * 4.5f * Mathf.Sin(tHop * Mathf.PI) * w : 0f;
        }

        /// <summary>The crushed car's plastic bits fly in the rally livery: repaint them in the Yaris colours.</summary>
        private void PaintShards()
        {
            _shardsPainted = true;
            var colors = new[] { YarisModel.PaintColor, YarisModel.PaintColor * 0.85f, new Color(0.05f, 0.06f, 0.075f), new Color(0.8f, 0.83f, 0.86f) };
            Physics.SyncTransforms();
            int n = Physics.OverlapSphereNonAlloc(transform.position, 1.2f, Near, 1 << RcCar.CarLayer, QueryTriggerInteraction.Collide);
            int k = 0;
            var mpb = new MaterialPropertyBlock();
            for (int i = 0; i < n; i++)
            {
                var c = Near[i];
                if (c == null || c.name != "RcShard") continue;
                var r = c.GetComponent<Renderer>();
                if (r == null) continue;
                mpb.SetColor("_Color", colors[k++ % colors.Length]);
                r.SetPropertyBlock(mpb);
            }
        }

        // ================================================================ music

        private void UpdateMusic(bool on, bool wrecked, float dt)
        {
            if (on && _music == null && _tapeStop < 0f)
            {
                var clip = YarisMusic.Clip;
                if (clip != null) StartMusic(clip);
            }
            if (_music == null) return;

            if (!on && _tapeStop < 0f && wrecked) _tapeStop = 0f;
            if (_tapeStop >= 0f)
            {
                // crushed: pitch and level sag like a cassette losing power
                _tapeStop += dt;
                float k = Mathf.Clamp01(_tapeStop / 0.7f);
                _music.pitch = Mathf.Lerp(1f, 0.22f, k * k);
                _musicFade = 1f - k * k;
                if (k >= 1f) { StopMusic(); return; }
            }
            else _musicFade = Mathf.MoveTowards(_musicFade, on ? 1f : 0f, dt / (on ? 0.25f : 0.35f));
            if (!on && _tapeStop < 0f && _musicFade <= 0f) { StopMusic(); return; }

            var l = ActiveListener.Find();
            if (l != null && Time.time >= _nextAcoustics)
            {
                _nextAcoustics = Time.time + 0.15f;
                Vector3 lp = l.transform.position, pos = transform.position + Vector3.up * 0.1f;
                _occ = AudioOcclusion.Sample(lp, pos, l.transform.root, transform, GameAudio.OcclusionMask);
                _floorTarget = 1f;
                _cutoff = 22000f;
                if (FloorAcoustics.DifferentFloor(lp, pos)) _floorTarget = FloorAcoustics.Transfer(lp, pos, _occ <= 0f, out _cutoff);
            }
            _floorGain = Mathf.MoveTowards(_floorGain, _floorTarget, dt * 3f);
            float cut = Mathf.Min(_occ > 0.5f ? 1800f : _occ > 0.1f ? 5000f : 22000f, _floorGain < 0.99f ? _cutoff : 22000f);
            if (_lp != null) _lp.cutoffFrequency = Mathf.Lerp(_lp.cutoffFrequency, cut, 0.25f);
            float settings = GameAudio.SfxVolume * Mathf.Clamp01(GameAudio.MusicVolume / 0.7f);
            _music.volume = YarisEgg.MusicVolume * settings * _musicFade * _floorGain * (1f - 0.35f * Mathf.Clamp01(_occ));
        }

        private void StartMusic(AudioClip clip)
        {
            var go = new GameObject("YarisMusic");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            _music = go.AddComponent<AudioSource>();
            _music.clip = clip;
            _music.loop = true;
            _music.playOnAwake = false;
            _music.spatialBlend = 1f;
            _music.rolloffMode = AudioRolloffMode.Linear;
            _music.minDistance = YarisEgg.MusicMinDistance;
            _music.maxDistance = YarisEgg.MusicMaxDistance;
            _music.spread = 60f;
            _music.dopplerLevel = 0f;
            _music.priority = 64;
            _music.volume = 0f;
            _lp = go.AddComponent<AudioLowPassFilter>();
            _lp.cutoffFrequency = 22000f;
            _musicFade = 0f;
            _music.time = 0f;
            _music.Play();
        }

        private void StopMusic()
        {
            if (_music != null) Destroy(_music.gameObject);
            _music = null;
            _lp = null;
            _musicFade = 0f;
        }

        private void OnDestroy() => StopMusic();
    }
}
