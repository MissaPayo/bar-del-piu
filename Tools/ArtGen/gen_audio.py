"""Bar del Piu - generador de audio (chiptune cálido + ambiente de bar).

Genera WAV de 16 bits en Assets/Audio/{SFX,Music,Ambience}.
Los nombres de archivo coinciden con los enums Sfx / MusicTrack de AudioManager.

Uso: python Tools/ArtGen/gen_audio.py
"""
import os
import wave

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio")
SR = 44100
rng = np.random.default_rng(42)


# ------------------------------------------------------------------ utilidades
def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def note_hz(n):
    """n = semitonos desde A4."""
    return 440.0 * 2 ** (n / 12.0)


NOTE = {"C": -9, "C#": -8, "Db": -8, "D": -7, "D#": -6, "Eb": -6, "E": -5, "F": -4, "F#": -3,
        "Gb": -3, "G": -2, "G#": -1, "Ab": -1, "A": 0, "A#": 1, "Bb": 1, "B": 2}


def hz(name):
    """'C4', 'F#3'..."""
    pitch, octv = name[:-1], int(name[-1])
    return note_hz(NOTE[pitch] + (octv - 4) * 12)


def phase(freq, n):
    f = np.broadcast_to(np.asarray(freq, dtype=float), (n,))
    return np.cumsum(f) / SR


def sine(freq, n):
    return np.sin(2 * np.pi * phase(freq, n))


def tri(freq, n):
    p = phase(freq, n) % 1.0
    return 4 * np.abs(p - 0.5) - 1


def pulse(freq, n, duty=0.5, soft=6.0):
    """Pulso suavizado (menos áspero que un cuadrado puro)."""
    p = phase(freq, n) % 1.0
    return np.tanh(soft * (np.sin(2 * np.pi * p) - np.cos(np.pi * duty)))


def saw(freq, n):
    p = phase(freq, n) % 1.0
    return 2 * p - 1


def noise(n):
    return rng.uniform(-1, 1, n)


def env(n, a=0.005, d=0.1, s=0.0, r=0.05, sustain_time=None):
    """ADSR en muestras. Sin sustain_time: A -> D hasta s -> R al final."""
    a_n, d_n, r_n = int(a * SR), int(d * SR), int(r * SR)
    e = np.zeros(n)
    i = 0
    if a_n:
        e[:min(a_n, n)] = np.linspace(0, 1, a_n)[:min(a_n, n)]
    i = a_n
    if i < n:
        k = min(d_n, n - i)
        e[i:i + k] = 1 + (s - 1) * (1 - np.exp(-5 * np.arange(k) / max(d_n, 1)))
        i += k
    if i < n:
        e[i:] = e[i - 1] if i > 0 else s
    if r_n and n > r_n:
        e[-r_n:] *= np.linspace(1, 0, r_n)
    return e


def exp_decay(n, tau):
    return np.exp(-np.arange(n) / (tau * SR))


def fft_filter(x, lo=None, hi=None, slope=2.0):
    """Filtro pasa-banda suave en el dominio de la frecuencia."""
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    g = np.ones_like(f)
    if lo:
        g *= 1 / (1 + (lo / np.maximum(f, 1)) ** (2 * slope))
    if hi:
        g *= 1 / (1 + (f / hi) ** (2 * slope))
    return np.fft.irfft(X * g, len(x))


def onepole_lp(x, cutoff):
    """Pasa-bajos de un polo con corte variable (array o escalar)."""
    c = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = 1 - np.exp(-2 * np.pi * c / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a[i] * (x[i] - acc)
        y[i] = acc
    return y


def pink(n):
    X = np.fft.rfft(rng.normal(0, 1, n))
    f = np.fft.rfftfreq(n, 1 / SR)
    X /= np.sqrt(np.maximum(f, 20))
    y = np.fft.irfft(X, n)
    return y / (np.max(np.abs(y)) + 1e-9)


def bell(freq, dur, ratios=(1, 2.76, 5.4, 8.93), amps=(1, 0.5, 0.25, 0.12), tau=0.4):
    n = int(dur * SR)
    out = np.zeros(n)
    for r, a in zip(ratios, amps):
        out += a * sine(freq * r, n) * exp_decay(n, tau / (r ** 0.5))
    return out


def place(buf, sig, at):
    i = int(at * SR)
    j = min(len(buf), i + len(sig))
    if i < len(buf):
        buf[i:j] += sig[:j - i]


def normalize(x, peak=0.89):
    m = np.max(np.abs(x)) + 1e-9
    return x * (peak / m)


def fade_edges(x, fin=0.002, fout=0.01):
    a, b = int(fin * SR), int(fout * SR)
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def make_loop(x, xfade):
    """Funde el final sobre el inicio para que el loop no tenga costura."""
    k = int(xfade * SR)
    head, body, tail = x[:k], x[k:-k] if k else x, x[-k:]
    w = np.linspace(0, 1, k)
    if x.ndim == 2:
        w = w[:, None]
    mixed = tail * (1 - w) + head * w
    return np.concatenate([body, mixed])


def write(rel, x, peak=0.89, stereo=False):
    path = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = normalize(x, peak)
    data = np.clip(x, -1, 1)
    data = (data * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(2 if stereo else 1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("  ", rel, "%.2fs" % (len(x) / SR))


# ------------------------------------------------------------------ SFX
def sfx():
    n = lambda d: int(d * SR)

    # Llaves
    d = 0.07
    x = fft_filter(noise(n(d)), lo=2500) * exp_decay(n(d), 0.006) * 0.6
    x += sine(1800, n(d)) * exp_decay(n(d), 0.015) * 0.5
    write("SFX/tap_select.wav", fade_edges(x), 0.6)

    d = 0.35
    x = sine(np.linspace(160, 90, n(d)), n(d)) * exp_decay(n(d), 0.05)
    for f, a in ((523, .4), (1187, .3), (2350, .18), (3610, .1)):
        x += a * sine(f, n(d)) * exp_decay(n(d), 0.07)
    x += fft_filter(noise(n(d)), lo=3000) * exp_decay(n(d), 0.004) * 0.7
    write("SFX/tap_open.wav", fade_edges(x), 0.8)

    d = 0.25
    x = sine(np.linspace(220, 140, n(d)), n(d)) * exp_decay(n(d), 0.03) * 0.8
    for f, a in ((690, .35), (1520, .25), (3010, .12)):
        x += a * sine(f, n(d)) * exp_decay(n(d), 0.045)
    x += fft_filter(noise(n(d)), lo=4000) * exp_decay(n(d), 0.05) * 0.15
    write("SFX/tap_close.wav", fade_edges(x), 0.7)

    # Chorro en loop (2 s): ruido filtrado con modulación + burbujas
    d = 2.6
    N = n(d)
    t = t_axis(d)
    base = fft_filter(noise(N), lo=500, hi=3200)
    mod = 0.75 + 0.25 * np.sin(2 * np.pi * 3.1 * t) * np.sin(2 * np.pi * 0.7 * t + 1)
    x = base * mod
    for _ in range(70):
        f0 = rng.uniform(350, 900)
        bd = rng.uniform(0.02, 0.05)
        bn = n(bd)
        blip = sine(np.linspace(f0, f0 * 1.6, bn), bn) * np.hanning(bn) * rng.uniform(0.15, 0.4)
        place(x, blip, rng.uniform(0, d - bd))
    x = make_loop(x, 0.6)
    write("SFX/pour_loop.wav", x, 0.6)

    # Espuma asentándose (loop 2 s): crepitar suave
    d = 2.5
    N = n(d)
    x = fft_filter(noise(N), lo=5000) * 0.08
    for _ in range(260):
        cl = n(0.003)
        place(x, noise(cl) * np.hanning(cl) * rng.uniform(0.1, 0.5), rng.uniform(0, d - 0.01))
    x = fft_filter(x, lo=2500)
    x = make_loop(x, 0.5)
    write("SFX/foam_fizz_loop.wav", x, 0.35)

    # Vaso: apoyar y deslizar
    d = 0.4
    x = sine(np.linspace(140, 80, n(d)), n(d)) * exp_decay(n(d), 0.03) * 0.8
    x += bell(2650, d, ratios=(1, 1.55, 2.3), amps=(0.35, 0.2, 0.1), tau=0.12)
    x += fft_filter(noise(n(d)), lo=1500) * exp_decay(n(d), 0.005) * 0.4
    write("SFX/glass_place.wav", fade_edges(x), 0.75)

    d = 0.45
    N = n(d)
    sweep = np.linspace(900, 1800, N)
    x = onepole_lp(noise(N), sweep) * np.sin(np.pi * np.linspace(0, 1, N)) ** 0.7 * 0.8
    place(x, bell(3100, 0.2, ratios=(1, 1.5), amps=(0.3, 0.15), tau=0.08), 0.33)
    write("SFX/glass_slide.wav", fade_edges(x), 0.7)

    # Resultados del servido
    def chime(notes, step, dur, voice="tri", tau=0.25, sparkle=False):
        total = step * len(notes) + dur
        buf = np.zeros(n(total))
        for i, nm in enumerate(notes):
            f = hz(nm)
            k = n(dur)
            s = (tri(f, k) * 0.6 + sine(f * 2, k) * 0.25 + sine(f * 3.01, k) * 0.08) if voice == "tri" \
                else pulse(f, k, 0.25, 3.0) * 0.5
            place(buf, s * env(k, 0.003, tau, 0.0, 0.03), i * step)
        if sparkle:
            sp = fft_filter(noise(n(total)), lo=7000) * exp_decay(n(total), 0.15) * 0.15
            buf += sp
        return fade_edges(buf)

    write("SFX/serve_perfect.wav", chime(["C6", "E6", "G6", "C7"], 0.055, 0.45, tau=0.3, sparkle=True), 0.8)
    write("SFX/serve_good.wav", chime(["G5", "C6"], 0.07, 0.35, tau=0.22), 0.7)
    write("SFX/serve_regular.wav", chime(["E5"], 0.0, 0.3, tau=0.15), 0.55)
    # Malo: «womp» descendente
    d = 0.55
    N = n(d)
    f = np.concatenate([np.full(N // 2, hz("D4")), np.linspace(hz("D4"), hz("A3") * 0.94, N - N // 2)])
    x = onepole_lp(pulse(f, N, 0.5, 2.5), 900) * env(N, 0.01, 0.4, 0.3, 0.12)
    write("SFX/serve_bad.wav", fade_edges(x), 0.65)

    # Combo: una campanita en Do6; el AudioManager sube el tono por nivel
    x = bell(hz("C6"), 0.5, ratios=(1, 2, 3.01, 4.2), amps=(1, 0.35, 0.15, 0.08), tau=0.35)
    write("SFX/combo_up.wav", fade_edges(x), 0.6)

    # Propina: moneda
    d = 0.5
    buf = np.zeros(n(d))
    for at, f in ((0.0, 2093), (0.07, 2794)):
        k = n(d - at)
        s = sine(f, k) + 0.4 * sine(f * 2.4, k) + 0.2 * sine(f * 3.9, k)
        place(buf, s * exp_decay(k, 0.12 if at else 0.03), at)
    write("SFX/tip_coin.wav", fade_edges(buf), 0.55)

    # Clientes
    d = 0.9
    buf = np.zeros(n(d))
    for at in (0.0, 0.16):
        place(buf, bell(1318, d - at, ratios=(1, 2.32, 4.25, 6.8), amps=(1, .45, .2, .1), tau=0.3), at)
    write("SFX/customer_arrive.wav", fade_edges(buf), 0.5)

    d = 0.18
    N = n(d)
    x = pulse(np.linspace(hz("C5"), hz("G5"), N), N, 0.25, 2.0) * env(N, 0.005, 0.12, 0.0, 0.03)
    x = onepole_lp(x, 3500)
    write("SFX/customer_happy.wav", fade_edges(x), 0.5)

    d = 0.5
    buf = np.zeros(n(d))
    for i in range(3):
        k = n(0.05)
        knock = fft_filter(noise(k), lo=500, hi=1400) * exp_decay(k, 0.008)
        knock += sine(260, k) * exp_decay(k, 0.012) * 0.6
        place(buf, knock, 0.02 + i * 0.13)
    write("SFX/customer_impatient.wav", fade_edges(buf), 0.6)

    d = 0.7
    N = n(d)
    t = t_axis(d)
    f = np.linspace(190, 110, N) * (1 + 0.04 * np.sin(2 * np.pi * 9 * t))
    x = onepole_lp(saw(f, N), 700) * env(N, 0.03, 0.5, 0.4, 0.15)
    x += onepole_lp(saw(f * 1.01, N), 600) * env(N, 0.03, 0.5, 0.4, 0.15) * 0.6
    write("SFX/customer_leave_angry.wav", fade_edges(x), 0.6)

    # Errores y avisos
    d = 0.35
    N = n(d)
    x = onepole_lp(pulse(150, N, 0.5, 4) + pulse(158, N, 0.5, 4), 1200) * env(N, 0.005, 0.3, 0.8, 0.06)
    write("SFX/error.wav", fade_edges(x), 0.6)

    d = 0.3
    buf = np.zeros(n(d))
    for i, nm in enumerate(("A5", "E5")):
        k = n(0.1)
        place(buf, tri(hz(nm), k) * env(k, 0.002, 0.08, 0, 0.02), i * 0.12)
    write("SFX/patience_warning.wav", fade_edges(buf), 0.45)

    d = 0.06
    N = n(d)
    x = fft_filter(noise(N), lo=1800, hi=4500) * exp_decay(N, 0.006) + sine(1250, N) * exp_decay(N, 0.01) * 0.6
    write("SFX/timer_tick.wav", fade_edges(x), 0.5)

    k = n(0.18)
    write("SFX/countdown_beep.wav", fade_edges(tri(hz("A5"), k) * env(k, 0.003, 0.15, 0.3, 0.04)), 0.55)
    k = n(0.55)
    x = (tri(hz("A6"), k) * 0.6 + pulse(hz("A5"), k, 0.25, 2) * 0.25) * env(k, 0.003, 0.4, 0.4, 0.15)
    write("SFX/countdown_go.wav", fade_edges(x), 0.65)

    # Fin de la partida: campana de «última ronda» + bajada
    d = 2.4
    buf = bell(hz("E5"), d, ratios=(1, 2.0, 2.76, 4.1, 5.4), amps=(1, .4, .5, .2, .15), tau=0.9) * 0.8
    for i, nm in enumerate(("C5", "A4", "F4", "C4")):
        k = n(0.4)
        place(buf, tri(hz(nm), k) * env(k, 0.004, 0.3, 0.2, 0.08) * 0.5, 0.35 + i * 0.16)
    write("SFX/game_over.wav", fade_edges(buf), 0.8)

    # Nuevo récord: fanfarria corta
    d = 1.6
    buf = np.zeros(n(d))
    seq = [("C5", 0.0, .12), ("E5", .12, .12), ("G5", .24, .12), ("C6", .36, .3), ("A5", .7, .12), ("C6", .82, .7)]
    for nm, at, du in seq:
        k = n(du + 0.1)
        s = pulse(hz(nm), k, 0.25, 2.5) * 0.4 + tri(hz(nm) / 2, k) * 0.4
        place(buf, s * env(k, 0.004, du, 0.5, 0.08), at)
    buf += fft_filter(noise(len(buf)), lo=7000) * exp_decay(len(buf), 0.5) * 0.06
    write("SFX/new_record.wav", fade_edges(buf), 0.8)

    # Intro: encender luces, neón, transiciones
    d = 0.5
    N = n(d)
    x = fft_filter(noise(N), lo=2000) * exp_decay(N, 0.004) * 0.8
    x += sine(np.linspace(1400, 900, N), N) * exp_decay(N, 0.01) * 0.4
    hum = (sine(100, N) + 0.5 * sine(200, N) + 0.25 * sine(300, N)) * np.clip(np.arange(N) / (0.05 * SR), 0, 1) * exp_decay(N, 0.25) * 0.25
    write("SFX/light_on.wav", fade_edges(x + hum), 0.7)

    d = 1.1
    N = n(d)
    t = t_axis(d)
    gate = np.zeros(N)
    for a0, a1 in ((0.0, 0.06), (0.14, 0.2), (0.3, 0.33), (0.42, 1.1)):
        gate[int(a0 * SR):int(a1 * SR)] = 1
    gate = onepole_lp(gate, 200)
    buzz = onepole_lp(saw(120, N) + 0.6 * saw(240.5, N), 2200) * gate
    buzz += fft_filter(noise(N), lo=4000) * gate * 0.08
    buzz *= np.concatenate([np.ones(N - n(0.4)), np.linspace(1, 0.25, n(0.4))])
    write("SFX/neon_on.wav", fade_edges(buzz), 0.55)

    d = 0.4
    N = n(d)
    x = onepole_lp(noise(N), np.linspace(400, 3500, N) * np.sin(np.pi * np.linspace(0, 1, N)) + 200)
    x *= np.sin(np.pi * np.linspace(0, 1, N)) ** 1.5
    write("SFX/whoosh.wav", fade_edges(x), 0.55)

    d = 0.15
    N = n(d)
    x = sine(np.linspace(300, 900, N), N) * exp_decay(N, 0.03) + noise(N) * exp_decay(N, 0.003) * 0.3
    write("SFX/pop.wav", fade_edges(x), 0.6)

    k = n(0.04)
    write("SFX/score_tick.wav", fade_edges(tri(hz("E6"), k) * exp_decay(k, 0.012)), 0.4)

    # UI
    k = n(0.05)
    write("SFX/ui_click.wav", fade_edges(tri(1400, k) * exp_decay(k, 0.012) + noise(k) * exp_decay(k, 0.002) * 0.3), 0.5)
    k = n(0.04)
    write("SFX/ui_hover.wav", fade_edges(sine(2200, k) * exp_decay(k, 0.008)), 0.25)
    k = n(0.1)
    write("SFX/ui_back.wav", fade_edges(tri(np.linspace(900, 600, k), k) * exp_decay(k, 0.03)), 0.45)
    # Blip de diálogo: «voz» de pulso con formante (el tono lo varía el AudioManager)
    k = n(0.055)
    x = pulse(hz("A3"), k, 0.3, 3) * env(k, 0.003, 0.04, 0.0, 0.01)
    x = fft_filter(x, lo=300, hi=2400)
    write("SFX/dialog_blip.wav", fade_edges(x), 0.4)


# ------------------------------------------------------------------ música
def drum_kick(k):
    return sine(np.linspace(150, 45, k), k) * exp_decay(k, 0.09)


def drum_snare(k):
    return fft_filter(noise(k), lo=1200, hi=8000) * exp_decay(k, 0.06) * 0.7 + tri(200, k) * exp_decay(k, 0.03) * 0.4


def drum_hat(k, open_=False):
    return fft_filter(noise(k), lo=7000) * exp_decay(k, 0.08 if open_ else 0.018) * 0.4


def song(name, bpm, prog, melody, swing=0.58, drums=True, rush=True, bars_per_chord=1, lead=True):
    beat = 60 / bpm
    bar = beat * 4
    total_bars = len(prog) * bars_per_chord
    dur = total_bars * bar
    N = int(dur * SR)
    tail = int(1.5 * SR)  # cola para reverb/decays que se pliega al inicio
    L = {k: np.zeros(N + tail) for k in ("bass", "keys", "lead", "drums", "rush")}

    def at8(bar_i, eighth):  # posición con swing en corcheas
        pair, odd = divmod(eighth, 2)
        return bar_i * bar + pair * beat + (swing * beat if odd else 0)

    for ci, chord in enumerate(prog):
        root, tones = chord
        for b in range(bars_per_chord):
            bi = ci * bars_per_chord + b
            # Bajo: tónica / quinta caminando en negras
            pattern = [root, tones[2] - 12 if tones[2] - 12 > root else tones[2] - 24, root + 12, tones[1] - 12]
            for q in range(4):
                f = note_hz(pattern[q] - 24)
                k = int(beat * 0.9 * SR)
                s = tri(f, k) * env(k, 0.004, 0.25, 0.6, 0.05) + sine(f / 2, k) * 0.3 * env(k, 0.004, 0.2, 0.3, 0.05)
                place(L["bass"], s, bi * bar + q * beat)
            # Teclas tipo Rhodes: acorde en 1 y en el «y» del 2
            for hit, ln in ((0, 1.4), (3, 1.9)):
                k = int(beat * ln * SR)
                s = np.zeros(k)
                for tn in tones:
                    f = note_hz(tn)
                    s += (sine(f, k) + 0.25 * sine(f * 2, k) * exp_decay(k, 0.15)
                          + 0.12 * pulse(f, k, 0.25, 2) * exp_decay(k, 0.3))
                s = onepole_lp(s, 2600) * env(k, 0.01, 0.6, 0.35, 0.15) * 0.22
                place(L["keys"], s, at8(bi, hit))
            if drums:
                for e in range(8):
                    pos = at8(bi, e)
                    place(L["drums"], drum_hat(int(0.12 * SR), open_=(e == 7)), pos)
                    if e in (0, 5):
                        place(L["drums"], drum_kick(int(0.3 * SR)) * 0.9, pos)
                    if e in (2, 6):
                        place(L["drums"], drum_snare(int(0.25 * SR)) * 0.7, pos)
            if rush:
                for s16 in range(16):
                    k = int(0.06 * SR)
                    acc = 1.0 if s16 % 4 == 2 else 0.55
                    shk = fft_filter(noise(k), lo=5000, hi=11000) * np.hanning(k) * 0.35 * acc
                    place(L["rush"], shk, bi * bar + s16 * beat / 4)
                for q in (1, 3):
                    k = int(0.2 * SR)
                    tamb = fft_filter(noise(k), lo=6000) * exp_decay(k, 0.07) * 0.4
                    tamb += sum(sine(f, k) * exp_decay(k, 0.06) * 0.05 for f in (5200, 7300, 8900))
                    place(L["rush"], tamb, bi * bar + q * beat)
                # bajo una octava arriba en corcheas (empuje)
                for e in range(8):
                    f = note_hz(root - 12)
                    k = int(beat * 0.4 * SR)
                    place(L["rush"], pulse(f, k, 0.25, 2.5) * env(k, 0.003, 0.08, 0.2, 0.02) * 0.18, at8(bi, e))

    if lead:
        for bar_i, eighth, semis, length in melody:
            f = note_hz(semis - 12)
            k = int(length * beat * SR)
            t = np.arange(k) / SR
            vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - 0.15) * 4, 0, 1)
            s = pulse(f * vib, k, 0.125, 2.2) * 0.5 + tri(f * vib, k) * 0.5
            s = onepole_lp(s, 3200) * env(k, 0.01, 0.2, 0.6, min(0.08, length * beat * 0.4)) * 0.3
            place(L["lead"], s, at8(bar_i, eighth))
            # eco a corchea con punto
            place(L["lead"], s * 0.25, at8(bar_i, eighth) + beat * 0.75)

    def fold(x):
        y = x[:N].copy()
        y[:tail] += x[N:N + tail]
        return y

    stems = {k: fold(v) for k, v in L.items()}

    def stereo(parts):
        """parts: lista de (señal, paneo -1..1)."""
        l = np.zeros(N)
        r = np.zeros(N)
        for s, pan in parts:
            l += s * np.sqrt((1 - pan) / 2)
            r += s * np.sqrt((1 + pan) / 2)
        return np.stack([l, r], axis=1)

    main = stereo([(stems["bass"], 0), (stems["keys"], -0.25), (stems["lead"], 0.2), (stems["drums"], 0.1)])
    # reverb simple: líneas de retardo realimentadas
    wet = np.zeros_like(main)
    for dl, g, side in ((0.031, 0.35, 0), (0.047, 0.3, 1), (0.071, 0.25, 0), (0.113, 0.2, 1)):
        d = int(dl * SR)
        src = fft_filter(main[:, side], hi=4000)
        wet[d:, 1 - side] += src[:-d] * g
    main += wet * 0.5
    peak = np.max(np.abs(main))
    write("Music/music_%s.wav" % name, main / peak, 0.8, stereo=True)
    if rush:
        r = stereo([(stems["rush"], 0.3)])
        write("Music/music_%s_rush.wav" % name, r, 0.6, stereo=True)


def chord(root_name, kind):
    r = NOTE[root_name[:-1]] + (int(root_name[-1]) - 4) * 12
    ints = {"maj7": (0, 4, 7, 11), "m7": (0, 3, 7, 10), "7": (0, 4, 7, 10), "6": (0, 4, 7, 9)}[kind]
    return (r, [r + i for i in ints])


def gen_melody(prog, bars_per_chord, seed, density=0.55):
    """Melodía simple con notas del acorde y pasos de escala (Fa mayor)."""
    r = np.random.default_rng(seed)
    scale = [NOTE[n] for n in ("F", "G", "A", "Bb", "C", "D", "E")]
    scale = sorted({s + 12 * o for s in scale for o in (0, 1)})
    mel = []
    motif = None
    for ci, (root, tones) in enumerate(prog):
        for b in range(bars_per_chord):
            bi = ci * bars_per_chord + b
            if bi % 4 == 3:  # respira cada 4 compases: nota larga
                mel.append((bi, 0, tones[r.integers(1, 4)] % 12 + 12 if tones[1] % 12 + 12 < 20 else tones[1], 3.5))
                continue
            if motif is not None and bi % 4 == 2:
                for e, s, ln in motif:  # repite el motivo transpuesto al acorde
                    mel.append((bi, e, s + (tones[0] - prog[0][1][0]) % 12 - (12 if (tones[0] - prog[0][1][0]) % 12 > 6 else 0), ln))
                continue
            e = 0
            bar_notes = []
            while e < 8:
                if r.random() < density:
                    ln = r.choice([1, 1, 2, 3])
                    if r.random() < 0.6:
                        tgt = tones[r.integers(0, 4)] % 12 + 12
                    else:
                        tgt = scale[r.integers(2, len(scale) - 2)]
                    tgt = max(5, min(20, tgt))
                    bar_notes.append((e, tgt, ln * 0.5 * 0.95))
                    e += ln
                else:
                    e += 1
            if bi % 4 == 0:
                motif = bar_notes
            mel += [(bi, ee, s, ln) for ee, s, ln in bar_notes]
    return mel


def music():
    game_prog = [chord("F3", "maj7"), chord("D3", "m7"), chord("G3", "m7"), chord("C3", "7"),
                 chord("A3", "m7"), chord("D3", "m7"), chord("G3", "m7"), chord("C3", "7")]
    song("game", 104, game_prog, gen_melody(game_prog, 2, seed=5), bars_per_chord=2)
    menu_prog = [chord("Bb2", "maj7"), chord("A2", "m7"), chord("G2", "m7"), chord("F2", "6")]
    song("menu", 84, menu_prog, gen_melody(menu_prog, 2, seed=11, density=0.4),
         drums=False, rush=False, bars_per_chord=2)
    # Jingle de resultados (no loop)
    res_prog = [chord("F3", "maj7")]
    song("results", 96, res_prog, [(0, 0, 8, 0.9), (0, 1, 12, 0.9), (0, 2, 15, 0.9), (0, 3, 20, 2.5)],
         drums=False, rush=False, bars_per_chord=1)


# ------------------------------------------------------------------ ambiente
def ambience():
    d = 32.0
    N = int(d * SR)
    t = np.arange(N) / SR
    l = np.zeros(N)
    r = np.zeros(N)
    # «walla»: varias voces de ruido rosa en bandas de formante con envolvente silábica
    for v in range(14):
        center = rng.uniform(300, 1100)
        band = fft_filter(pink(N), lo=center * 0.7, hi=center * 1.6)
        syl = np.zeros(N)
        pos = rng.uniform(0, 1)
        while pos < d:
            ln = rng.uniform(0.08, 0.25)
            k = int(ln * SR)
            i = int(pos * SR)
            seg = np.hanning(k) * rng.uniform(0.4, 1.0)
            syl[i:i + k] += seg[:max(0, min(k, N - i))]
            pos += ln + rng.uniform(0.0, 0.12)
            if rng.random() < 0.06:
                pos += rng.uniform(0.6, 2.0)  # pausa en la conversación
        talk = band * syl
        pan = rng.uniform(-0.8, 0.8)
        l += talk * np.sqrt((1 - pan) / 2)
        r += talk * np.sqrt((1 + pan) / 2)
    bed = fft_filter(pink(N), lo=80, hi=600) * 0.25
    l += bed
    r += np.roll(bed, 1200)
    # vasos que chocan, sillas
    for _ in range(26):
        at = rng.uniform(0, d - 1)
        g = bell(rng.uniform(2200, 3600), 0.6, ratios=(1, 1.47, 2.09), amps=(1, .5, .3), tau=0.15) * rng.uniform(0.15, 0.35)
        pan = rng.uniform(-0.9, 0.9)
        place(l, g * np.sqrt((1 - pan) / 2), at)
        place(r, g * np.sqrt((1 + pan) / 2), at)
    for _ in range(4):
        at = rng.uniform(0, d - 1)
        k = int(rng.uniform(0.3, 0.6) * SR)
        sc = onepole_lp(noise(k), np.linspace(400, 900, k)) * np.hanning(k) * 0.3
        place(l, sc * 0.7, at)
        place(r, sc * 0.4, at)
    st = np.stack([l, r], axis=1)
    st = fft_filter(st[:, 0], hi=5000)[:, None] * [1, 0] + fft_filter(st[:, 1], hi=5000)[:, None] * [0, 1]
    st = make_loop(st, 2.0)
    write("Ambience/ambience_bar.wav", st, 0.6, stereo=True)


# ------------------------------------------------------------------ cumpleaños (easter egg)
def birthday():
    """Melodía tradicional de cumpleaños (dominio público) en versión chiptune, Fa mayor, 3/4."""
    bpm = 108
    beat = 60 / bpm
    # (nota, duración en negras); el compás empieza con anacrusa
    mel = [("C4", .75), ("C4", .25), ("D4", 1), ("C4", 1), ("F4", 1), ("E4", 2),
           ("C4", .75), ("C4", .25), ("D4", 1), ("C4", 1), ("G4", 1), ("F4", 2),
           ("C4", .75), ("C4", .25), ("C5", 1), ("A4", 1), ("F4", 1), ("E4", 1), ("D4", 2),
           ("Bb4", .75), ("Bb4", .25), ("A4", 1), ("F4", 1), ("G4", 1), ("F4", 3)]
    chords = [(0, "F"), (1, "F"), (4, "C"), (7, "C"), (10, "F"), (13, "F"), (16, "Bb"), (19, "C"), (22, "F")]
    total = sum(d for _, d in mel) + 1
    N = int(total * beat * SR)
    buf = np.zeros(N + SR)
    t = 0.0
    for nm, d in mel:
        f = hz(nm) * 2
        k = int(d * beat * SR)
        s = pulse(f, k, 0.25, 2.2) * 0.45 + tri(f, k) * 0.45
        s = onepole_lp(s, 4000) * env(k, 0.008, 0.25, 0.6, min(0.06, d * beat * 0.4))
        place(buf, s * 0.5, t * beat)
        t += d
    roots = {"F": "F2", "C": "C3", "Bb": "Bb2"}
    triads = {"F": ("F3", "A3", "C4"), "C": ("E3", "G3", "C4"), "Bb": ("D3", "F3", "Bb3")}
    for i, (at, ch) in enumerate(chords):
        end = chords[i + 1][0] if i + 1 < len(chords) else total
        b = at
        while b < end - 0.01:
            k = int(beat * 0.9 * SR)
            place(buf, tri(hz(roots[ch]), k) * env(k, 0.004, 0.2, 0.5, 0.05) * 0.35, (b + 0.75) * beat)
            for q in (1, 2):
                if b + q < end:
                    kk = int(beat * 0.5 * SR)
                    chord_sig = sum(sine(hz(n), kk) for n in triads[ch])
                    place(buf, chord_sig * env(kk, 0.005, 0.15, 0.3, 0.04) * 0.12, (b + 0.75 + q) * beat)
            b += 3
    write("Music/music_birthday.wav", fade_edges(buf[:N + SR // 2]), 0.8)

    # Corneta de fiesta
    d = 0.7
    N = n_(d)
    tt = t_axis(d)
    f = np.concatenate([np.linspace(330, 420, N // 5), np.full(N - N // 5, 420)]) * (1 + 0.01 * np.sin(2 * np.pi * 7 * tt))
    x = onepole_lp(saw(f, N) + 0.5 * saw(f * 1.5, N), 2500) * env(N, 0.03, 0.5, 0.7, 0.15)
    x += fft_filter(noise(N), lo=2000) * 0.05 * env(N, 0.03, 0.5, 0.7, 0.15)
    write("SFX/party_horn.wav", fade_edges(x), 0.6)

    # Aplausos y vítores
    d = 2.2
    N = n_(d)
    buf = np.zeros(N)
    for _ in range(140):
        at = rng.uniform(0, d - 0.1) ** 1.3 / d ** 0.3
        k = n_(0.03)
        clap = fft_filter(noise(k), lo=900, hi=5000) * exp_decay(k, 0.008) * rng.uniform(0.3, 1.0)
        place(buf, clap, at)
    for _ in range(3):
        at = rng.uniform(0.1, 0.6)
        k = n_(0.6)
        f0 = rng.uniform(500, 800)
        f = np.linspace(f0, f0 * 1.4, k)
        place(buf, onepole_lp(saw(f, k), 1800) * np.hanning(k) * 0.25, at)
    buf *= np.concatenate([np.ones(N - n_(0.8)), np.linspace(1, 0, n_(0.8))])
    write("SFX/cheer.wav", fade_edges(buf), 0.7)

    # Soplar velas
    d = 0.5
    N = n_(d)
    x = fft_filter(noise(N), lo=300, hi=2500) * np.sin(np.pi * np.linspace(0, 1, N)) ** 0.6
    write("SFX/candle_blow.wav", fade_edges(x), 0.55)


def car_pass():
    """Auto que pasa: motor + rodado con efecto Doppler y volumen que sube y baja."""
    d = 3.2
    N = n_(d)
    t = t_axis(d)
    pos = (t - d / 2) / 0.9                        # -..+ al pasar frente a la cámara
    gain = 1 / (1 + pos ** 2)
    doppler = 1 + 0.08 * (-np.tanh(pos * 1.5))     # más agudo al acercarse
    eng = saw(70 * doppler, N) + 0.5 * saw(140.5 * doppler, N)
    eng = onepole_lp(eng, 500 + 1200 * gain)
    tire = fft_filter(noise(N), lo=300, hi=2500) * 0.6
    x = (eng * 0.5 + tire) * gain
    write("SFX/car_pass.wav", fade_edges(x, 0.2, 0.3), 0.6)


def mischief():
    """Sonidos de travesuras en el menú: hipo, trago y ampolleta que se rompe."""
    d = 0.22
    N = n_(d)
    f = np.concatenate([np.linspace(300, 900, N // 3), np.linspace(900, 700, N - N // 3)])
    x = onepole_lp(pulse(f, N, 0.3, 2.5), 2400) * env(N, 0.005, 0.12, 0.0, 0.05)
    x += fft_filter(noise(N), lo=1500) * exp_decay(N, 0.01) * 0.3
    write("SFX/hiccup.wav", fade_edges(x), 0.6)

    d = 0.6
    buf = np.zeros(n_(d))
    for i in range(3):
        k = n_(0.12)
        f0 = 260 - i * 30
        g = sine(np.linspace(f0, f0 * 0.6, k), k) * np.hanning(k)
        g += fft_filter(noise(k), lo=200, hi=900) * np.hanning(k) * 0.4
        place(buf, g * (1 - i * 0.2), i * 0.17)
    write("SFX/gulp.wav", fade_edges(buf), 0.6)

    d = 0.9
    N = n_(d)
    x = fft_filter(noise(N), lo=2500) * exp_decay(N, 0.05) * 0.8
    for _ in range(14):
        place(x, bell(rng.uniform(2500, 6000), 0.4, ratios=(1, 1.6, 2.3), amps=(1, .5, .3), tau=0.06) * rng.uniform(0.2, 0.5),
              rng.uniform(0.0, 0.35))
    x += sine(np.linspace(180, 60, N), N) * exp_decay(N, 0.03) * 0.6  # «pop» del filamento
    write("SFX/glass_break.wav", fade_edges(x), 0.75)

    d = 0.12
    N = n_(d)
    x = (sine(100, N) + 0.5 * sine(200, N)) * 0.3 + fft_filter(noise(N), lo=3000) * 0.3
    x *= np.concatenate([np.ones(N // 3), np.zeros(N // 6), np.ones(N - N // 3 - N // 6)]) * exp_decay(N, 0.06)
    write("SFX/lamp_flicker.wav", fade_edges(x), 0.45)


def n_(d):
    return int(d * SR)


if __name__ == "__main__":
    print("SFX"); sfx()
    print("Música"); music()
    print("Ambiente"); ambience()
    print("Cumpleaños"); birthday()
    car_pass()
    mischief()
