using System;
using System.IO;
using HideAndSeek.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HideAndSeek.Characters.Editor
{
    /// <summary>
    /// Records action animations frame by frame (30 fps) in the lit preview studio, one folder of PNGs per clip, for
    /// review sheets and videos. Command line (no -nographics):
    ///   set HNS_REC_OUT=C:\out &amp; set HNS_REC=kill:ThroatSlit,walk,heal_self
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod HideAndSeek.Characters.Editor.CharacterBuildMenu.RecordAnimations
    /// HNS_REC_STEP=N writes every N-th frame only, HNS_REC_SIZE=640x400.
    /// </summary>
    public static partial class CharacterBuildMenu
    {
        const float RecDt = 1f / 30f;
        static string recOut;
        static int recW = 640, recH = 400, recStep = 1;
        static Camera recCam;

        public static void RecordAnimations()
        {
            bool ok = false;
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            var prevSim = Physics.simulationMode;
            try
            {
                recOut = Environment.GetEnvironmentVariable("HNS_REC_OUT");
                if (string.IsNullOrEmpty(recOut)) recOut = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Logs", "rec");
                string list = Environment.GetEnvironmentVariable("HNS_REC") ?? "walk";
                if (int.TryParse(Environment.GetEnvironmentVariable("HNS_REC_STEP"), out int st) && st > 0) recStep = st;
                var size = Environment.GetEnvironmentVariable("HNS_REC_SIZE");
                if (!string.IsNullOrEmpty(size) && size.Contains("x"))
                {
                    var parts = size.Split('x');
                    int.TryParse(parts[0], out recW);
                    int.TryParse(parts[1], out recH);
                }
                BuildAssets();
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildStudio(false);
                Physics.simulationMode = SimulationMode.Script;
                var floorCol = new GameObject("RecFloor").AddComponent<BoxCollider>();
                floorCol.size = new Vector3(60f, 0.2f, 60f);
                floorCol.center = new Vector3(0f, -0.1f, 0f);
                recCam = new GameObject("Rec Camera").AddComponent<Camera>();
                ConfigureCamera(recCam);
                recCam.enabled = false;
                foreach (var raw in list.Split(','))
                {
                    string item = raw.Trim();
                    if (item.Length == 0) continue;
                    try { RecordItem(item); }
                    catch (Exception e) { Debug.LogException(e); }
                }
                ok = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = prevAsync;
                Physics.simulationMode = prevSim; // never leave Script mode in the project settings
            }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>One clip: ticks the characters at 30 fps for <paramref name="seconds"/>, the camera callback aims every frame.</summary>
        sealed class Clip
        {
            public string Dir;
            public int Frame, Tick;

            public void Shot(Camera cam)
            {
                if (Tick++ % recStep != 0) return;
                var tex = Render(cam, recW, recH);
                File.WriteAllBytes(Path.Combine(Dir, $"f_{Frame++:0000}.png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static Clip NewClip(string name)
        {
            var c = new Clip { Dir = Path.Combine(recOut, name.Replace(':', '_')) };
            if (Directory.Exists(c.Dir)) Directory.Delete(c.Dir, true);
            Directory.CreateDirectory(c.Dir);
            Debug.Log("[Rec] clip " + c.Dir);
            return c;
        }

        static void Orbit(Vector3 target, float yawDeg, float dist, float height, float fov = 34f)
        {
            float a = yawDeg * Mathf.Deg2Rad;
            LookFrom(recCam, target + new Vector3(Mathf.Sin(a) * dist, height, Mathf.Cos(a) * dist), target, fov);
        }

        static void RecordItem(string item)
        {
            if (item.StartsWith("emote:")) { RecordEmote(item.Substring(6)); return; }
            if (item.StartsWith("kill:"))
            {
                if (Enum.TryParse<KillMove>(item.Substring(5), out var move)) RecordKill(move);
                return;
            }
            RecordMisc(item);
        }

        static void RecordKill(KillMove move)
        {
            bool saw = KillMoves.IsSaw(move), glock = move == KillMove.GlockFinisher;
            float dist = glock ? 2.3f : ActionTimeline.KnifeAttackDistance;
            var s = SpawnAt(1, Vector3.zero, 0f, out _, out var sa);
            var v = SpawnAt(3, new Vector3(0f, 0f, dist), 180f, out _, out var va);
            s.Actions.SetHeldItem(saw ? HeldItem.Chainsaw : glock ? HeldItem.Glock : HeldItem.Knife);
            TickAll(saw ? 1.6f : 0.5f, sa, va);
            int hits = 0;
            s.Actions.KillHit += i =>
            {
                hits++;
                int slot = KillMoves.SawSlot(move, i);
                int cut = slot >= 0 ? s.Actions.SawFinisherLimb(slot) : KillMoves.SeveredAtHit(move, i);
                if (cut != 0 && v.Sever(cut, new Vector3(0.6f, 1.4f, 0.8f))) RecStumps(v, cut);
                if (KillMoves.Kind(move, i) != KillHitKind.Grab) v.Actions.PlayHitReaction();
            };
            s.Actions.PlayKill(move, v);
            v.Actions.PlayKillReaction(move, s);
            float len = KillMoves.SequenceDuration(move) + 0.6f;
            bool set = false;
            var clip = NewClip("kill_" + move);
            Vector3 mid = new Vector3(0f, 0.75f, dist * 0.5f);
            for (float t = 0f; t < len; t += RecDt)
            {
                sa.Tick(RecDt);
                va.Tick(RecDt);
                Physics.Simulate(RecDt);
                if (!set && t >= KillMoves.Duration(move))
                {
                    set = true;
                    v.Actions.SetLifeState(KillMoves.EndsDead(move) ? LifeState.Dead : LifeState.Downed);
                }
                float x = t / len;
                if (float.TryParse(Environment.GetEnvironmentVariable("HNS_REC_YAW"), out float yaw)) Orbit(mid, yaw, 2.6f, 1.6f, 40f);
                else Orbit(mid, Mathf.Lerp(62f, 118f, x), glock ? 4.4f : 3.3f, 0.65f, glock ? 38f : 36f);
                clip.Shot(recCam);
            }
            int want = KillMoves.Events(move).Length;
            Debug.Log($"[Rec] {move}: {hits}/{want} events, {KillMoves.Duration(move):0.00} s");
            Kill(s);
            Kill(v);
            ClearGibs();
        }

        static Material recStumpMat;

        /// <summary>Raw cut caps like in the game (LimbStumps, plain red material instead of the blood shader).</summary>
        static void RecStumps(BlockyCharacter c, int mask)
        {
            if (recStumpMat == null)
            {
                recStumpMat = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.06f, 0.06f) };
                recStumpMat.SetFloat("_Glossiness", 0.7f);
            }
            for (int bit = 1; bit <= Limb.Head; bit <<= 1)
                if ((mask & bit) != 0) LimbStumps.Create(c, bit, recStumpMat);
        }

        /// <summary>Removes the cut-off parts lying around after a clip.</summary>
        static void ClearGibs()
        {
            foreach (var rb in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                if (rb != null) UnityEngine.Object.DestroyImmediate(rb.gameObject);
        }
    }
}
