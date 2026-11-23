using System;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Which music a boombox plays. The lobby one (<see cref="Ensure"/>) plays <see cref="MatchManager.LobbyRadio"/>;
    /// a radio found on the parking (<see cref="MapRadioSystem"/>) is the same model and player, made with
    /// <see cref="Create"/> for its own <see cref="RadioChannel"/>, with its own panel title, a volume factor (sound
    /// between floors) and a rule when its panel may stay open.
    /// </summary>
    public partial class Boombox
    {
        private Func<RadioChannel> _channel;

        /// <summary>Title of the panel.</summary>
        public string PanelTitle = "BOOMBOX";

        /// <summary>Extra volume factor (map radio: floors / walls), 1 = as set on the panel.</summary>
        public float VolumeScale = 1f;

        /// <summary>The panel closes as soon as this says no (map radio: carried away, out of reach). Null = always.</summary>
        public Func<bool> PanelAllowed;

        /// <summary>The channel this boombox plays (lobby: the lobby radio of the current match).</summary>
        public RadioChannel Channel => _channel != null ? _channel() : MatchManager.Instance?.LobbyRadio;

        public AudioSource Source => _src;
        /// <summary>Everything visible (moves on the bass); its renderers are the model.</summary>
        public Transform Visual => _visual;
        public BoxCollider Collider { get; private set; }

        /// <summary>
        /// A boombox that is not the lobby one: same model / sound chain, plays <paramref name="channel"/>. Not registered
        /// as <see cref="Instance"/>.
        /// </summary>
        public static Boombox Create(string name, Func<RadioChannel> channel)
        {
            var go = new GameObject(name);
            var b = go.AddComponent<Boombox>();
            b._channel = channel;
            b.Build();
            b.BuildAudio();
            b.Collider = go.GetComponent<BoxCollider>();
            return b;
        }

        /// <summary>Is this boombox's panel the open one.</summary>
        public bool OwnsPanel => _panelOwner == this;
    }
}
