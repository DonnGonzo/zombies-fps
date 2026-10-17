using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Tests
{
    public class ShopDataTests
    {
        ShopData _shop;

        [SetUp]
        public void SetUp() => _shop = ScriptableObject.CreateInstance<ShopData>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_shop);

        [Test]
        public void Levels_FollowTheXpCurve()
        {
            Assert.AreEqual(1, _shop.LevelFor(0));
            Assert.AreEqual(1, _shop.LevelFor(_shop.XpToNext(1) - 1));
            Assert.AreEqual(2, _shop.LevelFor(_shop.XpToNext(1)));
            Assert.AreEqual(3, _shop.LevelFor(_shop.XpToNext(1) + _shop.XpToNext(2)));
            Assert.AreEqual(5, _shop.XpIntoLevel(_shop.XpToNext(1) + 5));
            Assert.AreEqual(_shop.maxLevel, _shop.LevelFor(long.MaxValue / 4));
        }

        [Test]
        public void Attachments_UnlockInLevelOrder_FromLevelOne()
        {
            Assert.AreEqual(1, _shop.At(0).unlockLevel);
            for (int i = 1; i < _shop.Count; i++)
                Assert.GreaterOrEqual(_shop.At(i).unlockLevel, _shop.At(i - 1).unlockLevel, _shop.At(i).name);
        }

        [Test]
        public void WithAttachment_ReplacesTheSameGunSlot()
        {
            int redDot = _shop.attachments.FindIndex(a => a.name == "Red-Dot Sight");
            int scope = _shop.attachments.FindIndex(a => a.name == "Marksman Scope");
            int grip = _shop.attachments.FindIndex(a => a.name == "Foregrip");
            int mask = _shop.WithAttachment(0, redDot);
            mask = _shop.WithAttachment(mask, grip);
            mask = _shop.WithAttachment(mask, scope);
            Assert.IsTrue(ShopData.Has(mask, scope));
            Assert.IsTrue(ShopData.Has(mask, grip));
            Assert.IsFalse(ShopData.Has(mask, redDot), "two optics can't be fitted at once");
        }

        [Test]
        public void Apply_ChangesACopy_AndCachesIt()
        {
            var gun = ScriptableObject.CreateInstance<GunData>();
            gun.damage = 100;
            gun.automatic = false;
            int barrel = _shop.attachments.FindIndex(a => a.name == "Heavy Barrel");
            int auto = _shop.attachments.FindIndex(a => a.name == "Full-Auto Conversion");
            int mask = _shop.WithAttachment(_shop.WithAttachment(0, barrel), auto);
            var fitted = _shop.Apply(gun, mask);
            Assert.AreNotSame(gun, fitted);
            Assert.AreEqual(125, fitted.damage);
            Assert.IsTrue(fitted.automatic);
            Assert.AreEqual(100, gun.damage, "the original asset is untouched");
            Assert.AreSame(fitted, _shop.Apply(gun, mask));
            Assert.AreSame(gun, _shop.Apply(gun, 0));
            Object.DestroyImmediate(fitted);
            Object.DestroyImmediate(gun);
        }
    }
}
