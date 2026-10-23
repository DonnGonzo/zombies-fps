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
    /// The story runs from round 1 and finishes at round 50: Setup (1-10), Rising stakes (11-25), Twist
    /// (26-40) and Endgame (41-50). After that the game carries on as long as the squad lasts (a win at
    /// round 500), with the Trader on the radio and his bounty missions on the board.
    /// </summary>
    public static class StoryData
    {
        public const string DispatcherName = "Dispatcher";
        public const string DirectorName = "Director Vale";
        public const string TraderName = "The Trader";
        public static readonly Color RadioColor = new Color(0.55f, 0.85f, 1f);
        public static readonly Color DirectorColor = new Color(1f, 0.45f, 0.4f);
        public static readonly Color TraderColor = new Color(1f, 0.8f, 0.45f);

        /// <summary>The story is told by round 50; the finale plays when that round is cleared.</summary>
        public const int FinalRound = 50;
        /// <summary>Acts in the story itself. One more "act" follows it: the aftermath, for as long as the squad survives.</summary>
        public const int StoryActs = 4;
        /// <summary>The round the radio voice turns out to be Director Vale.</summary>
        public const int UnmaskRound = 28;

        public static readonly string[] ActNames = { "Setup", "Rising Stakes", "The Twist", "Endgame", "Aftermath" };
        public static readonly string[] ActSummaries =
        {
            "You woke up in a dead research facility. A voice on the radio, the Dispatcher, says the exits open once the power is back.",
            "The power is coming back, and so is something on Sublevel 9. The Dispatcher won't say what the Lantern is.",
            "The Dispatcher is Director Vale, who ran this place. Every generator you started fed the Lantern, the thing making the dead walk. Time to break it.",
            "The Lantern is cracking and Vale has sealed the facility. The only way left is the old lift, and it needs your power to reach the surface.",
            "The Lantern is dead and Vale has gone quiet, but the dead didn't get the message. The Trader runs the radio now and pays bounties for every job. How long can you hold out?",
        };

        /// <summary>The opening step before Act 1's jobs: start the gate generator so the doors can be bought.</summary>
        public const string PrologueTitle = "Restore the Power";
        public const string PrologueObjective = "Find the three notes with the start-up code in the Gate Yard, then enter the code at the generator panel by the south wall.";
        /// <summary>What the squad is working towards in each act, for the story screen.</summary>
        public static readonly string[] ActObjectives =
        {
            "Push into the facility. Open the doors, take the Dispatcher's jobs and find the reconditioning unit by the Lantern Core.",
            "Keep the power flowing and find out what the Lantern is. Something on Sublevel 9 is waking up.",
            "Vale lied. Break the Lantern's hold on the facility and survive the purge.",
            "Hold the facility to round 50 so the old lift can carry you to the surface.",
            "The story is over. Take the Trader's bounties and see how long you last. Round 500 is the record.",
        };
        /// <summary>The last round of each act (the aftermath never ends).</summary>
        public static int ActEndRound(int act) => act == 0 ? 10 : act == 1 ? 25 : act == 2 ? 40 : act == 3 ? FinalRound : int.MaxValue;
        public static int ActStartRound(int act) => act == 0 ? 1 : ActEndRound(act - 1) + 1;
        /// <summary>The Dispatcher, once the generator is running.</summary>
        public static readonly string[] PowerOn =
        {
            "That's the generator! I've got lights on the board. Every door in the facility is live now.",
            "I'm putting your orders on your suit display. Check it whenever you're lost.",
        };

        /// <summary>Which act a round belongs to: 0 Setup, 1 Rising stakes, 2 Twist, 3 Endgame, 4 the aftermath once the story is over.</summary>
        public static int ActFor(int round) => round <= 10 ? 0 : round <= 25 ? 1 : round <= 40 ? 2 : round <= FinalRound ? 3 : 4;

        public static bool StoryOver(int round) => round > FinalRound;

        /// <summary>Whether the radio voice has been unmasked by this round (the speaker name changes).</summary>
        public static bool Unmasked(int round) => round >= UnmaskRound;
        /// <summary>Who's on the radio: the Dispatcher, then Vale once unmasked, then the Trader once the story is over.</summary>
        public static string RadioSpeaker(int round) => StoryOver(round) ? TraderName : Unmasked(round) ? DirectorName : DispatcherName;
        public static Color RadioSpeakerColor(int round) => StoryOver(round) ? TraderColor : Unmasked(round) ? DirectorColor : RadioColor;

        public static readonly StoryBeat[] Beats =
        {
            // Act 1: Setup.
            new StoryBeat(1, "Can anyone hear me? This is Dispatch, facility security. Good, you're awake.",
                "The power's out on every level and the containment doors failed. Those things out there used to be staff.",
                "Every door down there runs on the gate generator, and it's dead. The night shift left its start-up code on notes somewhere in your yard.",
                "Find all three numbers and punch them into the panel by the generator. Then I can open the exits."),
            new StoryBeat(2, "Kills buy you everything down there. The doors, the wall guns, all of it still runs on facility credit."),
            new StoryBeat(3, "The yards and the depot should still have weapons lockers. Open the doors if you can afford it."),
            new StoryBeat(5, "Your vitals just spiked. The facility's field is reacting to you. Lean into it. It might keep you alive."),
            new StoryBeat(7, "Somebody left parts for the old field kits lying around. Get them to a workbench and you can build something useful."),
            new StoryBeat(10, "There's a reconditioning unit by the Lantern Core, the one that hums. Feed a gun into it and it comes out meaner.",
                "Ignore the warning labels. Nobody who wrote them is coming back."),

            // Act 2: Rising stakes.
            new StoryBeat(12, "Generator two is on. I'm seeing lights on Sublevel 4. Keep going."),
            new StoryBeat(15, "You're doing better than the last team. Much better."),
            new StoryBeat(18, "Readings from Sublevel 9 are going up every time you clear a wave. It's probably interference.",
                "There's a containment core down there. The staff called it the Lantern. It's nothing you need to worry about."),
            new StoryBeat(20, "Someone's been sending signals from the lower labs. Ignore them. The system is old and full of echoes."),
            new StoryBeat(23, "The Lantern is drawing almost everything you restore. That's expected. Keep the power coming."),
            new StoryBeat(25, "The grid is fully restored.",
                "Lantern at full charge. Thank you. Truly. You have no idea how long I've waited for a team like you."),

            // Act 3: The twist.
            new StoryBeat(26, "Why haven't the exits opened? Patience. There's one more thing I need from you."),
            new StoryBeat(UnmaskRound, "...Fine. You found the logs. Yes, my name is Vale. I ran this facility. I still do.",
                "The Lantern doesn't stop the dead. It makes them. And every round you survive feeds it.",
                "You were never meant to leave. You were meant to keep it charged."),
            new StoryBeat(30, "Sabotaging the relays won't save you. It will just make the next wave angrier."),
            new StoryBeat(33, "You're making this harder than it needs to be. I could open a door for you. Just one. Stop pulling cables and it's yours."),
            new StoryBeat(35, "The Lantern is unstable. Do you understand what happens to the people standing next to it when it breaks?"),
            new StoryBeat(38, "I can hear the cracks in the core from here. Please stop."),
            new StoryBeat(40, "...You've cracked the core. It's leaking everywhere now.",
                "Fine. The surface lift is real, and it's at the bottom of the old shaft. I'm sealing every other door. Let's see how far you get."),

            // Act 4: Endgame.
            new StoryBeat(41, "This is Vale. Facility-wide lockdown is in effect. Nobody leaves. Including the dead."),
            new StoryBeat(43, "The lift needs power to climb, and the only power left is what you make down there. Every round you clear lifts it a floor."),
            new StoryBeat(45, "I built the Lantern to end death. Do you know what it did instead? It ended everything else."),
            new StoryBeat(47, "I'm making you an offer, one last time. Stay. Keep the Lantern burning and I'll keep you alive forever."),
            new StoryBeat(49, "The core is screaming. Even I can hear it now. Whatever's left of it is coming for you."),
            new StoryBeat(FinalRound, "This is the last of it. Hold this round and the lift reaches the surface. Hold it."),

            // After the story: the Trader has the radio.
            new StoryBeat(51, "Trader here. Found Vale's headset on the floor. Radio's mine now.",
                "Lantern's dead but the dead didn't notice. I'm putting bounties on the board. Work them and I'll make it worth your while."),
            new StoryBeat(60, "Sixty. You're the longest-lived customers I've ever had."),
            new StoryBeat(75, "Seventy-five. The lift's still up top. The rest of the world can wait a little longer."),
            new StoryBeat(100, "A hundred rounds. I'm naming a sandwich after you."),
            new StoryBeat(150, "One-fifty. They're coming up from floors I didn't know we had."),
            new StoryBeat(200, "Two hundred. Whatever's down there is learning. Shoot faster."),
            new StoryBeat(300, "Three hundred. I've stopped writing your names in the ledger. I need the ink."),
            new StoryBeat(400, "Four hundred. If anyone ever asks what happened down here, I'm telling them about you."),
            new StoryBeat(499, "One more. Hold this and nobody will ever top it."),
        };

        public static StoryBeat? BeatFor(int round)
        {
            foreach (var b in Beats)
                if (b.round == round) return b;
            return null;
        }

        /// <summary>Said by the radio when round 50 is cleared and the story is over.</summary>
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

        /// <summary>Said by the radio on a game over, by act.</summary>
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

        public static string TraderLine(int round, int index)
        {
            var lines = TraderLore[ActFor(round)];
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
