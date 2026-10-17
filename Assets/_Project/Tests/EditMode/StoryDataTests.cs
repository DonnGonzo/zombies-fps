using System;
using NUnit.Framework;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class StoryDataTests
    {
        [Test]
        public void Acts_FollowTheDesignDoc()
        {
            Assert.AreEqual(0, StoryData.ActFor(1));
            Assert.AreEqual(0, StoryData.ActFor(25));
            Assert.AreEqual(1, StoryData.ActFor(26));
            Assert.AreEqual(1, StoryData.ActFor(100));
            Assert.AreEqual(2, StoryData.ActFor(101));
            Assert.AreEqual(2, StoryData.ActFor(250));
            Assert.AreEqual(3, StoryData.ActFor(251));
            Assert.AreEqual(3, StoryData.ActFor(500));
            Assert.AreEqual(4, StoryData.ActNames.Length);
            Assert.AreEqual(4, StoryData.ActSummaries.Length);
        }

        [Test]
        public void Beats_LandOnTheRequiredRounds_InOrder()
        {
            foreach (int round in new[] { 25, 100, 250, 300, 400 })
                Assert.IsTrue(StoryData.BeatFor(round).HasValue, $"round {round}");
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
            Assert.IsFalse(StoryData.Unmasked(100));
            Assert.IsTrue(StoryData.Unmasked(110));
        }
    }
}
