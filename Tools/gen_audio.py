#!/usr/bin/env python3
"""Synthesizes all game audio (original, CC0): a full SFX set and two music loops (menu + table) in the Hijaz maqam on D, played by
a Karplus-Strong 'oud', a soft pad, a frame-drum groove and bell accents. No samples, no third-party material.
Output: Assets/_Project/Audio/Sfx/*.wav and Assets/_Project/Audio/Music/*.ogg. (needs numpy, scipy, soundfile)
Usage: python3 Tools/gen_audio.py [sfx|music|all]   (needs numpy + scipy)"""
import os, sys, wave
import numpy as np
from scipy import signal

SR = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', '_Project', 'Audio')
rng = np.random.default_rng(20260930)

# ------------------------------------------------------------------ helpers
def t_(dur): return np.arange(int(SR * dur)) / SR
def env_ad(n, a=0.004, d=0.2, curve=4.0):
    t = np.arange(n) / SR
    return np.minimum(1, t / max(a, 1e-4)) * np.exp(-t / max(d, 1e-4) * curve / 4.0)
def sine(f, dur, ph=0.0): t = t_(dur); return np.sin(2 * np.pi * f * t + ph)
def lp(x, fc, order=2): return signal.sosfilt(signal.butter(order, fc / (SR / 2), 'low', output='sos'), x)
def hp(x, fc, order=2): return signal.sosfilt(signal.butter(order, fc / (SR / 2), 'high', output='sos'), x)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo / (SR / 2), hi / (SR / 2)], 'band', output='sos'), x)
def noise(dur): return rng.uniform(-1, 1, int(SR * dur))
def pad(x, n): return np.pad(x, (0, max(0, n - len(x))))[:n]
def mixn(*parts):
    n = max(len(p) for p in parts); out = np.zeros(n)
    for p in parts: out[:len(p)] += p
    return out
def place(dst, src, at, gain=1.0):
    i = int(at * SR)
    if i >= len(dst): return
    e = min(len(dst), i + len(src)); dst[i:e] += src[:e - i] * gain
def norm(x, peak=0.85): m = np.max(np.abs(x)); return x / m * peak if m > 0 else x
def fade(x, a=0.002, b=0.006):
    x = x.copy(); na, nb = int(a * SR), int(b * SR)
    x[:na] *= np.linspace(0, 1, na); x[-nb:] *= np.linspace(1, 0, nb); return x
def write(path, x, stereo=None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if stereo is not None: data = np.stack([x, stereo], 1)
    else: data = x[:, None]
    pcm = (np.clip(data, -1, 1) * 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(pcm.shape[1]); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
def reverb(x, secs=1.6, wet=0.25, damp=3800):
    n = int(SR * secs); ir = rng.standard_normal(n) * np.exp(-np.arange(n) / SR * 5.0 / secs * 1.5)
    ir = lp(ir, damp); ir[:int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))
    ir /= np.sqrt(np.sum(ir ** 2))                                   # unit-energy impulse response
    tail = signal.fftconvolve(x, ir)                                 # length len(x) + n - 1
    out = np.zeros(len(tail)); out[:len(x)] += x * (1 - wet); out += tail * wet * 1.7
    return out
def pluck(freq, dur, bright=0.5, decay=0.996, pos=0.18):
    """Karplus-Strong string (vectorised as a comb filter) with a pick-position notch: an oud-like tone."""
    n = int(SR * dur); N = max(4, int(round(SR / freq)))
    exc = lp(rng.uniform(-1, 1, N), 6000 * bright + 1500) if N > 8 else rng.uniform(-1, 1, N)
    x = np.zeros(n); x[:N] = exc
    a = np.zeros(N + 2); a[0] = 1.0; a[N] = -decay * 0.5; a[N + 1] = -decay * 0.5
    out = signal.lfilter([1.0], a, x)
    k = int(N * pos)
    if k > 0: out[k:] = out[k:] - 0.55 * out[:-k]
    return out * np.exp(-np.arange(n) / SR / (dur * 0.6)) * 1.4
def bell(freq, dur, amp=1.0, inharm=(1.0, 2.76, 5.4, 8.9)):
    t = t_(dur); out = np.zeros_like(t)
    for k, h in enumerate(inharm): out += np.sin(2 * np.pi * freq * h * t) * np.exp(-t * (3.0 + 2.2 * k) / dur * 2.0) / (1 + k * 0.9)
    return out * amp
def pad_tone(freq, dur, detune=0.003):
    t = t_(dur); out = np.zeros_like(t)
    for d in (-detune, 0, detune):
        out += signal.sawtooth(2 * np.pi * freq * (1 + d) * t) * 0.33
    out = lp(out, 900 + 400 * np.sin(2 * np.pi * 0.11 * t).mean())
    a = np.minimum(1, t / 1.8) * np.minimum(1, (dur - t) / 2.2)
    return out * a

# ------------------------------------------------------------------ SFX
def sfx():
    S = os.path.join(ROOT, 'Sfx'); out = {}
    out['click'] = fade(mixn(sine(1400, 0.05) * env_ad(int(SR * .05), .001, .018), sine(2100, 0.05) * env_ad(int(SR * .05), .001, .012) * .35) * .6)
    out['toggle_on'] = fade(mixn(sine(900, .09) * env_ad(int(SR * .09), .002, .03), np.concatenate([np.zeros(int(.04 * SR)), sine(1350, .09) * env_ad(int(SR * .09), .002, .04)])) * .5)
    out['toggle_off'] = fade(mixn(sine(1200, .09) * env_ad(int(SR * .09), .002, .03), np.concatenate([np.zeros(int(.04 * SR)), sine(780, .1) * env_ad(int(SR * .1), .002, .04)])) * .5)
    n = int(SR * .12); fl = bp(noise(.12), 1800, 7000) * env_ad(n, .002, .05)
    out['card_flip'] = fade(fl * .9)
    d = int(SR * .06); dl = hp(noise(.06), 2500) * env_ad(d, .001, .02)
    out['card_deal'] = fade(dl * .7)
    n = int(SR * .18); th = sine(150, .18) * np.exp(-t_(.18) * 34) + lp(noise(.18), 1400) * np.exp(-t_(.18) * 46) * .8
    out['card_place'] = fade(th * .95 + bp(noise(.18), 3000, 9000) * np.exp(-t_(.18) * 90) * .25)
    n = int(SR * .35); sw = bp(noise(.35), 900, 5500); sw *= np.sin(np.linspace(0, np.pi, n)) ** 2
    out['card_slide'] = fade(sw * .55)
    n = int(SR * .5); ws = lp(noise(.5), np.linspace(400, 5200, n).mean()); ws = bp(noise(.5), 400, 4500) * (np.sin(np.linspace(0, np.pi, n)) ** 1.5)
    out['whoosh'] = fade(ws * .6)
    out['bid'] = fade(bell(1320, .35, .5, (1.0, 2.0, 3.0)) * .6)
    out['pass'] = fade(mixn(sine(330, .16) * np.exp(-t_(.16) * 22), sine(247, .2) * np.exp(-t_(.2) * 18)) * .6)
    out['turn_ping'] = fade(bell(1568, .55, .45, (1.0, 2.01, 3.02)) * .7)
    out['timer_tick'] = fade(sine(1900, .03) * env_ad(int(SR * .03), .0005, .01) * .5)
    out['deny'] = fade(mixn(signal.square(2 * np.pi * 150 * t_(.14)) * .25, signal.square(2 * np.pi * 142 * t_(.14)) * .25) * np.exp(-t_(.14) * 14))
    n = int(SR * .08); pp = sine(np.linspace(500, 1100, n)[-1], .08) * env_ad(n, .002, .03); f = np.linspace(520, 980, n); pp = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(n, .002, .035)
    out['pop'] = fade(pp * .6)
    sp = np.zeros(int(SR * .7))
    for k, (f, at) in enumerate(((2093, 0), (2637, .06), (3136, .12), (2349, .19), (3520, .25))): place(sp, bell(f, .3, .5, (1.0, 2.0)), at, .7 - k * .06)
    out['sparkle'] = fade(sp * .8)
    tw = np.zeros(int(SR * .9)); place(tw, out['whoosh'][:int(SR * .3)], 0, .5)
    for k, f in enumerate((659, 880, 1175)): place(tw, bell(f, .45, .55, (1.0, 2.0, 2.76)), .16 + k * .09, .8)
    out['trick_win'] = fade(tw)
    rw = np.zeros(int(SR * 1.7)); notes = (523.25, 659.25, 783.99, 1046.5)
    for k, f in enumerate(notes): place(rw, bell(f, .9, .55, (1.0, 2.0, 3.0, 4.2)), k * .11, 1.0)
    place(rw, mixn(*[bell(f, 1.2, .35, (1.0, 2.0)) for f in (523.25, 659.25, 783.99, 1046.5)]), .5, .8)
    out['round_win'] = fade(reverb(rw, .9, .22)[:len(rw)])
    rl = np.zeros(int(SR * 1.3)); place(rl, bell(392, .8, .5, (1.0, 2.0, 2.4)), 0, .8); place(rl, bell(311.13, 1.0, .5, (1.0, 2.0, 2.4)), .22, .8); place(rl, bell(233.08, 1.1, .4, (1.0, 2.0)), .48, .7)
    out['round_lose'] = fade(reverb(rl, .8, .2)[:len(rl)])
    gw = np.zeros(int(SR * 3.4))
    seq = ((523.25, 0), (659.25, .13), (783.99, .26), (1046.5, .39), (783.99, .7), (1046.5, .83), (1318.5, .96), (1568.0, 1.2))
    for f, at in seq: place(gw, bell(f, 1.1, .5, (1.0, 2.0, 3.0, 4.2)), at, .9)
    for f in (523.25, 659.25, 783.99, 1046.5, 1568.0): place(gw, bell(f, 2.0, .3, (1.0, 2.0)), 1.55, .7)
    for k in range(9): place(gw, bell(rng.choice([2093, 2637, 3136, 3520]), .3, .3, (1.0, 2.0)), 1.6 + k * .14, .5)
    out['game_win'] = fade(reverb(gw, 1.4, .3)[:len(gw)])
    gl = np.zeros(int(SR * 2.4))
    for k, f in enumerate((392, 349.23, 311.13, 261.63)): place(gl, bell(f, 1.1, .5, (1.0, 2.0, 2.4)), k * .28, .8)
    out['game_lose'] = fade(reverb(gl, 1.2, .25)[:len(gl)])
    cn = np.zeros(int(SR * .6)); place(cn, bell(1568, .3, .6, (1.0, 2.0)), 0, .8); place(cn, bell(2093, .4, .6, (1.0, 2.0)), .07, .9)
    out['coin'] = fade(cn)
    for name, x in out.items(): write(os.path.join(S, name + '.wav'), norm(x, 0.8))
    print('sfx:', len(out), 'files')

# ------------------------------------------------------------------ music
HIJAZ = [0, 1, 4, 5, 7, 8, 10]                                   # D Eb F# G A Bb C
def hz(step, octave=0, root=62):
    """step counts scale degrees (0 = D), negative allowed; returns Hz."""
    o, d = divmod(step, 7); midi = root + HIJAZ[d] + 12 * (o + octave)
    return 440.0 * 2 ** ((midi - 69) / 12.0)
def dum(vol=1.0):     # deep frame-drum hit
    n = int(SR * .35); t = np.arange(n) / SR; f = 95 + 60 * np.exp(-t * 30)
    return (np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 11) + lp(noise(.35), 500) * np.exp(-t * 35) * .4) * vol
def tak(vol=1.0):     # bright rim hit
    n = int(SR * .12); t = np.arange(n) / SR
    return (bp(noise(.12), 2200, 6500) * np.exp(-t * 55) + np.sin(2 * np.pi * 480 * t) * np.exp(-t * 60) * .2) * vol
def ka(vol=1.0):
    n = int(SR * .08); t = np.arange(n) / SR
    return bp(noise(.08), 3000, 8000) * np.exp(-t * 80) * vol

def render_track(bpm, bars, melody, bass_pattern, groove, oud_gain, pad_chords, name, tail=2.5, chime_at=()):
    beat = 60.0 / bpm; total = bars * 4 * beat
    L = np.zeros(int((total + tail) * SR)); R = np.zeros_like(L)
    def add(x, at, gain=1.0, pan=0.0):
        place(L, x, at, gain * (1 - max(0, pan))); place(R, x, at, gain * (1 + min(0, pan)))
    # pad: sustained fifth-drones changing every 4 bars
    per = 4 * 4 * beat
    for i, chord in enumerate(pad_chords * (bars // 4 // len(pad_chords) + 1)):
        if i * per >= total: break
        for f in chord: add(pad_tone(f, per + 3.0), i * per - 0.4 if i else 0, 0.10, 0.0)
    # bass (oud low octave) and groove
    for bar in range(bars):
        for at, step, dur in bass_pattern[bar % len(bass_pattern)]:
            add(pluck(hz(step, -1), dur * beat + 0.6, .35, .997), (bar * 4 + at) * beat, .55 * oud_gain, -0.1)
        for at, kind, vol in groove[bar % len(groove)]:
            fn = {'D': dum, 'T': tak, 'K': ka}[kind]; add(fn(1.0), (bar * 4 + at) * beat + rng.uniform(0, .004), .5 * vol, 0.15 if kind != 'D' else 0)
    # melody phrases
    for at, step, dur, oct_ in melody:
        add(pluck(hz(step, oct_), dur * beat + 1.2, .55, .9975), at * beat, .9 * oud_gain, 0.08)
    for at, f in chime_at: add(bell(f, 2.2, .35, (1.0, 2.0, 3.0)), at * beat, .6, -.2)
    mono = (L + R) * .5
    wl = reverb(L, 2.2, .28); wr = reverb(R, 2.6, .28)
    n = int(total * SR)
    # fold the reverb tail back onto the start so the loop is seamless
    def fold(x):
        head = x[:n].copy(); tailp = x[n:n + int(tail * SR)]; head[:len(tailp)] += tailp; return head
    l, r = fold(wl), fold(wr)
    peak = max(np.max(np.abs(l)), np.max(np.abs(r)))
    import soundfile as sf
    path = os.path.join(ROOT, 'Music', name + '.ogg')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sf.write(path, np.stack([l / peak * .8, r / peak * .8], 1), SR, format='OGG', subtype='VORBIS')
    print('music:', name, '%.1fs' % (n / SR))

def music():
    # ---------------- menu: 66 bpm, spacious, 16 bars
    m = []; a = 0.0
    phr = [  # (beat offset, scale step, beats, octave)
        (0, 4, 1.5, 0), (1.5, 3, .5, 0), (2, 2, 1, 0), (3, 1, 1, 0),
        (4, 0, 2, 0), (6, 1, 1, 0), (7, 3, 1, 0),
        (8, 4, 1, 0), (9, 5, 1, 0), (10, 6, 1.5, 0), (11.5, 5, .5, 0), (12, 4, 2, 0), (14, 3, 2, 0),
        (16, 2, 1, 0), (17, 1, 1, 0), (18, 0, 2, 0), (20, 1, 1.5, 0), (21.5, 2, .5, 0), (22, 3, 2, 0),
    ]
    for base in (0, 32):
        for at, st, dur, o in phr: m.append((base + at, st, dur, o))
    for at, st, dur, o in ((64 + 0, 7, 2, 0), (64 + 2, 6, 1, 0), (64 + 3, 5, 1, 0), (64 + 4, 4, 2, 0), (64 + 6, 3, 2, 0)): m.append((at, st, dur, o))
    bass = [[(0, 0, 3.5)], [(0, 4, 3.5)], [(0, 3, 3.5)], [(0, 0, 3.5)]]
    groove = [[(0, 'D', .9)], [(0, 'D', .8)], [(0, 'D', .9), (2, 'T', .35)], [(0, 'D', .8)]]
    chords = [[73.42, 110.0], [98.0, 146.83], [77.78, 116.54], [73.42, 110.0]]
    render_track(66, 16, m, bass, groove, 0.85, chords, 'menu_theme', chime_at=[(0, 1174.7), (32, 1568.0), (48, 1174.7)])
    # ---------------- table: 92 bpm, maqsum groove, 24 bars
    mel = []
    A = [(0, 4, .5, 0), (.5, 5, .5, 0), (1, 4, .5, 0), (1.5, 3, .5, 0), (2, 2, 1, 0), (3, 1, 1, 0),
         (4, 0, 1.5, 0), (5.5, 1, .5, 0), (6, 2, 1, 0), (7, 3, 1, 0),
         (8, 4, .5, 0), (8.5, 3, .5, 0), (9, 4, .5, 0), (9.5, 5, .5, 0), (10, 6, 1, 0), (11, 5, 1, 0),
         (12, 4, 1, 0), (13, 3, 1, 0), (14, 2, 2, 0)]
    B = [(0, 7, 1, 0), (1, 6, .5, 0), (1.5, 5, .5, 0), (2, 4, 1, 0), (3, 5, 1, 0),
         (4, 6, 1.5, 0), (5.5, 5, .5, 0), (6, 4, 1, 0), (7, 3, 1, 0),
         (8, 2, 1, 0), (9, 1, .5, 0), (9.5, 2, .5, 0), (10, 3, 1, 0), (11, 2, 1, 0),
         (12, 1, 1, 0), (13, 0, 1, 0), (14, 0, 2, 0)]
    for bar in (2, 6, 10, 14, 18):
        base = bar * 4
        for at, st, dur, o in (A if bar in (2, 10, 18) else B): mel.append((base + at, st, dur, o))
    # gentle arpeggio ostinato on the off bars
    for bar in range(24):
        if bar in (2, 3, 6, 7, 10, 11, 14, 15, 18, 19): continue
        base = bar * 4
        for k, st in enumerate((0, 2, 4, 2, 0, 2, 4, 5)): mel.append((base + k * .5, st, .5, 0)) if bar % 2 == 0 else None
    bass = [[(0, 0, 1), (2, 0, 1), (3, 4, .5)], [(0, 4, 1), (2, 4, 1), (3, 3, .5)], [(0, 3, 1), (2, 3, 1), (3, 2, .5)], [(0, 0, 1), (2, 0, 1), (3, 1, .5)]]
    maq = [(0, 'D', 1.0), (1, 'T', .6), (1.5, 'K', .3), (2, 'T', .5), (2.5, 'D', .8), (3, 'T', .6), (3.5, 'K', .3)]
    groove = [maq, maq, maq, [(0, 'D', 1.0), (1, 'T', .6), (2, 'T', .5), (2.5, 'D', .8), (3, 'T', .6), (3.25, 'K', .3), (3.5, 'T', .5), (3.75, 'K', .3)]]
    chords = [[73.42, 110.0], [98.0, 146.83], [77.78, 116.54], [73.42, 110.0]]
    render_track(92, 24, mel, bass, groove, 0.7, chords, 'table_theme', chime_at=[(0, 1174.7), (48, 1568.0)])

if __name__ == '__main__':
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    if what in ('sfx', 'all'): sfx()
    if what in ('music', 'all'): music()
