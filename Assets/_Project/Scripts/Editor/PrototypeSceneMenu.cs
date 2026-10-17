using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.Editor
{
    /// <summary>Editor shortcuts for the prototype. Rebuild recreates the scene if it ever gets lost or broken.</summary>
    public static class PrototypeSceneMenu
    {
        const string ScenePath = "Assets/_Project/Scenes/Prototype.unity";
        const string DataFolder = "Assets/_Project/Data";

        [MenuItem("Zombies FPS/Open Prototype Scene", priority = 0)]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) RebuildScene();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Zombies FPS/Rebuild Prototype Scene", priority = 1)]
        public static void RebuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("Game").AddComponent<PrototypeBootstrap>();
            boot.gunData = LoadOrCreate<GunData>("Gun_Rook9");
            boot.zombieData = LoadOrCreate<ZombieData>("Zombie_Walker");
            boot.roundData = LoadOrCreate<RoundData>("RoundData");
            // No fallback asset: without one the game uses the built-in prices and wall guns.
            boot.priceTable = AssetDatabase.LoadAssetAtPath<PriceTable>($"{DataFolder}/PriceTable.asset");
            boot.upgradeData = LoadOrCreate<UpgradeData>("UpgradeData");
            boot.missionData = LoadOrCreate<MissionData>("MissionData");
            var types = new System.Collections.Generic.List<ZombieData>();
            foreach (var name in new[] { "Zombie_Runner", "Zombie_Crawler", "Zombie_Brute", "Zombie_Spitter", "Zombie_Boss" })
            {
                // No fallback assets: a missing type uses the built-in values.
                var type = AssetDatabase.LoadAssetAtPath<ZombieData>($"{DataFolder}/{name}.asset");
                if (type != null) types.Add(type);
            }
            boot.zombieTypes = types.ToArray();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == ScenePath) return;
            ArrayUtility.Insert(ref scenes, 0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes;
        }

        static T LoadOrCreate<T>(string name) where T : ScriptableObject
        {
            string path = $"{DataFolder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
