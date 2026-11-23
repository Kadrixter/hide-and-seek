using System;
using Unity.Collections;
using Unity.Netcode;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>Round awards (accepted by the user 23.11: 10 of his + 4 proposals). Numbers are fixed for the network.</summary>
    public enum AwardId : byte
    {
        None = 0,
        Knifer = 1,       // Nożownik
        Butcher = 2,      // Rzeźnik
        Sniper = 3,       // Snajper
        Angel = 4,        // Anioł Stróż
        Rescuer = 5,      // Ratownik
        Ghost = 6,        // Duch
        Marathon = 7,     // Maratończyk
        RallyDriver = 8,  // Kierowca rajdowy
        Chatterbox = 9,   // Gaduła
        Unlucky = 10,     // Pechowiec
        ToyBreaker = 11,  // Niszczyciel zabawek
        FirstBlood = 12,  // Pierwsza krew
        LastStand = 13,   // Ostatni bastion
        LegBonk = 14,     // Nogą w łeb
    }

    /// <summary>One award as sent to the clients: who (one or more nicks on a tie) and the result text.</summary>
    public struct AwardPack : INetworkSerializable, IEquatable<AwardPack>
    {
        public byte Id;
        public FixedString128Bytes Nicks;
        public FixedString64Bytes Value;
        /// <summary>NetworkObjectId of the (first) winner, for the badge over the outro figure.</summary>
        public ulong Holder;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Nicks);
            s.SerializeValue(ref Value);
            s.SerializeValue(ref Holder);
        }

        public bool Equals(AwardPack o) => Id == o.Id && Nicks.Equals(o.Nicks) && Value.Equals(o.Value) && Holder == o.Holder;
    }

    /// <summary>Names, descriptions and icon letters of the awards (Polish, no emoji: the icon is a letter in a ring).</summary>
    public static class AwardBook
    {
        public static string Name(AwardId id) => id switch
        {
            AwardId.Knifer => "NOŻOWNIK",
            AwardId.Butcher => "RZEŹNIK",
            AwardId.Sniper => "SNAJPER",
            AwardId.Angel => "ANIOŁ STRÓŻ",
            AwardId.Rescuer => "RATOWNIK",
            AwardId.Ghost => "DUCH",
            AwardId.Marathon => "MARATOŃCZYK",
            AwardId.RallyDriver => "KIEROWCA RAJDOWY",
            AwardId.Chatterbox => "GADUŁA",
            AwardId.Unlucky => "PECHOWIEC",
            AwardId.ToyBreaker => "NISZCZYCIEL ZABAWEK",
            AwardId.FirstBlood => "PIERWSZA KREW",
            AwardId.LastStand => "OSTATNI BASTION",
            AwardId.LegBonk => "NOGĄ W ŁEB",
            _ => "",
        };

        public static string Description(AwardId id) => id switch
        {
            AwardId.Knifer => "najwięcej zabójstw nożem",
            AwardId.Butcher => "najwięcej zabójstw piłą motorową",
            AwardId.Sniper => "najwięcej zabójstw z glocka",
            AwardId.Angel => "najwięcej uleczonych osób",
            AwardId.Rescuer => "najwięcej podniesionych i wskrzeszonych",
            AwardId.Ghost => "najdłużej przeżył",
            AwardId.Marathon => "najdłuższy dystans",
            AwardId.RallyDriver => "najdłużej jeździł autkiem RC",
            AwardId.Chatterbox => "najdłużej mówił na czacie głosowym",
            AwardId.Unlucky => "złapany jako pierwszy",
            AwardId.ToyBreaker => "najwięcej rozwalonych autek RC i telefonów",
            AwardId.FirstBlood => "pierwsze trafienie w rundzie",
            AwardId.LastStand => "ostatni żywy chowający",
            AwardId.LegBonk => "najwięcej ogłuszeń rzuconą nogą",
            _ => "",
        };

        /// <summary>Icon: one letter (or sign) drawn inside a ring.</summary>
        public static string Letter(AwardId id) => id switch
        {
            AwardId.Knifer => "N",
            AwardId.Butcher => "R",
            AwardId.Sniper => "S",
            AwardId.Angel => "A",
            AwardId.Rescuer => "+",
            AwardId.Ghost => "D",
            AwardId.Marathon => "M",
            AwardId.RallyDriver => "K",
            AwardId.Chatterbox => "G",
            AwardId.Unlucky => "P",
            AwardId.ToyBreaker => "Z",
            AwardId.FirstBlood => "1",
            AwardId.LastStand => "O",
            AwardId.LegBonk => "L",
            _ => "?",
        };

        /// <summary>Seekers' awards get the red accent, the rest the sea-green one.</summary>
        public static bool SeekerAward(AwardId id) =>
            id is AwardId.Knifer or AwardId.Butcher or AwardId.Sniper or AwardId.ToyBreaker or AwardId.FirstBlood;

        /// <summary>"1 zabójstwo", "2 zabójstwa", "5 zabójstw".</summary>
        public static string Plural(int n, string one, string few, string many)
        {
            int m10 = n % 10, m100 = n % 100;
            string w = n == 1 ? one : m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? few : many;
            return n + " " + w;
        }

        /// <summary>"0:48", "4:12", "12:03".</summary>
        public static string Clock(double seconds)
        {
            int s = Math.Max(0, (int)Math.Round(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        /// <summary>"640 m", "1,2 km".</summary>
        public static string Metres(float m) => m >= 1000f ? $"{m / 1000f:0.0} km".Replace('.', ',') : $"{Math.Round(m / 10f) * 10f:0} m";

        /// <summary>"38 s", "4 min 12 s".</summary>
        public static string Duration(float s)
        {
            int t = (int)Math.Round(s);
            return t < 60 ? $"{t} s" : $"{t / 60} min {t % 60} s";
        }
    }
}
