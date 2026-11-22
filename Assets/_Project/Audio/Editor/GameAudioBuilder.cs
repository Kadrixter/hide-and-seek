using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HideAndSeek.Player;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Audio.Editor
{
    /// <summary>
    /// (Re)builds Resources/GameAudioLibrary.asset from the clip folders and updates the player's FootstepLibrary.
    /// <para>Naming convention:
    /// <list type="bullet">
    /// <item>Sfx/&lt;Category&gt;/&lt;GameSfxName&gt;_NN.wav (or exactly &lt;GameSfxName&gt;.wav) - every match becomes a variation.</item>
    /// <item>Ambient/&lt;AmbientIdName&gt;.wav (first match wins).</item>
    /// <item>Sfx/Footsteps/&lt;Surface&gt;/&lt;Surface&gt;_&lt;Walk|Run|Crouch&gt;_NN.wav - replaces the synthesised set of that
    /// surface; surfaces/gaits without files keep the synthesised clips (fallback). Crouch falls back to Walk.</item>
    /// </list></para>
    /// UI blips (UiClick, UiHover, UiError, CountdownTick, PlayerJoined, PlayerLeft) are synthesised into Sfx/UI when missing.
    /// </summary>
    public static class GameAudioBuilder
    {
        public const string AudioRoot = "Assets/_Project/Audio";
        public const string SfxRoot = AudioRoot + "/Sfx";
        public const string AmbientRoot = AudioRoot + "/Ambient";
        public const string FootstepRoot = SfxRoot + "/Footsteps";
        public const string UiDir = SfxRoot + "/UI";
        public const string LibraryPath = AudioRoot + "/Resources/GameAudioLibrary.asset";
        public const string FootstepLibraryPath = "Assets/_Project/Player/Audio/FootstepLibrary.asset";

        // ------------------------------------------------------------------ tuning table

        private struct Def
        {
            public float Volume, Jitter, Range, ReverbMix;
            public bool Echo, Loop, Is2D;
            public Def(float volume, float jitter, float range, bool echo = false, bool loop = false, bool is2D = false, float reverbMix = 1f)
            { Volume = volume; Jitter = jitter; Range = range; Echo = echo; Loop = loop; Is2D = is2D; ReverbMix = is2D ? 0f : reverbMix; }
        }

        private static readonly Dictionary<GameSfx, Def> Defs = new Dictionary<GameSfx, Def>
        {
            // UI / menu (2D)
            { GameSfx.UiClick, new Def(0.6f, 0.03f, 10f, is2D: true) },
            { GameSfx.UiHover, new Def(0.35f, 0.03f, 10f, is2D: true) },
            { GameSfx.UiError, new Def(0.6f, 0f, 10f, is2D: true) },
            { GameSfx.LobbyButtonPress, new Def(0.9f, 0.04f, 20f) },
            { GameSfx.CountdownTick, new Def(0.7f, 0f, 10f, is2D: true) },
            { GameSfx.RoundStart, new Def(0.9f, 0f, 10f, is2D: true) },
            { GameSfx.SeekerReleased, new Def(1f, 0f, 10f, is2D: true) },
            { GameSfx.WinSting, new Def(0.9f, 0f, 10f, is2D: true) },
            { GameSfx.LoseSting, new Def(0.9f, 0f, 10f, is2D: true) },
            { GameSfx.PlayerJoined, new Def(0.6f, 0f, 10f, is2D: true) },
            { GameSfx.PlayerLeft, new Def(0.6f, 0f, 10f, is2D: true) },
            // knife
            { GameSfx.KnifeDraw, new Def(0.7f, 0.05f, 10f) },
            { GameSfx.KnifeSwing, new Def(0.8f, 0.08f, 12f) },
            { GameSfx.KnifeStab, new Def(1f, 0.08f, 15f) },
            { GameSfx.BodyFall, new Def(1f, 0.06f, 22f) },
            // voice
            { GameSfx.HiderScream, new Def(1f, 0.05f, 45f, echo: true) },
            { GameSfx.HiderPain, new Def(0.9f, 0.06f, 25f) },
            { GameSfx.HiderDeath, new Def(1f, 0.04f, 45f, echo: true) },
            { GameSfx.HiderBreathPanic, new Def(0.7f, 0f, 10f, loop: true) },
            // downed / rescue
            { GameSfx.CrawlLoop, new Def(0.6f, 0f, 10f, loop: true) },
            { GameSfx.HeartbeatLoop, new Def(0.85f, 0f, 5f, loop: true, reverbMix: 0f) },
            { GameSfx.ReviveLoop, new Def(0.7f, 0f, 10f, loop: true) },
            { GameSfx.ReviveDone, new Def(0.8f, 0.03f, 15f) },
            { GameSfx.MedkitPickup, new Def(0.7f, 0.05f, 10f) },
            { GameSfx.MedkitUse, new Def(0.8f, 0.04f, 10f) },
            // glock
            { GameSfx.GlockEquip, new Def(0.7f, 0.05f, 10f) },
            { GameSfx.GlockShot, new Def(1f, 0.04f, 120f, echo: true) },
            { GameSfx.GlockShotDistant, new Def(0.75f, 0.05f, 300f) },
            { GameSfx.GlockReload, new Def(0.8f, 0.03f, 15f) },
            { GameSfx.GlockDryFire, new Def(0.7f, 0.05f, 8f) },
            { GameSfx.BulletImpact, new Def(0.9f, 0.08f, 35f) },
            { GameSfx.ShellCasing, new Def(0.5f, 0.1f, 10f) },
            // misc
            { GameSfx.HintWhistle, new Def(1f, 0.03f, 60f, echo: true) },
            { GameSfx.FlashlightClick, new Def(0.6f, 0.05f, 8f) },
            { GameSfx.FlashlightAdjust, new Def(0.5f, 0.05f, 6f) },
            // elevators (the chime carries through the lobby, the hum is heard inside / right next to the cabin)
            { GameSfx.ElevatorDing, new Def(0.9f, 0.02f, 30f, echo: true) },
            { GameSfx.ElevatorDoor, new Def(0.8f, 0.05f, 18f) },
            { GameSfx.ElevatorMove, new Def(0.7f, 0f, 10f, loop: true) },
            { GameSfx.ChainsawStart, new Def(1f, 0.03f, 60f, echo: true) },
            { GameSfx.ChainsawIdle, new Def(0.75f, 0f, 40f, loop: true) },
            { GameSfx.ChainsawRev, new Def(1f, 0f, 110f, echo: true, loop: true) },
            { GameSfx.ChainsawCut, new Def(1f, 0.03f, 80f, echo: true) },
            { GameSfx.CarAlarm, new Def(1f, 0f, 160f, echo: true, loop: true) },
            { GameSfx.InjectStab, new Def(0.9f, 0.03f, 18f) },
            { GameSfx.ResurrectGasp, new Def(1f, 0.03f, 35f, echo: true) },
            { GameSfx.BandageWrap, new Def(0.8f, 0.03f, 14f) },
            { GameSfx.ForkSparks, new Def(1f, 0f, 30f) },
            { GameSfx.ForkScream, new Def(1f, 0f, 40f) },
            { GameSfx.ArcadeEat, new Def(0.55f, 0.03f, 10f) },
            { GameSfx.ArcadeDie, new Def(0.6f, 0f, 12f) },
            { GameSfx.ArcadeStart, new Def(0.55f, 0f, 12f) },
            { GameSfx.ArcadeStick, new Def(0.5f, 0.08f, 6f) },
            { GameSfx.RcMotor, new Def(0.6f, 0f, 22f, loop: true) },
            { GameSfx.RcHorn, new Def(1f, 0f, 70f, echo: true, loop: true) },
            { GameSfx.RcCrush, new Def(1f, 0.04f, 30f, echo: true) },
            { GameSfx.RcPlace, new Def(0.7f, 0.06f, 8f) },
            { GameSfx.RcBump, new Def(0.7f, 0.08f, 14f) },
            { GameSfx.MinigunSpinUp, new Def(1f, 0f, 40f, echo: true) },
            { GameSfx.MinigunFire, new Def(1f, 0f, 90f, echo: true, loop: true) },
            { GameSfx.MinigunSpinDown, new Def(1f, 0f, 40f, echo: true) },
            { GameSfx.DoorOpen, new Def(0.9f, 0.03f, 18f) },
            { GameSfx.DoorSlam, new Def(1f, 0.03f, 40f, echo: true) },
            { GameSfx.ElevatorButton, new Def(0.6f, 0.06f, 6f) },
            { GameSfx.VendButton, new Def(0.6f, 0.04f, 8f) },
            { GameSfx.VendDrop, new Def(0.85f, 0.04f, 14f) },
            { GameSfx.CanOpen, new Def(0.8f, 0.05f, 10f) },
            { GameSfx.CanGulp, new Def(0.75f, 0.04f, 8f) },
            { GameSfx.CanAhh, new Def(0.7f, 0.04f, 10f) },
            { GameSfx.CanCrush, new Def(0.8f, 0.06f, 10f) },
            { GameSfx.CanBounce, new Def(0.8f, 0.08f, 16f) },
        };

        private static readonly Dictionary<AmbientId, (float volume, float range, bool bed)> AmbientDefs = new Dictionary<AmbientId, (float, float, bool)>
        {
            { AmbientId.MenuMusic, (0.8f, 0f, true) },
            { AmbientId.LobbyRoom, (0.12f, 0f, true) },     // wind rumble cut out of the clip (22.11), only room hum + hiss left
            { AmbientId.GarageBed, (0.5f, 0f, true) },      // AmbientSynth: fan hum + faint air, no rumble / wind
            { AmbientId.Ventilation, (0.5f, 12f, false) },  // clip: the dip + gust swell cut out, steady airflow only
            { AmbientId.Drips, (0.6f, 10f, false) },
            { AmbientId.LampBuzz, (0.22f, 5f, false) },     // AmbientSynth ballast hum (the old clip squealed)
            { AmbientId.TensionMusic, (0.7f, 0f, true) },
        };

        /// <summary>
        /// Ids with no clip on purpose (not reported as missing, no library entry). DistantCity was nothing but a wind
        /// rumble blowing in at the ramps - removed with the rest of the wind (22.11).
        /// </summary>
        private static readonly HashSet<AmbientId> RetiredAmbient = new HashSet<AmbientId> { AmbientId.DistantCity };

        // ------------------------------------------------------------------ entry points

        [MenuItem("HideAndSeek/Audio/Build Game Audio Library")]
        public static void BuildMenu() => Build();

        /// <summary>Batch entry: -executeMethod HideAndSeek.Audio.Editor.GameAudioBuilder.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[GameAudio] Build FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Builds everything; returns the list of GameSfx/AmbientId ids that have no clip.</summary>
        public static List<string> Build()
        {
            EnsureFolder(SfxRoot);
            EnsureFolder(AmbientRoot);
            EnsureFolder(AudioRoot + "/Resources");
            SynthesizeUi();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var clips = CollectClips(SfxRoot, FootstepRoot);
            var ambientClips = CollectClips(AmbientRoot, null);

            var lib = AssetDatabase.LoadAssetAtPath<GameAudioLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<GameAudioLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            var missing = new List<string>();
            var report = new StringBuilder();
            var entries = new List<SfxEntry>();
            foreach (GameSfx id in Enum.GetValues(typeof(GameSfx)))
            {
                if (id == GameSfx.None) continue;
                var def = Defs.TryGetValue(id, out var d) ? d : new Def(0.8f, 0.05f, 20f);
                var list = Match(clips, id.ToString());
                foreach (var path in list.Select(AssetDatabase.GetAssetPath)) ConfigureImporter(path, def.Loop, !def.Is2D || IsMonoSource(path), false);
                list = list.Select(c => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GetAssetPath(c))).ToList();
                entries.Add(new SfxEntry
                {
                    id = id,
                    clips = list.ToArray(),
                    volume = def.Volume,
                    pitchJitter = def.Jitter,
                    range = def.Range,
                    echo = def.Echo,
                    reverbMix = def.ReverbMix,
                });
                if (list.Count == 0) missing.Add("GameSfx." + id);
                report.Append(id).Append('=').Append(list.Count).Append(' ');
            }

            var ambient = new List<AmbientEntry>();
            foreach (AmbientId id in Enum.GetValues(typeof(AmbientId)))
            {
                if (id == AmbientId.None || RetiredAmbient.Contains(id)) continue;
                (float volume, float range, bool bed) def = AmbientDefs.TryGetValue(id, out var d) ? d : (0.5f, 15f, false);
                var clip = Match(ambientClips, id.ToString()).FirstOrDefault();
                if (clip != null)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    ConfigureImporter(path, true, !def.bed, def.bed);
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                else missing.Add("AmbientId." + id);
                ambient.Add(new AmbientEntry { id = id, clip = clip, volume = def.volume, range = def.range, pitchJitter = def.bed ? 0f : 0.04f });
            }

            lib.sfx = entries.ToArray();
            lib.ambient = ambient.ToArray();
            EditorUtility.SetDirty(lib);

            string fsReport = UpdateFootstepLibrary();
            AssetDatabase.SaveAssets();

            Debug.Log("[GameAudio] Library built: " + LibraryPath + " | sfx " + entries.Count(e => e.clips.Length > 0) + "/" + entries.Count +
                      ", ambient " + ambient.Count(a => a.clip != null) + "/" + ambient.Count + "\n[GameAudio] clips per id: " + report +
                      "\n[GameAudio] footsteps: " + fsReport);
            if (missing.Count == 0) Debug.Log("[GameAudio] Missing ids: none");
            else Debug.LogWarning("[GameAudio] Missing ids (" + missing.Count + "): " + string.Join(", ", missing));
            return missing;
        }

        // ------------------------------------------------------------------ footsteps

        /// <summary>
        /// Puts generated footstep clips (Sfx/Footsteps/&lt;Surface&gt;) into the FootstepLibrary, keeping synthesised
        /// clips where nothing was generated. Also called by the Player audio builder after it re-synthesises.
        /// </summary>
        public static string UpdateFootstepLibrary() => ApplyGeneratedFootsteps(AssetDatabase.LoadAssetAtPath<FootstepLibrary>(FootstepLibraryPath));

        public static string ApplyGeneratedFootsteps(FootstepLibrary lib)
        {
            if (lib == null) return "FootstepLibrary not found at " + FootstepLibraryPath;
            var clips = CollectClips(FootstepRoot, null);
            var sets = lib.surfaces?.Where(s => s != null).ToList() ?? new List<SurfaceClipSet>();
            var sb = new StringBuilder();
            foreach (SurfaceType surface in Enum.GetValues(typeof(SurfaceType)))
            {
                var walk = Match(clips, surface + "_Walk");
                var run = Match(clips, surface + "_Run");
                var crouch = Match(clips, surface + "_Crouch");
                foreach (var c in walk.Concat(run).Concat(crouch)) ConfigureImporter(AssetDatabase.GetAssetPath(c), false, true, false);

                var set = sets.FirstOrDefault(s => s.surface == surface);
                if (set == null)
                {
                    if (walk.Count == 0 && run.Count == 0) { sb.Append(surface).Append(": none (falls back to concrete); "); continue; }
                    set = new SurfaceClipSet { surface = surface };
                    sets.Add(set);
                }
                if (walk.Count > 0) set.walk = walk.ToArray();
                if (run.Count > 0) set.run = run.ToArray();
                else if (set.run == null || set.run.Length == 0) set.run = set.walk;
                if (crouch.Count > 0) set.crouch = crouch.ToArray();
                else if (set.crouch == null || set.crouch.Length == 0) set.crouch = set.walk;
                if (set.walk == null || set.walk.Length == 0) set.walk = set.run;
                sb.Append(surface).Append(": walk ").Append(set.walk.Length).Append(walk.Count > 0 ? " gen" : " synth")
                  .Append(", run ").Append(set.run.Length).Append(run.Count > 0 ? " gen" : " synth")
                  .Append(", crouch ").Append(set.crouch.Length).Append(crouch.Count > 0 ? " gen" : " synth").Append("; ");
            }
            lib.surfaces = sets.OrderBy(s => (int)s.surface).ToArray();
            EditorUtility.SetDirty(lib);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ helpers

        private static Dictionary<string, AudioClip> CollectClips(string root, string excludeRoot)
        {
            var map = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(root)) return map;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (excludeRoot != null && path.StartsWith(excludeRoot + "/", StringComparison.OrdinalIgnoreCase)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (!map.ContainsKey(name)) map[name] = clip;
            }
            return map;
        }

        /// <summary>Clips named exactly "name" or "name_NN" (NN digits), sorted by name.</summary>
        private static List<AudioClip> Match(Dictionary<string, AudioClip> clips, string name)
        {
            var result = new List<(string, AudioClip)>();
            foreach (var kv in clips)
            {
                string n = kv.Key;
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) { result.Add((n, kv.Value)); continue; }
                if (n.Length <= name.Length + 1 || !n.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase)) continue;
                string suffix = n.Substring(name.Length + 1);
                if (suffix.All(char.IsDigit)) result.Add((n, kv.Value));
            }
            return result.OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase).Select(r => r.Item2).ToList();
        }

        private static bool IsMonoSource(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return clip != null && clip.channels == 1;
        }

        /// <summary>Short sfx: DecompressOnLoad/ADPCM-like Vorbis; loops: CompressedInMemory; beds/music: Streaming.</summary>
        private static void ConfigureImporter(string path, bool loop, bool mono, bool stream)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            var s = importer.defaultSampleSettings;
            var load = stream ? AudioClipLoadType.Streaming : loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            var fmt = AudioCompressionFormat.Vorbis;
            float quality = stream ? 0.7f : loop ? 0.6f : 0.75f;
            bool dirty = false;
            if (s.loadType != load || s.compressionFormat != fmt || Mathf.Abs(s.quality - quality) > 0.01f ||
                s.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate)
            {
                s.loadType = load;
                s.compressionFormat = fmt;
                s.quality = quality;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = s;
                dirty = true;
            }
            if (importer.forceToMono != mono) { importer.forceToMono = mono; dirty = true; }
            if (importer.loadInBackground != stream) { importer.loadInBackground = stream; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }

        private static void SynthesizeUi()
        {
            EnsureFolder(UiDir);
            Write(UiDir + "/UiClick_01.wav", UiSynth.Click);
            Write(UiDir + "/UiHover_01.wav", UiSynth.Hover);
            Write(UiDir + "/UiError_01.wav", UiSynth.Error);
            Write(UiDir + "/CountdownTick_01.wav", UiSynth.Tick);
            Write(UiDir + "/PlayerJoined_01.wav", () => UiSynth.TwoTone(true));
            Write(UiDir + "/PlayerLeft_01.wav", () => UiSynth.TwoTone(false));
        }

        private static void Write(string assetPath, Func<float[]> synth)
        {
            string full = Path.GetFullPath(assetPath);
            if (File.Exists(full)) return; // designers may replace it with a real file
            File.WriteAllBytes(full, UiSynth.EncodeWav(synth()));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
