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

        // Recorded sounds from Ivan's Rust & Blood SFX library win over the synthesised ones where they're in
        // Resources/Audio/SFX (see tools/sfx/pack_sfx.py); the call sites never change.
        public static AudioClip Gunshot => s_Gunshot ? s_Gunshot : (s_Gunshot = File("rifle_fire") ?? MakeGunshot());
        public static AudioClip DryFire => s_DryFire ? s_DryFire : (s_DryFire = File("rifle_empty") ?? MakeClick("DryFire", 0.04f, 0.5f, 11));
        public static AudioClip MagOut => s_MagOut ? s_MagOut : (s_MagOut = File("mag_out") ?? MakeClick("MagOut", 0.09f, 0.6f, 12));
        public static AudioClip MagIn => s_MagIn ? s_MagIn : (s_MagIn = File("mag_in") ?? MakeClick("MagIn", 0.12f, 0.8f, 13));
        public static AudioClip HitTick => s_HitTick ? s_HitTick : (s_HitTick = MakeTone("HitTick", 0.05f, 1900f, 0f, 0.35f));
        public static AudioClip HeadshotTick => s_HeadshotTick ? s_HeadshotTick : (s_HeadshotTick = MakeTone("HeadshotTick", 0.12f, 2600f, 1300f, 0.45f));
        public static AudioClip KillTick => s_KillTick ? s_KillTick : (s_KillTick = MakeTone("KillTick", 0.16f, 1200f, 600f, 0.4f));
        public static AudioClip PlayerHurt => s_PlayerHurt ? s_PlayerHurt : (s_PlayerHurt = File("body_impact") ?? MakeThud());
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

        static AudioClip s_Explosion, s_Concussion, s_Whoosh, s_KnifeHit, s_MeleeHit, s_PerkJingle, s_Radio, s_Plate, s_Drone, s_Pin;
        static AudioClip[] s_Blips;

        public static AudioClip Explosion => s_Explosion ? s_Explosion : (s_Explosion = File("explosion") ?? MakeExplosion("Explosion", 1.6f, 1f, 7));
        /// <summary>A sharp bang with a long high ring.</summary>
        public static AudioClip Concussion => s_Concussion ? s_Concussion : (s_Concussion = MakeConcussion());
        /// <summary>Air whoosh for melee swings and thrown knives.</summary>
        public static AudioClip Whoosh => s_Whoosh ? s_Whoosh : (s_Whoosh = File("swing") ?? MakeWhoosh());
        public static AudioClip KnifeHit => s_KnifeHit ? s_KnifeHit : (s_KnifeHit = MakeClick("KnifeHit", 0.1f, 0.9f, 21));
        public static AudioClip MeleeHit => s_MeleeHit ? s_MeleeHit : (s_MeleeHit = File("melee_hit") ?? MakeThud());

        static readonly System.Collections.Generic.Dictionary<string, AudioClip> s_Files = new System.Collections.Generic.Dictionary<string, AudioClip>();

        /// <summary>A recorded clip from Resources/Audio/SFX, or null when it isn't in the project.</summary>
        static AudioClip File(string name)
        {
            if (!s_Files.TryGetValue(name, out var clip)) s_Files[name] = clip = Resources.Load<AudioClip>("Audio/SFX/" + name);
            return clip;
        }

        /// <summary>A bullet chipping stone (recorded only: null when the library isn't in the project).</summary>
        public static AudioClip SurfaceHit => File("surface_hit");

        static readonly string[] FleshHits = { "flesh_hit_1", "flesh_hit_2", "flesh_hit_3" };

        /// <summary>A wet hit for a bullet striking a zombie (recorded only: null when the library isn't in the project).</summary>
        public static AudioClip FleshHit() => File(FleshHits[Random.Range(0, FleshHits.Length)]);

        /// <summary>
        /// A footstep on <paramref name="surface"/> (concrete, wood, metal, gravel, dirt), recorded only: null when the
        /// library isn't in the project, as there were no footsteps before it.
        /// </summary>
        public static AudioClip Footstep(string surface) => File("footstep_" + surface) ?? File("footstep_concrete");
        /// <summary>Grenade pin and spoon.</summary>
        public static AudioClip Pin => s_Pin ? s_Pin : (s_Pin = MakeTone("Pin", 0.08f, 3200f, 2600f, 0.3f));
        /// <summary>A bright little tune when a perk tonic is bought.</summary>
        public static AudioClip PerkJingle => s_PerkJingle ? s_PerkJingle : (s_PerkJingle = MakeChime("PerkJingle", 1.4f, 3.5f, 0.35f,
            new[] { 523.3f, 659.3f, 784f, 1046.5f }, new[] { 0f, 0.12f, 0.24f, 0.36f }));
        /// <summary>Radio squelch before and after the Dispatcher speaks.</summary>
        public static AudioClip Radio => s_Radio ? s_Radio : (s_Radio = MakeRadio());
        public static AudioClip ArmorPlate => s_Plate ? s_Plate : (s_Plate = MakeChime("Plate", 0.5f, 9f, 0.4f, new[] { 410f, 655f, 1230f }, new[] { 0f, 0.01f, 0.02f }));
        /// <summary>A low looping drone for the facility's ambience.</summary>
        public static AudioClip Drone => s_Drone ? s_Drone : (s_Drone = MakeDrone());

        static AudioClip s_Zap, s_Thunder, s_FieldHum, s_Surge;
        /// <summary>An electric crack: the reactor's arcs and the trial's lightning strikes.</summary>
        public static AudioClip Zap => s_Zap ? s_Zap : (s_Zap = MakeZap());
        /// <summary>A long rolling discharge, for the reactor waking and the trial ending.</summary>
        public static AudioClip Thunder => s_Thunder ? s_Thunder : (s_Thunder = MakeThunder());
        /// <summary>A looping buzz for the energy fields sealing a room.</summary>
        public static AudioClip FieldHum => s_FieldHum ? s_FieldHum : (s_FieldHum = MakeFieldHum());
        /// <summary>A looping, throbbing charge for the reactor while it wakes.</summary>
        public static AudioClip ReactorSurge => s_Surge ? s_Surge : (s_Surge = MakeSurge());

        /// <summary>A short syllable of placeholder "speech", for voice lines until real recordings exist.</summary>
        public static AudioClip RandomBlip()
        {
            if (s_Blips == null || s_Blips[0] == null)
            {
                s_Blips = new AudioClip[6];
                for (int i = 0; i < s_Blips.Length; i++) s_Blips[i] = MakeBlip(i);
            }
            return s_Blips[Random.Range(0, s_Blips.Length)];
        }

        static AudioClip MakeExplosion(string name, float length, float gain, int seed)
        {
            var rng = new System.Random(seed);
            int n = (int)(Rate * length);
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * Mathf.Lerp(0.6f, 0.02f, Mathf.Clamp01(t * 3f));
                lp2 += (lp - lp2) * 0.15f;
                float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 28f, Mathf.Clamp01(t * 2f)) * t) * Mathf.Exp(-t * 4f);
                float env = Mathf.Clamp01(t * 400f) * Mathf.Exp(-t * 2.6f);
                d[i] = Mathf.Clamp((lp2 * 2.2f + boom * 0.9f) * env * gain, -1f, 1f);
            }
            return Build(name, d);
        }

        static AudioClip MakeConcussion()
        {
            var rng = new System.Random(31);
            int n = (int)(Rate * 2.2f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float bang = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 30f);
                float ring = Mathf.Sin(2f * Mathf.PI * 3400f * t) * 0.12f * Mathf.Exp(-t * 1.2f) * Mathf.Clamp01(t * 20f);
                d[i] = Mathf.Clamp(bang * 0.9f + ring, -1f, 1f);
            }
            return Build("Concussion", d);
        }

        static AudioClip MakeWhoosh()
        {
            var rng = new System.Random(41);
            int n = (int)(Rate * 0.28f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float cutoff = Mathf.Lerp(0.05f, 0.35f, Mathf.Sin(Mathf.PI * t / 0.28f));
                lp += (noise - lp) * cutoff;
                d[i] = lp * Mathf.Sin(Mathf.PI * t / 0.28f) * 0.7f;
            }
            return Build("Whoosh", d);
        }

        static AudioClip MakeRadio()
        {
            var rng = new System.Random(51);
            int n = (int)(Rate * 0.32f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.35f;
                float beep = t < 0.09f ? Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 1250f * t)) * 0.18f : 0f;
                float env = Mathf.Clamp01(t * 80f) * Mathf.Clamp01((0.32f - t) * 20f);
                d[i] = (noise + beep) * env;
            }
            return Build("Radio", d);
        }

        /// <summary>A formant-ish syllable: a buzzy tone with a vowel-like filter sweep.</summary>
        static AudioClip MakeBlip(int variant)
        {
            float length = 0.11f + 0.03f * (variant % 3);
            int n = (int)(Rate * length);
            var d = new float[n];
            float f0 = 150f + 18f * variant;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float phase = (f0 * (1f + 0.15f * Mathf.Sin(t * 30f))) * t;
                float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
                float cutoff = Mathf.Lerp(0.08f, 0.3f, 0.5f + 0.5f * Mathf.Sin(t * (40f + variant * 9f)));
                lp += (saw - lp) * cutoff;
                float env = Mathf.Sin(Mathf.PI * t / length);
                d[i] = lp * env * 0.55f;
            }
            return Build("Blip" + variant, d);
        }

        static AudioClip MakeDrone()
        {
            const float length = 8f;
            int n = (int)(Rate * length);
            var d = new float[n];
            var rng = new System.Random(61);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Whole cycles of every partial in 8 s, so the loop has no seam.
                float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.15f
                            + Mathf.Sin(2f * Mathf.PI * 110.25f * t) * 0.08f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 4f));
                lp += ((float)(rng.NextDouble() * 2.0 - 1.0) - lp) * 0.01f;
                d[i] = (hum + lp * 0.6f) * 0.35f;
            }
            return Build("Drone", d);
        }

        static AudioClip MakeZap()
        {
            var rng = new System.Random(71);
            int n = (int)(Rate * 0.35f);
            var d = new float[n];
            float hp = 0f, last = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // High-passed noise in stuttering bursts, over a falling buzz.
                hp = 0.6f * (hp + noise - last);
                last = noise;
                float stutter = Mathf.Sin(2f * Mathf.PI * 38f * t) > -0.2f ? 1f : 0.25f;
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(240f, 90f, t / 0.35f) * t)) * 0.18f;
                float env = Mathf.Clamp01(t * 600f) * Mathf.Exp(-t * 11f);
                d[i] = Mathf.Clamp((hp * 0.9f * stutter + buzz) * env, -1f, 1f);
            }
            return Build("Zap", d);
        }

        static AudioClip MakeThunder()
        {
            var rng = new System.Random(73);
            const float length = 3.2f;
            int n = (int)(Rate * length);
            var d = new float[n];
            float lp = 0f, lp2 = 0f, hp = 0f, last = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                hp = 0.7f * (hp + noise - last);
                last = noise;
                lp += (noise - lp) * 0.05f;
                lp2 += (lp - lp2) * 0.08f;
                float crack = hp * Mathf.Exp(-t * 18f);
                // The rumble swells and rolls off in waves.
                float roll = lp2 * 4.2f * (0.6f + 0.4f * Mathf.Sin(t * 7f + Mathf.Sin(t * 2.3f) * 2f)) * Mathf.Clamp01(t * 6f) * Mathf.Exp(-t * 1.1f);
                float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(48f, 30f, t / length) * t) * 0.35f * Mathf.Exp(-t * 1.6f);
                d[i] = Mathf.Clamp(crack * 0.8f + roll + sub, -1f, 1f);
            }
            return Build("Thunder", d);
        }

        static AudioClip MakeFieldHum()
        {
            const float length = 2f;
            int n = (int)(Rate * length);
            var d = new float[n];
            var rng = new System.Random(79);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Whole cycles in 2 s so it loops cleanly: mains hum and its buzzy harmonics, with a little hiss.
                float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.3f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 120f * t)) * 0.06f
                            + Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.1f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 2f * t));
                float hiss = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.03f;
                d[i] = (hum + hiss) * 0.5f;
            }
            return Build("FieldHum", d);
        }

        static AudioClip MakeSurge()
        {
            const float length = 4f;
            int n = (int)(Rate * length);
            var d = new float[n];
            var rng = new System.Random(83);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Whole cycles in 4 s: a low throb at 2 beats a second over rising and falling overtones.
                float throb = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 2f * t);
                float body = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 90.25f * t) * 0.2f
                             + Mathf.Sin(2f * Mathf.PI * 135f * t) * 0.12f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.5f * t));
                lp += ((float)(rng.NextDouble() * 2.0 - 1.0) - lp) * 0.2f;
                float crackle = rng.NextDouble() < 0.0015 ? 0.6f : 0f;
                d[i] = Mathf.Clamp((body * throb + lp * 0.12f + crackle * lp) * 0.6f, -1f, 1f);
            }
            return Build("Surge", d);
        }

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

        static readonly AudioClip[] s_Upgraded = new AudioClip[4];

        /// <summary>
        /// The shot of a gun with an upgrade machine tier: plain for tier 0, then a sound of its own per tier so
        /// an upgraded gun is easy to pick out by ear. Mk II zaps, Mk III crackles with electricity, Mk IV booms.
        /// </summary>
        public static AudioClip GunshotFor(int tier)
        {
            if (tier <= 0) return Gunshot;
            tier = Mathf.Min(tier, s_Upgraded.Length - 1);
            if (!s_Upgraded[tier]) s_Upgraded[tier] = tier == 1 ? MakeZapShot() : tier == 2 ? MakeArcShot() : MakePlasmaShot();
            return s_Upgraded[tier];
        }

        /// <summary>The crack every shot starts with: bright noise that dies away fast.</summary>
        static float Crack(System.Random rng, ref float lp, float t, float decay)
        {
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * Mathf.Lerp(0.9f, 0.08f, Mathf.Clamp01(t * 8f));
            return lp * Mathf.Exp(-t * decay);
        }

        /// <summary>Mk II: a short crack with a falling laser "pew" and an icy shimmer on top.</summary>
        static AudioClip MakeZapShot()
        {
            var rng = new System.Random(71);
            int n = (int)(Rate * 0.5f);
            var d = new float[n];
            float lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float crack = Crack(rng, ref lp, t, 26f);
                float freq = Mathf.Lerp(2600f, 420f, Mathf.Clamp01(t * 5f));
                phase += 2f * Mathf.PI * freq / Rate;
                float pew = Mathf.Sin(phase) * Mathf.Exp(-t * 9f);
                float shimmer = Mathf.Sin(2f * Mathf.PI * 5200f * t) * Mathf.Sin(2f * Mathf.PI * 37f * t) * Mathf.Exp(-t * 14f);
                float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 45f, Mathf.Clamp01(t * 6f)) * t) * Mathf.Exp(-t * 28f);
                d[i] = Mathf.Clamp(crack * 0.6f + pew * 0.45f + shimmer * 0.12f + thump * 0.6f, -1f, 1f);
            }
            return Build("Gunshot Mk II", d);
        }

        /// <summary>Mk III: a buzzing electric arc with crackling sparks and a rising whine.</summary>
        static AudioClip MakeArcShot()
        {
            var rng = new System.Random(72);
            int n = (int)(Rate * 0.6f);
            var d = new float[n];
            float lp = 0f, whinePhase = 0f, spark = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float crack = Crack(rng, ref lp, t, 20f);
                // A sawtooth buzz, chopped by a fast tremolo.
                float saw = 2f * ((140f * t) % 1f) - 1f;
                float buzz = saw * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 34f * t)) * Mathf.Exp(-t * 6f);
                // Sparks: random snaps that thin out.
                if (rng.NextDouble() < 0.004 * Mathf.Exp(-t * 4f)) spark = (float)(rng.NextDouble() * 2.0 - 1.0);
                spark *= 0.93f;
                whinePhase += 2f * Mathf.PI * Mathf.Lerp(600f, 1500f, Mathf.Clamp01(t * 3f)) / Rate;
                float whine = Mathf.Sin(whinePhase) * Mathf.Exp(-t * 7f);
                float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 40f, Mathf.Clamp01(t * 6f)) * t) * Mathf.Exp(-t * 24f);
                d[i] = Mathf.Clamp(crack * 0.55f + buzz * 0.3f + spark * 0.5f + whine * 0.18f + thump * 0.7f, -1f, 1f);
            }
            return Build("Gunshot Mk III", d);
        }

        /// <summary>Mk IV: a deep plasma boom with a low whoosh and a throbbing echo.</summary>
        static AudioClip MakePlasmaShot()
        {
            var rng = new System.Random(73);
            int n = (int)(Rate * 0.85f);
            var dry = new float[n];
            float lp = 0f, rumble = 0f, tonePhase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float crack = Crack(rng, ref lp, t, 16f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                rumble += (noise - rumble) * 0.03f;
                float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 26f, Mathf.Clamp01(t * 3f)) * t) * Mathf.Exp(-t * 7f);
                tonePhase += 2f * Mathf.PI * Mathf.Lerp(180f, 520f, Mathf.Clamp01(t * 2.5f)) / Rate;
                float tone = (Mathf.Sin(tonePhase) + Mathf.Sin(tonePhase * 1.02f)) * 0.5f * Mathf.Exp(-t * 6f);
                dry[i] = crack * 0.6f + rumble * 3f * Mathf.Exp(-t * 5f) + boom * 0.9f + tone * 0.25f;
            }
            var d = new float[n];
            int echo = (int)(Rate * 0.11f);
            for (int i = 0; i < n; i++)
            {
                float s = dry[i];
                if (i >= echo) s += dry[i - echo] * 0.35f;
                if (i >= echo * 2) s += dry[i - echo * 2] * 0.15f;
                d[i] = Mathf.Clamp(s, -1f, 1f);
            }
            return Build("Gunshot Mk IV", d);
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
