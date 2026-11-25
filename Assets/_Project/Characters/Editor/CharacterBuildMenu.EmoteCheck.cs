using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Emote check (batch, no -nographics): plays every emote at 60 fps in the lit studio (pair emotes with a partner
    /// at the emote distance, loops stopped after a while so the way out is checked too) and logs PASS / FAIL lines
    /// "[EmoteCheck]" for: contact gaps (<see cref="EmoteContacts"/>, |gap| &lt; 6 cm), interpenetration of the two
    /// bodies (box SAT, &lt; 5 cm), forearms through the own body, anything below the floor, sliding planted soles,
    /// durations. Writes a contact sheet per emote (key moments x 3 angles) to HNS_REC_OUT. HNS_EMOTES limits the list.
    /// </summary>
    public static partial class CharacterBuildMenu
    {
        const float CheckDt = 1f / 60f;
        const int SheetW = 300, SheetH = 260, SheetCols = 8;
        static readonly float[] SheetYaws = { 30f, 105f, 215f };

        public static void CheckEmotes()
        {
            bool ok = false;
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                recOut = Environment.GetEnvironmentVariable("HNS_REC_OUT");
                if (string.IsNullOrEmpty(recOut)) recOut = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Logs", "emotecheck");
                Directory.CreateDirectory(recOut);
                BuildAssets();
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildStudio(false);
                recCam = new GameObject("Check Camera").AddComponent<Camera>();
                ConfigureCamera(recCam);
                recCam.enabled = false;
                var list = new List<EmoteId>();
                string only = Environment.GetEnvironmentVariable("HNS_EMOTES");
                if (!string.IsNullOrEmpty(only))
                {
                    foreach (var n in only.Split(','))
                        if (Enum.TryParse<EmoteId>(n.Trim(), out var e) && Emotes.Get(e) != null) list.Add(e);
                }
                else { list.AddRange(Emotes.Solo); list.AddRange(Emotes.Pair); }
                int fails = 0;
                foreach (var id in list)
                {
                    try { fails += CheckEmote(id); }
                    catch (Exception e) { Debug.LogException(e); fails++; }
                }
                Debug.Log($"[EmoteCheck] all done: {list.Count} emotes, {fails} failed checks");
                ok = true;
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { ShaderUtil.allowAsyncCompilation = prevAsync; }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        sealed class Worst
        {
            public float V = float.NegativeInfinity, T;
            public string What = "-";
            public void Add(float v, float t, string what) { if (v > V) { V = v; T = t; What = what; } }
        }

        static int CheckEmote(EmoteId id)
        {
            var info = Emotes.Get(id);
            bool pair = info.IsPair;
            var a = SpawnAt(2, Vector3.zero, 0f, out _, out var aa);
            BlockyCharacter bc = null;
            BlockyCharacterAnimator ba = null;
            if (pair)
            {
                bc = SpawnAt(4, new Vector3(0f, 0f, info.PairDistance), 180f, out _, out ba);
                a.Actions.SetEmotePartner(bc, 0);
                bc.Actions.SetEmotePartner(a, 1);
            }
            var bodies = pair ? new[] { a, bc } : new[] { a };
            var anims = pair ? new[] { aa, ba } : new[] { aa };
            TickAll(0.5f, anims);
            // Floor baseline: the idle stance (block corners of the feet already dip a little).
            var idle = new PartBox[BodyBoxes.Count];
            Boxes(a, idle);
            float floorBase = 0f;
            foreach (var bx0 in idle) floorBase = Mathf.Max(floorBase, Depth(bx0));
            foreach (var c in bodies) c.Actions.PlayEmote(id, 0f);

            float stopAt = info.Loops ? (info.Standing ? Mathf.Max(6f, info.Period * 2f) : 11f) : 999f;
            float len = info.Loops ? stopAt + Mathf.Max(info.Exit, info.Out) + 0.3f : info.Duration + 0.1f;
            var contacts = EmoteContacts.For(id);
            int nextContact = 0;
            Array.Sort(contacts, (x, y) => x.T.CompareTo(y.T));

            // Sheet moments: contacts first, then evenly spread.
            var shots = new List<float>();
            foreach (var k in contacts) if (shots.Count < 4 && !shots.Contains(k.T)) shots.Add(k.T);
            shots.Add(info.ThumbTime);
            int need = SheetCols - shots.Count;
            for (int i = 0; i < need; i++) shots.Add(len * (i + 0.5f) / need);
            shots.Sort();
            var sheet = new Texture2D(SheetW * SheetCols, SheetH * SheetYaws.Length, TextureFormat.RGBA32, false);
            int shot = 0;

            var buf = new PartBox[BodyBoxes.Count];
            var boxesA = new PartBox[BodyBoxes.Count];
            var boxesB = new PartBox[BodyBoxes.Count];
            var pen = new Worst();
            var armPen = new Worst();
            var penBySec = new Dictionary<int, Worst>();
            var self = new Worst();
            var floor = new Worst();
            var slide = new Worst();
            var soles = new Vector3[4];
            var slideRun = new float[4];
            var log = new StringBuilder();
            int fails = 0;
            bool stopped = false;
            float t = 0f;
            Vector3 mid = pair ? new Vector3(0f, 1.0f, info.PairDistance * 0.5f) : new Vector3(0f, info.Standing ? 1.0f : 0.6f, 0f);
            while (t < len)
            {
                float dt = CheckDt;
                if (nextContact < contacts.Length && contacts[nextContact].T > t && contacts[nextContact].T < t + dt) dt = contacts[nextContact].T - t;
                if (shot < shots.Count && shots[shot] > t && shots[shot] < t + dt) dt = Mathf.Min(dt, shots[shot] - t);
                if (!stopped && t + dt >= stopAt) { stopped = true; foreach (var c in bodies) c.Actions.StopEmote(); }
                foreach (var an in anims) an.Tick(dt);
                t += dt;

                // Bodies: partner overlap, forearms through the own body, floor.
                Boxes(a, boxesA);
                if (pair) Boxes(bc, boxesB);
                for (int i = 0; i < BodyBoxes.Count && pair; i++)
                for (int j = 0; j < BodyBoxes.Count; j++)
                {
                    float d = BodyBoxes.Penetration(boxesA[i], boxesB[j]);
                    bool arm = IsForearm(i) || IsForearm(j);
                    (arm ? armPen : pen).Add(d, t, $"{(BodyPart)i}/{(BodyPart)j}");
                    if (arm) continue;
                    int sec = Mathf.FloorToInt(t);
                    if (!penBySec.TryGetValue(sec, out var w)) penBySec[sec] = w = new Worst();
                    w.Add(d, t, $"{(BodyPart)i}/{(BodyPart)j}");
                }
                for (int bI = 0; bI < bodies.Length; bI++)
                {
                    var bx = bI == 0 ? boxesA : boxesB;
                    foreach (int fa in new[] { 3, 5 })
                    foreach (int part in new[] { 0, 1 })
                        self.Add(BodyBoxes.Penetration(bx[fa].Inflated(-0.02f), bx[part]), t, $"b{bI} {(BodyPart)fa}/{(BodyPart)part}");
                    for (int i = 0; i < BodyBoxes.Count; i++) floor.Add(Depth(bx[i]) - floorBase, t, $"b{bI} {(BodyPart)i}");
                    // Soles: sliding while planted (both frames on the floor).
                    var pose = CharacterPose.Capture(bodies[bI]);
                    for (int leg = 0; leg < 2; leg++)
                    {
                        int s = bI * 2 + leg;
                        Vector3 now = bodies[bI].transform.TransformPoint(BodyBoxes.SolePos(pose, leg));
                        bool planted = now.y < 0.02f && soles[s].y < 0.02f && t > 0.05f;
                        if (planted) slideRun[s] += new Vector2(now.x - soles[s].x, now.z - soles[s].z).magnitude;
                        else slideRun[s] = 0f;
                        if (bodies[bI].Actions.EmoteWeight > 0.98f) slide.Add(slideRun[s], t, $"b{bI} sole{leg}");
                        soles[s] = now;
                    }
                }

                while (nextContact < contacts.Length && contacts[nextContact].T <= t + 1e-4f)
                {
                    var k = contacts[nextContact++];
                    float g = EmoteContacts.Gap(k, a, bc, buf);
                    bool pass = Mathf.Abs(g) < 0.06f;
                    if (!pass) fails++;
                    log.AppendLine($"[EmoteCheck] {id} contact '{k.Label}' t={k.T:0.00} gap={g * 100f:0.0} cm {(pass ? "PASS" : "FAIL")}");
                }
                if (shot < shots.Count && shots[shot] <= t + 1e-4f)
                {
                    for (int r = 0; r < SheetYaws.Length; r++)
                    {
                        float yaw = SheetYaws[r] + (pair ? 60f : 0f);
                        Orbit(mid, yaw, pair ? 4.2f : 3.6f, pair ? 0.5f : 0.35f, 38f);
                        var tile = Render(recCam, SheetW, SheetH);
                        sheet.SetPixels(shot * SheetW, (SheetYaws.Length - 1 - r) * SheetH, SheetW, SheetH, tile.GetPixels());
                        UnityEngine.Object.DestroyImmediate(tile);
                    }
                    shot++;
                }
            }
            fails += Line(log, id, "overlap", pen, 0.05f, pair);
            fails += Line(log, id, "arm-overlap", armPen, 0.09f, pair);
            if (pair)
            {
                var sb = new StringBuilder($"[EmoteCheck] {id} overlap per second:");
                foreach (var kv in penBySec) if (kv.Value.V > 0.01f) sb.Append($" {kv.Key}s={kv.Value.V * 100f:0}cm({kv.Value.What})");
                log.AppendLine(sb.ToString());
            }
            fails += Line(log, id, "forearm-in-body", self, 0.05f, true);
            fails += Line(log, id, "below-floor", floor, 0.03f, true);
            fails += Line(log, id, "sole-slide", slide, 0.03f, true);
            float dur = info.Loops ? info.Period : info.Duration;
            log.AppendLine($"[EmoteCheck] {id} duration {(info.Loops ? "loop period" : "one-shot")} {dur:0.00} s, exit {info.Exit:0.00} s, simulated {len:0.00} s, sheet times {string.Join(" ", shots.ConvertAll(x => x.ToString("0.00")))}");
            Debug.Log(log.ToString());
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(recOut, $"sheet_{id}.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Kill(a);
            if (bc != null) Kill(bc);
            return fails;
        }

        /// <summary>
        /// How deep a part is below the floor: torso / head by the lowest corner, limbs by the bone axis end plus the
        /// thickness of the block turned down (a tilted foot block always dips a little at the corner).
        /// </summary>
        static float Depth(in PartBox b)
        {
            if (b.Part == BodyPart.Torso || b.Part == BodyPart.Head) return -(b.Center.y - BodyBoxes.Radius(b, Vector3.up));
            float ay = b.Axis(1).y;
            return -(b.Center.y - Mathf.Abs(ay) * b.Half.y - b.Half.x * Mathf.Sqrt(Mathf.Max(0f, 1f - ay * ay)));
        }

        static bool IsForearm(int part) => part == (int)BodyPart.ForearmR || part == (int)BodyPart.ForearmL;

        static void Boxes(BlockyCharacter c, PartBox[] into)
        {
            BodyBoxes.Get(CharacterPose.Capture(c), into);
            for (int i = 0; i < into.Length; i++) into[i] = into[i].Transformed(c.transform.position, c.transform.rotation);
        }

        static int Line(StringBuilder log, EmoteId id, string what, Worst w, float limit, bool used)
        {
            if (!used) return 0;
            bool pass = w.V < limit;
            log.AppendLine($"[EmoteCheck] {id} {what} max={w.V * 100f:0.0} cm at t={w.T:0.00} ({w.What}) {(pass ? "PASS" : "FAIL")}");
            return pass ? 0 : 1;
        }
    }
}
