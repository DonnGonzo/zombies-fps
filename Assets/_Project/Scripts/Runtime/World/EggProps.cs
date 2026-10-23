using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The easter egg's living props on every machine, from step two on: the four relics where they lie and on
    /// the pedestals, the spark on the Archives altar with its column of mana, Sergeant Brannock, the spark in the
    /// binding stones and the bleeding portal they open, the spark and heart on the blood altar, Azhrakel rising
    /// out of his pit, Ashgrave on his throne, the two choice stones' glow and the ways back up. EasterEgg tells it
    /// what to show from its network state (see Show); all of it is colliderless.
    /// </summary>
    public class EggProps : MonoBehaviour
    {
        /// <summary>What's showing, worked out by EasterEgg from its network variables.</summary>
        public struct View
        {
            public bool[] relicOnFloor, relicPlaced;
            /// <summary>0 hidden, else the altar's spark is there; charge is 0-1 while the ritual runs.</summary>
            public bool altarSpark, ritual;
            public float charge;
            public bool ritualPaused;
            public bool watchman;
            public float watchmanHurt;
            public bool stonesSpark, stonesAwake, portal;
            public bool altarItems, god, godRising, choices;
            public bool general, generalStanding;
        }

        static readonly Color RelicGlow = new Color(1f, 0.82f, 0.45f);
        static readonly Color HeartColor = new Color(0.9f, 0.25f, 0.18f);

        readonly Transform[] _relics = new Transform[EasterEggData.RelicCount];
        readonly Transform[] _placed = new Transform[EasterEggData.RelicCount];
        Transform _altarSpark, _column, _stonesSpark, _portal, _altarItems, _choiceFight, _choiceServe;
        Light _altarLight, _stonesLight, _portalLight;
        Material _columnMat, _portalMat;
        float _columnHalf = 3f;
        Figure _watchman, _god, _general;
        AudioSource _hum;
        View _view;
        float _godRiseAt = -100f, _standAt = -100f;
        bool _wasRising, _wasStanding;

        /// <summary>
        /// Sergeant Brannock's character model. Set this before the match starts, or put a prefab at
        /// Resources/<see cref="WatchmanModelPath"/>, and it replaces the blocky stand-in: feet at the prefab's origin,
        /// facing +Z, about 1.8 m tall. An Animator on it gets a float "Speed" (metres a second) if it has one.
        /// </summary>
        public static GameObject WatchmanModel;
        public const string WatchmanModelPath = "Characters/Brannock";
        /// <summary>The supplied animated old mage (ImportedModels), who stands in for Brannock when it's in the project.</summary>
        public const string MageModel = "stylized-old-mage-animated-character";

        Vector3 _watchmanAt, _watchmanFacing = Vector3.back;
        float _watchmanSpeed;

        public static EggProps Build(Transform parent)
        {
            var go = new GameObject("Easter Egg Props");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<EggProps>();
            p.Make();
            return p;
        }

        void Make()
        {
            for (int i = 0; i < EasterEggData.RelicCount; i++)
            {
                _relics[i] = Relic(i, ZoneMap.RelicSpot(i) + Vector3.up * 0.75f, true);
                _placed[i] = Relic(i, ZoneMap.Pedestal(i) + Vector3.up * (ZoneMap.PedestalHeight + 0.25f), false);
            }

            var altar = ZoneMap.ArchivesAltar + Vector3.up * (ZoneMap.AltarHeight + 0.35f);
            _altarSpark = Spark("Altar Spark", altar, ZoneMap.ManaColor);
            _altarLight = PointLight("Altar Light", _altarSpark, ZoneMap.ManaColor, 9f);
            _columnMat = new Material(Greybox.Unlit(new Color(ZoneMap.ManaColor.r, ZoneMap.ManaColor.g, ZoneMap.ManaColor.b, 0.18f)));
            // From the altar's top up to the ceiling.
            float bottom = ZoneMap.ArchivesAltar.y + ZoneMap.AltarHeight;
            _columnHalf = (ZoneMap.WallHeight - bottom) / 2f;
            var column = Greybox.Prim(PrimitiveType.Cylinder, "Mana Column", transform, new Vector3(ZoneMap.ArchivesAltar.x, bottom + _columnHalf, ZoneMap.ArchivesAltar.z), new Vector3(1.2f, _columnHalf, 1.2f), Color.white, collider: false);
            column.GetComponent<Renderer>().sharedMaterial = _columnMat;
            Greybox.DisableShadows(column);
            _column = column.transform;

            _watchman = Figure.Watchman(transform, ZoneMap.WatchmanSpot);
            // The supplied old mage first; then the built Brannock, then the packed one.
            var model = ImportedModels.Get(MageModel);
            if (model == null) model = WatchmanModel;
            if (model == null) model = Resources.Load<GameObject>(WatchmanModelPath);
            if (model != null)
            {
                _watchman.UseModel(model);
                var spell = ImportedModels.Get("wizard-spell");
                if (spell != null)
                {
                    var ring = Object.Instantiate(spell, _watchman.Root, false);
                    ring.transform.localScale = Vector3.one * 2f;
                    ring.transform.localPosition = Vector3.up * 0.025f;
                }
            }
            _watchmanAt = ZoneMap.WatchmanSpot;

            _stonesSpark = Spark("Stones Spark", ZoneMap.StonesCenter + Vector3.up * 1.0f, ZoneMap.ManaColor);
            _stonesLight = PointLight("Stones Light", _stonesSpark, ZoneMap.ManaColor, 10f);
            _portalMat = new Material(Greybox.Unlit(new Color(ZoneMap.BloodGlow.r, ZoneMap.BloodGlow.g * 0.6f, ZoneMap.BloodGlow.b, 0.55f)));
            _portal = Greybox.Empty("Bleeding Stones", transform, ZoneMap.StonesCenter);
            var pool = Greybox.Prim(PrimitiveType.Cylinder, "Blood Pool", _portal, new Vector3(0f, 0.64f, 0f), new Vector3(2.6f, 0.01f, 2.6f), Color.white, collider: false);
            pool.GetComponent<Renderer>().sharedMaterial = _portalMat;
            var swirl = Greybox.Prim(PrimitiveType.Cylinder, "Portal Swirl", _portal, new Vector3(0f, 2.2f, 0f), new Vector3(1.4f, 1.6f, 1.4f), Color.white, collider: false);
            swirl.GetComponent<Renderer>().sharedMaterial = _portalMat;
            Greybox.DisableShadows(_portal.gameObject);
            _portalLight = PointLight("Portal Light", _portal, ZoneMap.BloodGlow, 12f);
            _portalLight.transform.localPosition = Vector3.up * 2f;

            _altarItems = new GameObject("Altar Offerings").transform;
            _altarItems.SetParent(transform, false);
            _altarItems.localPosition = ZoneMap.BloodAltar + Vector3.up * 1.5f;
            Spark("Offered Spark", _altarItems.position + new Vector3(-0.45f, 0f, 0f), ZoneMap.ManaColor).SetParent(_altarItems, true);
            Heart(_altarItems.position + new Vector3(0.45f, 0f, 0f)).SetParent(_altarItems, true);
            PointLight("Offering Light", _altarItems, ZoneMap.BloodGlow, 8f);

            _god = Figure.God(transform, ZoneMap.GodSpot);
            _general = Figure.General(transform, ZoneMap.ThroneSpot);
            // Ivan's models where they're in the project: the Mutant in red for the god, Warrok for his general.
            _god.UseModel(ZombieModel.Mutant, "Red", 5.4f);
            _general.UseModel(ZombieModel.Warrok, null, 2.3f);

            _choiceFight = ChoiceGlow(ZoneMap.FightSpot);
            _choiceServe = ChoiceGlow(ZoneMap.ServeSpot);
            ExitGlow(ZoneMap.SanctumExitSpot);
            ExitGlow(ZoneMap.KeepExitSpot);

            // On its own object: moving it to the altar or the stones must not carry every prop (and Brannock) with it.
            var humAt = new GameObject("Egg Hum").transform;
            humAt.SetParent(transform, false);
            _hum = GameSettings.Effect(humAt.gameObject.AddComponent<AudioSource>());
            _hum.clip = ProceduralAudio.ReactorSurge;
            _hum.loop = true;
            _hum.spatialBlend = 0.9f;
            _hum.minDistance = 4f;
            _hum.maxDistance = 30f;
            _hum.rolloffMode = AudioRolloffMode.Linear;
            _hum.playOnAwake = false;

            _view = new View { relicOnFloor = new bool[EasterEggData.RelicCount], relicPlaced = new bool[EasterEggData.RelicCount] };
            Apply();
        }

        // ---------------------------------------------------------------- pieces

        Transform Relic(int i, Vector3 at, bool loose)
        {
            var root = Greybox.Empty(EasterEggData.RelicNames[i], transform, at);
            var gold = new Color(0.75f, 0.58f, 0.22f);
            var silver = new Color(0.75f, 0.77f, 0.8f);
            switch (i)
            {
                case 0: // The Watch Chalice.
                    Greybox.Prim(PrimitiveType.Cylinder, "Foot", root, new Vector3(0f, -0.16f, 0f), new Vector3(0.16f, 0.015f, 0.16f), gold, collider: false, surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "Stem", root, new Vector3(0f, -0.08f, 0f), new Vector3(0.04f, 0.08f, 0.04f), gold, collider: false, surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Sphere, "Cup", root, new Vector3(0f, 0.04f, 0f), new Vector3(0.2f, 0.16f, 0.2f), gold, collider: false, surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "Wine", root, new Vector3(0f, 0.1f, 0f), new Vector3(0.17f, 0.005f, 0.17f), new Color(0.5f, 0.05f, 0.08f), collider: false, unlit: true);
                    break;
                case 1: // The Silver Bell.
                    Greybox.Prim(PrimitiveType.Sphere, "Bell", root, Vector3.zero, new Vector3(0.22f, 0.26f, 0.22f), silver, collider: false, surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "Lip", root, new Vector3(0f, -0.1f, 0f), new Vector3(0.26f, 0.02f, 0.26f), silver, collider: false, surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "Handle", root, new Vector3(0f, 0.19f, 0f), new Vector3(0.05f, 0.07f, 0.05f), new Color(0.3f, 0.2f, 0.12f), collider: false, surface: Surface.Wood, moving: true);
                    break;
                case 2: // The Brass Lamp.
                    Greybox.Prim(PrimitiveType.Sphere, "Body", root, Vector3.zero, new Vector3(0.24f, 0.13f, 0.16f), gold, collider: false, surface: Surface.Metal, moving: true);
                    Greybox.Box("Spout", root, new Vector3(0.15f, 0.03f, 0f), new Vector3(0.14f, 0.04f, 0.05f), gold, collider: false, surface: Surface.Metal, moving: true);
                    Greybox.Prim(PrimitiveType.Sphere, "Flame", root, new Vector3(0.23f, 0.08f, 0f), new Vector3(0.05f, 0.09f, 0.05f), new Color(1f, 0.75f, 0.35f), collider: false, unlit: true);
                    Greybox.Prim(PrimitiveType.Cylinder, "Lid", root, new Vector3(0f, 0.07f, 0f), new Vector3(0.08f, 0.02f, 0.08f), gold, collider: false, surface: Surface.Metal, moving: true);
                    break;
                default: // The Star Tablet.
                    Greybox.Box("Tablet", root, Vector3.zero, new Vector3(0.24f, 0.3f, 0.05f), new Color(0.35f, 0.33f, 0.3f), collider: false, surface: Surface.Concrete, moving: true);
                    for (int k = 0; k < 4; k++)
                        Greybox.Prim(PrimitiveType.Cube, "Star", root, new Vector3(0f, 0f, 0.03f), new Vector3(0.025f, 0.16f, 0.005f), RelicGlow, collider: false, unlit: true)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, k * 45f);
                    break;
            }
            var halo = Greybox.Prim(PrimitiveType.Sphere, "Halo", root, Vector3.zero, Vector3.one * 0.5f, new Color(RelicGlow.r, RelicGlow.g, RelicGlow.b, loose ? 0.18f : 0.1f), collider: false, unlit: true);
            Greybox.DisableShadows(halo);
            PointLight("Relic Light", root, RelicGlow, loose ? 5f : 3f).intensity = loose ? 1.4f : 0.8f;
            root.gameObject.SetActive(false);
            return root;
        }

        Transform Spark(string name, Vector3 at, Color color)
        {
            var root = Greybox.Empty(name, transform, at);
            Greybox.Prim(PrimitiveType.Sphere, "Core", root, Vector3.zero, Vector3.one * 0.16f, Color.Lerp(color, Color.white, 0.6f), collider: false, unlit: true);
            var shell = Greybox.Prim(PrimitiveType.Sphere, "Shell", root, Vector3.zero, Vector3.one * 0.34f, new Color(color.r, color.g, color.b, 0.3f), collider: false, unlit: true);
            var cage = Greybox.Prim(PrimitiveType.Cylinder, "Cage", root, Vector3.zero, new Vector3(0.36f, 0.01f, 0.36f), new Color(0.6f, 0.45f, 0.2f), collider: false, surface: Surface.Metal, moving: true);
            cage.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Greybox.DisableShadows(shell);
            root.gameObject.SetActive(false);
            return root;
        }

        Transform Heart(Vector3 at)
        {
            var root = Greybox.Empty("Elemental Heart", transform, at);
            Greybox.Prim(PrimitiveType.Sphere, "Heart", root, Vector3.zero, new Vector3(0.26f, 0.3f, 0.24f), HeartColor, collider: false, unlit: true);
            for (int k = 0; k < 5; k++)
                Greybox.Box("Root", root, Quaternion.Euler(0f, k * 72f, 0f) * new Vector3(0.12f, -0.05f, 0f), new Vector3(0.18f, 0.025f, 0.025f), new Color(0.2f, 0.3f, 0.1f), collider: false, surface: Surface.Wood, moving: true)
                    .transform.localRotation = Quaternion.Euler(0f, k * 72f, -25f);
            return root;
        }

        Transform ChoiceGlow(Vector3 p)
        {
            var root = Greybox.Empty("Choice Glow", transform, p);
            var ring = Greybox.Prim(PrimitiveType.Cylinder, "Ring", root, new Vector3(0f, 0.08f, 0f), new Vector3(1.9f, 0.01f, 1.9f), new Color(1f, 0.3f, 0.15f, 0.5f), collider: false, unlit: true);
            Greybox.DisableShadows(ring);
            PointLight("Choice Light", root, new Color(1f, 0.35f, 0.15f), 6f).transform.localPosition = new Vector3(0f, 2.4f, 0f);
            root.gameObject.SetActive(false);
            return root;
        }

        void ExitGlow(Vector3 p)
        {
            var sheet = Greybox.Box("Exit Light", transform, p + Vector3.up * 1.6f + Vector3.forward * 0.05f, new Vector3(2.2f, 3.1f, 0.02f), new Color(0.6f, 0.8f, 1f, 0.4f), collider: false);
            sheet.GetComponent<Renderer>().sharedMaterial = Greybox.Unlit(new Color(0.6f, 0.8f, 1f, 0.4f));
            Greybox.DisableShadows(sheet);
            PointLight("Exit Glow", sheet.transform, new Color(0.6f, 0.8f, 1f), 7f);
        }

        Light PointLight(string name, Transform parent, Color color, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;
            return l;
        }

        // ---------------------------------------------------------------- state

        public void Show(View v)
        {
            if (v.godRising && !_wasRising) _godRiseAt = Time.time;
            if (v.generalStanding && !_wasStanding) _standAt = Time.time;
            _wasRising = v.godRising;
            _wasStanding = v.generalStanding;
            _view = v;
            Apply();
        }

        /// <summary>The ritual's progress, every frame while it runs.</summary>
        public void SetCharge(float charge, bool paused)
        {
            _view.charge = charge;
            _view.ritualPaused = paused;
        }

        /// <summary>Where Brannock is now (he runs about during the ambush). The figure follows smoothly.</summary>
        public void SetWatchmanAt(Vector3 at) => _watchmanAt = at;

        /// <summary>Flashes the Watchman white while he's being hit.</summary>
        public void SetWatchmanHurt(float hurt) => _view.watchmanHurt = hurt;

        void Apply()
        {
            var v = _view;
            for (int i = 0; i < EasterEggData.RelicCount; i++)
            {
                Set(_relics[i], v.relicOnFloor != null && v.relicOnFloor[i]);
                Set(_placed[i], v.relicPlaced != null && v.relicPlaced[i]);
            }
            Set(_altarSpark, v.altarSpark);
            Set(_column, v.ritual);
            _watchman.Root.gameObject.SetActive(v.watchman);
            Set(_stonesSpark, v.stonesSpark);
            Set(_portal, v.portal);
            Set(_altarItems, v.altarItems);
            _god.Root.gameObject.SetActive(v.god);
            _general.Root.gameObject.SetActive(v.general);
            Set(_choiceFight, v.choices);
            Set(_choiceServe, v.choices);
            bool hum = v.ritual || v.stonesAwake || v.portal;
            if (hum && !_hum.isPlaying)
            {
                _hum.transform.position = v.ritual ? ZoneMap.ArchivesAltar : ZoneMap.StonesCenter;
                _hum.Play();
            }
            else if (!hum && _hum.isPlaying) _hum.Stop();
        }

        static void Set(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < EasterEggData.RelicCount; i++)
            {
                if (_relics[i].gameObject.activeSelf)
                {
                    _relics[i].localRotation = Quaternion.Euler(0f, t * 50f + i * 90f, 0f);
                    _relics[i].localPosition = ZoneMap.RelicSpot(i) + Vector3.up * (0.75f + Mathf.Sin(t * 1.8f + i) * 0.06f);
                }
                if (_placed[i].gameObject.activeSelf)
                    _placed[i].localRotation = Quaternion.Euler(0f, t * (_view.ritual ? 120f : 20f) + i * 90f, 0f);
            }
            if (_altarSpark.gameObject.activeSelf)
            {
                float charge = _view.ritual ? _view.charge : 0.15f;
                float pulse = 1f + Mathf.Sin(t * (4f + charge * 8f)) * 0.08f;
                _altarSpark.localScale = Vector3.one * (0.8f + charge * 0.8f) * pulse;
                _altarSpark.localRotation = Quaternion.Euler(t * 40f, t * 70f, 0f);
                _altarLight.intensity = (_view.ritualPaused ? 0.6f : 1f) * (0.8f + charge * 3.2f) * pulse;
            }
            if (_column.gameObject.activeSelf)
            {
                var c = _columnMat.color;
                c.a = (_view.ritualPaused ? 0.05f : 0.08f + 0.18f * _view.charge) * (0.85f + 0.15f * Mathf.Sin(t * 6f));
                _columnMat.color = c;
                _column.localScale = new Vector3(0.8f + 0.6f * _view.charge, _columnHalf, 0.8f + 0.6f * _view.charge);
            }
            if (_stonesSpark.gameObject.activeSelf)
            {
                _stonesSpark.localRotation = Quaternion.Euler(t * 60f, t * 90f, 0f);
                _stonesLight.intensity = (_view.stonesAwake ? 3.2f : 1.6f) + Mathf.PerlinNoise(t * 7f, 2f) * 1.2f;
            }
            if (_portal.gameObject.activeSelf)
            {
                _portal.localRotation = Quaternion.Euler(0f, t * 45f, 0f);
                var c = _portalMat.color;
                c.a = 0.45f + 0.2f * Mathf.Sin(t * 3f);
                _portalMat.color = c;
                _portalLight.intensity = 2.4f + Mathf.Sin(t * 3f) * 0.8f;
            }
            if (_altarItems.gameObject.activeSelf)
            {
                _altarItems.localPosition = ZoneMap.BloodAltar + Vector3.up * (1.6f + Mathf.Sin(t * 1.5f) * 0.05f);
                _altarItems.localRotation = Quaternion.Euler(0f, t * 25f, 0f);
            }
            if (_watchman.Root.gameObject.activeSelf) MoveWatchman(t);
            if (_god.Root.gameObject.activeSelf)
            {
                // Rises out of the ember pit over four seconds, then sways with his arms out.
                float rise = Mathf.Clamp01((t - _godRiseAt) / 4f);
                rise = rise * rise * (3f - 2f * rise);
                _god.Root.localPosition = ZoneMap.GodSpot + Vector3.down * (1f - rise) * 6f;
                // He roars as he clears the pit.
                _god.Idle(t, 0f, roar: t - _godRiseAt - 2.5f);
            }
            if (_general.Root.gameObject.activeSelf)
            {
                float stand = _view.generalStanding ? Mathf.Clamp01((t - _standAt) / 1.8f) : 0f;
                _general.Seat(1f - stand * stand * (3f - 2f * stand));
                _general.Idle(t, 0f);
            }
        }

        void MoveWatchman(float t)
        {
            var root = _watchman.Root;
            var from = root.localPosition;
            var to = _watchmanAt;
            // Far off (back to his post after the ambush, or joining late) he's simply there.
            var next = (to - from).sqrMagnitude > 6f * 6f ? to : Vector3.Lerp(from, to, 1f - Mathf.Exp(-10f * Time.deltaTime));
            var step = next - from;
            step.y = 0f;
            float speed = Time.deltaTime > 0f ? step.magnitude / Time.deltaTime : 0f;
            _watchmanSpeed = Mathf.Lerp(_watchmanSpeed, speed, 1f - Mathf.Exp(-8f * Time.deltaTime));
            root.localPosition = next;
            if (_watchmanSpeed > 0.4f && step.sqrMagnitude > 0.000001f) _watchmanFacing = step.normalized;
            else if (_watchmanSpeed < 0.2f && (_watchmanAt - ZoneMap.WatchmanSpot).sqrMagnitude < 0.25f)
                _watchmanFacing = Quaternion.Euler(0f, 160f, 0f) * Vector3.forward;
            root.localRotation = Quaternion.Slerp(root.localRotation, Quaternion.LookRotation(_watchmanFacing), 1f - Mathf.Exp(-10f * Time.deltaTime));
            _watchman.Run(t, _watchmanSpeed);
            _watchman.Idle(t, _view.watchmanHurt, _watchmanSpeed);
        }

        void OnDestroy()
        {
            if (_columnMat != null) Destroy(_columnMat);
            if (_portalMat != null) Destroy(_portalMat);
        }

        // ---------------------------------------------------------------- figures

        /// <summary>A blocky person (or god) built from primitives, with simple idle and seated poses.</summary>
        sealed class Figure
        {
            public Transform Root, Body, Head, ArmL, ArmR, LegL, LegR;
            float _scale;
            readonly List<Renderer> _renderers = new List<Renderer>();
            Material[][] _materials, _whites;
            Material _white;
            Transform _model;
            Animator _animator;
            ZombieModel _animated;
            bool _hasSpeed, _flashing;
            float _stride;
            static readonly int SpeedParam = Animator.StringToHash("Speed");

            static Figure Make(Transform parent, string name, Vector3 at, float scale, Color coat, Color skin, Color legs, Surface coatSurface)
            {
                var f = new Figure { _scale = scale };
                f.Root = Greybox.Empty(name, parent, at);
                f.Body = Greybox.Empty("Body", f.Root, new Vector3(0f, 0.95f * scale, 0f));
                f.Body.localScale = Vector3.one * scale;
                Greybox.Prim(PrimitiveType.Capsule, "Torso", f.Body, new Vector3(0f, 0.38f, 0f), new Vector3(0.5f, 0.38f, 0.32f), coat, collider: false, surface: coatSurface, moving: true);
                f.Head = Greybox.Empty("Head", f.Body, new Vector3(0f, 0.92f, 0f));
                Greybox.Prim(PrimitiveType.Sphere, "Skull", f.Head, Vector3.zero, new Vector3(0.24f, 0.28f, 0.26f), skin, collider: false, surface: Surface.Skin, moving: true);
                f.ArmL = Greybox.Empty("ArmL", f.Body, new Vector3(-0.31f, 0.66f, 0f));
                f.ArmR = Greybox.Empty("ArmR", f.Body, new Vector3(0.31f, 0.66f, 0f));
                foreach (var arm in new[] { f.ArmL, f.ArmR })
                {
                    Greybox.Box("Arm", arm, new Vector3(0f, -0.3f, 0f), new Vector3(0.13f, 0.6f, 0.13f), coat, collider: false, surface: coatSurface, moving: true);
                    Greybox.Prim(PrimitiveType.Sphere, "Hand", arm, new Vector3(0f, -0.63f, 0f), Vector3.one * 0.11f, skin, collider: false, surface: Surface.Skin, moving: true);
                }
                f.LegL = Greybox.Empty("LegL", f.Body, new Vector3(-0.12f, 0f, 0f));
                f.LegR = Greybox.Empty("LegR", f.Body, new Vector3(0.12f, 0f, 0f));
                foreach (var leg in new[] { f.LegL, f.LegR })
                {
                    Greybox.Box("Leg", leg, new Vector3(0f, -0.45f, 0f), new Vector3(0.17f, 0.9f, 0.18f), legs, collider: false, surface: Surface.Fabric, moving: true);
                    Greybox.Box("Boot", leg, new Vector3(0f, -0.88f, 0.06f), new Vector3(0.18f, 0.14f, 0.3f), new Color(0.12f, 0.1f, 0.08f), collider: false, surface: Surface.Rubber, moving: true);
                }
                return f;
            }

            void Finish()
            {
                _white = Greybox.Unlit(Color.white);
                CaptureMaterials();
                Root.gameObject.SetActive(false);
            }

            void CaptureMaterials()
            {
                _renderers.Clear();
                (_model != null ? _model : Root).GetComponentsInChildren(true, _renderers);
                _materials = new Material[_renderers.Count][];
                _whites = new Material[_renderers.Count][];
                for (int i = 0; i < _renderers.Count; i++)
                {
                    _materials[i] = _renderers[i].sharedMaterials;
                    _whites[i] = new Material[_materials[i].Length];
                    for (int k = 0; k < _whites[i].Length; k++) _whites[i][k] = _white;
                }
            }

            /// <summary>Swaps the blocky body for a character model (see <see cref="WatchmanModel"/>).</summary>
            public void UseModel(GameObject prefab)
            {
                var go = Object.Instantiate(prefab, Root, false);
                go.name = prefab.name;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                if (prefab.name == MageModel)
                {
                    var rs = go.GetComponentsInChildren<Renderer>(); var bounds = rs[0].bounds;
                    foreach (var r in rs) bounds.Encapsulate(r.bounds);
                    go.transform.localScale *= 1.8f / Mathf.Max(0.1f, bounds.size.y);
                }
                _model = go.transform;
                _animator = go.GetComponentInChildren<Animator>();
                if (_animator != null)
                {
                    _animator.applyRootMotion = false;
                    foreach (var p in _animator.parameters)
                        if (p.nameHash == SpeedParam && p.type == AnimatorControllerParameterType.Float) _hasSpeed = true;
                }
                Body.gameObject.SetActive(false);
                CaptureMaterials();
            }

            /// <summary>
            /// Swaps the blocky body for <paramref name="kit"/>'s animated model, <paramref name="height"/> metres
            /// tall, in texture <paramref name="variant"/>. Keeps the blocky body when the kit isn't in the project.
            /// </summary>
            public void UseModel(ZombieModel.Kit kit, string variant, float height)
            {
                var model = ZombieModel.Create(Root, height, kit);
                if (model == null) return;
                model.UseVariant(variant);
                model.SetLook(Color.white, 1f);
                model.ResetLife(1u);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                _animated = model;
                _model = model.transform;
                // The blocky body goes, but not its lights (Azhrakel's ember glow).
                foreach (var r in Body.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                CaptureMaterials();
            }

            /// <summary>Legs and arms swing with his speed (the model's Animator gets the speed instead).</summary>
            public void Run(float t, float speed)
            {
                if (_model != null)
                {
                    if (_hasSpeed) _animator.SetFloat(SpeedParam, speed);
                    return;
                }
                float k = Mathf.Clamp01(speed / 4f);
                _stride += Time.deltaTime * (2f + speed * 2.2f);
                float swing = Mathf.Sin(_stride) * 50f * k;
                LegL.localRotation = Quaternion.Euler(swing, 0f, 0f);
                LegR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                Body.localRotation = Quaternion.Euler(12f * k, 0f, 0f);
                Body.localPosition = new Vector3(0f, 0.95f * _scale + Mathf.Abs(Mathf.Cos(_stride)) * 0.06f * k, 0f);
            }

            /// <summary>Sergeant Brannock: Watch-blue greatcoat, steel cap, a lantern on his belt and an old rifle.</summary>
            public static Figure Watchman(Transform parent, Vector3 at)
            {
                var f = Make(parent, "Sergeant Brannock", at, 1f, new Color(0.15f, 0.2f, 0.34f), new Color(0.72f, 0.58f, 0.48f), new Color(0.2f, 0.2f, 0.22f), Surface.Fabric);
                f.Root.localRotation = Quaternion.Euler(0f, 160f, 0f);
                Greybox.Prim(PrimitiveType.Sphere, "Helmet", f.Head, new Vector3(0f, 0.08f, 0f), new Vector3(0.29f, 0.16f, 0.31f), new Color(0.32f, 0.33f, 0.3f), collider: false, surface: Surface.Metal, moving: true);
                Greybox.Box("Beard", f.Head, new Vector3(0f, -0.1f, 0.1f), new Vector3(0.18f, 0.12f, 0.08f), new Color(0.75f, 0.74f, 0.7f), collider: false, surface: Surface.Fabric, moving: true);
                Greybox.Box("Coat Tail", f.Body, new Vector3(0f, -0.15f, 0f), new Vector3(0.48f, 0.4f, 0.3f), new Color(0.13f, 0.17f, 0.3f), collider: false, surface: Surface.Fabric, moving: true);
                Greybox.Box("Belt", f.Body, new Vector3(0f, 0.08f, 0f), new Vector3(0.51f, 0.06f, 0.33f), new Color(0.25f, 0.17f, 0.1f), collider: false, surface: Surface.Rubber, moving: true);
                Greybox.Prim(PrimitiveType.Sphere, "Belt Lantern", f.Body, new Vector3(0.24f, 0.0f, 0.12f), new Vector3(0.09f, 0.12f, 0.09f), new Color(1f, 0.75f, 0.35f), collider: false, unlit: true);
                Greybox.Box("Rifle", f.ArmR, new Vector3(0.02f, -0.55f, 0.25f), new Vector3(0.05f, 0.07f, 0.95f), new Color(0.25f, 0.18f, 0.12f), collider: false, surface: Surface.Wood, moving: true);
                f.Finish();
                return f;
            }

            /// <summary>Azhrakel: a giant of cracked black rock, fire in the cracks, horns, and eyes like furnace doors.</summary>
            public static Figure God(Transform parent, Vector3 at)
            {
                var rock = new Color(0.1f, 0.08f, 0.08f);
                var f = Make(parent, "Azhrakel", at, 2.9f, rock, rock, rock, Surface.Concrete);
                f.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var fire = ZoneMap.BloodGlow;
                for (int k = 0; k < 7; k++)
                    Greybox.Prim(PrimitiveType.Cube, "Crack", f.Body, new Vector3(Mathf.Sin(k * 2.1f) * 0.18f, 0.12f + k * 0.1f, 0.165f), new Vector3(0.04f, 0.14f, 0.01f), fire, collider: false, unlit: true)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, k * 37f);
                foreach (int s in new[] { -1, 1 })
                {
                    Greybox.Prim(PrimitiveType.Sphere, "Eye", f.Head, new Vector3(s * 0.06f, 0.02f, 0.12f), new Vector3(0.06f, 0.035f, 0.02f), new Color(1f, 0.85f, 0.3f), collider: false, unlit: true);
                    var horn = Greybox.Prim(PrimitiveType.Capsule, "Horn", f.Head, new Vector3(s * 0.13f, 0.17f, -0.02f), new Vector3(0.06f, 0.16f, 0.06f), new Color(0.2f, 0.16f, 0.14f), collider: false, surface: Surface.Concrete, moving: true);
                    horn.transform.localRotation = Quaternion.Euler(-25f, 0f, s * -35f);
                }
                var crown = new GameObject("Ember Light").AddComponent<Light>();
                crown.transform.SetParent(f.Body, false);
                crown.transform.localPosition = new Vector3(0f, 0.6f, 0.5f);
                crown.type = LightType.Point;
                crown.color = fire;
                crown.range = 6f;
                crown.intensity = 3f;
                crown.shadows = LightShadows.None;
                f.Finish();
                return f;
            }

            /// <summary>Corvin Ashgrave: tall, in blackened Watch plate, a frost-blue sash and a greatsword across his knees.</summary>
            public static Figure General(Transform parent, Vector3 at)
            {
                var plate = new Color(0.2f, 0.21f, 0.24f);
                var f = Make(parent, "Ashgrave", at, 1.15f, plate, new Color(0.62f, 0.62f, 0.68f), plate, Surface.Metal);
                f.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Greybox.Prim(PrimitiveType.Sphere, "Helm", f.Head, new Vector3(0f, 0.03f, 0f), new Vector3(0.29f, 0.32f, 0.31f), plate, collider: false, surface: Surface.Metal, moving: true);
                Greybox.Prim(PrimitiveType.Cube, "Visor", f.Head, new Vector3(0f, 0.02f, 0.14f), new Vector3(0.16f, 0.03f, 0.02f), new Color(0.6f, 0.7f, 1f), collider: false, unlit: true);
                Greybox.Box("Sash", f.Body, new Vector3(0f, 0.4f, 0.165f), new Vector3(0.12f, 0.7f, 0.01f), new Color(0.35f, 0.45f, 0.8f), collider: false, surface: Surface.Fabric, moving: true)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                foreach (int s in new[] { -1, 1 })
                    Greybox.Prim(PrimitiveType.Sphere, "Pauldron", f.Body, new Vector3(s * 0.3f, 0.7f, 0f), new Vector3(0.24f, 0.16f, 0.26f), Shade(plate, 1.3f), collider: false, surface: Surface.Metal, moving: true);
                Greybox.Box("Greatsword", f.ArmR, new Vector3(0f, -0.65f, 0.35f), new Vector3(0.04f, 0.08f, 1.2f), new Color(0.7f, 0.75f, 0.85f), collider: false, surface: Surface.Metal, moving: true);
                f.Finish();
                return f;
            }

            static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

            /// <summary>Seated (1) on the throne, or standing (0).</summary>
            public void Seat(float seated)
            {
                if (_animated != null)
                {
                    _animated.Sit(seated);
                    // Back onto the throne's seat, which is about as high as his hips.
                    _model.localPosition = new Vector3(0f, 0f, -0.35f * seated);
                    return;
                }
                Body.localPosition = new Vector3(0f, Mathf.Lerp(0.95f * _scale, 1.12f, seated), Mathf.Lerp(0f, -0.35f, seated));
                LegL.localRotation = LegR.localRotation = Quaternion.Euler(-85f * seated, 0f, 0f);
            }

            /// <param name="roar">Seconds into a roar (the animated models only; negative or long past for none).</param>
            public void Idle(float t, float hurt, float speed = 0f, float roar = 99f)
            {
                if (_animated != null)
                {
                    _animated.Animate(Time.deltaTime, speed, false, 0f, 1f, 0, roar >= 0f ? roar : 99f, false);
                    bool flashing = hurt > 0f && Mathf.Repeat(t * 12f, 1f) < 0.5f;
                    if (flashing != _flashing) _animated.SetFlash(_flashing = flashing);
                    return;
                }
                float breathe = Mathf.Sin(t * 1.6f);
                float k = Mathf.Clamp01(speed / 4f);
                float swing = Mathf.Sin(_stride) * 45f * k;
                Head.localRotation = Quaternion.Euler(breathe * 2f, Mathf.Sin(t * 0.4f) * 14f * (1f - k), 0f);
                ArmL.localRotation = Quaternion.Euler(breathe * 4f - swing, 0f, -8f);
                ArmR.localRotation = Quaternion.Euler(-breathe * 4f - 10f + swing, 0f, 8f);
                bool flash = hurt > 0f && Mathf.Repeat(t * 12f, 1f) < 0.5f;
                if (flash == _flashing) return;
                _flashing = flash;
                for (int i = 0; i < _renderers.Count; i++)
                    if (_renderers[i] != null) _renderers[i].sharedMaterials = flash ? _whites[i] : _materials[i];
            }
        }
    }
}
