"""Porownanie dwoch nagran tego samego zdania (np. klasyczny vs niskie opoznienie): widmo w pasmach i roznica
po dopasowaniu w czasie. Uzycie: python compare_pair.py a.wav b.wav [start_s dlugosc_s]"""
import sys, wave
import numpy as np

def rd(p):
    with wave.open(p) as w:
        x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(float) / 32768
        return x.reshape(-1, w.getnchannels()).mean(1), w.getframerate()

a, r = rd(sys.argv[1]); b, _ = rd(sys.argv[2])
t0 = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
ln = float(sys.argv[4]) if len(sys.argv) > 4 else 6.0
ref = a[int(t0 * r): int((t0 + ln) * r)]
n = len(b)
size = 1 << int(np.ceil(np.log2(n + len(ref))))
c = np.fft.irfft(np.fft.rfft(b, size) * np.conj(np.fft.rfft(ref, size)), size)[: n - len(ref)]
k = int(np.argmax(c))
seg = b[k: k + len(ref)]
g = np.dot(seg, ref) / np.dot(seg, seg)
res = ref - g * seg
print(f"przesuniecie {(k - t0 * r) / r * 1000:.1f} ms, wzmocnienie b->a {20*np.log10(g):.2f} dB, "
      f"roznica (SNR a wzgledem b) {10*np.log10(np.sum(ref**2)/np.sum(res**2)):.1f} dB")
fa, fb = np.abs(np.fft.rfft(ref)) ** 2, np.abs(np.fft.rfft(seg)) ** 2
f = np.fft.rfftfreq(len(ref), 1 / r)
for lo, hi in [(80, 300), (300, 1000), (1000, 3000), (3000, 6000), (6000, 8000), (8000, 12000), (12000, 16000), (16000, 20000)]:
    m = (f >= lo) & (f < hi)
    ea, eb = fa[m].sum(), fb[m].sum()
    tot = fa[(f > 80) & (f < 8000)].sum()
    print(f"  {lo:5d}-{hi:5d} Hz: a {10*np.log10(ea/tot+1e-20):6.1f} dB  b {10*np.log10(eb/tot+1e-20):6.1f} dB  (b-a {10*np.log10((eb+1e-20)/(ea+1e-20)):+.1f} dB)")
