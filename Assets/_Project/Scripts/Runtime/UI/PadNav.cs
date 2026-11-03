using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// Controller navigation for the IMGUI menus. Ui's buttons, toggles and sliders register themselves here as
    /// they repaint; the d-pad or left stick moves a focus between them by screen position, A presses the focused
    /// one, B presses the screen's back button, and left and right nudge a focused slider. Focus is remembered by
    /// rectangle, so it survives screens being redrawn and jumps to a sensible default when a new screen opens.
    /// </summary>
    public static class PadNav
    {
        public enum Kind { Button, Toggle, Slider }

        public struct Item
        {
            public Rect rect;
            public Kind kind;
            public bool preferred;
        }

        static readonly List<Item> s_Now = new List<Item>(), s_Last = new List<Item>();
        static int s_Frame = -1;
        static Rect s_Focus;
        static bool s_HasFocus;
        static bool s_Submit, s_Cancel, s_SubmitUsed, s_CancelUsed;
        static int s_Adjust;
        static int s_FocusedIndex = -1;
        static bool s_WasActive;

        /// <summary>The focus is shown and acts only while the player is on a controller.</summary>
        static bool Active => GameInput.UsingGamepad && !GameInput.Suspended;

        /// <summary>Repaint is the one IMGUI pass a frame that every control goes through in order, so all the work happens there.</summary>
        static bool Repainting => Event.current != null && Event.current.type == EventType.Repaint;

        static void BeginFrame()
        {
            if (s_Frame == Time.frameCount) return;
            // After frames with no menu on screen, last time's controls are stale: start this screen afresh.
            if (Time.frameCount - s_Frame > 1) s_Now.Clear();
            s_Frame = Time.frameCount;
            s_Last.Clear();
            s_Last.AddRange(s_Now);
            s_Now.Clear();
            s_FocusedIndex = -1;
            s_Submit = s_Cancel = s_SubmitUsed = s_CancelUsed = false;
            s_Adjust = 0;
            if (s_Last.Count == 0)
            {
                s_HasFocus = false;
                return;
            }

            // Find last frame's focus again; if nothing is where it was, a new screen opened.
            int at = s_HasFocus ? Nearest(s_Last, s_Focus, 6f) : -1;
            if (at < 0) at = DefaultIndex(s_Last);
            // The press that picks the pad back up only shows where the focus is.
            bool woke = Active && !s_WasActive;
            s_WasActive = Active;
            if (Active && !woke)
            {
                Vector2Int step = GameInput.NavStep;
                var item = s_Last[at];
                if (item.kind == Kind.Slider && step.x != 0) s_Adjust = step.x;
                else if (step != Vector2Int.zero)
                {
                    // Screen y grows downwards, the stick's upwards.
                    int next = Pick(s_Last, at, new Vector2(step.x, -step.y));
                    if (next >= 0) at = next;
                }
                s_Submit = GameInput.SubmitPressed;
                s_Cancel = GameInput.CancelPressed;
            }
            s_Focus = s_Last[at].rect;
            s_HasFocus = true;
        }

        /// <summary>
        /// Adds a control drawn at a GUI rect (inside groups and scroll views too). Returns true if it has the focus.
        /// Call it after drawing the control so the focus ring goes on top.
        /// </summary>
        public static bool Register(Rect guiRect, Kind kind, bool preferred = false)
        {
            if (!Repainting || !GUI.enabled) return false;
            BeginFrame();
            var screen = ToScreen(guiRect);
            s_Now.Add(new Item { rect = screen, kind = kind, preferred = preferred });
            bool focused = s_HasFocus && Same(screen, s_Focus);
            if (!focused) return false;
            s_FocusedIndex = s_Now.Count - 1;
            if (Active) DrawRing(guiRect);
            return Active;
        }

        /// <summary>A focused button or toggle was pressed with A this frame.</summary>
        public static bool TakeSubmit()
        {
            if (!s_Submit || s_SubmitUsed) return false;
            s_SubmitUsed = true;
            return true;
        }

        /// <summary>B was pressed this frame and no other back button has taken it.</summary>
        public static bool TakeCancel()
        {
            if (!Repainting || !GUI.enabled) return false;
            BeginFrame();
            if (!s_Cancel || s_CancelUsed) return false;
            s_CancelUsed = true;
            return true;
        }

        /// <summary>Left or right on a focused slider: -1, 0 or +1.</summary>
        public static int Adjust => s_Adjust;

        /// <summary>How many controls have registered so far this frame; pass to KeepVisible to mark where a scroll view starts.</summary>
        public static int Mark
        {
            get
            {
                if (Repainting) BeginFrame();
                return s_Now.Count;
            }
        }

        /// <summary>
        /// Scrolls a scroll view so the focus stays in sight. Call after EndScrollView with the view's rect (in the
        /// same coordinates as BeginScrollView) and the Mark taken just before it.
        /// </summary>
        public static Vector2 KeepVisible(Rect view, Vector2 scroll, int mark)
        {
            if (!Repainting || !Active || s_FocusedIndex < mark || s_FocusedIndex >= s_Now.Count) return scroll;
            var v = ToScreen(view);
            var f = s_Now[s_FocusedIndex].rect;
            float margin = 8f, delta = 0f;
            if (f.yMax > v.yMax - margin) delta = f.yMax - (v.yMax - margin);
            else if (f.yMin < v.yMin + margin) delta = f.yMin - (v.yMin + margin);
            if (Mathf.Abs(delta) < 0.5f) return scroll;
            float before = scroll.y;
            scroll.y = Mathf.Max(0f, scroll.y + delta);
            // The control moves by however far the view really scrolled; follow it so the focus isn't lost.
            s_Focus.y -= scroll.y - before;
            return scroll;
        }

        static Rect ToScreen(Rect r)
        {
            Vector2 p = GUIUtility.GUIToScreenPoint(r.position);
            return new Rect(p, r.size);
        }

        static bool Same(Rect a, Rect b) =>
            Mathf.Abs(a.x - b.x) < 1f && Mathf.Abs(a.y - b.y) < 1f && Mathf.Abs(a.width - b.width) < 1f && Mathf.Abs(a.height - b.height) < 1f;

        /// <summary>The item closest to a rect (by corner and size), if one is within tolerance pixels; else -1.</summary>
        public static int Nearest(IReadOnlyList<Item> items, Rect r, float tolerance)
        {
            int best = -1;
            float bestD = tolerance;
            for (int i = 0; i < items.Count; i++)
            {
                var o = items[i].rect;
                float d = Mathf.Abs(o.x - r.x) + Mathf.Abs(o.y - r.y) + Mathf.Abs(o.width - r.width) + Mathf.Abs(o.height - r.height);
                if (d <= bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Where focus starts on a new screen: a control asked for it, or else the first one.</summary>
        public static int DefaultIndex(IReadOnlyList<Item> items)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].preferred) return i;
            return 0;
        }

        /// <summary>
        /// The next control from one item in a screen direction (x right, y down): the closest one that lies that
        /// way, where sideways distance counts double and controls that line up with this one count as straight on.
        /// Returns -1 if there is none.
        /// </summary>
        public static int Pick(IReadOnlyList<Item> items, int from, Vector2 dir)
        {
            var a = items[from].rect;
            bool horizontal = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                if (i == from) continue;
                var b = items[i].rect;
                float along, across;
                if (horizontal)
                {
                    along = dir.x > 0 ? b.xMin - a.xMax : a.xMin - b.xMax;
                    // Overlapping side by side still counts if its centre is that way.
                    if (along < 0f && (b.center.x - a.center.x) * dir.x > 1f) along = 0f;
                    across = Gap(a.yMin, a.yMax, b.yMin, b.yMax);
                }
                else
                {
                    along = dir.y > 0 ? b.yMin - a.yMax : a.yMin - b.yMax;
                    if (along < 0f && (b.center.y - a.center.y) * dir.y > 1f) along = 0f;
                    across = Gap(a.xMin, a.xMax, b.xMin, b.xMax);
                }
                if (along < 0f) continue;
                // Among equally near rows, prefer the one most in line with where we are.
                float centre = horizontal ? Mathf.Abs(b.center.y - a.center.y) : Mathf.Abs(b.center.x - a.center.x);
                float score = along + across * 2f + centre * 0.01f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        static float Gap(float a0, float a1, float b0, float b1) => b0 > a1 ? b0 - a1 : a0 > b1 ? a0 - b1 : 0f;

        static void DrawRing(Rect r)
        {
            float t = 2f, pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
            var c = Ui.A(Ui.Amber, pulse);
            var o = new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f);
            Ui.Fill(new Rect(o.x, o.y, o.width, t), c);
            Ui.Fill(new Rect(o.x, o.yMax - t, o.width, t), c);
            Ui.Fill(new Rect(o.x, o.y, t, o.height), c);
            Ui.Fill(new Rect(o.xMax - t, o.y, t, o.height), c);
        }
    }
}
