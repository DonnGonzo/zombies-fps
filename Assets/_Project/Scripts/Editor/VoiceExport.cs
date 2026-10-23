using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.Editor
{
    public static class VoiceExport
    {
        [Serializable] public class Line { public string speaker, text, path; }
        [Serializable] public class Script { public Line[] lines; }
        public static Line[] AllLines()
        {
            var lines = new Dictionary<string, Line>();
            void Add(string speaker, string text, string path = null)
            {
                path = path ?? VoiceLibrary.PathFor(speaker, text);
                lines[path] = new Line { speaker = VoiceLibrary.SpeakerKey(speaker), text = text, path = path };
            }
            void Radio(string text, int act) => Add(act < 4 ? "Vale" : "Trader", text);
            foreach (var beat in StoryData.Beats)
                foreach (var line in beat.lines) Radio(line, StoryData.ActFor(beat.round));
            foreach (var line in StoryData.PowerOn) Add("Vale", line);
            foreach (var line in StoryData.Finale) Add("Vale", line);
            foreach (var line in StoryData.Victory) Add("Trader", line);
            for (int act = 0; act < StoryData.GameOver.Length; act++) Radio(StoryData.GameOver[act], act);
            foreach (var field in new[] { StoryData.MissionSet, StoryData.MissionComplete })
                for (int act = 0; act < field.Length; act++) foreach (var line in field[act]) Radio(line, act);
            foreach (var line in StoryData.BossRound.Concat(StoryData.BlackoutRound).Concat(new[] { StoryData.SurvivorLost }))
                foreach (var speaker in new[] { "Vale", "Trader" }) Add(speaker, line);
            foreach (var act in StoryData.TraderLore) foreach (var line in act) Add("Trader", line);
            for (int c = 0; c < Characters.Count; c++)
            {
                foreach (var e in StoryData.Voice[c]) foreach (var line in e) Add(Characters.At(c).name, line);
                foreach (var line in StoryData.MirrorBanter) Add(Characters.At(c).name, line);
            }
            foreach (var pair in StoryData.BanterPairs)
            {
                Add(Characters.At(pair.a).name, pair.lineA);
                Add(Characters.At(pair.b).name, pair.lineB);
            }
            void Egg(object value)
            {
                if (value is EggLine e)
                {
                    var folder = EasterEggData.VoiceFolder(e.speaker);
                    Add(folder.TrimEnd('/').Split('/').Last(), e.text, folder + e.id);
                }
                else if (value is IEnumerable list && !(value is string)) foreach (var item in list) Egg(item);
                else if (value is EggStep || (value != null && value.GetType().FullName.StartsWith("System.ValueTuple")))
                    foreach (var field in value.GetType().GetFields()) Egg(field.GetValue(value));
            }
            foreach (var field in typeof(EasterEggData).GetFields(BindingFlags.Public | BindingFlags.Static)) Egg(field.GetValue(null));
            return lines.Values.OrderBy(l => l.path).ToArray();
        }

        [MenuItem("Zombies FPS/Voice/Export dialogue script")]
        public static void Export()
        {
            Directory.CreateDirectory("tools/voice");
            File.WriteAllText("tools/voice/dialogue.json", JsonUtility.ToJson(new Script { lines = AllLines() }, true));
        }

        public static void WeaponPreviews()
        {
            Directory.CreateDirectory("Temp/weapon-review");
            foreach (var gun in AssetDatabase.FindAssets("t:GunData", new[] { "Assets/_Project/Data" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GunData>))
            {
                foreach (string pose in new[] { "hip", "aim", "reload" })
                {
                    var root = new GameObject("View model review");
                    var preview = new PreviewRenderUtility();
                    try
                    {
                        var mount = new GameObject("Gun mount").transform;
                        mount.SetParent(root.transform, false);
                        mount.localPosition = pose == "aim" ? new Vector3(0,-0.071f,0.2f) : Weapons.WeaponView.HipPosition;
                        if (pose == "reload") { mount.localPosition += new Vector3(0,-0.1f,-0.02f); mount.localRotation = Quaternion.Euler(35,0,28); }
                        var model = Weapons.GunModels.Build(gun, mount);
                        model.Root.SetActive(true); model.Fit?.Invoke(0);
                        var armsRoot = new GameObject("Arms"); armsRoot.transform.SetParent(root.transform, false);
                        var arms = armsRoot.AddComponent<Weapons.FirstPersonArms>(); arms.Init(root.transform); arms.Equip(gun, model);
                        arms.Pose(pose == "reload" ? 0.5f : -1f, 0, false);
                        preview.AddSingleGO(root);
                        preview.camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        preview.camera.nearClipPlane = 0.03f; preview.camera.farClipPlane = 10;
                        preview.camera.fieldOfView = pose == "aim" ? gun.aimFov : 75;
                        preview.camera.clearFlags = CameraClearFlags.Color; preview.camera.backgroundColor = new Color(0.18f,0.21f,0.24f);
                        preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(30,-30,0);
                        preview.lights[1].intensity = 0.6f; preview.ambientColor = Color.gray;
                        preview.BeginStaticPreview(new Rect(0,0,1280,720)); preview.Render();
                        var texture = preview.EndStaticPreview();
                        File.WriteAllBytes("Temp/weapon-review/" + gun.name + "_" + pose + ".png", texture.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                    finally { preview.Cleanup(); if (root != null) UnityEngine.Object.DestroyImmediate(root); }
                }
            }
        }
    }

    public sealed class DialogueAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_Project/Resources/Audio/Voice/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.8f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
        }
    }
}
