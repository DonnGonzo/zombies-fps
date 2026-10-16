using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class RoundDataTests
    {
        RoundData _data;

        [SetUp]
        public void SetUp() => _data = ScriptableObject.CreateInstance<RoundData>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_data);

        [Test]
        public void ZombieCount_FollowsFormula()
        {
            // base + perRound * round^0.85 with the defaults (6 + 2.2 * r^0.85).
            Assert.AreEqual(8, _data.ZombieCountForRound(1));
            Assert.AreEqual(Mathf.RoundToInt(6f + 2.2f * Mathf.Pow(100f, 0.85f)), _data.ZombieCountForRound(100));
        }

        [Test]
        public void ZombieCount_NeverDecreases_AndScalesWithPlayers()
        {
            for (int r = 2; r <= RoundData.MaxRound; r++)
                Assert.GreaterOrEqual(_data.ZombieCountForRound(r), _data.ZombieCountForRound(r - 1), $"round {r}");
            Assert.Greater(_data.ZombieCountForRound(10, 4), _data.ZombieCountForRound(10, 1));
        }

        [Test]
        public void Health_StartsAtBase_AndNeverDecreases()
        {
            Assert.AreEqual(_data.healthBase, _data.HealthForRound(1));
            for (int r = 2; r <= RoundData.MaxRound; r++)
                Assert.GreaterOrEqual(_data.HealthForRound(r), _data.HealthForRound(r - 1), $"round {r}");
        }

        [Test]
        public void Health_SoftCapsAfterRound30()
        {
            // Growth per round after the soft cap must be slower than before it.
            double before = (double)_data.HealthForRound(30) / _data.HealthForRound(29);
            double after = (double)_data.HealthForRound(60) / _data.HealthForRound(59);
            Assert.Less(after, before);
        }

        [Test]
        public void Health_AtRound500_IsPositiveAndFinite()
        {
            long h = _data.HealthForRound(RoundData.MaxRound);
            Assert.Greater(h, 0);
            Assert.Less(h, long.MaxValue);
        }

        [Test]
        public void RoundsOutsideRange_AreClamped()
        {
            Assert.AreEqual(_data.HealthForRound(1), _data.HealthForRound(0));
            Assert.AreEqual(_data.HealthForRound(RoundData.MaxRound), _data.HealthForRound(9999));
        }

        [Test]
        public void SpeedTiers_ByRound()
        {
            Assert.AreEqual(ZombieSpeedTier.Walk, _data.SpeedTierForRound(1));
            Assert.AreEqual(ZombieSpeedTier.Jog, _data.SpeedTierForRound(_data.jogFromRound));
            Assert.AreEqual(ZombieSpeedTier.Sprint, _data.SpeedTierForRound(_data.sprintFromRound));
        }
    }
}
