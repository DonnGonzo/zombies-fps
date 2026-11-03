using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.World;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Host-owned main story: which step of StoryData.Steps the squad is on and how far through it. Each step opens
    /// as soon as the one before it is done, whatever the round, so the story goes as fast as the squad plays it.
    ///
    /// Steps use the map's existing props: the start-up code (MapState), doors, a zone's generator or a high spot's
    /// beacon (held while a round is on), the mission board's survivor, kills and cleared rounds. The board draws the
    /// story's prop and survivor (MissionBoard.RefreshProps); StoryDirector says the radio calls on every machine.
    /// </summary>
    public class StoryProgress : NetworkBehaviour
    {
        public static StoryProgress Instance { get; private set; }
        /// <summary>Every machine: the story moved from one step to another (previous, current).</summary>
        public static event Action<int, int> StepChanged;
        /// <summary>Every machine: progress on the current step changed (step, previous, current).</summary>
        public static event Action<int, int, int> ProgressChanged;
        /// <summary>Every machine: the story's survivor was killed; the step starts again next round.</summary>
        public static event Action SurvivorLost;

        /// <summary>The current step. StoryData.StepCount once the story is over.</summary>
        public NetworkVariable<int> Step = new NetworkVariable<int>(0);
        /// <summary>How far through the current step: notes read, doors open, seconds held, kills, rounds.</summary>
        public NetworkVariable<int> Progress = new NetworkVariable<int>(0);
        /// <summary>Where a step picked when it started: the high spot for a beacon, the survivor's zone. -1 for none (yet).</summary>
        public NetworkVariable<int> Place = new NetworkVariable<int>(-1);

        float _held;
        readonly List<int> _picks = new List<int>();

        public StoryStep Current => StoryData.StepAt(Step.Value);
        public bool Complete => StoryData.StoryOver(Step.Value);
        public int Act => StoryData.ActOf(Step.Value);
        public float Fraction => Current != null ? Mathf.Clamp01(Progress.Value / (float)Current.target) : 1f;

        /// <summary>The story's protect step is on (its survivor may still be waiting to come out).</summary>
        public bool WantsSurvivor => Current != null && Current.goal == StoryGoal.Protect;
        /// <summary>The zone the story's survivor is in, or -1.</summary>
        public int SurvivorZone => WantsSurvivor ? Place.Value : -1;
        /// <summary>The zone whose generator the story wants held now, or -1.</summary>
        public int HoldZone => Current != null && Current.goal == StoryGoal.Hold ? Current.zone : -1;

        /// <summary>The generator or beacon the current step is held at, if it's that kind of step.</summary>
        public bool TryGetProp(ZoneMap map, out Generator prop)
        {
            prop = null;
            var step = Current;
            if (step == null || map == null) return false;
            if (step.goal == StoryGoal.Hold && map.Generators != null && step.zone >= 0 && step.zone < map.Generators.Length) prop = map.Generators[step.zone];
            else if (step.goal == StoryGoal.Beacon && map.Beacons != null && Place.Value >= 0 && Place.Value < map.Beacons.Length) prop = map.Beacons[Place.Value];
            return prop != null;
        }

        /// <summary>Where the current step happens, for the map and the HUD; false if it has no one place.</summary>
        public bool TryGetWhere(out Vector3 where, out string name)
        {
            where = Vector3.zero;
            name = null;
            var step = Current;
            if (step == null) return false;
            switch (step.goal)
            {
                case StoryGoal.Power:
                    where = ZoneMap.PanelPosition;
                    name = ZoneMap.ZoneNames[ZoneMap.StartZone];
                    return true;
                case StoryGoal.Reach:
                    where = ZoneMap.ZoneCenter(step.zone);
                    name = ZoneMap.ZoneNames[step.zone];
                    return true;
                case StoryGoal.Hold:
                    var map = GameConfig.Map;
                    where = map != null && map.Generators != null && step.zone < map.Generators.Length ? map.Generators[step.zone].transform.position : ZoneMap.ZoneCenter(step.zone);
                    name = ZoneMap.ZoneNames[step.zone];
                    return true;
                case StoryGoal.Beacon:
                    if (Place.Value < 0 || Place.Value >= ZoneMap.HighSpotCount) return false;
                    where = ZoneMap.HighSpot(Place.Value);
                    name = $"{ZoneMap.HighSpotNames[Place.Value]} ({ZoneMap.ZoneNames[ZoneMap.HighSpotZones[Place.Value]]})";
                    return true;
                case StoryGoal.Protect:
                    if (SurvivorZone < 0 || SurvivorZone >= ZoneMap.ZoneCount) return false;
                    where = ZoneMap.SurvivorSpot(SurvivorZone);
                    name = ZoneMap.ZoneNames[SurvivorZone];
                    return true;
            }
            return false;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Step.OnValueChanged += OnStepChanged;
            Progress.OnValueChanged += OnProgressChanged;
            Place.OnValueChanged += OnPlaceChanged;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            Step.OnValueChanged -= OnStepChanged;
            Progress.OnValueChanged -= OnProgressChanged;
            Place.OnValueChanged -= OnPlaceChanged;
            if (Instance == this) Instance = null;
        }

        void OnStepChanged(int previous, int current)
        {
            Refresh();
            StepChanged?.Invoke(previous, current);
        }

        void OnProgressChanged(int previous, int current)
        {
            Refresh();
            ProgressChanged?.Invoke(Step.Value, previous, current);
        }

        void OnPlaceChanged(int previous, int current) => Refresh();

        static void Refresh()
        {
            if (MissionBoard.Instance != null) MissionBoard.Instance.RefreshProps();
        }

        // ---------------------------------------------------------------- server

        void Update()
        {
            if (!IsServer) return;
            var step = Current;
            if (step == null) return;
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.IsGameOver) return;
            bool roundOn = rounds != null && rounds.Phase.Value == GamePhase.RoundActive;
            switch (step.goal)
            {
                case StoryGoal.Power:
                    var power = MapState.Instance;
                    if (power == null) break;
                    if (power.IsPowered) NextStep();
                    else if (Progress.Value != power.NotesFound) Progress.Value = power.NotesFound;
                    break;
                case StoryGoal.OpenDoors:
                    int open = MapState.Instance != null ? CountBits(MapState.Instance.OpenDoorMask) : 0;
                    SetProgress(open);
                    break;
                case StoryGoal.Reach:
                    if (AnyoneInZone(step.zone)) NextStep();
                    break;
                case StoryGoal.Hold:
                case StoryGoal.Beacon:
                    if (step.goal == StoryGoal.Beacon && Place.Value < 0) Place.Value = PickHighSpot();
                    if (roundOn && TryGetProp(GameConfig.Map, out var prop) && MissionBoard.AnyoneNear(prop.transform.position, step.goal == StoryGoal.Beacon))
                        Hold(Time.deltaTime);
                    break;
            }
        }

        static int CountBits(int mask)
        {
            int n = 0;
            for (uint m = (uint)mask; m != 0; m &= m - 1) n++;
            return n;
        }

        static bool AnyoneInZone(int zone)
        {
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsAlive) continue;
                var at = p.transform.position;
                if (ZoneMap.ZoneOfPoint(at) == zone && at.y > -1f && at.y < ZoneMap.WallHeight) return true;
            }
            return false;
        }

        void Hold(float dt)
        {
            _held += dt;
            while (_held >= 1f)
            {
                _held -= 1f;
                SetProgress(Progress.Value + 1);
            }
        }

        void SetProgress(int value)
        {
            var step = Current;
            if (step == null) return;
            value = Mathf.Clamp(value, 0, step.target);
            if (value != Progress.Value) Progress.Value = value;
            if (value >= step.target) NextStep();
        }

        void NextStep() => GoTo(Step.Value + 1);

        void GoTo(int step)
        {
            step = Mathf.Clamp(step, 0, StoryData.StepCount);
            _held = 0f;
            // Progress first, so the new step never starts with the last one's count.
            Progress.Value = 0;
            Place.Value = -1;
            Step.Value = step;
            var def = Current;
            if (def == null) return;
            if (def.goal == StoryGoal.Beacon) Place.Value = PickHighSpot();
            else if (def.goal == StoryGoal.Protect) TryPlaceSurvivor();
        }

        /// <summary>The Defector comes out in an open zone once no other survivor is being protected.</summary>
        void TryPlaceSurvivor()
        {
            var board = MissionBoard.Instance;
            if (!WantsSurvivor || Place.Value >= 0 || board == null || board.ActiveSurvivorSlot >= 0) return;
            int openMask = MapState.Instance != null ? MapState.Instance.OpenDoorMask : 0;
            _picks.Clear();
            for (int z = 0; z < ZoneMap.ZoneCount; z++)
                if (ZoneMap.IsZoneOpen(z, openMask) && z != ZoneMap.StartZone) _picks.Add(z);
            int zone = _picks.Count > 0 ? _picks[UnityEngine.Random.Range(0, _picks.Count)] : ZoneMap.StartZone;
            _held = 0f;
            board.ServerHealSurvivor();
            Place.Value = zone;
        }

        /// <summary>A high spot in an open zone if there is one; otherwise any, as a reason to open doors.</summary>
        int PickHighSpot()
        {
            int openMask = MapState.Instance != null ? MapState.Instance.OpenDoorMask : 0;
            _picks.Clear();
            for (int s = 0; s < ZoneMap.HighSpotCount; s++)
                if (ZoneMap.IsZoneOpen(ZoneMap.HighSpotZones[s], openMask)) _picks.Add(s);
            return _picks.Count > 0 ? _picks[UnityEngine.Random.Range(0, _picks.Count)] : UnityEngine.Random.Range(0, ZoneMap.HighSpotCount);
        }

        /// <summary>Host only. The board's survivor tick for the story's survivor: the clock runs while a round is on.</summary>
        public void ServerSurvivorTick(float dt)
        {
            if (IsServer && SurvivorZone >= 0) Hold(dt);
        }

        /// <summary>Host only. The story's survivor died: the step starts over with a fresh one next round.</summary>
        public void ServerSurvivorDied()
        {
            if (!IsServer || SurvivorZone < 0) return;
            _held = 0f;
            Progress.Value = 0;
            Place.Value = -1;
            SurvivorLostRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void SurvivorLostRpc() => SurvivorLost?.Invoke();

        /// <summary>Host only. A round started (after the mission board dealt): a survivor waiting to come out does.</summary>
        public void ServerOnRoundStarted()
        {
            if (!IsServer) return;
            TryPlaceSurvivor();
            if (Current != null && Current.goal == StoryGoal.Beacon && Place.Value < 0) Place.Value = PickHighSpot();
        }

        /// <summary>Host only. A zombie died to a player.</summary>
        public void ServerOnKill()
        {
            if (IsServer && Current != null && Current.goal == StoryGoal.Kills) SetProgress(Progress.Value + 1);
        }

        /// <summary>Host only. A round was cleared.</summary>
        public void ServerOnRoundCompleted()
        {
            if (IsServer && Current != null && Current.goal == StoryGoal.ClearRounds) SetProgress(Progress.Value + 1);
        }

        /// <summary>Host only. Back to the first step for a fresh game.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            _held = 0f;
            Progress.Value = 0;
            Place.Value = -1;
            Step.Value = 0;
        }

        /// <summary>Test console: finish the current step (the power step starts the generator).</summary>
        public void DebugCompleteStep()
        {
            if (!IsServer || Complete) return;
            if (Current.goal == StoryGoal.Power && MapState.Instance != null) MapState.Instance.ServerPowerOn(null);
            NextStep();
        }

        /// <summary>Test console: jump to a step (the generator starts for any step after the first).</summary>
        public void DebugGoTo(int step)
        {
            if (!IsServer) return;
            if (step > 0 && MapState.Instance != null) MapState.Instance.ServerPowerOn(null);
            GoTo(step);
        }
    }
}
