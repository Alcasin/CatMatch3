"""Original CatMatch3 SFX. Python 3 standard library only; no external audio inputs.

Run from any directory: python Tools/generate_sfx.py [--check]
Writes only the seven generated WAVs; existing Unity metadata is never changed.
"""
import argparse
import hashlib
import io
import math
from pathlib import Path
import random
import struct
import wave

RATE = 22050
OUTPUT = Path(__file__).resolve().parents[1] / "Assets/_CatMatch3/Art/Audio/SFX"
DURATIONS = {"Select": 0.055, "Invalid": 0.16, "Match": 0.12,
             "Striped": 0.22, "Combo": 0.26, "Win": 0.62, "Fail": 0.44}
TAU = 2 * math.pi


def bell(t, start, frequency, length, gain=1.0):
    age = t - start
    if age < 0 or age >= length:
        return 0.0
    attack = min(1.0, age / 0.006)
    release = min(1.0, (length - age) / 0.03)
    envelope = attack * release * math.exp(-5.5 * age / length)
    return gain * envelope * (math.sin(TAU * frequency * age)
                              + 0.22 * math.sin(TAU * frequency * 2 * age)
                              + 0.06 * math.sin(TAU * frequency * 3 * age))


def render(name, duration):
    rng = random.Random(0xCA7300 + list(DURATIONS).index(name))
    samples = []
    low = slow = 0.0
    for i in range(round(duration * RATE)):
        t = i / RATE
        u = t / duration
        noise = rng.uniform(-1, 1)
        low += 0.32 * (noise - low)
        slow += 0.06 * (noise - slow)
        band = low - slow
        if name == "Select":
            value = bell(t, 0, 950, duration, 0.65) + 0.08 * band * math.exp(-t * 100)
        elif name == "Invalid":
            phase = TAU * (240 * t - 45 * t * t / duration)
            value = (math.sin(phase) + 0.12 * math.sin(2 * phase)) * math.exp(-t * 25)
        elif name == "Match":
            phase = TAU * (640 * t - 130 * t * t / duration)
            value = 0.8 * math.sin(phase) * math.exp(-t * 36) + 0.18 * band * math.exp(-t * 55)
        elif name == "Striped":
            envelope = math.sin(math.pi * u) ** 1.6
            value = envelope * (0.9 * band + 0.06 * math.sin(TAU * (520 * t + 350 * t * t)))
        elif name == "Combo":
            value = bell(t, 0, 783.991, 0.25, 0.7) + bell(t, 0.055, 987.767, 0.205, 0.5)
        elif name == "Win":
            value = sum(bell(t, start, freq, length, gain) for start, freq, length, gain in
                        ((0, 523.251, 0.28, 0.7), (0.15, 659.255, 0.28, 0.7), (0.30, 783.991, 0.32, 0.8)))
        else:
            value = bell(t, 0, 329.628, 0.28, 0.75) + bell(t, 0.18, 261.626, 0.26, 0.65)
        samples.append(value)

    # Remove DC, soften both endpoints and retain ample headroom in every clip.
    mean = sum(samples) / len(samples)
    for i in range(len(samples)):
        fade = min(1.0, i / (RATE * 0.004), (len(samples) - 1 - i) / (RATE * 0.012))
        samples[i] = math.tanh((samples[i] - mean) * fade)
    window = [min(1.0, i / (RATE * 0.004), (len(samples) - 1 - i) / (RATE * 0.012)) for i in range(len(samples))]
    offset = sum(samples) / sum(window)
    samples = [value - offset * weight for value, weight in zip(samples, window)]
    peak = max(abs(v) for v in samples)
    pcm = [round(v * 0.55 / peak * 32767) for v in samples]
    stream = io.BytesIO()
    with wave.open(stream, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(struct.pack("<" + "h" * len(pcm), *pcm))
    return stream.getvalue(), pcm


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify deterministic bytes and waveform bounds without writing")
    args = parser.parse_args()
    if not args.check:
        OUTPUT.mkdir(parents=True, exist_ok=True)
    total = 0
    for name, duration in DURATIONS.items():
        data, pcm = render(name, duration)
        path = OUTPUT / f"SFX_{name}.wav"
        if args.check:
            if not path.is_file() or path.read_bytes() != data:
                raise SystemExit(f"Missing or changed generated cue: {path}")
        else:
            path.write_bytes(data)
        assert pcm[0] == pcm[-1] == 0
        assert max(abs(v) for v in pcm) < 32767
        assert abs(sum(pcm) / len(pcm) / 32767) < 0.002
        rms = math.sqrt(sum((v / 32767) ** 2 for v in pcm) / len(pcm))
        print(f"SFX_{name}: {len(pcm) / RATE:.3f}s, mono PCM16/22050Hz, peak=0.55, rms={rms:.3f}, sha256={hashlib.sha256(data).hexdigest()[:12]}")
        total += len(data)
    print(f"{'Verified' if args.check else 'Generated'} 7 original cues, {total} bytes total.")


if __name__ == "__main__":
    main()
