using System;
using NUnit.Framework;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class StoryDataTests
    {
        [Test]
        public void Story_RunsFromRoundOne_AndFinishesByFifty()
        {
            Assert.AreEqual(50, StoryData.FinalRound);
            Assert.AreEqual(0, StoryData.ActFor(1));
            Assert.AreEqual(0, StoryData.ActFor(10));
            Assert.AreEqual(1, StoryData.ActFor(11));
            Assert.AreEqual(1, StoryData.ActFor(25));
            Assert.AreEqual(2, StoryData.ActFor(26));
            Assert.AreEqual(2, StoryData.ActFor(40));
            Assert.AreEqual(3, StoryData.ActFor(41));
            Assert.AreEqual(3, StoryData.ActFor(50));
            // After the story: the aftermath, for as long as the squad lasts.
            Assert.AreEqual(StoryData.StoryActs, StoryData.ActFor(51));
            Assert.AreEqual(StoryData.StoryActs, StoryData.ActFor(500));
            Assert.IsFalse(StoryData.StoryOver(50));
            Assert.IsTrue(StoryData.StoryOver(51));
            // One entry per act, plus the aftermath, everywhere lines are picked by act.
            int acts = StoryData.StoryActs + 1;
            Assert.AreEqual(acts, StoryData.ActNames.Length);
            Assert.AreEqual(acts, StoryData.ActSummaries.Length);
            Assert.AreEqual(acts, StoryData.GameOver.Length);
            Assert.AreEqual(acts, StoryData.MissionSet.Length);
            Assert.AreEqual(acts, StoryData.MissionComplete.Length);
            Assert.AreEqual(acts, StoryData.TraderLore.Length);
            Assert.AreEqual(acts, StoryData.ActObjectives.Length);
            // The story screen's act ranges agree with ActFor.
            for (int round = 1; round <= 60; round++)
            {
                int act = StoryData.ActFor(round);
                Assert.That(round >= StoryData.ActStartRound(act) && round <= StoryData.ActEndRound(act), $"round {round}");
            }
            Assert.Greater(StoryData.Finale.Length, 0);
            Assert.AreEqual(StoryData.TraderName, StoryData.RadioSpeaker(60), "the Trader has the radio after the story");
        }

        [Test]
        public void Beats_LandOnTheRequiredRounds_InOrder()
        {
            // Every act has calls, the twist lands, and the final round has one.
            foreach (int round in new[] { 1, 10, 25, StoryData.UnmaskRound, 40, StoryData.FinalRound, StoryData.FinalRound + 1 })
                Assert.IsTrue(StoryData.BeatFor(round).HasValue, $"round {round}");
            int storyBeats = 0;
            foreach (var b in StoryData.Beats)
                if (b.round <= StoryData.FinalRound) storyBeats++;
            Assert.GreaterOrEqual(storyBeats, 20, "the story is told over the first 50 rounds");
            for (int i = 1; i < StoryData.Beats.Length; i++)
                Assert.Greater(StoryData.Beats[i].round, StoryData.Beats[i - 1].round);
            foreach (var b in StoryData.Beats)
            {
                Assert.Greater(b.lines.Length, 0);
                Assert.LessOrEqual(b.round, RoundData.MaxRound);
            }
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
            Assert.IsFalse(StoryData.Unmasked(StoryData.UnmaskRound - 1));
            Assert.IsTrue(StoryData.Unmasked(StoryData.UnmaskRound));
        }
    }
}
