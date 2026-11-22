using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows.Forms;

namespace HideAndSeekUpdater
{
    /// <summary>Window: versions, release notes, "full package" option, progress, Aktualizuj / Zamknij.</summary>
    internal class UpdaterForm : Form
    {
        private readonly Options _o;
        private readonly Label _versions, _status;
        private readonly TextBox _notes;
        private readonly CheckBox _full;
        private readonly ProgressBar _bar;
        private readonly Button _go, _close;
        private ReleaseInfo _release;
        private bool _busy, _finished;

        private static readonly Color Bg = Color.FromArgb(14, 17, 16), Panel = Color.FromArgb(28, 33, 31), Fg = Color.FromArgb(225, 230, 226),
            Dim = Color.FromArgb(140, 150, 145), Accent = Color.FromArgb(70, 200, 110);

        public UpdaterForm(Options o)
        {
            _o = o;
            Text = "Hide and Seek - aktualizacja";
            ClientSize = new Size(560, 400);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Segoe UI", 10f);
            try { Icon = Icon.ExtractAssociatedIcon(Path.Combine(o.Dir, Program.GameExe)); } catch { }

            var title = new Label { Text = "HIDE AND SEEK", Font = new Font("Segoe UI", 18f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 14) };
            _versions = new Label { Text = "Sprawdzam wersję...", ForeColor = Dim, AutoSize = false, Location = new Point(22, 54), Size = new Size(516, 22) };
            _notes = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                BackColor = Panel, ForeColor = Fg, Location = new Point(22, 84), Size = new Size(516, 150), TabStop = false,
            };
            _full = new CheckBox
            {
                Text = "Pełna wersja (z ffmpeg i yt-dlp do radia)", AutoSize = true, Location = new Point(22, 246),
                ForeColor = Fg, Checked = o.Full,
            };
            _bar = new ProgressBar { Location = new Point(22, 282), Size = new Size(516, 18), Maximum = 1000 };
            _status = new Label { Text = "", ForeColor = Dim, AutoSize = false, Location = new Point(22, 306), Size = new Size(516, 40) };
            _go = new Button { Text = "Aktualizuj", Enabled = false, Location = new Point(300, 352), Size = new Size(116, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 90, 45) };
            _close = new Button { Text = "Zamknij", Location = new Point(422, 352), Size = new Size(116, 34), FlatStyle = FlatStyle.Flat, BackColor = Panel };
            _go.FlatAppearance.BorderColor = Accent;
            _close.FlatAppearance.BorderColor = Dim;
            _go.Click += delegate { Start(); };
            _close.Click += delegate { Close(); };
            Controls.AddRange(new Control[] { title, _versions, _notes, _full, _bar, _status, _go, _close });
            FormClosing += (s, e) => { if (_busy) e.Cancel = true; };
            Shown += delegate { new Thread(Check) { IsBackground = true }.Start(); };
        }

        private void Ui(Action a)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(a); else a();
        }

        private void Status(string s) { Program.Log(s); Ui(() => _status.Text = s); }

        // ------------------------------------------------------------ check

        private void Check()
        {
            string local = Program.LocalVersion(_o.Dir);
            try
            {
                _release = Program.FetchRelease(_o.Api);
            }
            catch (Exception e)
            {
                Program.Log("check failed: " + e);
                Ui(() =>
                {
                    _versions.Text = "Twoja wersja: " + (local == "" ? "?" : local);
                    _status.Text = "Nie udało się sprawdzić aktualizacji: " + e.Message;
                });
                return;
            }
            bool newer = Program.Compare(_release.Version, local) > 0;
            bool toolsMissing = !Program.ToolsPresent(_o.Dir);
            Ui(() =>
            {
                _versions.Text = "Twoja wersja: " + (local == "" ? "brak" : local) + "     najnowsza: " + _release.Version;
                _notes.Text = _release.Notes.Replace("[full]", "").Replace("[pelna]", "").Replace("[pełna]", "").Trim()
                    .Replace("\n", Environment.NewLine);
                if (_release.Tools == null) { _full.Checked = false; _full.AutoCheck = false; _full.ForeColor = Dim; _full.Text += "  (brak w wydaniach)"; }
                else if (_release.Full || toolsMissing)
                {
                    _full.Checked = true;
                    _full.AutoCheck = false; // forced, but kept readable (a disabled checkbox is grey on black)
                    _full.Text += _release.Full ? "  - wymagana" : "  - brak u ciebie";
                }
                _go.Text = newer ? "Aktualizuj" : "Zainstaluj ponownie";
                _go.Enabled = true;
                _status.Text = newer ? "Dostępna nowa wersja." : "Masz najnowszą wersję.";
                if (_o.Auto && newer) Start();
            });
        }

        // ------------------------------------------------------------ install

        private void Start()
        {
            if (_finished) { StartGame(); Close(); return; }
            if (_busy || _release == null) return;
            _busy = true;
            _go.Enabled = _close.Enabled = false;
            _full.AutoCheck = false;
            bool tools = _full.Checked && _release.Tools != null;
            new Thread(() => Work(tools)) { IsBackground = true }.Start();
        }

        private void Work(bool tools)
        {
            string stage = Path.Combine(_o.Dir, "_update");
            bool ok = false;
            try
            {
                WaitForGame();
                Fs.Delete(stage);
                Directory.CreateDirectory(stage);

                string gameZip = Path.Combine(stage, Program.GameZip);
                Download(_release.Game, gameZip, 0, tools ? 0.3 : 0.9);
                string toolsZip = null;
                if (tools)
                {
                    toolsZip = Path.Combine(stage, Program.ToolsZip);
                    Download(_release.Tools, toolsZip, 0.3, 0.9);
                }

                Status("Rozpakowuję...");
                string gameDir = Path.Combine(stage, "game");
                ZipFile.ExtractToDirectory(gameZip, gameDir);
                string gameRoot = Fs.RootWith(gameDir, Program.GameExe);
                if (gameRoot == null) throw new Exception("W paczce gry nie ma " + Program.GameExe + ".");
                string toolsRoot = null;
                if (toolsZip != null)
                {
                    string td = Path.Combine(stage, "tools");
                    ZipFile.ExtractToDirectory(toolsZip, td);
                    toolsRoot = Fs.RootWith(td, "ffmpeg.exe") ?? Fs.RootWith(Path.Combine(td, "Tools"), "ffmpeg.exe");
                    if (toolsRoot == null) throw new Exception("W paczce narzędzi nie ma ffmpeg.exe.");
                }
                Progress(0.93);

                Status("Podmieniam pliki...");
                Install(gameRoot, toolsRoot, Path.Combine(stage, "backup"));
                Progress(1);
                ok = true;
                Status("Gotowe, wersja " + _release.Version + ".");
            }
            catch (Exception e)
            {
                Program.Log("update failed: " + e);
                Status("Błąd: " + e.Message + "  (szczegóły: Dokumenty\\HideAndSeek\\updater.log)");
            }
            finally
            {
                Fs.Delete(stage);
                _busy = false;
            }

            Ui(() =>
            {
                _close.Enabled = true;
                if (!ok) { _go.Enabled = true; _go.Text = "Spróbuj ponownie"; return; }
                if (_o.Run) { StartGame(); Close(); }
                else { _finished = true; _go.Text = "Uruchom grę"; _go.Enabled = true; }
            });
        }

        private void WaitForGame()
        {
            if (_o.Pid > 0)
            {
                Status("Czekam na zamknięcie gry...");
                try { Process.GetProcessById(_o.Pid).WaitForExit(60000); } catch (ArgumentException) { /* already closed */ }
            }
            string exe = Path.Combine(_o.Dir, Program.GameExe);
            for (int i = 0; i < 120; i++)
            {
                bool running = false;
                foreach (var p in Process.GetProcessesByName("HideAndSeek"))
                {
                    try { if (string.Equals(p.MainModule.FileName, exe, StringComparison.OrdinalIgnoreCase)) running = true; }
                    catch { running = true; } // cannot read the path: assume it is this game
                }
                if (!running) return;
                if (i == 0) Status("Zamknij grę Hide and Seek, aktualizacja ruszy sama...");
                Thread.Sleep(500);
            }
            throw new Exception("Gra nadal działa - zamknij ją i spróbuj ponownie.");
        }

        private void Download(Asset a, string path, double from, double to)
        {
            Status("Pobieram " + a.Name + "...");
            var sw = Stopwatch.StartNew();
            long last = 0;
            Program.DownloadFile(a, path, (done, total) =>
            {
                if (sw.ElapsedMilliseconds - last < 150 && done != total) return;
                last = sw.ElapsedMilliseconds;
                double f = total > 0 ? (double)done / total : 0;
                Progress(from + (to - from) * f);
                double mbs = done / 1048576.0 / Math.Max(0.1, sw.Elapsed.TotalSeconds);
                Ui(() => _status.Text = string.Format("Pobieram {0}: {1:0.0} / {2:0.0} MB  ({3:0.0} MB/s)",
                    a.Name, done / 1048576.0, total / 1048576.0, mbs));
            });
        }

        private void Progress(double f) { Ui(() => _bar.Value = (int)(Math.Max(0, Math.Min(1, f)) * 1000)); }

        /// <summary>
        /// Swaps every top-level entry of the new package into the game folder (old one goes to backup); Tools only when
        /// a tools package was downloaded. Entries that are not in the package are left alone. On error: rollback.
        /// </summary>
        private void Install(string gameRoot, string toolsRoot, string backup)
        {
            Directory.CreateDirectory(backup);
            var done = new List<KeyValuePair<string, string>>(); // destination, its backup ("" = did not exist)
            string self = Path.GetFullPath(Application.ExecutablePath);
            try
            {
                var entries = new List<string>(Directory.GetFileSystemEntries(gameRoot));
                foreach (string src in entries)
                {
                    string name = Path.GetFileName(src);
                    if (name.Equals("Tools", StringComparison.OrdinalIgnoreCase)) continue; // tools come from their own package
                    string dst = Path.Combine(_o.Dir, name);
                    if (string.Equals(Path.GetFullPath(dst), self, StringComparison.OrdinalIgnoreCase))
                    {
                        // Cannot overwrite the running updater: the game swaps Updater.exe.new on its next start.
                        string pending = dst + ".new";
                        Fs.Delete(pending);
                        if (!Fs.SameBytes(src, dst)) File.Copy(src, pending);
                        continue;
                    }
                    Swap(src, dst, backup, done);
                }
                if (toolsRoot != null) Swap(toolsRoot, Path.Combine(_o.Dir, "Tools"), backup, done);
            }
            catch
            {
                Program.Log("rolling back " + done.Count + " entries");
                for (int i = done.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        Fs.Delete(done[i].Key);
                        if (done[i].Value != "") Fs.Move(done[i].Value, done[i].Key);
                    }
                    catch (Exception e) { Program.Log("rollback failed " + done[i].Key + ": " + e.Message); }
                }
                throw;
            }
        }

        private static void Swap(string src, string dst, string backup, List<KeyValuePair<string, string>> done)
        {
            string bak = "";
            if (Fs.Exists(dst))
            {
                bak = Path.Combine(backup, Path.GetFileName(dst));
                Fs.Move(dst, bak);
            }
            done.Add(new KeyValuePair<string, string>(dst, bak));
            Fs.Move(src, dst);
        }

        private void StartGame()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(_o.Dir, Program.GameExe)) { WorkingDirectory = _o.Dir, UseShellExecute = true });
            }
            catch (Exception e) { Program.Log("start game failed: " + e.Message); }
        }
    }
}
