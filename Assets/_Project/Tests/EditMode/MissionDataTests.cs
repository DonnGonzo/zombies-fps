using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class MissionDataTests
    {
        MissionData _data;

        [SetUp]
        public void SetUp() => _data = ScriptableObject.CreateInstance<MissionData>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_data);

        [Test]
        public void Slot_PacksAndUnpacks()
        {
            ulong slot = MissionData.Pack(7, true, 5, 123456, 654321);
            Assert.AreEqual(7, MissionData.IdOf(slot));
            Assert.IsTrue(MissionData.DoneOf(slot));
            Assert.AreEqual(5, MissionData.ZoneOf(slot));
            Assert.AreEqual(123456, MissionData.ProgressOf(slot));
            Assert.AreEqual(654321, MissionData.TargetOf(slot));
            Assert.AreEqual(MissionData.NoMission, MissionData.IdOf(MissionData.Empty));
        }

        [Test]
        public void Sets_RefreshOnRoundOneThenEveryTen()
        {
            Assert.IsTrue(_data.IsRefreshRound(1));
            Assert.IsFalse(_data.IsRefreshRound(9));
            Assert.IsTrue(_data.IsRefreshRound(10));
            Assert.IsTrue(_data.IsRefreshRound(20));
            Assert.AreEqual(1, _data.SetStartRound(9));
            Assert.AreEqual(10, _data.SetStartRound(10));
            Assert.AreEqual(10, _data.SetStartRound(19));
            Assert.AreEqual(10, _data.NextRefreshRound(1));
            Assert.AreEqual(20, _data.NextRefreshRound(10));
        }

        [Test]
        public void Targets_GrowWithRounds_AndStayCapped()
        {
            foreach (var def in _data.missions)
            {
                Assert.GreaterOrEqual(def.TargetFor(1), 1, def.title);
                Assert.GreaterOrEqual(def.TargetFor(100), def.TargetFor(1), def.title);
                Assert.LessOrEqual(def.TargetFor(500), def.targetMax, def.title);
                Assert.Greater(def.PointsFor(1), 0, def.title);
                StringAssert.DoesNotContain("{0}", def.Describe(def.TargetFor(1)), def.title);
            }
        }

        [Test]
        public void KillTypeMissions_WaitForTheirZombies()
        {
            int brutes = _data.missions.FindIndex(m => m.objective == MissionObjective.KillType && m.zombieKind == ZombieKind.Brute);
            Assert.GreaterOrEqual(brutes, 0);
            System.Func<ZombieKind, int> firstRound = k => k == ZombieKind.Brute ? 20 : 1;
            Assert.IsFalse(_data.IsEligible(brutes, 10, firstRound));
            Assert.IsTrue(_data.IsEligible(brutes, 20, firstRound));
            Assert.IsTrue(_data.IsEligible(0, 1, firstRound));
        }
    }
}
