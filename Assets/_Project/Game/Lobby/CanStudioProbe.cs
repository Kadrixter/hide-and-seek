using System.Collections;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Opt-in close-up renders of the soda can models: -canshots &lt;folder&gt; renders all brands from the front,
    /// the back, the side, the lid (closed / open) and crushed, on a neutral studio backdrop, then quits.
    /// </summary>
    public sealed class CanStudioProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-canshots");
            if (i < 0 || i + 1 >= a.Length) return;
            var g = new GameObject("CanStudioProbe");
            DontDestroyOnLoad(g);
            g.AddComponent<CanStudioProbe>().dir = a[i + 1];
        }

        string dir;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            System.IO.Directory.CreateDirectory(dir);
            var root = new GameObject("CanStudio").transform;
            root.position = new Vector3(0f, -500f, 0f);
            const int layer = 31;
            // Backdrop + lights.
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.layer = layer;
            back.transform.SetParent(root, false);
            back.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            back.transform.localScale = new Vector3(6f, 3f, 1f);
            var bm = new Material(Shader.Find("Standard")) { color = new Color(0.32f, 0.34f, 0.37f) };
            back.GetComponent<Renderer>().sharedMaterial = bm;
            Light L(Vector3 pos, float intensity, Color c)
            {
                var l = new GameObject("L").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.localPosition = pos;
                l.type = LightType.Point; l.range = 6f; l.intensity = intensity; l.color = c;
                l.cullingMask = 1 << layer;
                return l;
            }
            L(new Vector3(-1.2f, 0.9f, -1.6f), 2.2f, new Color(1f, 0.97f, 0.92f));
            L(new Vector3(1.6f, 0.4f, -1.0f), 1.0f, new Color(0.8f, 0.88f, 1f));
            L(new Vector3(0f, 1.5f, 0.6f), 1.2f, Color.white);

            var cam = new GameObject("CanCam").AddComponent<Camera>();
            cam.transform.SetParent(root, false);
            cam.enabled = false;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.21f, 0.23f);
            cam.nearClipPlane = 0.01f;
            cam.allowMSAA = true;
            var rt = new RenderTexture(1600, 600, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.fieldOfView = 24f;

            // Row of all brands, 2.2x like in the hand.
            var cans = new SodaCan[SodaCanArt.BrandCount];
            for (int b = 0; b < cans.Length; b++)
            {
                cans[b] = SodaCan.Create(root, b, layer, 2.2f);
                cans[b].transform.localPosition = new Vector3((b - 3.5f) * 0.19f, 0f, 0f);
            }
            void Shot(string name, float yaw, Vector3 camPos, Vector3 look, float open, float crush)
            {
                foreach (var c in cans)
                {
                    c.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    c.SetOpen(open);
                    c.SetCrush(crush);
                }
                cam.transform.localPosition = camPos;
                cam.transform.LookAt(root.TransformPoint(look));
                cam.Render();
                Save(rt, dir + "/" + name + ".png");
            }
            yield return null;
            Shot("front", 0f, new Vector3(0f, 0.05f, -3.2f), new Vector3(0f, 0f, 0f), 0f, 0f);
            Shot("back", 180f, new Vector3(0f, 0.05f, -3.2f), new Vector3(0f, 0f, 0f), 0f, 0f);
            Shot("side", 90f, new Vector3(0f, 0.05f, -3.2f), new Vector3(0f, 0f, 0f), 0f, 0f);
            Shot("top_closed", 0f, new Vector3(0f, 1.6f, -2.2f), new Vector3(0f, 0.12f, 0f), 0f, 0f);
            Shot("top_open", 0f, new Vector3(0f, 1.6f, -2.2f), new Vector3(0f, 0.12f, 0f), 1f, 0f);
            Shot("crushed", 30f, new Vector3(0f, 0.3f, -3.2f), new Vector3(0f, 0f, 0f), 1f, 1f);

            // Close-ups of one can.
            foreach (var c in cans) c.gameObject.SetActive(false);
            var hero = cans[0];
            hero.gameObject.SetActive(true);
            hero.transform.localPosition = Vector3.zero;
            var rt2 = new RenderTexture(900, 900, 24) { antiAliasing = 8 };
            cam.targetTexture = rt2;
            for (int b = 0; b < cans.Length; b++)
            {
                hero.Brand = b;
                hero.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
                hero.SetOpen(0f); hero.SetCrush(0f);
                cam.transform.localPosition = new Vector3(0.15f, 0.2f, -1.15f);
                cam.transform.LookAt(root.TransformPoint(new Vector3(0f, 0.02f, 0f)));
                cam.Render();
                Save(rt2, dir + "/hero_" + b + ".png");
            }
            // Crushed close-up.
            hero.Brand = 4; hero.SetOpen(1f); hero.SetCrush(1f);
            hero.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
            cam.transform.localPosition = new Vector3(0.15f, 0.2f, -1.15f);
            cam.transform.LookAt(root.TransformPoint(new Vector3(0f, 0.0f, 0f)));
            cam.Render();
            Save(rt2, dir + "/hero_crushed.png");
            hero.SetCrush(0f);
            // Tab closed close-up.
            hero.Brand = 2; hero.SetOpen(0f);
            hero.transform.localRotation = Quaternion.identity;
            cam.transform.localPosition = new Vector3(0f, 0.55f, -0.32f);
            cam.transform.LookAt(root.TransformPoint(new Vector3(0f, 0.13f, 0f)));
            cam.Render();
            Save(rt2, dir + "/lid_closed_close.png");
            hero.Brand = 2;
            hero.SetOpen(1f);
            hero.transform.localRotation = Quaternion.identity;
            cam.transform.localPosition = new Vector3(0f, 0.55f, -0.32f);
            cam.transform.LookAt(root.TransformPoint(new Vector3(0f, 0.13f, 0f)));
            cam.Render();
            Save(rt2, dir + "/lid_close.png");
            Debug.Log("[CanStudio] saved");
            Application.Quit();
        }

        static void Save(RenderTexture rt, string path)
        {
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var img = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            img.Apply();
            System.IO.File.WriteAllBytes(path, img.EncodeToPNG());
            RenderTexture.active = old;
            Destroy(img);
        }
    }
}
