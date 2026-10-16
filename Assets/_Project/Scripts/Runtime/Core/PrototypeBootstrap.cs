using UnityEngine;
using UnityEngine.SceneManagement;
using ZombiesFPS.Data;
using ZombiesFPS.Player;
using ZombiesFPS.UI;
using ZombiesFPS.Weapons;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Builds the whole Phase 0 prototype at runtime: lighting, greybox arena + NavMesh, the player
    /// with camera and gun, the zombie spawner and the HUD. Drop it on an empty GameObject in any scene.
    /// Building from code keeps the prototype free of hand-made prefabs while the design settles;
    /// pieces move into prefabs once they stop changing.
    /// </summary>
    public class PrototypeBootstrap : MonoBehaviour
    {
        [Tooltip("Leave empty to use built-in defaults.")]
        public GunData gunData;
        public ZombieData zombieData;
        public RoundData roundData;

        PlayerHealth _playerHealth;
        float _deathTime;

        static readonly Color FogColor = new Color(0.05f, 0.06f, 0.08f);

        void Awake()
        {
            if (gunData == null) gunData = ScriptableObject.CreateInstance<GunData>();
            if (zombieData == null) zombieData = ScriptableObject.CreateInstance<ZombieData>();
            if (roundData == null) roundData = ScriptableObject.CreateInstance<RoundData>();

            SetUpEnvironment();
            var arena = PrototypeArena.Build();
            new GameObject("FX").AddComponent<FxSystem>();

            var player = BuildPlayer(arena.PlayerStart, out var gun, out var look);
            _playerHealth = player.GetComponent<PlayerHealth>();
            _playerHealth.Died += () => _deathTime = Time.time;

            var spawner = new GameObject("Zombie Spawner").AddComponent<ZombieSpawner>();
            spawner.Init(zombieData, roundData, arena.SpawnPoints, player.transform, _playerHealth);

            new GameObject("HUD").AddComponent<PrototypeHud>().Init(gun, _playerHealth, spawner, look);
        }

        void Update()
        {
            if (_playerHealth != null && !_playerHealth.IsAlive && Time.time - _deathTime > 1f && GameInput.DebugPressed(DebugKey.Restart))
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
                else SceneManager.LoadScene(scene.name);
            }
        }

        static void SetUpEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.skybox = null;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            moon.color = new Color(0.62f, 0.7f, 0.95f);
            moon.intensity = 0.45f;
            moon.shadows = LightShadows.Soft;
        }

        GameObject BuildPlayer(Vector3 start, out HitscanGun gun, out PlayerLook look)
        {
            var player = new GameObject("Player");
            player.transform.position = start;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.04f;

            var motor = player.AddComponent<PlayerMotor>();
            look = player.AddComponent<PlayerLook>();
            var health = player.AddComponent<PlayerHealth>();
            gun = player.AddComponent<HitscanGun>();

            var pivot = Greybox.Empty("Camera Pivot", player.transform, new Vector3(0f, 1.62f, 0f));
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            camGo.AddComponent<AudioListener>();

            var gunAudio = camGo.AddComponent<AudioSource>();
            gunAudio.playOnAwake = false;
            gunAudio.spatialBlend = 0f;
            var uiAudio = camGo.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.spatialBlend = 0f;

            var view = Greybox.Empty("Weapon", camGo.transform, Vector3.zero).gameObject.AddComponent<WeaponView>();

            look.Init(pivot, camGo.transform);
            health.Init(motor, look, pivot, uiAudio);
            gun.Init(gunData, cam, look, motor, health, gunAudio, uiAudio, view);
            view.Init(gun, motor, look);
            return player;
        }
    }
}
