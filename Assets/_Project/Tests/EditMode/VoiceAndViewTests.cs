using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.Tests
{
    public class VoiceAndViewTests
    {
        [Serializable] class Line { public string speaker, text, path; }
        [Serializable] class Script { public Line[] lines; }

        [Test]
        public void FullDialogueManifestHasPlayableMonoRecordings()
        {
            var script = JsonUtility.FromJson<Script>(File.ReadAllText("tools/voice/dialogue.json"));
            Assert.GreaterOrEqual(script.lines.Length, 334);
            foreach (var line in script.lines)
            {
                var clip = Resources.Load<AudioClip>(line.path);
                Assert.IsNotNull(clip, line.path);
                Assert.That(clip.length, Is.InRange(0.2f, 120f), line.path);
                Assert.AreEqual(1, clip.channels, line.path);
            }
        }

        [Test]
        public void SurvivorEventsAndBanterResolveToTheirOwnVoices()
        {
            for (int c = 0; c < Characters.Count; c++)
            {
                var name = Characters.At(c).name;
                foreach (var e in StoryData.Voice[c])
                    foreach (var text in e) Assert.IsNotNull(VoiceLibrary.Get(name,text), name + ": " + text);
                foreach (var text in StoryData.MirrorBanter) Assert.IsNotNull(VoiceLibrary.Get(name,text));
            }
            foreach (var b in StoryData.BanterPairs)
            {
                Assert.IsNotNull(VoiceLibrary.Get(Characters.At(b.a).name,b.lineA));
                Assert.IsNotNull(VoiceLibrary.Get(Characters.At(b.b).name,b.lineB));
            }
            Assert.AreEqual(VoiceLibrary.PathFor(StoryData.DirectorName,"test"), VoiceLibrary.PathFor(StoryData.DispatcherName,"test"));
        }

        [Test]
        public void EveryGunHasTwoHandsFollowingItsMovingGripSpace()
        {
            foreach (var path in AssetDatabase.FindAssets("t:GunData", new[] { "Assets/_Project/Data" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var gun = AssetDatabase.LoadAssetAtPath<GunData>(path);
                var root = new GameObject("Weapon grip test");
                try
                {
                    var mount = new GameObject("Mount").transform; mount.SetParent(root.transform, false);
                    mount.localPosition = WeaponView.HipPosition;
                    var model = GunModels.Build(gun,mount); model.Root.SetActive(true);
                    var armsRoot = new GameObject("Arms"); armsRoot.transform.SetParent(root.transform,false);
                    var arms = armsRoot.AddComponent<FirstPersonArms>(); arms.Init(root.transform); arms.Equip(gun,model);
                    arms.Pose(-1,0,false);
                    var gripSpace = model.Root.transform.Find("Model") ?? model.Root.transform;
                    var hand = armsRoot.transform.Find("Trigger hand");
                    var local = gripSpace.InverseTransformPoint(hand.position);
                    Assert.IsEmpty(armsRoot.GetComponentsInChildren<Collider>());
                    Assert.IsNotNull(armsRoot.transform.Find("Support hand"));
                    mount.localRotation = Quaternion.Euler(35,-32,28); mount.localPosition += Vector3.down * 0.1f;
                    arms.Pose(0.5f,0,false);
                    Assert.Less(Vector3.Distance(local,gripSpace.InverseTransformPoint(hand.position)),0.0001f,gun.name);
                    arms.Pose(-1,0,true); Assert.IsFalse(armsRoot.activeSelf);
                    arms.Pose(-1,0,false); Assert.IsTrue(armsRoot.activeSelf);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
    }
}
