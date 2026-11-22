using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game.Editor
{
    public static partial class MultiplayerBuilder
    {
        /// <summary>
        /// Next to the exe: version.txt (read by Updater.exe) and Updater.exe compiled from &lt;project&gt;/Updater
        /// (csc from .NET Framework 4.8, part of Windows). A failed updater build only warns - the game still works.
        /// </summary>
        private static void WriteUpdaterFiles(string outDir)
        {
            File.WriteAllText(Path.Combine(outDir, "version.txt"), PlayerSettings.bundleVersion);
            string script = Path.GetFullPath("Updater/build.ps1");
            if (!File.Exists(script)) { Debug.LogWarning("[MultiplayerBuilder] Updater/build.ps1 missing, no Updater.exe"); return; }
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Out \"{Path.Combine(outDir, "Updater.exe")}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            string output = p!.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode == 0) Debug.Log($"[MultiplayerBuilder] {output.Trim()}");
            else Debug.LogWarning($"[MultiplayerBuilder] Updater build failed: {output}");
        }
    }
}
