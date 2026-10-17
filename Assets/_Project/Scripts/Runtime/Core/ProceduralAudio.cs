using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Synthesises placeholder sound effects at startup so the prototype has audio feedback with no assets.
    /// Replace with real clips (Addressables) in Phase 7; keep the same call sites.
    /// </summary>
    public static class ProceduralAudio
    {
        const int Rate = 44100;

        static AudioClip s_Gunshot, s_DryFire, s_MagOut, s_MagIn, s_HitTick, s_HeadshotTick, s_KillTick, s_PlayerHurt, s_ZombieSwipe;
        static AudioClip s_RoundStart, s_RoundEnd, s_Purchase, s_Denied, s_DoorOpen;
        static AudioClip[] s_Groans;

        public static AudioClip Gunshot => s_Gunshot ? s_Gunshot : (s_Gunshot = MakeGunshot());
        public static AudioClip DryFire => s_DryFire ? s_DryFire : (s_DryFire = MakeClick("DryFire", 0.04f, 0.5f, 11));
        public static AudioClip MagOut => s_MagOut ? s_MagOut : (s_MagOut = MakeClick("MagOut", 0.09f, 0.6f, 12));
        public static AudioClip MagIn => s_MagIn ? s_MagIn : (s_MagIn = MakeClick("MagIn", 0.12f, 0.8f, 13));
        public static AudioClip HitTick => s_HitTick ? s_HitTick : (s_HitTick = MakeTone("HitTick", 0.05f, 1900f, 0f, 0.35f));
        public static AudioClip HeadshotTick => s_HeadshotTick ? s_HeadshotTick : (s_HeadshotTick = MakeTone("HeadshotTick", 0.12f, 2600f, 1300f, 0.45f));
        public static AudioClip KillTick => s_KillTick ? s_KillTick : (s_KillTick = MakeTone("KillTick", 0.16f, 1200f, 600f, 0.4f));
        public static AudioClip PlayerHurt => s_PlayerHurt ? s_PlayerHurt : (s_PlayerHurt = MakeThud());
        public static AudioClip ZombieSwipe => s_ZombieSwipe ? s_ZombieSwipe : (s_ZombieSwipe = MakeSwipe());
        // Low, ominous chord as a round begins; a rising three-note figure when it is cleared.
        public static AudioClip RoundStart => s_RoundStart ? s_RoundStart : (s_RoundStart = MakeChime("RoundStart", 3f, 1.2f, 0.5f,
            new[] { 55f, 82.4f, 110f, 130.8f }, new[] { 0f, 0f, 0.05f, 0.1f }));
        public static AudioClip Purchase => s_Purchase ? s_Purchase : (s_Purchase = MakeChime("Purchase", 0.6f, 7f, 0.45f,
            new[] { 1318.5f, 1760f }, new[] { 0f, 0.07f }));
        public static AudioClip Denied => s_Denied ? s_Denied : (s_Denied = MakeBuzz());
        public static AudioClip DoorOpen => s_DoorOpen ? s_DoorOpen : (s_DoorOpen = MakeRumble());
        public static AudioClip RoundEnd => s_RoundEnd ? s_RoundEnd : (s_RoundEnd = MakeChime("RoundEnd", 2.2f, 2.2f, 0.4f,
            new[] { 220f, 261.6f, 329.6f }, new[] { 0f, 0.22f, 0.44f }));

        public static AudioClip RandomGroan()
        {
            if (s_Groans == null || s_Groans[0] == null)
            {
                s_Groans = new AudioClip[3];
                for (int i = 0; i < s_Groans.Length; i++) s_Groans[i] = MakeGroan(i);
            }
            return s_Groans[Random.Range(0, s_Groans.Length)];
        }

        static AudioClip Build(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeGunshot()
        {
            var rng = new System.Random(1);
            int n = (int)(Rate * 0.45f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Crack: bright noise with a very fast decay, then a darker tail.
                lp += (noise - lp) * Mathf.Lerp(0.9f, 0.08f, Mathf.Clamp01(t * 8f));
                float crack = lp * Mathf.Exp(-t * 18f);
                // Thump: pitch-dropping sine for body.
                float freq = Mathf.Lerp(110f, 38f, Mathf.Clamp01(t * 6f));
                float thump = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 26f);
                d[i] = Mathf.Clamp(crack * 0.9f + thump * 0.8f, -1f, 1f);
            }
            return Build("Gunshot", d);
        }

        static AudioClip MakeClick(string name, float length, float gain, int seed)
        {
            var rng = new System.Random(seed);
            int n = (int)(Rate * length);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float metal = Mathf.Sin(2f * Mathf.PI * 3100f * t) * 0.4f;
                d[i] = (noise * 0.6f + metal) * Mathf.Exp(-t * 90f) * gain;
            }
            return Build(name, d);
        }

        static AudioClip MakeTone(string name, float length, float f1, float f2, float gain)
        {
            int n = (int)(Rate * length);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 35f);
                float s = Mathf.Sin(2f * Mathf.PI * f1 * t);
                if (f2 > 0f) s = s * 0.6f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.4f;
                d[i] = s * env * gain;
            }
            return Build(name, d);
        }

        /// <summary>Struck-bell notes: each a fundamental plus a quieter octave, decaying from its start time.</summary>
        static AudioClip MakeChime(string name, float length, float decay, float gain, float[] freqs, float[] starts)
        {
            int n = (int)(Rate * length);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float sum = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    float local = t - starts[k];
                    if (local < 0f) continue;
                    float env = Mathf.Exp(-local * decay) * Mathf.Clamp01(local * 200f);
                    float f = freqs[k];
                    sum += (Mathf.Sin(2f * Mathf.PI * f * local) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * local)) * env;
                }
                d[i] = Mathf.Clamp(sum * gain / Mathf.Sqrt(freqs.Length), -1f, 1f);
            }
            return Build(name, d);
        }

        static AudioClip MakeBuzz()
        {
            int n = (int)(Rate * 0.32f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Two detuned square waves: a flat "no".
                float a = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 98f * t));
                float b = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 104f * t));
                float env = Mathf.Clamp01(t * 80f) * Mathf.Clamp01((0.32f - t) * 20f);
                d[i] = (a + b) * 0.12f * env;
            }
            return Build("Denied", d);
        }

        static AudioClip MakeRumble()
        {
            var rng = new System.Random(7);
            int n = (int)(Rate * 1.1f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.04f;
                float env = Mathf.Clamp01(t * 20f) * Mathf.Exp(-t * 2.5f);
                d[i] = Mathf.Clamp(lp * 3.5f * env + Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.25f * env, -1f, 1f);
            }
            return Build("DoorOpen", d);
        }

        static AudioClip MakeThud()
        {
            var rng = new System.Random(5);
            int n = (int)(Rate * 0.3f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.05f;
                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 45f, t * 3f) * t);
                d[i] = (body * 0.8f + lp * 2f) * Mathf.Exp(-t * 14f) * 0.8f;
            }
            return Build("PlayerHurt", d);
        }

        static AudioClip MakeSwipe()
        {
            var rng = new System.Random(6);
            int n = (int)(Rate * 0.25f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * Mathf.Lerp(0.05f, 0.4f, t / 0.25f);
                float env = Mathf.Sin(Mathf.PI * t / 0.25f);
                d[i] = lp * env * 0.7f;
            }
            return Build("ZombieSwipe", d);
        }

        static AudioClip MakeGroan(int variant)
        {
            var rng = new System.Random(100 + variant);
            float length = 1.1f + variant * 0.25f;
            int n = (int)(Rate * length);
            var d = new float[n];
            float baseFreq = 62f + variant * 11f;
            float phase = 0f, lp = 0f, breath = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float freq = baseFreq + Mathf.Sin(t * (5f + variant)) * 9f - t * 12f;
                phase += freq / Rate;
                float saw = (phase % 1f) * 2f - 1f;
                lp += (saw - lp) * 0.06f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                breath += (noise - breath) * 0.12f;
                float env = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((length - t) / 0.35f);
                float tremolo = 0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 7f);
                d[i] = (lp * 1.4f + breath * 0.5f) * env * tremolo * 0.7f;
            }
            return Build("ZombieGroan" + variant, d);
        }
    }
}
