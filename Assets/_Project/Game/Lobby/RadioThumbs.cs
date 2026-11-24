using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace HideAndSeek.Game
{
    /// <summary>
    /// YouTube thumbnails for the boombox panel (i.ytimg.com, hqdefault 480x360 with black bars: drawn cropped to
    /// 16:9 with <see cref="Crop"/>). Loaded once per video, kept for the session; null while loading / when missing.
    /// </summary>
    public sealed class RadioThumbs : MonoBehaviour
    {
        /// <summary>The 16:9 part of a hqdefault picture (UV rect for GUI.DrawTextureWithTexCoords).</summary>
        public static readonly Rect Crop = new(0f, 0.125f, 1f, 0.75f);

        private static RadioThumbs _runner;
        private static readonly Dictionary<string, Texture2D> Loaded = new();
        private static readonly HashSet<string> Asked = new();
        private static int _busy;

        /// <summary>Thumbnail of the link's video (starts loading it the first time).</summary>
        public static Texture2D Get(string url)
        {
            string id = RadioCache.VideoId(url ?? "");
            if (id == null) return null;
            if (Loaded.TryGetValue(id, out var t)) return t;
            if (Asked.Add(id))
            {
                if (_runner == null)
                {
                    var go = new GameObject("[RadioThumbs]") { hideFlags = HideFlags.HideAndDontSave };
                    DontDestroyOnLoad(go);
                    _runner = go.AddComponent<RadioThumbs>();
                }
                _runner.StartCoroutine(Load(id));
            }
            return null;
        }

        private static IEnumerator Load(string id)
        {
            while (_busy >= 3) yield return null;
            _busy++;
            using (var req = UnityWebRequestTexture.GetTexture($"https://i.ytimg.com/vi/{id}/hqdefault.jpg", true))
            {
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var tex = DownloadHandlerTexture.GetContent(req);
                    if (tex != null)
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Trilinear;
                        tex.hideFlags = HideFlags.DontSave;
                        Loaded[id] = tex;
                    }
                }
                else Debug.Log($"[Radio] thumbnail {id}: {req.error}");
            }
            _busy--;
        }
    }
}
