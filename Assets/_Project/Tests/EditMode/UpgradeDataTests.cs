using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class UpgradeDataTests
    {
        UpgradeData _data;

        [SetUp]
        public void SetUp() => _data = ScriptableObject.CreateInstance<UpgradeData>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_data);

        [Test]
        public void PowerUpBonus_GrowsWithDiminishingReturns()
        {
            foreach (var p in _data.powerUps)
            {
                Assert.AreEqual(0f, p.Bonus(0), p.name);
                Assert.AreEqual(p.maxBonus * 0.5f, p.Bonus(Mathf.RoundToInt(p.halfLevel)), 0.01f, p.name);
                float lastGain = float.MaxValue;
                for (int level = 1; level <= 40; level++)
                {
                    float gain = p.Bonus(level) - p.Bonus(level - 1);
                    Assert.Greater(gain, 0f, $"{p.name} level {level}");
                    Assert.LessOrEqual(gain, lastGain + 1e-6f, $"{p.name} level {level}");
                    Assert.Less(p.Bonus(level), p.maxBonus + 1e-4f, $"{p.name} level {level}");
                    lastGain = gain;
                }
            }
        }

        [Test]
        public void Levels_PackAndUnpackIndependently()
        {
            ulong packed = 0;
            for (int i = 0; i < UpgradeData.MaxPowerUps; i++) packed = UpgradeData.WithLevel(packed, i, i * 30 + 1);
            for (int i = 0; i < UpgradeData.MaxPowerUps; i++) Assert.AreEqual(i * 30 + 1, UpgradeData.LevelOf(packed, i));
            packed = UpgradeData.WithLevel(packed, 3, 0);
            Assert.AreEqual(0, UpgradeData.LevelOf(packed, 3));
            Assert.AreEqual(61, UpgradeData.LevelOf(packed, 2));
            Assert.AreEqual(255, UpgradeData.LevelOf(UpgradeData.WithLevel(0, 0, 999), 0));
        }

        [Test]
        public void StatBonus_ReadsPackedLevels()
        {
            ulong packed = UpgradeData.WithLevel(0, 0, 8); // Hollow Point at its half level.
            Assert.AreEqual(_data.powerUps[0].maxBonus * 0.5f, _data.StatBonus(packed, _data.powerUps[0].stat), 0.01f);
            Assert.AreEqual(0f, _data.StatBonus(packed, PowerStat.MoveSpeed));
        }

        [Test]
        public void Rounds_OfferEveryFifth_EmpowerEveryTwentyFifth()
        {
            Assert.IsFalse(_data.IsPowerUpRound(4));
            Assert.IsTrue(_data.IsPowerUpRound(5));
            Assert.IsTrue(_data.IsPowerUpRound(50));
            Assert.IsFalse(_data.IsPowerUpRound(55), "offers stop after round 50");
            Assert.IsFalse(_data.IsPowerUpRound(500));
            Assert.IsFalse(_data.IsEmpoweredRound(5));
            Assert.IsTrue(_data.IsEmpoweredRound(25));
            Assert.IsTrue(_data.IsEmpoweredRound(50));
            Assert.IsFalse(_data.IsEmpoweredRound(75));
        }

        [Test]
        public void MachineWindow_ChangesAfterEachPowerUpRound()
        {
            Assert.AreEqual(0, _data.WindowOf(1));
            Assert.AreEqual(0, _data.WindowOf(5));
            Assert.AreEqual(1, _data.WindowOf(6));
            Assert.AreEqual(1, _data.WindowOf(10));
            Assert.AreEqual(2, _data.WindowOf(11));
        }

        [Test]
        public void MachineTiers_GetStronger()
        {
            Assert.AreEqual(1f, _data.TierDamage(0));
            Assert.AreEqual(1f, _data.TierCapacity(0));
            for (int t = 1; t <= _data.MaxTier; t++)
            {
                Assert.Greater(_data.TierDamage(t), _data.TierDamage(t - 1));
                Assert.GreaterOrEqual(_data.TierCapacity(t), _data.TierCapacity(t - 1));
            }
            Assert.IsTrue(_data.Tier(_data.MaxTier).explosive);
        }

        [Test]
        public void UpgradedBullets_GoThroughMoreZombies_AndGlowPerTier()
        {
            var sniper = ScriptableObject.CreateInstance<GunData>();
            sniper.pierce = 5;
            var smg = ScriptableObject.CreateInstance<GunData>();
            Assert.AreEqual(5, sniper.PierceWith(null));
            Assert.AreEqual(0, smg.PierceWith(null), "most guns stop in the first zombie");
            int last = 0;
            var colors = new System.Collections.Generic.HashSet<Color>();
            for (int t = 1; t <= _data.MaxTier; t++)
            {
                int pierce = smg.PierceWith(_data.Tier(t));
                Assert.GreaterOrEqual(pierce, last);
                Assert.Greater(pierce, 0, "every upgraded gun pierces");
                last = pierce;
                Assert.Greater(UpgradeData.TierGlow(t).a, 0f);
                colors.Add(UpgradeData.TierGlow(t));
            }
            Assert.AreEqual(_data.MaxTier, colors.Count, "each tier glows its own colour");
            Assert.AreEqual(0f, UpgradeData.TierGlow(0).a, "a plain gun doesn't glow");
            Object.DestroyImmediate(sniper);
            Object.DestroyImmediate(smg);
        }
    }
}
