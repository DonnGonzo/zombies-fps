using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.Player
{
    /// <summary>
    /// Mouse and right-stick look (yaw on the body, pitch on the camera pivot), controller aim assist, cursor lock,
    /// recoil and camera shake. Recoil moves where you actually aim; shake is visual only (and rumbles a pad).
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Tooltip("Degrees per mouse pixel.")]
        public float sensitivity = 0.09f;
        [Range(0.1f, 1f)] public float aimSensitivityScale = 0.65f;
        public float maxPitch = 85f;
        [Tooltip("How fast queued recoil is applied to the view. Higher is snappier.")]
        public float recoilSnap = 28f;

        Transform _pitchPivot;
        Transform _camera;
        float _pitch;
        Vector2 _pendingRecoil;
        float _shake;
        bool _aimWasHeld;
        Quaternion _cameraBaseRotation = Quaternion.identity;

        /// <summary>A left click on the game view locks the cursor again. The pause menu turns this off while the mouse is over it.</summary>
        public static bool ClickToResume = true;

        public bool InputLocked { get; set; }
        public bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;
        /// <summary>True on the frame the click that locked the cursor happened, so that click doesn't also fire.</summary>
        public bool LockedThisFrame { get; private set; }
        public float AimAmount { get; set; }
        public Vector2 LastLookDelta { get; private set; }

        public void Init(Transform pitchPivot, Transform cameraTransform)
        {
            _pitchPivot = pitchPivot;
            _camera = cameraTransform;
            _cameraBaseRotation = _camera.localRotation;
        }

        void Start()
        {
            SetCursorLocked(true);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCursorLocked(false);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void AddRecoil(float pitchDegrees, float yawDegrees)
        {
            _pendingRecoil += new Vector2(yawDegrees, pitchDegrees);
            GameInput.Rumble(0.05f + pitchDegrees * 0.04f, 0.25f + pitchDegrees * 0.08f, 0.07f);
        }

        public void AddShake(float amount)
        {
            // Rumble isn't cut by Reduce camera shake; it has its own setting.
            if (amount > 0.2f) GameInput.Rumble(amount * 0.7f, amount * 0.45f, 0.12f + amount * 0.25f);
            if (GameSettings.ReduceShake) amount *= 0.2f;
            _shake = Mathf.Min(_shake + amount, 1.5f);
        }

        void Update()
        {
            LockedThisFrame = false;
            // In game the HUD's pause menu owns Esc; this fallback is for when there is no HUD.
            if (GameInput.PausePressed && ClickToResume) SetCursorLocked(false);
            else if (!CursorLocked && !InputLocked && ClickToResume && GameInput.KeyDown(KeyCode.Mouse0))
            {
                SetCursorLocked(true);
                LockedThisFrame = true;
            }

            float dt = Time.deltaTime;
            Vector2 look = Vector2.zero;
            bool aimHeld = CursorLocked && !InputLocked && GameInput.AimHeld;
            if (aimHeld && !_aimWasHeld) AimAssist.StartAim();
            _aimWasHeld = aimHeld;
            if (CursorLocked && !InputLocked)
            {
                // The stick has its own extra aiming slow-down on top of the mouse's.
                float mouseScale = Mathf.Lerp(1f, aimSensitivityScale, AimAmount);
                float padScale = Mathf.Lerp(1f, aimSensitivityScale * GameSettings.PadAimSensitivity, AimAmount);
                look = (GameInput.MouseLook * mouseScale + GameInput.PadLook * padScale) * sensitivity;
                look = AimAssist.Apply(look, _pitchPivot, AimAmount, GameInput.Move.magnitude, dt);
            }
            LastLookDelta = look;

            // Feed queued recoil in over a few frames so a shot reads as a kick, not a teleport.
            Vector2 recoilStep = _pendingRecoil * (1f - Mathf.Exp(-recoilSnap * dt));
            _pendingRecoil -= recoilStep;

            transform.Rotate(0f, look.x + recoilStep.x, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - look.y - recoilStep.y, -maxPitch, maxPitch);
            // While dead, PlayerHealth owns the pivot for the death slump.
            if (_pitchPivot != null && !InputLocked) _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            if (_camera != null)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 3f);
                float s = _shake * _shake;
                float t = Time.time * 30f;
                var shakeRot = Quaternion.Euler(
                    (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 6f * s,
                    (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 6f * s,
                    (Mathf.PerlinNoise(t, 0.7f) - 0.5f) * 8f * s);
                _camera.localRotation = _cameraBaseRotation * shakeRot;
            }
        }
    }
}
