using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.World;

namespace ZombiesFPS.Tests
{
    public class MapUpdateTests
    {
        [Test]
        public void MapKey_DefaultsToM_AndEveryActionHasALabel()
        {
            Assert.AreEqual(KeyCode.M, Bindings.Default(GameAction.Map));
            for (int i = 0; i < Bindings.Count; i++) Assert.IsFalse(string.IsNullOrEmpty(Bindings.Label((GameAction)i)), ((GameAction)i).ToString());
        }

        [Test]
        public void HighGroundMissions_ArePlaced_AndFitTheSlot()
        {
            var data = ScriptableObject.CreateInstance<MissionData>();
            int found = 0;
            for (int id = 0; id < data.Count; id++)
                if (data.At(id).objective == MissionObjective.HoldHighGround) found++;
            Assert.GreaterOrEqual(found, 1);
            Assert.IsTrue(MissionData.IsPlaced(MissionObjective.HoldHighGround));
            Assert.IsFalse(MissionData.IsPlaced(MissionObjective.Kills));
            // The slot's 6-bit place field holds every zone and every high spot.
            ulong slot = MissionData.Pack(3, false, ZoneMap.HighSpotCount - 1, 5, 30);
            Assert.AreEqual(ZoneMap.HighSpotCount - 1, MissionData.ZoneOf(slot));
            Assert.Less(ZoneMap.ZoneCount, 64);
        }

        [Test]
        public void Ladder_IsInReachOnlyInFrontOfIt()
        {
            Ladder.ClearAll();
            var ladder = new Ladder(new Vector3(0f, 0f, 0f), Vector3.back, 4f);
            Assert.IsTrue(ladder.InReach(new Vector3(0f, 0f, -0.4f)));
            Assert.IsTrue(ladder.InReach(new Vector3(0.3f, 2f, -0.4f)));
            Assert.IsFalse(ladder.InReach(new Vector3(0f, 0f, 0.6f)), "behind the wall");
            Assert.IsFalse(ladder.InReach(new Vector3(1.5f, 0f, -0.4f)), "off to the side");
            Assert.IsFalse(ladder.InReach(new Vector3(0f, 5f, -0.4f)), "above the top");
            Assert.AreSame(ladder, Ladder.Near(new Vector3(0f, 1f, -0.5f)));
            Ladder.ClearAll();
        }
    }
}
