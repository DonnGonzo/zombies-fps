using System;
using NUnit.Framework;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class StoryDataTests
    {
        [Test]
        public void Story_IsAChainOfSteps_InActOrder()
        {
            Assert.GreaterOrEqual(StoryData.StepCount, 8);
            Assert.AreEqual(StoryGoal.Power, StoryData.StepAt(0).goal, "the story opens with the generator");
            Assert.AreEqual(0, StoryData.ActOf(0));
            Assert.AreEqual(StoryData.StoryActs - 1, StoryData.ActOf(StoryData.StepCount - 1));
            for (int s = 1; s < StoryData.StepCount; s++)
            {
                int gap = StoryData.ActOf(s) - StoryData.ActOf(s - 1);
                Assert.That(gap == 0 || gap == 1, $"step {s} skips or goes back an act");
            }
            for (int act = 0; act < StoryData.StoryActs; act++)
                Assert.Less(StoryData.FirstStepOf(act), StoryData.StepCount, $"act {act} has steps");
            // After the story: the aftermath, for as long as the squad lasts.
            Assert.AreEqual(StoryData.StoryActs, StoryData.ActOf(StoryData.StepCount));
            Assert.IsFalse(StoryData.StoryOver(StoryData.StepCount - 1));
            Assert.IsTrue(StoryData.StoryOver(StoryData.StepCount));
            Assert.IsNull(StoryData.StepAt(StoryData.StepCount));
            // One entry per act, plus the aftermath, everywhere lines are picked by act.
            int acts = StoryData.StoryActs + 1;
            Assert.AreEqual(acts, StoryData.ActNames.Length);
            Assert.AreEqual(acts, StoryData.ActSummaries.Length);
            Assert.AreEqual(acts, StoryData.GameOver.Length);
            Assert.AreEqual(acts, StoryData.MissionSet.Length);
            Assert.AreEqual(acts, StoryData.MissionComplete.Length);
            Assert.AreEqual(acts, StoryData.TraderLore.Length);
            Assert.Greater(StoryData.Finale.Length, 0);
            Assert.Greater(StoryData.TraderTakesOver.Length, 0);
        }

        [Test]
        public void Steps_HaveATarget_APlace_AndAnObjective()
        {
            foreach (var step in StoryData.Steps)
            {
                Assert.GreaterOrEqual(step.target, 1, step.title);
                Assert.IsFalse(string.IsNullOrEmpty(step.title));
                StringAssert.DoesNotContain("{0}", step.Describe(), step.title);
                if (step.goal == StoryGoal.Hold || step.goal == StoryGoal.Reach)
                    Assert.That(step.zone >= 0 && step.zone < ZombiesFPS.World.ZoneMap.ZoneCount, step.title);
                if (step.along != null)
                    foreach (var (at, line) in step.along)
                    {
                        Assert.That(at > 0 && at < step.target, $"{step.title}: a call along the way at {at}");
                        Assert.IsFalse(string.IsNullOrEmpty(line));
                    }
            }
            // The twist: the radio voice is the Dispatcher until the Defector's logs, then Vale, then the Trader.
            int unmask = StoryData.UnmaskStep;
            Assert.That(unmask > 0 && unmask < StoryData.StepCount);
            Assert.AreEqual(StoryData.DispatcherName, StoryData.RadioSpeaker(unmask));
            Assert.AreEqual(StoryData.DirectorName, StoryData.RadioSpeaker(unmask + 1));
            Assert.AreEqual(StoryData.TraderName, StoryData.RadioSpeaker(StoryData.StepCount), "the Trader has the radio after the story");
            Assert.That(StoryData.MorrowRevealStep > 0 && StoryData.MorrowRevealStep < StoryData.StepCount);
        }

        [Test]
        public void RoundBeats_AreInOrder_AndTheTradersWaitForTheStory()
        {
            for (int i = 1; i < StoryData.Beats.Length; i++)
                Assert.Greater(StoryData.Beats[i].round, StoryData.Beats[i - 1].round);
            foreach (var b in StoryData.Beats)
            {
                Assert.Greater(b.lines.Length, 0);
                Assert.LessOrEqual(b.round, RoundData.MaxRound);
            }
            Assert.IsTrue(StoryData.BeatFor(StoryData.TraderBeatsFrom + 10).HasValue);
            Assert.IsFalse(StoryData.BeatFor(1).HasValue, "round 1 opens the first story step instead");
        }

        [Test]
        public void EveryCharacter_HasLinesForEveryEvent()
        {
            Assert.AreEqual(Characters.Count, StoryData.Voice.Length);
            int events = Enum.GetValues(typeof(VoiceEvent)).Length;
            for (int c = 0; c < Characters.Count; c++)
            {
                int total = 0;
                foreach (VoiceEvent e in Enum.GetValues(typeof(VoiceEvent)))
                {
                    var lines = StoryData.VoiceLines(c, e);
                    Assert.Greater(lines.Length, 0, $"{Characters.At(c).name} {e}");
                    Assert.Less(lines.Length, 256);
                    total += lines.Length;
                }
                Assert.AreEqual(4, StoryData.VoiceLines(c, VoiceEvent.Milestone).Length);
                Assert.GreaterOrEqual(total, 30, Characters.At(c).name);
                Assert.AreEqual(events, StoryData.Voice[c].Length);
            }
        }

        [Test]
        public void Banter_UsesRealCharacters_BothWays()
        {
            for (int a = 0; a < Characters.Count; a++)
            for (int b = 0; b < Characters.Count; b++)
            {
                if (a == b) continue;
                bool found = false;
                foreach (var p in StoryData.BanterPairs) found |= p.a == a && p.b == b;
                Assert.IsTrue(found, $"{a} -> {b}");
            }
        }

        [Test]
        public void Milestones_MapToTheirLines()
        {
            Assert.AreEqual(0, StoryData.MilestoneIndex(50));
            Assert.AreEqual(3, StoryData.MilestoneIndex(500));
            Assert.AreEqual(-1, StoryData.MilestoneIndex(51));
            Assert.IsFalse(StoryData.Unmasked(StoryData.UnmaskStep));
            Assert.IsTrue(StoryData.Unmasked(StoryData.UnmaskStep + 1));
        }
    }
}
