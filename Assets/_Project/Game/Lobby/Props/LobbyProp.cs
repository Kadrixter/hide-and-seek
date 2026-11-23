using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Lobby mini interactions (one object of each kind). Toggles keep their state in MatchManager.PropBits.</summary>
    public enum LobbyPropKind : byte
    {
        LightSwitch = 0,  // ceiling lights on / off (lamps stay on: cosy mode)
        FanSwitch = 1,    // ceiling fan on / off
        Fireplace = 2,    // electric fireplace on / off
        RecordPlayer = 3, // record spins, tone arm down, crackle
        Aquarium = 4,     // feed the fish
        Globe = 5,        // spin the floor globe
        Darts = 6,        // throw a dart (3 per round)
    }

    /// <summary>
    /// Something in the lobby the player can use with E (placed by the builder, MultiplayerBuilder.LobbyFurniture).
    /// <see cref="aimLocal"/> is where the prompt shows and the hand goes. Using it asks the server
    /// (MatchManager.LobbyProps), which broadcasts the use: the user's arm reaches (everyone sees it) and the object reacts.
    /// </summary>
    public class LobbyProp : MonoBehaviour
    {
        public LobbyPropKind kind;
        public Vector3 aimLocal;
        public float range = 2.1f;

        public static readonly List<LobbyProp> All = new();
        public Vector3 Aim => transform.TransformPoint(aimLocal);

        public static LobbyProp Of(LobbyPropKind k)
        {
            foreach (var p in All) if (p != null && p.kind == k) return p;
            return null;
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            LobbyPropDriver.Ensure();
        }

        private void OnDisable() => All.Remove(this);

        public ReachStyle Style => kind switch
        {
            LobbyPropKind.Aquarium => ReachStyle.Sprinkle,
            LobbyPropKind.Globe => ReachStyle.Sweep,
            LobbyPropKind.RecordPlayer => ReachStyle.Careful,
            LobbyPropKind.Darts => ReachStyle.Throw,
            _ => ReachStyle.Press,
        };

        public string PromptText(int bits) => kind switch
        {
            LobbyPropKind.LightSwitch => LobbyPropBits.On(bits, kind) ? "E  zgaś światło" : "E  zapal światło",
            LobbyPropKind.FanSwitch => LobbyPropBits.On(bits, kind) ? "E  wyłącz wiatrak" : "E  włącz wiatrak",
            LobbyPropKind.Fireplace => LobbyPropBits.On(bits, kind) ? "E  zgaś kominek" : "E  rozpal kominek",
            LobbyPropKind.RecordPlayer => LobbyPropBits.On(bits, kind) ? "E  zatrzymaj płytę" : "E  puść płytę",
            LobbyPropKind.Aquarium => "E  nakarm rybki",
            LobbyPropKind.Globe => "E  zakręć globusem",
            _ => "E  rzuć lotką",
        };
    }

    /// <summary>Toggle bits in MatchManager.PropBits. A set bit flips the default (lights / fan / fire on, record off).</summary>
    public static class LobbyPropBits
    {
        public static bool IsToggle(LobbyPropKind k) => k <= LobbyPropKind.RecordPlayer;
        public static bool On(int bits, LobbyPropKind k)
        {
            bool flipped = (bits & (1 << (int)k)) != 0;
            bool def = k != LobbyPropKind.RecordPlayer;
            return flipped ? !def : def;
        }
    }
}
