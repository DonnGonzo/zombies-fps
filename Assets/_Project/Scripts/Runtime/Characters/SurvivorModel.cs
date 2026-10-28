using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ZombiesFPS.Core;

namespace ZombiesFPS.CharacterModels
{
    /// <summary>
    /// A teammate as other players see them: Ely (or any Mixamo character in Resources/Survivors/Character) played
    /// with Ivan's shooter pack in Resources/Survivors/Animations. The pack was exported on the Mutant's skeleton, so a
    /// hidden Mutant plays the clips and Ely copies its pose bone by bone, T-pose to T-pose
    /// (<see cref="CharacterSkin.Retarget"/>): the two rigs' rest rotations differ too much to share Generic clips.
    ///
    /// The legs blend between idle, walking and running in eight directions from the player's velocity in their own
    /// facing; the upper body plays a shot, a throw or a flinch over them, and a jump takes the whole body. A slide
    /// is posed by hand over the standing pose (the pack has no slide clip): hips dropped and leaning back, one leg out.
    /// </summary>
    public sealed class SurvivorModel : MonoBehaviour
    {
        const string CharacterFolder = "Survivors/Character";
        const string TextureFolder = "Survivors/Character/Textures/";
        const string AnimationFolder = "Survivors/Animations/";
        const string RigPath = "Creatures/Mutant/Mutant";

        enum Clip { Idle, Walk, Back, Left, Right, Run, RunBack, Jump, Fire, Throw, Hit }
        const int BaseCount = 8, UpperCount = 3, ClipCount = 11;
        static readonly string[] Files =
        {
            "rifle aiming idle", "walking", "walking backwards", "strafe left", "strafe right", "rifle run", "run backwards", "rifle jump",
            "firing rifle", "toss grenade", "hit reaction",
        };
        static readonly Clip[] Fallback =
        {
            Clip.Walk, Clip.Idle, Clip.Walk, Clip.Walk, Clip.Walk, Clip.Walk, Clip.Back, Clip.Idle, Clip.Idle, Clip.Fire, Clip.Fire,
        };
        // The throw clip ("toss grenade", 3.2 s): played from the arm drawing back to the release at the speed that
        // lets go when the thrower's throwable leaves, then the follow-through a little faster than recorded.
        const float TossStart = 0.38f, TossRelease = 0.56f, TossEnd = 0.85f, TossFollowRate = 1.6f;
        // The ground speed (m/s) each loop looks right at, so the feet keep up with the player.
        static readonly float[] RefSpeed = { 1f, 1.5f, 1.3f, 1.4f, 1.4f, 4.6f, 3.2f };

        static bool s_Loaded;
        static GameObject s_Character, s_Rig;
        static AnimationClip[] s_Clips;

        PlayableGraph _graph;
        AnimationMixerPlayable _base, _upper;
        AnimationLayerMixerPlayable _layers;
        AnimationClipPlayable[] _playables;
        readonly float[] _time = new float[ClipCount];
        readonly float[] _weight = new float[BaseCount];
        readonly float[] _target = new float[BaseCount];
        float _upperWeight, _airborne, _slide, _tall = 1.8f;
        CharacterSkin _skin;
        Transform[] _srcParts;
        Quaternion[] _srcRest, _align;
        Vector3 _srcHipsRest, _hipsBind;
        Transform _hips;

        /// <summary>The upper chest, for anything that rides the body (the gun).</summary>
        public Transform Chest { get; private set; }
        public Transform Head { get; private set; }
        /// <summary>The hand the throw clip throws with.</summary>
        public Transform RightHand { get; private set; }

        /// <summary>Whether a survivor character and the basic clips are in the project.</summary>
        public static bool Available
        {
            get
            {
                Load();
                return s_Character != null;
            }
        }

        static void Load()
        {
            if (s_Loaded) return;
            s_Loaded = true;
            s_Clips = new AnimationClip[ClipCount];
            for (int i = 0; i < ClipCount; i++)
                foreach (var c in Resources.LoadAll<AnimationClip>(AnimationFolder + Files[i]))
                    if (!c.name.StartsWith("__preview__")) { s_Clips[i] = c; break; }
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < ClipCount; i++)
                    if (s_Clips[i] == null) s_Clips[i] = s_Clips[(int)Fallback[i]];
            if (s_Clips[(int)Clip.Idle] == null || s_Clips[(int)Clip.Walk] == null)
            {
                Debug.LogWarning("[Characters] No shooter clips in Resources/" + AnimationFolder + "; teammates stay blocky.");
                return;
            }
            foreach (var go in Resources.LoadAll<GameObject>(CharacterFolder))
                if (go.transform.parent == null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null && FindBone(go.transform, "Head") != null)
                {
                    s_Character = go;
                    break;
                }
            if (s_Character == null) Debug.LogWarning("[Characters] No survivor character in Resources/" + CharacterFolder + "; teammates stay blocky.");
            s_Rig = Resources.Load<GameObject>(RigPath);
            if (s_Rig == null)
            {
                Debug.LogWarning("[Characters] The shooter clips play on the Mutant (Resources/" + RigPath + "), which is missing; teammates stay blocky.");
                s_Character = null;
            }
        }

        static Transform FindBone(Transform root, string bone)
        {
            if (root.name == bone || root.name.EndsWith(":" + bone)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var t = FindBone(root.GetChild(i), bone);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Builds the survivor under <paramref name="parent"/>, <paramref name="height"/> metres tall, facing its forward.</summary>
        public static SurvivorModel Create(Transform parent, float height)
        {
            if (!Available) return null;
            var go = Instantiate(s_Rig, parent, false);
            go.name = "Survivor";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var model = go.AddComponent<SurvivorModel>();
            if (!model.Init(height))
            {
                Destroy(go);
                return null;
            }
            return model;
        }

        bool Init(float height)
        {
            // The hidden rig the clips play on, at rest.
            _hips = FindBone(transform, "Hips");
            var rigHead = FindBone(transform, "Head");
            var rigTop = FindBone(transform, "HeadTop_End") ?? rigHead;
            if (_hips == null || rigHead == null) return false;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            float tall = transform.InverseTransformPoint(rigTop.position).y;
            if (tall < 0.01f) return false;
            _srcParts = CharacterSkin.FindMixamo(transform);
            _srcRest = CharacterSkin.Capture(transform, _srcParts);
            _srcHipsRest = transform.InverseTransformPoint(_hips.position);
            _hipsBind = _hips.localPosition;

            // Ely over it, as tall as the rig and lined up with its T-pose.
            _skin = CharacterSkin.Wrap(Instantiate(s_Character, transform, false), s_Character.name);
            if (_skin == null) return false;
            _skin.transform.localScale = Vector3.one * (tall / Mathf.Max(_skin.Height, 0.01f));
            _align = _skin.AlignTo(transform, _srcParts);
            transform.localScale = Vector3.one * (height / tall);
            _tall = tall;
            Head = _skin.Head;
            RightHand = _skin.Parts[(int)Part.RHand];
            Chest = _skin.Parts[(int)Part.UpperChest] ?? _skin.Parts[(int)Part.Chest] ?? _skin.Parts[(int)Part.Spine];

            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Ely's own textures, looked up by material name (Name_Diffuse, _Normal, _Glow), as the FBX's embedded
            // ones may not come through the import.
            var shared = _skin.Renderer.sharedMaterials;
            var mats = new Material[shared.Length];
            for (int m = 0; m < shared.Length; m++) mats[m] = shared[m] != null ? Skin(shared[m]) : Greybox.Lit(Color.white);
            _skin.Renderer.sharedMaterials = mats;

            BuildGraph(animator);
            return true;
        }
        static Material Skin(Material source)
        {
            string name = source.name.Replace(" (Instance)", "");
            var diffuse = Resources.Load<Texture2D>(TextureFolder + name + "_Diffuse");
            var normal = Resources.Load<Texture2D>(TextureFolder + name + "_Normal");
            var glow = Resources.Load<Texture2D>(TextureFolder + name + "_Glow");
            if (diffuse == null && source.HasProperty("_MainTex")) diffuse = source.GetTexture("_MainTex") as Texture2D;
            if (normal == null && source.HasProperty("_BumpMap")) normal = source.GetTexture("_BumpMap") as Texture2D;
            var mat = PropModels.Textured(name, diffuse, normal, 0.15f, 0.35f);
            mat.enableInstancing = false;
            if (glow != null)
            {
                mat.SetTexture("_EmissionMap", glow);
                mat.SetColor("_EmissionColor", Color.white);
                mat.EnableKeyword("_EMISSION");
            }
            return mat;
        }

        void BuildGraph(Animator animator)
        {
            _graph = PlayableGraph.Create("Survivor");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Pose", animator);
            _base = AnimationMixerPlayable.Create(_graph, BaseCount);
            _upper = AnimationMixerPlayable.Create(_graph, UpperCount);
            _layers = AnimationLayerMixerPlayable.Create(_graph, 2);
            _playables = new AnimationClipPlayable[ClipCount];
            for (int i = 0; i < ClipCount; i++)
            {
                var p = AnimationClipPlayable.Create(_graph, s_Clips[i]);
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
            // Shots, throws and flinches move the chest, arms and head; the legs keep walking.
            var mask = new AvatarMask();
            var chest = FindBone(transform, "Spine1") ?? FindBone(transform, "Spine");
            var all = GetComponentsInChildren<Transform>(true);
            mask.transformCount = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                mask.SetTransformPath(i, PathOf(all[i]));
                mask.SetTransformActive(i, chest != null && all[i].IsChildOf(chest));
            }
            _layers.SetLayerMaskFromAvatarMask(1, mask);
            output.SetSourcePlayable(_layers);
            // Standing at rest from the start, so whatever is fitted to the chest is fitted to that pose.
            _weight[(int)Clip.Idle] = 1f;
            _base.SetInputWeight((int)Clip.Idle, 1f);
            _graph.Evaluate(0f);
            Pose();
        }

        string PathOf(Transform t)
        {
            if (t == transform) return "";
            string path = t.name;
            for (var p = t.parent; p != null && p != transform; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>Keeps the rig over its feet (the player moves it, not the clip) and copies its pose onto Ely.</summary>
        void Pose()
        {
            var h = _hips.localPosition;
            float reach = 0.08f / Mathf.Max(transform.localScale.x, 1e-4f);
            h.x = Mathf.Clamp(h.x, _hipsBind.x - reach, _hipsBind.x + reach);
            h.z = _hipsBind.z;
            _hips.localPosition = h;
            // Down into a slide (in the body's own scale, so a taller survivor drops as far in proportion).
            if (_slide > 0.001f) _hips.position -= transform.up * (SlideHipDrop * _slide * transform.lossyScale.y * _tall / 1.8f);
            _skin.Retarget(transform, _srcParts, _srcRest, _srcHipsRest, _align);
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        /// <param name="velocity">Ground velocity in the player's own facing (x right, z forward), m/s.</param>
        /// <param name="grounded">False while jumping or falling.</param>
        /// <param name="fire">Seconds since the last shot (large when none).</param>
        /// <param name="toss">Seconds since the last throw started winding up.</param>
        /// <param name="tossWindUp">Seconds from that start to the throwable leaving the hand.</param>
        /// <param name="hit">Seconds since they were last hurt.</param>
        /// <param name="slide">0 standing to 1 fully down in a slide.</param>
        public void Animate(float dt, Vector3 velocity, bool grounded, float fire, float toss, float tossWindUp, float hit, float slide = 0f)
        {
            if (!_graph.IsValid() || dt <= 0f) return;
            float blend = 1f - Mathf.Exp(-9f * dt);
            // Sliding legs stop stepping: the slide pose goes over the standing one.
            _slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slide));
            velocity *= 1f - _slide;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            System.Array.Clear(_target, 0, BaseCount);
            _airborne = Mathf.MoveTowards(_airborne, grounded ? 0f : 1f, dt * (grounded ? 6f : 4f));
            float moving = Mathf.Clamp01((speed - 0.15f) / 0.5f);
            float fast = Mathf.Clamp01((speed - 2.6f) / 1.6f);
            float onFoot = 1f - _airborne;
            _target[(int)Clip.Idle] = (1f - moving) * onFoot;
            if (speed > 0.01f)
            {
                // Each direction's share of the move, from where it points in the player's facing.
                var dir = velocity / speed;
                float fwd = Mathf.Max(0f, dir.z), back = Mathf.Max(0f, -dir.z), left = Mathf.Max(0f, -dir.x), right = Mathf.Max(0f, dir.x);
                float sum = Mathf.Max(fwd + back + left + right, 1e-4f);
                float m = moving * onFoot / sum;
                _target[(int)Clip.Walk] = m * fwd * (1f - fast);
                _target[(int)Clip.Run] = m * fwd * fast;
                _target[(int)Clip.Back] = m * back * (1f - fast);
                _target[(int)Clip.RunBack] = m * back * fast;
                _target[(int)Clip.Left] = m * left;
                _target[(int)Clip.Right] = m * right;
            }
            _target[(int)Clip.Jump] = _airborne;
            if (_airborne <= 0f) _time[(int)Clip.Jump] = 0f;

            for (int i = 0; i < BaseCount; i++)
            {
                _weight[i] = Mathf.Lerp(_weight[i], _target[i], blend);
                _base.SetInputWeight(i, _weight[i]);
                float len = Mathf.Max(s_Clips[i].length, 0.05f);
                if (i == (int)Clip.Jump)
                {
                    // Takeoff into the hang of the jump, held there until landing.
                    _time[i] = Mathf.Min(_time[i] + dt, len * 0.55f);
                }
                else
                {
                    float rate = i == (int)Clip.Idle ? 1f : Mathf.Clamp(speed / RefSpeed[i], 0.5f, 2f);
                    _time[i] = (_time[i] + dt * rate) % len;
                }
                _playables[i].SetTime(_time[i]);
            }

            // Upper body: a flinch wins over a throw, a throw over a shot.
            int active = -1;
            float at = 0f, upper = 0f;
            float hitLen = s_Clips[(int)Clip.Hit].length, tossLen = s_Clips[(int)Clip.Throw].length;
            float tossRelease = TossRelease * tossLen, tossTime = tossWindUp + (TossEnd - TossRelease) * tossLen / TossFollowRate;
            if (hit < Mathf.Min(hitLen, 0.8f)) { active = (int)Clip.Hit; at = hit; upper = Fade(hit, Mathf.Min(hitLen, 0.8f)); }
            else if (toss >= 0f && toss < tossTime)
            {
                active = (int)Clip.Throw;
                at = toss < tossWindUp ? Mathf.Lerp(TossStart * tossLen, tossRelease, toss / Mathf.Max(tossWindUp, 0.01f))
                    : tossRelease + (toss - tossWindUp) * TossFollowRate;
                upper = Fade(toss, tossTime);
            }
            else if (fire < 0.35f) { active = (int)Clip.Fire; at = Mathf.Min(fire, s_Clips[(int)Clip.Fire].length * 0.3f); upper = Fade(fire, 0.35f); }
            _upperWeight = Mathf.Lerp(_upperWeight, upper, 1f - Mathf.Exp(-25f * dt));
            for (int i = 0; i < UpperCount; i++)
            {
                int clip = BaseCount + i;
                _upper.SetInputWeight(i, clip == active ? 1f : 0f);
                if (clip == active) _playables[clip].SetTime(at);
            }
            _layers.SetInputWeight(1, _upperWeight);
            _graph.Evaluate(0f);
            if (_slide > 0.001f) PoseSlide(_slide);
            Pose();
        }

        // The slide pose, in degrees about the body's own right axis (negative swings a leg forward or leans back).
        const float SlideHipsLean = -16f, SlideSpineLean = -10f, SlideHeadNod = 22f;
        const float SlideLeadHip = -62f, SlideLeadKnee = 10f, SlideTuckHip = -18f, SlideTuckKnee = 105f;
        /// <summary>How far the hips drop at the bottom of a slide, in metres of a 1.8 m survivor.</summary>
        const float SlideHipDrop = 0.5f;

        /// <summary>Bends the hidden rig into a slide on top of whatever the clips posed, parent bones before children.</summary>
        void PoseSlide(float k)
        {
            Vector3 right = transform.right;
            Turn(Part.Hips, SlideHipsLean * k, right);
            Turn(Part.Spine, SlideSpineLean * k, right);
            Turn(Part.Head, SlideHeadNod * k, right);
            Turn(Part.LUpperLeg, SlideLeadHip * k, right);
            Turn(Part.RUpperLeg, SlideTuckHip * k, right);
            Turn(Part.LLowerLeg, SlideLeadKnee * k, right);
            Turn(Part.RLowerLeg, SlideTuckKnee * k, right);
        }

        void Turn(Part part, float degrees, Vector3 axis)
        {
            var bone = _srcParts[(int)part];
            if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        static float Fade(float t, float length) => Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((length - t) / 0.2f);

        public void SetVisible(bool visible) => _skin.Renderer.enabled = visible;
    }
}
