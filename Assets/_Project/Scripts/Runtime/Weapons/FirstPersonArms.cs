using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.Weapons
{
    /// <summary>Two gripping gloves and sleeved forearms, anchored to the equipped weapon's own space.</summary>
    public sealed class FirstPersonArms : MonoBehaviour
    {
        Transform _right, _left, _rightSleeve, _leftSleeve, _rightCuff, _leftCuff;
        Transform _gripSpace, _cameraSpace;
        Vector3 _mainGrip, _supportGrip;
        bool _pistol;
        Material _glove, _seam, _cloth;

        public void Init(Transform cameraSpace)
        {
            _cameraSpace = cameraSpace;
            _glove = Greybox.Lit(new Color(0.075f, 0.083f, 0.09f), Surface.Rubber, true);
            _seam = Greybox.Lit(new Color(0.15f, 0.16f, 0.15f), Surface.Fabric, true);
            _cloth = Greybox.Lit(new Color(0.18f, 0.20f, 0.16f), Surface.Fabric, true);
            _right = Hand("Trigger hand"); _left = Hand("Support hand");
            _rightSleeve = Part("Right sleeve", transform, Vector3.zero, Vector3.one, _cloth);
            _leftSleeve = Part("Left sleeve", transform, Vector3.zero, Vector3.one, _cloth);
            _rightCuff = Part("Right cuff", transform, Vector3.zero, Vector3.one, _seam);
            _leftCuff = Part("Left cuff", transform, Vector3.zero, Vector3.one, _seam);
            Greybox.DisableShadows(gameObject);
        }

        Transform Part(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        Transform Hand(string name)
        {
            var hand = new GameObject(name).transform;
            hand.SetParent(transform, false);
            Part("Palm", hand, Vector3.zero, new Vector3(0.038f, 0.086f, 0.058f), _glove);
            Part("Back padding", hand, new Vector3(0.018f, 0.008f, 0), new Vector3(0.012f, 0.062f, 0.044f), _seam);
            // Fingers curl around the front of the grip, leaving the trigger finger higher.
            for (int i = 0; i < 4; i++)
            {
                float y = 0.028f - i * 0.019f;
                Part("Knuckle " + i, hand, new Vector3(0.009f, y, 0.028f), new Vector3(0.028f, 0.018f, 0.028f), _glove);
                Part("Finger " + i, hand, new Vector3(-0.014f, y, 0.037f), new Vector3(0.038f, 0.017f, 0.021f), _glove);
                Part("Fingertip " + i, hand, new Vector3(-0.032f, y, 0.023f), new Vector3(0.018f, 0.016f, 0.035f), _glove);
            }
            var thumb = Part("Thumb", hand, new Vector3(-0.003f, 0.033f, -0.023f), new Vector3(0.055f, 0.024f, 0.024f), _glove);
            thumb.localRotation = Quaternion.Euler(0, -25, -25);
            return hand;
        }

        public void Equip(GunData gun, GunModels.Model model)
        {
            _gripSpace = model.Root.transform.Find("Model") ?? model.Root.transform;
            _pistol = model.Shape == GunModels.Shape.Revolver;
            _mainGrip = new Vector3(0.028f, -0.065f, 0f);
            _supportGrip = new Vector3(-0.008f, -0.035f, 0.23f);
            switch (gun.displayName)
            {
                case "Rook-9 Carbine": _mainGrip = new Vector3(0.028f,-0.115f,0.065f); _supportGrip = new Vector3(0,-0.055f,0.32f); break;
                case "Hatchet .44": _mainGrip = new Vector3(0.024f,-0.06f,-0.025f); break;
                case "Service 9mm": _mainGrip = new Vector3(0.023f,-0.035f,-0.026f); break;
                case "Wren-7 SMG": _mainGrip = new Vector3(0.029f,-0.115f,-0.025f); _supportGrip = new Vector3(0,-0.075f,0.16f); break;
                case "Talon LMG": _mainGrip = new Vector3(0.035f,-0.14f,0.02f); _supportGrip = new Vector3(0,-0.12f,0.33f); break;
                case "TEC-9": _mainGrip = new Vector3(0.026f,-0.085f,-0.02f); _supportGrip = new Vector3(0,-0.045f,0.16f); break;
                case "Tommy Gun": _mainGrip = new Vector3(0.025f,-0.085f,0.10f); _supportGrip = new Vector3(0,-0.09f,0.39f); break;
            }
            if (_pistol) _supportGrip = _mainGrip + new Vector3(-0.055f,-0.012f,0.01f);
        }

        public void Pose(float reload, float melee, bool hidden)
        {
            gameObject.SetActive(!hidden && _gripSpace != null);
            if (!gameObject.activeSelf) return;
            _right.position = _gripSpace.TransformPoint(_mainGrip);
            _right.rotation = _gripSpace.rotation * Quaternion.Euler(-12, 0, -8);
            Vector3 support = _supportGrip;
            if (reload >= 0f)
            {
                float reach = Mathf.Sin(Mathf.PI * Mathf.Clamp01(reload));
                support = Vector3.Lerp(support, _mainGrip + new Vector3(-0.07f,-0.19f,0.09f), reach);
            }
            support += Vector3.down * (melee * 0.3f);
            _left.position = _gripSpace.TransformPoint(support);
            _left.rotation = _gripSpace.rotation * Quaternion.Euler(0, 0, _pistol ? 170f : -90f);
            Sleeve(_right, _rightSleeve, _rightCuff, new Vector3(0.29f,-0.38f,-0.06f));
            Sleeve(_left, _leftSleeve, _leftCuff, new Vector3(-0.28f,-0.38f,-0.04f));
        }

        void Sleeve(Transform hand, Transform sleeve, Transform cuff, Vector3 elbow)
        {
            Vector3 wrist = hand.TransformPoint(new Vector3(0,-0.043f,-0.012f));
            Vector3 start = _cameraSpace.TransformPoint(elbow);
            Vector3 delta = wrist - start;
            var rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            sleeve.position = Vector3.Lerp(start, wrist, 0.45f);
            sleeve.rotation = rotation;
            sleeve.localScale = new Vector3(0.085f, delta.magnitude * 1.12f, 0.078f);
            cuff.position = wrist - delta.normalized * 0.015f;
            cuff.rotation = rotation;
            cuff.localScale = new Vector3(0.065f, 0.045f, 0.06f);
        }
    }
}
