using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using ZombiesFPS.Core;

namespace ZombiesFPS.Characters
{
    /// <summary>The humanoid bones a pose is copied through (see <see cref="CharacterSkin.Retarget"/>).</summary>
    public enum Part
    {
        Hips, Spine, Chest, UpperChest, Neck, Head,
        LShoulder, LUpperArm, LLowerArm, LHand, RShoulder, RUpperArm, RLowerArm, RHand,
        LUpperLeg, LLowerLeg, LFoot, LToes, RUpperLeg, RLowerLeg, RFoot, RToes,
        LThumb1, LThumb2, LThumb3, LIndex1, LIndex2, LIndex3, LMiddle1, LMiddle2, LMiddle3, LRing1, LRing2, LRing3, LPinky1, LPinky2, LPinky3,
        RThumb1, RThumb2, RThumb3, RIndex1, RIndex2, RIndex3, RMiddle1, RMiddle2, RMiddle3, RRing1, RRing2, RRing3, RPinky1, RPinky2, RPinky3,
    }

    /// <summary>
    /// A skinned character from one of Ivan's packs (Resources/Characters/Name, made by tools/characters/pack_characters.py):
    /// a plain binary skeleton and mesh, so it needs no model import settings and looks the same on every machine.
    /// The rest pose is a T-pose facing +Z with its feet at the origin, in metres.
    ///
    /// Poses come from elsewhere: <see cref="Retarget"/> copies a Mixamo rig's pose (the zombie clips) onto it, and
    /// <see cref="NpcGait"/> walks it procedurally. Both work in "model space": each bone turns from its T-pose by the
    /// same rotation the source bone turned from its own T-pose, so any two T-posed humanoids can share animation.
    /// Everything it needs is in serialized fields, so an Instantiate'd copy works too.
    /// </summary>
    public sealed class CharacterSkin : MonoBehaviour
    {
        public const string Folder = "Characters/";
        public const string Imp = "Imp", Puglin = "Puglin", Ranger = "Ranger";

        static readonly int PartCount = System.Enum.GetValues(typeof(Part)).Length;

        // Bone names per part, for the Quaternius (Unreal-style) skeleton and for Mixamo (after its "prefix:").
        static readonly string[] UnrealNames =
        {
            "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "Head",
            "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l", "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
            "thigh_l", "calf_l", "foot_l", "ball_l", "thigh_r", "calf_r", "foot_r", "ball_r",
            "thumb_01_l", "thumb_02_l", "thumb_03_l", "index_01_l", "index_02_l", "index_03_l", "middle_01_l", "middle_02_l", "middle_03_l",
            "ring_01_l", "ring_02_l", "ring_03_l", "pinky_01_l", "pinky_02_l", "pinky_03_l",
            "thumb_01_r", "thumb_02_r", "thumb_03_r", "index_01_r", "index_02_r", "index_03_r", "middle_01_r", "middle_02_r", "middle_03_r",
            "ring_01_r", "ring_02_r", "ring_03_r", "pinky_01_r", "pinky_02_r", "pinky_03_r",
        };
        public static readonly string[] MixamoNames =
        {
            "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
            "LeftHandThumb1", "LeftHandThumb2", "LeftHandThumb3", "LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3",
            "LeftHandMiddle1", "LeftHandMiddle2", "LeftHandMiddle3", "LeftHandRing1", "LeftHandRing2", "LeftHandRing3",
            "LeftHandPinky1", "LeftHandPinky2", "LeftHandPinky3",
            "RightHandThumb1", "RightHandThumb2", "RightHandThumb3", "RightHandIndex1", "RightHandIndex2", "RightHandIndex3",
            "RightHandMiddle1", "RightHandMiddle2", "RightHandMiddle3", "RightHandRing1", "RightHandRing2", "RightHandRing3",
            "RightHandPinky1", "RightHandPinky2", "RightHandPinky3",
        };

        sealed class Data
        {
            public string[] Names;
            public int[] Parents;
            public Vector3[] Pos, Scale;
            public Quaternion[] Rot;
            public Mesh Mesh;
            public string[] Keys;
            public float Height;
        }

        static readonly Dictionary<string, Data> s_Data = new Dictionary<string, Data>();
        static readonly Dictionary<string, Material> s_Materials = new Dictionary<string, Material>();

        /// <summary>The character's own name (Imp, Puglin, Ranger).</summary>
        public string Character;
        /// <summary>Height of the rest pose in this object's own units (metres at scale 1).</summary>
        public float Height;
        public SkinnedMeshRenderer Renderer;
        /// <summary>Every bone, parents before children.</summary>
        public Transform[] Bones;
        /// <summary>Indexed by <see cref="Part"/>; null where the skeleton has no such bone.</summary>
        public Transform[] Parts;
        /// <summary>Each part's rotation in this object's space at rest, and the hips' rest position.</summary>
        public Quaternion[] RestRotations;
        public Vector3 HipsRest;
        /// <summary>The middle of the skull, in the head bone's space (for the head hitbox).</summary>
        public Vector3 HeadCenterLocal;

        public Transform Hips => Parts[(int)Part.Hips];
        public Transform Head => Parts[(int)Part.Head];
        public Vector3 HeadCenter => Head.TransformPoint(HeadCenterLocal);

        /// <summary>Whether the character's file is in the project.</summary>
        public static bool Exists(string character) => Load(character) != null;

        /// <summary>
        /// Builds <paramref name="character"/> under <paramref name="parent"/> (feet at its origin, facing its +Z), with the
        /// colour variant <paramref name="variant"/> where the pack has several. Null when the file isn't there.
        /// </summary>
        public static CharacterSkin Create(string character, Transform parent, int variant = 0)
        {
            var d = Load(character);
            if (d == null) return null;
            var go = new GameObject(character);
            go.transform.SetParent(parent, false);
            var skin = go.AddComponent<CharacterSkin>();
            skin.Build(d, character, variant);
            return skin;
        }

        static Data Load(string character)
        {
            if (s_Data.TryGetValue(character, out var cached)) return cached;
            Data d = null;
            try { d = Read(character); }
            catch (System.Exception e) { Debug.LogWarning($"Character {character} couldn't be loaded: {e.Message}"); }
            s_Data[character] = d;
            return d;
        }

        static string ReadString(BinaryReader r) => System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));

        static Data Read(string character)
        {
            var asset = Resources.Load<TextAsset>(Folder + character + "/" + character);
            if (asset == null) return null;
            using var r = new BinaryReader(new MemoryStream(asset.bytes));
            if (new string(r.ReadChars(4)) != "SKN1") return null;
            var d = new Data();
            int nb = r.ReadInt32();
            d.Names = new string[nb];
            d.Parents = new int[nb];
            d.Pos = new Vector3[nb];
            d.Rot = new Quaternion[nb];
            d.Scale = new Vector3[nb];
            for (int i = 0; i < nb; i++)
            {
                d.Names[i] = ReadString(r);
                d.Parents[i] = r.ReadInt32();
                d.Pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                d.Rot[i] = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                d.Scale[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            }
            int subs = r.ReadInt32();
            d.Keys = new string[subs];
            for (int i = 0; i < subs; i++) d.Keys[i] = ReadString(r);
            int n = r.ReadInt32();
            var pos = new Vector3[n];
            var nrm = new Vector3[n];
            var uv = new Vector2[n];
            var weights = new BoneWeight[n];
            for (int i = 0; i < n; i++) pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int i = 0; i < n; i++) nrm[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int i = 0; i < n; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
            var idx = r.ReadBytes(n * 4);
            float top = 0f;
            for (int i = 0; i < n; i++)
            {
                weights[i].boneIndex0 = idx[i * 4];
                weights[i].boneIndex1 = idx[i * 4 + 1];
                weights[i].boneIndex2 = idx[i * 4 + 2];
                weights[i].boneIndex3 = idx[i * 4 + 3];
                top = Mathf.Max(top, pos[i].y);
            }
            for (int i = 0; i < n; i++)
            {
                weights[i].weight0 = r.ReadSingle();
                weights[i].weight1 = r.ReadSingle();
                weights[i].weight2 = r.ReadSingle();
                weights[i].weight3 = r.ReadSingle();
            }
            var mesh = new Mesh { name = character, indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            mesh.boneWeights = weights;
            mesh.subMeshCount = subs;
            for (int s = 0; s < subs; s++)
            {
                var tris = new int[r.ReadInt32()];
                for (int i = 0; i < tris.Length; i++) tris[i] = r.ReadInt32();
                mesh.SetTriangles(tris, s);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            d.Mesh = mesh;
            d.Height = top;
            return d;
        }

        void Build(Data d, string character, int variant)
        {
            Character = character;
            Height = d.Height;
            int nb = d.Names.Length;
            Bones = new Transform[nb];
            for (int i = 0; i < nb; i++)
            {
                var t = new GameObject(d.Names[i]).transform;
                t.SetParent(d.Parents[i] < 0 ? transform : Bones[d.Parents[i]], false);
                t.localPosition = d.Pos[i];
                t.localRotation = d.Rot[i];
                t.localScale = d.Scale[i];
                Bones[i] = t;
            }
            if (d.Mesh.bindposes == null || d.Mesh.bindposes.Length != nb)
            {
                var bind = new Matrix4x4[nb];
                for (int i = 0; i < nb; i++) bind[i] = Bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
                d.Mesh.bindposes = bind;
            }

            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(transform, false);
            Renderer = meshGo.AddComponent<SkinnedMeshRenderer>();
            Renderer.sharedMesh = d.Mesh;
            Renderer.bones = Bones;
            Renderer.rootBone = Bones[0];
            Renderer.updateWhenOffscreen = true;
            var mats = new Material[d.Keys.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = MaterialFor(character, d.Keys[i], variant);
            Renderer.sharedMaterials = mats;

            Parts = new Transform[PartCount];
            for (int p = 0; p < PartCount; p++)
                foreach (var b in Bones)
                    if (b.name == UnrealNames[p]) { Parts[p] = b; break; }
            RestRotations = Capture(transform, Parts);
            HipsRest = transform.InverseTransformPoint(Hips.position);

            // The skull's middle: halfway between the head bone and the top of the mesh, a little forward.
            var head = Head;
            var top = transform.TransformPoint(new Vector3(0f, d.Height, 0f));
            var center = Vector3.Lerp(head.position, top, 0.45f) + transform.forward * (0.02f * d.Height);
            HeadCenterLocal = head.InverseTransformPoint(center);
        }

        /// <summary>Colour, normal and glow textures for one of the character's materials (shared between copies).</summary>
        static Material MaterialFor(string character, string key, int variant)
        {
            string path = Folder + character + "/" + key;
            var albedo = variant > 0 ? Resources.Load<Texture2D>(path + "_albedo_" + (variant + 1)) : null;
            if (albedo == null) albedo = Resources.Load<Texture2D>(path + "_albedo");
            string cacheKey = path + "#" + (albedo != null ? albedo.name : "");
            if (s_Materials.TryGetValue(cacheKey, out var cached) && cached != null) return cached;
            var mat = PropModels.Textured(character + " " + key, albedo, Resources.Load<Texture2D>(path + "_normal"), 0f, 0.2f);
            mat.enableInstancing = false;
            var glow = Resources.Load<Texture2D>(path + "_emission");
            if (glow != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", glow);
                mat.SetColor("_EmissionColor", new Color(1.6f, 1.4f, 1.1f));
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            s_Materials[cacheKey] = mat;
            return mat;
        }

        /// <summary>The character's materials in colour <paramref name="variant"/> (shared: copy them before changing them).</summary>
        public static Material[] Materials(string character, int variant)
        {
            var d = Load(character);
            if (d == null) return new Material[0];
            var mats = new Material[d.Keys.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = MaterialFor(character, d.Keys[i], variant);
            return mats;
        }

        /// <summary>How many colour variants a character has.</summary>
        public static int Variants(string character)
        {
            if (s_Variants.TryGetValue(character, out int known)) return known;
            var d = Load(character);
            int n = 0;
            if (d != null)
                for (n = 1; Resources.Load<Texture2D>(Folder + character + "/" + d.Keys[0] + "_albedo_" + (n + 1)) != null; n++) { }
            s_Variants[character] = n;
            return n;
        }

        static readonly Dictionary<string, int> s_Variants = new Dictionary<string, int>();

        /// <summary>Each part's rotation relative to <paramref name="root"/> (identity where the part is missing).</summary>
        public static Quaternion[] Capture(Transform root, Transform[] parts)
        {
            var inv = Quaternion.Inverse(root.rotation);
            var rest = new Quaternion[parts.Length];
            for (int p = 0; p < parts.Length; p++) rest[p] = parts[p] != null ? inv * parts[p].rotation : Quaternion.identity;
            return rest;
        }

        /// <summary>A Mixamo skeleton's parts under <paramref name="root"/> (names after any "prefix:").</summary>
        public static Transform[] FindMixamo(Transform root)
        {
            var parts = new Transform[PartCount];
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int p = 0; p < PartCount; p++)
            {
                string want = MixamoNames[p];
                foreach (var t in all)
                {
                    var n = t.name;
                    int colon = n.LastIndexOf(':');
                    if ((colon >= 0 ? n.Substring(colon + 1) : n) == want) { parts[p] = t; break; }
                }
            }
            return parts;
        }

        /// <summary>
        /// Per part, the turn that lines this character's rest bone up with the source skeleton's rest bone (both in
        /// their own object's space), so small differences between two T-poses (Mixamo's legs stand a little apart and
        /// its arms droop a little) don't end up added to every animation. Call while both are at rest.
        /// </summary>
        public Quaternion[] AlignTo(Transform sourceRoot, Transform[] source)
        {
            var align = new Quaternion[Parts.Length];
            for (int p = 0; p < Parts.Length; p++)
            {
                align[p] = Quaternion.identity;
                int c = ChildPart(p);
                if (p == (int)Part.Hips || c < 0 || Parts[p] == null || Parts[c] == null || source[p] == null || source[c] == null) continue;
                var mine = transform.InverseTransformDirection(Parts[c].position - Parts[p].position);
                var theirs = sourceRoot.InverseTransformDirection(source[c].position - source[p].position);
                if (mine.sqrMagnitude < 1e-10f || theirs.sqrMagnitude < 1e-10f) continue;
                align[p] = Quaternion.FromToRotation(mine, theirs);
            }
            return align;
        }

        /// <summary>The part a part points at (for lining up rest poses), -1 for the ends.</summary>
        static int ChildPart(int p)
        {
            switch ((Part)p)
            {
                case Part.Head: case Part.LToes: case Part.RToes: return -1;
                case Part.LHand: return (int)Part.LMiddle1;
                case Part.RHand: return (int)Part.RMiddle1;
                case Part.UpperChest: return (int)Part.Neck;
                case Part.RShoulder: return (int)Part.RUpperArm;
                case Part.RUpperLeg: return (int)Part.RLowerLeg;
            }
            if (p >= (int)Part.LThumb1) return (p - (int)Part.LThumb1) % 3 == 2 ? -1 : p + 1;
            return p + 1;
        }

        /// <summary>
        /// Puts this character in the pose of another T-posed skeleton (<paramref name="source"/> under
        /// <paramref name="sourceRoot"/>, whose rest rotations are <paramref name="sourceRest"/>). Each part turns from
        /// the source's T-pose as the source part did (after <paramref name="align"/>, from <see cref="AlignTo"/>), and
        /// the hips move as far, scaled by leg length.
        /// </summary>
        public void Retarget(Transform sourceRoot, Transform[] source, Quaternion[] sourceRest, Vector3 sourceHipsRest, Quaternion[] align)
        {
            var srcInv = Quaternion.Inverse(sourceRoot.rotation);
            var root = transform.rotation;
            // Parts are listed parents first, so each one is set after the bones it hangs from.
            for (int p = 0; p < Parts.Length; p++)
            {
                var t = Parts[p];
                if (t == null || source[p] == null) continue;
                var delta = srcInv * source[p].rotation * Quaternion.Inverse(sourceRest[p]);
                t.rotation = root * (delta * align[p] * RestRotations[p]);
            }
            var srcHips = source[(int)Part.Hips];
            if (srcHips != null && sourceHipsRest.y > 1e-4f)
            {
                var moved = sourceRoot.InverseTransformPoint(srcHips.position) - sourceHipsRest;
                Hips.position = transform.TransformPoint(HipsRest + moved * (HipsRest.y / sourceHipsRest.y));
            }
        }

        /// <summary>
        /// Sets parts from rotations given in this object's space, relative to the T-pose and composed down the
        /// chain: <paramref name="own"/>[p] turns part p (and everything below it) on top of its parents' turns.
        /// Parts left at identity follow their parents. Used by <see cref="NpcGait"/>.
        /// </summary>
        public void Pose(Quaternion[] own, Vector3 hipsOffset)
        {
            var root = transform.rotation;
            var total = s_Total;
            for (int p = 0; p < Parts.Length; p++)
            {
                var t = Parts[p];
                if (t == null) continue;
                var parent = ParentPart(p);
                total[p] = (parent >= 0 ? total[parent] : Quaternion.identity) * own[p];
                t.rotation = root * (total[p] * RestRotations[p]);
            }
            Hips.position = transform.TransformPoint(HipsRest + hipsOffset);
        }

        static readonly Quaternion[] s_Total = new Quaternion[PartCount];

        /// <summary>The part each part hangs from (for composing turns down the chain), -1 for the hips.</summary>
        static int ParentPart(int p)
        {
            switch ((Part)p)
            {
                case Part.Hips: return -1;
                case Part.Spine: return (int)Part.Hips;
                case Part.Chest: return (int)Part.Spine;
                case Part.UpperChest: return (int)Part.Chest;
                case Part.Neck: return (int)Part.UpperChest;
                case Part.Head: return (int)Part.Neck;
                case Part.LShoulder: case Part.RShoulder: return (int)Part.UpperChest;
                case Part.LUpperLeg: case Part.RUpperLeg: return (int)Part.Hips;
            }
            // Arms, legs and fingers: the previous part in the list, except each finger's first joint hangs off the hand.
            if (p >= (int)Part.LThumb1)
            {
                int k = (p - (int)Part.LThumb1) % 15;
                bool left = p < (int)Part.RThumb1;
                return k % 3 == 0 ? (int)(left ? Part.LHand : Part.RHand) : p - 1;
            }
            return p - 1;
        }
    }
}
