using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The story tab of the map screen, opened once the generator is running: the current objective and how far
    /// through it the squad is, the story's steps act by act, the last radio calls this player heard, their own
    /// record, the current jobs, and Doctor Morrow's work once the squad has found the Lantern Core. Everything here reads replicated state, so it's the same story for everyone, seen
    /// from each player's own screen.
    /// </summary>
    public static class StoryScreen
    {
        const string Tick = "✓";
        const string Arrow = "▶";

        public static void Draw(float w, float h, float u, NetPlayer local)
        {
            var rounds = RoundManager.Instance;
            int round = rounds != null ? rounds.DisplayRound : 1;
            int step = StoryObjective.Step;

            float top = 96 * u, gap = 24 * u;
            float lw = Mathf.Min(660 * u, (w - 80 * u) * 0.6f), rw = Mathf.Min(460 * u, w - 80 * u - lw - gap);
            float x = (w - lw - rw - gap) / 2f, ph = h - top - 30 * u;
            DrawMain(new Rect(x, top, lw, ph), u, step);
            DrawSide(new Rect(x + lw + gap, top, rw, ph), u, local, round);
        }

        static void DrawMain(Rect r, float u, int step)
        {
            int act = StoryData.ActOf(step);
            Color radio = StoryData.RadioSpeakerColor(step);
            Ui.PanelBox(r, radio, u, 0.92f);
            float x = r.x + 26 * u, iw = r.width - 52 * u, y = r.y + 20 * u;
            Ui.Label(new Rect(x, y, iw, 22 * u), "MAIN STORY  ·  " + StoryObjective.Header, Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(x, y + 22 * u, iw, 40 * u), StoryData.ActNames[act].ToUpperInvariant(), Ui.Header, radio, TextAnchor.MiddleLeft);
            y += 74 * u;

            // What to do now, and how far through it.
            Ui.Label(new Rect(x, y, iw, 22 * u), "CURRENT OBJECTIVE", Ui.Tiny, Ui.Amber, TextAnchor.MiddleLeft);
            y += 24 * u;
            Ui.Label(new Rect(x, y, iw, 26 * u), StoryObjective.Title, Ui.Bold, Ui.Text, TextAnchor.MiddleLeft);
            y += 28 * u;
            y += Para(new Rect(x, y, iw, 0f), StoryObjective.Text, Ui.Body, Ui.Text) + 8 * u;
            if (!StoryObjective.Complete)
            {
                Ui.Bar(new Rect(x, y + 6 * u, iw * 0.6f, 6 * u), StoryObjective.Fraction, radio);
                Ui.Label(new Rect(x, y - 2 * u, iw, 22 * u), StoryObjective.ProgressText, Ui.Tiny, Ui.Muted, TextAnchor.MiddleRight);
                y += 24 * u;
            }
            y += Para(new Rect(x, y, iw, 0f), StoryData.ActSummaries[act], Ui.Small, Ui.Muted) + 18 * u;

            // The steps, act by act. Steps past the next one stay a mystery.
            Ui.Label(new Rect(x, y, iw, 22 * u), "STEPS", Ui.Tiny, Ui.Amber, TextAnchor.MiddleLeft);
            y += 26 * u;
            for (int a = 0; a < StoryData.StoryActs; a++)
            {
                int first = StoryData.FirstStepOf(a);
                int last = a + 1 < StoryData.StoryActs ? StoryData.FirstStepOf(a + 1) - 1 : StoryData.StepCount - 1;
                if (a > act)
                {
                    Chapter(ref y, x, iw, u, 0, $"Act {a + 1}: {StoryData.ActNames[a]}", $"{last - first + 1} steps");
                    continue;
                }
                Chapter(ref y, x, iw, u, a < act ? 2 : 1, $"Act {a + 1}: {StoryData.ActNames[a]}", "");
                if (a < act) continue;
                for (int s = first; s <= last; s++)
                {
                    int state = s < step ? 2 : s == step ? 1 : 0;
                    Chapter(ref y, x + 22 * u, iw - 22 * u, u, state, s <= step + 1 ? StoryData.StepAt(s).title : "???", state == 1 ? "now" : "");
                }
            }
            y += 14 * u;

            // The last few calls this player heard.
            var heard = StoryDirector.Heard;
            if (heard.Count == 0 || y > r.yMax - 80 * u) return;
            Ui.Label(new Rect(x, y, iw, 22 * u), "RADIO LOG", Ui.Tiny, Ui.Amber, TextAnchor.MiddleLeft);
            y += 24 * u;
            for (int i = heard.Count - 1; i >= 0; i--)
            {
                var (speaker, text, color) = heard[i];
                string line = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{speaker}:</color> {text}";
                float lh = Height(line, Ui.Tiny, iw);
                if (y + lh > r.yMax - 16 * u) break;
                Para(new Rect(x, y, iw, 0f), line, Ui.Tiny, Ui.A(Ui.Text, i == heard.Count - 1 ? 0.95f : 0.65f));
                y += lh + 6 * u;
            }
        }

        /// <summary>A chapter row: state 2 done, 1 now, 0 still to come.</summary>
        static void Chapter(ref float y, float x, float iw, float u, int state, string title, string detail)
        {
            Color c = state == 2 ? Ui.Good : state == 1 ? Ui.Text : Ui.A(Ui.Muted, 0.6f);
            string mark = state == 2 ? Tick : state == 1 ? Arrow : "·";
            Ui.Label(new Rect(x, y, 24 * u, 24 * u), mark, Ui.Small, state == 1 ? Ui.Amber : c, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(x + 26 * u, y, iw - 26 * u, 24 * u), title, Ui.Small, c, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(x, y, iw, 24 * u), state == 2 ? "done" : detail, Ui.Tiny, Ui.A(c, 0.8f), TextAnchor.MiddleRight);
            y += 26 * u;
        }

        static void DrawSide(Rect r, float u, NetPlayer local, int round)
        {
            Ui.PanelBox(r, Ui.Amber, u, 0.92f);
            float x = r.x + 22 * u, iw = r.width - 44 * u, y = r.y + 20 * u;

            // This player's own record.
            Ui.Label(new Rect(x, y, iw, 22 * u), "YOUR RECORD", Ui.Tiny, Ui.Amber, TextAnchor.MiddleLeft);
            y += 24 * u;
            if (local != null)
            {
                Ui.Label(new Rect(x, y, iw, 26 * u), local.TaggedName, Ui.Bold, local.SuitColor, TextAnchor.MiddleLeft);
                y += 28 * u;
                Ui.Label(new Rect(x, y, iw, 22 * u), $"Round {round}   ·   {local.Kills.Value} kills   ·   {local.Headshots.Value} headshots   ·   {local.Points.Value:N0} pts",
                    Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
                y += 34 * u;
            }

            // The Dispatcher's jobs.
            var board = MissionBoard.Instance;
            var data = GameConfig.Missions;
            Ui.Label(new Rect(x, y, iw, 22 * u), "JOBS", Ui.Tiny, Ui.Sky, TextAnchor.MiddleLeft);
            if (board != null && data != null && board.SetRound.Value > 0)
                Ui.Label(new Rect(x, y, iw, 22 * u), $"done jobs refill next round  ·  new set at {data.NextRefreshRound(board.SetRound.Value)}", Ui.Tiny, Ui.Muted, TextAnchor.MiddleRight);
            y += 24 * u;
            bool any = false;
            if (board != null)
                for (int i = 0; i < MissionData.SlotCount; i++)
                {
                    var def = board.DefIn(i);
                    if (def == null) continue;
                    any = true;
                    ulong slot = board.SlotValue(i);
                    bool done = MissionData.DoneOf(slot), failed = MissionData.FailedOf(slot);
                    int target = MissionData.TargetOf(slot), progress = MissionData.ProgressOf(slot);
                    Color c = done ? Ui.Good : failed ? Ui.Bad : Ui.Text;
                    Ui.Label(new Rect(x, y, iw, 22 * u), def.title, Ui.Small, c, TextAnchor.MiddleLeft);
                    Ui.Label(new Rect(x, y, iw, 22 * u), done ? "DONE" : failed ? "FAILED" : $"{progress}/{target}", Ui.Small, c, TextAnchor.MiddleRight);
                    y += 22 * u;
                    y += Para(new Rect(x, y, iw, 0f), def.Describe(target), Ui.Tiny, Ui.Muted) + 8 * u;
                }
            if (!any)
            {
                Ui.Label(new Rect(x, y, iw, 22 * u), "The Dispatcher has no jobs for you yet.", Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
                y += 24 * u;
            }
            y += 14 * u;

            // The map's secret, once the squad has stumbled on it.
            var egg = EasterEgg.Instance;
            bool found = StoryObjective.EggDiscovered;
            Ui.Label(new Rect(x, y, iw, 22 * u), "DOCTOR MORROW'S WORK", Ui.Tiny, EasterEggData.ScientistColor, TextAnchor.MiddleLeft);
            y += 24 * u;
            if (!found)
            {
                Para(new Rect(x, y, iw, 0f), "Undiscovered. Somewhere deeper in the facility, something hums like it's listening.", Ui.Tiny, Ui.Muted);
                return;
            }
            int doneSteps = egg.StepsDone.Value;
            Ui.Label(new Rect(x, y, iw, 22 * u), $"{doneSteps} of {EasterEggData.Steps.Length} steps", Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
            y += 24 * u;
            for (int s = 0; s < EasterEggData.Steps.Length; s++)
            {
                var step = EasterEggData.At(s);
                bool done = s < doneSteps, now = s == doneSteps && egg.Active.Value, next = s == doneSteps && !egg.Active.Value;
                // Steps beyond the next stay a mystery.
                string title = s <= doneSteps ? step.title : "???";
                Color c = done ? Ui.Good : now || next ? Ui.Text : Ui.A(Ui.Muted, 0.6f);
                Ui.Label(new Rect(x, y, 24 * u, 22 * u), done ? Tick : now || next ? Arrow : "·", Ui.Small, now ? EasterEggData.ScientistColor : c, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x + 24 * u, y, iw - 24 * u, 22 * u), title, Ui.Small, c, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x, y, iw, 22 * u), done ? "done" : now ? "now" : next ? "next" : "", Ui.Tiny, Ui.A(c, 0.8f), TextAnchor.MiddleRight);
                y += 24 * u;
                if (next && !string.IsNullOrEmpty(step.hint)) y += Para(new Rect(x + 24 * u, y, iw - 24 * u, 0f), step.hint, Ui.Tiny, Ui.Muted) + 4 * u;
            }
            string carrying = egg.Carrying;
            if (!string.IsNullOrEmpty(carrying)) Ui.Label(new Rect(x, y + 6 * u, iw, 22 * u), "Carrying: " + carrying, Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
        }

        static float Height(string text, GUIStyle style, float width)
        {
            bool wrap = style.wordWrap;
            style.wordWrap = true;
            float h = style.CalcHeight(new GUIContent(text), width);
            style.wordWrap = wrap;
            return h;
        }

        /// <summary>Wrapped text from the top of a rect; returns its height.</summary>
        static float Para(Rect r, string text, GUIStyle style, Color color)
        {
            float h = Height(text, style, r.width);
            bool wrap = style.wordWrap;
            style.wordWrap = true;
            Ui.Label(new Rect(r.x, r.y, r.width, h), text, style, color, TextAnchor.UpperLeft);
            style.wordWrap = wrap;
            return h;
        }
    }
}
