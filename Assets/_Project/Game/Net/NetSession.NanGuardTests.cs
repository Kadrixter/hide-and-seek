using System.Collections;
using HideAndSeek.Game.Gfx;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// -testnanguard &lt;dir&gt; (host in a round, agent blacksq): proves that the full-screen effects no longer spread a NaN
    /// pixel. Puts 14 specks of 3 mm with zero normals (= the old map bug: one NaN pixel each) in front of a lit car and
    /// takes, for several anti-aliasing / bloom settings, the plain view, the Esc menu (blur), the drunk look and the
    /// night vision. Before the fix each speck became a black square (blur) or a cluster of black dots (drunk).
    /// Logs [NanGuard], quits.
    /// </summary>
    public partial class NetSession
    {
        private static readonly (AaMode aa, bool bloom)[] GuardModes =
        {
            (AaMode.Msaa4Smaa, true), (AaMode.Fxaa, true), (AaMode.Smaa, false), (AaMode.Msaa2, false), (AaMode.Off, false),
        };

        private IEnumerator NanGuardTest(string dir)
        {
            yield return WaitForGarageRound();
            var me = NetPlayer.Local;
            if (me.Flashlight != null) me.Flashlight.SetOn(true, false);
            var root = GameObject.Find("ParkingGarage");
            var car = root != null ? root.transform.Find("Cars/Level-1/" + (ArgOr("-blacksqcar", null) ?? "Car_018_Estate_0_White_A19")) : null;
            if (car != null && FreeSide(car, out var feet)) Pose(me, feet, car.position + Vector3.up * 0.35f);
            for (int k = 0; k < 20; k++) yield return null;
            var specks = Specks(me.Camera.transform);
            NanScan.Active = true;
            NanScan.Every = 1;
            NanScan.MaxSaves = 0;
            var hud = typeof(GameHud);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var inst = hud.GetField("_instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
            var show = hud.GetMethod("ShowEscMenu", flags);
            var close = hud.GetMethod("CloseMenus", flags);
            foreach (var (aa, bloom) in GuardModes)
            {
                GfxSettings.TestAa = aa;
                GfxSettings.TestBloom = bloom;
                GfxSettings.Apply();
                string tag = $"{aa}_{(bloom ? "bloom" : "nobloom")}";
                int before = NanScan.BadFrames;
                for (int k = 0; k < 15; k++) yield return null;
                Debug.Log($"[NanGuard] {tag}: frames with NaN in the HDR frame {NanScan.BadFrames - before} / 15, last {NanScan.LastBadCount} px");
                Shot(dir, $"guard_{tag}_1_view");
                yield return new WaitForSeconds(0.3f);
                if (inst != null && show != null) show.Invoke(inst, null);
                yield return new WaitForSeconds(0.8f);
                Shot(dir, $"guard_{tag}_2_escmenu");
                yield return new WaitForSeconds(0.3f);
                if (inst != null && close != null) close.Invoke(inst, null);
                yield return new WaitForSeconds(0.3f);
                var dv = me.Camera.GetComponent<DrunkVision>();
                if (dv == null) dv = me.Camera.gameObject.AddComponent<DrunkVision>();
                dv.enabled = true;
                for (int k = 0; k < 40; k++) { dv.Amount = 1f; dv.Clock = Time.time; yield return null; }
                Shot(dir, $"guard_{tag}_3_drunk");
                yield return null;
                dv.Amount = 0f;
                yield return new WaitForSeconds(0.3f);
                var nv = me.Camera.GetComponent<NightVision>();
                if (nv == null) nv = me.Camera.gameObject.AddComponent<NightVision>();
                nv.enabled = true;
                for (int k = 0; k < 15; k++) yield return null;
                Shot(dir, $"guard_{tag}_4_nightvision");
                yield return null;
                nv.enabled = false;
                yield return new WaitForSeconds(0.3f);
            }
            Destroy(specks);
            Debug.Log($"[NanGuard] done, frames with NaN {NanScan.BadFrames}");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        /// <summary>14 tiny Standard quads with zero normals 1.4 m in front of the camera, in the flashlight spot.</summary>
        private static GameObject Specks(Transform cam)
        {
            var go = new GameObject("NanSpecks");
            go.transform.SetPositionAndRotation(cam.position + cam.forward * 1.4f, cam.rotation);
            var v = new System.Collections.Generic.List<Vector3>();
            var t = new System.Collections.Generic.List<int>();
            var rnd = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                var c = new Vector3((float)rnd.NextDouble() * 0.5f - 0.25f, (float)rnd.NextDouble() * 0.3f - 0.15f, 0f);
                int b = v.Count;
                const float h = 0.0015f;
                v.Add(c + new Vector3(-h, -h)); v.Add(c + new Vector3(-h, h)); v.Add(c + new Vector3(h, h)); v.Add(c + new Vector3(h, -h));
                t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var m = new Mesh { name = "NanSpecks" };
            m.SetVertices(v);
            m.SetNormals(new Vector3[v.Count]);   // all zero: the shader normalises them to NaN
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mat = new Material(Shader.Find("Standard")) { color = Color.white };
            mat.SetFloat("_Glossiness", 0.8f);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
