using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Core
{
    /// <summary>Offline recordings; no service credentials or network requests ship with the game.</summary>
    public static class VoiceLibrary
    {
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        public static string SpeakerKey(string speaker)
        {
            if (speaker == StoryData.DispatcherName || speaker == StoryData.DirectorName) return "Vale";
            if (speaker == StoryData.TraderName) return "Trader";
            if (speaker == "Dr. Hale") return "Hale";
            return speaker;
        }

        public static string PathFor(string speaker, string text)
        {
            using var hash = SHA256.Create();
            var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(text));
            var id = new StringBuilder(16);
            for (int i = 0; i < 8; i++) id.Append(bytes[i].ToString("x2"));
            return "Audio/Voice/" + SpeakerKey(speaker) + "/" + id;
        }

        public static AudioClip Get(string speaker, string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var path = PathFor(speaker, text);
            if (!Clips.TryGetValue(path, out var clip)) Clips[path] = clip = Resources.Load<AudioClip>(path);
            return clip;
        }

        public static float Duration(string speaker, string text)
        {
            var clip = Get(speaker, text);
            return clip != null ? clip.length : Mathf.Clamp(1.4f + text.Length * 0.05f, 2f, 4.5f);
        }
    }
}
