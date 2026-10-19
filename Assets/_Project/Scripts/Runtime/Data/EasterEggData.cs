using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>What a step of the easter egg asks of the squad. New kinds of step get a case in EasterEgg.</summary>
    public enum EggStepKind : byte
    {
        /// <summary>Everyone in one room, sealed in, until a set number of extra zombies are dead.</summary>
        Survival,
        /// <summary>Not built yet: shown as a locked step.</summary>
        Locked,
    }

    /// <summary>One line Doctor Morrow says: its text (also the subtitle) and the voice file that plays it.</summary>
    public readonly struct EggLine
    {
        /// <summary>The clip's name in Resources/Audio/Voice/Morrow. Without the file, a placeholder voice plays.</summary>
        public readonly string id;
        public readonly string text;

        public EggLine(string id, string text)
        {
            this.id = id;
            this.text = text;
        }
    }

    /// <summary>One step of the easter egg: what it asks, where, and what Doctor Morrow says around it.</summary>
    public sealed class EggStep
    {
        public string title;
        public string hint;
        public EggStepKind kind;
        /// <summary>The room the step happens in.</summary>
        public int zone;
        /// <summary>Survival: how many extra zombies to kill, and what type they are.</summary>
        public int zombies;
        public ZombieKind zombieKind;
        public EggLine[] onStart;
        /// <summary>Survival: lines at kill counts along the way (kills, line).</summary>
        public (int kills, EggLine line)[] along;
        public EggLine[] onComplete;
    }

    /// <summary>
    /// The map's easter egg: Doctor Ignatius Morrow's unfinished work in the Lantern Core. Step one, waking the
    /// reactor, is built: the whole squad gathers in the Lantern Core, presses the reactor, and is sealed in with
    /// fifty runners that don't count toward the round. Four more steps follow later; adding one is a new entry
    /// in Steps (and, for a new kind of step, a case in EasterEgg).
    /// </summary>
    public static class EasterEggData
    {
        public const string ScientistName = "Dr. Morrow";
        public static readonly Color ScientistColor = new Color(0.55f, 1f, 0.75f);
        /// <summary>Where Doctor Morrow's voice files go, under Resources.</summary>
        public const string VoiceFolder = "Audio/Voice/Morrow/";
        /// <summary>The Lantern Core, where the reactor stands.</summary>
        public const int LanternZone = 9;

        public static readonly EggStep[] Steps =
        {
            new EggStep
            {
                title = "Wake the Lantern",
                hint = "Gather the whole squad in the Lantern Core and touch the reactor.",
                kind = EggStepKind.Survival,
                zone = LanternZone,
                zombies = 50,
                zombieKind = ZombieKind.Runner,
                onStart = new[]
                {
                    new EggLine("s1_start_1", "Ahh... hands on my Lantern. Warm hands. Living hands. How long has it been?"),
                    new EggLine("s1_start_2", "Don't be frightened. I am Ignatius Morrow. I built this machine, long before Vale put his name on it."),
                    new EggLine("s1_start_3", "You want to know what it is for? My primal goal, my one true work: to burn death out of the world. Forever."),
                    new EggLine("s1_start_4", "But the Lantern is cold, and it is hungry. Feed it the dead. Fifty of them should wake it. I've sealed the doors, so nobody wanders off."),
                },
                along = new[]
                {
                    (10, new EggLine("s1_along_1", "Yes! Can you feel the floor humming? The core remembers what it was made for.")),
                    (25, new EggLine("s1_along_2", "Halfway. Vale called this a battery. A battery! It is a heart, and it is starting to beat.")),
                    (40, new EggLine("s1_along_3", "Almost there. Keep them coming. Don't let them near the glass!")),
                },
                onComplete = new[]
                {
                    new EggLine("s1_done_1", "Listen to it sing. The Lantern is awake, for the first time in eleven years."),
                    new EggLine("s1_done_2", "Vale only ever wanted a battery. I wanted a door. And you have just knocked on it."),
                    new EggLine("s1_done_3", "This was only the first spark. Come back to me when you're ready. There is so much more to do."),
                },
            },
            Locked("The Second Spark"),
            Locked("The Third Spark"),
            Locked("The Fourth Spark"),
            Locked("The Door"),
        };

        static EggStep Locked(string title) => new EggStep
        {
            title = title,
            hint = "Doctor Morrow hasn't told you what comes next.",
            kind = EggStepKind.Locked,
            zone = LanternZone,
        };

        /// <summary>How many steps are built and playable.</summary>
        public static int PlayableSteps
        {
            get
            {
                int n = 0;
                while (n < Steps.Length && Steps[n].kind != EggStepKind.Locked) n++;
                return n;
            }
        }

        public static EggStep At(int step) => step >= 0 && step < Steps.Length ? Steps[step] : null;

        /// <summary>Zombies of a survival step alive at once: a dozen, and a few more for each extra player.</summary>
        public static int AliveCap(int players) => 12 + 3 * Mathf.Max(0, players - 1);
        /// <summary>A survival step's zombies have this share of the round's zombie health, so fifty of them stay a fair fight.</summary>
        public const float HealthScale = 0.7f;
        public const float SpawnInterval = 0.45f;
    }
}
