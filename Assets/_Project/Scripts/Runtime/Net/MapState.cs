using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Host-owned map state everyone sees: which doors are open, as a bit per door. Every machine builds the
    /// same ZoneMap locally, so only this mask is networked; each machine opens its own doors to match.
    /// </summary>
    public class MapState : NetworkBehaviour
    {
        public static MapState Instance { get; private set; }

        public NetworkVariable<int> OpenDoors = new NetworkVariable<int>(0);

        public int OpenDoorMask => OpenDoors.Value;
        public bool IsDoorOpen(int door) => (OpenDoors.Value & (1 << door)) != 0;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            OpenDoors.OnValueChanged += OnDoorsChanged;
            ApplyDoors(OpenDoors.Value, instant: true);
        }

        public override void OnNetworkDespawn()
        {
            OpenDoors.OnValueChanged -= OnDoorsChanged;
            if (Instance == this) Instance = null;
        }

        void OnDoorsChanged(int previous, int current) => ApplyDoors(current, instant: false);

        static void ApplyDoors(int mask, bool instant)
        {
            var map = GameConfig.Map;
            if (map == null) return;
            foreach (var door in map.Doors) door.SetOpen((mask & (1 << door.Index)) != 0, instant);
        }

        /// <summary>Host only. Opens a door (the buyer has already paid).</summary>
        public void ServerOpenDoor(int door)
        {
            if (!IsServer) return;
            OpenDoors.Value |= 1 << door;
        }

        /// <summary>Host only. Closes every door for a fresh game.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            OpenDoors.Value = 0;
        }
    }
}
