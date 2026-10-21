using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.CharacterModels
{
    /// <summary>
    /// The Yuan-ti (Ziggertron's D&amp;D Heroes model, CC-BY 4.0): a serpent-bodied guardian with its own baked clips
    /// (Resources/Characters/YuanTi, made by tools/yuanti/pack_yuanti.py). Unlike the humanoids it has no Mixamo rig to
    /// copy: it plays its "ANM1" clips straight onto its bones, crossfading between them. Feet (coils) at the origin,
    /// facing +Z, about 2.9 m tall when it rears up.
    /// </summary>
    public sealed class SerpentModel : MonoBehaviour
    {
        public const string Character = "YuanTi";

        sealed class ClipData
        {
            public float Fps;
            public int Frames, Bones;
            public Vector3[] Pos;
            public Quaternion[] Rot;
            public float Length => (Frames - 1) / Fps;
        }

        static Dictionary<string, ClipData> s_Clips;
        static bool s_Tried;

        // Where each attack's blow lands, as a share of the clip (measured from the clips: the hands' furthest reach).
        static readonly string[] Attacks = { "strike", "thrust" };
        static readonly float[] AttackHit = { 0.7f, 0.58f };
        // Ground speed (m/s) the slither and the rush look right at.
        const float SlitherSpeed = 1.6f, RunSpeed = 3.6f;
        const float Fade = 0.22f;

        CharacterSkin _skin;
        Transform[] _bones;
        Transform _spine, _neck, _head;
        Material[] _materials, _flash;

        string _clip = "idle", _prevClip;
        float _time, _prevTime, _fade = 1f;
        float _deadAt = -1f;
        int _lastSwing = -1;
        Vector3[] _pos;
        Quaternion[] _rot;

        public Transform Spine => _spine;
        public Transform Neck => _neck;
        public Vector3 HeadCenter => _head.position + transform.up * 0.14f * transform.lossyScale.y + transform.forward * 0.05f;
        public Renderer Renderer => _skin.Renderer;

        public static bool Available => CharacterSkin.Exists(Character) && LoadClips() != null;

        /// <summary>Builds the serpent under <paramref name="parent"/>; null when its files aren't in the project.</summary>
        public static SerpentModel Create(Transform parent)
        {
            if (LoadClips() == null) return null;
            var skin = CharacterSkin.Create(Character, parent);
            if (skin == null) return null;
            var model = skin.gameObject.AddComponent<SerpentModel>();
            model.Init(skin);
            return model;
        }

        void Init(CharacterSkin skin)
        {
            _skin = skin;
            _bones = skin.Bones;
            _pos = new Vector3[_bones.Length];
            _rot = new Quaternion[_bones.Length];
            _spine = Find("Bip01 Spine1");
            _neck = Find("Bip01 Neck");
            _head = Find("Bip01 Head");
            _materials = skin.Renderer.sharedMaterials;
            _flash = new Material[_materials.Length];
            var white = Greybox.Lit(Color.white);
            for (int i = 0; i < _flash.Length; i++) _flash[i] = white;
            ResetLife();
        }

        // Bone names carry the exporter's "_N" suffix; match on the start.
        Transform Find(string prefix)
        {
            foreach (var b in _bones)
                if (b.name == prefix || b.name.StartsWith(prefix + "_")) return b;
            return _bones[0];
        }

        static Dictionary<string, ClipData> LoadClips()
        {
            if (s_Tried) return s_Clips;
            s_Tried = true;
            var asset = Resources.Load<TextAsset>(CharacterSkin.Folder + Character + "/" + Character + "_clips");
            if (asset == null) return null;
            try
            {
                using var r = new BinaryReader(new MemoryStream(asset.bytes));
                var clips = new Dictionary<string, ClipData>();
                int n = r.ReadInt32();
                for (int c = 0; c < n; c++)
                {
                    string name = System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
                    var d = new ClipData { Fps = r.ReadSingle(), Frames = r.ReadInt32(), Bones = r.ReadInt32() };
                    int count = d.Frames * d.Bones;
                    d.Pos = new Vector3[count];
                    d.Rot = new Quaternion[count];
                    for (int i = 0; i < count; i++)
                    {
                        d.Pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        d.Rot[i] = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    }
                    clips[name] = d;
                }
                s_Clips = clips;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Yuan-ti clips couldn't be loaded: {e.Message}");
            }
            return s_Clips;
        }

        /// <summary>A fresh life: coiled and idle.</summary>
        public void ResetLife()
        {
            _deadAt = -1f;
            _lastSwing = -1;
            _clip = "roar";
            _time = 0f;
            _prevClip = null;
            _fade = 1f;
            _head.localScale = Vector3.one;
            Sample(0f);
        }

        public void Die(bool headshot)
        {
            if (_deadAt >= 0f) return;
            _deadAt = Time.time;
            Play("die", 0f);
        }

        public void SetFlash(bool flashing) => _skin.Renderer.sharedMaterials = flashing ? _flash : _materials;

        void Play(string clip, float time)
        {
            if (clip == _clip)
            {
                _time = time;
                return;
            }
            _prevClip = _clip;
            _prevTime = _time;
            _clip = clip;
            _time = time;
            _fade = 0f;
        }

        /// <param name="speed">Ground speed seen on this machine (m/s).</param>
        /// <param name="swing">0 none, 0..1 wind-up, 1..2 strike and recover (as NetZombie measures it).</param>
        /// <param name="windup">Seconds of the swing before the blow lands; <paramref name="swingTime"/> the whole swing.</param>
        public void Animate(float dt, float speed, float swing, float windup, float swingTime, int swingVariant, bool stunned)
        {
            if (!s_Clips.TryGetValue(_clip, out var current)) current = s_Clips["idle"];
            if (_deadAt >= 0f)
            {
                _time = Mathf.Min(_time + dt, current.Length);
            }
            else if (swing > 0f)
            {
                // The blow in the clip lands as the swing's wind-up ends, wherever it falls in the clip.
                int a = Mathf.Abs(swingVariant) % Attacks.Length;
                var clip = s_Clips[Attacks[a]];
                float hit = AttackHit[a] * clip.Length;
                float t = swing < 1f ? swing * hit : hit + (swing - 1f) * (clip.Length - hit);
                if (swingVariant != _lastSwing && _clip != Attacks[a])
                {
                    _lastSwing = swingVariant;
                    Play(Attacks[a], t);
                }
                else _time = t;
            }
            else if (_clip == "roar" && _time < current.Length)
            {
                _time += dt;
            }
            else if (stunned)
            {
                if (_clip != "hit") Play("hit", 0f);
                else _time = Mathf.Repeat(_time + dt, current.Length);
            }
            else
            {
                string want = speed > 2.6f ? "run" : speed > 0.3f ? "slither" : "idle";
                float rate = want == "run" ? Mathf.Clamp(speed / RunSpeed, 0.7f, 1.6f)
                    : want == "slither" ? Mathf.Clamp(speed / SlitherSpeed, 0.6f, 1.8f) : 1f;
                if (_clip != want) Play(want, 0f);
                else _time = Mathf.Repeat(_time + dt * rate, s_Clips[want].Length);
            }
            if (_prevClip != null) _prevTime += dt;
            _fade = Mathf.Min(1f, _fade + dt / Fade);
            Sample(_fade);
        }

        void Sample(float fade)
        {
            if (!s_Clips.TryGetValue(_clip, out var clip)) return;
            Pose(clip, _time, _pos, _rot);
            if (fade < 1f && _prevClip != null && s_Clips.TryGetValue(_prevClip, out var prev))
            {
                var p = s_Scratch.Length == _pos.Length ? s_Scratch : (s_Scratch = new Vector3[_pos.Length]);
                var q = s_ScratchRot.Length == _rot.Length ? s_ScratchRot : (s_ScratchRot = new Quaternion[_rot.Length]);
                Pose(prev, Mathf.Min(_prevTime, prev.Length), p, q);
                float w = fade * fade * (3f - 2f * fade);
                for (int i = 0; i < _pos.Length; i++)
                {
                    _pos[i] = Vector3.Lerp(p[i], _pos[i], w);
                    _rot[i] = Quaternion.Slerp(q[i], _rot[i], w);
                }
            }
            int n = Mathf.Min(_bones.Length, clip.Bones);
            for (int i = 0; i < n; i++) _bones[i].SetLocalPositionAndRotation(_pos[i], _rot[i]);
        }

        static Vector3[] s_Scratch = new Vector3[0];
        static Quaternion[] s_ScratchRot = new Quaternion[0];

        static void Pose(ClipData clip, float time, Vector3[] pos, Quaternion[] rot)
        {
            float f = Mathf.Clamp(time * clip.Fps, 0f, clip.Frames - 1);
            int a = Mathf.Min((int)f, clip.Frames - 1);
            int b = Mathf.Min(a + 1, clip.Frames - 1);
            float k = f - a;
            int n = Mathf.Min(pos.Length, clip.Bones);
            for (int i = 0; i < n; i++)
            {
                int ia = a * clip.Bones + i, ib = b * clip.Bones + i;
                pos[i] = Vector3.LerpUnclamped(clip.Pos[ia], clip.Pos[ib], k);
                rot[i] = Quaternion.SlerpUnclamped(clip.Rot[ia], clip.Rot[ib], k);
            }
        }
    }
}
