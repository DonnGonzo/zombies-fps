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
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
                Assert.Greater(filter.sharedMesh.vertexCount, 0, id + " contains an empty mesh.");
            foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                Assert.Greater(skin.sharedMesh.vertexCount, 0, id + " contains an empty skinned mesh.");
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
                var baked = new Mesh();
                try
                {
                    var source = visible[0].sharedMesh;
                    Assert.Greater(source.vertexCount, 100);
                    Assert.AreEqual(source.vertexCount, source.boneWeights.Length);
                    Assert.IsTrue(source.boneWeights.All(w => w.weight0 + w.weight1 + w.weight2 + w.weight3 > 0.99f), "Unweighted vertices");
                    Assert.IsTrue(source.bindposes.All(m => Mathf.Abs(m.determinant) > 1e-12f), "Singular bind poses");
                    Assert.Greater(visible[0].transform.lossyScale.y, 0.0001f, "Skin scale");
                    visible[0].BakeMesh(baked);
                    baked.RecalculateBounds();
                    var size = Vector3.Scale(baked.bounds.size, visible[0].transform.lossyScale);
                    Assert.That(size.y, Is.InRange(1f, 2.5f), "Deformed skin must remain human-sized: " + size);
                    var center = visible[0].transform.TransformPoint(baked.bounds.center);
                    Assert.Less(Vector3.Distance(center, parent.transform.position + Vector3.up), 1f, "Skin must remain at its hitbox: " + center);
                }
                finally { Object.DestroyImmediate(baked); }
                model.Die(0, false);
                model.Animate(0.016f, 0f, false, 0f, 1f, 0, 0f, false);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void BloodPuffHasTransparentCornersAndFadesCompletely()
        {
            var material = FxSprites.Make(FxSprites.Smoke, false, new Color(0.32f, 0.02f, 0.02f, 0.6f));
            Assert.IsNotNull(material);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var cameraObject = new GameObject("Puff render test");
            var target = new RenderTexture(64, 64, 16);
            var pixels = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            bool fog = RenderSettings.fog;
            try
            {
                RenderSettings.fog = false;
                quad.layer = 31;
                quad.transform.position = new Vector3(0, 0, 2);
                quad.GetComponent<Renderer>().sharedMaterial = material;
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 0.5f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.cullingMask = 1 << 31; camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
                Assert.Greater(pixels.GetPixel(2, 2).grayscale, 0.95f, "Smoke corners must not render a black square.");
                Assert.Less(pixels.GetPixel(32, 32).grayscale, 0.95f, "The center must actually draw blood mist.");
                material.color = new Color(0.32f, 0.02f, 0.02f, 0f);
                camera.Render(); pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
                Assert.IsTrue(pixels.GetPixels().All(c => c.grayscale > 0.99f), "Zero alpha must leave no opaque quad.");
            }
            finally
            {
                RenderSettings.fog = fog; RenderTexture.active = previous;
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(quad);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material);
            }
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
