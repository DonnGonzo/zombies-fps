using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>What a step of the easter egg asks of the squad. Each kind is played out by EasterEgg.</summary>
    public enum EggStepKind : byte
    {
        /// <summary>Everyone in one room, sealed in, until a set number of extra zombies are dead.</summary>
        Survival,
        /// <summary>Relics set round an altar, then the room held for a set time while zombies pour in.</summary>
        Ritual,
        /// <summary>Keep someone alive through a big wave of zombies and monsters.</summary>
        Rescue,
        /// <summary>A special boss, with zombies coming on top, until the boss is dead.</summary>
        Boss,
        /// <summary>The final step: the blood altar, the god, the choice, and the fight that follows.</summary>
        Summon,
    }

    /// <summary>Who says a line of the easter egg.</summary>
    public enum EggSpeaker : byte
    {
        Morrow,
        Watchman,
        God,
        General,
    }

    /// <summary>The easter egg's own bosses (NetZombie draws a Warden body differently for each).</summary>
    public enum EggBoss : byte
    {
        None,
        /// <summary>The Rotbloom, keeper of the binding stones under the Greenhouse.</summary>
        Rotbloom,
        /// <summary>Azhrakel, the Ember Below: the god.</summary>
        God,
        /// <summary>Lord-General Corvin Ashgrave, the Betrayer.</summary>
        General,
    }

    /// <summary>The permanent reward the easter egg's ending leaves on every player for the rest of the game.</summary>
    public enum EggBoon : byte
    {
        None,
        /// <summary>Beat the god: everything doubled (damage, health, reload, stamina), and a little faster.</summary>
        Godblood,
        /// <summary>Killed the Betrayer for the god: three times the damage and health.</summary>
        EmberOath,
    }

    /// <summary>One line of the easter egg: who says it, its text (also the subtitle) and the voice file that plays it.</summary>
    public readonly struct EggLine
    {
        public readonly EggSpeaker speaker;
        /// <summary>The clip's name in the speaker's voice folder. Without the file, a placeholder voice plays.</summary>
        public readonly string id;
        public readonly string text;

        public EggLine(string id, string text) : this(EggSpeaker.Morrow, id, text) { }

        public EggLine(EggSpeaker speaker, string id, string text)
        {
            this.speaker = speaker;
            this.id = id;
            this.text = text;
        }
    }

    /// <summary>One step of the easter egg: what it asks, where, and what's said around it.</summary>
    public sealed class EggStep
    {
        public string title;
        public string hint;
        public EggStepKind kind;
        /// <summary>The room the step happens in (for the final step, the room its way down opens from).</summary>
        public int zone;
        /// <summary>Survival and Rescue: how many extra zombies to kill, and what type most of them are.</summary>
        public int zombies;
        public ZombieKind zombieKind;
        /// <summary>Ritual: how long the room has to be held, in seconds.</summary>
        public int seconds;
        /// <summary>Seconds of talk before the first zombie climbs out.</summary>
        public float introDelay = 9f;
        public EggLine[] onStart;
        /// <summary>Lines along the way, at a count: kills, or seconds held for a ritual.</summary>
        public (int at, EggLine line)[] along;
        public EggLine[] onComplete;
        /// <summary>Rescue: lines when the one being protected goes down.</summary>
        public EggLine[] onFail;
    }

    /// <summary>
    /// The map's easter egg: Doctor Ignatius Morrow's unfinished work, and what it was really for.
    ///
    /// 1. Wake the Lantern: the whole squad in the Lantern Core touches the reactor and holds out against fifty
    ///    runners. Doctor Morrow hands over a mana spark, alive but empty.
    /// 2. Charge the Spark: four relics, two in the Chapel and two in the Sunken Temple, go round the altar in the
    ///    Archives; the spark goes on the altar, and the room is held for three minutes.
    /// 3. The Watchman's Trust: Sergeant Brannock, last of the Lantern Watch, turns up in the Crane Yard. He
    ///    talks only after the squad keeps him alive through a hundred zombies and a pack of monsters, and tells
    ///    them what the spark is for: Azhrakel, a god of fire sealed under the facility, and Corvin Ashgrave, the
    ///    Watch's general who stole from it and fled.
    /// 4. The Rotbloom: the charged spark set in the binding stones under the Greenhouse wakes their keeper, a
    ///    boss. Its death gives back the spark and the Elemental Heart, and the stones open a way down.
    /// 5. The Ember Below: in the Blood Sanctum the spark and heart go on the blood altar and Azhrakel rises. Fight
    ///    him (his brand, a gun, and Godblood: double everything) or serve him and kill Ashgrave in his Hollow Keep
    ///    (Ember Oath: triple damage and health, and Ashgrave's sword, Oathbreaker).
    /// </summary>
    public static class EasterEggData
    {
        public const string ScientistName = "Dr. Morrow";
        public static readonly Color ScientistColor = new Color(0.55f, 1f, 0.75f);
        /// <summary>The Lantern Core, where the reactor stands.</summary>
        public const int LanternZone = 9;
        public const int ArchivesZone = 10;
        public const int ChapelZone = 3;
        public const int TempleZone = 17;
        public const int CraneYardZone = 8;
        public const int GreenhouseZone = 11;

        // ---------------------------------------------------------------- speakers

        public static string SpeakerName(EggSpeaker s) => s switch
        {
            EggSpeaker.Watchman => "Sgt. Brannock",
            EggSpeaker.God => "Azhrakel",
            EggSpeaker.General => "Gen. Ashgrave",
            _ => ScientistName,
        };

        public static Color SpeakerColor(EggSpeaker s) => s switch
        {
            EggSpeaker.Watchman => new Color(1f, 0.78f, 0.42f),
            EggSpeaker.God => new Color(1f, 0.36f, 0.24f),
            EggSpeaker.General => new Color(0.72f, 0.74f, 1f),
            _ => ScientistColor,
        };

        /// <summary>Where a speaker's voice files go, under Resources.</summary>
        public static string VoiceFolder(EggSpeaker s) => s switch
        {
            EggSpeaker.Watchman => "Audio/Voice/Brannock/",
            EggSpeaker.God => "Audio/Voice/Azhrakel/",
            EggSpeaker.General => "Audio/Voice/Ashgrave/",
            _ => "Audio/Voice/Morrow/",
        };

        /// <summary>Doctor Morrow talks over the lab's intercoms (and in your head, later); the others are in the room.</summary>
        public static bool OverRadio(EggSpeaker s) => s == EggSpeaker.Morrow;

        // ---------------------------------------------------------------- the steps

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
                    new EggLine("s1_done_3", "Take the spark from the core. Careful, it bites. It is alive, but empty. My old altar in the Archives can fill it, once its four relics are back where they belong. The monks hid them in the Chapel and the Sunken Temple."),
                },
            },
            new EggStep
            {
                title = "Charge the Spark",
                hint = "Find the four relics (two in the Chapel, two in the Sunken Temple), set them round the altar in the Archives, then lay the spark on it and hold the room for three minutes.",
                kind = EggStepKind.Ritual,
                zone = ArchivesZone,
                seconds = 180,
                zombieKind = ZombieKind.Walker,
                introDelay = 8f,
                onStart = new[]
                {
                    new EggLine("s2_start_1", "The circle is whole. Put the spark on the stone. Gently now."),
                    new EggLine("s2_start_2", "The monks of the Scriptorium filled their lamps here with mana, the old word for what runs under everything. It takes time, and the dead can smell it."),
                    new EggLine("s2_start_3", "Three minutes. Stay in the Archives and keep them off the altar. If the room empties, the ritual stalls."),
                },
                along = new[]
                {
                    (60, new EggLine("s2_along_1", "A third of the way. The spark is drinking. Can you hear it humming?")),
                    (120, new EggLine("s2_along_2", "Two minutes! It's brighter than I ever managed on my own.")),
                    (165, new EggLine("s2_along_3", "Nearly full. Don't let them touch the stone!")),
                },
                onComplete = new[]
                {
                    new EggLine("s2_done_1", "Full. Full of mana! Take it, quickly, before it cools."),
                    new EggLine("s2_done_2", "There was a man who knew what a charged spark was really for. The Watch kept a post in the Crane Yard. If any of them still breathe, find him."),
                },
            },
            new EggStep
            {
                title = "The Watchman's Trust",
                hint = "Find the old soldier in the Crane Yard and talk to him. Then keep him alive.",
                kind = EggStepKind.Rescue,
                zone = CraneYardZone,
                zombies = 100,
                zombieKind = ZombieKind.Walker,
                introDelay = 16f,
                onStart = new[]
                {
                    new EggLine(EggSpeaker.Watchman, "s3_start_1", "Stop right there. That glow in your hands. Where did you get a charged spark?"),
                    new EggLine(EggSpeaker.Watchman, "s3_start_2", "Morrow. Of course it's Morrow. I'm Brannock, last sergeant of the Lantern Watch, and I don't talk to strangers carrying his toys."),
                    new EggLine(EggSpeaker.Watchman, "s3_start_3", "You want answers? Then prove you're not his puppets. Hear that? They always come when that thing glows. Keep them off me!"),
                },
                along = new[]
                {
                    (30, new EggLine(EggSpeaker.Watchman, "s3_along_1", "Not bad. Not bad at all. Watch the containers, they come over the top!")),
                    (65, new EggLine(EggSpeaker.Watchman, "s3_along_2", "Brutes! Go for the head, they've got plate on everything else.")),
                    (100, new EggLine(EggSpeaker.Watchman, "s3_along_3", "Almost there! Clean up the rest, then the big one, and we're done!")),
                },
                onFail = new[]
                {
                    new EggLine(EggSpeaker.Watchman, "s3_fail_1", "Agh! I'm down... no, I'm alright. Pathetic. Come and talk to me when you can actually hold a line."),
                },
                onComplete = new[]
                {
                    new EggLine(EggSpeaker.Watchman, "s3_done_1", "Hah. You fight like the Watch did. Alright. I owe you a story."),
                    new EggLine(EggSpeaker.Watchman, "s3_done_2", "Morrow told you he wants to burn death out of the world. He wasn't lying. He just never told you what does the burning."),
                    new EggLine(EggSpeaker.Watchman, "s3_done_3", "Under this place sleeps Azhrakel, the Ember Below. A god of fire and blood. The Watch was founded to keep it sealed, and we kept it four hundred years."),
                    new EggLine(EggSpeaker.Watchman, "s3_done_4", "Then our own Lord-General, Corvin Ashgrave, made a bargain with it. He stole a piece of its power, betrayed the god and the Watch both, and vanished."),
                    new EggLine(EggSpeaker.Watchman, "s3_done_5", "That spark is a key. Under the Greenhouse are the old binding stones. Set the spark in them and the circle wakes, and so does its keeper. Kill it, and the way down opens."),
                },
            },
            new EggStep
            {
                title = "The Rotbloom",
                hint = "Set the charged spark in the binding stones in the Greenhouse, and kill what wakes up.",
                kind = EggStepKind.Boss,
                zone = GreenhouseZone,
                zombieKind = ZombieKind.Walker,
                introDelay = 10f,
                onStart = new[]
                {
                    new EggLine(EggSpeaker.Watchman, "s4_start_1", "That's it, the spark's in the stones. Now get ready. The circle has a keeper."),
                    new EggLine("s4_start_2", "What are you doing? No, no, not the garden! That circle was sealed for a reason!"),
                    new EggLine(EggSpeaker.Watchman, "s4_start_3", "The Rotbloom. Grown out of the god's own blood. Cut it down!"),
                },
                onComplete = new[]
                {
                    new EggLine(EggSpeaker.Watchman, "s4_done_1", "It's down! Take its heart, and get the spark out of the stones."),
                    new EggLine("s4_done_2", "You've opened it. Do you understand what you've done? ...No. Go on. I want to see it too."),
                    new EggLine(EggSpeaker.Watchman, "s4_done_3", "The stones are bleeding. That's the way down to the blood altar. Everyone goes together, or nobody goes."),
                },
            },
            new EggStep
            {
                title = "The Ember Below",
                hint = "Go down through the bleeding stones in the Greenhouse, all of you, and lay the spark and the heart on the blood altar.",
                kind = EggStepKind.Summon,
                zone = GreenhouseZone,
                zombieKind = ZombieKind.Runner,
                introDelay = 4f,
                onStart = new[]
                {
                    new EggLine(EggSpeaker.God, "s5_start_1", "A spark... and a heart. The little ones bring me gifts."),
                    new EggLine(EggSpeaker.God, "s5_start_2", "I am Azhrakel. I was burning before your kind learned fire, and I have slept beneath your walls four hundred years."),
                    new EggLine(EggSpeaker.God, "s5_start_3", "One of you was clever once. Corvin Ashgrave. He bowed, he swore, and then he stole from me and ran to a place even I cannot reach."),
                    new EggLine(EggSpeaker.God, "s5_start_4", "So choose. Raise your weapons against me and win, and I will give you my brand and my blood. Or serve me: go where I cannot, and bring me the Betrayer's death."),
                },
                onComplete = new EggLine[0],
            },
        };

        /// <summary>How many steps are built and playable.</summary>
        public static int PlayableSteps => Steps.Length;

        public static EggStep At(int step) => step >= 0 && step < Steps.Length ? Steps[step] : null;

        // ---------------------------------------------------------------- extra lines

        public static readonly EggLine FirstRelic = new EggLine("s2_relic_1", "A relic! The monks hid them well. Bring all four to my altar in the Archives.");
        public static readonly EggLine AllRelics = new EggLine("s2_ready_1", "All four are home. Now lay the spark on the altar, and whatever happens, don't leave the room.");
        public static readonly EggLine[] ChoseFight =
        {
            new EggLine(EggSpeaker.God, "s5_fight_1", "Brave. Foolish. Delicious. Come, then!"),
        };
        public static readonly EggLine[] ChoseServe =
        {
            new EggLine(EggSpeaker.God, "s5_serve_1", "Good. Through the fire, then. He waits for you in his Hollow Keep."),
        };
        public static readonly EggLine[] GeneralIntro =
        {
            new EggLine(EggSpeaker.General, "s5_gen_1", "Four hundred years, and my first guests are the devil's errand runners."),
            new EggLine(EggSpeaker.General, "s5_gen_2", "I'm Ashgrave. I led the Watch, I made the bargain, and yes, I broke it."),
            new EggLine(EggSpeaker.General, "s5_gen_3", "Azhrakel would have burned the world clean of everything that lives. I took enough of its fire to keep it asleep, and I have held its door shut from in here ever since."),
            new EggLine(EggSpeaker.General, "s5_gen_4", "But you came to kill me, so let's not pretend. Draw, then. Let's see what the god bought."),
        };
        public static readonly EggLine[] FightWon =
        {
            new EggLine(EggSpeaker.God, "s5_won_1", "Enough... enough! You have won. Take my brand, and take my blood. Let the world burn a little brighter in your hands."),
            new EggLine("s5_won_2", "A god, beaten! My door is open, and the dead have a new reason to fear us. Thank you, my friends. Thank you."),
        };
        public static readonly EggLine[] ServeWon =
        {
            new EggLine(EggSpeaker.General, "s5_gendone_1", "Well struck... Keep my blade. Oathbreaker. It never cared who held it."),
            new EggLine(EggSpeaker.God, "s5_gendone_2", "The Betrayer is dead. As I promised: my fire is yours, three times over. Now go, and remember who you serve."),
        };

        // ---------------------------------------------------------------- relics

        public const int RelicCount = 4;
        /// <summary>Relic names in relic order: the first two are in the Chapel, the others in the Sunken Temple.</summary>
        public static readonly string[] RelicNames = { "Watch Chalice", "Silver Bell", "Brass Lamp", "Star Tablet" };
        public static int RelicZone(int relic) => relic < 2 ? ChapelZone : TempleZone;

        // ---------------------------------------------------------------- tuning

        /// <summary>Zombies of a survival step alive at once: a dozen, and a few more for each extra player.</summary>
        public static int AliveCap(int players) => 12 + 3 * Mathf.Max(0, players - 1);
        /// <summary>A survival step's zombies have this share of the round's zombie health, so fifty of them stay a fair fight.</summary>
        public const float HealthScale = 0.7f;
        public const float SpawnInterval = 0.45f;

        /// <summary>The mana ritual: a steady stream, never a flood.</summary>
        public static int RitualAliveCap(int players) => 9 + 3 * Mathf.Max(0, players - 1);
        public const float RitualSpawnInterval = 1.5f;
        public const float RitualHealthScale = 0.8f;

        /// <summary>The Watchman's ambush: twelve monsters among the hundred, and a Warden to finish.</summary>
        public static readonly ZombieKind[] AmbushMonsters =
        {
            ZombieKind.Brute, ZombieKind.Spitter, ZombieKind.Crawler, ZombieKind.Brute, ZombieKind.Spitter, ZombieKind.Crawler,
            ZombieKind.Brute, ZombieKind.Spitter, ZombieKind.Crawler, ZombieKind.Brute, ZombieKind.Spitter, ZombieKind.Brute,
        };
        public static int AmbushAliveCap(int players) => 16 + 4 * Mathf.Max(0, players - 1);
        public const float AmbushSpawnInterval = 0.35f;
        /// <summary>The Watchman takes this many points of zombie damage before he goes down.</summary>
        public const float WatchmanHealth = 3000f;
        public const float WatchmanReach = 2.2f;

        /// <summary>Zombies alive at once alongside a boss.</summary>
        public static int BossAddsCap(int players) => 8 + 3 * Mathf.Max(0, players - 1);
        public const float BossAddsInterval = 1.6f;

        // ---------------------------------------------------------------- the bosses

        public static string BossName(EggBoss b) => b switch
        {
            EggBoss.Rotbloom => "The Rotbloom",
            EggBoss.God => "Azhrakel, the Ember Below",
            EggBoss.General => "Lord-General Ashgrave",
            _ => null,
        };

        /// <summary>Health, as a share of the round's zombie health, before the Warden's own multiplier and the squad's size.</summary>
        public static float BossHealth(EggBoss b) => b switch
        {
            EggBoss.Rotbloom => 1.6f,
            EggBoss.God => 2.6f,
            EggBoss.General => 2.2f,
            _ => 1f,
        };

        /// <summary>Size on top of the Warden's body.</summary>
        public static float BossScale(EggBoss b) => b switch
        {
            EggBoss.Rotbloom => 1.12f,
            EggBoss.God => 1.3f,
            EggBoss.General => 0.62f,
            _ => 1f,
        };

        /// <summary>Skin tint and the colour of the light it gives off.</summary>
        public static Color BossTint(EggBoss b) => b switch
        {
            EggBoss.Rotbloom => new Color(0.55f, 0.85f, 0.35f),
            EggBoss.God => new Color(1f, 0.35f, 0.22f),
            EggBoss.General => new Color(0.75f, 0.78f, 0.95f),
            _ => Color.white,
        };

        public static Color BossGlow(EggBoss b) => b switch
        {
            EggBoss.Rotbloom => new Color(0.5f, 1f, 0.3f),
            EggBoss.God => new Color(1f, 0.4f, 0.1f),
            EggBoss.General => new Color(0.55f, 0.6f, 1f),
            _ => Color.white,
        };

        /// <summary>Seconds between a boss's special moves (seed pods, fire rain, calling the fallen Watch).</summary>
        public static float BossMoveEvery(EggBoss b) => b switch
        {
            EggBoss.Rotbloom => 13f,
            EggBoss.God => 9f,
            EggBoss.General => 14f,
            _ => 15f,
        };

        /// <summary>Fire rain: a mark under each player, then the strike. Moving off the mark dodges it.</summary>
        public const float StrikeWarning = 1.4f;
        public const float StrikeRadius = 2.4f;
        public const float StrikeDamage = 55f;

        // ---------------------------------------------------------------- rewards

        public const string GodGunName = "Brand of Azhrakel";
        public const string SwordName = "Oathbreaker";

        public static string BoonName(EggBoon b) => b == EggBoon.Godblood ? "Godblood" : b == EggBoon.EmberOath ? "Ember Oath" : null;
        public static string BoonText(EggBoon b) => b == EggBoon.Godblood ? "Double damage, health, reload speed and stamina, and a little faster"
            : b == EggBoon.EmberOath ? "Triple damage and health" : null;
        public static float BoonDamage(EggBoon b) => b == EggBoon.Godblood ? 2f : b == EggBoon.EmberOath ? 3f : 1f;
        public static float BoonHealth(EggBoon b) => b == EggBoon.Godblood ? 2f : b == EggBoon.EmberOath ? 3f : 1f;
        public static float BoonReload(EggBoon b) => b == EggBoon.Godblood ? 2f : 1f;
        public static float BoonStamina(EggBoon b) => b == EggBoon.Godblood ? 2f : 1f;
        /// <summary>Doubling run speed would throw players through walls and past every zombie, so Godblood adds a fifth.</summary>
        public static float BoonSpeed(EggBoon b) => b == EggBoon.Godblood ? 1.2f : 1f;

        /// <summary>Oathbreaker's hits to kill a zombie (bosses aside): one up to round 55, two up to 100, three after.</summary>
        public static int SwordHits(int round) => round <= 55 ? 1 : round <= 100 ? 2 : 3;
        public const float SwordReach = 1.5f;
        /// <summary>Against bosses Oathbreaker is just a very good blade: this many times the knife.</summary>
        public const float SwordBossDamage = 6f;

        /// <summary>Azhrakel's brand: an ember rifle, the one gun nothing else on the map drops.</summary>
        public static GunData MakeGodGun()
        {
            var gun = ScriptableObject.CreateInstance<GunData>();
            gun.name = gun.displayName = GodGunName;
            gun.damage = 480;
            gun.headshotMultiplier = 2.5f;
            gun.range = 160f;
            gun.automatic = true;
            gun.roundsPerMinute = 620f;
            gun.pierce = 4;
            gun.pierceDamageKept = 0.9f;
            gun.explosiveRounds = true;
            gun.magazineSize = 50;
            gun.startingReserve = 400;
            gun.maxReserve = 600;
            gun.reloadTime = 2.0f;
            gun.emptyReloadTime = 2.4f;
            gun.hipSpread = 1.1f;
            gun.aimSpread = 0.08f;
            gun.movingSpread = 0.9f;
            gun.bloomPerShot = 0.12f;
            gun.maxBloom = 0.9f;
            gun.recoilPitch = 0.45f;
            gun.recoilYaw = 0.18f;
            gun.aimFov = 48f;
            gun.raiseTime = 0.5f;
            gun.modelLength = 1.2f;
            gun.accent = new Color(1f, 0.32f, 0.12f);
            return gun;
        }
    }
}
