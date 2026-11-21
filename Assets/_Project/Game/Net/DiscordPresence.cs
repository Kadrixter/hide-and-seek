using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Discord Rich Presence over the Discord client's local IPC pipe (\\.\pipe\discord-ipc-N, the same protocol the
    /// Discord SDKs use): handshake with the application id, then SET_ACTIVITY frames. Runs on a worker thread, so a
    /// missing / closed Discord never stalls the game; reconnects every few seconds. Only sends when the activity
    /// actually changes (Discord rate-limits updates to ~5 per 20 s).
    /// </summary>
    public static class DiscordPresence
    {
        public const string ApplicationId = "1572633943482830940";

        /// <summary>What to show. Built by the game every frame, compared and sent by the worker when it changes.</summary>
        public struct Activity : IEquatable<Activity>
        {
            public string Details, State, LargeText, SmallImage, SmallText, PartyId;
            public int PartySize, PartyMax;
            public long Start, End; // unix seconds (0 = none)

            public bool Equals(Activity o) => Details == o.Details && State == o.State && LargeText == o.LargeText &&
                                              SmallImage == o.SmallImage && SmallText == o.SmallText && PartyId == o.PartyId &&
                                              PartySize == o.PartySize && PartyMax == o.PartyMax && Start == o.Start &&
                                              Math.Abs(End - o.End) <= 2;
        }

        public static bool Connected { get; private set; }
        public static bool Enabled { get; private set; }

        private static Thread _thread;
        private static volatile bool _run;
        private static readonly object Lock = new();
        private static Activity _wanted, _sent;
        private static bool _hasWanted, _clear, _dirty;
        private static int _pid;

        public static void Start()
        {
            if (_thread != null) return;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-nodiscord") >= 0) return;
            _pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            Enabled = true;
            _run = true;
            _thread = new Thread(Worker) { IsBackground = true, Name = "DiscordPresence" };
            _thread.Start();
        }

        public static void Stop()
        {
            _run = false;
            Enabled = false;
        }

        /// <summary>Sets the wanted activity (cheap; call as often as you like).</summary>
        public static void Set(Activity a)
        {
            lock (Lock)
            {
                if (_hasWanted && _wanted.Equals(a) && !_clear) return;
                _wanted = a;
                _hasWanted = true;
                _clear = false;
                _dirty = true;
            }
        }

        /// <summary>Removes the status (user turned it off in the settings).</summary>
        public static void Clear()
        {
            lock (Lock)
            {
                _hasWanted = false;
                _clear = true;
                _dirty = true;
            }
        }

        // ------------------------------------------------------------------ worker

        private static void Worker()
        {
            var pending = new ConcurrentQueue<byte[]>();
            while (_run)
            {
                NamedPipeClientStream pipe = null;
                try
                {
                    pipe = ConnectAny();
                    if (pipe == null)
                    {
                        Sleep(5000);
                        continue;
                    }
                    WriteFrame(pipe, 0, "{\"v\":1,\"client_id\":\"" + ApplicationId + "\"}");
                    var (op, json) = ReadFrame(pipe);
                    if (op != 1 || json == null || !json.Contains("READY"))
                    {
                        Debug.LogWarning("[Discord] handshake failed: " + json);
                        Sleep(10000);
                        continue;
                    }
                    Connected = true;
                    Debug.Log("[Discord] rich presence connected");
                    lock (Lock) _dirty = true; // resend after reconnect
                    float lastSend = -100f;
                    long ticks = 0;
                    while (_run && pipe.IsConnected)
                    {
                        string payload = null;
                        lock (Lock)
                        {
                            // At most one update per 4 s (rate limit), always the newest state.
                            if (_dirty && ticks * 0.1f - lastSend >= 4f)
                            {
                                _dirty = false;
                                payload = _clear || !_hasWanted ? BuildSet(null) : BuildSet(_wanted);
                                if (_hasWanted) _sent = _wanted;
                            }
                        }
                        if (payload != null)
                        {
                            WriteFrame(pipe, 1, payload);
                            var (_, ack) = ReadFrame(pipe); // ack (or error) - keeps the pipe drained
                            if (ack != null && ack.Contains("\"evt\":\"ERROR\"")) Debug.LogWarning("[Discord] SET_ACTIVITY error: " + ack);
                            else Debug.Log("[Discord] status: " + Describe(payload));
                            lastSend = ticks * 0.1f;
                        }
                        Sleep(100);
                        ticks++;
                    }
                }
                catch (Exception e)
                {
                    if (Connected) Debug.Log("[Discord] disconnected: " + e.Message);
                }
                finally
                {
                    Connected = false;
                    try { pipe?.Dispose(); } catch { /* ignore */ }
                }
                Sleep(5000);
            }
        }

        private static void Sleep(int ms)
        {
            for (int i = 0; i < ms / 100 && _run; i++) Thread.Sleep(100);
        }

        private static NamedPipeClientStream ConnectAny()
        {
            for (int i = 0; i < 10; i++)
            {
                var p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.None);
                try
                {
                    p.Connect(300);
                    if (p.IsConnected) return p;
                }
                catch { /* try next */ }
                p.Dispose();
            }
            return null;
        }

        private static void WriteFrame(Stream s, int op, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] frame = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            s.Write(frame, 0, frame.Length);
            s.Flush();
        }

        private static (int op, string json) ReadFrame(Stream s)
        {
            byte[] head = ReadExact(s, 8);
            if (head == null) throw new IOException("pipe closed");
            int op = BitConverter.ToInt32(head, 0), len = BitConverter.ToInt32(head, 4);
            if (len < 0 || len > 1 << 20) throw new IOException("bad frame");
            byte[] body = ReadExact(s, len);
            if (body == null) throw new IOException("pipe closed");
            if (op == 2) throw new IOException("closed by Discord: " + Encoding.UTF8.GetString(body));
            return (op, Encoding.UTF8.GetString(body));
        }

        private static byte[] ReadExact(Stream s, int n)
        {
            var buf = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = s.Read(buf, got, n - got);
                if (r <= 0) return null;
                got += r;
            }
            return buf;
        }

        // ------------------------------------------------------------------ JSON

        private static string BuildSet(Activity? a)
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":").Append(_pid).Append(",\"activity\":");
            if (a == null) sb.Append("null");
            else
            {
                var v = a.Value;
                sb.Append("{\"type\":0");
                if (!string.IsNullOrEmpty(v.Details)) sb.Append(",\"details\":").Append(Str(v.Details));
                if (!string.IsNullOrEmpty(v.State)) sb.Append(",\"state\":").Append(Str(v.State));
                if (v.Start > 0 || v.End > 0)
                {
                    sb.Append(",\"timestamps\":{");
                    if (v.End > 0) sb.Append("\"end\":").Append(v.End);
                    else sb.Append("\"start\":").Append(v.Start);
                    sb.Append('}');
                }
                sb.Append(",\"assets\":{\"large_image\":\"logo\",\"large_text\":").Append(Str(string.IsNullOrEmpty(v.LargeText) ? "Hide and Seek" : v.LargeText));
                if (!string.IsNullOrEmpty(v.SmallImage))
                    sb.Append(",\"small_image\":").Append(Str(v.SmallImage)).Append(",\"small_text\":").Append(Str(v.SmallText ?? ""));
                sb.Append('}');
                if (!string.IsNullOrEmpty(v.PartyId) && v.PartyMax > 0)
                    sb.Append(",\"party\":{\"id\":").Append(Str(v.PartyId)).Append(",\"size\":[").Append(Mathf.Max(1, v.PartySize)).Append(',').Append(v.PartyMax).Append("]}");
                sb.Append(",\"instance\":false}");
            }
            sb.Append("},\"nonce\":\"").Append(Guid.NewGuid().ToString("N")).Append("\"}");
            return sb.ToString();
        }

        private static string Str(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        private static string Describe(string payload)
        {
            int d = payload.IndexOf("\"details\":", StringComparison.Ordinal), st = payload.IndexOf("\"state\":", StringComparison.Ordinal);
            string Cut(int i) => i < 0 ? "" : payload.Substring(i, Math.Min(60, payload.Length - i));
            return payload.Contains("\"activity\":null") ? "(wyczyszczony)" : Cut(d) + " | " + Cut(st);
        }

        // ------------------------------------------------------------------ text helpers

        /// <summary>"z 1 osobą" / "z 3 osobami" (instrumental case).</summary>
        public static string WithPeople(int n) => n == 1 ? "z 1 osobą" : $"z {n} osobami";

    }
}
