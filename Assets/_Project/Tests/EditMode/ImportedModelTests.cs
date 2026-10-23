using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.Tests
{
    public class ImportedModelTests
    {
        [TestCase("church-bench-2")]
        [TestCase("desert-eagle-gun")]
        [TestCase("gothic-window")]
        [TestCase("gun-m4a1")]
        [TestCase("gun-rhino-smg")]
        [TestCase("heavy-pulse-machine-gun")]
        [TestCase("hl2-beta-air-exchange-truck-remade")]
        [TestCase("hospital-bed")]
        [TestCase("low-poly-9mm-hand-gun")]
        [TestCase("prison-cell")]
        [TestCase("submachine-gun")]
        [TestCase("tommy-gun")]
        [TestCase("zombie-hazmat")]
        [TestCase("zombie-pickup-truck")]
        public void AllSuppliedPacksHaveUsableTexturedPrefabs(string id)
        {
            var prefab = ImportedModels.Get(id);
            Assert.IsNotNull(prefab, id);
            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(), "Map collision must remain independent of visual quality settings.");
            var renderers = prefab.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers);
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material);
                    Assert.IsNotNull(material.shader);
                    Assert.AreNotEqual("Hidden/InternalErrorShader", material.shader.name);
                    if (id != "prison-cell") Assert.IsNotNull(material.mainTexture, id + "/" + material.name);
                }
        }

        [Test]
        public void EveryImportedGunBuildsWithCamosAttachmentsAndForwardMuzzle()
        {
            var prices = AssetDatabase.LoadAssetAtPath<PriceTable>("Assets/_Project/Data/PriceTable.asset");
            foreach (var name in new[] { "Rook-9 Carbine", "Hatchet .44", "Wren-7 SMG", "Talon LMG", "Service 9mm", "TEC-9", "Tommy Gun" })
            {
                var gun = prices.wallGuns.First(x => x.gun.displayName == name).gun;
                var parent = new GameObject(name);
                try
                {
                    var view = GunArt.BuildView(gun, parent.transform);
                    Assert.IsNotNull(view, name);
                    Assert.IsNotNull(view.Root.GetComponentInChildren<MeshFilter>(true));
                    view.Fit(0); view.Repaint(Color.blue); view.Repaint(null);
                    view.ReloadPose(0.5f); view.ReloadPose(-1f);
                    Assert.Greater(view.Muzzle.localPosition.z, 0f, name);
                    Assert.IsTrue(GunArt.Extent(gun, out float length, out _, out float width));
                    Assert.That(length, Is.InRange(0.2f, 1f));
                    Assert.That(width, Is.InRange(0.005f, 0.25f));
                }
                finally { Object.DestroyImmediate(parent); }
            }
        }

        [Test]
        public void NewWallGunsDoNotMoveExistingSavedCosmeticIds()
        {
            var prices = AssetDatabase.LoadAssetAtPath<PriceTable>("Assets/_Project/Data/PriceTable.asset");
            var legacy = Object.Instantiate(prices);
            var reward = EasterEggData.MakeGodGun();
            try
            {
                legacy.wallGuns.RemoveRange(9, legacy.wallGuns.Count - 9);
                var old = legacy.BuildCatalog(prices.wallGuns[0].gun, reward);
                var current = prices.BuildCatalog(prices.wallGuns[0].gun, reward);
                CollectionAssert.AreEqual(old, current.Take(old.Length).ToArray());
                Assert.AreEqual(old.Length + 3, current.Length);
            }
            finally { Object.DestroyImmediate(legacy); Object.DestroyImmediate(reward); }
        }

        [Test]
        public void ReloadReturnsToSeatedMagazineAndContainsImportedMotion()
        {
            var motion = Resources.Load<ImportedMagazineMotion>("ImportedModels/9mmReload");
            Assert.IsNotNull(motion);
            Assert.AreEqual(Vector3.zero, motion.Evaluate(-1f));
            Assert.AreEqual(Vector3.zero, motion.Evaluate(0f));
            Assert.AreEqual(Vector3.zero, motion.Evaluate(1f));
            Assert.Greater(motion.Evaluate(0.3f).magnitude, 0.1f);
            var pistol = ImportedModels.Get("low-poly-9mm-hand-gun");
            Assert.Less(ImportedModels.Bounds(pistol).size.y, 0.2f, "The exported detached magazine must be seated before building the prefab.");
        }

        [Test]
        public void HazmatCombatRigCanAnimateAndDieWithTexturedSkin()
        {
            var parent = new GameObject("Hazmat combat smoke test");
            try
            {
                var model = ZombiesFPS.Zombies.ZombieModel.Create(parent.transform, 1.8f, ZombiesFPS.Zombies.ZombieModel.Hazmat);
                Assert.IsNotNull(model);
                model.UseSkin(null, 0);
                model.ResetLife(42);
                model.Animate(0.016f, 1f, false, 0f, 1f, 0, 0f, false);
                var visible = parent.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray();
                Assert.AreEqual(1, visible.Length);
                Assert.AreEqual(2, visible[0].sharedMaterials.Length);
                Assert.IsTrue(visible[0].sharedMaterials.All(m => m.mainTexture != null));
                model.Die(0, false);
                model.Animate(0.016f, 0f, false, 0f, 1f, 0, 0f, false);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void HazmatRetainsRigAndNativeClips()
        {
            var prefab = ImportedModels.Get("zombie-hazmat");
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.AreEqual(1, renderers.Length, "Head and suit share the enemy tint, hit flash and skin retargeting.");
            Assert.AreEqual(2, renderers[0].sharedMaterials.Length);
            Assert.Greater(renderers[0].bones.Length, 40);
            foreach (var name in new[] { "Zombie_Idle", "Zombie_EnemySpotted", "Zombie_Skill", "Zombie_Walk_Root" })
                Assert.Greater(Resources.Load<AnimationClip>("ImportedModels/HazmatAnimations/" + name).length, 1f);
        }
    }
}
