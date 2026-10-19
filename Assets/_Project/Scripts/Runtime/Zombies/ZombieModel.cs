using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
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
    /// </summary>
    public sealed class ZombieModel : MonoBehaviour
    {
        const string CharacterFolder = "Zombies/Character";
        const string AnimationFolder = "Zombies/Animations/";
        const string Placeholder = "X Bot";

        // Base layer (whole body): locomotion and death. Upper layer (spine up): attacks and the spitter's scream.
        enum Clip { Idle, Walk, Run, Crawl, CrawlRun, Death, Dying, Attack, Bite, Bite2, Scream }
        const int BaseCount = 7, UpperCount = 4, ClipCount = 11;
        static readonly string[] Files =
        {
            "zombie idle", "zombie walk", "zombie run", "zombie crawl", "running crawl", "zombie death", "zombie dying",
            "zombie attack", "zombie biting", "zombie biting (2)", "zombie scream",
        };
        // What to play when a file is missing (only idle, walk and one death are needed).
        static readonly Clip[] Fallback =
        {
            Clip.Walk, Clip.Idle, Clip.Walk, Clip.Walk, Clip.Crawl, Clip.Dying, Clip.Death, Clip.Bite, Clip.Attack, Clip.Bite, Clip.Attack,
        };
        // Ground speed (m/s) each loop looks right at; playback is sped up or slowed to the zombie's real speed.
        static readonly float[] RefSpeed = { 1f, 0.9f, 3.2f, 0.5f, 1.6f };

        static readonly Color EyeGlow = new Color(1f, 0.85f, 0.2f);

        static bool s_Loaded;
        static GameObject s_Character;
        static AnimationClip[] s_Clips;

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

        float _deadAt = -1f, _upperWeight;
        int _deathClip;
        bool _headHidden;

        /// <summary>Whether a character model and the basic animations are in the project.</summary>
        public static bool Available
        {
            get
            {
                Load();
                return s_Character != null;
            }
        }

        public Transform Spine => _spine;
        public Transform Neck => _neck;
        /// <summary>Middle of the skull, where the head hitbox goes.</summary>
        public Vector3 HeadCenter => _headTop != null ? Vector3.Lerp(_headBone.position, _headTop.position, 0.45f) : _headBone.position + _headBone.up * 0.1f;

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
            if (s_Clips[(int)Clip.Idle] == null || s_Clips[(int)Clip.Death] == null) return;

            // Any skinned character in the folder; one the user added wins over the placeholder mannequin.
            var found = new List<GameObject>();
            foreach (var go in Resources.LoadAll<GameObject>(CharacterFolder))
                if (go.transform.parent == null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null && FindBone(go.transform, "Head") != null)
                    found.Add(go);
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var go in found)
                if (s_Character == null || s_Character.name == Placeholder) s_Character = go;
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

        static bool IsUnder(Transform t, Transform parent)
        {
            for (; t != null; t = t.parent) if (t == parent) return true;
            return false;
        }

        /// <summary>Builds a model under <paramref name="parent"/>, scaled to stand <paramref name="height"/> metres tall.</summary>
        public static ZombieModel Create(Transform parent, float height)
        {
            if (!Available) return null;
            var go = Instantiate(s_Character, parent, false);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var model = go.AddComponent<ZombieModel>();
            if (!model.Init(height))
            {
                Destroy(go);
                return null;
            }
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

            // Scale to the zombie's height (packs come in centimetres, metres or anything in between).
            var top = _headTop != null ? _headTop : _headBone;
            float tall = transform.InverseTransformPoint(top.position).y;
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
            var white = Greybox.Lit(Color.white);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var shared = _renderers[i].sharedMaterials;
                _materials[i] = new Material[shared.Length];
                _flash[i] = new Material[shared.Length];
                _baseColors[i] = new Color[shared.Length];
                for (int m = 0; m < shared.Length; m++)
                {
                    _materials[i][m] = shared[m] != null ? new Material(shared[m]) : white;
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
            bool faceless = s_Character.name == Placeholder;
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
            return true;
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
        }

        /// <summary>A fresh, standing zombie.</summary>
        public void ResetLife(uint seed)
        {
            _deadAt = -1f;
            _upperWeight = 0f;
            _headHidden = false;
            _headBone.localScale = _headScale;
            foreach (var eye in _eyes) eye.SetActive(true);
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
            }
        }

        public void SetFlash(bool flashing)
        {
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterials = flashing ? _flash[i] : _materials[i];
        }

        // ---------------------------------------------------------------- per frame

        /// <param name="speed">Ground speed seen on this machine (m/s).</param>
        /// <param name="swing">0 none, 0..1 wind-up, 1..2 strike and recover (as NetZombie measures it).</param>
        /// <param name="swingTime">Seconds the whole swing lasts.</param>
        /// <param name="swingVariant">Which attack to play (alternates per swing).</param>
        /// <param name="spit">Seconds since the last spit (large when none).</param>
        public void Animate(float dt, float speed, bool crawls, float swing, float swingTime, int swingVariant, float spit, bool stunned)
        {
            if (!_graph.IsValid()) return;
            float blend = 1f - Mathf.Exp(-7f * dt);

            // Base layer: what the legs are doing.
            var target = s_Target;
            System.Array.Clear(target, 0, BaseCount);
            if (_deadAt >= 0f)
            {
                target[_deathClip] = 1f;
                blend = 1f - Mathf.Exp(-14f * dt);
                var len = s_Clips[_deathClip].length;
                _time[_deathClip] = Mathf.Min(_time[_deathClip] + dt, len - 0.01f);
            }
            else if (crawls)
            {
                float fast = Mathf.Clamp01((speed - 1.1f) / 0.8f);
                target[(int)Clip.Crawl] = 1f - fast;
                target[(int)Clip.CrawlRun] = fast;
            }
            else
            {
                float moving = Mathf.Clamp01((speed - 0.08f) / 0.35f);
                float fast = Mathf.Clamp01((speed - 1.9f) / 1.3f);
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
                    float rate = i == (int)Clip.Idle ? 1f : Mathf.Clamp(speed / RefSpeed[i], 0.5f, 2.2f);
                    _time[i] += dt * rate * slow;
                    float len = Mathf.Max(s_Clips[i].length, 0.05f);
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
                    activeTime = s * Mathf.Min(s_Clips[active].length, swingTime * 1.6f);
                    upper = Mathf.Clamp01(s / 0.12f) * Mathf.Clamp01((1f - s) / 0.2f);
                }
                else if (spit < 1.1f)
                {
                    active = (int)Clip.Scream;
                    activeTime = Mathf.Min(0.4f + spit * 1.5f, s_Clips[active].length - 0.01f);
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
                float reach = 0.1f / Mathf.Max(transform.localScale.x, 1e-4f);
                h.x = Mathf.Clamp(h.x, _hipsBind.x - reach, _hipsBind.x + reach);
                h.z = Mathf.Clamp(h.z, _hipsBind.z - reach, _hipsBind.z + reach);
                _hips.localPosition = h;
            }
            if (_headHidden) _headBone.localScale = Vector3.zero;
        }
    }
}
