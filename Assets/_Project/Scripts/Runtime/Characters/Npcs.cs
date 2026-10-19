using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.CharacterModels
{
    /// <summary>
    /// Character models for the story's people. Sergeant Brannock is the Ranger outfit in Lantern Watch blue, plugged
    /// into the easter egg through <see cref="EggProps.WatchmanModel"/>; <see cref="NpcGait"/> walks and runs him from
    /// however the egg moves him.
    /// </summary>
    public static class Npcs
    {
        /// <summary>Watch-blue colour variant of the Ranger (see tools/characters/pack_characters.py).</summary>
        const int WatchBlue = 2;

        static GameObject s_Holder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            if (EggProps.WatchmanModel != null || !CharacterSkin.Exists(CharacterSkin.Ranger)) return;
            // The egg Instantiates the model; the original waits here, switched off, so it never animates or draws.
            s_Holder = new GameObject("Character Models");
            s_Holder.SetActive(false);
            Object.DontDestroyOnLoad(s_Holder);
            EggProps.WatchmanModel = Brannock(s_Holder.transform);
        }

        /// <summary>Sergeant Brannock: hooded Watch coat, an old soldier's face and a white beard. Feet at the origin, facing +Z.</summary>
        public static GameObject Brannock(Transform parent)
        {
            var skin = CharacterSkin.Create(CharacterSkin.Ranger, parent, WatchBlue);
            if (skin == null) return null;
            skin.name = "Sergeant Brannock";
            skin.transform.localScale = Vector3.one * 0.97f;
            AddFace(skin.Head, skin.transform);
            skin.gameObject.AddComponent<NpcGait>();
            return skin.gameObject;
        }

        /// <summary>
        /// The outfit pack has no heads (they come with Quaternius' base characters), so a simple face fills the hood:
        /// weathered skin, dark eyes and a short white beard, riding the head bone.
        /// </summary>
        static void AddFace(Transform head, Transform root)
        {
            var face = Greybox.Empty("Face", head, Vector3.zero);
            // Placed in the character's own space (metres, facing +Z), then carried by the head bone.
            face.position = root.TransformPoint(new Vector3(0f, 1.69f, -0.035f));
            face.rotation = root.rotation;
            var skin = new Color(0.72f, 0.55f, 0.44f);
            var beard = new Color(0.8f, 0.79f, 0.75f);
            var dark = new Color(0.1f, 0.08f, 0.07f);
            Part(face, PrimitiveType.Sphere, "Skull", Vector3.zero, new Vector3(0.17f, 0.23f, 0.2f), skin, Surface.Skin);
            Part(face, PrimitiveType.Sphere, "Nose", new Vector3(0f, -0.005f, 0.1f), new Vector3(0.03f, 0.05f, 0.04f), skin * 0.92f, Surface.Skin);
            Part(face, PrimitiveType.Sphere, "Beard", new Vector3(0f, -0.075f, 0.065f), new Vector3(0.14f, 0.1f, 0.09f), beard, Surface.Fabric);
            Part(face, PrimitiveType.Sphere, "Moustache", new Vector3(0f, -0.032f, 0.095f), new Vector3(0.09f, 0.025f, 0.03f), beard, Surface.Fabric);
            foreach (int s in new[] { -1, 1 })
            {
                Part(face, PrimitiveType.Sphere, "Eye", new Vector3(0.035f * s, 0.015f, 0.093f), new Vector3(0.025f, 0.016f, 0.012f), dark, Surface.Plain);
                Part(face, PrimitiveType.Cube, "Brow", new Vector3(0.036f * s, 0.035f, 0.094f), new Vector3(0.04f, 0.01f, 0.012f), beard * 0.85f, Surface.Plain);
            }
        }

        static void Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 size, Color color, Surface surface)
        {
            var go = Greybox.Prim(type, name, parent, pos, size, color, collider: false, surface: surface, moving: true);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
