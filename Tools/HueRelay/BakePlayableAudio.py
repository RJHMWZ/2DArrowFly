"""Bake the finite HueRelay tone palette for Playworks / WebGL audio.

Run from the Unity project root. Native players retain the original synthesis.
Existing asset GUIDs are preserved when rebaking.
"""
import math
import pathlib
import re
import struct
import uuid
import wave

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets/HueRelay/Audio/Playable'
OUTPUT.mkdir(parents=True, exist_ok=True)
SCALE = [0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 19, 16]
# Matches HueAudio.OutputGain. The default effectsVolume=.5 produces +6 dB;
# storing the gain in the samples avoids Playworks clamping playback gains >1.
OUTPUT_GAIN = 4.
tones = {(180., .14), (330., .1), (140., .2), (660., .14)}
for note in SCALE:
    frequency = 330 * 2 ** (note / 12)
    tones.update([(frequency, .3), (frequency / 2, .36), (frequency * 1.26, .34)])
for note in SCALE[:10]:
    for ratio in [1, 1.26, 1.5]:
        tones.add((262 * 2 ** (note / 12) * ratio, .9))
for ratio in [1, 1.26, 1.5, 2]:
    tones.add((392 * ratio, .7))
for note in [0, 4, 7, 12, 16]:
    f = 392 * 2 ** (note / 12)
    tones.update([(f, .7), (f / 2, .8)])

rate = 22050
w = 2 * math.pi * 3800 / rate
alpha = math.sin(w) / .6
den = 1 + alpha
b0 = b2 = (1 - math.cos(w)) * .5 / den
b1 = (1 - math.cos(w)) / den
a1, a2 = -2 * math.cos(w) / den, (1 - alpha) / den
entries = []
for index, (frequency, duration) in enumerate(sorted(tones)):
    path = OUTPUT / f'tone_{frequency:.6f}_{duration:.2f}.wav'
    meta = pathlib.Path(str(path) + '.meta')
    if meta.exists():
        guid = re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
    else:
        guid = uuid.uuid4().hex
        meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 1
    quality: 0.65
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf-8')
    data = bytearray()
    z1 = z2 = env = 0.
    for n in range(int((duration + .02) * rate)):
        t = n / rate
        gain = .0001 + (.2 - .0001) * t / .008 if t < .008 else .2 * (.0001 / .2) ** ((t - .008) / max(.001, duration - .008))
        value = math.sin(2 * math.pi * frequency * t) * gain
        filtered = b0 * value + z1
        z1, z2 = b1 * value - a1 * filtered + z2, b2 * value - a2 * filtered
        env += (1 - math.exp(-1 / (rate * (.003 if abs(filtered) > env else .25)))) * (abs(filtered) - env)
        over = 20 * math.log10(max(.000001, env)) + 24
        db = 0 if over <= -15 else -over * (1 - 1 / 12) if over >= 15 else -(1 - 1 / 12) * (over + 15) ** 2 / 60
        sample = filtered * 10 ** (db / 20) * .9 * OUTPUT_GAIN
        data.extend(struct.pack('<h', max(-32768, min(32767, round(sample * 32767)))))
    with wave.open(str(path), 'wb') as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(rate)
        stream.writeframes(data)
    entries.append(f'  - frequency: {frequency:.8f}\n    duration: {duration}\n    clip: {{fileID: 8300000, guid: {guid}, type: 3}}\n')

scene_path = ROOT / 'Assets/HueRelay/Scenes/HueRelay.unity'
scene = scene_path.read_text(encoding='utf-8-sig')
audio_guid = re.search(r'^guid: (\w+)', (ROOT/'Assets/HueRelay/Scripts/Audio/HueAudio.cs.meta').read_text(), re.M)[1]
pattern = r'(--- !u!114 &\d+\n(?:(?!--- !u!).)*?m_Script: \{fileID: 11500000, guid: ' + audio_guid + r'.*?\n)(.*?)(?=--- !u!|\Z)'
def bind(match):
    body = re.sub(r'  playableTones:\n.*', '', match[2], flags=re.S)
    return match[1] + body + '  playableTones:\n' + ''.join(entries)
scene, count = re.subn(pattern, bind, scene, count=1, flags=re.S)
assert count == 1, 'HueAudio scene component not found'
scene_path.write_text(scene, encoding='utf-8')
print(f'Baked and bound {len(entries)} playable tones.')
