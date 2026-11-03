using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.World;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The current story objective in words, shared by the HUD's objective box, the pause menu's story card and
    /// the map's story tab, so they always agree. Reads only replicated state.
    /// </summary>
    public static class StoryObjective
    {
        public static StoryProgress Story => StoryProgress.Instance;
        public static int Step => Story != null ? Story.Step.Value : 0;
        public static bool Complete => StoryData.StoryOver(Step);
        public static StoryStep Current => StoryData.StepAt(Step);

        public static string Title => Current != null ? Current.title : "Survive";

        /// <summary>What to do, with where if the step picked a place when it started.</summary>
        public static string Text
        {
            get
            {
                var step = Current;
                if (step == null) return StoryData.AftermathObjective;
                string text = step.Describe();
                if ((step.goal == StoryGoal.Beacon || step.goal == StoryGoal.Protect) && Story != null && Story.TryGetWhere(out _, out string where))
                    text += $" ({where})";
                return text;
            }
        }

        /// <summary>"ACT 2 OF 4  ·  STEP 5 OF 12", or the aftermath.</summary>
        public static string Header
        {
            get
            {
                int step = Step;
                if (StoryData.StoryOver(step)) return "STORY COMPLETE  ·  AFTERMATH";
                return $"ACT {StoryData.ActOf(step) + 1} OF {StoryData.StoryActs}  ·  STEP {step + 1} OF {StoryData.StepCount}";
            }
        }

        /// <summary>How far through the step, in words: "23/45 s", "1/2 doors", "CODE 4 _ 7".</summary>
        public static string ProgressText
        {
            get
            {
                var step = Current;
                var story = Story;
                if (step == null || story == null) return "";
                int p = story.Progress.Value, t = step.target;
                var rounds = RoundManager.Instance;
                bool roundOn = rounds != null && rounds.Phase.Value == GamePhase.RoundActive;
                switch (step.goal)
                {
                    case StoryGoal.Power:
                        var power = MapState.Instance;
                        return power == null ? "" : power.NotesFound < ZoneMap.CodeDigits ? $"CODE  {power.KnownCode}" : $"ENTER  {power.KnownCode}";
                    case StoryGoal.OpenDoors: return $"{p}/{t} doors";
                    case StoryGoal.Reach: return ZoneMap.ZoneNames[Mathf.Clamp(step.zone, 0, ZoneMap.ZoneCount - 1)];
                    case StoryGoal.Kills: return $"{p}/{t} kills";
                    case StoryGoal.ClearRounds: return $"{p}/{t} rounds";
                    case StoryGoal.Protect:
                        if (story.SurvivorZone < 0) return "she comes out next round";
                        return roundOn ? $"{p}/{t} s" : $"{p}/{t} s  ·  clock runs in rounds";
                    default:
                        return roundOn ? $"{p}/{t} s" : $"{p}/{t} s  ·  counts during rounds";
                }
            }
        }

        /// <summary>0-1 for the bar (the power step counts notes read).</summary>
        public static float Fraction => Story != null ? Story.Fraction : Complete ? 1f : 0f;

        /// <summary>Doctor Morrow's side story shows once the squad has found the Lantern Core or touched the reactor.</summary>
        public static bool EggDiscovered
        {
            get
            {
                var egg = EasterEgg.Instance;
                return egg != null && (egg.Active.Value || egg.StepsDone.Value > 0 || Step >= StoryData.MorrowRevealStep);
            }
        }

        /// <summary>The side objective line, or null: the next step of Morrow's work while none is being played.</summary>
        public static string SideText
        {
            get
            {
                var egg = EasterEgg.Instance;
                if (!EggDiscovered || egg.Finished || egg.Active.Value) return null;
                var next = EasterEggData.At(egg.StepsDone.Value);
                return next != null ? $"SIDE  ·  Dr. Morrow: {next.title}" : null;
            }
        }
    }
}
