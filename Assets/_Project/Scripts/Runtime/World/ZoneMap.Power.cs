using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The start-up power puzzle in the Gate Yard: the generator's control panel beside the start-zone generator,
    /// and the spots where the night shift's notes can lie, each holding one digit of the start-up code. The host
    /// picks three spots every game (MapState); unused spots stay empty. The panel is solid and goes in before
    /// the NavMesh bake; the notes are colliderless.
    /// </summary>
    public partial class ZoneMap
    {
        public const int CodeDigits = 3;
        const float NoteRange = 1.8f;
        const float PanelRange = 1.9f;

        static readonly Color Parchment = new Color(0.86f, 0.79f, 0.62f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.1f);
        static readonly Color WaxRed = new Color(0.55f, 0.08f, 0.06f);
        static readonly Color NoteGlow = new Color(1f, 0.78f, 0.4f);
        static readonly Color PanelIron = new Color(0.2f, 0.2f, 0.22f);
        static readonly Color PanelBronze = new Color(0.55f, 0.38f, 0.17f);

        /// <summary>A place a note can lie: on top of something (flat) or pinned to a wall (facing into the zone).</summary>
        readonly struct NoteSpot
        {
            public readonly Vector3 Position;
            /// <summary>The way the note's face points: up for a note lying flat, into the zone for one on a wall.</summary>
            public readonly Vector3 Facing;
            public readonly float Turn;

            public NoteSpot(Vector3 position, Vector3 facing, float turn)
            {
                Position = position;
                Facing = facing;
                Turn = turn;
            }
        }

        // Built on first use: ZoneAnchors lives in another part of this class, and static initialisers across
        // partial files run in no fixed order.
        static NoteSpot[] s_NoteSpots;
        static NoteSpot[] NoteSpots => s_NoteSpots ??= new[]
        {
            new NoteSpot(L(StartZone, -1.8f, 1.105f, 4.05f), Vector3.up, 20f),        // on the low wall
            new NoteSpot(L(StartZone, -6.2f, 1.205f, -10.1f), Vector3.up, -35f),      // on the crates by the wall gun
            new NoteSpot(L(StartZone, 6.9f, 1.205f, 9.3f), Vector3.up, 70f),          // on the crates by the north door
            new NoteSpot(L(StartZone, -14.2f, 2.205f, 13.6f), Vector3.up, -10f),      // up on the watch platform
            new NoteSpot(L(StartZone, 1.4f, 0.245f, -18.2f), Vector3.up, 110f),       // on the fallen gate
            new NoteSpot(L(StartZone, -15.64f, 1.55f, 5f), Vector3.right, 4f),        // pinned to the west wall
            new NoteSpot(L(StartZone, 4f, 1.55f, 15.64f), Vector3.back, -6f),         // pinned to the north wall
            new NoteSpot(L(StartZone, 15.64f, 1.55f, -10.5f), Vector3.left, 3f),      // pinned to the east wall
            new NoteSpot(L(StartZone, -4.64f, 1.5f, -19.5f), Vector3.right, -5f),     // pinned in the old gate passage
        };

        public static int NoteSpotCount => NoteSpots.Length;
        public static Vector3 NotePosition(int spot) => NoteSpots[spot].Position;

        /// <summary>The control panel beside the start-zone generator, on the floor in front of its screen.</summary>
        public static Vector3 PanelPosition => GeneratorPosition(StartZone) + new Vector3(1.75f, 0f, -0.45f);

        GameObject[] _notes;
        Light[] _noteLights;
        Renderer[] _noteGlows;
        Renderer _panelScreen;
        Light _panelLight;

        /// <summary>The panel and the notes. Before the bake, so the panel's stand is solid for the NavMesh.</summary>
        void BuildPower()
        {
            BuildPanel();
            _notes = new GameObject[NoteSpotCount];
            _noteLights = new Light[NoteSpotCount];
            _noteGlows = new Renderer[NoteSpotCount];
            for (int s = 0; s < NoteSpotCount; s++) BuildNote(s);
        }

        void BuildPanel()
        {
            // A cast-iron lectern with a bronze face: a dark screen that lights up when the generator runs, a
            // ten-key pad and a fat cable running to the generator.
            var panel = Greybox.Empty("Generator Panel", Root, PanelPosition);
            Greybox.Box("Stand", panel, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 1.1f, 0.42f), PanelIron, surface: Surface.Metal);
            var head = Greybox.Box("Head", panel, new Vector3(0f, 1.25f, 0.02f), new Vector3(0.8f, 0.5f, 0.5f), PanelBronze, collider: false, surface: Surface.Metal);
            head.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            var face = Greybox.Empty("Face", panel, new Vector3(0f, 1.27f, 0.25f));
            face.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            _panelScreen = Greybox.Prim(PrimitiveType.Cube, "Screen", face, new Vector3(0f, 0.12f, 0.01f), new Vector3(0.5f, 0.13f, 0.02f), Door.NoPowerColor, collider: false, unlit: true)
                .GetComponent<Renderer>();
            for (int key = 0; key < 12; key++)
            {
                int row = key / 3, col = key % 3;
                Greybox.Box("Key", face, new Vector3(-0.1f + col * 0.1f, -0.02f - row * 0.06f, 0.015f), new Vector3(0.075f, 0.045f, 0.03f), new Color(0.75f, 0.72f, 0.62f),
                    collider: false, surface: Surface.Metal);
            }
            Greybox.Box("Cable", panel, new Vector3(-0.85f, 0.04f, 0.1f), new Vector3(1.2f, 0.08f, 0.1f), new Color(0.08f, 0.08f, 0.08f), collider: false, surface: Surface.Rubber);
            var light = new GameObject("Panel Light").AddComponent<Light>();
            light.transform.SetParent(panel, false);
            light.transform.localPosition = new Vector3(0f, 1.6f, 0.6f);
            light.type = LightType.Point;
            light.range = 3.5f;
            light.intensity = 1.2f;
            light.color = Door.NoPowerColor;
            light.shadows = LightShadows.None;
            _panelLight = light;
        }

        void BuildNote(int s)
        {
            var spot = NoteSpots[s];
            var root = Greybox.Empty("Note " + s, Root, spot.Position);
            // The note's local +y is its face and +z its top edge (up the wall when pinned); a turn about its face
            // keeps it from looking placed by a ruler.
            bool pinned = Mathf.Abs(spot.Facing.y) < 0.5f;
            root.localRotation = (pinned ? Quaternion.LookRotation(Vector3.up, spot.Facing) : Quaternion.identity) * Quaternion.Euler(0f, spot.Turn, 0f);
            Greybox.Box("Paper", root, new Vector3(0f, 0.002f, 0f), new Vector3(0.22f, 0.004f, 0.29f), Parchment, collider: false, surface: Surface.Fabric);
            for (int line = 0; line < 6; line++)
            {
                float len = line == 5 ? 0.08f : 0.16f - (line % 2) * 0.03f;
                Greybox.Box("Ink", root, new Vector3(-0.07f + len / 2f, 0.0045f, 0.1f - line * 0.035f), new Vector3(len, 0.001f, 0.008f), Ink, collider: false);
            }
            // The digit itself, big and circled, and a wax seal (or a nail through it on a wall).
            Greybox.Box("Numeral", root, new Vector3(0.05f, 0.0045f, -0.09f), new Vector3(0.05f, 0.001f, 0.07f), Ink, collider: false);
            if (pinned) Greybox.Prim(PrimitiveType.Cylinder, "Nail", root, new Vector3(0f, 0.01f, 0.12f), new Vector3(0.025f, 0.01f, 0.025f), new Color(0.3f, 0.3f, 0.3f), collider: false, surface: Surface.Metal);
            else Greybox.Prim(PrimitiveType.Cylinder, "Seal", root, new Vector3(-0.06f, 0.006f, -0.1f), new Vector3(0.045f, 0.003f, 0.045f), WaxRed, collider: false);
            // A faint halo so a searching squad can spot it, gone once it's been read.
            var glow = Greybox.Prim(PrimitiveType.Cube, "Glow", root, new Vector3(0f, -0.001f, 0f), new Vector3(0.3f, 0.001f, 0.37f), new Color(NoteGlow.r, NoteGlow.g, NoteGlow.b, 0.35f),
                collider: false, unlit: true);
            _noteGlows[s] = glow.GetComponent<Renderer>();
            var light = new GameObject("Note Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            light.type = LightType.Point;
            light.range = 1.8f;
            light.intensity = 0.9f;
            light.color = NoteGlow;
            light.shadows = LightShadows.None;
            _noteLights[s] = light;
            Greybox.DisableShadows(root.gameObject);
            _notes[s] = root.gameObject;
        }

        void AddPowerBuyables(List<Buyable> buyables)
        {
            buyables.Add(new Buyable { Kind = BuyableKind.PowerPanel, Index = 0, Position = PanelPosition + new Vector3(0f, 1f, 1.1f), Range = PanelRange });
            for (int s = 0; s < NoteSpotCount; s++)
            {
                // Stand in front of a pinned note; reach a lying one from anywhere round it (the platform's from its deck).
                var spot = NoteSpots[s];
                var floor = new Vector3(spot.Position.x, spot.Position.y > 2f ? spot.Position.y : 0f, spot.Position.z);
                if (Mathf.Abs(spot.Facing.y) < 0.5f) floor += spot.Facing * 0.8f;
                buyables.Add(new Buyable { Kind = BuyableKind.Note, Index = s, Position = floor + Vector3.up, Range = NoteRange });
            }
        }

        /// <summary>Every machine: which spots have a note, and which of those have been read.</summary>
        public void ShowNote(int spot, bool present, bool read)
        {
            if (_notes == null || spot < 0 || spot >= _notes.Length) return;
            _notes[spot].SetActive(present);
            _noteLights[spot].enabled = present && !read;
            _noteGlows[spot].enabled = present && !read;
        }

        /// <summary>Every machine: the generator running (or not), with the panel's screen and lamp to match.</summary>
        public void ShowPower(bool powered, bool instant)
        {
            if (Generators != null && StartZone < Generators.Length) Generators[StartZone].SetMains(powered, instant);
            var color = powered ? Door.OpenColor : Door.NoPowerColor;
            if (_panelScreen != null) _panelScreen.sharedMaterial = Greybox.Unlit(color);
            if (_panelLight != null) _panelLight.color = color;
            if (powered && !instant && Generators != null)
            {
                var at = Generators[StartZone].transform.position + Vector3.up;
                AudioSource.PlayClipAtPoint(ProceduralAudio.PowerOn, at, GameSettings.EffectsVolume);
            }
        }
    }
}
