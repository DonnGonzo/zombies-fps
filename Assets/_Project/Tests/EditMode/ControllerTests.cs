using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Player;
using ZombiesFPS.UI;

namespace ZombiesFPS.Tests
{
    public class ControllerTests
    {
        [Test]
        public void DefaultPadLayout_CoversEveryAction_WithNoButtonUsedTwice()
        {
            var layout = PadBindings.DefaultLayout();
            Assert.AreEqual(Bindings.Count, layout.Length);
            var seen = new HashSet<PadButton>();
            for (int i = 0; i < layout.Length; i++)
            {
                var action = (GameAction)i;
                if (PadBindings.IsFixed(action))
                {
                    Assert.AreEqual(PadButton.None, layout[i], action.ToString());
                    continue;
                }
                if (layout[i] == PadButton.None) continue;
                Assert.IsTrue(PadBindings.IsCapturable(layout[i]), action.ToString());
                Assert.IsTrue(seen.Add(layout[i]), $"{layout[i]} is used twice");
            }
            Assert.AreEqual(PadButton.RightTrigger, layout[(int)GameAction.Fire]);
            Assert.AreEqual(PadButton.South, layout[(int)GameAction.Jump]);
            Assert.IsFalse(PadBindings.IsCapturable(PadButton.Start), "Start is kept for the menu");
        }

        [Test]
        public void PadRebind_SwapsWithTheActionThatHadTheButton()
        {
            var layout = PadBindings.DefaultLayout();
            PadButton jump = layout[(int)GameAction.Jump], reload = layout[(int)GameAction.Reload];
            PadBindings.Assign(layout, GameAction.Jump, reload);
            Assert.AreEqual(reload, layout[(int)GameAction.Jump]);
            Assert.AreEqual(jump, layout[(int)GameAction.Reload]);

            // Binding an unbound action takes the button away from its old owner, leaving that one unbound.
            PadBindings.Assign(layout, GameAction.Weapon1, layout[(int)GameAction.Map]);
            Assert.AreEqual(PadButton.None, layout[(int)GameAction.Map]);
            Assert.AreEqual(PadButton.Select, layout[(int)GameAction.Weapon1]);
        }

        [Test]
        public void StickShaping_HasADeadZone_ReachesFullTilt_AndKeepsDirection()
        {
            Assert.AreEqual(Vector2.zero, GameInput.ShapeStick(new Vector2(0.08f, 0.05f), 0.12f, 1.6f));
            var full = GameInput.ShapeStick(new Vector2(0f, 0.97f), 0.12f, 1.6f);
            Assert.AreEqual(1f, full.magnitude, 1e-4f);
            var diag = GameInput.ShapeStick(new Vector2(0.4f, 0.4f), 0.12f, 1f);
            Assert.AreEqual(diag.x, diag.y, 1e-5f);
            // A curve above 1 is gentler near the middle and the same at the edge.
            float linear = GameInput.ShapeStick(new Vector2(0.5f, 0f), 0.1f, 1f).x;
            float curved = GameInput.ShapeStick(new Vector2(0.5f, 0f), 0.1f, 2f).x;
            Assert.Less(curved, linear);
            float prev = 0f;
            for (float x = 0.1f; x <= 1f; x += 0.05f)
            {
                float v = GameInput.ShapeStick(new Vector2(x, 0f), 0.1f, 1.6f).x;
                Assert.GreaterOrEqual(v, prev);
                prev = v;
            }
        }

        [Test]
        public void PadNames_FollowThePadMaker()
        {
            Assert.AreEqual("A", GameInput.PadName(PadButton.South, PadGlyphs.Xbox));
            Assert.AreEqual("Cross", GameInput.PadName(PadButton.South, PadGlyphs.PlayStation));
            Assert.AreEqual("B", GameInput.PadName(PadButton.South, PadGlyphs.Nintendo));
            Assert.AreEqual("RT", GameInput.PadName(PadButton.RightTrigger, PadGlyphs.Xbox));
            Assert.AreEqual("R2", GameInput.PadName(PadButton.RightTrigger, PadGlyphs.PlayStation));
            Assert.AreEqual("ZR", GameInput.PadName(PadButton.RightTrigger, PadGlyphs.Nintendo));
            Assert.AreEqual("View", GameInput.PadName(PadButton.Select, PadGlyphs.Xbox));
            for (var b = PadButton.South; b <= PadButton.DpadRight; b++)
                for (var g = PadGlyphs.Auto; g <= PadGlyphs.Nintendo; g++)
                    Assert.IsFalse(string.IsNullOrEmpty(GameInput.PadName(b, g)), $"{b} {g}");
        }

        static PadNav.Item At(float x, float y, float w = 100f, float h = 40f) => new PadNav.Item { rect = new Rect(x, y, w, h) };

        [Test]
        public void MenuFocus_MovesToTheNearestControlThatWay()
        {
            // Two columns of three, plus a wide button under both.
            var items = new List<PadNav.Item>
            {
                At(0, 0), At(120, 0),
                At(0, 50), At(120, 50),
                At(0, 100), At(120, 100),
                At(0, 160, 220f),
            };
            Assert.AreEqual(2, PadNav.Pick(items, 0, new Vector2(0f, 1f)));
            Assert.AreEqual(1, PadNav.Pick(items, 0, Vector2.right));
            Assert.AreEqual(3, PadNav.Pick(items, 1, new Vector2(0f, 1f)));
            Assert.AreEqual(6, PadNav.Pick(items, 5, new Vector2(0f, 1f)));
            Assert.AreEqual(-1, PadNav.Pick(items, 0, Vector2.left));
            Assert.AreEqual(-1, PadNav.Pick(items, 0, new Vector2(0f, -1f)));
            Assert.AreEqual(4, PadNav.Pick(items, 6, new Vector2(0f, -1f)));
        }

        [Test]
        public void MenuFocus_FindsItsControlAgain_OrStartsOnThePreferredOne()
        {
            var items = new List<PadNav.Item> { At(0, 0), At(0, 50), At(0, 100) };
            Assert.AreEqual(1, PadNav.Nearest(items, new Rect(0.5f, 50f, 100f, 40f), 6f));
            Assert.AreEqual(-1, PadNav.Nearest(items, new Rect(300f, 300f, 100f, 40f), 6f));
            Assert.AreEqual(0, PadNav.DefaultIndex(items));
            var preferred = items[2];
            preferred.preferred = true;
            items[2] = preferred;
            Assert.AreEqual(2, PadNav.DefaultIndex(items));
        }

        [Test]
        public void AimAssist_OffsetIsInLookDegrees_AndFadesToTheConeEdge()
        {
            var eye = new GameObject("Eye").transform;
            try
            {
                var off = AimAssist.Offset(eye, new Vector3(1f, 0f, 1f));
                Assert.AreEqual(45f, off.x, 0.01f);
                Assert.AreEqual(0f, off.y, 0.01f);
                off = AimAssist.Offset(eye, new Vector3(0f, 1f, 1f));
                Assert.AreEqual(45f, off.y, 0.01f);
            }
            finally { Object.DestroyImmediate(eye.gameObject); }
            Assert.AreEqual(1f, AimAssist.Closeness(0f));
            Assert.AreEqual(1f, AimAssist.Closeness(0.3f));
            Assert.AreEqual(0f, AimAssist.Closeness(1f));
            Assert.Greater(AimAssist.Closeness(0.6f), AimAssist.Closeness(0.8f));
        }
    }
}
