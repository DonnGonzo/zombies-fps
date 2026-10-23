using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Data;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Tests
{
    public class ZombieVoiceTests
    {
        [Test]
        public void RecordedVoicesAreLoadedAsShortMonoClips()
        {
            Assert.GreaterOrEqual(ZombieVoice.Count(ZombieVoice.Call.Groan), 12);
            Assert.GreaterOrEqual(ZombieVoice.Count(ZombieVoice.Call.Attack), 8);
            Assert.GreaterOrEqual(ZombieVoice.Count(ZombieVoice.Call.Death), 4);
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio/Zombie"))
            {
                Assert.AreEqual(1, clip.channels, clip.name);
                Assert.That(clip.length, Is.InRange(0.15f, 8f), clip.name);
            }
        }

        [Test]
        public void PickNeverRepeatsTheLastClip()
        {
            for (int roll = 0; roll < 200; roll++)
            {
                int last = roll % 7;
                int i = ZombieVoice.PickIndex(7, last, roll * 31);
                Assert.That(i, Is.InRange(0, 6));
                Assert.AreNotEqual(last, i);
            }
            Assert.AreEqual(0, ZombieVoice.PickIndex(1, 0, 5));
        }

        [Test]
        public void BudgetCapsGroansAndSpacesThemOut()
        {
            var budget = new ZombieVoice.VoiceBudget();
            Assert.IsTrue(budget.TryStart(10f, 3f, true));
            Assert.IsFalse(budget.TryStart(10.1f, 3f, true), "groans start at least GroanSpacing apart");
            Assert.IsTrue(budget.TryStart(11f, 3f, true));
            Assert.IsTrue(budget.TryStart(12f, 3f, true));
            Assert.IsFalse(budget.TryStart(12.8f, 3f, true), "no more than MaxGroans at once");
            Assert.IsTrue(budget.TryStart(12.8f, 1f, false), "a snarl or death still gets through");
            Assert.IsTrue(budget.TryStart(13.5f, 3f, true), "the first groan has ended");
        }

        [Test]
        public void BudgetCapsAllVoices()
        {
            var budget = new ZombieVoice.VoiceBudget();
            for (int i = 0; i < ZombieVoice.MaxVoices; i++) Assert.IsTrue(budget.TryStart(1f, 2f, false));
            Assert.IsFalse(budget.TryStart(1.5f, 1f, false));
            Assert.IsTrue(budget.TryStart(3.1f, 1f, false));
        }

        [Test]
        public void BigZombiesSoundLowerAndEachHasItsOwnVoice()
        {
            Assert.Less(ZombieVoice.BasePitch(ZombieKind.Brute, 5), ZombieVoice.BasePitch(ZombieKind.Runner, 5));
            Assert.Less(ZombieVoice.BasePitch(ZombieKind.Boss, 9), ZombieVoice.BasePitch(ZombieKind.Walker, 9));
            Assert.AreNotEqual(ZombieVoice.BasePitch(ZombieKind.Walker, 1), ZombieVoice.BasePitch(ZombieKind.Walker, 2));
            for (uint s = 0; s < 50; s++) Assert.That(ZombieVoice.BasePitch(ZombieKind.Walker, s), Is.InRange(0.9f, 1.08f));
        }

        [Test]
        public void FarZombiesGroanLessOften()
        {
            float near = 0f, far = 0f;
            for (int i = 0; i < 200; i++)
            {
                near += ZombieVoice.NextGroanDelay(3f, false);
                far += ZombieVoice.NextGroanDelay(30f, false);
            }
            Assert.Greater(far, near * 1.8f);
            Assert.Greater(near / 200f, 3.5f, "even a close zombie doesn't groan back to back");
        }
    }
}
