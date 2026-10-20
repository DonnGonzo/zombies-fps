using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ZombiesFPS.CharacterModels;
using ZombiesFPS.Core;

namespace ZombiesFPS.Zombies
{
    /// <summary>
    /// An imported, rigged zombie (any Mixamo-skeleton character in Resources/Zombies/Character) played with the
    /// Mixamo zombie animations in Resources/Zombies/Animations. NetZombie still owns movement, hitboxes and timing;
    /// this only turns "how fast, attacking, spitting, dead" into a blended pose. Everything is driven from state
    /// every machine already sees, so all players see the same animation.
    ///
    /// Swapping the character: drop another Mixamo character FBX (exported "FBX for Unity", with skin) into the
    /// Character folder. Any file there other than "X Bot" is used in preference to it.
    ///
    /// Monsters: <see cref="UseSkin"/> dresses a life as one of Ivan's Quaternius characters (Imp, Puglin) instead.
    /// The Mixamo rig still plays the clips, hidden, and its pose is copied onto the monster every frame
    /// (<see cref="CharacterSkin.Retarget"/>); the hitboxes follow the monster's own spine and head.
    ///
    /// Bosses: other <see cref="Kit"/>s pair another character with its own clips (the Mutant from Ivan's creature
    /// pack for the Warden and Azhrakel). Warrok (Ashgrave) wears the Mutant's clips as a skin: a hidden Mutant
    /// plays them and its pose is copied onto Warrok bone by bone from T-pose to T-pose, as for the monsters, since
    /// the two skeletons' rest rotations differ too much to share Generic clips directly.
    /// </summary>
    public sealed class ZombieModel : MonoBehaviour
    {
        const string Placeholder = "X Bot";

        /// <summary>A character and the clips it plays, one file per <see cref="Clip"/>.</summary>
        public sealed class Kit
        {
            public readonly string CharacterFolder, TextureFolder, AnimationFolder;
            public readonly string[] Files;
            // The rotting-skin shader (the zombies), or the character's own textures as they are (the bosses).
            public readonly bool Undead;
            // A character (Mixamo or Unreal skeleton) worn over the rig (Resources folder), or null to show the rig's own character.
            public readonly string SkinFolder;

            internal bool Loaded;
            internal GameObject Character;
            internal AnimationClip[] Clips;
            internal float[] Stride;
            internal GameObject Skin;
            bool? _dressed;

            /// <summary>Whether the worn character's textures are in the project (a kit with no skin always is).</summary>
            public bool Dressed
            {
                get
                {
                    if (SkinFolder == null) return true;
                    if (_dressed == null) _dressed = Resources.LoadAll<Texture2D>(SkinFolder + "/Textures").Length > 0;
                    return _dressed.Value;
                }
            }

            public Kit(string characterFolder, string animationFolder, string[] files, bool undead, string skinFolder = null)
            {
                CharacterFolder = characterFolder;
                TextureFolder = characterFolder + "/Textures/";
                AnimationFolder = animationFolder;
                Files = files;
                Undead = undead;
                SkinFolder = skinFolder;
            }
        }

        static readonly string[] MutantFiles =
        {
            "mutant breathing idle", "mutant walking", "mutant run", "mutant walking", "mutant run", "mutant dying", "mutant dying",
            "mutant swiping", "mutant punch", "mutant jump attack", "mutant roaring",
        };

        /// <summary>The horde: Ch30 (or any Mixamo character in the folder) with the Mixamo zombie clips.</summary>
        public static readonly Kit Zombie = new Kit("Zombies/Character", "Zombies/Animations/", new[]
        {
            "zombie idle", "zombie walk", "zombie run", "zombie crawl", "running crawl", "zombie death", "zombie dying",
            "zombie attack", "zombie biting", "zombie biting (2)", "zombie scream",
        }, undead: true);

        /// <summary>The Mutant from Ivan's creature pack, with its own clips: the Warden, and (in red) Azhrakel.</summary>
        public static readonly Kit Mutant = new Kit("Creatures/Mutant", "Creatures/Animations/", MutantFiles, undead: false);

        /// <summary>Warrok, worn over the Mutant playing its clips: General Ashgrave.</summary>
        public static readonly Kit Warrok = new Kit("Creatures/Mutant", "Creatures/Animations/", MutantFiles, undead: false, skinFolder: "Creatures/Warrok");

        /// <summary>
        /// The Biomechanical Mutant (Unreal skeleton), worn over the Mutant playing its clips: the Warden.
        /// </summary>
        public static readonly Kit BioMutant = new Kit("Creatures/Mutant", "Creatures/Animations/", MutantFiles, undead: false, skinFolder: "Creatures/BioMutant");

        // Base layer (whole body): locomotion and death. Upper layer (spine up): attacks and the spitter's scream.
        enum Clip { Idle, Walk, Run, Crawl, CrawlRun, Death, Dying, Attack, Bite, Bite2, Scream }
        const int BaseCount = 7, UpperCount = 4, ClipCount = 11;
        // What to play when a file is missing (only idle, walk and one death are needed).
        static readonly Clip[] Fallback =
        {
            Clip.Walk, Clip.Idle, Clip.Walk, Clip.Walk, Clip.Crawl, Clip.Dying, Clip.Death, Clip.Bite, Clip.Attack, Clip.Bite, Clip.Attack,
        };
        // Ground speed (m/s) each loop looks right at; playback is sped up or slowed to the zombie's real speed.
        // These are only fallbacks: the real strides are measured from the clips (MeasureStrides).
        static readonly float[] RefSpeed = { 1f, 0.9f, 3.2f, 0.5f, 1.6f };

        static readonly Color EyeGlow = new Color(1f, 0.85f, 0.2f);

        Kit _kit;
        // The kit's clips (its measured stride speeds are in Kit.Stride, in the model's own units).
        AnimationClip[] _clips;
        // Each material's name in the FBX, to find its textures (and their variants) by.
        string[][] _materialNames;
        float _sit;
        Transform _upLegL, _upLegR, _legL, _legR;

        PlayableGraph _graph;
        AnimationMixerPlayable _base, _upper;
        AnimationLayerMixerPlayable _layers;
        AnimationClipPlayable[] _playables;
        readonly float[] _time = new float[ClipCount];
        readonly float[] _weight = new float[BaseCount];
        static readonly float[] s_Target = new float[BaseCount];

        Transform _hips, _spine, _neck, _headBone, _headTop;
        Vector3 _hipsBind, _headScale;
        Renderer[] _renderers;
        Material[][] _materials, _flash;
        Color[][] _baseColors;
        Vector3 _baseScale;
        GameObject[] _eyes;

        // The Mixamo rig's parts and T-pose, for copying its pose onto a monster skin.
        Transform[] _srcParts;
        Quaternion[] _srcRest;
        Vector3 _srcHipsRest;
        float _tall;
        CharacterSkin _skin;
        readonly Dictionary<string, CharacterSkin> _skins = new Dictionary<string, CharacterSkin>();
        readonly Dictionary<CharacterSkin, Quaternion[]> _align = new Dictionary<CharacterSkin, Quaternion[]>();
        Quaternion[] _skinAlign;
        // The rig's local pose at rest, to put it back while a new skin lines itself up with it.
        Quaternion[] _srcLocalRest;
        Vector3 _srcHipsLocalRest;
        Material[] _skinMaterials, _skinFlash;
        Color[] _skinColors;
        Vector3 _skinHeadScale;

        float _deadAt = -1f, _upperWeight;
        int _deathClip;
        bool _headHidden;

        /// <summary>Whether the zombie character model and the basic animations are in the project.</summary>
        public static bool Available => IsAvailable(Zombie);

        /// <summary>Whether <paramref name="kit"/>'s character and its basic animations are in the project.</summary>
        public static bool IsAvailable(Kit kit)
        {
            Load(kit);
            return kit.Character != null;
        }

        public Transform Spine => _skin != null ? _skin.Parts[(int)Part.Spine] : _spine;
        public Transform Neck => _skin != null ? _skin.Parts[(int)Part.Neck] : _neck;
        /// <summary>Middle of the skull, where the head hitbox goes.</summary>
        public Vector3 HeadCenter => _skin != null ? _skin.HeadCenter
            : _headTop != null ? Vector3.Lerp(_headBone.position, _headTop.position, 0.45f) : _headBone.position + _headBone.up * 0.1f;

        static void Load(Kit kit)
        {
            if (kit.Loaded) return;
            kit.Loaded = true;

            var clips = kit.Clips = new AnimationClip[ClipCount];
            for (int i = 0; i < ClipCount; i++)
                foreach (var c in Resources.LoadAll<AnimationClip>(kit.AnimationFolder + kit.Files[i]))
                    if (!c.name.StartsWith("__preview__")) { clips[i] = c; break; }
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < ClipCount; i++)
                    if (clips[i] == null) clips[i] = clips[(int)Fallback[i]];
            if (clips[(int)Clip.Idle] == null || clips[(int)Clip.Death] == null)
            {
                Debug.LogWarning($"[Zombies] No idle or death clip in Resources/{kit.AnimationFolder}; {kit.CharacterFolder} stays blocky.");
                return;
            }

            // Any skinned character in the folder; one the user added wins over the placeholder mannequin.
            var found = new List<GameObject>();
            foreach (var go in Resources.LoadAll<GameObject>(kit.CharacterFolder))
                if (go.transform.parent == null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null && FindBone(go.transform, "Head") != null)
                    found.Add(go);
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var go in found)
                if (kit.Character == null || kit.Character.name == Placeholder) kit.Character = go;
            if (kit.Character == null) Debug.LogWarning($"[Zombies] No rigged character in Resources/{kit.CharacterFolder}.");
            if (kit.SkinFolder != null)
            {
                kit.Skin = FindCharacter(kit.SkinFolder);
                if (kit.Skin == null) Debug.LogWarning($"[Zombies] No rigged character in Resources/{kit.SkinFolder}; showing {kit.CharacterFolder} instead.");
            }
        }

        static GameObject FindCharacter(string folder)
        {
            foreach (var go in Resources.LoadAll<GameObject>(folder))
                if (go.transform.parent == null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null && FindBone(go.transform, "Head") != null)
                    return go;
            return null;
        }

        static Transform FindBone(Transform root, string bone)
        {
            // Any case: Unreal skeletons name it "head".
            if (string.Equals(root.name, bone, System.StringComparison.OrdinalIgnoreCase)
                || root.name.EndsWith(":" + bone, System.StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var t = FindBone(root.GetChild(i), bone);
                if (t != null) return t;
            }
            return null;
        }

        static bool IsUnder(Transform t, Transform parent)
        {
            for (; t != null; t = t.parent) if (t == parent) return true;
            return false;
        }

        /// <summary>
        /// Builds a model of <paramref name="kit"/> (the zombie when null) under <paramref name="parent"/>, scaled to
        /// stand <paramref name="height"/> metres tall.
        /// </summary>
        public static ZombieModel Create(Transform parent, float height, Kit kit = null)
        {
            kit = kit ?? Zombie;
            if (!IsAvailable(kit)) return null;
            var go = Instantiate(kit.Character, parent, false);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var model = go.AddComponent<ZombieModel>();
            model._kit = kit;
            model._clips = kit.Clips;
            if (!model.Init(height))
            {
                Destroy(go);
                return null;
            }
            // Warrok and the like: worn over the rig from the start.
            if (kit.Skin != null) model.UseSkin(null, 0);
            return model;
        }

        bool Init(float height)
        {
            _hips = FindBone(transform, "Hips");
            _spine = FindBone(transform, "Spine");
            _neck = FindBone(transform, "Neck");
            _headBone = FindBone(transform, "Head");
            _headTop = FindBone(transform, "HeadTop_End");
            if (_hips == null || _spine == null || _neck == null || _headBone == null) return false;
            _upLegL = FindBone(transform, "LeftUpLeg");
            _upLegR = FindBone(transform, "RightUpLeg");
            _legL = FindBone(transform, "LeftLeg");
            _legR = FindBone(transform, "RightLeg");

            // Scale to the zombie's height (packs come in centimetres, metres or anything in between).
            var top = _headTop != null ? _headTop : _headBone;
            float tall = transform.InverseTransformPoint(top.position).y;
            _tall = tall;
            // The rig's T-pose, before any clip moves it, for monster skins.
            _srcParts = CharacterSkin.FindMixamo(transform);
            _srcRest = CharacterSkin.Capture(transform, _srcParts);
            _srcHipsRest = transform.InverseTransformPoint(_hips.position);
            _srcLocalRest = new Quaternion[_srcParts.Length];
            for (int p = 0; p < _srcParts.Length; p++) _srcLocalRest[p] = _srcParts[p] != null ? _srcParts[p].localRotation : Quaternion.identity;
            _srcHipsLocalRest = _hips.localPosition;
            if (tall > 0.01f) transform.localScale = Vector3.one * (height / tall);
            _baseScale = transform.localScale;
            _hipsBind = _hips.localPosition;
            _headScale = _headBone.localScale;

            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            // Hitboxes follow the bones, so the pose must update even when no camera on this machine sees it.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Own copies of the materials, so each zombie can be tinted for its type.
            _renderers = GetComponentsInChildren<Renderer>(true);
            _materials = new Material[_renderers.Length][];
            _flash = new Material[_renderers.Length][];
            _baseColors = new Color[_renderers.Length][];
            _materialNames = new string[_renderers.Length][];
            var white = Greybox.Lit(Color.white);
            var undead = _kit.Undead ? Resources.Load<Material>("Materials/Undead") : null;
            if (undead != null && (undead.shader == null || !undead.shader.isSupported)) undead = null;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var shared = _renderers[i].sharedMaterials;
                _materials[i] = new Material[shared.Length];
                _flash[i] = new Material[shared.Length];
                _baseColors[i] = new Color[shared.Length];
                _materialNames[i] = new string[shared.Length];
                for (int m = 0; m < shared.Length; m++)
                {
                    _materialNames[i][m] = shared[m] != null ? shared[m].name.Replace(" (Instance)", "") : "";
                    _materials[i][m] = shared[m] == null ? white
                        : undead != null ? UndeadSkin(undead, shared[m], _kit.TextureFolder)
                        : !_kit.Undead ? OwnSkin(shared[m], _kit.TextureFolder, null)
                        : new Material(shared[m]);
                    _flash[i][m] = white;
                    _baseColors[i][m] = ColorOf(_materials[i][m]);
                }
                _renderers[i].sharedMaterials = _materials[i];
                if (_renderers[i] is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            // The faceless placeholder mannequin gets glowing eyes like the old zombies; a real character has its own.
            var center = HeadCenter;
            var fwd = transform.forward;
            var right = transform.right;
            bool faceless = _kit.Character.name == Placeholder;
            _eyes = new GameObject[faceless ? 2 : 0];
            for (int s = 0; s < _eyes.Length; s++)
            {
                var eye = Greybox.Prim(PrimitiveType.Sphere, s == 0 ? "EyeL" : "EyeR", _headBone, Vector3.zero, Vector3.one, EyeGlow, collider: false, unlit: true);
                eye.transform.position = center + fwd * (0.085f * height / 1.78f) + right * ((s == 0 ? -0.033f : 0.033f) * height / 1.78f);
                eye.transform.localScale = Vector3.one * (0.026f / Mathf.Max(_headBone.lossyScale.x, 1e-4f));
                eye.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _eyes[s] = eye;
            }

            BuildGraph(animator);
            // Measured once per kit, on a model that's in the scene (a pose doesn't update under an inactive parent).
            if (_kit.Stride == null && gameObject.activeInHierarchy) MeasureStrides();
            return true;
        }

        /// <summary>
        /// Rotting skin from the character's own textures. They're looked up by material name in the Textures
        /// folder (Name_Diffuse, Name_Normal, Name_Gloss), so the look never depends on how the FBX's embedded
        /// textures came through the import; otherwise whatever textures the imported material has are used.
        /// </summary>
        static Material UndeadSkin(Material undead, Material source, string folder)
        {
            var mat = new Material(undead);
            string name = source.name.Replace(" (Instance)", "");
            var diffuse = Resources.Load<Texture2D>(folder + name + "_Diffuse");
            var normal = Resources.Load<Texture2D>(folder + name + "_Normal");
            var gloss = Resources.Load<Texture2D>(folder + name + "_Gloss");
            if (diffuse == null && source.HasProperty("_MainTex")) diffuse = source.GetTexture("_MainTex") as Texture2D;
            if (normal == null && source.HasProperty("_BumpMap")) normal = source.GetTexture("_BumpMap") as Texture2D;
            if (diffuse != null) mat.SetTexture("_MainTex", diffuse);
            if (normal != null) mat.SetTexture("_BumpMap", normal);
            if (gloss != null) mat.SetTexture("_GlossMap", gloss);
            mat.name = name + " (Undead)";
            return mat;
        }

        /// <summary>
        /// The character's own look, lit, from the same Textures folder (Name_Diffuse, Name_Normal, Name_Glow), or
        /// from a <paramref name="variant"/> of it (Name_Variant_Diffuse...) where one is there.
        /// </summary>
        static Material OwnSkin(Material source, string folder, string variant)
        {
            string name = source.name.Replace(" (Instance)", "");
            Texture2D Find(string map)
            {
                Texture2D t = null;
                if (variant != null) t = Resources.Load<Texture2D>(folder + name + "_" + variant + "_" + map);
                return t != null ? t : Resources.Load<Texture2D>(folder + name + "_" + map);
            }
            var diffuse = Find("Diffuse");
            if (diffuse == null && source.HasProperty("_MainTex")) diffuse = source.GetTexture("_MainTex") as Texture2D;
            var normal = Find("Normal");
            if (normal == null && source.HasProperty("_BumpMap")) normal = source.GetTexture("_BumpMap") as Texture2D;
            var mat = PropModels.Textured(name + (variant != null ? " " + variant : ""), diffuse, normal, 0f, 0.25f);
            mat.enableInstancing = false;
            var glow = Find("Glow");
            if (glow != null)
            {
                mat.SetTexture("_EmissionMap", glow);
                mat.SetColor("_EmissionColor", Color.white * 1.6f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return mat;
        }

        /// <summary>
        /// Re-dresses the model in a variant of its textures (Azhrakel's red Mutant), or its own ones when null.
        /// Only for kits drawn with their own textures; the type tint (<see cref="SetLook"/>) still applies on top.
        /// </summary>
        public void UseVariant(string variant)
        {
            if (_kit.Undead || variant == _variant) return;
            _variant = variant;
            for (int i = 0; i < _materials.Length; i++)
                for (int m = 0; m < _materials[i].Length; m++)
                {
                    if (_materialNames[i][m] == "") continue;
                    var old = _materials[i][m];
                    var source = new Material(old) { name = _materialNames[i][m] };
                    _materials[i][m] = OwnSkin(source, _kit.TextureFolder, variant);
                    Destroy(source);
                    Destroy(old);
                    _baseColors[i][m] = ColorOf(_materials[i][m]);
                }
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterials = _materials[i];
        }

        string _variant;

        /// <summary>
        /// Seated (1) or standing (0): bends the hips and knees over whatever the clips are doing, for a figure on a
        /// throne (the hips stay where they are, on the seat).
        /// </summary>
        public void Sit(float seated) => _sit = Mathf.Clamp01(seated);

        /// <summary>
        /// How fast each loop actually walks, measured once from the clip itself so playback can match the zombie's
        /// real speed and the feet don't slide. Works whether the clip moves the hips forward or walks in place:
        /// the planted foot moves backward under the hips at exactly the ground speed.
        /// </summary>
        void MeasureStrides()
        {
            var stride = _kit.Stride = new float[RefSpeed.Length];
            var footL = FindBone(transform, "LeftToeBase") ?? FindBone(transform, "LeftFoot");
            var footR = FindBone(transform, "RightToeBase") ?? FindBone(transform, "RightFoot");
            float unit = Mathf.Max(transform.lossyScale.z, 1e-4f);
            _layers.SetInputWeight(1, 0f);
            var speeds = new List<float>();
            for (int clip = 1; clip < RefSpeed.Length; clip++)
            {
                stride[clip] = RefSpeed[clip] / unit;
                if (footL == null || footR == null) continue;
                for (int i = 0; i < BaseCount; i++) _base.SetInputWeight(i, i == clip ? 1f : 0f);
                float len = _clips[clip].length;
                if (len < 0.1f) continue;
                const int N = 48;
                float dt = len / N;
                speeds.Clear();
                Vector3 prevHips = default, prevL = default, prevR = default;
                for (int k = 0; k <= N; k++)
                {
                    _playables[clip].SetTime(k * dt);
                    _graph.Evaluate(0f);
                    var hips = transform.InverseTransformPoint(_hips.position);
                    var l = transform.InverseTransformPoint(footL.position);
                    var r = transform.InverseTransformPoint(footR.position);
                    if (k > 0)
                    {
                        bool left = l.y < r.y;
                        if (left == prevL.y < prevR.y)
                        {
                            var foot = left ? l - prevL : r - prevR;
                            speeds.Add(((hips.z - prevHips.z) - foot.z) / dt);
                        }
                    }
                    prevHips = hips; prevL = l; prevR = r;
                }
                if (speeds.Count < 6) continue;
                // The median shrugs off the jump where the loop wraps.
                speeds.Sort();
                float v = speeds[speeds.Count / 2];
                if (v * unit > 0.15f && v * unit < 7f) stride[clip] = v;
            }
            stride[0] = RefSpeed[0] / unit;
        }

        void BuildGraph(Animator animator)
        {
            _graph = PlayableGraph.Create("Zombie");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Pose", animator);
            _base = AnimationMixerPlayable.Create(_graph, BaseCount);
            _upper = AnimationMixerPlayable.Create(_graph, UpperCount);
            _layers = AnimationLayerMixerPlayable.Create(_graph, 2);
            _playables = new AnimationClipPlayable[ClipCount];
            for (int i = 0; i < ClipCount; i++)
            {
                var p = AnimationClipPlayable.Create(_graph, _clips[i]);
                p.SetApplyFootIK(false);
                p.Pause();
                _playables[i] = p;
                if (i < BaseCount) _graph.Connect(p, 0, _base, i);
                else _graph.Connect(p, 0, _upper, i - BaseCount);
            }
            _graph.Connect(_base, 0, _layers, 0);
            _graph.Connect(_upper, 0, _layers, 1);
            _layers.SetInputWeight(0, 1f);
            _layers.SetInputWeight(1, 0f);

            // Attacks move the spine, arms and head only, so the legs keep walking underneath.
            var mask = new AvatarMask();
            var all = GetComponentsInChildren<Transform>(true);
            mask.transformCount = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                mask.SetTransformPath(i, PathOf(all[i]));
                mask.SetTransformActive(i, IsUnder(all[i], _spine));
            }
            _layers.SetLayerMaskFromAvatarMask(1, mask);
            output.SetSourcePlayable(_layers);
        }

        string PathOf(Transform t)
        {
            if (t == transform) return "";
            string path = t.name;
            for (var p = t.parent; p != null && p != transform; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        static Color ColorOf(Material m)
        {
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            return Color.white;
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        // ---------------------------------------------------------------- per life

        /// <summary>Colours the model for this life's type and sets its build.</summary>
        public void SetLook(Color tint, float heightScale)
        {
            transform.localScale = _baseScale * heightScale;
            for (int i = 0; i < _materials.Length; i++)
                for (int m = 0; m < _materials[i].Length; m++)
                {
                    var mat = _materials[i][m];
                    var c = _baseColors[i][m] * tint;
                    c.a = _baseColors[i][m].a;
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                    else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                }
            // Monsters keep most of their own colours: the type's tint is only a wash over them.
            if (_skin != null)
            {
                var wash = Color.Lerp(Color.white, tint, 0.35f);
                for (int m = 0; m < _skinMaterials.Length; m++)
                {
                    var c = _skinColors[m] * wash;
                    c.a = 1f;
                    _skinMaterials[m].color = c;
                }
            }
        }

        /// <summary>A fresh, standing zombie.</summary>
        public void ResetLife(uint seed)
        {
            _deadAt = -1f;
            _upperWeight = 0f;
            _headHidden = false;
            _headBone.localScale = _headScale;
            foreach (var eye in _eyes) eye.SetActive(_skin == null);
            // Start the loops at different points so a horde doesn't step in unison.
            for (int i = 0; i < ClipCount; i++) _time[i] = ((seed >> (i % 16)) & 0xFF) / 255f * 3f;
            for (int i = 0; i < BaseCount; i++) _weight[i] = 0f;
            _weight[(int)Clip.Idle] = 1f;
        }

        public void Die(int variant, bool headshot)
        {
            if (_deadAt >= 0f) return;
            _deadAt = Time.time;
            _deathClip = variant % 2 == 0 ? (int)Clip.Death : (int)Clip.Dying;
            _time[_deathClip] = 0f;
            if (headshot)
            {
                // Takes the head off: the bone shrinks to nothing, which collapses the skull into the neck.
                _headHidden = true;
                foreach (var eye in _eyes) eye.SetActive(false);
                if (_skin != null) _skin.Head.localScale = Vector3.zero;
            }
        }

        public void SetFlash(bool flashing)
        {
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterials = flashing ? _flash[i] : _materials[i];
            if (_skin != null) _skin.Renderer.sharedMaterials = flashing ? _skinFlash : _skinMaterials;
        }

        // ---------------------------------------------------------------- per frame

        /// <summary>The ground speed (m/s) clip <paramref name="clip"/> walks at, at this zombie's size.</summary>
        float Stride(int clip) => Mathf.Max(_kit.Stride[clip] * transform.lossyScale.z, 0.1f);

        /// <param name="speed">Ground speed seen on this machine (m/s).</param>
        /// <param name="swing">0 none, 0..1 wind-up, 1..2 strike and recover (as NetZombie measures it).</param>
        /// <param name="swingTime">Seconds the whole swing lasts.</param>
        /// <param name="swingVariant">Which attack to play (alternates per swing).</param>
        /// <param name="spit">Seconds since the last spit (large when none).</param>
        public void Animate(float dt, float speed, bool crawls, float swing, float swingTime, int swingVariant, float spit, bool stunned)
        {
            if (!_graph.IsValid()) return;
            if (_kit.Stride == null) MeasureStrides();
            float blend = 1f - Mathf.Exp(-7f * dt);

            // Base layer: what the legs are doing.
            var target = s_Target;
            System.Array.Clear(target, 0, BaseCount);
            if (_deadAt >= 0f)
            {
                target[_deathClip] = 1f;
                blend = 1f - Mathf.Exp(-14f * dt);
                var len = _clips[_deathClip].length;
                _time[_deathClip] = Mathf.Min(_time[_deathClip] + dt, len - 0.01f);
            }
            else if (crawls)
            {
                float crawlPace = Stride((int)Clip.Crawl), fastPace = Stride((int)Clip.CrawlRun);
                float from = crawlPace * 1.4f, to = Mathf.Max(fastPace * 0.8f, from + 0.4f);
                float fast = Mathf.Clamp01((speed - from) / (to - from));
                target[(int)Clip.Crawl] = 1f - fast;
                target[(int)Clip.CrawlRun] = fast;
            }
            else
            {
                // Walk until well past the walk's own pace, then blend into the run.
                float walkPace = Stride((int)Clip.Walk), runPace = Stride((int)Clip.Run);
                float from = walkPace * 1.4f, to = Mathf.Max(runPace * 0.8f, from + 0.5f);
                float moving = Mathf.Clamp01((speed - 0.08f) / 0.35f);
                float fast = Mathf.Clamp01((speed - from) / (to - from));
                target[(int)Clip.Idle] = 1f - moving;
                target[(int)Clip.Walk] = moving * (1f - fast);
                target[(int)Clip.Run] = moving * fast;
            }

            float slow = stunned ? 0.3f : 1f;
            for (int i = 0; i < BaseCount; i++)
            {
                _weight[i] = Mathf.Lerp(_weight[i], target[i], blend);
                _base.SetInputWeight(i, _weight[i]);
                if (i < RefSpeed.Length)
                {
                    // Loops play at the speed the zombie is really moving (idle at its own pace).
                    float rate = i == (int)Clip.Idle ? 1f : Mathf.Clamp(speed / Stride(i), 0.35f, 3f);
                    _time[i] += dt * rate * slow;
                    float len = Mathf.Max(_clips[i].length, 0.05f);
                    if (_time[i] > len) _time[i] %= len;
                }
                _playables[i].SetTime(_time[i]);
            }

            // Upper layer: a swipe or bite over whatever the legs are doing, or the spitter's scream.
            float upper = 0f;
            int active = -1;
            float activeTime = 0f;
            if (_deadAt < 0f)
            {
                // The attack clips are all standing ones; a crawler's lunge is in its crawl.
                if (swing > 0f && !crawls)
                {
                    // Wind-up is the first half of the clip, the strike and recovery the second. Long Mixamo attacks
                    // are cut to fit the game's swing, rather than played at triple speed.
                    float s = swing < 1f ? swing * 0.5f : 0.5f + (swing - 1f) * 0.5f;
                    active = (int)Clip.Attack + Mathf.Abs(swingVariant) % 3;
                    activeTime = s * Mathf.Min(_clips[active].length, swingTime * 1.6f);
                    upper = Mathf.Clamp01(s / 0.12f) * Mathf.Clamp01((1f - s) / 0.2f);
                }
                else if (spit < 1.1f)
                {
                    active = (int)Clip.Scream;
                    activeTime = Mathf.Min(0.4f + spit * 1.5f, _clips[active].length - 0.01f);
                    upper = Mathf.Clamp01(spit / 0.1f) * Mathf.Clamp01((1.1f - spit) / 0.35f);
                }
            }
            _upperWeight = Mathf.Lerp(_upperWeight, upper, 1f - Mathf.Exp(-20f * dt));
            for (int i = 0; i < UpperCount; i++)
            {
                int clip = BaseCount + i;
                _upper.SetInputWeight(i, clip == active ? 1f : 0f);
                if (clip == active) _playables[clip].SetTime(activeTime);
            }
            _layers.SetInputWeight(1, _upperWeight);

            _graph.Evaluate(0f);

            if (_deadAt < 0f)
            {
                // The zombie is moved by its navigation, not the clip: keep the hips inside the body collider so
                // a walk that travels doesn't drift off the body and snap back each loop.
                var h = _hips.localPosition;
                // Forward travel is pinned outright (a clamp would pop back each loop); side sway is kept.
                float reach = 0.1f / Mathf.Max(transform.localScale.x, 1e-4f);
                h.x = Mathf.Clamp(h.x, _hipsBind.x - reach, _hipsBind.x + reach);
                h.z = _hipsBind.z;
                _hips.localPosition = h;
            }
            if (_sit > 0f) PoseSeated();
            if (_headHidden) _headBone.localScale = Vector3.zero;
            if (_skin != null)
            {
                _skin.Retarget(transform, _srcParts, _srcRest, _srcHipsRest, _skinAlign);
                _skin.Head.localScale = _headHidden ? Vector3.zero : _skinHeadScale;
            }
        }

        /// <summary>Thighs swung forward level and shins hung straight down, eased in by <see cref="_sit"/>.</summary>
        void PoseSeated()
        {
            if (_upLegL == null || _upLegR == null || _legL == null || _legR == null) return;
            var fwd = transform.forward;
            foreach (var (thigh, shin) in new[] { (_upLegL, _legL), (_upLegR, _legR) })
            {
                Vector3 along = (shin.position - thigh.position).normalized;
                var level = Quaternion.FromToRotation(along, Vector3.ProjectOnPlane(fwd, Vector3.up).normalized + Vector3.down * 0.1f);
                thigh.rotation = Quaternion.Slerp(Quaternion.identity, level, _sit) * thigh.rotation;
                var foot = shin.childCount > 0 ? shin.GetChild(0) : null;
                if (foot == null) continue;
                Vector3 down = (foot.position - shin.position).normalized;
                var hang = Quaternion.FromToRotation(down, Vector3.down + fwd * 0.08f);
                shin.rotation = Quaternion.Slerp(Quaternion.identity, hang, _sit) * shin.rotation;
            }
        }

        // ---------------------------------------------------------------- monster skins

        /// <summary>Lines a new skin's rest pose up with the rig's T-pose (the rig is put back in it for a moment).</summary>
        Quaternion[] AlignSkin(CharacterSkin skin)
        {
            var pose = new Quaternion[_srcParts.Length];
            for (int p = 0; p < _srcParts.Length; p++)
                if (_srcParts[p] != null)
                {
                    pose[p] = _srcParts[p].localRotation;
                    _srcParts[p].localRotation = _srcLocalRest[p];
                }
            var hips = _hips.localPosition;
            _hips.localPosition = _srcHipsLocalRest;
            var align = skin.AlignTo(transform, _srcParts);
            for (int p = 0; p < _srcParts.Length; p++)
                if (_srcParts[p] != null) _srcParts[p].localRotation = pose[p];
            _hips.localPosition = hips;
            return align;
        }

        /// <summary>
        /// Dresses this life as <paramref name="character"/> (see <see cref="CharacterSkin"/>) in colour
        /// <paramref name="variant"/>, or as the zombie model itself when null or when the character isn't in the project.
        /// </summary>
        public void UseSkin(string character, int variant)
        {
            // A kit that wears a character always does.
            bool own = character == null && _kit.Skin != null;
            if (own) character = "#" + _kit.SkinFolder;
            CharacterSkin skin = null;
            if (character != null && !_skins.TryGetValue(character, out skin))
            {
                skin = own ? CharacterSkin.Wrap(Instantiate(_kit.Skin, transform, false), _kit.SkinFolder) : CharacterSkin.Create(character, transform, 0);
                if (skin != null)
                {
                    // As tall as the zombie, in the rig's own units (the model's scale then makes it the zombie's height).
                    skin.transform.localScale = Vector3.one * (_tall / Mathf.Max(skin.Height, 0.01f));
                    _align[skin] = AlignSkin(skin);
                }
                _skins[character] = skin;
            }
            foreach (var s in _skins.Values)
                if (s != null) s.gameObject.SetActive(s == skin);
            _skin = skin;
            _skinAlign = skin != null ? _align[skin] : null;
            foreach (var r in _renderers) r.enabled = skin == null;
            foreach (var eye in _eyes) eye.SetActive(skin == null && !_headHidden);
            if (skin == null) return;

            // Own copies of the variant's materials, so each monster can be tinted for its type.
            var shared = own ? WornMaterials(skin) : CharacterSkin.Materials(character, variant);
            _skinMaterials = new Material[shared.Length];
            _skinFlash = new Material[shared.Length];
            _skinColors = new Color[shared.Length];
            var white = Greybox.Lit(Color.white);
            for (int m = 0; m < shared.Length; m++)
            {
                _skinMaterials[m] = new Material(shared[m]);
                _skinFlash[m] = white;
                _skinColors[m] = ColorOf(_skinMaterials[m]);
            }
            skin.Renderer.sharedMaterials = _skinMaterials;
            _skinHeadScale = Vector3.one;
        }

        Material[] _worn;

        /// <summary>The worn character's own look, from its Textures folder (made once per model).</summary>
        Material[] WornMaterials(CharacterSkin skin)
        {
            if (_worn != null) return _worn;
            var shared = skin.Renderer.sharedMaterials;
            _worn = new Material[shared.Length];
            for (int m = 0; m < shared.Length; m++)
                _worn[m] = shared[m] != null ? OwnSkin(shared[m], _kit.SkinFolder + "/Textures/", null) : Greybox.Lit(Color.white);
            return _worn;
        }
    }
}
