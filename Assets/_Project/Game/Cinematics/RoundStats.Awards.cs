using System;
using System.Collections.Generic;
using System.Linq;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;

namespace HideAndSeek.Game.Cinematics
{
    public sealed partial class RoundStats
    {
        /// <summary>
        /// The awards of this round in display order. An award is only given when somebody really has a result
        /// (> 0 or over a small threshold); ties share it (up to 3 nicks).
        /// </summary>
        public List<AwardPack> ComputeAwards(bool seekersWon)
        {
            var list = new List<AwardPack>();
            var all = _e.Values.Where(e => NetPlayer.Find(e.Id) != null).ToList();
            var seekers = all.Where(e => e.Seeker).ToList();
            var hiders = all.Where(e => !e.Seeker).ToList();
            double seekFrom = SeekStart >= 0d ? SeekStart : RoundStart;

            Most(list, AwardId.Knifer, seekers, e => e.KnifeKills, n => AwardBook.Plural(n, "zabójstwo", "zabójstwa", "zabójstw") + " nożem");
            Most(list, AwardId.Butcher, seekers, e => e.SawKills, n => AwardBook.Plural(n, "zabójstwo", "zabójstwa", "zabójstw") + " piłą");
            Most(list, AwardId.Sniper, seekers, e => e.GlockKills, n => AwardBook.Plural(n, "zabójstwo", "zabójstwa", "zabójstw") + " z glocka");
            if (FirstBloodId != ulong.MaxValue && Find(FirstBloodId) is Entry fb && NetPlayer.Find(fb.Id) != null)
                Add(list, AwardId.FirstBlood, new List<Entry> { fb }, "pierwsze trafienie, po " + AwardBook.Clock(fb.FirstHitAt - seekFrom) + " szukania");
            Most(list, AwardId.ToyBreaker, seekers, e => e.Toys, n => AwardBook.Plural(n, "rozwalona zabawka", "rozwalone zabawki", "rozwalonych zabawek") + " (RC, telefony)");

            Most(list, AwardId.Angel, hiders, e => e.Heals, n => AwardBook.Plural(n, "uleczona osoba", "uleczone osoby", "uleczonych osób"));
            Most(list, AwardId.Rescuer, hiders, e => e.Rescues + e.Revives, n => AwardBook.Plural(n, "podniesiona osoba", "podniesione osoby", "podniesionych osób") + " (z wskrzeszonymi)");
            Ghost(list, hiders);
            LastStand(list, hiders, seekersWon);
            Most(list, AwardId.LegBonk, hiders, e => e.LegStuns, n => AwardBook.Plural(n, "ogłuszenie", "ogłuszenia", "ogłuszeń") + " rzuconą nogą");
            Unlucky(list, hiders, seekFrom);

            MostF(list, AwardId.Marathon, all, e => e.Distance, 30f, v => "przebiegł " + AwardBook.Metres(v));
            MostF(list, AwardId.RallyDriver, hiders, e => e.RcTime, 2f, v => "jeździł autkiem RC " + AwardBook.Duration(v));
            MostF(list, AwardId.Chatterbox, all, e => e.VoiceTime, 3f, v => "mówił " + AwardBook.Duration(v) + " na czacie głosowym");
            return list;
        }

        static void Add(List<AwardPack> list, AwardId id, List<Entry> who, string value)
        {
            if (who.Count == 0) return;
            var nicks = string.Join(", ", who.Take(3).Select(w => w.Nick));
            list.Add(new AwardPack { Id = (byte)id, Nicks = Clip128(nicks), Value = Clip64(value), Holder = who[0].Id });
        }

        static void Most(List<AwardPack> list, AwardId id, List<Entry> from, Func<Entry, int> v, Func<int, string> text)
        {
            int best = from.Count > 0 ? from.Max(v) : 0;
            if (best <= 0) return;
            Add(list, id, from.Where(e => v(e) == best).ToList(), text(best));
        }

        static void MostF(List<AwardPack> list, AwardId id, List<Entry> from, Func<Entry, float> v, float min, Func<float, string> text)
        {
            float best = from.Count > 0 ? from.Max(v) : 0f;
            if (best < min) return;
            Add(list, id, from.Where(e => v(e) >= best - 0.001f).ToList(), text(best));
        }

        /// <summary>Longest alive; on a tie (within 2 s, e.g. several survived to the end) the one seekers saw least.</summary>
        void Ghost(List<AwardPack> list, List<Entry> hiders)
        {
            if (hiders.Count == 0) return;
            float best = hiders.Max(e => e.AliveTime);
            if (best < 5f) return;
            var top = hiders.Where(e => e.AliveTime >= best - 2f).OrderBy(e => e.SeenTime).ToList();
            var g = top[0];
            string value = g.DiedAt < 0d
                ? (g.SeenTime < 0.5f ? "przeżył najdłużej, nikt go nie widział" : $"przeżył najdłużej, widziany tylko {AwardBook.Duration(g.SeenTime)}")
                : "przeżył najdłużej: " + AwardBook.Clock(g.AliveTime);
            Add(list, AwardId.Ghost, new List<Entry> { g }, value);
        }

        /// <summary>Seekers won: the hider who fell last. Hiders won: the only hider still alive (if just one).</summary>
        void LastStand(List<AwardPack> list, List<Entry> hiders, bool seekersWon)
        {
            if (hiders.Count < 2) return;
            if (seekersWon)
            {
                if (DeathOrder.Count == 0) return;
                var last = Find(DeathOrder[^1]);
                if (last != null && NetPlayer.Find(last.Id) != null) Add(list, AwardId.LastStand, new List<Entry> { last }, "ostatni żywy, padł jako ostatni");
                return;
            }
            var alive = hiders.Where(e => NetPlayer.Find(e.Id) is NetPlayer p && p.Life.Value != LifeState.Dead).ToList();
            if (alive.Count == 1) Add(list, AwardId.LastStand, alive, "jedyny przeżył do końca");
        }

        static void Unlucky(List<AwardPack> list, List<Entry> hiders, double seekFrom)
        {
            var caught = hiders.Where(e => e.CaughtAt >= 0d).OrderBy(e => e.CaughtAt).ToList();
            if (caught.Count == 0 || hiders.Count < 2) return;
            Add(list, AwardId.Unlucky, new List<Entry> { caught[0] }, "złapany pierwszy, po " + AwardBook.Clock(caught[0].CaughtAt - seekFrom) + " szukania");
        }

        static Unity.Collections.FixedString128Bytes Clip128(string s)
        {
            while (Utf8(s) > 120) s = s.Substring(0, s.Length - 1);
            return s;
        }

        static Unity.Collections.FixedString64Bytes Clip64(string s)
        {
            while (Utf8(s) > 60) s = s.Substring(0, s.Length - 1);
            return s;
        }

        static int Utf8(string s) => System.Text.Encoding.UTF8.GetByteCount(s);
    }
}
