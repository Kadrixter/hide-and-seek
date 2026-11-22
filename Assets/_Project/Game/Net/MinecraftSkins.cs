using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HideAndSeek.Characters;
using UnityEngine;
using UnityEngine.Networking;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Downloads a player's skin by Minecraft nick (Mojang API: name -> UUID -> profile -> texture PNG).
    /// Every client downloads skins itself, so only the nick travels over the game connection.
    /// Results are cached in memory and on disk (works offline after the first download).
    /// </summary>
    public static class MinecraftSkins
    {
        private static readonly Dictionary<string, Texture2D> Memory = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<Action<Texture2D, string>>> Pending = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex ValidNick = new("^[A-Za-z0-9_]{3,16}$");

        public static bool IsValidNick(string nick) => !string.IsNullOrEmpty(nick) && ValidNick.IsMatch(nick);

        private static string CacheDir => Path.Combine(UserFiles.Root, "mcskins");

        /// <summary>Calls back with the prepared 64x64 texture, or null + error message (in Polish).</summary>
        public static void Get(string nick, Action<Texture2D, string> done)
        {
            if (!IsValidNick(nick))
            {
                done?.Invoke(null, "Nieprawidłowy nick (3-16 znaków: litery, cyfry, _).");
                return;
            }
            if (Memory.TryGetValue(nick, out var cached) && cached != null)
            {
                done?.Invoke(cached, null);
                return;
            }
            if (Pending.TryGetValue(nick, out var waiting))
            {
                waiting.Add(done);
                return;
            }
            Pending[nick] = new List<Action<Texture2D, string>> { done };
            Fx.Runner.StartCoroutine(Download(nick));
        }

        private static void Finish(string nick, Texture2D tex, string error)
        {
            if (tex != null) Memory[nick] = tex;
            if (!Pending.TryGetValue(nick, out var list)) return;
            Pending.Remove(nick);
            foreach (var cb in list) cb?.Invoke(tex, error);
        }

        private static IEnumerator Download(string nick)
        {
            string file = Path.Combine(CacheDir, nick.ToLowerInvariant() + ".png");

            // 1) name -> uuid
            string uuid = null, error = null;
            using (var req = UnityWebRequest.Get("https://api.mojang.com/users/profiles/minecraft/" + nick))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
                    uuid = Match(req.downloadHandler.text, "\"id\"\\s*:\\s*\"([0-9a-fA-F]{32})\"");
                else if (req.responseCode == 204 || req.responseCode == 404)
                    error = $"Nie ma gracza Minecraft o nicku {nick}.";
                else
                    error = "Brak połączenia z serwerami Minecrafta.";
            }

            // 2) uuid -> profile (base64 textures) -> skin url
            string url = null;
            if (uuid != null)
            {
                using var req = UnityWebRequest.Get("https://sessionserver.mojang.com/session/minecraft/profile/" + uuid);
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    string value = Match(req.downloadHandler.text, "\"value\"\\s*:\\s*\"([A-Za-z0-9+/=]+)\"");
                    if (value != null)
                    {
                        string json = Encoding.UTF8.GetString(Convert.FromBase64String(value));
                        url = Match(json, "\"SKIN\"\\s*:\\s*\\{\\s*\"url\"\\s*:\\s*\"([^\"]+)\"");
                    }
                    if (url == null) error = $"{nick} używa domyślnego skina Minecrafta.";
                }
                else error = "Nie udało się pobrać profilu gracza.";
            }

            // 3) png
            Texture2D tex = null;
            if (url != null)
            {
                if (url.StartsWith("http://")) url = "https://" + url.Substring(7);
                using var req = UnityWebRequest.Get(url);
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    byte[] png = req.downloadHandler.data;
                    tex = Load(png, nick);
                    if (tex != null)
                    {
                        try
                        {
                            Directory.CreateDirectory(CacheDir);
                            File.WriteAllBytes(file, png);
                        }
                        catch (Exception e) { Debug.LogWarning($"[MinecraftSkins] cache write: {e.Message}"); }
                    }
                    else error = "Skin ma nieobsługiwany format.";
                }
                else error = "Nie udało się pobrać pliku skina.";
            }

            // Offline fallback: last downloaded copy.
            if (tex == null && File.Exists(file))
            {
                tex = Load(File.ReadAllBytes(file), nick);
                if (tex != null) error = null;
            }

            if (tex == null) Debug.Log($"[MinecraftSkins] {nick}: {error}");
            Finish(nick, tex, error);
        }

        private static Texture2D Load(byte[] png, string nick)
        {
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!raw.LoadImage(png)) return null;
            var tex = CustomSkins.Prepare(raw, "mc_" + nick);
            if (tex != raw) UnityEngine.Object.Destroy(raw);
            return tex;
        }

        private static string Match(string text, string pattern)
        {
            var m = Regex.Match(text ?? "", pattern);
            return m.Success ? m.Groups[1].Value : null;
        }
    }

    /// <summary>Coroutine host for static helpers.</summary>
    internal static class Fx
    {
        private static MonoBehaviour _runner;

        public static MonoBehaviour Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("[NetFx]");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<RunnerBehaviour>();
                }
                return _runner;
            }
        }

        private class RunnerBehaviour : MonoBehaviour { }
    }
}
