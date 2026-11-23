using System;
using System.IO;
using HideAndSeek.Characters;
using HideAndSeek.Game.MiniGames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Preview renders of the board-game tables inside the real lobby (batchmode, no exe needed):
    /// -executeMethod HideAndSeek.Game.Editor.MiniGameStudio.RenderFromCommandLine -mgout &lt;dir&gt;.
    /// Opens Lobby.unity without saving, builds both styles of both tables, sets a middle-game position, renders
    /// close-ups, piece line-ups and a top view of the room (placement / collisions check).
    /// </summary>
    public static class MiniGameStudio
    {
        const string Fen = "r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/3P1N2/PPP2PPP/RNBQK2R w KQkq - 0 5";

        public static void RenderFromCommandLine()
        {
            int code = 0;
            try { Render(Arg("-mgout", "Logs/shots/models")); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        static string Arg(string name, string def)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
        }

        static Material FindTemplate()
        {
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
                if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name == "Standard") return r.sharedMaterial;
            return new Material(Shader.Find("Standard"));
        }

        static void Render(string outDir)
        {
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(MultiplayerBuilder.LobbyScenePath, OpenSceneMode.Single);
            var template = FindTemplate();

            // Placement in the lobby (same as MiniGameSpots fallback) + the alternative styles next to them for the preview.
            var chess = MiniGameSpots.Fallback(MiniGameSpots.ChessName);
            var ttt = MiniGameSpots.Fallback(MiniGameSpots.TttName);
            MiniGameSpots.Resolve(MiniGameSpots.ChessName, out var cPos, out var cRot, out _);
            var chessA = Table("ChessA", cPos, cRot.eulerAngles.y);
            var cm = ChessTableModel.Build(chessA, template, 0);
            Setup(cm, Fen);
            var tttA = Table("TttA", ttt.pos, ttt.yaw);
            var tm = TttTableModel.Build(tttA, template);
            DrawSample(tm);

            // Studio copies (out of the room, own light) for the style comparison.
            var studio = new GameObject("Studio").transform;
            studio.position = new Vector3(40f, 0f, 0f);
            Floor(studio, template);
            var c0 = ChessTableModel.Build(Table("Chess0", studio.position + new Vector3(-2f, 0f, 0f), 0f, studio), template, 0);
            var c1 = ChessTableModel.Build(Table("Chess1", studio.position + new Vector3(2f, 0f, 0f), 0f, studio), template, 1);
            Setup(c0, Fen);
            Setup(c1, Fen);
            // Wall board in the studio: a plain wall behind it.
            var t0 = TttTableModel.Build(Table("Ttt0", studio.position + new Vector3(0f, MiniGameSpots.BoardHeight, 4.6f), 180f, studio), template);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(studio, false);
            wall.transform.localPosition = new Vector3(0f, 1.6f, 4.7f);
            wall.transform.localScale = new Vector3(6f, 3.2f, 0.2f);
            wall.GetComponent<Renderer>().sharedMaterial = new Material(template) { color = new Color(0.45f, 0.4f, 0.35f) };
            DrawSample(t0);
            Lights(studio);
            // Piece line-up.
            var lineup = new GameObject("Lineup").transform;
            lineup.position = studio.position + new Vector3(0f, 0f, -3f);
            int[] order = { 1, 4, 2, 3, 5, 6 };
            for (int i = 0; i < 6; i++)
            {
                var w = c0.Piece(order[i], lineup); w.transform.localPosition = new Vector3((i - 2.5f) * 0.09f, 0f, -0.05f);
                var b = c0.Piece(-order[i], lineup); b.transform.localPosition = new Vector3((i - 2.5f) * 0.09f, 0f, 0.07f);
                var w1 = c1.Piece(order[i], lineup); w1.transform.localPosition = new Vector3((i - 2.5f) * 0.09f + 0.7f, 0f, -0.05f);
                var b1 = c1.Piece(-order[i], lineup); b1.transform.localPosition = new Vector3((i - 2.5f) * 0.09f + 0.7f, 0f, 0.07f);
            }
            var knightSide = c0.Piece(2, lineup); knightSide.transform.localPosition = new Vector3(-0.45f, 0f, 0f); knightSide.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var knightSideB = c0.Piece(-2, lineup); knightSideB.transform.localPosition = new Vector3(-0.55f, 0f, 0f); knightSideB.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // A character for scale at the lobby table.
            var person = new GameObject("Person").transform;
            person.position = chessA.TransformPoint(new Vector3(0.75f, 0f, -0.3f));
            person.rotation = chessA.rotation * Quaternion.Euler(0f, -60f, 0f);
            BlockyCharacterFactory.Create(person, 1, "Test");

            // Seated players (pure pose function, the same one the game uses).
            var seatedA = Seated(chessA, 0, 2, 0.15f, null, 0f);
            var seatedB = Seated(chessA, 1, 3, 1f, null, 0f);
            var reachFar = Seated(c0.Root, 0, 0, 0f, c0.BoardPivot.TransformPoint(ChessTableModel.SquareLocal(ChessBoard.ParseSq("d7")) + Vector3.up * 0.16f), 1f);
            var reachNear = Seated(c0.Root, 1, 4, 0f, c0.BoardPivot.TransformPoint(ChessTableModel.SquareLocal(ChessBoard.ParseSq("g8")) + Vector3.up * 0.14f), 1f);
            var reachSide = Seated(c1.Root, 0, 1, 0f, c1.BoardPivot.TransformPoint(ChessTableModel.SquareLocal(ChessBoard.ParseSq("h5")) + Vector3.up * 0.14f), 1f);
            var thinkB = Seated(c1.Root, 1, 2, 1f, null, 0f);
            var drawer = StandingAt(t0.Root, 0, 2, TttTableModel.CellLocal(2), 1f);
            var thinker = StandingAt(t0.Root, 1, 3, Vector3.zero, 0f);
            _ = seatedA; _ = seatedB; _ = reachFar; _ = reachNear; _ = reachSide; _ = thinkB; _ = drawer; _ = thinker;

            var cam = new GameObject("StudioCam").AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 40f;
            cam.allowHDR = true;
            var s = studio.position;
            Vector3 C0 = s + new Vector3(-2f, 0f, 0f), C1 = s + new Vector3(2f, 0f, 0f), T0 = s;
            Shot(cam, outDir, "chess_styles", s + new Vector3(0f, 2.2f, -2.6f), s + new Vector3(0f, 0.6f, 0f), 50f);
            Shot(cam, outDir, "chess0_close", C0 + new Vector3(0f, 1.05f, -0.62f), C0 + new Vector3(0f, 0.67f, 0.02f), 40f);
            Shot(cam, outDir, "chess1_close", C1 + new Vector3(0f, 1.05f, -0.62f), C1 + new Vector3(0f, 0.67f, 0.02f), 40f);
            Shot(cam, outDir, "pieces", lineup.position + new Vector3(0.1f, 0.22f, -0.42f), lineup.position + new Vector3(0.1f, 0.05f, 0f), 42f);
            Shot(cam, outDir, "knight", lineup.position + new Vector3(-0.5f, 0.09f, -0.22f), lineup.position + new Vector3(-0.5f, 0.05f, 0f), 30f);
            Vector3 B0 = s + new Vector3(0f, MiniGameSpots.BoardHeight, 4.6f);
            Shot(cam, outDir, "ttt_board", B0 + new Vector3(0.2f, 0.25f, -2.4f), B0 + new Vector3(0f, -0.25f, 0f), 45f);
            Shot(cam, outDir, "ttt_close", B0 + new Vector3(0f, 0.1f, -0.75f), B0 + new Vector3(0f, -0.05f, 0f), 55f);
            Shot(cam, outDir, "ttt_players", B0 + new Vector3(1.9f, 0.2f, -2.8f), B0 + new Vector3(0f, -0.4f, -0.4f), 50f);
            Shot(cam, outDir, "ttt_side", B0 + new Vector3(2.2f, 0.1f, -0.9f), B0 + new Vector3(0f, -0.35f, -0.4f), 50f);
            Shot(cam, outDir, "pose_side_c0", C0 + new Vector3(2.1f, 1.0f, 0f), C0 + new Vector3(0f, 0.75f, 0f), 45f);
            Shot(cam, outDir, "pose_34_c0", C0 + new Vector3(-1.6f, 1.6f, -1.8f), C0 + new Vector3(0f, 0.7f, 0f), 45f);
            Shot(cam, outDir, "pose_side_c1", C1 + new Vector3(2.1f, 1.0f, 0.1f), C1 + new Vector3(0f, 0.75f, 0f), 45f);
            Shot(cam, outDir, "pose_34_c1", C1 + new Vector3(1.6f, 1.6f, 1.8f), C1 + new Vector3(0f, 0.7f, 0f), 45f);
            Shot(cam, outDir, "pose_lobby", chessA.TransformPoint(new Vector3(2.0f, 1.6f, -1.4f)), chessA.TransformPoint(new Vector3(0f, 0.7f, 0f)), 50f);
            // Board view of seat A (what the player sees; in the game the own torso and head are hidden).
            foreach (var r in reachFar.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(reachFar.ShoulderRight) && !r.transform.IsChildOf(reachFar.ShoulderLeft))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            Shot(cam, outDir, "view_seatA", c0.Root.TransformPoint(new Vector3(0f, ChessTableModel.BoardTop + 0.78f, -0.52f)), c0.Root.TransformPoint(new Vector3(0f, ChessTableModel.BoardTop, -0.035f)), 60f);
            // In the lobby: both tables from the room, and a top view of the whole room.
            Shot(cam, outDir, "lobby_chess", chessA.TransformPoint(new Vector3(-1.9f, 1.7f, -1.6f)), chessA.TransformPoint(new Vector3(0f, 0.6f, 0f)), 55f);
            Shot(cam, outDir, "lobby_ttt_close", tttA.TransformPoint(new Vector3(0.25f, 0.05f, 1.05f)), tttA.TransformPoint(new Vector3(0f, -0.08f, 0f)), 50f);
            Shot(cam, outDir, "lobby_ttt_ledge", tttA.TransformPoint(new Vector3(-0.35f, -0.25f, 0.45f)), tttA.TransformPoint(new Vector3(-0.1f, -0.42f, 0.03f)), 50f);
            Shot(cam, outDir, "lobby_ttt", tttA.TransformPoint(new Vector3(1.6f, 0.3f, 2.8f)), tttA.TransformPoint(new Vector3(0f, -0.3f, 0f)), 55f);
            cam.orthographic = true;
            cam.orthographicSize = 6.3f;
            Shot(cam, outDir, "lobby_top", new Vector3(0f, 3.45f, 0f), new Vector3(0f, 0f, 0.0001f), 60f, Vector3.forward);
            Debug.Log($"[MiniGameStudio] rendered to {Path.GetFullPath(outDir)}");
        }

        /// <summary>A character sitting on a stool of a table, posed by <see cref="BoardGamePose"/>.</summary>
        static BlockyCharacter Seated(Transform table, int seat, int skin, float think, Vector3? reach, float reachW)
        {
            float side = seat == 0 ? -1f : 1f;
            var root = new GameObject("Seated").transform;
            float stoolZ = ChessTableModel.StoolZ, half = ChessTableModel.TableTop * 0.5f;
            root.position = table.TransformPoint(new Vector3(0f, 0f, side * stoolZ));
            root.rotation = table.rotation * Quaternion.Euler(0f, seat == 0 ? 0f : 180f, 0f);
            var c = BlockyCharacterFactory.Create(root, skin, "P" + skin);
            var t = c.transform;
            Vector3 rest = table.TransformPoint(new Vector3(-side * 0.17f, ChessTableModel.TopY + 0.05f, side * (half - 0.12f)));
            var input = new BoardGamePose.Input
            {
                SeatTop = t.InverseTransformPoint(table.TransformPoint(new Vector3(0f, TableKit.StoolHeight + 0.034f, side * stoolZ))),
                TableEdge = t.InverseTransformPoint(table.TransformPoint(new Vector3(0f, ChessTableModel.TopY, side * half))),
                FootRestY = TableKit.FootRing + 0.015f,
                Reach = t.InverseTransformPoint(reach ?? rest),
                ReachW = reachW,
                Think = think,
                Look = t.InverseTransformPoint(reach ?? table.TransformPoint(new Vector3(0f, 0.77f, 0f))),
                Seed = skin,
            };
            BoardGamePose.Compute(input, 1.3f + skin).Apply(c);
            return c;
        }

        /// <summary>A character standing at the wall board (standing variant of <see cref="BoardGamePose"/>), drawing in a cell or thinking.</summary>
        static BlockyCharacter StandingAt(Transform board, int seat, int skin, Vector3 cellLocal, float reachW)
        {
            var root = new GameObject("Standing").transform;
            root.position = board.TransformPoint(new Vector3(seat == 0 ? 0.45f : -0.45f, TttTableModel.FloorY, 0.85f));
            root.rotation = board.rotation * Quaternion.Euler(0f, 180f, 0f);
            var c = BlockyCharacterFactory.Create(root, skin, "S" + skin);
            var t = c.transform;
            Vector3 tip = cellLocal + new Vector3(-0.05f, 0.05f, 0f);
            Vector3 fist = tip + new Vector3(0f, -0.55f, 0.83f).normalized * 0.115f;
            Vector3 step = board.TransformPoint(new Vector3(Mathf.Clamp(cellLocal.x + (seat == 0 ? 0.12f : -0.12f), -0.4f, 0.4f), TttTableModel.FloorY, 0.42f));
            Vector3 shift = t.InverseTransformPoint(step);
            shift.y = 0f;
            var input = new BoardGamePose.Input
            {
                Standing = true,
                Shift = Vector3.ClampMagnitude(shift, 0.7f),
                Reach = t.InverseTransformPoint(board.TransformPoint(fist)),
                ReachW = reachW,
                Think = reachW > 0f ? 0f : 1f,
                Look = t.InverseTransformPoint(board.TransformPoint(reachW > 0f ? tip : new Vector3(0f, TttTableModel.GridY, 0f))),
                Seed = skin,
            };
            BoardGamePose.Compute(input, 2f + skin).Apply(c);
            return c;
        }

        static Transform Table(string name, Vector3 pos, float yaw, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, true);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static void Setup(ChessTableModel m, string fen)
        {
            var b = new ChessBoard();
            b.SetFen(fen);
            for (int sq = 0; sq < 64; sq++)
            {
                if (b.Sq[sq] == 0) continue;
                var p = m.Piece(b.Sq[sq], m.Pieces);
                p.transform.localPosition = ChessTableModel.SquareLocal(sq);
                p.transform.localRotation = Quaternion.Euler(0f, ChessTableModel.PieceYaw(b.Sq[sq], ChessBoard.File(sq) < 4), 0f);
            }
            // A couple of captured pieces next to the board.
            m.Piece(-1, m.Pieces).transform.localPosition = ChessTableModel.CapturedLocal(0, 0);
            m.Piece(1, m.Pieces).transform.localPosition = ChessTableModel.CapturedLocal(1, 0);
            ChessClockFace.Draw(m.ClockTex, 271000, 288000, true, true);
        }

        static void DrawSample(TttTableModel m)
        {
            TttChalk.DrawMark(m, 4, 'X', 1f, 1);
            TttChalk.DrawMark(m, 0, 'O', 1f, 2);
            TttChalk.DrawMark(m, 2, 'X', 1f, 3);
            TttChalk.DrawMark(m, 6, 'O', 1f, 4);
            m.SlateTex.Apply(true);
        }

        static void Floor(Transform studio, Material template)
        {
            var f = GameObject.CreatePrimitive(PrimitiveType.Plane);
            f.transform.SetParent(studio, false);
            f.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            var m = new Material(template) { color = new Color(0.55f, 0.42f, 0.3f) };
            f.GetComponent<Renderer>().sharedMaterial = m;
        }

        static void Lights(Transform studio)
        {
            void L(Vector3 p, float i, float range, bool shadows)
            {
                var l = new GameObject("StudioLight").AddComponent<Light>();
                l.transform.SetParent(studio, false);
                l.transform.localPosition = p;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.82f, 0.6f);
                l.intensity = i;
                l.range = range;
                l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            }
            L(new Vector3(0f, 3.2f, 0f), 1.4f, 8f, true);
            L(new Vector3(0f, 3.2f, 3f), 1.4f, 8f, true);
            L(new Vector3(0f, 3.2f, -3f), 1.6f, 8f, true);
            L(new Vector3(-2.5f, 1.8f, -2f), 0.5f, 6f, false);
        }

        static void Shot(Camera cam, string dir, string name, Vector3 from, Vector3 at, float fov, Vector3? up = null)
        {
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(at - from, up ?? Vector3.up);
            cam.fieldOfView = fov;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
