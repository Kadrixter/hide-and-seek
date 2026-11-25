using System;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Emote clips of <see cref="CharacterBuildMenu.RecordAnimations"/>: <c>emote:Wave</c>, <c>emote:Hug</c> ...
    /// (any <see cref="EmoteId"/>; pair emotes record both bodies). Loops play two periods and are then stopped, so the
    /// way out is on the clip too. HNS_REC_YAW overrides the camera angle (0 = in front).
    /// </summary>
    public static partial class CharacterBuildMenu
    {
        static void RecordEmote(string name)
        {
            if (!Enum.TryParse<EmoteId>(name, out var id) || Emotes.Get(id) == null)
            {
                Debug.LogError("[Rec] unknown emote " + name);
                return;
            }
            var info = Emotes.Get(id);
            bool pair = info.IsPair;
            float d = info.PairDistance;
            var a = SpawnAt(2, Vector3.zero, 0f, out _, out var aa);
            BlockyCharacter bch = null;
            BlockyCharacterAnimator ba = null;
            if (pair)
            {
                bch = SpawnAt(4, new Vector3(0f, 0f, d), 180f, out _, out ba);
                a.Actions.SetEmotePartner(bch, 0);
                bch.Actions.SetEmotePartner(a, 1);
            }
            int beats = 0;
            a.Actions.EmoteBeatFired += (_, k, i) => { beats++; Debug.Log($"[Rec] beat {k} #{i}"); };
            if (pair) TickAll(0.4f, aa, ba);
            else TickAll(0.4f, aa);
            a.Actions.PlayEmote(id, 0f);
            if (pair) bch.Actions.PlayEmote(id, 0f);

            float stopAt = info.Loops ? (!info.Standing ? 6f + (info.IsPair ? 6f : 0f) : Mathf.Max(info.Period * 2f, 4f)) : 999f;
            // HNS_REC_STOP=PairDance:9,Piggyback:9 overrides when a loop is stopped.
            foreach (var kv in (Environment.GetEnvironmentVariable("HNS_REC_STOP") ?? "").Split(','))
            {
                var parts = kv.Split(':');
                if (parts.Length == 2 && parts[0] == id.ToString() && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float st)) stopAt = st;
            }
            float len = info.Loops ? stopAt + Mathf.Max(info.Exit, info.Out) + 0.5f : info.Duration + 0.4f;
            var clip = NewClip("emote_" + id);
            Vector3 mid = pair ? new Vector3(0f, 1.05f, d * 0.5f) : new Vector3(0f, !info.Standing ? 0.6f : 0.95f, 0f);
            bool fixedYaw = float.TryParse(Environment.GetEnvironmentVariable("HNS_REC_YAW"), out float yaw);
            bool stopped = false;
            for (float t = 0f; t < len; t += RecDt)
            {
                if (!stopped && t >= stopAt)
                {
                    stopped = true;
                    a.Actions.StopEmote();
                    if (pair) bch.Actions.StopEmote();
                }
                aa.Tick(RecDt);
                if (pair) ba.Tick(RecDt);
                float x = t / len;
                float y = fixedYaw ? yaw : pair ? Mathf.Lerp(52f, 128f, x) : Mathf.Lerp(30f, -30f, x);
                Orbit(mid, y, pair ? 4.4f : 3.7f, pair ? 0.5f : 0.35f, 36f);
                clip.Shot(recCam);
            }
            Debug.Log($"[Rec] emote {id}: {beats} beats, {len:0.00} s");
            Kill(a);
            if (bch != null) Kill(bch);
        }
    }
}
