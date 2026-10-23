using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>One of the four playable survivors. Each has a voice set with its own personality (VoiceLines).</summary>
    public class Character
    {
        public readonly string name;
        public readonly string role;
        public readonly string blurb;
        /// <summary>Pitch of the placeholder voice blips.</summary>
        public readonly float voicePitch;
        public readonly Color color;

        public Character(string name, string role, string blurb, float voicePitch, Color color)
        {
            this.name = name;
            this.role = role;
            this.blurb = blurb;
            this.voicePitch = voicePitch;
            this.color = color;
        }
    }

    public static class Characters
    {
        public static readonly Character[] All =
        {
            new Character("Marlow", "Ex-cop", "Gruff, dry and tired of everything. Keeps the squad moving.", 0.8f, new Color(0.45f, 0.6f, 0.9f)),
            new Character("Dee", "Mechanic", "Sarcastic, fearless, and talks to machines like old friends.", 1.08f, new Color(0.95f, 0.55f, 0.3f)),
            new Character("Dr. Hale", "Facility scientist", "Nervous, clever, and knows more about this place than he says.", 0.95f, new Color(0.5f, 0.85f, 0.55f)),
            new Character("Rook", "Courier", "Young, cocky and always joking, until he isn't.", 1.25f, new Color(0.95f, 0.85f, 0.35f)),
        };

        public static int Count => All.Length;
        public static Character At(int id) => All[Mathf.Clamp(id, 0, All.Length - 1)];

        const string PrefKey = "ZombiesFPS.Character";
        public static int LoadPick() => Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, All.Length - 1);
        public static void SavePick(int id) => PlayerPrefs.SetInt(PrefKey, Mathf.Clamp(id, 0, All.Length - 1));
    }
}
