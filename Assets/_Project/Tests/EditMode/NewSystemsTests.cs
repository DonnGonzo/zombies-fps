using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class NewSystemsTests
    {
        [Test]
        public void MissionSlot_KeepsFailedFlagAndWideZone()
        {
            ulong slot = MissionData.Pack(3, false, 40, 77, 120, failed: true);
            Assert.AreEqual(3, MissionData.IdOf(slot));
            Assert.IsFalse(MissionData.DoneOf(slot));
            Assert.AreEqual(40, MissionData.ZoneOf(slot));
            Assert.IsTrue(MissionData.FailedOf(slot));
            Assert.AreEqual(77, MissionData.ProgressOf(slot));
            Assert.AreEqual(120, MissionData.TargetOf(slot));
            Assert.IsFalse(MissionData.FailedOf(MissionData.Pack(3, false, 40, 77, 120)));
        }

        [Test]
        public void Prestige_UnlocksAtThirty_UpToTen()
        {
            var shop = ScriptableObject.CreateInstance<ShopData>();
            try
            {
                Assert.AreEqual(10, shop.MaxPrestige);
                Assert.IsFalse(shop.CanPrestige(29, 0));
                Assert.IsTrue(shop.CanPrestige(30, 0));
                Assert.IsTrue(shop.CanPrestige(30, 9));
                Assert.IsFalse(shop.CanPrestige(30, 10));
                Assert.AreEqual("", shop.PrestigeName(0));
                Assert.AreEqual("Master", shop.PrestigeName(10));
                for (int i = 0; i < shop.CamoCount; i++)
                {
                    var camo = shop.CamoAt(i);
                    if (camo.unlockPrestige <= 0) continue;
                    Assert.IsFalse(shop.IsCamoUnlocked(i, 999, camo.unlockPrestige - 1), camo.name);
                    Assert.IsTrue(shop.IsCamoUnlocked(i, 1, camo.unlockPrestige), camo.name);
                }
            }
            finally { Object.DestroyImmediate(shop); }
        }

        [Test]
        public void Perks_EightMachines_BitmaskRoundTrips()
        {
            var perks = ScriptableObject.CreateInstance<PerkData>();
            try
            {
                Assert.AreEqual(8, perks.Count);
                int mask = 0;
                mask = PerkData.With(mask, PerkKind.Ironhide);
                mask = PerkData.With(mask, PerkKind.Bloodline);
                mask = PerkData.With(mask, PerkKind.Bloodline);
                Assert.IsTrue(PerkData.Has(mask, PerkKind.Ironhide));
                Assert.IsTrue(PerkData.Has(mask, PerkKind.Bloodline));
                Assert.IsFalse(PerkData.Has(mask, PerkKind.FleetFoot));
                Assert.AreEqual(2, PerkData.CountOwned(mask));
                for (int i = 0; i < perks.Count; i++) Assert.Greater(perks.At(i).price, 0);
            }
            finally { Object.DestroyImmediate(perks); }
        }

        [Test]
        public void StationPrices_ClimbAndCap()
        {
            var perks = ScriptableObject.CreateInstance<PerkData>();
            try
            {
                Assert.AreEqual(perks.platePrice, perks.PlatePrice(1));
                Assert.Greater(perks.PlatePrice(50), perks.PlatePrice(10));
                Assert.AreEqual(perks.platePriceMax, perks.PlatePrice(500));
                Assert.AreEqual(perks.rerollPrice, perks.RerollPrice(1));
                Assert.AreEqual(perks.rerollPriceMax, perks.RerollPrice(500));
            }
            finally { Object.DestroyImmediate(perks); }
        }

        [Test]
        public void Explosions_FallOffToTheEdge_AndScaleWithRounds()
        {
            Assert.AreEqual(1000, EquipmentData.Falloff(1000, 0f, 6f, 0.3f));
            Assert.AreEqual(650, EquipmentData.Falloff(1000, 3f, 6f, 0.3f));
            Assert.AreEqual(300, EquipmentData.Falloff(1000, 6f, 6f, 0.3f));
            Assert.AreEqual(300, EquipmentData.Falloff(1000, 60f, 6f, 0.3f));

            var eq = ScriptableObject.CreateInstance<EquipmentData>();
            try
            {
                Assert.Greater(eq.grenade.DamageFor(100000), eq.grenade.DamageFor(100));
                Assert.Greater(eq.MeleeDamageFor(100000), eq.MeleeDamageFor(100));
                Assert.AreSame(eq.knife, eq.Def(ThrowableKind.Knife));
                Assert.Greater(eq.concussion.stunTime, 0f);
            }
            finally { Object.DestroyImmediate(eq); }
        }

        [Test]
        public void Blackout_EveryTwentyFiveRounds()
        {
            var rounds = ScriptableObject.CreateInstance<RoundData>();
            try
            {
                Assert.IsFalse(rounds.IsBlackoutRound(24));
                Assert.IsTrue(rounds.IsBlackoutRound(25));
                Assert.IsTrue(rounds.IsBlackoutRound(500));
                Assert.IsFalse(rounds.IsBlackoutRound(0));
            }
            finally { Object.DestroyImmediate(rounds); }
        }
    }
}
