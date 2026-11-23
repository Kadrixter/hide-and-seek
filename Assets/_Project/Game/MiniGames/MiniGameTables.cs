using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Builds the chess and tic-tac-toe tables in the lobby at runtime (MatchManager calls <see cref="Ensure"/> every
    /// lobby frame): at the anchors "MiniGameSpot_Chess" / "MiniGameSpot_TicTacToe" left by the lobby builder, or at
    /// the fallback spots of <see cref="MiniGameSpots"/>. The object lives in the lobby scene, so it goes with it.
    /// </summary>
    public sealed class MiniGameTables : MonoBehaviour
    {
        static MiniGameTables _inst;
        ChessTable _chess;
        TttTable _ttt;

        public static ChessTable Chess => _inst != null ? _inst._chess : null;
        public static TttTable Ttt => _inst != null ? _inst._ttt : null;

        public static MiniTable Get(int game) => game == MatchManager.GameChess ? Chess : game == MatchManager.GameTtt ? Ttt : (MiniTable)null;

        public static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("MiniGameTables");
            _inst = go.AddComponent<MiniGameTables>();
            _inst.Build();
        }

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }

        void Build()
        {
            var template = FindTemplate();
            MiniGameSpots.Resolve(MiniGameSpots.ChessName, out var cp, out var cr, out bool cf);
            MiniGameSpots.Resolve(MiniGameSpots.TttName, out var tp, out var tr, out bool tf);
            _chess = ChessTable.Create(transform, cp, cr, template);
            _ttt = TttTable.Create(transform, tp, tr, template);
            MiniGameLocal.Ensure(); // HUD (also the spectator line) and the seated player's input
            Debug.Log($"[MiniGames] tables built: chess {cp} ({(cf ? "fallback" : "anchor")}), ttt {tp} ({(tf ? "fallback" : "anchor")})");
        }

        /// <summary>A Standard material of the lobby to clone (same shader variants as the room), else a new Standard.</summary>
        static Material FindTemplate()
        {
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                var m = r.sharedMaterial;
                if (m != null && m.shader != null && m.shader.name == "Standard" && !m.IsKeywordEnabled("_ALPHABLEND_ON")) return m;
            }
            return new Material(Shader.Find("Standard"));
        }

        /// <summary>The table and seat the local player sits at, from the replicated states.</summary>
        public static MiniTable LocalTable(out int seat)
        {
            seat = -1;
            var me = NetPlayer.Local;
            if (me == null || _inst == null) return null;
            foreach (MiniTable t in new MiniTable[] { _inst._chess, _inst._ttt })
            {
                if (t == null) continue;
                int s = t.SeatOf(me.NetworkObjectId);
                if (s >= 0) { seat = s; return t; }
            }
            return null;
        }

        /// <summary>Nearest free seat in front of the player for the "E" prompt.</summary>
        public static bool TryPrompt(Vector3 eye, Vector3 fwd, System.Func<Vector3, Transform, bool> inView, out int game, out int seat, out Vector3 aim, out string text, out float dist)
        {
            game = seat = -1; aim = default; text = null; dist = float.MaxValue;
            if (_inst == null) return false;
            foreach (MiniTable t in new MiniTable[] { _inst._chess, _inst._ttt })
            {
                if (t == null) continue;
                Vector3 c = t.Centre;
                if (Vector3.Distance(eye, c) > 2.4f || !inView(c, t.transform)) continue;
                for (int s = 0; s < 2; s++)
                {
                    if (t.SeatPlayer(s) != ulong.MaxValue) continue;
                    float d = Vector3.Distance(new Vector3(eye.x, 0f, eye.z), new Vector3(t.SeatStand(s).x, 0f, t.SeatStand(s).z));
                    if (d >= dist || d > 2.2f) continue;
                    dist = d;
                    game = t.Game;
                    seat = s;
                    aim = c + Vector3.up * 0.25f;
                    text = $"E  {t.JoinVerb} - {t.Title} ({t.SeatName(s)})";
                }
            }
            return game >= 0;
        }
    }
}
