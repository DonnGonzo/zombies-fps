using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.Player
{
    /// <summary>
    /// Mouse look (yaw on the body, pitch on the camera pivot), cursor lock, recoil and camera shake.
    /// Recoil moves where you actually aim; shake is visual only.
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
        Quaternion _cameraBaseRotation = Quaternion.identity;

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
        }

        public void AddShake(float amount)
        {
            _shake = Mathf.Min(_shake + amount, 1.5f);
        }

        void Update()
        {
            LockedThisFrame = false;
            if (GameInput.PausePressed) SetCursorLocked(false);
            else if (!CursorLocked && !InputLocked && GameInput.FirePressed)
            {
                SetCursorLocked(true);
                LockedThisFrame = true;
            }

            float dt = Time.deltaTime;
            Vector2 look = Vector2.zero;
            if (CursorLocked && !InputLocked)
            {
                look = GameInput.Look * sensitivity * Mathf.Lerp(1f, aimSensitivityScale, AimAmount);
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
