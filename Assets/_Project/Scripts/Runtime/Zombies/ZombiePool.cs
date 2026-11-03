using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ZombiesFPS.Zombies
{
    /// <summary>
    /// Reuses zombie network objects instead of instantiating mid-round. Registered as the prefab handler
    /// on every machine: the host takes zombies with <see cref="Get"/> before spawning them, clients get
    /// theirs through <see cref="Instantiate"/>, and despawned zombies come back here on both.
    /// </summary>
    public class ZombiePool : INetworkPrefabInstanceHandler
    {
        public static ZombiePool Instance { get; private set; }

        readonly GameObject _prefab;
        readonly Stack<NetworkObject> _free = new Stack<NetworkObject>();
        int _created;

        public ZombiePool(GameObject prefab, int prewarm)
        {
            Instance = this;
            _prefab = prefab;
            for (int i = 0; i < prewarm; i++) _free.Push(Create());
        }

        NetworkObject Create()
        {
            var go = Object.Instantiate(_prefab);
            go.name = "Zombie " + (++_created);
            // Build the body now so a spawn mid-round only has to reset it.
            go.GetComponent<NetZombie>().Prebuild();
            go.SetActive(false);
            return go.GetComponent<NetworkObject>();
        }

        /// <summary>Host: an inactive-to-active zombie at a position, ready for ServerSetup and Spawn.</summary>
        public NetZombie Get(Vector3 position, Quaternion rotation) => Take(position, rotation).GetComponent<NetZombie>();

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation) => Take(position, rotation);

        NetworkObject Take(Vector3 position, Quaternion rotation)
        {
            NetworkObject networkObject = null;
            // Pooled objects can be destroyed under us when the scene unloads.
            while (_free.Count > 0 && networkObject == null) networkObject = _free.Pop();
            if (networkObject == null) networkObject = Create();
            networkObject.transform.SetPositionAndRotation(position, rotation);
            networkObject.gameObject.SetActive(true);
            return networkObject;
        }

        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject == null) return;
            networkObject.gameObject.SetActive(false);
            _free.Push(networkObject);
        }
    }
}
