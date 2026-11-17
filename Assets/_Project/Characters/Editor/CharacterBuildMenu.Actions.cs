using System;
using System.Collections.Generic;
using System.IO;
using HideAndSeek.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Action layer assets (prop / fx materials, ViewModel layer), showcase entries and preview renders.
    /// Command line:
    ///   Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod HideAndSeek.Characters.Editor.CharacterBuildMenu.CaptureActionPreviews
    /// </summary>
    public static partial class CharacterBuildMenu
    {
        public const string PropMaterialPath = ResourcesDir + "/Props/BlockyProp.mat";
        public const string FlashMaterialPath = ResourcesDir + "/Fx/MuzzleFlash.mat";
        public const string SmokeMaterialPath = ResourcesDir + "/Fx/GunSmoke.mat";

        /// <summary>Names layer 9 "ViewModel" in the TagManager (like BetaSceneBuilder does for layer 8).</summary>
        public static void EnsureViewModelLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var entry = layers.GetArrayElementAtIndex(FirstPersonViewModel.Layer);
            if (entry.stringValue == FirstPersonViewModel.LayerName) return;
            if (!string.IsNullOrEmpty(entry.stringValue))
                Debug.LogWarning($"[Characters] Layer {FirstPersonViewModel.Layer} was '{entry.stringValue}', renaming to {FirstPersonViewModel.LayerName}.");
            entry.stringValue = FirstPersonViewModel.LayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildActionAssets()
        {
            EnsureViewModelLayer();
            EnsureFolder(ResourcesDir + "/Props");
            EnsureFolder(ResourcesDir + "/Fx");

            var prop = LoadOrCreateMaterial(PropMaterialPath);
            ConfigureOpaqueMaterial(prop, null, Color.white, 0.22f);

            CreateFxMaterial(FlashMaterialPath, "HideAndSeek/FxAdditive");
            CreateFxMaterial(SmokeMaterialPath, "HideAndSeek/FxAlpha");
        }

        static void CreateFxMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new Exception("Shader not found: " + shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }

            m.shader = shader;
            m.color = Color.white;
            EditorUtility.SetDirty(m);
        }

        static void AddActionShowcase()
        {
            var go = new GameObject("Action Showcase (K D G R H T F, 1-5)");
            go.AddComponent<ActionShowcaseController>();
        }

        // ================================================================== previews

        static string AnimPreviewDir => Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "docs", "previews", "anim");

        [MenuItem("HideAndSeek/Characters/Capture Action Previews")]
        public static void CaptureActionPreviews()
        {
            bool ok = false;
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                BuildAssets();
                ShaderUtil.allowAsyncCompilation = false;
                CaptureActions();
                ok = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = prevAsync;
            }

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        const float Dt = 1f / 60f;

        static void TickAll(float seconds, params BlockyCharacterAnimator[] anims)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Dt)
                foreach (var a in anims)
                    a.Tick(Dt);
        }

        static void LookFrom(Camera cam, Vector3 pos, Vector3 target, float fov)
        {
            cam.fieldOfView = fov;
            cam.transform.position = pos;
            cam.transform.LookAt(target);
        }

        static void SaveAnim(Texture2D tex, string file)
        {
            Directory.CreateDirectory(AnimPreviewDir);
            string path = Path.Combine(AnimPreviewDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[Characters] Action preview: " + path);
        }

        static BlockyCharacter SpawnAt(int skin, Vector3 pos, float yaw, out PreviewMotion motion, out BlockyCharacterAnimator anim)
        {
            var ch = Spawn(skin, pos, out motion, out anim);
            ch.transform.parent.rotation = Quaternion.Euler(0f, yaw, 0f);
            anim.idleLookAround = false;
            return ch;
        }

        static void CaptureActions()
        {
            Directory.CreateDirectory(AnimPreviewDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildStudio(false);
            var camGo = new GameObject("Preview Camera");
            var cam = camGo.AddComponent<Camera>();
            ConfigureCamera(cam);
            cam.enabled = false;
            const int w = 420, h = 480;

            // ---------------------------------------------------------- knife pair
            {
                var s = SpawnAt(1, Vector3.zero, 0f, out _, out var sa);
                var v = SpawnAt(3, new Vector3(0f, 0f, ActionTimeline.KnifeAttackDistance), 180f, out _, out var va);
                s.Actions.SetHeldItem(HeldItem.Knife);
                TickAll(0.5f, sa, va);
                int hits = 0;
                s.Actions.StabHit += i => hits++;
                s.Actions.PlayKnifeAttack(v);
                v.Actions.PlayStabbedReaction();
                v.Actions.SetLifeState(LifeState.Downed);
                float[] shots = { 0.45f, 0.95f, 1.35f, 1.85f, 2.3f, 2.85f, 3.3f };
                var sheet = new Texture2D(shots.Length * w, h * 2, TextureFormat.RGBA32, false);
                float t = 0f;
                Vector3 mid = new Vector3(0f, 0.75f, 0.2f);
                for (int i = 0; i < shots.Length; i++)
                {
                    TickAll(shots[i] - t, sa, va);
                    t = shots[i];
                    LookFrom(cam, new Vector3(4.6f, 1.2f, 0.2f), mid, 32f);
                    Blit(sheet, Render(cam, w, h), i * w, h);
                    LookFrom(cam, new Vector3(-3.0f, 1.9f, -2.4f), mid, 34f);
                    Blit(sheet, Render(cam, w, h), i * w, 0);
                }

                SaveAnim(sheet, "knife_pair_t045_095_135_185_230_285_330.png");
                if (hits != 4) throw new Exception("Expected 4 StabHit events, got " + hits);
                Kill(s);
                Kill(v);
            }

            // ---------------------------------------------------------- downed crawl, dead, get up
            {
                var c = SpawnAt(2, Vector3.zero, 0f, out var m, out var a);
                TickAll(0.3f, a);
                c.Actions.SetLifeState(LifeState.Downed);
                TickAll(1.2f, a);
                var tiles = new List<Texture2D>();
                LookFrom(cam, new Vector3(3.6f, 1.5f, -0.3f), new Vector3(0f, 0.25f, -0.3f), 34f);
                tiles.Add(Render(cam, w, h)); // idle downed side
                LookFrom(cam, new Vector3(2.2f, 2.0f, 2.0f), new Vector3(0f, 0.25f, -0.3f), 34f);
                tiles.Add(Render(cam, w, h)); // idle downed front 3/4
                m.HorizontalSpeed = 0.7f;
                m.Velocity = new Vector3(0f, 0f, 0.7f);
                TickAll(1.0f, a);
                for (int i = 0; i < 3; i++)
                {
                    TickAll(0.27f, a);
                    LookFrom(cam, new Vector3(3.6f, 1.5f, -0.3f), new Vector3(0f, 0.25f, -0.3f), 34f);
                    tiles.Add(Render(cam, w, h));
                }

                LookFrom(cam, new Vector3(2.2f, 2.0f, 2.0f), new Vector3(0f, 0.25f, -0.3f), 34f);
                tiles.Add(Render(cam, w, h));
                m.HorizontalSpeed = 0f;
                m.Velocity = Vector3.zero;
                c.Actions.SetLifeState(LifeState.Dead);
                TickAll(1.5f, a);
                LookFrom(cam, new Vector3(2.4f, 2.2f, 1.2f), new Vector3(0f, 0.2f, -0.3f), 34f);
                tiles.Add(Render(cam, w, h));
                c.Actions.SetLifeState(LifeState.Downed);
                TickAll(1.2f, a);
                c.Actions.SetLifeState(LifeState.Alive);
                TickAll(0.45f, a);
                LookFrom(cam, new Vector3(3.8f, 1.3f, -0.2f), new Vector3(0f, 0.65f, -0.2f), 34f);
                tiles.Add(Render(cam, w, h));
                TickAll(0.35f, a);
                tiles.Add(Render(cam, w, h));
                TickAll(1.0f, a);
                tiles.Add(Render(cam, w, h));
                var sheet = new Texture2D(tiles.Count * w, h, TextureFormat.RGBA32, false);
                for (int i = 0; i < tiles.Count; i++) Blit(sheet, tiles[i], i * w, 0);
                SaveAnim(sheet, "downed_idle2_crawl4_dead_getup3.png");
                Kill(c);
            }

            // ---------------------------------------------------------- glock: hold idle, walking, shoot, reload
            {
                var g = SpawnAt(0, Vector3.zero, 0f, out var m, out var a);
                g.Actions.SetHeldItem(HeldItem.Glock);
                TickAll(0.8f, a);
                var tiles = new List<Texture2D>();
                LookFrom(cam, new Vector3(1.6f, 1.5f, 2.4f), new Vector3(0f, 1.0f, 0f), 32f);
                tiles.Add(Render(cam, w, h));
                LookFrom(cam, new Vector3(3f, 1.2f, 0f), new Vector3(0f, 0.95f, 0f), 32f);
                tiles.Add(Render(cam, w, h));
                m.HorizontalSpeed = 1.6f;
                m.Velocity = new Vector3(0f, 0f, 1.6f);
                TickAll(1.3f, a);
                tiles.Add(Render(cam, w, h));
                m.HorizontalSpeed = 0f;
                m.Velocity = Vector3.zero;
                TickAll(0.8f, a);
                g.Actions.PlayShoot();
                var flash = MuzzleFlash.Spawn(g.Actions.MuzzlePoint, false);
                TickAll(1f / 60f, a);
                if (flash != null) flash.Step(0f);
                LookFrom(cam, new Vector3(2.2f, 1.4f, 1.6f), new Vector3(0f, 1.1f, 0.3f), 32f);
                tiles.Add(Render(cam, w, h));
                if (flash != null) Object.DestroyImmediate(flash.gameObject);
                TickAll(1.5f, a);
                g.Actions.PlayReload();
                float t = 0f;
                foreach (float shot in new[] { 0.3f, 0.6f, 0.86f, 1.15f })
                {
                    TickAll(shot - t, a);
                    t = shot;
                    LookFrom(cam, new Vector3(1.2f, 1.45f, 1.9f), new Vector3(0f, 1.0f, 0.2f), 30f);
                    tiles.Add(Render(cam, w, h));
                }

                var sheet = new Texture2D(tiles.Count * w, h, TextureFormat.RGBA32, false);
                for (int i = 0; i < tiles.Count; i++) Blit(sheet, tiles[i], i * w, 0);
                SaveAnim(sheet, "glock_hold34_side_walk_shoot_reload4.png");

                // Knife and medkit holds, hit flinch.
                var tiles2 = new List<Texture2D>();
                g.Actions.SetHeldItem(HeldItem.Knife);
                TickAll(1f, a);
                LookFrom(cam, new Vector3(1.6f, 1.5f, 2.4f), new Vector3(0f, 1.0f, 0f), 32f);
                tiles2.Add(Render(cam, w, h));
                g.Actions.SetHeldItem(HeldItem.Medkit);
                TickAll(1f, a);
                tiles2.Add(Render(cam, w, h));
                g.Actions.PlayHitReaction();
                TickAll(0.08f, a);
                tiles2.Add(Render(cam, w, h));
                var sheet2 = new Texture2D(tiles2.Count * w, h, TextureFormat.RGBA32, false);
                for (int i = 0; i < tiles2.Count; i++) Blit(sheet2, tiles2[i], i * w, 0);
                SaveAnim(sheet2, "hold_knife_medkit_hitflinch.png");
                Kill(g);
            }

            // ---------------------------------------------------------- revive
            {
                var p = SpawnAt(4, Vector3.zero, 0f, out _, out var pa);
                p.Actions.SetLifeStateImmediate(LifeState.Downed);
                var md = SpawnAt(2, new Vector3(-0.45f, 0f, -0.3f), 90f, out _, out var ma);
                md.Actions.SetHeldItem(HeldItem.Medkit);
                TickAll(0.5f, pa, ma);
                md.Actions.SetReviving(true);
                TickAll(1.0f, pa, ma);
                var sheet = new Texture2D(2 * w, h, TextureFormat.RGBA32, false);
                LookFrom(cam, new Vector3(-0.2f, 1.7f, 3.0f), new Vector3(-0.2f, 0.35f, -0.4f), 34f);
                Blit(sheet, Render(cam, w, h), 0, 0);
                TickAll(0.3f, pa, ma);
                LookFrom(cam, new Vector3(-3.0f, 1.9f, 1.2f), new Vector3(-0.2f, 0.35f, -0.4f), 34f);
                Blit(sheet, Render(cam, w, h), w, 0);
                SaveAnim(sheet, "revive.png");
                Kill(p);
                Kill(md);
            }

            // ---------------------------------------------------------- first person
            {
                var eye = new GameObject("FppCamera");
                var main = eye.AddComponent<Camera>();
                ConfigureCamera(main);
                main.enabled = false;
                main.fieldOfView = 70f;
                main.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -2.5f), Quaternion.Euler(8f, 0f, 0f));
                var vm = FirstPersonViewModel.Create(main.transform, 0, null);
                vm.autoUpdate = false;
                var vmCam = vm.ViewModelCamera;
                vmCam.enabled = false;
                // A wall right in front of the camera: the arms must still render on top.
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 1.5f, -2.5f + 0.35f);
                wall.transform.localScale = new Vector3(3f, 3f, 0.1f);
                wall.SetActive(false);
                var target = SpawnAt(3, new Vector3(0f, 0f, -1.2f), 180f, out _, out var ta);
                TickAll(0.5f, ta);

                var tiles = new List<Texture2D>();
                void Shot() => tiles.Add(RenderFpp(main, vmCam, 640, 400));
                void Run(float s)
                {
                    for (float x = 0f; x < s - 1e-4f; x += Dt) vm.Tick(Dt);
                }

                vm.SetHeldItem(HeldItem.Glock);
                Run(1f);
                Shot();
                vm.PlayShoot();
                Run(Dt);
                var flash = MuzzleFlash.Spawn(vm.MuzzlePoint, true);
                Shot();
                if (flash != null) Object.DestroyImmediate(flash.gameObject);
                Run(0.6f);
                vm.PlayReload();
                Run(0.3f);
                Shot();
                Run(0.4f);
                Shot();
                Run(0.45f);
                Shot();
                Run(1f);
                vm.SetHeldItem(HeldItem.Knife);
                Run(1f);
                Shot();
                vm.PlayKnifeAttack();
                Run(0.45f);
                Shot();
                Run(0.5f);
                Shot();
                Run(2f);
                vm.SetHeldItem(HeldItem.Medkit);
                Run(1f);
                Shot();
                wall.SetActive(true);
                vm.SetHeldItem(HeldItem.Glock);
                Run(1f);
                Shot();

                const int cols = 4;
                int rows = (tiles.Count + cols - 1) / cols;
                var sheet = new Texture2D(cols * 640, rows * 400, TextureFormat.RGBA32, false);
                for (int i = 0; i < tiles.Count; i++) Blit(sheet, tiles[i], (i % cols) * 640, (rows - 1 - i / cols) * 400);
                SaveAnim(sheet, "fpp_glock_shoot_reload3_knife_stab2_medkit_wall.png");
                Object.DestroyImmediate(eye);
                Object.DestroyImmediate(wall);
                Kill(target);
            }

            // ---------------------------------------------------------- prop close-up
            {
                var root = new GameObject("Props");
                var items = new[] { HeldItem.Knife, HeldItem.Glock, HeldItem.Medkit };
                for (int i = 0; i < items.Length; i++)
                {
                    var p = BlockyProps.Create(items[i], root.transform, 0);
                    p.transform.position = new Vector3((i - 1) * 0.55f, 1.0f, 0f);
                    p.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                }

                LookFrom(cam, new Vector3(0.6f, 1.5f, 1.8f), new Vector3(0f, 0.95f, 0f), 30f);
                SaveAnim(Render(cam, 900, 500), "props_knife_glock_medkit.png");
                Object.DestroyImmediate(root);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log("[Characters] Action previews written to " + AnimPreviewDir);
        }

        static Texture2D RenderFpp(Camera main, Camera vm, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            main.allowMSAA = false;
            vm.allowMSAA = false;
            main.allowHDR = false;
            vm.allowHDR = false;
            Debug.Log("[Characters] FPP render");
            var prev = RenderTexture.active;
            main.targetTexture = rt;
            main.aspect = w / (float)h;
            main.Render();
            vm.targetTexture = rt;
            vm.aspect = w / (float)h;
            vm.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            main.targetTexture = null;
            vm.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
