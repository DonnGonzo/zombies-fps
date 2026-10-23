using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class ZombieTypeTests
    {
        [Test]
        public void Types_ArriveOnTheirRounds()
        {
            var expected = new[] { (ZombieKind.Runner, 8), (ZombieKind.Crawler, 12), (ZombieKind.Brute, 20), (ZombieKind.Spitter, 30), (ZombieKind.Stalker, 15) };
            foreach (var (kind, first) in expected)
            {
                var data = ZombieData.CreateDefault(kind);
                Assert.AreEqual(0f, data.WeightForRound(first - 1), kind.ToString());
                Assert.Greater(data.WeightForRound(first), 0f, kind.ToString());
                Assert.Less(data.WeightForRound(first), data.spawnWeight + 1e-5f, kind.ToString());
                Assert.AreEqual(data.spawnWeight, data.WeightForRound(first + 100), 1e-5f, kind.ToString());
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Boss_NeverComesFromTheMix_AndHasAWeakPoint()
        {
            var boss = ZombieData.CreateDefault(ZombieKind.Boss);
            Assert.IsTrue(boss.IsBoss);
            Assert.AreEqual(0f, boss.WeightForRound(100));
            Assert.Greater(boss.weakPointMultiplier, 1f);
            Assert.Less(boss.bodyMultiplier, 1f);
            Object.DestroyImmediate(boss);
        }

        [Test]
        public void BossRounds_EveryTenth_MoreBossesLater()
        {
            var round = ScriptableObject.CreateInstance<RoundData>();
            Assert.AreEqual(0, round.BossCountForRound(9));
            Assert.AreEqual(1, round.BossCountForRound(10));
            Assert.AreEqual(1, round.BossCountForRound(40));
            Assert.AreEqual(2, round.BossCountForRound(50));
            Assert.AreEqual(3, round.BossCountForRound(100));
            Assert.AreEqual(round.maxBosses, round.BossCountForRound(500));
            Object.DestroyImmediate(round);
        }
    }
}
