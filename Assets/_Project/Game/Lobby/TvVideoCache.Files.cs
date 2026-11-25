using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Lobby TV downloads: the cache folder's housekeeping (size limit, YouTube's "dash" files, deleting).</summary>
    public static partial class TvVideoCache
    {
        /// <summary>Over <see cref="MaxBytes"/>: deletes the videos used longest ago (not the ones of this session).</summary>
        private static void Trim()
        {
            try
            {
                var files = new List<FileInfo>();
                long total = 0;
                foreach (var f in new DirectoryInfo(Dir).GetFiles("*.mp4"))
                {
                    if (f.Name.Contains(".dl")) continue;
                    files.Add(f);
                    total += f.Length;
                }
                if (total <= MaxBytes) return;
                files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                foreach (var f in files)
                {
                    if (total <= MaxBytes * 0.9) break;
                    lock (All) if (All.ContainsKey(System.IO.Path.GetFileNameWithoutExtension(f.Name))) continue;
                    try { f.Delete(); total -= f.Length; } catch { /* playing in another copy */ }
                }
            }
            catch (Exception e) { Debug.Log("[Tv] cache trim: " + e.Message); }
        }

        /// <summary>The file still has YouTube's "dash" brand (ftyp), which Media Foundation will not open.</summary>
        private static bool IsDash(string f)
        {
            try
            {
                using var s = File.OpenRead(f);
                var b = new byte[12];
                return s.Read(b, 0, 12) == 12 && Encoding.ASCII.GetString(b, 4, 8) == "ftypdash";
            }
            catch { return false; }
        }

        private static void TryDelete(string f)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { /* in use: the next start removes it */ }
        }
    }
}
