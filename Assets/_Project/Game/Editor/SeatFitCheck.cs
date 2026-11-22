using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HideAndSeek.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BodyPart = HideAndSeek.Characters.BodyPart;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Geometry check of the lobby seated poses against the real scene (no play mode, nothing rendered, the scene is
    /// not saved): every seat x every pose, the rig's part boxes (outer clothing layer and breathing included) are
    /// tested against the triangles of every renderer around (separating axis test, penetration depth), plus:
    /// the head over the whole look range (TPP), the first-person eye's clearance from the own body and the
    /// furniture, forearms vs the own legs / torso, bodies on neighbouring seats against each other (any pose pair),
    /// and how high the feet hang. The old (pre-fit) poses are checked too, for comparison. Only the poses a seat offers
    /// are checked (<see cref="SeatPoses.Supports"/>); the pin-up poses also over their idle motion (breathing, swinging
    /// foot: several moments of <see cref="SeatPoses.IdlePeriod"/>), legs against each other, and the head against the
    /// own limbs over the look range. Poses lying along the sofa (<see cref="SeatPoses.SpansNeighbour"/>) are not paired
    /// with the other seat of the same sofa (it must be free then). Also writes Logs/seatposes_views.txt: side / front /
    /// top views of every pin-up pose as text (geometry only, nothing rendered).
    /// Batch: -executeMethod HideAndSeek.Game.Editor.SeatFitCheck.RunLobby  (report: Logs/seatfit.txt)
    /// Menu: Hide and Seek/Lobby/Seat fit check
    /// </summary>
    public static class SeatFitCheck
    {
        // Penetration up to 1.5 cm is accepted (soft cushions give: the body must visibly rest on the furniture,
        // touching beats hovering); deeper is a problem.
        const float Tolerance = 0.015f;
        const float ContactMax = 0.012f;     // a resting body further than this from the furniture hovers
        const float EyeBodyMin = 0.07f;     // near clip 0.03 + the near plane's half diagonal + margin
        const float EyeFurnitureMin = 0.08f;
        const float SelfAllow = 0.015f;     // forearm sinking into the own clothes (both have a 1.4 cm layer)

        class Mesh3
        {
            public MeshRenderer R;
            public string Name;
            public Vector3[] V;
            public int[] T;
            public Bounds B;
        }

        struct Hit
        {
            public string Part, What;
            public float Depth;
        }

        [MenuItem("Hide and Seek/Lobby/Seat fit check")]
        public static void RunLobby()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MultiplayerBuilder.LobbyScenePath, OpenSceneMode.Single);
            var meshes = CollectMeshes();
            var seats = Object.FindObjectsByType<LobbySeat>(FindObjectsInactive.Exclude).OrderBy(s => s.name, System.StringComparer.Ordinal).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"Seat fit check {System.DateTime.Now:yyyy-MM-dd HH:mm}  scene {MultiplayerBuilder.LobbyScenePath}");
            sb.AppendLine($"{seats.Count} seats, {meshes.Count} renderers. All skins share one rig (BlockyMeshBuilder), so one body covers every skin.");
            sb.AppendLine();
            ValidateShapes(seats, meshes, sb);
            sb.AppendLine();
            // Solve afresh (not from the baked file), then bake the result for the runtime.
            var trace = new StringBuilder();
            SeatPoses.UseBaked = false;
            SeatPoses.ClearCache();
            SeatPoses.Trace = trace;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var seat in seats)
                for (int v = 0; v < SeatPoses.Count; v++) SeatPoses.Body(v, seat.Shape);
            SeatPoses.Trace = null;
            sb.AppendLine($"Solving every pose for every seat shape: {sw.ElapsedMilliseconds} ms (editor only; the game loads the baked result)");
            sb.AppendLine(WriteBaked(seats));
            sb.AppendLine();
            var before = Check(seats, meshes, true, sb);
            sb.AppendLine();
            var after = Check(seats, meshes, false, sb);
            sb.AppendLine();
            sb.AppendLine($"SUMMARY before: {before.hits} intersections (max {before.max * 100f:0.0} cm), after: {after.hits} intersections (max {after.max * 100f:0.0} cm), " +
                          $"eye problems after: {after.eye}, neighbour overlaps before/after: {before.neighbours}/{after.neighbours}");
            sb.AppendLine();
            sb.AppendLine("=== Solver trace ===");
            sb.Append(trace);
            WriteViews(seats);
            SeatPoses.UseBaked = true;
            SeatPoses.ClearCache();
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "seatfit.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            foreach (var line in sb.ToString().Split('\n')) if (line.Trim().Length > 0) Debug.Log("[SeatFit] " + line.TrimEnd());
            Debug.Log($"[SeatFit] report: {path}");
        }

        // ================================================================== per pose set

        static (int hits, float max, int eye, int neighbours) Check(List<LobbySeat> seats, List<Mesh3> meshes, bool legacy, StringBuilder sb)
        {
            sb.AppendLine(legacy ? "=== BEFORE (old poses: pelvis 0.5 m for every seat) ===" : "=== AFTER (poses fitted to the seat) ===");
            int total = 0, eyeIssues = 0, nb = 0;
            float max = 0f;
            var boxes = new PartBox[BodyBoxes.Count];
            foreach (var seat in seats)
            {
                var shape = seat.Shape;
                var t = seat.transform;
                string where = $"{seat.name} ({Kind(seat)})";
                for (int v = 0; v < SeatPoses.Count; v++)
                {
                    if (legacy ? v >= SeatPoses.RandomCount : !SeatPoses.Supports(shape.MirrorSource ?? shape, v)) continue;
                    var lim0 = SeatPoses.Limits(v, shape);
                    var pose = legacy ? Legacy(v) : SeatPoses.Evaluate(v, shape, lim0.Center, lim0.Pitch);
                    var hits = new List<Hit>();
                    BodyBoxes.Get(pose, boxes);
                    for (int i = 0; i < boxes.Length; i++)
                        Furniture(World(Breath(boxes[i]), t), meshes, hits);
                    // Idle motion (pin-up poses): the body at several moments of the cycle.
                    var idleSelf = new List<string>();
                    if (!legacy && v >= SeatPoses.RandomCount)
                    {
                        var ib = new PartBox[BodyBoxes.Count];
                        for (int k = 1; k < IdleSamples; k++)
                        {
                            float time = k * SeatPoses.IdlePeriod / IdleSamples;
                            var ip = SeatPoses.Evaluate(v, shape, lim0.Center, lim0.Pitch, time);
                            BodyBoxes.Get(ip, ib);
                            var ih = new List<Hit>();
                            for (int i = 0; i < ib.Length; i++)
                                if (i != (int)BodyPart.Head) Furniture(World(Breath(ib[i]), t), meshes, ih);
                            foreach (var h in ih) hits.Add(new Hit { Part = h.Part + " (idle)", What = h.What, Depth = h.Depth });
                            float lc = SeatPoses.LegCross(ip, ib, out string lw);
                            if (lc > 0f) idleSelf.Add($"legs (idle t {time:0.0}): {lw} {lc * 100f:0.0} cm");
                            BodyBoxes.Get(ip, ib);
                            foreach (int a in new[] { (int)BodyPart.ForearmR, (int)BodyPart.ForearmL })
                            foreach (int b in new[] { (int)BodyPart.ThighR, (int)BodyPart.ThighL, (int)BodyPart.ShinR, (int)BodyPart.ShinL, (int)BodyPart.Torso })
                            {
                                float d = BodyBoxes.Penetration(ib[a], ib[b]);
                                if (d > SelfAllow) idleSelf.Add($"{(BodyPart)a}x{(BodyPart)b} (idle t {time:0.0}) {d * 100f:0.0} cm");
                            }
                        }
                    }

                    // Head over the whole look range (what other players see).
                    var lim = SeatPoses.Limits(v, shape);
                    var headSelf = new Dictionary<string, float>();
                    var headAt = new Dictionary<string, string>();
                    if (!legacy)
                    {
                        foreach (var (yaw, pitch) in LookGrid(lim))
                        {
                            var hp = SeatPoses.Evaluate(v, shape, yaw, pitch);
                            BodyBoxes.Get(hp, boxes);
                            var hh = new List<Hit>();
                            Furniture(World(boxes[(int)BodyPart.Head], t), meshes, hh);
                            foreach (var h in hh) hits.Add(new Hit { Part = $"Head(yaw {yaw:0} pitch {pitch:0})", What = h.What, Depth = h.Depth });
                            // The head against the own arms / legs (a hand in the hair, the chin on the fists).
                            if (v >= SeatPoses.RandomCount) // pin-up poses (the original four predate this check)
                            foreach (int b in SeatPoses.HeadVs)
                            {
                                float d = BodyBoxes.Penetration(boxes[(int)BodyPart.Head], boxes[b]);
                                string key = $"Head x {(BodyPart)b}";
                                if (d > SelfAllow && (!headSelf.TryGetValue(key, out float o) || d > o)) { headSelf[key] = d; headAt[key] = $"yaw {yaw:0} pitch {pitch:0}"; }
                            }
                        }
                        BodyBoxes.Get(pose, boxes);
                    }

                    // Forearms vs the own legs / torso.
                    var self = new List<string>();
                    foreach (int a in new[] { (int)BodyPart.ForearmR, (int)BodyPart.ForearmL, (int)BodyPart.UpperArmR, (int)BodyPart.UpperArmL })
                    foreach (int b in new[] { (int)BodyPart.ThighR, (int)BodyPart.ThighL, (int)BodyPart.ShinR, (int)BodyPart.ShinL, (int)BodyPart.Torso })
                    {
                        bool upperTorso = (a == (int)BodyPart.UpperArmR || a == (int)BodyPart.UpperArmL) && b == (int)BodyPart.Torso;
                        if (upperTorso) continue; // joined at the shoulder
                        float d = BodyBoxes.Penetration(boxes[a], boxes[b]);
                        if (d > SelfAllow) self.Add($"{(BodyPart)a}x{(BodyPart)b} {d * 100f:0.0} cm");
                    }
                    if (!legacy)
                    {
                        float lc = SeatPoses.LegCross(pose, new PartBox[BodyBoxes.Count], out string lw);
                        if (lc > 0f) self.Add($"legs: {lw} {lc * 100f:0.0} cm");
                        foreach (var kv in headSelf) self.Add($"{kv.Key} over the look range {kv.Value * 100f:0.0} cm (at {headAt[kv.Key]})");
                        self.AddRange(idleSelf);
                    }

                    // Feet: lowest point of each shin.
                    float soleR = Lowest(World(boxes[(int)BodyPart.ShinR], t)), soleL = Lowest(World(boxes[(int)BodyPart.ShinL], t));
                    float floor = FloorBelow(meshes, t.TransformPoint(new Vector3(0f, 0f, 0.3f)));
                    // Contact: how far the body (torso and legs) is from the furniture it rests on (seat meshes only).
                    float gap = float.MaxValue;
                    string gapAt = "";
                    if (!legacy)
                    {
                        foreach (int part in new[] { (int)BodyPart.Torso, (int)BodyPart.ThighR, (int)BodyPart.ThighL, (int)BodyPart.ShinR, (int)BodyPart.ShinL })
                        {
                            var wb = World(boxes[part], t);
                            for (int u = -1; u <= 1; u++)
                            for (int w2 = -1; w2 <= 1; w2++)
                            for (int k = -1; k <= 1; k++)
                            {
                                float d = FurnitureDistance(wb.Point(u, w2, k), meshes, 0.3f, out string fn, seat.transform.parent);
                                if (d < gap) { gap = d; gapAt = $"{(BodyPart)part} - {fn}"; }
                            }
                        }
                        if (gap > ContactMax) self.Add($"hovers: nearest furniture {gap * 100f:0.0} cm away");
                    }

                    var worst = hits.GroupBy(h => h.Part + " x " + h.What).Select(g => (key: g.Key, d: g.Max(h => h.Depth))).OrderByDescending(x => x.d).ToList();
                    float pmax = worst.Count > 0 ? worst[0].d : 0f;
                    total += worst.Count + self.Count;
                    max = Mathf.Max(max, pmax);
                    Vector3 pel = pose.PelvisPos;
                    sb.AppendLine($"{where} pose {v}{(legacy ? "" : $" {SitPoseCatalog.Name((byte)v)}")}: {(worst.Count + self.Count == 0 ? "CLEAN" : $"{worst.Count + self.Count} PROBLEMS, deepest {pmax * 100f:0.0} cm")}" +
                                  $" | pelvis y {pel.y:0.000} z {pel.z:0.000} | feet: lowest point R {soleR - floor:0.000} L {soleL - floor:0.000} m above the floor" +
                                  (legacy ? "" : $" | contact: {(gap > ContactMax ? "HOVERS " : "")}{(gap > 1f ? "none" : $"{gap * 100f:0.0} cm ({gapAt})")}"));
                    foreach (var w in worst.Take(10)) sb.AppendLine($"    {w.key}: {w.d * 100f:0.0} cm");
                    foreach (var s in self) sb.AppendLine($"    self: {s}");

                    if (!legacy)
                    {
                        // First-person eye: clear of the own (visible) body and of the furniture over the look range.
                        float minBody = float.MaxValue, minFurn = float.MaxValue;
                        string atBody = "", atFurn = "";
                        foreach (var (yaw, pitch) in LookGrid(lim))
                        {
                            Vector3 eye = SeatPoses.Eye(v, shape, yaw, pitch);
                            for (int i = 0; i < boxes.Length; i++)
                            {
                                if (i == (int)BodyPart.Head) continue;
                                float d = Breath(boxes[i]).Distance(eye);
                                if (d < minBody) { minBody = d; atBody = $"{(BodyPart)i} at yaw {yaw:0} pitch {pitch:0}"; }
                            }
                            float df = FurnitureDistance(t.TransformPoint(eye), meshes, 0.5f, out string fn);
                            if (df < minFurn) { minFurn = df; atFurn = $"{fn} at yaw {yaw:0} pitch {pitch:0}"; }
                        }
                        bool bad = minBody < EyeBodyMin || minFurn < EyeFurnitureMin;
                        if (bad) eyeIssues++;
                        Vector3 eyeN = SeatPoses.Eye(v, shape, lim.Center, lim.Pitch);
                        sb.AppendLine($"    eye: {(bad ? "PROBLEM " : "")}nearest own body {minBody * 100f:0.0} cm ({atBody}), nearest furniture {(minFurn > 1f ? "> 50 cm" : $"{minFurn * 100f:0.0} cm ({atFurn})")}" +
                                      $", look limits yaw {lim.Center:0}+-{lim.Yaw:0} up {lim.Up:0} down {lim.Down:0} (neutral pitch {lim.Pitch:0}, head tilt {lim.Roll:0})" +
                                      $", neutral eye {eyeN.y:0.00} m above the floor");
                    }
                }
            }

            // Bodies on neighbouring seats (any pose pair).
            var A = new PartBox[BodyBoxes.Count];
            var B = new PartBox[BodyBoxes.Count];
            for (int i = 0; i < seats.Count; i++)
            for (int j = i + 1; j < seats.Count; j++)
            {
                if (Vector3.Distance(seats[i].transform.position, seats[j].transform.position) > 2.2f) continue;
                float worst = 0f;
                string at = "";
                bool sameSofa = seats[i].transform.parent != null && seats[i].transform.parent == seats[j].transform.parent;
                for (int va = 0; va < SeatPoses.Count; va++)
                for (int vb = 0; vb < SeatPoses.Count; vb++)
                {
                    if (legacy ? va >= SeatPoses.RandomCount || vb >= SeatPoses.RandomCount
                            : !SeatPoses.Supports(seats[i].Shape.MirrorSource ?? seats[i].Shape, va) || !SeatPoses.Supports(seats[j].Shape.MirrorSource ?? seats[j].Shape, vb)) continue;
                    // Lying along the sofa needs the other seat of that sofa free.
                    if (!legacy && sameSofa && (SeatPoses.SpansNeighbour(va) || SeatPoses.SpansNeighbour(vb))) continue;
                    BodyBoxes.Get(legacy ? Legacy(va) : SeatPoses.Evaluate(va, seats[i].Shape, 0f, 0f), A);
                    BodyBoxes.Get(legacy ? Legacy(vb) : SeatPoses.Evaluate(vb, seats[j].Shape, 0f, 0f), B);
                    foreach (var a in A)
                    foreach (var b in B)
                    {
                        float d = BodyBoxes.Penetration(World(a, seats[i].transform), World(b, seats[j].transform));
                        if (d > worst) { worst = d; at = $"pose {va} {a.Part} x pose {vb} {b.Part}"; }
                    }
                }
                if (worst > Tolerance) nb++;
                sb.AppendLine($"neighbours {seats[i].name} x {seats[j].name}: {(worst > Tolerance ? $"OVERLAP {worst * 100f:0.0} cm ({at})" : "clear")}");
            }
            sb.AppendLine($"{(legacy ? "BEFORE" : "AFTER")}: {total} problems, deepest {max * 100f:0.0} cm, eye problems {eyeIssues}, neighbour overlaps {nb}");
            return (total, max, eyeIssues, nb);
        }

        const string BakedPath = "Assets/_Project/Characters/Runtime/Actions/SeatPoses.Baked.cs";

        /// <summary>Writes SeatPoses.Baked.cs with the solved poses of every seat shape in the lobby (plus the generic one).</summary>
        static string WriteBaked(List<LobbySeat> seats)
        {
            var shapes = seats.Select(s => s.Shape).Where(s => s.MirrorSource == null).Append(SeatShape.Generic())
                .GroupBy(s => s.Id).Select(g => g.First()).OrderBy(s => s.Id).ToList();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var code = new StringBuilder();
            void L(string line) => code.Append(line).Append("\r\n");
            L("// <auto-generated>");
            L("// Written by the editor seat check (HideAndSeek.Game.Editor.SeatFitCheck, menu Hide and Seek/Lobby/Seat fit check):");
            L("// the seated poses solved for every lobby seat shape, so nothing is solved at runtime. Do not edit by hand.");
            L("// </auto-generated>");
            L("namespace HideAndSeek.Characters");
            L("{");
            L("    public static partial class SeatPoses");
            L("    {");
            L($"        static readonly int BakedVersion = {SeatPoses.SolverVersion};");
            L("        static readonly float[] BakedData =");
            L("        {");
            foreach (var shape in shapes)
            for (int v = 0; v < SeatPoses.Count; v++)
            {
                if (!SeatPoses.Supports(shape, v)) continue;
                var d = SeatPoses.Bake(v, shape);
                L($"            // {shape.Kind} (shape {shape.Id}), pose {v}");
                for (int i = 0; i < d.Length; i += 8)
                {
                    var line = new StringBuilder("           ");
                    for (int j = i; j < Mathf.Min(i + 8, d.Length); j++) line.Append(' ').Append(d[j].ToString("R", inv)).Append("f,");
                    L(line.ToString());
                }
            }
            L("        };");
            L("    }");
            L("}");
            string text = code.ToString();
            string old = File.Exists(BakedPath) ? File.ReadAllText(BakedPath) : "";
            if (old == text) return $"Baked poses ({shapes.Count} shapes x {SeatPoses.Count}): {BakedPath} up to date.";
            File.WriteAllText(BakedPath, text);
            return $"Baked poses ({shapes.Count} shapes x {SeatPoses.Count}): {BakedPath} WRITTEN (recompile to use).";
        }

        /// <summary>
        /// The runtime solver keeps the body out of <see cref="SeatShape"/> solids; they must enclose the real furniture.
        /// Lists the furniture's mesh vertices that stick out of them.
        /// </summary>
        static void ValidateShapes(List<LobbySeat> seats, List<Mesh3> meshes, StringBuilder sb)
        {
            sb.AppendLine("=== Seat shapes vs the real furniture meshes (vertices outside the solver's solids, 3 mm tolerance) ===");
            var done = new HashSet<string>();
            foreach (var seat in seats)
            {
                var furniture = seat.transform.parent;
                if (furniture == null) continue;
                string key = furniture.name + "/" + seat.Shape.Id;
                if (!done.Add(key)) continue;
                int bad = 0, all = 0;
                var lines = new List<string>();
                foreach (var m in meshes)
                {
                    if (!m.R.transform.IsChildOf(furniture)) continue;
                    int outside = 0;
                    Vector3 example = default;
                    foreach (var v in m.V)
                    {
                        Vector3 l = seat.transform.InverseTransformPoint(v);
                        all++;
                        if (seat.Shape.Contains(l, 0.003f)) continue;
                        if (outside++ == 0) example = l;
                    }
                    bad += outside;
                    if (outside > 0) lines.Add($"    {m.Name}: {outside}/{m.V.Length} outside, e.g. seat-space {example.x:0.000} {example.y:0.000} {example.z:0.000}");
                }
                sb.AppendLine($"{seat.name} ({Kind(seat)}): {(bad == 0 ? "all" : $"{all - bad}/{all}")} furniture vertices inside the solids");
                foreach (var l in lines) sb.AppendLine(l);
            }
        }

        // ================================================================== text views of the pin-up poses

        const string PartLetters = "THAFafLSls"; // torso, head, right upper arm / forearm, left ..., right thigh / shin, left ...

        /// <summary>
        /// Logs/seatposes_views.txt: every pin-up pose on the first seat of each kind, seen from the front (x right, y up),
        /// the side (z right, y up) and from above (x right, z up), seat space; '#' = the seat's solids, letters = body
        /// parts (upper case right / torso / head, lower case left). Geometry only, nothing is rendered.
        /// </summary>
        static void WriteViews(List<LobbySeat> seats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Pin-up poses as text views (seat space; 2.5 cm per column, 5 cm per row).");
            sb.AppendLine("T torso, H head, A/F right upper arm / forearm, a/f left, L/S right thigh / shin, l/s left, # seat solids.");
            var done = new HashSet<int>();
            var boxes = new PartBox[BodyBoxes.Count];
            foreach (var seat in seats)
            {
                var shape = seat.Shape;
                if (shape.MirrorSource != null || !done.Add(shape.Id)) continue;
                for (int v = SeatPoses.RandomCount; v < SeatPoses.Count; v++)
                {
                    if (!SeatPoses.Supports(shape, v)) continue;
                    var lim = SeatPoses.Limits(v, shape);
                    BodyBoxes.Get(SeatPoses.Evaluate(v, shape, lim.Center, lim.Pitch), boxes);
                    var solids = SeatPoses.SpansNeighbour(v) && shape.Wide != null ? shape.Wide : shape;
                    bool sofa = shape.Kind == SeatKind.Sofa;
                    float x0 = sofa ? -1.85f : -0.8f, x1 = sofa ? 0.85f : 0.8f, z0 = sofa ? -0.85f : -0.75f, z1 = sofa ? 0.6f : 0.85f;
                    sb.AppendLine();
                    sb.AppendLine($"===== {seat.name} ({Kind(seat)}) pose {v} {SitPoseCatalog.Name((byte)v)} =====");
                    sb.AppendLine("-- front (looking at the seat from the room; x ->, y ^)");
                    View(sb, boxes, solids, x0, x1, 0f, 2.1f, (a, b, d) => new Vector3(a, b, z1 - d), z1 - z0);
                    sb.AppendLine("-- side (from the right; z ->, y ^)");
                    View(sb, boxes, solids, z0, z1, 0f, 2.1f, (a, b, d) => new Vector3(x1 - d, b, a), x1 - x0);
                    sb.AppendLine("-- top (x ->, z ^)");
                    View(sb, boxes, solids, x0, x1, z0, z1, (a, b, d) => new Vector3(a, 2.1f - d, b), 2.1f);
                }
            }
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "seatposes_views.txt");
            File.WriteAllText(path, sb.ToString());
        }

        static void View(StringBuilder sb, PartBox[] boxes, SeatShape shape, float a0, float a1, float b0, float b1,
            System.Func<float, float, float, Vector3> at, float depth)
        {
            for (float b = b1 - 0.025f; b > b0; b -= 0.05f)
            {
                var line = new StringBuilder();
                for (float a = a0 + 0.0125f; a < a1; a += 0.025f)
                {
                    char c = ' ';
                    for (float d = 0f; d <= depth && c == ' '; d += 0.02f)
                    {
                        Vector3 p = at(a, b, d);
                        for (int i = 0; i < boxes.Length && c == ' '; i++)
                            if (boxes[i].Distance(p) <= 0f) c = PartLetters[i];
                        if (c == ' ' && p.y > 0.005f && shape.Contains(p, 0f)) c = '#';
                    }
                    line.Append(c);
                }
                sb.AppendLine(line.ToString().TrimEnd());
            }
        }

        const int IdleSamples = 12;

        static IEnumerable<(float yaw, float pitch)> LookGrid(SeatPoses.LookLimits lim)
        {
            var yaws = new List<float>();
            for (float y = -lim.Yaw; y < lim.Yaw; y += 15f) yaws.Add(lim.Center + y);
            yaws.Add(lim.Center + lim.Yaw);
            var pitches = new List<float>();
            for (float p = -lim.Down; p < lim.Up; p += 10f) pitches.Add(p);
            pitches.Add(lim.Up);
            foreach (float y in yaws)
            foreach (float p in pitches)
                yield return (y, p);
        }

        static string Kind(LobbySeat s)
        {
            var sh = s.Shape;
            if (sh.Kind == SeatKind.Sofa) return sh.ArmSide > 0 ? "sofa, armrest right" : "sofa, armrest left";
            return sh.Kind == SeatKind.Pouf ? "pouf" : "generic";
        }

        // ================================================================== geometry

        /// <summary>Breathing scales the body mesh by up to (0.8 %, 0.4 %, 3 %).</summary>
        static PartBox Breath(PartBox b)
        {
            if (b.Part != BodyPart.Torso) return b;
            b.Half = Vector3.Scale(b.Half, new Vector3(1.008f, 1.004f, 1.03f));
            return b;
        }

        static PartBox World(PartBox b, Transform seat) => b.Transformed(seat.position, seat.rotation);

        static float Lowest(PartBox b)
        {
            float y = float.MaxValue;
            for (int i = 0; i < 8; i++)
                y = Mathf.Min(y, b.Point((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f).y);
            return y;
        }

        static List<Mesh3> CollectMeshes()
        {
            var list = new List<Mesh3>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var m = mf.sharedMesh;
                var l2w = r.transform.localToWorldMatrix;
                var v = m.vertices;
                for (int i = 0; i < v.Length; i++) v[i] = l2w.MultiplyPoint3x4(v[i]);
                list.Add(new Mesh3 { R = r, Name = PathOf(r.transform), V = v, T = m.triangles, B = r.bounds });
            }
            return list;
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p != null && p.parent != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        static float FloorBelow(List<Mesh3> meshes, Vector3 at)
        {
            // Highest upward-facing surface under the point, below 5 cm (floor, rug).
            float best = 0f;
            foreach (var m in meshes)
            {
                if (m.B.max.y > 0.06f || at.x < m.B.min.x || at.x > m.B.max.x || at.z < m.B.min.z || at.z > m.B.max.z) continue;
                best = Mathf.Max(best, m.B.max.y);
            }
            return best;
        }

        static void Furniture(PartBox b, List<Mesh3> meshes, List<Hit> hits)
        {
            Vector3 ext = new Vector3(BodyBoxes.Radius(b, Vector3.right), BodyBoxes.Radius(b, Vector3.up), BodyBoxes.Radius(b, Vector3.forward));
            var aabb = new Bounds(b.Center, ext * 2f);
            foreach (var m in meshes)
            {
                if (!m.B.Intersects(aabb)) continue;
                float worst = 0f;
                for (int i = 0; i < m.T.Length; i += 3)
                {
                    Vector3 a = m.V[m.T[i]], c = m.V[m.T[i + 1]], d = m.V[m.T[i + 2]];
                    if (Mathf.Max(a.x, Mathf.Max(c.x, d.x)) < aabb.min.x || Mathf.Min(a.x, Mathf.Min(c.x, d.x)) > aabb.max.x ||
                        Mathf.Max(a.y, Mathf.Max(c.y, d.y)) < aabb.min.y || Mathf.Min(a.y, Mathf.Min(c.y, d.y)) > aabb.max.y ||
                        Mathf.Max(a.z, Mathf.Max(c.z, d.z)) < aabb.min.z || Mathf.Min(a.z, Mathf.Min(c.z, d.z)) > aabb.max.z) continue;
                    float pen = BoxTriangle(b, a, c, d);
                    if (pen > worst) worst = pen;
                }
                if (worst > Tolerance) hits.Add(new Hit { Part = b.Part.ToString(), What = m.Name, Depth = worst });
            }
        }

        /// <summary>Penetration of a triangle into an oriented box (smallest overlap over the 13 SAT axes), 0 = apart.</summary>
        static float BoxTriangle(in PartBox b, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector3 n = Vector3.Cross(e0, -e2);
            if (n.sqrMagnitude < 1e-12f) return 0f;
            float best = float.MaxValue;
            if (!Axis(b, p0, p1, p2, n.normalized, ref best)) return 0f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 ax = b.Axis(i);
                if (!Axis(b, p0, p1, p2, ax, ref best)) return 0f;
                foreach (var e in new[] { e0, e1, e2 })
                {
                    Vector3 c = Vector3.Cross(ax, e);
                    if (c.sqrMagnitude < 1e-10f) continue;
                    if (!Axis(b, p0, p1, p2, c.normalized, ref best)) return 0f;
                }
            }
            return best;
        }

        static bool Axis(in PartBox b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 ax, ref float best)
        {
            float c = Vector3.Dot(b.Center, ax), r = BodyBoxes.Radius(b, ax);
            float t0 = Vector3.Dot(p0, ax), t1 = Vector3.Dot(p1, ax), t2 = Vector3.Dot(p2, ax);
            float tmin = Mathf.Min(t0, Mathf.Min(t1, t2)), tmax = Mathf.Max(t0, Mathf.Max(t1, t2));
            float overlap = Mathf.Min(c + r - tmin, tmax - (c - r));
            if (overlap < best) best = overlap;
            return overlap > 0f;
        }

        static float FurnitureDistance(Vector3 p, List<Mesh3> meshes, float range, out string name, Transform only = null)
        {
            float best = float.MaxValue;
            name = "";
            var probe = new Bounds(p, Vector3.one * range * 2f);
            foreach (var m in meshes)
            {
                if (!m.B.Intersects(probe)) continue;
                if (only != null && !m.R.transform.IsChildOf(only)) continue;
                for (int i = 0; i < m.T.Length; i += 3)
                {
                    float d = (ClosestOnTriangle(p, m.V[m.T[i]], m.V[m.T[i + 1]], m.V[m.T[i + 2]]) - p).magnitude;
                    if (d < best) { best = d; name = m.Name; }
                }
            }
            return best;
        }

        static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }

        // ================================================================== the poses before the fit (for the report)

        static CharacterPose Legacy(int v)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.5f, -0.12f);
            switch (v)
            {
                default:
                    p.Torso = Quaternion.Euler(-3f, 0f, 0f);
                    p.SolveLeg(0, new Vector3(0.14f, 0.06f, 0.3f), new Vector3(0.1f, 0.4f, 1f));
                    p.SolveLeg(1, new Vector3(-0.14f, 0.06f, 0.3f), new Vector3(-0.1f, 0.4f, 1f));
                    p.SolveArm(0, new Vector3(0.19f, 0.58f, 0.22f), new Vector3(1f, -0.2f, -0.5f));
                    p.SolveArm(1, new Vector3(-0.19f, 0.58f, 0.22f), new Vector3(-1f, -0.2f, -0.5f));
                    break;
                case 1:
                    p.PelvisPos = new Vector3(0f, 0.49f, -0.08f);
                    p.Pelvis = Quaternion.Euler(-8f, 0f, 0f);
                    p.Torso = Quaternion.Euler(-24f, 0f, 0f);
                    p.SolveLeg(0, new Vector3(0.15f, 0.05f, 0.47f), new Vector3(0.1f, 0.5f, 1f));
                    p.SolveLeg(1, new Vector3(-0.17f, 0.05f, 0.33f), new Vector3(-0.15f, 0.5f, 1f));
                    p.SolveArm(0, new Vector3(0.25f, 0.46f, -0.42f), new Vector3(0.3f, 0.2f, -1f));
                    p.SolveArm(1, new Vector3(-0.25f, 0.46f, -0.42f), new Vector3(-0.3f, 0.2f, -1f));
                    break;
                case 2:
                    p.PelvisPos = new Vector3(0f, 0.5f, -0.1f);
                    p.Pelvis = Quaternion.Euler(8f, 0f, 0f);
                    p.Torso = Quaternion.Euler(26f, 0f, 0f);
                    p.SolveLeg(0, new Vector3(0.21f, 0.05f, 0.28f), new Vector3(0.45f, 0.3f, 1f));
                    p.SolveLeg(1, new Vector3(-0.21f, 0.05f, 0.28f), new Vector3(-0.45f, 0.3f, 1f));
                    p.SolveArm(0, new Vector3(0.05f, 0.6f, 0.37f), new Vector3(0.6f, -1f, 0.1f));
                    p.SolveArm(1, new Vector3(-0.05f, 0.62f, 0.36f), new Vector3(-0.6f, -1f, 0.1f));
                    break;
                case 3:
                    p.Pelvis = Quaternion.Euler(-3f, 4f, 0f);
                    p.Torso = Quaternion.Euler(-7f, 5f, 2f);
                    p.SolveLeg(1, new Vector3(-0.13f, 0.06f, 0.3f), new Vector3(-0.05f, 0.4f, 1f));
                    p.SolveLeg(0, new Vector3(-0.1f, 0.24f, 0.36f), new Vector3(-0.35f, 0.7f, 1f));
                    p.SolveArm(0, new Vector3(0.0f, 0.71f, 0.18f), new Vector3(1f, -0.2f, -0.4f));
                    p.SolveArm(1, new Vector3(-0.3f, 0.48f, -0.16f), new Vector3(-1f, 0f, -0.3f));
                    break;
            }
            return p;
        }
    }
}
