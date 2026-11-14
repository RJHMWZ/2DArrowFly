using System;
using UnityEngine;

namespace HueRelay
{
    /// <summary>
    /// 通过音频线程实时合成原 Web Audio 音效，包含正弦声部、3800 Hz 低通和软拐点压缩。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class HueAudio : MonoBehaviour
    {
        /// <summary>
        /// 可复用的单个振荡声部；phase 使用弧度，time 和 duration 使用秒。
        /// </summary>
        struct Voice
        {
            public double phase;
            public double frequency;
            public double time;
            public double duration;
            public float volume;
            public bool active;
        }

        /// <summary>
        /// 固定 48 声部池，音频回调期间不创建声部对象。
        /// </summary>
        private readonly Voice[] voices = new Voice[48];

        /// <summary>
        /// 同步主线程写入声部和音频线程混音的锁。
        /// </summary>
        private readonly object gate = new object();

        /// <summary>
        /// 当前音频输出采样率。
        /// </summary>
        private int rate;

        /// <summary>
        /// 动态压缩器追踪的振幅包络。
        /// </summary>
        private float env;
        private double b0;
        private double b1;
        private double b2;
        private double a1;
        private double a2;
        private double z1;
        private double z2;

        /// <summary>
        /// 原 HTML 的接力音阶，单位为相对于基音的半音数。
        /// </summary>
        private static readonly int[] Scale =
        {
            0,
            2,
            4,
            7,
            9,
            12,
            14,
            16,
            19,
            21,
            19,
            16
        };

        /// <summary>
        /// 根据当前采样率计算 Q=0.3 的低通系数，并启动供音频回调使用的循环静音总线。
        /// </summary>
        private void Awake()
        {
            rate = AudioSettings.outputSampleRate;
            double w = 2 * Math.PI * 3800 / rate;
            double cs = Math.Cos(w);
            double alpha = Math.Sin(w) / .6;
            double den = 1 + alpha;
            b0 = b2 = (1 - cs) * .5 / den;
            b1 = (1 - cs) / den;
            a1 = -2 * cs / den;
            a2 = (1 - alpha) / den;
            var s = GetComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0;
            var c = AudioClip.Create("Web Audio oscillator bus", 256, 1, rate, false);
            s.clip = c;
            s.Play();
        }

        /// <summary>
        /// 从固定声部池取得空闲槽位，安排带 8 毫秒起音和指数衰减的正弦音。
        /// </summary>
        /// <param name="f">正弦音基频，单位为 Hz。</param>
        /// <param name="d">音量包络持续时间，单位为秒。</param>
        /// <param name="volume">峰值音量，按原版限制为不超过 0.2。</param>
        public void Tone(double f, double d = .3, double volume = .13)
        {
            lock (gate)
            {
                for (int i = 0; i < voices.Length; i++)
                    if (!voices[i].active)
                    {
                        voices[i] = new Voice
                        {
                            frequency = f,
                            duration = d,
                            volume = (float)Math.Min(volume, .2),
                            active = true
                        };
                        break;
                    }
            }
        }

        /// <summary>
        /// 按连锁序号播放原版音阶，并在高连锁时叠加低八度和泛音。
        /// </summary>
        /// <param name="k">从 0 开始的连锁音阶序号。</param>
        public void Blip(int k)
        {
            int i = k < 10 ? k : 10 + (k - 10) % 2;
            double f = 330 * Math.Pow(2, Scale[i] / 12.0);
            Tone(f, .3, .11);
            if (k >= 2)
                Tone(f / 2, .36, .05);
            if (k >= 5)
                Tone(f * 1.26, .34, .03);
        }

        /// <summary>
        /// 计算一段接力结束时所用的和弦基频。
        /// </summary>
        /// <param name="k">当前连锁长度，从 1 开始。</param>
        /// <returns>接力结束和弦的基频，单位为 Hz。</returns>
        public static double Ending(int k)
        {
            return 262 * Math.Pow(2, Scale[Math.Min(k - 1, 9)] / 12.0);
        }

        /// <summary>
        /// 在音频线程混合声部、执行低通与动态压缩；禁止在此访问场景对象或创建 Unity 资源。
        /// </summary>
        /// <param name="data">当前音频输出缓冲区。</param>
        /// <param name="channels">每帧音频采样的声道数量。</param>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (rate <= 0)
                return;
            double dt = 1.0 / rate;
            lock (gate)
            {
                for (int n = 0; n < data.Length; n += channels)
                {
                    double sample = 0;
                    for (int i = 0; i < voices.Length; i++)
                    {
                        var v = voices[i];
                        if (!v.active)
                            continue;
                        double gain = v.time < .008 ? .0001 + (v.volume - .0001) * v.time / .008 : v.volume * Math.Pow(.0001 / v.volume, (v.time - .008) / Math.Max(.001, v.duration - .008));
                        sample += Math.Sin(v.phase) * gain;
                        v.phase += Math.PI * 2 * v.frequency * dt;
                        v.time += dt;
                        if (v.time > v.duration + .02)
                            v.active = false;
                        voices[i] = v;
                    }

                    double filtered = b0 * sample + z1;
                    z1 = b1 * sample - a1 * filtered + z2;
                    z2 = b2 * sample - a2 * filtered;
                    float lp = (float)filtered;
                    float amplitude = Mathf.Abs(lp);
                    float coeff = 1 - Mathf.Exp(-1f / (rate * (amplitude > env ? .003f : .25f)));
                    env += coeff * (amplitude - env);
                    float db = 20 * Mathf.Log10(Mathf.Max(.000001f, env));
                    float over = db + 24;
                    float gainDB = over <= -15 ? 0 : over >= 15 ? -over * (1 - 1 / 12f) : -(1 - 1 / 12f) * (over + 15) * (over + 15) / 60;
                    float output = lp * Mathf.Pow(10, gainDB / 20) * .9f;
                    for (int ch = 0; ch < channels; ch++)
                        data[n + ch] = output;
                }
            }
        }

        /// <summary>
        /// 按平台能力播放与 Web Vibration 数组对应的震动节奏；编辑器中不触发设备震动。
        /// </summary>
        /// <param name="durations">震动、停顿交替的毫秒时长，首项表示震动。</param>
        public static void Vibrate(params long[] durations)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (durations.Length == 1)
                        vibrator.Call("vibrate", durations[0]);
                    else
                    {
                        var pattern = new long[durations.Length + 1];
                        Array.Copy(durations, 0, pattern, 1, durations.Length);
                        vibrator.Call("vibrate", pattern, -1);
                    }
                }
            }
            catch (Exception)
            {
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// 释放运行时创建的音频总线 Clip。
        /// </summary>
        private void OnDestroy()
        {
            var s = GetComponent<AudioSource>();
            if (s && s.clip)
                Destroy(s.clip);
        }
    }
}
