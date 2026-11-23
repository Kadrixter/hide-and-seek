"""
Analiza testu -testvoicemode (czat glosowy: klasyczny vs niskie opoznienie).

Uzycie:
  python analyze_voicemode.py <feed.wav> <host.log> <client.log> <vm_client.wav> <vm_client_stamps.txt> <out_dir> [ffmpeg.exe]

Wejscie: nagranie podawane zamiast mikrofonu (16 kHz mono), logi hosta (t0 kazdego wariantu) i klienta (liczniki
strumienia co 0.5 s), nagranie zrodla glosu u klienta (wyjscie AudioSource, 48 kHz stereo) ze stemplami UTC blokow.
Wyjscie: wycinek kazdego wariantu (wav, opcjonalnie mp3) i tabela: opoznienie (od "zlapania" probki przez mikrofon
do miksera Unity, bez bufora karty dzwiekowej), dziury (liczniki odbiorcy), poziom RMS / szczyt, udzial pasma 8-20 kHz.
"""
import os, re, sys, subprocess, wave
import numpy as np

TICKS = 10_000_000


def read_wav(path):
    with wave.open(path, "rb") as w:
        n, ch, rate = w.getnframes(), w.getnchannels(), w.getframerate()
        x = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float64) / 32768.0
    return x.reshape(-1, ch), rate


def write_wav(path, x, rate):
    x = np.clip(x, -1, 1)
    with wave.open(path, "wb") as w:
        w.setnchannels(x.shape[1] if x.ndim > 1 else 1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes((x * 32767).astype(np.int16).tobytes())


def variants_from_host(log):
    out = []
    for line in open(log, encoding="utf-8", errors="replace"):
        m = re.search(r"\[VoiceModeTest\] variant (\S+) mode=(.+?) delay=(\d+) jitter=(\w+) t0=(\d+) len=(\d+)", line)
        if m:
            out.append(dict(name=m[1], mode=m[2], delay=int(m[3]), jitter=m[4] == "True", t0=int(m[5]), n=int(m[6])))
    return out


def stats_from_client(log):
    rows = []
    for line in open(log, encoding="utf-8", errors="replace"):
        m = re.search(r"\[VoiceModeTest\] stat t=(\d+) .*?target=(\d+) buffered=(\d+) gaps=(\d+) gapMs=(\d+) "
                      r"underruns=(\d+) dropouts=(\d+) lost=(\d+) late=(\d+) packets=(\d+)", line)
        if m:
            rows.append(tuple(int(v) for v in m.groups()))
    return np.array(rows, dtype=np.int64)


def stat_at(st, t):
    i = np.searchsorted(st[:, 0], t)
    return st[min(max(i, 0), len(st) - 1)]


def frame_clock(stamps_path, rate):
    """Frame index -> UTC ticks. Callbacks only run late, so the clock is the lower envelope of the stamps (rolling
    minimum over ~4 s); piecewise, because the audio thread can stall (a hitch of the PC) and the mapping jumps."""
    s = np.loadtxt(stamps_path, dtype=np.int64)
    f = s[:, 0].astype(np.float64)
    b = TICKS / rate
    e = (s[:, 1] - s[0, 1]).astype(np.float64) - b * f
    w = 200
    env = np.array([e[max(0, i - w): i + w].min() for i in range(len(e))])
    # never earlier than the stamp itself allows: keep the envelope below e but above a later stamp's lower bound
    times = s[0, 1] + b * f + env
    return f, times


def best_lag(ref, sig, max_lag):
    """Lag (samples) of ref inside sig[0 : len(ref)+max_lag] by normalised cross-correlation."""
    n = len(ref) + max_lag
    seg = sig[:n]
    if len(seg) < len(ref) + 1:
        return None, 0.0
    size = 1 << int(np.ceil(np.log2(len(seg) + len(ref))))
    c = np.fft.irfft(np.fft.rfft(seg, size) * np.conj(np.fft.rfft(ref, size)), size)[: len(seg) - len(ref) + 1]
    e = np.concatenate([[0.0], np.cumsum(seg ** 2)])
    win = e[len(ref):] - e[: len(e) - len(ref)]
    norm = np.sqrt(win[: len(c)] * np.sum(ref ** 2)) + 1e-12
    r = c / norm
    k = int(np.argmax(r))
    return k, float(r[k])


def main():
    feed_p, host_log, client_log, rec_p, stamps_p, out_dir = sys.argv[1:7]
    ffmpeg = sys.argv[7] if len(sys.argv) > 7 else None
    os.makedirs(out_dir, exist_ok=True)
    feed, fr = read_wav(feed_p)
    feed = feed[:, 0]
    rec, rate = read_wav(rec_p)
    mono = rec.mean(axis=1)
    dec = rate // fr
    mono16 = mono[: len(mono) // dec * dec].reshape(-1, dec).mean(axis=1)  # 48 -> 16 kHz (box filter, for alignment)
    sf, stimes = frame_clock(stamps_p, rate)
    b = TICKS / rate
    # Pad the front: a variant may start before the client began recording (its audio arrives later anyway).
    pad = 2 * rate
    rec = np.concatenate([np.zeros((pad, rec.shape[1])), rec])
    mono16 = np.concatenate([np.zeros(pad // dec), mono16])
    sf = np.concatenate([[-pad], sf + pad])
    stimes = np.concatenate([[stimes[0] - pad * b], stimes])
    st = stats_from_client(client_log)
    variants = variants_from_host(host_log)
    rows = []
    for i, v in enumerate(variants):
        f0 = float(np.interp(v["t0"], stimes, sf))  # recording frame mixed at the capture time of the first sample
        start16 = int(f0 / dec)
        span = v["n"] + int(1.8 * fr)
        sig = mono16[start16: start16 + span]
        # Per 1 s chunk of speech: delay = where it lands in the recording.
        lats, sims = [], []
        for c0 in range(0, v["n"] - fr, fr // 2):
            ref = feed[c0: c0 + fr]
            if np.sqrt(np.mean(ref ** 2)) < 0.01:
                continue
            k, r = best_lag(ref, sig[c0:], int(1.5 * fr))
            if k is None or r < 0.3:
                continue
            lats.append(k * 1000.0 / fr)
            sims.append(r)
        lat = np.array(lats)
        # The variant's audio in the recording (48 kHz), from the median delay on.
        med = float(np.median(lat)) if len(lat) else 0.0
        s48 = int(f0 + med / 1000.0 * rate)
        clip = rec[max(0, s48 - int(0.3 * rate)): s48 + int(v["n"] / fr * rate) + int(0.5 * rate)]
        name = f"{i + 1:02d}_{v['name']}"
        wav_p = os.path.join(out_dir, name + ".wav")
        write_wav(wav_p, clip, rate)
        if ffmpeg:
            subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", wav_p, "-b:a", "160k", wav_p[:-4] + ".mp3"])
        x = clip.mean(axis=1)
        fl = int(0.02 * rate)
        fr_rms = np.sqrt(np.mean(x[: len(x) // fl * fl].reshape(-1, fl) ** 2, axis=1))
        active = fr_rms > 10 ** (-45 / 20)
        rms = 10 * np.log10(np.mean(fr_rms[active] ** 2) + 1e-20) if active.any() else -200
        peak = 20 * np.log10(np.max(np.abs(clip)) + 1e-12)
        clipped = np.mean(np.abs(clip) >= 0.98) * 100
        spec = np.abs(np.fft.rfft(x)) ** 2
        freqs = np.fft.rfftfreq(len(x), 1 / rate)
        hf = 10 * np.log10(spec[(freqs > 8000) & (freqs < 20000)].sum() / (spec[(freqs > 80) & (freqs < 8000)].sum() + 1e-20) + 1e-20)
        # Clicks: energy of the sample-to-sample difference in 2 ms windows, loudest window vs the 99th percentile.
        dx = np.diff(x)
        w2 = int(0.002 * rate)
        e2 = np.sum(dx[: len(dx) // w2 * w2].reshape(-1, w2) ** 2, axis=1)
        click = 10 * np.log10(e2.max() / (np.percentile(e2, 99) + 1e-20) + 1e-20)
        s_a = stat_at(st, v["t0"])
        s_b = stat_at(st, v["t0"] + int((v["n"] / fr + 1.8) * TICKS))
        d = s_b - s_a
        rows.append(dict(name=v["name"], mode=v["mode"], delay=v["delay"], jitter=v["jitter"],
                         lat_med=med, lat_min=float(lat.min()) if len(lat) else 0, lat_max=float(lat.max()) if len(lat) else 0,
                         sim=float(np.mean(sims)) if sims else 0, gaps=int(d[3]), gap_ms=int(d[4]), underruns=int(d[5]),
                         lost=int(d[7]), late=int(d[8]), packets=int(d[9]), target_end=int(s_b[1]),
                         rms=rms, peak=peak, clipped=clipped, hf=hf, click=click, file=name))
    hdr = ("wariant", "tryb", "bufor", "jitter", "opozn. med", "min", "max", "podob.", "dziury", "cisza ms",
           "zgubione", "pakiety", "cel na koncu", "RMS dBFS", "szczyt dBFS", "% >=0.98", "8-20k dB", "klik dB")
    lines = [" | ".join(hdr)]
    for r in rows:
        lines.append(" | ".join(str(x) for x in (
            r["file"], r["mode"], r["delay"], "tak" if r["jitter"] else "nie", f"{r['lat_med']:.0f}", f"{r['lat_min']:.0f}",
            f"{r['lat_max']:.0f}", f"{r['sim']:.3f}", r["gaps"], r["gap_ms"], r["lost"], r["packets"], r["target_end"],
            f"{r['rms']:.1f}", f"{r['peak']:.1f}", f"{r['clipped']:.2f}", f"{r['hf']:.1f}", f"{r['click']:.1f}")))
    text = "\n".join(lines)
    open(os.path.join(out_dir, "wyniki.txt"), "w", encoding="utf-8").write(text + "\n")
    print(text)


if __name__ == "__main__":
    main()
