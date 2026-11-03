using System;
using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>Things a player character says something about. Sent over the network as a byte.</summary>
    public enum VoiceEvent : byte
    {
        RoundStart,
        RoundEnd,
        Downed,
        Revived,
        TeammateDown,
        BuyDoor,
        BuyGun,
        BuyPerk,
        Upgrade,
        LowAmmo,
        PowerUp,
        Boss,
        BossDown,
        Milestone,
        Blackout,
        MissionDone,
    }

    /// <summary>What a step of the main story asks of the squad. Each kind is played out by StoryProgress.</summary>
    public enum StoryGoal : byte
    {
        /// <summary>Read the notes and enter the start-up code (MapState's power).</summary>
        Power,
        /// <summary>Have a number of doors open.</summary>
        OpenDoors,
        /// <summary>Anyone gets into a zone.</summary>
        Reach,
        /// <summary>Someone stands at a zone's generator for a number of seconds while a round is on.</summary>
        Hold,
        /// <summary>Someone holds a high spot's beacon for a number of seconds while a round is on.</summary>
        Beacon,
        /// <summary>Keep a survivor alive for a number of seconds (the survivor of the mission board).</summary>
        Protect,
        /// <summary>The squad kills a number of zombies.</summary>
        Kills,
        /// <summary>The squad clears a number of rounds.</summary>
        ClearRounds,
    }

    /// <summary>One step of the main story: what it asks, where, and the radio calls around it.</summary>
    public sealed class StoryStep
    {
        public readonly string title;
        /// <summary>What to do, for the HUD and the story screen. {0} is the target.</summary>
        public readonly string objective;
        public readonly int act;
        public readonly StoryGoal goal;
        /// <summary>Reach and Hold: the zone. -1 when the place is picked when the step starts (or there is none).</summary>
        public readonly int zone;
        public readonly int target;
        public string[] onStart;
        /// <summary>Calls along the way, when progress reaches a count.</summary>
        public (int at, string line)[] along;
        public string[] onDone;

        public StoryStep(string title, int act, StoryGoal goal, int zone, int target, string objective)
        {
            this.title = title;
            this.act = act;
            this.goal = goal;
            this.zone = zone;
            this.target = Mathf.Max(1, target);
            this.objective = objective;
        }

        public string Describe() => string.Format(objective, target);
        /// <summary>Hold, Beacon and Protect count seconds.</summary>
        public bool Timed => goal == StoryGoal.Hold || goal == StoryGoal.Beacon || goal == StoryGoal.Protect;
    }

    /// <summary>One radio call from the Dispatcher: a round and the lines said, in order.</summary>
    public readonly struct StoryBeat
    {
        public readonly int round;
        public readonly string[] lines;

        public StoryBeat(int round, params string[] lines)
        {
            this.round = round;
            this.lines = lines;
        }
    }

    /// <summary>A two-line exchange between two characters standing near each other.</summary>
    public readonly struct Banter
    {
        public readonly int a, b;
        public readonly string lineA, lineB;

        public Banter(int a, int b, string lineA, string lineB)
        {
            this.a = a;
            this.b = b;
            this.lineA = lineA;
            this.lineB = lineB;
        }
    }

    /// <summary>
    /// The dialogue script: the Dispatcher's radio calls, the Trader's lore,
    /// each character's voice lines and the banter pairs. Text supplies subtitles and stable recording IDs.
    ///
    /// The story is a chain of steps (Steps) in four acts: Setup, Rising stakes, Twist and Endgame. Each step
    /// opens as soon as the one before it is done, at any round. After the last one the game carries on as long
    /// as the squad lasts (a win at round 500), with the Trader on the radio and his bounties on the board.
    /// </summary>
    public static class StoryData
    {
        public const string DispatcherName = "Dispatcher";
        public const string DirectorName = "Director Vale";
        public const string TraderName = "The Trader";
        public static readonly Color RadioColor = new Color(0.55f, 0.85f, 1f);
        public static readonly Color DirectorColor = new Color(1f, 0.45f, 0.4f);
        public static readonly Color TraderColor = new Color(1f, 0.8f, 0.45f);

        /// <summary>Acts in the story itself. One more "act" follows it: the aftermath, for as long as the squad survives.</summary>
        public const int StoryActs = 4;

        public static readonly string[] ActNames = { "Setup", "Rising Stakes", "The Twist", "Endgame", "Aftermath" };
        public static readonly string[] ActSummaries =
        {
            "You woke up in a dead research facility. A voice on the radio, the Dispatcher, says the exits open once the power is back.",
            "The power is coming back, and so is something on Sublevel 9. The Dispatcher won't say what the Lantern is.",
            "The Dispatcher is Director Vale, who ran this place. Every generator you started fed the Lantern, the thing making the dead walk. Time to break it.",
            "The Lantern is cracking and Vale has sealed the facility. The only way left is the old lift, and it needs your power to reach the surface.",
            "The Lantern is dead and Vale has gone quiet, but the dead didn't get the message. The Trader runs the radio now and pays bounties for every job. How long can you hold out?",
        };

        /// <summary>The Dispatcher, once the generator is running.</summary>
        public static readonly string[] PowerOn =
        {
            "That's the generator! I've got lights on the board. Every door in the facility is live now.",
            "I'm putting your orders on your suit display. Check it whenever you're lost.",
        };

        // ---------------------------------------------------------------- the story's steps

        /// <summary>
        /// The main story is a chain of steps: each one opens as soon as the one before it is done, whatever the
        /// round. The radio's story calls play as steps start, along the way and as they finish.
        /// </summary>
        public static readonly StoryStep[] Steps =
        {
            // Act 1: Setup.
            new StoryStep("Restore the Power", 0, StoryGoal.Power, -1, ZoneMapCodeDigits,
                "Find the three notes with the start-up code in the Gate Yard, then enter the code at the generator panel by the south wall.")
            {
                onStart = new[]
                {
                    "Can anyone hear me? This is Dispatch, facility security. Good, you're awake.",
                    "The power's out on every level and the containment doors failed. Those things out there used to be staff.",
                    "Every door down there runs on the gate generator, and it's dead. The night shift left its start-up code on notes somewhere in your yard.",
                    "Find all three numbers and punch them into the panel by the generator. Then I can open the exits.",
                },
                // The Dispatcher's PowerOn call plays when the generator starts.
            },
            new StoryStep("Push In", 0, StoryGoal.OpenDoors, -1, 2, "Open {0} doors. Kills pay for them.")
            {
                onStart = new[]
                {
                    "Kills buy you everything down there. The doors, the wall guns, all of it still runs on facility credit.",
                    "The yards and the depot should still have weapons lockers. Open the doors if you can afford it.",
                },
            },
            new StoryStep("Find the Lantern Core", 0, StoryGoal.Reach, 9, 1,
                "Get into the Lantern Core. The reconditioning unit is there, and the reactor hums like something in it is listening.")
            {
                onDone = new[]
                {
                    "There's a reconditioning unit by the Lantern Core, the one that hums. Feed a gun into it and it comes out meaner.",
                    "Ignore the warning labels. Nobody who wrote them is coming back.",
                },
            },

            // Act 2: Rising stakes.
            new StoryStep("Generator Two", 1, StoryGoal.Hold, 2, 40, "Stand by the generator in the Garage for {0} seconds while the horde comes.")
            {
                onDone = new[] { "Generator two is on. I'm seeing lights on Sublevel 4. Keep going." },
            },
            new StoryStep("Signal Boost", 1, StoryGoal.Beacon, -1, 45, "Climb to the marked high spot and hold its beacon for {0} seconds so Dispatch can read Sublevel 9.")
            {
                along = new[] { (25, "You're doing better than the last team. Much better.") },
                onDone = new[]
                {
                    "Readings from Sublevel 9 are going up every time you clear a wave. It's probably interference.",
                    "There's a containment core down there. The staff called it the Lantern. It's nothing you need to worry about.",
                },
            },
            new StoryStep("Restore the Grid", 1, StoryGoal.Hold, 7, 50, "Stand by the relay in the Pump Station for {0} seconds while the horde comes.")
            {
                along = new[] { (25, "The Lantern is drawing almost everything you restore. That's expected. Keep the power coming.") },
                onDone = new[]
                {
                    "The grid is fully restored.",
                    "Lantern at full charge. Thank you. Truly. You have no idea how long I've waited for a team like you.",
                },
            },

            // Act 3: The twist.
            new StoryStep("The Defector", 2, StoryGoal.Protect, -1, 60, "A scientist from the lower labs has the facility logs. Keep her alive for {0} seconds.")
            {
                onStart = new[]
                {
                    "Why haven't the exits opened? Patience. There's one more thing I need from you.",
                    "Someone's been sending signals from the lower labs. Ignore them. The system is old and full of echoes.",
                },
                onDone = new[]
                {
                    "...Fine. You found the logs. Yes, my name is Vale. I ran this facility. I still do.",
                    "The Lantern doesn't stop the dead. It makes them. And every round you survive feeds it.",
                    "You were never meant to leave. You were meant to keep it charged.",
                },
            },
            new StoryStep("Cut the Feed", 2, StoryGoal.Hold, 9, 60, "Overload the Lantern's feed: stand by the relay in the Lantern Core for {0} seconds.")
            {
                onStart = new[] { "Sabotaging the relays won't save you. It will just make the next wave angrier." },
                along = new[] { (30, "You're making this harder than it needs to be. I could open a door for you. Just one. Stop pulling cables and it's yours.") },
            },
            new StoryStep("Survive the Purge", 2, StoryGoal.Kills, -1, 75, "Vale is throwing everything at you. Kill {0} zombies.")
            {
                along = new[] { (40, "The Lantern is unstable. Do you understand what happens to the people standing next to it when it breaks?") },
            },
            new StoryStep("Crack the Core", 2, StoryGoal.Hold, 10, 60, "Stand by the Archives relay for {0} seconds and the Lantern's core cracks.")
            {
                along = new[] { (30, "I can hear the cracks in the core from here. Please stop.") },
                onDone = new[]
                {
                    "...You've cracked the core. It's leaking everywhere now.",
                    "Fine. The surface lift is real, and it's at the bottom of the old shaft. I'm sealing every other door. Let's see how far you get.",
                },
            },

            // Act 4: Endgame.
            new StoryStep("Power the Lift", 3, StoryGoal.Hold, 6, 75, "Stand by the lift generator in the Loading Dock for {0} seconds.")
            {
                onStart = new[] { "This is Vale. Facility-wide lockdown is in effect. Nobody leaves. Including the dead." },
                along = new[] { (40, "I built the Lantern to end death. Do you know what it did instead? It ended everything else.") },
                onDone = new[] { "The lift needs power to climb, and the only power left is what you make down there. Every round you clear lifts it a floor." },
            },
            new StoryStep("Ride It Out", 3, StoryGoal.ClearRounds, -1, 3, "The lift is climbing. Clear {0} rounds to reach the surface.")
            {
                along = new[]
                {
                    (1, "I'm making you an offer, one last time. Stay. Keep the Lantern burning and I'll keep you alive forever."),
                    (2, "The core is screaming. Even I can hear it now. Whatever's left of it is coming for you."),
                    (2, "This is the last of it. Hold this round and the lift reaches the surface. Hold it."),
                },
            },
        };

        /// <summary>The code has three digits (ZoneMap.CodeDigits; Data can't see World).</summary>
        const int ZoneMapCodeDigits = 3;

        /// <summary>The step whose end unmasks the radio voice as Director Vale.</summary>
        public static int UnmaskStep => IndexOf(StoryGoal.Protect);
        public static int StepCount => Steps.Length;
        /// <summary>Once the squad has found the Lantern Core, Doctor Morrow's side story shows on the HUD and the story tab.</summary>
        public static int MorrowRevealStep => IndexOf(StoryGoal.Reach) + 1;
        public static StoryStep StepAt(int step) => step >= 0 && step < Steps.Length ? Steps[step] : null;

        static int IndexOf(StoryGoal goal)
        {
            for (int i = 0; i < Steps.Length; i++)
                if (Steps[i].goal == goal) return i;
            return -1;
        }

        /// <summary>Which act a step belongs to: 0-3 for the story, 4 for the aftermath once every step is done.</summary>
        public static int ActOf(int step) => step >= Steps.Length ? StoryActs : Steps[Mathf.Max(0, step)].act;
        public static bool StoryOver(int step) => step >= Steps.Length;
        /// <summary>Whether the radio voice has been unmasked once this step is reached (the speaker name changes).</summary>
        public static bool Unmasked(int step) => step > UnmaskStep;
        /// <summary>Who's on the radio: the Dispatcher, then Vale once unmasked, then the Trader once the story is over.</summary>
        public static string RadioSpeaker(int step) => StoryOver(step) ? TraderName : Unmasked(step) ? DirectorName : DispatcherName;
        public static Color RadioSpeakerColor(int step) => StoryOver(step) ? TraderColor : Unmasked(step) ? DirectorColor : RadioColor;
        /// <summary>The first step of an act (StepCount if none), for the story screen.</summary>
        public static int FirstStepOf(int act)
        {
            for (int i = 0; i < Steps.Length; i++)
                if (Steps[i].act == act) return i;
            return Steps.Length;
        }

        /// <summary>What's left to do once every step is done.</summary>
        public const string AftermathObjective = "The story is over. Take the Trader's bounties and see how long you last. Round 500 is the record.";

        /// <summary>Said by the Trader as the story ends, straight after the finale.</summary>
        public static readonly string[] TraderTakesOver =
        {
            "Trader here. Found Vale's headset on the floor. Radio's mine now.",
            "Lantern's dead but the dead didn't notice. I'm putting bounties on the board. Work them and I'll make it worth your while.",
        };

        /// <summary>
        /// Calls that still come at a round, whatever the story step: the facility's tips early on, then the
        /// Trader's milestones once the story is over (he only says those when he has the radio).
        /// </summary>
        public static readonly StoryBeat[] Beats =
        {
            new StoryBeat(5, "Your vitals just spiked. The facility's field is reacting to you. Lean into it. It might keep you alive."),
            new StoryBeat(7, "Somebody left parts for the old field kits lying around. Get them to a workbench and you can build something useful."),
            new StoryBeat(60, "Sixty. You're the longest-lived customers I've ever had."),
            new StoryBeat(75, "Seventy-five. The lift's still up top. The rest of the world can wait a little longer."),
            new StoryBeat(100, "A hundred rounds. I'm naming a sandwich after you."),
            new StoryBeat(150, "One-fifty. They're coming up from floors I didn't know we had."),
            new StoryBeat(200, "Two hundred. Whatever's down there is learning. Shoot faster."),
            new StoryBeat(300, "Three hundred. I've stopped writing your names in the ledger. I need the ink."),
            new StoryBeat(400, "Four hundred. If anyone ever asks what happened down here, I'm telling them about you."),
            new StoryBeat(499, "One more. Hold this and nobody will ever top it."),
        };

        /// <summary>Round calls from this round on belong to the Trader, so they wait for the story to be over.</summary>
        public const int TraderBeatsFrom = 50;

        public static StoryBeat? BeatFor(int round)
        {
            foreach (var b in Beats)
                if (b.round == round) return b;
            return null;
        }

        /// <summary>Said by the radio when the story's last step is done.</summary>
        public static readonly string[] Finale =
        {
            "...The lift is through. Daylight. You actually did it. The Lantern's gone dark.",
            "I won't stop you. I don't think I can. Go, before I change my mind about the door.",
        };

        /// <summary>Said by the radio when you win at round 500.</summary>
        public static readonly string[] Victory =
        {
            "Five hundred. That's it. There's nothing left down here to come up those stairs.",
            "Go home. Drinks are on the house. Well, on the shop.",
        };

        /// <summary>Said by the radio on a game over, by act (the aftermath last).</summary>
        public static readonly string[] GameOver =
        {
            "Signal lost. I'll wake the next team.",
            "Another team gone. The Lantern thanks you for your contribution.",
            "And that's the end of the sabotage. I'll have the cables reconnected by morning.",
            "So close. The shaft keeps whoever falls in it.",
            "And that's closing time. Best customers I ever had.",
        };

        /// <summary>When a new set of missions arrives, by act.</summary>
        public static readonly string[][] MissionSet =
        {
            new[] { "I've marked three jobs for you. Do them and I'll make it worth your while.", "New jobs on your board. Credits for anyone who finishes them." },
            new[] { "More jobs. The facility pays well when you do as you're told.", "Three new jobs. Don't wander off the list." },
            new[] { "Someone's put sabotage jobs on your board. They didn't come from me.", "I see what's on your board now. Stop it." },
            new[] { "The lift is listing jobs automatically now. I can't stop that either.", "More jobs on the board. The way out pays its way." },
            new[] { "Fresh bounties on the board. Cash on completion.", "New bounties. The big ones pay big." },
        };

        public static readonly string[][] MissionComplete =
        {
            new[] { "Good work. Credits are on their way.", "Job done. That's the kind of team I need." },
            new[] { "Nicely done. The Lantern... the grid appreciates it.", "Job complete. Keep it up." },
            new[] { "That relay was expensive.", "Every job you finish costs me something. You know that?" },
            new[] { "Fine. Take your reward.", "Another job done. You really want to see the sky." },
            new[] { "Bounty paid. Pleasure doing business.", "That's a bounty. Don't spend it all at once. Actually, do. At my shop." },
        };

        public const string SurvivorLost = "The survivor's gone. Add them to the list.";
        public static readonly string[] BossRound = { "Something big is on the move toward you. Aim for the glowing core on its back.", "Heavy signature incoming. Don't let it corner you." };
        public static readonly string[] BlackoutRound = { "I'm losing the grid! Emergency lights only. They hunt better in the dark.", "Blackout. Every kill is worth double while it lasts. Survive it." };

        /// <summary>Lore from the Trader at the spawn shop, by act. One line each time the shop opens.</summary>
        public static readonly string[][] TraderLore =
        {
            new[]
            {
                "Fresh faces. Lovely. Spend your credits here, they're no good to you dead.",
                "I've been down here since before the lights went out. The dead don't bother me. Bad for business.",
                "That voice on the radio? Never trusted a voice without a face.",
                "Upgrades unlock as you learn your trade. Your level, your rules. Your points, my profit.",
            },
            new[]
            {
                "The Lantern. Ha. They named a bomb after a night-light.",
                "Sublevel 9 hums at night. Always has. Louder now, though.",
                "Doctor Hale? I sold him coffee for six years. Never once paid on time.",
                "Every time the grid comes back, I hear more of them below. Funny that.",
            },
            new[]
            {
                "So now you know who's on the radio. I've known for years. Nobody asked.",
                "Pull enough cables and even Vale has to listen.",
                "I left those sabotage jobs on your board. Don't tell anyone.",
                "The Lantern took my brother. Break it and I'll give you a discount. Small one.",
            },
            new[]
            {
                "The lift shaft? I've seen it. Long way up. Bring a better scope.",
                "If you make it out, tell them the Trader stayed. Somebody has to mind the shop.",
                "Nearly there and you're still buying grips. I respect that.",
                "Whatever's coming for you now, it isn't Vale. Not anymore.",
            },
            new[]
            {
                "You could have gone up that lift. You stayed. Good for business.",
                "Vale's headset fits me nicely. Don't tell her.",
                "Bounties are honest work. Well. Work.",
                "The record down here is round five hundred. Nobody's ever set it. No pressure.",
            },
        };

        public static string TraderLine(int act, int index)
        {
            var lines = TraderLore[Mathf.Clamp(act, 0, TraderLore.Length - 1)];
            return lines[((index % lines.Length) + lines.Length) % lines.Length];
        }

        // ---------------------------------------------------------------- player voices

        /// <summary>Lines per character per event: [character][event][variant]. Characters follow Characters.All.</summary>
        public static readonly string[][][] Voice =
        {
            // Marlow, ex-cop: gruff, dry, tired.
            new[]
            {
                new[] { "Here we go again.", "Eyes up. Here they come.", "Another shift." },
                new[] { "That's the round. Breathe.", "Clear. Reload while you can." },
                new[] { "I'm down! Somebody get over here!", "Officer down. That's me. That's the officer." },
                new[] { "Owe you one.", "Back on my feet. Thanks." },
                new[] { "Man down! Cover them!", "We've got someone down!" },
                new[] { "Door's open. Watch the corners.", "Opening it up. Stay tight." },
                new[] { "That'll do.", "Now we're armed." },
                new[] { "Tastes like evidence.", "Hope this stuff isn't expired." },
                new[] { "Upgraded. Let's go to work.", "Now that's a service weapon." },
                new[] { "I'm running dry.", "Low on ammo, cover me." },
                new[] { "I feel ten years younger.", "Something in my blood just changed. I'll take it." },
                new[] { "Big one. Hit the glowing part.", "That's a lot of suspect." },
                new[] { "Big one's down.", "Case closed." },
                new[] { "Fifty rounds. I've had shorter stakeouts.", "One hundred. My back hurts.", "Two-fifty. Nobody's believing this report.", "Five hundred. Put me on the pension." },
                new[] { "Lights out. Stay close.", "Can't see a thing. Shoot anything that moans." },
                new[] { "Job's done. Get paid.", "That's one off the list." },
            },
            // Dee, mechanic: sarcastic, fearless, loves machines.
            new[]
            {
                new[] { "Round's up. Let's make some noise.", "Ooh, they brought friends.", "Showtime." },
                new[] { "Clear! Somebody give me a wrench.", "That all you got?" },
                new[] { "Ow. Ow! Down, I'm down!", "I need a hand. Literally." },
                new[] { "My hero. Don't tell anyone.", "Thanks. I'll fix your gun later." },
                new[] { "Someone dropped! Move!", "Get them up, I'll cover!" },
                new[] { "Door's open. Hinges could use oil.", "There we go. Breaking and entering." },
                new[] { "Hello, gorgeous.", "Oh, I'm keeping this one." },
                new[] { "Glug glug. Better.", "Tastes like motor oil. Love it." },
                new[] { "Listen to that purr.", "The machine likes me. Everyone does." },
                new[] { "Running low!", "Mag's almost empty!" },
                new[] { "Whoa. I felt that in my teeth.", "Upgrade installed." },
                new[] { "That's a big boy.", "I'm going to need a bigger toolbox." },
                new[] { "Down goes the big one!", "Scrap it." },
                new[] { "Fifty! Somebody owes me a drink.", "A hundred rounds and I still haven't broken a nail.", "Two-fifty. These things are getting creative.", "Five hundred. I'm putting this on my resume." },
                new[] { "Who turned off the lights?", "Dark rounds. Fantastic. My favourite." },
                new[] { "Job done. Pay me.", "Easy money." },
            },
            // Dr. Hale, facility scientist: nervous, clever, guilty.
            new[]
            {
                new[] { "Oh no, oh no. Here they come.", "Statistically, we should be dead already.", "Stay calm. I'm calm. I'm fine." },
                new[] { "We survived. Somehow.", "Fascinating. Terrifying, but fascinating." },
                new[] { "I'm hurt! Somebody, please!", "This is exactly what the trials predicted!" },
                new[] { "Thank you. Truly.", "Back up. Remind me to write that down." },
                new[] { "They've gone down! Help them!", "Vital signs dropping! Over here!" },
                new[] { "I remember this corridor. That's not a good thing.", "Opening it. Please let it be empty." },
                new[] { "I've never fired one of these. How hard can it be?", "Point the dangerous end at them. Right." },
                new[] { "That's not the approved dosage.", "I helped design this. It shouldn't taste this bad." },
                new[] { "That machine is not supposed to work like that.", "We had a lab for this. It's better here." },
                new[] { "I'm nearly out of ammunition!", "Ammunition is a finite resource!" },
                new[] { "The field is rewriting us. That's... helpful, I suppose.", "I feel stronger. Worrying." },
                new[] { "That's a Mark Two subject. They were never supposed to wake.", "Shoot the core! The glowing part!" },
                new[] { "It's down. My notes were right.", "Another specimen down." },
                new[] { "Fifty rounds. The Lantern must be loving this.", "One hundred. I should tell you something. Later.", "Two-fifty. This is my fault, you know.", "Five hundred. I'm so sorry. And thank you." },
                new[] { "The Lantern is pulling power. Stay together.", "The dark makes them faster. I wrote that paper." },
                new[] { "Done. That's one variable controlled.", "Job complete." },
            },
            // Rook, courier: young, cocky, jokes until he can't.
            new[]
            {
                new[] { "Round time! Let's get it!", "Oh they're fast this time.", "Package delivery, coming right up." },
                new[] { "Easy. Too easy.", "Clear! Did you see that?" },
                new[] { "Okay, not funny, not funny, help!", "I'm down! This isn't in the job description!" },
                new[] { "Back in business!", "Knew you couldn't live without me." },
                new[] { "We've got a man down!", "Someone's on the floor! Go!" },
                new[] { "Door's open. Delivery complete.", "Signature not required." },
                new[] { "Oh, this is nice.", "Okay, okay, I'm dangerous now." },
                new[] { "Energy drink of the damned.", "That fizzed. Was it supposed to fizz?" },
                new[] { "Look at this thing! Look at it!", "Upgraded. Watch this." },
                new[] { "I'm almost out!", "Ammo! Ammo! Anyone?" },
                new[] { "Oh, I'm faster. I'm so much faster.", "Power-up! Let's go!" },
                new[] { "Nope. Nope. That's huge.", "I'm not getting paid enough for that." },
                new[] { "Who's the best? Us!", "Big one down! Easy." },
                new[] { "Fifty! Somebody film this!", "One hundred! A hundred! I'm a legend!", "Two-fifty. Okay, I'm actually tired now.", "Five hundred. I'm never delivering a package again." },
                new[] { "It's so dark. Why is it dark?", "Double points in the dark? Deal." },
                new[] { "Job done! Tip, please.", "Sorted." },
            },
        };

        public static string[] VoiceLines(int character, VoiceEvent e)
        {
            var set = Voice[Mathf.Clamp(character, 0, Voice.Length - 1)];
            int i = (int)e;
            return i >= 0 && i < set.Length ? set[i] : Array.Empty<string>();
        }

        /// <summary>Milestone lines are picked by the round, not at random.</summary>
        public static int MilestoneIndex(int round) => round switch { 50 => 0, 100 => 1, 250 => 2, 500 => 3, _ => -1 };

        public static readonly Banter[] BanterPairs =
        {
            new Banter(0, 1, "Dee, you're humming again.", "It helps me aim. Also it annoys you."),
            new Banter(1, 0, "Marlow, ever smile?", "Once. Didn't care for it."),
            new Banter(0, 2, "Doc, you knew about this place.", "I knew about the budget. That's different."),
            new Banter(2, 0, "Detective, you're very calm about all this.", "Ex-detective. And I've seen worse paperwork."),
            new Banter(0, 3, "Kid, stop showing off.", "Showing off is how I stay alive, old man."),
            new Banter(3, 0, "Marlow, were you a good cop?", "I was a tired cop. Same thing, on a good day."),
            new Banter(1, 2, "Doc, who builds a zombie machine?", "It was a containment array! The zombies were a side effect!"),
            new Banter(2, 1, "Your gun's making a noise.", "That's not the gun. That's me, judging your aim."),
            new Banter(1, 3, "Rook, what were you even delivering down here?", "A box marked Do Not Open. So, you know, I opened it."),
            new Banter(3, 1, "Dee, can you fix my gun?", "I can fix the gun. I can't fix the guy holding it."),
            new Banter(2, 3, "Rook, the box you opened. What colour was the light inside?", "Blue? Why are you making that face?"),
            new Banter(3, 2, "Doc, how many people worked here?", "About four hundred. Why do you... oh. Oh, right."),
        };

        /// <summary>For two players playing the same character.</summary>
        public static readonly string[] MirrorBanter = { "Why do you look like me?", "I was going to ask you that." };
    }
}
