using System;
using System.Collections.Generic;
using System.Diagnostics;
using HideAndSeek.Game.MiniGames.Pool;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Pool logic check (batchmode: -executeMethod HideAndSeek.Game.Editor.PoolSimCheck.RunFromCommandLine). Breaks and
    /// hundreds of random shots through <see cref="PoolSim.Run"/>: no NaN, no ball outside the bed or through a cushion,
    /// no overlaps at rest, sane durations, balls go down, determinism, the network text both ways, and the 8-ball rules
    /// on hand-made shot results (<see cref="PoolSimCheck.Rules"/>). Logs "[PoolCheck]"; exit code 0 = all passed.
    /// </summary>
    public static partial class PoolSimCheck
    {
        static int _fail, _pass;
        const float HalfL = PoolDims.PlayL * 0.5f, HalfW = PoolDims.PlayW * 0.5f, R = PoolDims.BallR, D = PoolDims.BallD;
        const float FieldX = HalfL - R, FieldY = HalfW - R, BedX = HalfL + 0.05f, BedZ = HalfW + 0.05f;

        [MenuItem("HideAndSeek/Mini games/Run pool check")]
        public static void RunMenu() => Run();

        public static void RunFromCommandLine()
        {
            bool ok = false;
            try { ok = Run(); }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static void Check(string name, bool ok, string detail = "")
        {
            if (ok) _pass++; else _fail++;
            if (!ok || detail.Length > 0) Debug.Log($"[PoolCheck] {(ok ? "OK  " : "FAIL")} {name} {detail}");
        }

        public static bool Run()
        {
            _fail = _pass = 0;
            var sw = Stopwatch.StartNew();
            Breaks();
            RandomShots();
            StraightPots();
            Determinism();
            Codec();
            Rules();
            Debug.Log($"[PoolCheck] SUMMARY passed={_pass} failed={_fail} ({sw.ElapsedMilliseconds} ms)");
            return _fail == 0;
        }

        /// <summary>Nominal pocket corners (for "is this ball in the jaws").</summary>
        static float NearPocket(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                var c = new Vector2((i % 3 - 1) * HalfL, (i < 3 ? -1 : 1) * HalfW);
                best = Mathf.Min(best, (p - c).magnitude);
            }
            return best;
        }

        static bool Bad(Vector2 v) => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y);

        /// <summary>Checks one simulated shot; returns a problem text or null.</summary>
        static string Validate(Vector2[] start, ushort down, PoolSimResult r)
        {
            if (r == null || r.Frames.Count == 0) return "no frames";
            if (float.IsNaN(r.Duration) || r.Duration > PoolSim.MaxTime + 0.1f) return $"duration {r.Duration}";
            for (int f = 0; f < r.Frames.Count; f++)
            {
                var fr = r.Frames[f];
                ushort dm = r.DownMask[f];
                for (int i = 0; i < 16; i++)
                {
                    if ((dm & (1 << i)) != 0) continue;
                    if (Bad(fr[i])) return $"NaN ball {i} frame {f}";
                    Vector2 q = fr[i];
                    bool onField = Mathf.Abs(q.x) <= FieldX + 2e-4f && Mathf.Abs(q.y) <= FieldY + 2e-4f;
                    if (!onField && NearPocket(q) > 0.16f) return $"ball {i} off the cloth at frame {f}: {q.x:0.0000},{q.y:0.0000}";
                    if (Mathf.Abs(q.x) > BedX + 0.02f || Mathf.Abs(q.y) > BedZ + 0.02f) return $"ball {i} behind the cushions at frame {f}: {q}";
                    if (f > 0 && (r.DownMask[f - 1] & (1 << i)) == 0 && (fr[i] - r.Frames[f - 1][i]).magnitude > 9.5f * PoolSimResult.FrameDt + 1e-4f)
                        return $"ball {i} jumped at frame {f}";
                }
            }
            for (int i = 0; i < 16; i++)
            {
                if ((r.FinalDown & (1 << i)) != 0) continue;
                for (int j = i + 1; j < 16; j++)
                {
                    if ((r.FinalDown & (1 << j)) != 0) continue;
                    float d = (r.FinalPos[i] - r.FinalPos[j]).magnitude;
                    if (d < D - 2e-4f) return $"balls {i},{j} overlap at rest ({d * 1000f:0.00} mm)";
                }
                Vector2 q = r.FinalPos[i];
                if (!(Mathf.Abs(q.x) <= FieldX + 2e-4f && Mathf.Abs(q.y) <= FieldY + 2e-4f) && NearPocket(q) > 0.16f) return $"ball {i} rests off the cloth {q}";
            }
            if ((down & r.FinalDown) != down) return "a pocketed ball came back";
            return null;
        }

        static PoolState Racked(int seed)
        {
            var s = new PoolState();
            s.Rack(new System.Random(seed));
            return s;
        }

        static void Breaks()
        {
            var rng = new System.Random(12345);
            int n = 200, potted = 0, scratch = 0, eight = 0, bad = 0, legal = 0, ballsDown = 0;
            float maxDur = 0f, sumDur = 0f;
            for (int k = 0; k < n; k++)
            {
                var s = Racked(k);
                s.Pos[0] = new Vector2(PoolState.KitchenX, (float)(rng.NextDouble() - 0.5) * 0.6f);
                float ang = Mathf.Atan2(PoolState.FootSpot2.y - s.Pos[0].y, PoolState.FootSpot2.x - s.Pos[0].x) + (float)(rng.NextDouble() - 0.5) * 0.02f;
                var shot = new PoolShot(ang, 0.85f + (float)rng.NextDouble() * 0.15f, 0f, (float)(rng.NextDouble() - 0.5) * 0.4f);
                var r = PoolSim.Run(s.Pos, s.Down, shot);
                string why = Validate(s.Pos, s.Down, r);
                if (why != null) { bad++; if (bad < 6) Check($"break {k}", false, why); }
                int obj = 0;
                foreach (int b in r.Pocketed) if (b > 0) obj++;
                if (obj > 0) potted++;
                ballsDown += obj;
                if (r.Pocketed.Contains(0)) scratch++;
                if (r.Pocketed.Contains(8)) eight++;
                if (obj > 0 || r.BallsToRail >= 4) legal++;
                maxDur = Mathf.Max(maxDur, r.Duration);
                sumDur += r.Duration;
            }
            Check("breaks valid", bad == 0, $"{n - bad}/{n}");
            Check("breaks pot something often (>= 20 %)", potted >= n * 0.2f, $"potted {potted}/{n}, balls down avg {ballsDown / (float)n:0.00}, scratch {scratch}, 8 down {eight}, legal {legal}");
            Check("break durations sane", maxDur < 25f && sumDur / n > 2f, $"avg {sumDur / n:0.0} s, max {maxDur:0.0} s");
        }

        static void RandomShots()
        {
            var rng = new System.Random(777);
            int n = 0, bad = 0, potted = 0, capped = 0;
            float maxDur = 0f, sumDur = 0f;
            long ms = 0;
            var sw = new Stopwatch();
            for (int game = 0; game < 40; game++)
            {
                var s = Racked(1000 + game);
                for (int shotNo = 0; shotNo < 25; shotNo++)
                {
                    if (s.OnTable == 0) break;
                    if (s.IsDown(0))
                    {
                        s.Down = (ushort)(s.Down & ~1);
                        s.Pos[0] = PoolSim.ClampCueBall(s.Pos, s.Down, new Vector2(PoolState.KitchenX, 0f), false);
                    }
                    var shot = new PoolShot((float)(rng.NextDouble() * Math.PI * 2 - Math.PI), (float)rng.NextDouble(),
                        (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1));
                    if (shotNo == 0) shot.Power = 1f;
                    sw.Restart();
                    var r = PoolSim.Run(s.Pos, s.Down, shot);
                    ms = Math.Max(ms, sw.ElapsedMilliseconds);
                    n++;
                    string why = Validate(s.Pos, s.Down, r);
                    if (why != null) { bad++; if (bad < 8) Check($"game {game} shot {shotNo} ({shot.Angle:0.000},{shot.Power:0.00},{shot.SpinX:0.00},{shot.SpinY:0.00})", false, why); }
                    foreach (int b in r.Pocketed) if (b > 0) potted++;
                    if (r.Duration >= PoolSim.MaxTime - 0.1f) capped++;
                    maxDur = Mathf.Max(maxDur, r.Duration);
                    sumDur += r.Duration;
                    Array.Copy(r.FinalPos, s.Pos, 16);
                    s.Down = r.FinalDown;
                    s.Quantize();
                }
            }
            Check("random shots valid", bad == 0, $"{n - bad}/{n}");
            Check("random shot durations, sim cost", capped == 0 && ms < 400, $"avg {sumDur / n:0.0} s, max {maxDur:0.0} s, hit the cap {capped}, balls potted {potted}, slowest sim {ms} ms");
        }

        /// <summary>Object ball 15 cm in front of each pocket, cue ball 60 cm behind it on the same line: it must drop.</summary>
        static void StraightPots()
        {
            int ok = 0, total = 0;
            for (int pk = 0; pk < 6; pk++)
            {
                bool side = pk % 3 == 1;
                Vector2 hole = side ? new Vector2(0f, (pk < 3 ? -1 : 1) * (HalfW + 0.03f)) : new Vector2((pk % 3 - 1) * (HalfL + 0.02f), (pk < 3 ? -1 : 1) * (HalfW + 0.02f));
                Vector2 into = side ? new Vector2(0f, Mathf.Sign(hole.y)) : new Vector2(Mathf.Sign(hole.x), Mathf.Sign(hole.y)).normalized;
                foreach (float power in new[] { 0.2f, 0.45f, 0.75f })
                {
                    var pos = new Vector2[16];
                    ushort down = 0xFFFF & ~((1 << 0) | (1 << 3));
                    for (int i = 0; i < 16; i++) pos[i] = Vector2.zero;
                    Vector2 obj = hole - into * (side ? 0.2f : 0.25f);
                    pos[3] = obj;
                    pos[0] = obj - into * 0.6f;
                    var r = PoolSim.Run(pos, (ushort)down, new PoolShot(Mathf.Atan2(into.y, into.x), power, 0f, 0f));
                    total++;
                    bool potted = r.Pocketed.Contains(3);
                    if (potted) ok++;
                    else Check($"straight pot pocket {pk} power {power}", false, $"ball 3 ends at {r.FinalPos[3]}, first hit {r.FirstHit}");
                    Check($"straight pot pocket {pk} power {power} valid", Validate(pos, down, r) == null, Validate(pos, down, r) ?? "");
                }
            }
            Check("straight pots", ok == total, $"{ok}/{total}");
            // A slow ball along the cushion into the corner: still drops.
            var p2 = new Vector2[16];
            ushort d2 = (ushort)(0xFFFF & ~1);
            p2[0] = new Vector2(0.3f, FieldY - 0.0005f);
            var r2 = PoolSim.Run(p2, d2, new PoolShot(0f, 0.3f, 0f, 0f));
            Check("cue ball along the cushion into the corner", r2.Pocketed.Contains(0), $"ends {r2.FinalPos[0]}, {r2.Duration:0.0} s");
        }

        static void Determinism()
        {
            var s = Racked(42);
            var shot = new PoolShot(0.01f, 1f, 0.3f, -0.2f);
            var a = PoolSim.Run((Vector2[])s.Pos.Clone(), s.Down, shot);
            var b = PoolSim.Run((Vector2[])s.Pos.Clone(), s.Down, shot);
            bool same = a.Frames.Count == b.Frames.Count && a.FinalDown == b.FinalDown && a.Events.Count == b.Events.Count;
            for (int f = 0; same && f < a.Frames.Count; f++)
                for (int i = 0; i < 16; i++)
                    if (a.Frames[f][i] != b.Frames[f][i]) { same = false; break; }
            Check("determinism (two runs)", same, $"frames {a.Frames.Count}, events {a.Events.Count}");
            // Through the network text: the decoded state simulates to the very same result.
            s.Phase = PoolState.Playing;
            s.Note = "Rozbija Łukasz";
            var c = new PoolState();
            Check("decode for determinism", PoolState.TryDecode(s.Encode(), c));
            var d = PoolSim.Run(c.Pos, c.Down, shot);
            bool same2 = d.Frames.Count == a.Frames.Count && d.FinalDown == a.FinalDown;
            for (int i = 0; same2 && i < 16; i++) same2 = d.FinalPos[i] == a.FinalPos[i];
            Check("determinism after Encode/TryDecode", same2);
        }

        static void Codec()
        {
            var rng = new System.Random(5);
            int maxBytes = 0;
            bool all = true;
            string[] notes =
            {
                "", "Faul: biała w łuzie - bila z ręki dla Żółć", new string('ż', 300), "8 wbita z faulem (najpierw trafiona bila rywala) - Zażółćgęśląjaźń123456 wygrywa!",
                "a|b|c\n\u0001", "emoji \U0001F600 x",
            };
            for (int k = 0; k < 300; k++)
            {
                var s = Racked(k);
                s.Phase = (byte)(k % 3);
                s.A = k % 5 == 0 ? PoolState.Nobody : (ulong)rng.Next() * 977UL;
                s.B = (ulong)k;
                s.Turn = (byte)(k & 1);
                s.Groups = (byte)(k % 3);
                s.BallInHand = k % 2 == 0;
                s.Kitchen = k % 4 == 0;
                s.Break = k % 3 == 0;
                s.Winner = k % 7 == 0 ? (byte)1 : (byte)255;
                s.EndReason = (byte)(k % 5);
                s.WinsA = (byte)(k % 256);
                s.WinsB = 255;
                s.Round = k * 13;
                s.ShotNo = k * 1001;
                s.Rematch = (byte)(k % 4);
                s.Down = (ushort)rng.Next(0, 65536);
                for (int i = 0; i < 16; i++) s.Pos[i] = new Vector2((float)(rng.NextDouble() * 2 - 1) * FieldX, (float)(rng.NextDouble() * 2 - 1) * FieldY);
                s.Quantize();
                s.Note = notes[k % notes.Length];
                string t = s.Encode();
                int bytes = System.Text.Encoding.UTF8.GetByteCount(t);
                maxBytes = Math.Max(maxBytes, bytes);
                var o = new PoolState();
                bool ok = PoolState.TryDecode(t, o) && o.Encode() == t && o.Phase == s.Phase && o.A == s.A && o.B == s.B && o.Turn == s.Turn
                          && o.Groups == s.Groups && o.BallInHand == s.BallInHand && o.Kitchen == s.Kitchen && o.Break == s.Break && o.Winner == s.Winner
                          && o.EndReason == s.EndReason && o.WinsA == s.WinsA && o.WinsB == s.WinsB && o.Round == s.Round && o.ShotNo == s.ShotNo
                          && o.Rematch == s.Rematch && o.Down == s.Down;
                for (int i = 0; ok && i < 16; i++) ok = o.Pos[i] == s.Pos[i];
                if (!ok || bytes >= 480) { all = false; Check($"codec {k}", false, $"{bytes} B: {t}"); }
            }
            Check("codec round trip", all, $"max {maxBytes} B");
            Check("codec rejects garbage", !PoolState.TryDecode("P1|x", new PoolState()) && !PoolState.TryDecode("", new PoolState()) && !PoolState.TryDecode(null, new PoolState()));
        }
    }
}
