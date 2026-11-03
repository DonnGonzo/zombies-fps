using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Host-owned optional missions. Three slots, each packed into one network variable (see MissionData.Pack).
    /// A fresh set comes on round 1 and every 10 rounds after; a finished or failed mission is replaced at the
    /// next round's start. The squad shares progress, and a finished mission pays every player.
    ///
    /// The board also owns the props the story uses (generators, beacons and the survivor), so it draws the
    /// story's current step on them too (see StoryProgress).
    /// </summary>
    public class MissionBoard : NetworkBehaviour
    {
        public static MissionBoard Instance { get; private set; }
        /// <summary>Raised on every machine when a mission is finished, with its id.</summary>
        public static event Action<int> MissionCompleted;
        /// <summary>Raised on every machine when a mission is lost (the survivor died), with its id.</summary>
        public static event Action<int> MissionFailed;

        /// <summary>Host: where zombies are drawn to besides players (a survivor being protected), if anywhere.</summary>
        public static Vector3? Lure { get; private set; }

        public NetworkVariable<ulong> Slot0 = new NetworkVariable<ulong>(MissionData.Empty);
        public NetworkVariable<ulong> Slot1 = new NetworkVariable<ulong>(MissionData.Empty);
        public NetworkVariable<ulong> Slot2 = new NetworkVariable<ulong>(MissionData.Empty);
        /// <summary>The round the current set was handed out on (0 = none yet).</summary>
        public NetworkVariable<int> SetRound = new NetworkVariable<int>(0);
        /// <summary>Health of the survivor in a protect mission.</summary>
        public NetworkVariable<float> SurvivorHealth = new NetworkVariable<float>(0f);

        Survivor _survivor;
        bool _dealtAfterStory;
        readonly Collider[] _near = new Collider[48];
        readonly HashSet<NetZombie> _nearZombies = new HashSet<NetZombie>();

        readonly List<int> _pool = new List<int>();
        readonly List<int> _zones = new List<int>();
        readonly float[] _holdTime = new float[MissionData.SlotCount];

        static MissionData Data => GameConfig.Missions;

        public ulong SlotValue(int slot) => slot == 0 ? Slot0.Value : slot == 1 ? Slot1.Value : slot == 2 ? Slot2.Value : MissionData.Empty;
        NetworkVariable<ulong> SlotVariable(int slot) => slot == 0 ? Slot0 : slot == 1 ? Slot1 : Slot2;

        public MissionDef DefIn(int slot) => Data != null ? Data.At(MissionData.IdOf(SlotValue(slot))) : null;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Slot0.OnValueChanged += OnSlotChanged;
            Slot1.OnValueChanged += OnSlotChanged;
            Slot2.OnValueChanged += OnSlotChanged;
            SurvivorHealth.OnValueChanged += OnSurvivorHealthChanged;
            _survivor = Survivor.Create();
            ApplyGenerators();
        }

        void OnSurvivorHealthChanged(float previous, float current)
        {
            if (current < previous - 0.5f && _survivor != null) _survivor.Hurt();
        }

        /// <summary>The protect mission in progress, or -1.</summary>
        public int ActiveSurvivorSlot
        {
            get
            {
                for (int i = 0; i < MissionData.SlotCount; i++)
                {
                    var def = DefIn(i);
                    ulong slot = SlotValue(i);
                    if (def != null && def.objective == MissionObjective.ProtectSurvivor && !MissionData.DoneOf(slot) && !MissionData.FailedOf(slot)) return i;
                }
                return -1;
            }
        }

        /// <summary>The zone the survivor is in: a protect mission's, else the story's (the Defector), else -1.</summary>
        public int SurvivorZone
        {
            get
            {
                int slot = ActiveSurvivorSlot;
                if (slot >= 0) return MissionData.ZoneOf(SlotValue(slot));
                var story = StoryProgress.Instance;
                return story != null ? story.SurvivorZone : -1;
            }
        }

        /// <summary>Where the survivor is, if a protect mission (or the story's) is running.</summary>
        public bool TryGetSurvivor(out Vector3 position)
        {
            int zone = SurvivorZone;
            position = zone >= 0 ? ZoneMap.SurvivorSpot(zone) : Vector3.zero;
            return zone >= 0;
        }

        public override void OnNetworkDespawn()
        {
            Slot0.OnValueChanged -= OnSlotChanged;
            Slot1.OnValueChanged -= OnSlotChanged;
            Slot2.OnValueChanged -= OnSlotChanged;
            SurvivorHealth.OnValueChanged -= OnSurvivorHealthChanged;
            if (_survivor != null) Destroy(_survivor.gameObject);
            Lure = null;
            if (Instance == this) Instance = null;
        }

        void OnSlotChanged(ulong previous, ulong current) => ApplyGenerators();

        /// <summary>Every machine: redraws the props after the story moved on.</summary>
        public void RefreshProps() => ApplyGenerators();

        /// <summary>
        /// Lights the generator of each hold mission: amber while it's being worked on, green once done. Generators
        /// the story has already started stay green; the story's current one is amber.
        /// </summary>
        void ApplyGenerators()
        {
            if (_survivor != null)
            {
                if (TryGetSurvivor(out var at))
                    _survivor.Show(at, ZoneMap.ZoneCenter(SurvivorZone) - at);
                else _survivor.Hide();
            }
            var map = GameConfig.Map;
            if (map == null || map.Generators == null) return;
            foreach (var g in map.Generators) g.SetState(GeneratorState.Idle, 0f);
            if (map.Beacons != null)
                foreach (var g in map.Beacons) g.SetState(GeneratorState.Idle, 0f);
            var story = StoryProgress.Instance;
            int storyStep = story != null ? story.Step.Value : 0;
            for (int s = 0; s < storyStep && s < StoryData.StepCount; s++)
            {
                var done = StoryData.StepAt(s);
                if (done.goal == StoryGoal.Hold && done.zone >= 0 && done.zone < map.Generators.Length) map.Generators[done.zone].SetState(GeneratorState.Done, 1f);
            }
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var props = PropsFor(DefIn(i), map);
                if (props == null) continue;
                ulong slot = SlotValue(i);
                int at = MissionData.ZoneOf(slot);
                if (at < 0 || at >= props.Length) continue;
                float progress = MissionData.TargetOf(slot) > 0 ? (float)MissionData.ProgressOf(slot) / MissionData.TargetOf(slot) : 0f;
                props[at].SetState(MissionData.DoneOf(slot) ? GeneratorState.Done : GeneratorState.Active, progress);
            }
            if (story != null && story.TryGetProp(map, out var prop)) prop.SetState(GeneratorState.Active, story.Fraction);
        }

        /// <summary>The props a hold mission uses: zone generators, or high-spot beacons. Null for other missions.</summary>
        static Generator[] PropsFor(MissionDef def, ZoneMap map)
        {
            if (def == null || map == null) return null;
            if (def.objective == MissionObjective.HoldGenerator) return map.Generators;
            if (def.objective == MissionObjective.HoldHighGround) return map.Beacons;
            return null;
        }

        // ---------------------------------------------------------------- server

        void Update()
        {
            if (!IsServer || Data == null) return;
            var rounds = RoundManager.Instance;
            Lure = TryGetSurvivor(out var lure) ? lure : (Vector3?)null;
            // Holding only counts while the horde is coming.
            if (rounds == null || rounds.Phase.Value != GamePhase.RoundActive) return;
            ProtectTick();
            var map = GameConfig.Map;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = DefIn(i);
                ulong slot = SlotValue(i);
                var props = PropsFor(def, map);
                if (props == null || MissionData.DoneOf(slot)) continue;
                int at = MissionData.ZoneOf(slot);
                // The beacon only counts from up on the high spot, not from the floor below it.
                bool level = def.objective == MissionObjective.HoldHighGround;
                if (at >= props.Length || !AnyoneNear(props[at].transform.position, level)) continue;
                _holdTime[i] += Time.deltaTime;
                while (_holdTime[i] >= 1f)
                {
                    _holdTime[i] -= 1f;
                    Advance(i, 1);
                }
            }
        }

        /// <summary>Host: the survivor counts down while alive; zombies in reach hurt them. The story's survivor too.</summary>
        void ProtectTick()
        {
            int i = ActiveSurvivorSlot;
            var story = StoryProgress.Instance;
            if (!Lure.HasValue || (i < 0 && (story == null || story.SurvivorZone < 0))) return;
            Vector3 at = Lure.Value;
            _nearZombies.Clear();
            int n = Physics.OverlapSphereNonAlloc(at + Vector3.up, Data.survivorReach, _near, ~0, QueryTriggerInteraction.Ignore);
            float dps = 0f;
            for (int c = 0; c < n; c++)
            {
                var hb = _near[c].GetComponent<ZombiesFPS.Combat.Hitbox>();
                if (hb == null || !(hb.Owner is NetZombie z) || !z.IsAlive || !_nearZombies.Add(z)) continue;
                var zd = z.Data;
                dps += zd.damage / Mathf.Max(0.5f, zd.attackWindup + zd.attackRecovery) * Data.survivorDamageShare;
            }
            if (dps > 0f)
            {
                SurvivorHealth.Value = Mathf.Max(0f, SurvivorHealth.Value - dps * Time.deltaTime);
                if (SurvivorHealth.Value <= 0f)
                {
                    if (i < 0)
                    {
                        story.ServerSurvivorDied();
                        return;
                    }
                    ulong slot = SlotValue(i);
                    SlotVariable(i).Value = MissionData.Pack(MissionData.IdOf(slot), false, MissionData.ZoneOf(slot), MissionData.ProgressOf(slot), MissionData.TargetOf(slot), failed: true);
                    MissionFailedRpc(MissionData.IdOf(slot));
                    return;
                }
            }
            if (i < 0)
            {
                story.ServerSurvivorTick(Time.deltaTime);
                return;
            }
            _holdTime[i] += Time.deltaTime;
            while (_holdTime[i] >= 1f)
            {
                _holdTime[i] -= 1f;
                Advance(i, 1);
            }
        }

        /// <summary>Host: a fresh survivor (full health) for the story's protect step.</summary>
        public void ServerHealSurvivor()
        {
            if (IsServer && Data != null) SurvivorHealth.Value = Data.survivorHealth;
        }

        [Rpc(SendTo.ClientsAndHost)]
        void MissionFailedRpc(int id) => MissionFailed?.Invoke(id);

        /// <summary>Host: a living player within holding distance of a prop (level: on the same floor as it).</summary>
        public static bool AnyoneNear(Vector3 position, bool level = false)
        {
            float r = Data != null ? Data.generatorRadius : 3f;
            foreach (var p in NetPlayer.All)
            {
                if (!p.IsAlive) continue;
                Vector3 d = p.transform.position - position;
                if (level && Mathf.Abs(d.y) > 1.5f) continue;
                d.y = 0f;
                if (d.sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        /// <summary>
        /// Host only. A round started: a new set in a new mission window (also after skipping rounds, and once
        /// after the story ends, for the Trader's bounties); otherwise every finished or failed job is replaced.
        /// </summary>
        public void ServerOnRoundStarted(int round)
        {
            if (!IsServer || Data == null) return;
            int setRound = Data.SetStartRound(round);
            bool storyOver = StoryProgress.Instance != null && StoryProgress.Instance.Complete;
            if (setRound != SetRound.Value || (storyOver && !_dealtAfterStory))
            {
                _dealtAfterStory = storyOver;
                DealSet(round, setRound);
                return;
            }
            bool any = false;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                ulong slot = SlotValue(i);
                if (MissionData.IdOf(slot) == MissionData.NoMission || MissionData.DoneOf(slot) || MissionData.FailedOf(slot))
                {
                    if (!any) BuildPool(round);
                    any = true;
                    DealSlot(i, round);
                }
            }
        }

        /// <summary>Every mission that can be handed out now, shuffled; after the story, the Trader's bounties first.</summary>
        void BuildPool(int round)
        {
            _pool.Clear();
            var story = StoryProgress.Instance;
            bool storyOver = story != null && story.Complete;
            // No generator jobs until the power's on: starting the facility's generator is the opening story step.
            bool powered = MapState.Instance == null || MapState.Instance.IsPowered;
            for (int id = 0; id < Data.Count; id++)
                if (Data.IsEligible(id, round, k => GameConfig.ZombieType(k).firstRound, storyOver) && (powered || Data.At(id).objective != MissionObjective.HoldGenerator))
                    _pool.Add(id);
            for (int i = _pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_pool[i], _pool[j]) = (_pool[j], _pool[i]);
            }
            if (!storyOver) return;
            int moved = 0;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!Data.At(_pool[i]).afterStory) continue;
                int id = _pool[i];
                _pool.RemoveAt(i);
                _pool.Insert(moved++, id);
            }
        }

        void DealSet(int round, int setRound)
        {
            SetRound.Value = setRound;
            for (int i = 0; i < MissionData.SlotCount; i++) SlotVariable(i).Value = MissionData.Empty;
            BuildPool(round);
            for (int i = 0; i < MissionData.SlotCount; i++) DealSlot(i, round);
        }

        /// <summary>Puts the first mission from the pool that isn't already on the board (or just finished there) in a slot.</summary>
        void DealSlot(int i, int round)
        {
            _holdTime[i] = 0f;
            int previous = MissionData.IdOf(SlotValue(i));
            var story = StoryProgress.Instance;
            // Only one survivor at a time, and the story's Defector comes first.
            bool survivorBusy = (ActiveSurvivorSlot >= 0 && ActiveSurvivorSlot != i) || (story != null && story.WantsSurvivor);
            int pick = -1;
            for (int n = 0; n < _pool.Count && pick < 0; n++)
            {
                int id = _pool[n];
                if (id == previous || OnBoard(id, i)) continue;
                if (survivorBusy && Data.At(id).objective == MissionObjective.ProtectSurvivor) continue;
                pick = id;
            }
            if (pick < 0)
            {
                SlotVariable(i).Value = MissionData.Empty;
                return;
            }
            _pool.Remove(pick);
            var def = Data.At(pick);
            int openMask = MapState.Instance != null ? MapState.Instance.OpenDoorMask : 0;
            int zone = def.objective == MissionObjective.HoldHighGround ? PickHighSpot(openMask)
                : MissionData.IsPlaced(def.objective) ? PickGeneratorZone(openMask) : 0;
            if (def.objective == MissionObjective.ProtectSurvivor) SurvivorHealth.Value = Data.survivorHealth;
            SlotVariable(i).Value = MissionData.Pack(pick, false, zone, 0, def.TargetFor(round));
        }

        /// <summary>Whether a mission is in another slot already.</summary>
        bool OnBoard(int id, int except)
        {
            for (int s = 0; s < MissionData.SlotCount; s++)
                if (s != except && MissionData.IdOf(SlotValue(s)) == id) return true;
            return false;
        }

        /// <summary>
        /// A generator in a zone that's already open, so every hold mission can be done without buying a door. Not
        /// the one the story wants held, and not the survivor's room.
        /// </summary>
        int PickGeneratorZone(int openMask)
        {
            _zones.Clear();
            var story = StoryProgress.Instance;
            int storyZone = story != null ? story.HoldZone : -1;
            for (int z = 0; z < ZoneMap.ZoneCount; z++)
                if (ZoneMap.IsZoneOpen(z, openMask) && z != storyZone) _zones.Add(z);
            return _zones.Count > 0 ? _zones[UnityEngine.Random.Range(0, _zones.Count)] : ZoneMap.StartZone;
        }

        /// <summary>A high spot in a zone that's open if there is one; otherwise any, so the mission is a reason to open doors.</summary>
        int PickHighSpot(int openMask)
        {
            _zones.Clear();
            for (int s = 0; s < ZoneMap.HighSpotCount; s++)
                if (ZoneMap.IsZoneOpen(ZoneMap.HighSpotZones[s], openMask)) _zones.Add(s);
            return _zones.Count > 0 ? _zones[UnityEngine.Random.Range(0, _zones.Count)] : UnityEngine.Random.Range(0, ZoneMap.HighSpotCount);
        }

        /// <summary>Host only. A zombie died to a player.</summary>
        public void ServerOnKill(NetZombie zombie, bool headshot, bool throughAnother = false)
        {
            if (!IsServer || Data == null) return;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = DefIn(i);
                if (def == null) continue;
                bool counts = def.objective == MissionObjective.Kills
                    || (def.objective == MissionObjective.HeadshotKills && headshot)
                    || (def.objective == MissionObjective.KillType && zombie.Kind == def.zombieKind)
                    || (def.objective == MissionObjective.PierceKills && throughAnother);
                if (counts) Advance(i, 1);
            }
        }

        /// <summary>Host only. Someone went down: any no-downs streak starts again.</summary>
        public void ServerOnPlayerDowned()
        {
            if (!IsServer || Data == null) return;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = DefIn(i);
                ulong slot = SlotValue(i);
                if (def == null || def.objective != MissionObjective.NoDowns || MissionData.DoneOf(slot)) continue;
                SlotVariable(i).Value = MissionData.Pack(MissionData.IdOf(slot), false, MissionData.ZoneOf(slot), 0, MissionData.TargetOf(slot));
            }
        }

        /// <summary>Host only. A round was cleared.</summary>
        public void ServerOnRoundCompleted()
        {
            if (!IsServer || Data == null) return;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = DefIn(i);
                if (def != null && def.objective == MissionObjective.NoDowns) Advance(i, 1);
            }
        }

        void Advance(int i, int amount)
        {
            ulong slot = SlotValue(i);
            if (MissionData.DoneOf(slot) || MissionData.FailedOf(slot) || MissionData.IdOf(slot) == MissionData.NoMission) return;
            int progress = MissionData.ProgressOf(slot) + amount;
            int target = MissionData.TargetOf(slot);
            bool done = progress >= target;
            SlotVariable(i).Value = MissionData.Pack(MissionData.IdOf(slot), done, MissionData.ZoneOf(slot), Mathf.Min(progress, target), target);
            if (done) Reward(MissionData.IdOf(slot));
        }

        void Reward(int id)
        {
            var def = Data.At(id);
            long points = def.PointsFor(SetRound.Value);
            foreach (var p in NetPlayer.All)
            {
                p.Points.Value += points;
                if (GameConfig.Shop != null) p.ServerAddXp(GameConfig.Shop.xpPerMission);
                if (def.bonus == MissionBonus.MaxAmmo) p.ServerRefillAllAmmo();
                else if (def.bonus == MissionBonus.PowerUpReroll) p.PowerRerolls.Value++;
                else if (def.bonus == MissionBonus.RareWeapon) p.ServerGiveRewardGun(GameConfig.GunId(GameConfig.Prices.RareGun(0)));
            }
            MissionCompletedRpc(id);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void MissionCompletedRpc(int id) => MissionCompleted?.Invoke(id);

        /// <summary>Host only. No missions until round 1 starts again (a fresh game).</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                SlotVariable(i).Value = MissionData.Empty;
                _holdTime[i] = 0f;
            }
            SetRound.Value = 0;
            SurvivorHealth.Value = 0f;
            Lure = null;
            _dealtAfterStory = false;
        }
    }
}
