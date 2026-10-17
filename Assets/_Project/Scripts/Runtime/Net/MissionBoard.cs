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
    /// A fresh set comes on round 1 and every 10 rounds after; unfinished missions just go away. The squad
    /// shares progress, and a finished mission pays every player.
    /// </summary>
    public class MissionBoard : NetworkBehaviour
    {
        public static MissionBoard Instance { get; private set; }
        /// <summary>Raised on every machine when a mission is finished, with its id.</summary>
        public static event Action<int> MissionCompleted;

        public NetworkVariable<ulong> Slot0 = new NetworkVariable<ulong>(MissionData.Empty);
        public NetworkVariable<ulong> Slot1 = new NetworkVariable<ulong>(MissionData.Empty);
        public NetworkVariable<ulong> Slot2 = new NetworkVariable<ulong>(MissionData.Empty);
        /// <summary>The round the current set was handed out on (0 = none yet).</summary>
        public NetworkVariable<int> SetRound = new NetworkVariable<int>(0);

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
            ApplyGenerators();
        }

        public override void OnNetworkDespawn()
        {
            Slot0.OnValueChanged -= OnSlotChanged;
            Slot1.OnValueChanged -= OnSlotChanged;
            Slot2.OnValueChanged -= OnSlotChanged;
            if (Instance == this) Instance = null;
        }

        void OnSlotChanged(ulong previous, ulong current) => ApplyGenerators();

        /// <summary>Lights the generator of each hold mission: amber while it's being worked on, green once done.</summary>
        void ApplyGenerators()
        {
            var map = GameConfig.Map;
            if (map == null || map.Generators == null) return;
            foreach (var g in map.Generators) g.SetState(GeneratorState.Idle, 0f);
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = DefIn(i);
                if (def == null || def.objective != MissionObjective.HoldGenerator) continue;
                ulong slot = SlotValue(i);
                int zone = MissionData.ZoneOf(slot);
                if (zone < 0 || zone >= map.Generators.Length) continue;
                float progress = MissionData.TargetOf(slot) > 0 ? (float)MissionData.ProgressOf(slot) / MissionData.TargetOf(slot) : 0f;
                map.Generators[zone].SetState(MissionData.DoneOf(slot) ? GeneratorState.Done : GeneratorState.Active, progress);
            }
        }

        // ---------------------------------------------------------------- server

        void Update()
        {
            if (!IsServer || Data == null) return;
            var rounds = RoundManager.Instance;
            // Holding only counts while the horde is coming.
            if (rounds == null || rounds.Phase.Value != GamePhase.RoundActive) return;
            var map = GameConfig.Map;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = DefIn(i);
                ulong slot = SlotValue(i);
                if (def == null || def.objective != MissionObjective.HoldGenerator || MissionData.DoneOf(slot)) continue;
                int zone = MissionData.ZoneOf(slot);
                if (map == null || zone >= map.Generators.Length || !AnyoneNear(map.Generators[zone].transform.position)) continue;
                _holdTime[i] += Time.deltaTime;
                while (_holdTime[i] >= 1f)
                {
                    _holdTime[i] -= 1f;
                    Advance(i, 1);
                }
            }
        }

        static bool AnyoneNear(Vector3 position)
        {
            float r = Data.generatorRadius;
            foreach (var p in NetPlayer.All)
            {
                if (!p.IsAlive) continue;
                Vector3 d = p.transform.position - position;
                d.y = 0f;
                if (d.sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        /// <summary>Host only. Hands out a new set when a round starts in a new mission window (also after skipping rounds).</summary>
        public void ServerOnRoundStarted(int round)
        {
            if (!IsServer || Data == null) return;
            int setRound = Data.SetStartRound(round);
            if (setRound != SetRound.Value) DealSet(round, setRound);
        }

        void DealSet(int round, int setRound)
        {
            SetRound.Value = setRound;
            _pool.Clear();
            for (int id = 0; id < Data.Count; id++)
                if (Data.IsEligible(id, round, k => GameConfig.ZombieType(k).firstRound)) _pool.Add(id);
            for (int i = _pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_pool[i], _pool[j]) = (_pool[j], _pool[i]);
            }

            int openMask = MapState.Instance != null ? MapState.Instance.OpenDoorMask : 0;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                _holdTime[i] = 0f;
                if (i >= _pool.Count)
                {
                    SlotVariable(i).Value = MissionData.Empty;
                    continue;
                }
                int id = _pool[i];
                var def = Data.At(id);
                int zone = def.objective == MissionObjective.HoldGenerator ? PickGeneratorZone(openMask) : 0;
                SlotVariable(i).Value = MissionData.Pack(id, false, zone, 0, def.TargetFor(round));
            }
        }

        /// <summary>A generator in a zone that's already open, so every hold mission can be done without buying a door.</summary>
        int PickGeneratorZone(int openMask)
        {
            _zones.Clear();
            for (int z = 0; z < ZoneMap.ZoneCount; z++)
                if (ZoneMap.IsZoneOpen(z, openMask)) _zones.Add(z);
            return _zones.Count > 0 ? _zones[UnityEngine.Random.Range(0, _zones.Count)] : ZoneMap.StartZone;
        }

        /// <summary>Host only. A zombie died to a player.</summary>
        public void ServerOnKill(NetZombie zombie, bool headshot)
        {
            if (!IsServer || Data == null) return;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = DefIn(i);
                if (def == null) continue;
                bool counts = def.objective == MissionObjective.Kills
                    || (def.objective == MissionObjective.HeadshotKills && headshot)
                    || (def.objective == MissionObjective.KillType && zombie.Kind == def.zombieKind);
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
            if (MissionData.DoneOf(slot) || MissionData.IdOf(slot) == MissionData.NoMission) return;
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
                if (def.bonus == MissionBonus.MaxAmmo) p.ServerRefillAllAmmo();
                else if (def.bonus == MissionBonus.PowerUpReroll) p.PowerRerolls.Value++;
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
        }
    }
}
