using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Player.Editor.Audio
{
    /// <summary>
    /// Synthesises all footstep / movement WAVs (deterministic seed, 44.1 kHz mono 16-bit), imports them,
    /// builds the <see cref="FootstepLibrary"/> and renders listening / visual previews to docs/previews/player.
    /// </summary>
    public static class PlayerAudioBuilder
    {
        public const ulong Seed = 20261117UL;
        public const string AudioDir = PlayerBuildMenu.Root + "/Audio";
        public const string FootstepsDir = AudioDir + "/Footsteps";
        public const string MovementDir = AudioDir + "/Movement";
        public const string LibraryPath = AudioDir + "/FootstepLibrary.asset";

        private static readonly (SurfaceType surface, int variations)[] Surfaces =
        {
            (SurfaceType.Concrete, 8), (SurfaceType.Metal, 6), (SurfaceType.Water, 6),
        };
        private static readonly FootstepGait[] Gaits = { FootstepGait.Walk, FootstepGait.Run, FootstepGait.Crouch };

        private const int JumpCount = 3, LandCount = 3, CrouchCount = 2;

        // ------------------------------------------------------------------ synthesis (in memory)

        private sealed class Bank
        {
            public readonly Dictionary<(SurfaceType, FootstepGait), List<float[]>> Steps = new Dictionary<(SurfaceType, FootstepGait), List<float[]>>();
            public readonly List<float[]> Jump = new List<float[]>(), LandLight = new List<float[]>(), LandHeavy = new List<float[]>();
            public readonly List<float[]> CrouchDown = new List<float[]>(), StandUp = new List<float[]>();
            public float[] Cloth;
        }

        private static Bank Synthesize()
        {
            var b = new Bank();
            foreach (var (surface, count) in Surfaces)
            foreach (var gait in Gaits)
            {
                var list = new List<float[]>();
                for (int v = 0; v < count; v++) list.Add(FootstepSynth.Step(surface, gait, v, Seed));
                b.Steps[(surface, gait)] = list;
            }
            for (int v = 0; v < JumpCount; v++) b.Jump.Add(FootstepSynth.Jump(v, Seed));
            for (int v = 0; v < LandCount; v++) b.LandLight.Add(FootstepSynth.Landing(false, v, Seed));
            for (int v = 0; v < LandCount; v++) b.LandHeavy.Add(FootstepSynth.Landing(true, v, Seed));
            for (int v = 0; v < CrouchCount; v++) b.CrouchDown.Add(FootstepSynth.CrouchRustle(true, v, Seed));
            for (int v = 0; v < CrouchCount; v++) b.StandUp.Add(FootstepSynth.CrouchRustle(false, v, Seed));
            b.Cloth = FootstepSynth.ClothLoop(3f, Seed);
            return b;
        }

        // ------------------------------------------------------------------ entry points

        [MenuItem("HideAndSeek/Player/Build Audio")]
        public static FootstepLibrary BuildAudio()
        {
            var bank = Synthesize();
            EnsureFolder(AudioDir);
            EnsureFolder(FootstepsDir);
            EnsureFolder(MovementDir);

            var lib = AssetDatabase.LoadAssetAtPath<FootstepLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<FootstepLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            var sets = new List<SurfaceClipSet>();
            foreach (var (surface, _) in Surfaces)
            {
                string dir = FootstepsDir + "/" + surface;
                EnsureFolder(dir);
                var set = new SurfaceClipSet { surface = surface };
                foreach (var gait in Gaits)
                {
                    var data = bank.Steps[(surface, gait)];
                    var clips = new AudioClip[data.Count];
                    for (int v = 0; v < data.Count; v++)
                        clips[v] = WriteClip($"{dir}/{surface}_{gait}_{v + 1:00}.wav", data[v], false);
                    if (gait == FootstepGait.Walk) set.walk = clips;
                    else if (gait == FootstepGait.Run) set.run = clips;
                    else set.crouch = clips;
                }
                sets.Add(set);
            }
            lib.surfaces = sets.ToArray();
            lib.jump = WriteList("Jump", bank.Jump);
            lib.landLight = WriteList("LandLight", bank.LandLight);
            lib.landHeavy = WriteList("LandHeavy", bank.LandHeavy);
            lib.crouchDown = WriteList("CrouchDown", bank.CrouchDown);
            lib.standUp = WriteList("StandUp", bank.StandUp);
            lib.clothLoop = WriteClip(MovementDir + "/ClothLoop.wav", bank.Cloth, true);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            RenderPreviews(bank);
            Debug.Log("[PlayerAudio] Audio built: " + LibraryPath);
            return lib;
        }

        /// <summary>Batch entry: synthesise, import and render previews only.</summary>
        public static void BuildAudioFromCommandLine()
        {
            try { BuildAudio(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[PlayerAudio] FAILED: " + e); EditorApplication.Exit(1); }
        }

        private static AudioClip[] WriteList(string name, List<float[]> data)
        {
            var clips = new AudioClip[data.Count];
            for (int i = 0; i < data.Count; i++) clips[i] = WriteClip($"{MovementDir}/{name}_{i + 1:00}.wav", data[i], false);
            return clips;
        }

        private static AudioClip WriteClip(string assetPath, float[] samples, bool loop)
        {
            byte[] bytes = Wav.Encode(samples);
            string full = Path.GetFullPath(assetPath);
            bool changed = !File.Exists(full) || !BytesEqual(File.ReadAllBytes(full), bytes);
            if (changed) File.WriteAllBytes(full, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
            var settings = importer.defaultSampleSettings;
            bool dirty = false;
            var wantLoad = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            var wantFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            if (settings.loadType != wantLoad || settings.compressionFormat != wantFormat ||
                settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
                (loop && Mathf.Abs(settings.quality - 0.6f) > 0.01f))
            {
                settings.loadType = wantLoad;
                settings.compressionFormat = wantFormat;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                if (loop) settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                dirty = true;
            }
            if (!importer.forceToMono) { importer.forceToMono = true; dirty = true; }
            if (importer.loadInBackground) { importer.loadInBackground = false; dirty = true; }
            if (dirty) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ previews

        private const float WalkVol = 0.62f, RunVol = 1f, CrouchVol = 0.32f;

        private static void RenderPreviews(Bank b)
        {
            string dir = Path.GetFullPath(PlayerBuildMenu.PreviewDir);
            Directory.CreateDirectory(dir);
            var markers = new List<(float time, string label)>();
            var montage = BuildMontage(b, markers);
            File.WriteAllBytes(Path.Combine(dir, "footsteps_montage.wav"), Wav.Encode(montage));
            WriteMontagePng(Path.Combine(dir, "footsteps_montage.png"), montage, markers);
            WriteGridPng(Path.Combine(dir, "footsteps_variations.png"), b);
        }

        private static float[] BuildMontage(Bank b, List<(float time, string label)> markers)
        {
            var r = new Rng(Seed ^ 0x3017AUL);
            var mix = new float[Dsp.Samples(60f)];
            float t = 0.4f;
            int last = -1;

            float[] PickNoRepeat(List<float[]> list)
            {
                int i = r.Range(0, list.Count - (last >= 0 && last < list.Count ? 1 : 0));
                if (last >= 0 && last < list.Count && i >= last) i++;
                last = i;
                return list[i];
            }

            void Steps(SurfaceType s, FootstepGait g, int count, float interval, float vol, string label, bool cloth = false)
            {
                markers.Add((t, label));
                last = -1;
                var list = b.Steps[(s, g)];
                float start = t;
                for (int k = 0; k < count; k++)
                {
                    Place(mix, PickNoRepeat(list), t, vol * r.Range(0.88f, 1.12f), r.Range(0.94f, 1.06f));
                    t += interval * r.Range(0.97f, 1.03f);
                }
                if (cloth)
                {
                    // cloth layer under the steps, pulsing with the gait
                    float dur = t - start;
                    int n = Dsp.Samples(dur);
                    int s0 = Dsp.Samples(start);
                    for (int i = 0; i < n && s0 + i < mix.Length; i++)
                    {
                        float tt = i / Dsp.Fs;
                        float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(tt / interval * Mathf.PI));
                        float fade = Mathf.Clamp01(tt / 0.3f) * Mathf.Clamp01((dur - tt) / 0.3f);
                        mix[s0 + i] += b.Cloth[i % b.Cloth.Length] * 0.16f * vol * pulse * fade;
                    }
                }
                t += 0.7f;
            }

            void One(float[] clip, float vol, string label, float after)
            {
                if (label != null) markers.Add((t, label));
                Place(mix, clip, t, vol, r.Range(0.97f, 1.03f));
                t += after;
            }

            Steps(SurfaceType.Concrete, FootstepGait.Walk, 10, 0.45f, WalkVol, "Concrete walk", true);
            Steps(SurfaceType.Concrete, FootstepGait.Run, 12, 0.355f, RunVol, "Concrete run", true);
            One(b.CrouchDown[0], 0.4f, "Crouch down", 0.7f);
            Steps(SurfaceType.Concrete, FootstepGait.Crouch, 8, 0.57f, CrouchVol, "Crouch walk", true);
            One(b.StandUp[0], 0.4f, "Stand up", 1.0f);

            One(b.Jump[0], 0.5f, "Jump + light land", 0.55f);
            One(b.LandLight[0], 0.8f, null, 0f);
            Place(mix, b.Steps[(SurfaceType.Concrete, FootstepGait.Run)][1], t, 0.5f, 1f);
            t += 1.2f;
            One(b.Jump[1], 0.5f, "Jump + heavy land", 0.75f);
            One(b.LandHeavy[0], 0.95f, null, 0f);
            Place(mix, b.Steps[(SurfaceType.Concrete, FootstepGait.Run)][4], t, 0.9f, 1f);
            t += 1.5f;

            Steps(SurfaceType.Metal, FootstepGait.Walk, 8, 0.45f, WalkVol * 1.05f, "Metal walk");
            Steps(SurfaceType.Metal, FootstepGait.Run, 8, 0.355f, RunVol * 1.05f, "Metal run");
            Steps(SurfaceType.Metal, FootstepGait.Crouch, 5, 0.57f, CrouchVol * 1.05f, "Metal crouch");
            Steps(SurfaceType.Water, FootstepGait.Walk, 8, 0.45f, WalkVol * 0.95f, "Water walk");
            Steps(SurfaceType.Water, FootstepGait.Run, 8, 0.355f, RunVol * 0.95f, "Water run");
            Steps(SurfaceType.Water, FootstepGait.Crouch, 5, 0.57f, CrouchVol * 0.95f, "Water crouch");

            // mixed: walking from concrete onto a grate and through a puddle
            markers.Add((t, "Concrete-grate-puddle"));
            var seq = new[] { SurfaceType.Concrete, SurfaceType.Concrete, SurfaceType.Metal, SurfaceType.Metal, SurfaceType.Concrete, SurfaceType.Water, SurfaceType.Water, SurfaceType.Concrete };
            foreach (var s in seq)
            {
                last = -1;
                Place(mix, PickNoRepeat(b.Steps[(s, FootstepGait.Walk)]), t, WalkVol * r.Range(0.9f, 1.1f), r.Range(0.94f, 1.06f));
                t += 0.45f;
            }
            t += 1f;

            int len = Mathf.Min(mix.Length, Dsp.Samples(t + 1.5f));
            var outBuf = new float[len];
            Array.Copy(mix, outBuf, len);
            Reverb(outBuf, 0.2f);
            float p = Dsp.Peak(outBuf);
            for (int i = 0; i < outBuf.Length; i++) outBuf[i] *= 0.89f / Mathf.Max(1e-6f, p);
            return outBuf;
        }

        private static void Place(float[] mix, float[] clip, float time, float gain, float pitch)
        {
            int s0 = Dsp.Samples(time);
            int n = (int)(clip.Length / pitch);
            for (int i = 0; i < n && s0 + i < mix.Length; i++)
            {
                float src = i * pitch;
                int a = (int)src;
                float f = src - a;
                float v = a + 1 < clip.Length ? clip[a] * (1f - f) + clip[a + 1] * f : clip[a];
                mix[s0 + i] += v * gain;
            }
        }

        /// <summary>Small Schroeder reverb approximating the in-game parking-garage preset (preview only).</summary>
        private static void Reverb(float[] buf, float wet)
        {
            int[] combs = { 1557, 1617, 1491, 1422, 1277, 1356 };
            int[] allpasses = { 225, 556 };
            var dry = (float[])buf.Clone();
            var acc = new float[buf.Length];
            foreach (int d0 in combs)
            {
                int d = d0 * 2; // ~70 ms combs => large space
                var line = new float[d];
                int idx = 0;
                float lp = 0f;
                for (int i = 0; i < buf.Length; i++)
                {
                    float y = line[idx];
                    lp = y * 0.65f + lp * 0.35f;
                    line[idx] = dry[i] + lp * 0.86f;
                    idx = (idx + 1) % d;
                    acc[i] += y;
                }
            }
            foreach (int d in allpasses)
            {
                var line = new float[d];
                int idx = 0;
                for (int i = 0; i < acc.Length; i++)
                {
                    float bufout = line[idx];
                    float y = -acc[i] + bufout;
                    line[idx] = acc[i] + bufout * 0.5f;
                    idx = (idx + 1) % d;
                    acc[i] = y;
                }
            }
            var hp = Biquad.HighPass(Dsp.Fs, 200f);
            var lp2 = Biquad.LowPass(Dsp.Fs, 4500f);
            for (int i = 0; i < buf.Length; i++) buf[i] = dry[i] + lp2.Tick(hp.Tick(acc[i])) * wet / combs.Length * 2f;
        }

        private static void WriteMontagePng(string path, float[] montage, List<(float time, string label)> markers)
        {
            float seconds = montage.Length / Dsp.Fs;
            int w = Mathf.Min(4000, Mathf.RoundToInt(seconds * 60f)), h = 380, labelH = 46;
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(16, 18, 22, 255);
            AudioViz.Draw(px, w, 0, 0, w, h - labelH, montage, seconds, 0.32f);
            for (int k = 0; k < markers.Count; k++)
            {
                int x = Mathf.RoundToInt(markers[k].time / seconds * w);
                for (int y = 0; y < h; y++) AudioViz.Set(px, w, x, y, y > h - labelH ? new Color(0.8f, 0.8f, 0.8f, 1f) : new Color(0.4f, 0.4f, 0.4f, 1f));
                AudioViz.Text(px, w, x + 3, h - 4 - (k % 3) * 13, markers[k].label, new Color(0.9f, 0.95f, 1f), 2);
            }
            Save(path, px, w, h);
        }

        private static void WriteGridPng(string path, Bank b)
        {
            var rows = new List<(string label, List<float[]> clips, float seconds)>();
            foreach (var (surface, _) in Surfaces)
            foreach (var gait in Gaits)
                rows.Add(($"{surface} {gait}", b.Steps[(surface, gait)], 0.5f));
            rows.Add(("Jump", b.Jump, 0.5f));
            rows.Add(("Land light", b.LandLight, 0.5f));
            rows.Add(("Land heavy", b.LandHeavy, 0.8f));
            rows.Add(("Crouch dn/up", new List<float[]>(b.CrouchDown) { b.StandUp[0], b.StandUp[1] }, 0.6f));
            rows.Add(("Cloth loop", new List<float[]> { b.Cloth }, 3f));

            const int labelW = 150, cellW = 150, cellH = 104, gap = 4, header = 26;
            int cols = 8;
            int w = labelW + cols * (cellW + gap);
            int h = header + rows.Count * (cellH + gap);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(22, 24, 30, 255);
            AudioViz.Text(px, w, 6, h - 6, "FOOTSTEP VARIATIONS - WAVEFORM + SPECTROGRAM (50 HZ - 18 KHZ LOG, 0.5 S PER CELL)", new Color(0.9f, 0.95f, 1f), 2);
            for (int r = 0; r < rows.Count; r++)
            {
                int yTop = h - header - r * (cellH + gap);
                AudioViz.Text(px, w, 6, yTop - cellH / 2 + 5, rows[r].label, new Color(0.85f, 0.9f, 0.95f), 2);
                var clips = rows[r].clips;
                for (int c = 0; c < clips.Count && c < cols; c++)
                {
                    int cw = rows[r].seconds > 2f ? cols * (cellW + gap) - gap : cellW;
                    AudioViz.Draw(px, w, labelW + c * (cellW + gap), yTop - cellH, cw, cellH, clips[c], rows[r].seconds, 0.33f);
                }
            }
            Save(path, px, w, h);
        }

        private static void Save(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
