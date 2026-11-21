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

        static KillMove[] KillPreviewList()
        {
            var only = System.Environment.GetEnvironmentVariable("HNS_KILLS"); // e.g. "SawFinisher,Impale" for quick iterations
            if (string.IsNullOrEmpty(only))
                return new[] { KillMove.ThroatSlit, KillMove.Overhead, KillMove.GutKnee, KillMove.Impale, KillMove.Finisher, KillMove.Chainsaw,
                    KillMove.SawChop, KillMove.SawSweep, KillMove.SawUpper, KillMove.SawNeck, KillMove.SawFinisher };
            var list = new System.Collections.Generic.List<KillMove>();
            foreach (var n in only.Split(','))
                if (System.Enum.TryParse<KillMove>(n.Trim(), out var m)) list.Add(m);
            return list.ToArray();
        }

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

            // ---------------------------------------------------------- fork in the socket + arcade
            {
                var c = SpawnAt(4, Vector3.zero, 0f, out _, out var ca);
                c.Actions.SetHeldItem(HeldItem.Fork);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 1f, 0.82f);
                wall.transform.localScale = new Vector3(3f, 2f, 0.1f);
                Vector3 outlet = new Vector3(0.1f, 0.3f, 0.765f);
                TickAll(0.5f, ca);
                float[] st = { 0.3f, 0.6f, 0.88f, 1.0f, 1.6f, 2.4f, 3.1f, 3.5f, 4.4f, 7f };
                var sheet = new Texture2D(st.Length * w, h, TextureFormat.RGBA32, false);
                float t = 0f;
                for (int i = 0; i < st.Length; i++)
                {
                    while (t < st[i] - 1e-4f)
                    {
                        c.Actions.SetShock(t, outlet);
                        ca.Tick(Dt);
                        t += Dt;
                    }
                    LookFrom(cam, new Vector3(2.6f, 1.2f, -0.6f), new Vector3(0f, 0.6f, 0.1f), 40f);
                    Blit(sheet, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(sheet, "fork_shock_t030_060_088_100_160_240_310_350_440_700.png");
                c.Actions.SetShock(-1f, outlet);
                c.Actions.SetHeldItem(HeldItem.None);
                Object.DestroyImmediate(wall);
                TickAll(0.5f, ca);
                var arc = new Texture2D(4 * w, h, TextureFormat.RGBA32, false);
                var dirs = new[] { Vector2.zero, Vector2.left, Vector2.up, Vector2.right };
                Vector3 stick = new Vector3(-0.15f, 1.17f, 0.5f), buttons = new Vector3(0.1f, 1.1f, 0.48f);
                for (int i = 0; i < 4; i++)
                {
                    c.Actions.SetArcade(true, stick + new Vector3(dirs[i].x, 0f, dirs[i].y) * 0.035f, buttons, dirs[i]);
                    TickAll(0.5f, ca);
                    LookFrom(cam, new Vector3(1.6f, 1.6f, 1.4f), new Vector3(0f, 1.05f, 0.35f), 36f);
                    Blit(arc, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(arc, "arcade_idle_left_up_right.png");
                Kill(c);
            }

            // ---------------------------------------------------------- limp (injured) + quick slash
            {
                var c = SpawnAt(2, Vector3.zero, 0f, out var cm, out var ca);
                c.Actions.SetLimp(1f);
                cm.Velocity = new Vector3(0f, 0f, 2.4f);
                cm.HorizontalSpeed = 2.4f;
                TickAll(2f, ca);
                var sheet = new Texture2D(8 * w, h * 2, TextureFormat.RGBA32, false);
                for (int i = 0; i < 8; i++)
                {
                    TickAll(0.11f, ca);
                    LookFrom(cam, new Vector3(3.2f, 1.1f, 0f), new Vector3(0f, 0.85f, 0f), 34f);
                    Blit(sheet, Render(cam, w, h), i * w, h);
                    LookFrom(cam, new Vector3(0.6f, 1.2f, 3.2f), new Vector3(0f, 0.85f, 0f), 34f);
                    Blit(sheet, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(sheet, "limp_walk_side_front.png");
                Kill(c);
                var sk = SpawnAt(1, Vector3.zero, 0f, out _, out var sa2);
                var vi = SpawnAt(3, new Vector3(0f, 0f, 0.95f), 180f, out _, out var va2);
                vi.Actions.SetLimp(1f);
                TickAll(0.4f, sa2, va2);
                sk.Actions.SetHeldItem(HeldItem.Knife);
                TickAll(0.6f, sa2, va2);
                sk.Actions.PlaySlash(vi);
                float[] st = { 0.06f, 0.14f, 0.2f, 0.28f, 0.4f, 0.6f };
                var ss = new Texture2D(st.Length * w, h, TextureFormat.RGBA32, false);
                float tt = 0f;
                for (int i = 0; i < st.Length; i++)
                {
                    if (tt < 0.2f && st[i] >= 0.2f) vi.Actions.PlayHitReaction();
                    TickAll(st[i] - tt, sa2, va2);
                    tt = st[i];
                    LookFrom(cam, new Vector3(2.6f, 1.3f, 0.5f), new Vector3(0f, 1f, 0.48f), 36f);
                    Blit(ss, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(ss, "slash_t006_014_020_028_040_060.png");
                Kill(sk);
                Kill(vi);
            }

            // ---------------------------------------------------------- adrenaline injection / self heal / resurrection gasp
            {
                var helper = SpawnAt(1, Vector3.zero, 0f, out _, out var ha);
                var dead = SpawnAt(3, new Vector3(0.15f, 0f, 0.78f), 90f, out _, out var da);
                dead.Actions.SetLifeStateImmediate(LifeState.Dead);
                TickAll(0.5f, ha, da);
                float[] shots = { 0.3f, 0.85f, 1.15f, 1.5f, 1.72f, 2.4f, 3.0f, 3.3f, 3.9f };
                var sheet = new Texture2D(shots.Length * w, h * 2, TextureFormat.RGBA32, false);
                float t = 0f;
                Vector3 mid = new Vector3(0f, 0.55f, 0.4f);
                for (int i = 0; i < shots.Length; i++)
                {
                    while (t < shots[i] - 1e-4f)
                    {
                        helper.Actions.SetHoldAnimation(HoldAnim.Inject, t, dead.Actions.ChestPoint.position);
                        ha.Tick(Dt);
                        da.Tick(Dt);
                        t += Dt;
                    }
                    LookFrom(cam, new Vector3(2.6f, 1.0f, 0.3f), mid, 34f);
                    Blit(sheet, Render(cam, w, h), i * w, h);
                    LookFrom(cam, new Vector3(-1.4f, 1.6f, 2.6f), mid, 36f);
                    Blit(sheet, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(sheet, "inject_t03_085_115_150_172_240_300_330_390.png");
                // Resurrection gasp of the dead one.
                helper.Actions.SetHoldAnimation(HoldAnim.None, 0f);
                dead.Actions.PrepareResurrectGasp();
                dead.Actions.SetLifeState(LifeState.Alive);
                float[] g = { 0.08f, 0.22f, 0.5f, 0.95f, 1.3f, 1.7f, 2.2f };
                var gs = new Texture2D(g.Length * w, h, TextureFormat.RGBA32, false);
                t = 0f;
                for (int i = 0; i < g.Length; i++)
                {
                    TickAll(g[i] - t, ha, da);
                    t = g[i];
                    LookFrom(cam, new Vector3(2.4f, 1.1f, 1.6f), new Vector3(0.1f, 0.5f, 0.8f), 36f);
                    Blit(gs, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(gs, "resurrect_gasp_t008_022_050_095_130_170_220.png");
                Kill(helper);
                Kill(dead);
            }
            {
                var c = SpawnAt(2, Vector3.zero, 0f, out _, out var ca);
                TickAll(0.3f, ca);
                c.Actions.SetLifeState(LifeState.Downed);
                TickAll(1.3f, ca);
                c.Actions.SetBandaged(0f);
                float[] shots = { 0.35f, 0.9f, 1.25f, 1.6f, 2.0f, 2.45f, 3.0f, 3.6f, 4.4f, 4.85f };
                var sheet = new Texture2D((shots.Length + 2) * w, h * 2, TextureFormat.RGBA32, false);
                float t = 0f;
                for (int i = 0; i < shots.Length; i++)
                {
                    while (t < shots[i] - 1e-4f)
                    {
                        c.Actions.SetHoldAnimation(HoldAnim.SelfHeal, t);
                        ca.Tick(Dt);
                        t += Dt;
                    }
                    LookFrom(cam, new Vector3(1.9f, 1.2f, 0.9f), new Vector3(0f, 0.3f, -0.35f), 36f);
                    Blit(sheet, Render(cam, w, h), i * w, h);
                    LookFrom(cam, new Vector3(0.2f, 1.0f, 1.3f), new Vector3(0f, 0.32f, -0.4f), 34f);
                    Blit(sheet, Render(cam, w, h), i * w, 0);
                }
                // Hold done: stands up (stays bandaged while wounded).
                c.Actions.SetBandaged(1f);
                c.Actions.SetLifeState(LifeState.Alive);
                c.Actions.SetHoldAnimation(HoldAnim.None, 0f);
                TickAll(0.45f, ca);
                LookFrom(cam, new Vector3(1.9f, 1.3f, 0.9f), new Vector3(0f, 0.5f, -0.2f), 36f);
                Blit(sheet, Render(cam, w, h), shots.Length * w, 0);
                TickAll(0.9f, ca);
                LookFrom(cam, new Vector3(2.2f, 1.3f, 1.2f), new Vector3(0f, 0.8f, 0f), 34f);
                Blit(sheet, Render(cam, w, h), (shots.Length + 1) * w, 0);
                SaveAnim(sheet, "selfheal_t035_090_125_160_200_245_300_360_440_485_up045_up135.png");
                Kill(c);
            }

            // ---------------------------------------------------------- kill variants + finisher
            foreach (var move in KillPreviewList())
            {
                var s = SpawnAt(1, Vector3.zero, 0f, out _, out var sa);
                var v = SpawnAt(3, new Vector3(0f, 0f, ActionTimeline.KnifeAttackDistance), 180f, out _, out var va);
                s.Actions.SetHeldItem(KillMoves.IsSaw(move) ? HeldItem.Chainsaw : HeldItem.Knife);
                TickAll(0.5f, sa, va);
                int hits = 0;
                var victimChar = v;
                var seekerChar = s;
                s.Actions.KillHit += i =>
                {
                    hits++;
                    int slot = KillMoves.SawSlot(move, i);
                    int cut = slot >= 0 ? seekerChar.Actions.SawFinisherLimb(slot) : KillMoves.SeveredAtHit(move, i);
                    if (cut != 0) victimChar.Sever(cut, new Vector3(0f, 2f, -1.5f));
                };
                s.Actions.PlayKill(move, v);
                v.Actions.PlayKillReaction(move, s);
                v.Actions.SetLifeState(KillMoves.EndsDead(move) ? LifeState.Dead : LifeState.Downed);
                float len = KillMoves.SequenceDuration(move);
                int count = KillMoves.IsFinisher(move) ? 12 : 8;
                var sheet = new Texture2D(count * w, h * 2, TextureFormat.RGBA32, false);
                float t = 0f;
                Vector3 mid = new Vector3(0f, 0.75f, 0.3f);
                var times = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    float shot = 0.3f + (len - 0.3f) * i / (count - 1);
                    TickAll(shot - t, sa, va);
                    t = shot;
                    times.Add(shot.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                    LookFrom(cam, new Vector3(4.6f, 1.2f, 0.3f), mid, 34f);
                    Blit(sheet, Render(cam, w, h), i * w, h);
                    LookFrom(cam, new Vector3(-2.6f, 1.7f, 3.2f), mid, 36f);
                    Blit(sheet, Render(cam, w, h), i * w, 0);
                }

                SaveAnim(sheet, $"kill_{move}_t{string.Join("_", times)}.png");
                int want = KillMoves.HitTimes(move).Length;
                if (hits != want) throw new Exception($"{move}: expected {want} KillHit events, got {hits}");
                Kill(s);
                Kill(v);
            }

            // ---------------------------------------------------------- chainsaw first hit: a leg falls off, hopping, dust
            {
                var s = SpawnAt(1, Vector3.zero, 0f, out _, out var sa);
                var v = SpawnAt(3, new Vector3(0f, 0f, ActionTimeline.KnifeAttackDistance), 180f, out var vm, out var va);
                s.Actions.SetHeldItem(HeldItem.Chainsaw);
                s.Actions.SetRevving(true);
                TickAll(0.5f, sa, va);
                s.Actions.PlaySawSlash(v, Limb.LegR);
                var times = new[] { 0.12f, 0.24f, 0.34f, 0.42f, 0.52f, 0.66f, 0.82f, 1.0f };
                var sheet = new Texture2D(times.Length * w, h * 2, TextureFormat.RGBA32, false);
                float t = 0f;
                bool cut = false;
                for (int i = 0; i < times.Length; i++)
                {
                    TickAll(times[i] - t, sa, va);
                    t = times[i];
                    if (!cut && t >= BlockyCharacterActions.SawSlashHitTime) { v.Sever(Limb.LegR, new Vector3(0f, 2f, -1.5f)); v.Actions.PlayHitReaction(); cut = true; }
                    LookFrom(cam, new Vector3(3.2f, 1.0f, 0.4f), new Vector3(0f, 0.55f, 0.4f), 34f);
                    Blit(sheet, Render(cam, w, h), i * w, h);
                    LookFrom(cam, new Vector3(-1.8f, 1.5f, 2.6f), new Vector3(0f, 0.5f, 0.45f), 36f);
                    Blit(sheet, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(sheet, "saw_leg_slash.png");
                Kill(s);
                Kill(v);

                // One-legged hopping (the leg is already gone).
                var c = SpawnAt(2, Vector3.zero, 0f, out var cm, out var ca);
                c.SetSevered(Limb.LegR);
                c.Actions.SetHop(0);
                cm.Velocity = new Vector3(0f, 0f, 1.0f);
                cm.HorizontalSpeed = 1.0f;
                TickAll(1.5f, ca);
                var hop = new Texture2D(8 * w, h * 2, TextureFormat.RGBA32, false);
                for (int i = 0; i < 8; i++)
                {
                    TickAll(0.13f, ca);
                    LookFrom(cam, new Vector3(3.2f, 1.1f, 0f), new Vector3(0f, 0.85f, 0f), 34f);
                    Blit(hop, Render(cam, w, h), i * w, h);
                    LookFrom(cam, new Vector3(0.6f, 1.2f, 3.2f), new Vector3(0f, 0.85f, 0f), 34f);
                    Blit(hop, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(hop, "hop_one_leg_side_front.png");
                Kill(c);

                // Dust throw.
                var d = SpawnAt(2, Vector3.zero, 0f, out _, out var da);
                TickAll(0.4f, da);
                d.Actions.PlayDust();
                var dust = new Texture2D(8 * w, h * 2, TextureFormat.RGBA32, false);
                float dt0 = 0f;
                var dts = new[] { 0.1f, 0.25f, 0.4f, 0.52f, 0.62f, 0.7f, 0.85f, 1.0f };
                for (int i = 0; i < 8; i++)
                {
                    TickAll(dts[i] - dt0, da);
                    dt0 = dts[i];
                    LookFrom(cam, new Vector3(3.2f, 1.0f, 0.6f), new Vector3(0f, 0.7f, 0.5f), 34f);
                    Blit(dust, Render(cam, w, h), i * w, h);
                    LookFrom(cam, new Vector3(-1.8f, 1.4f, 2.8f), new Vector3(0f, 0.7f, 0.4f), 36f);
                    Blit(dust, Render(cam, w, h), i * w, 0);
                }
                SaveAnim(dust, "dust_throw_t10_25_40_52_62_70_85_100.png");
                Kill(d);
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
