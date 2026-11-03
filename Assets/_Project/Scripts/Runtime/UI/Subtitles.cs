using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// Lines of dialogue on screen: the Dispatcher on the radio, the Trader, and the players' own voice lines.
    /// The HUD draws the newest few while Subtitles are on in Settings; the radio box shows either way.
    /// </summary>
    public static class Subtitles
    {
        public struct Line
        {
            public string speaker;
            public string text;
            public Color color;
            public float start;
            public float duration;
            public bool radio;

            public float Age => Time.time - start;
            public bool Expired => Age > duration;
            public float Alpha => Mathf.Clamp01(Age / 0.2f) * Mathf.Clamp01((duration - Age) / 0.5f);
        }

        const int MaxLines = 3;
        static readonly List<Line> s_Lines = new List<Line>();

        public static IReadOnlyList<Line> Lines
        {
            get
            {
                s_Lines.RemoveAll(l => l.Expired);
                return s_Lines;
            }
        }

        /// <summary>Shows a line. Duration defaults to reading time.</summary>
        public static void Show(string speaker, string text, Color color, bool radio = false, float duration = -1f)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (duration <= 0f) duration = Mathf.Clamp(1.6f + text.Length * 0.055f, 2.5f, 8f);
            // The same speaker saying something new replaces their old line.
            s_Lines.RemoveAll(l => l.speaker == speaker);
            s_Lines.Add(new Line { speaker = speaker, text = text, color = color, start = Time.time, duration = duration, radio = radio });
            while (s_Lines.Count > MaxLines) s_Lines.RemoveAt(0);
        }

        public static void Clear() => s_Lines.Clear();
    }
}
