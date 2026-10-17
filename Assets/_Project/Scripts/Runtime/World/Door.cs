using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Core;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.World
{
    /// <summary>
    /// A buyable door. Closed, it blocks players with its collider and zombies by carving the NavMesh.
    /// Opening drops it into the floor. Purely local: MapState tells every machine which doors are open.
    /// </summary>
    public class Door : MonoBehaviour
    {
        const float SinkDepth = 3.4f;
        const float SinkTime = 0.9f;

        Collider _collider;
        NavMeshObstacle _obstacle;
        AudioSource _audio;
        Vector3 _closedPosition;
        float _openedAt = -1f;

        public int Index { get; private set; }
        public bool IsOpen { get; private set; }

        public void Init(int index)
        {
            Index = index;
            _closedPosition = transform.localPosition;
            _collider = GetComponent<Collider>();
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.shape = NavMeshObstacleShape.Box;
            _obstacle.size = Vector3.one; // Local space, so it matches the scaled box.
            _obstacle.carving = true;
            _obstacle.carveOnlyStationary = true;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.minDistance = 4f;
            _audio.maxDistance = 50f;
            _audio.playOnAwake = false;

            // Cross boards so it reads as a barricade rather than a wall.
            var wood = Greybox.Lit(new Color(0.42f, 0.29f, 0.16f));
            for (int i = 0; i < 2; i++)
            {
                var plank = Greybox.Box("Plank", transform, new Vector3(0f, 0f, -0.6f), new Vector3(1.05f, 0.09f, 0.4f), Color.white, collider: false);
                plank.GetComponent<Renderer>().sharedMaterial = wood;
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 38f : -38f);
            }
        }

        public void SetOpen(bool open, bool instant)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            _collider.enabled = !open;
            _obstacle.enabled = !open;
            if (open)
            {
                _openedAt = instant ? Time.time - SinkTime : Time.time;
                if (!instant)
                {
                    FxSystem.Instance?.Impact(transform.position, Vector3.up, FxSystem.Dust, 14, 3f, 0.08f);
                    _audio.PlayOneShot(ProceduralAudio.DoorOpen, 1f);
                }
            }
            else
            {
                _openedAt = -1f;
                transform.localPosition = _closedPosition;
                SetVisible(true);
            }
        }

        void Update()
        {
            if (_openedAt < 0f) return;
            float t = Mathf.Clamp01((Time.time - _openedAt) / SinkTime);
            transform.localPosition = _closedPosition + Vector3.down * (SinkDepth * t * t);
            if (t >= 1f)
            {
                SetVisible(false);
                _openedAt = -1f;
            }
        }

        void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }
    }
}
