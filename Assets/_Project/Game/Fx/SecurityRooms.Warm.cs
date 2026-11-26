using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Session caches of the security rooms: every baked sign / poster / label texture, the procedural surface
    /// textures, materials, meshes and synthesised sounds are made once per game session (kept across scene loads with
    /// <see cref="HideFlags.DontUnloadUnusedAsset"/>) and prepared ahead in the menu / lobby, one piece per frame, by
    /// <see cref="SecurityRoomsWarmup"/>. The round start then only creates the room objects.
    /// </summary>
    public partial class SecurityRooms
    {
        /// <summary>True once the menu / lobby warm-up finished (logged with the build time).</summary>
        public static bool WarmDone { get; internal set; }

        private static T Keep<T>(T o) where T : UnityEngine.Object
        {
            if (o != null) o.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            return o;
        }

        /// <summary>Warm-up steps, each small enough for one frame.</summary>
        internal static IEnumerable<(string name, Action run)> WarmSteps()
        {
            foreach (SecurityTextures.Kind k in Enum.GetValues(typeof(SecurityTextures.Kind)))
            {
                var kind = k;
                yield return ("surface " + kind, () => SecurityTextures.Albedo(kind));
            }
            yield return ("walls", BakeWallTextures);
            yield return ("panel", () => { if (_panelTex == null) _panelTex = MakePanelTexture(); });
            yield return ("door sign", () => { if (_signTex == null) _signTex = MakeDoorSignTexture(); });
            yield return ("keys", () => { if (_keysTex == null) _keysTex = MakeKeyboardTexture(); });
            yield return ("phone", () => { if (_phoneTex == null) _phoneTex = MakePhoneTexture(); });
            yield return ("log", () => { if (_logTex == null) _logTex = MakeLogbookTexture(); });
            yield return ("stencil", () => { if (_stencilTex == null) _stencilTex = MakeStencilTexture(); });
            yield return ("microwave", () => { if (_microTex == null) _microTex = MakeMicrowaveTexture(); });
            yield return ("newspaper", () => { if (_newsTex == null) _newsTex = MakeNewspaperTexture(); });
            yield return ("grime", () => { if (_grimeAtlas == null) _grimeAtlas = MakeGrimeAtlas(); });
            yield return ("cobweb", () => { if (_webTex == null) _webTex = MakeCobwebTexture(); });
            for (int b = 0; b < CanBrands; b++)
            {
                int brand = CanBrand(b);
                yield return ("can label " + brand, () => HideAndSeek.Characters.SodaCanArt.Label(brand));
            }
            yield return ("crushed cans", () =>
            {
                foreach (Crush k in Enum.GetValues(typeof(Crush))) { CanMeshes(k, 0); CanMeshes(k, 1); }
            });
            yield return ("box label", () => { if (_boxLabel == null) _boxLabel = MakeBoxLabel(); });
            yield return ("jacket + notes", BakeStorageTextures);
            yield return ("chair pose", () => HideAndSeek.Characters.SeatPoses.Body(0, ChairShape));
            yield return ("sounds", () =>
            {
                if (_switchClip == null) _switchClip = Keep(MakeStaticClip("MonitorSwitch", 0.5f, 11));
                if (_floorClip == null) _floorClip = Keep(MakeStaticClip("MonitorFloor", 0.95f, 23));
            });
            yield return ("crate sounds", () =>
            {
                if (_scrapeClip == null) _scrapeClip = Keep(MakeScrapeClip());
                if (_sawWoodClip == null) _sawWoodClip = Keep(MakeSawWoodClip());
                if (_breakClip == null) _breakClip = Keep(MakeBreakClip());
            });
        }
    }

    /// <summary>
    /// Prepares the security-room caches in the menu or the lobby (where a short hitch does not matter), one step per
    /// couple of frames, starting a few seconds after the scene loaded. Not in the game scene: there the room builds
    /// whatever is missing itself.
    /// </summary>
    public class SecurityRoomsWarmup : MonoBehaviour
    {
        private Coroutine _run;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("[SecurityWarmup]");
            DontDestroyOnLoad(go);
            go.AddComponent<SecurityRoomsWarmup>();
        }

        private void Update()
        {
            if (SecurityRooms.WarmDone || _run != null) return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene == Net.NetSession.GameScene || Time.timeSinceLevelLoad < 1f) return;
            _run = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var sw = new System.Diagnostics.Stopwatch();
            double total = 0, worst = 0;
            string worstName = "";
            foreach (var (name, step) in SecurityRooms.WarmSteps())
            {
                if (SceneManager.GetActiveScene().name == Net.NetSession.GameScene)
                {
                    Debug.Log($"[Security] warm-up interrupted by the round start after {total:0} ms of work (the room bakes the rest)");
                    _run = null;
                    yield break;
                }
                sw.Restart();
                try { step(); }
                catch (Exception e) { Debug.LogWarning($"[Security] warm-up step '{name}' failed: {e.Message}"); }
                double ms = sw.Elapsed.TotalMilliseconds;
                total += ms;
                if (ms > worst) { worst = ms; worstName = name; }
                yield return null;
            }
            SecurityRooms.WarmDone = true;
            Debug.Log($"[Security] warm-up done in the {SceneManager.GetActiveScene().name}: {total:0} ms of work, longest step '{worstName}' {worst:0} ms");
        }
    }
}
