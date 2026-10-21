using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// The generator's start-up keypad for one player. Interact at the panel opens it (like the shop); the number
    /// keys type the code (Backspace rubs out), or on a gamepad the d-pad turns and takes each digit. The third
    /// digit sends the code to the host, which checks it and starts the generator if it's right.
    /// </summary>
    public partial class NetPlayer
    {
        const float CodeRetryDelay = 0.8f;

        readonly int[] _keypad = { -1, -1, -1 };
        int _keypadAt;
        float _serverNextCode;

        /// <summary>Owner only: the keypad is open.</summary>
        public bool KeypadOpen { get; private set; }
        /// <summary>Owner only: the digits typed so far (-1 for none) and which one is being typed.</summary>
        public int KeypadDigit(int i) => _keypad[i];
        public int KeypadCursor => _keypadAt;
        /// <summary>Owner only: when the last code was turned down, for the keypad's red flash.</summary>
        public float KeypadWrongTime { get; private set; } = -100f;
        /// <summary>Owner only: a code is on its way to the host.</summary>
        public bool KeypadWaiting { get; private set; }

        public void CloseKeypad()
        {
            KeypadOpen = false;
            ClearKeypad();
        }

        void ClearKeypad()
        {
            for (int i = 0; i < _keypad.Length; i++) _keypad[i] = -1;
            _keypadAt = 0;
        }

        void OwnerKeypadTick(bool canAct, bool picking)
        {
            bool atPanel = FocusedBuyable != null && FocusedBuyable.Kind == BuyableKind.PowerPanel;
            if (!canAct || !atPanel)
            {
                if (KeypadOpen) CloseKeypad();
                return;
            }
            if (GameInput.InteractPressed)
            {
                if (KeypadOpen) CloseKeypad();
                else
                {
                    KeypadOpen = true;
                    ClearKeypad();
                }
            }
            if (!KeypadOpen || picking || KeypadWaiting) return;

            int digit = GameInput.DigitPressed;
            if (digit >= 0) TypeDigit(digit);
            else if (GameInput.BackspacePressed || GameInput.KeypadBackPressed) Rubout();
            else if (GameInput.KeypadUpPressed || GameInput.KeypadDownPressed)
            {
                int d = _keypad[_keypadAt] < 0 ? 0 : (_keypad[_keypadAt] + (GameInput.KeypadUpPressed ? 1 : 9)) % 10;
                _keypad[_keypadAt] = d;
                KeyBeep();
            }
            else if (GameInput.KeypadNextPressed) TypeDigit(Mathf.Max(0, _keypad[_keypadAt]));
        }

        void TypeDigit(int digit)
        {
            _keypad[_keypadAt] = digit;
            KeyBeep();
            if (_keypadAt < _keypad.Length - 1)
            {
                _keypadAt++;
                return;
            }
            KeypadWaiting = true;
            EnterCodeRpc(_keypad[0] * 100 + _keypad[1] * 10 + _keypad[2]);
        }

        void Rubout()
        {
            if (_keypad[_keypadAt] < 0 && _keypadAt > 0) _keypadAt--;
            _keypad[_keypadAt] = -1;
            KeyBeep();
        }

        void KeyBeep()
        {
            if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.KeyBeep, 0.7f);
        }

        // ---------------------------------------------------------------- server

        [Rpc(SendTo.Server)]
        void EnterCodeRpc(int code)
        {
            var map = MapState.Instance;
            var rounds = RoundManager.Instance;
            bool ok = false;
            // A short wait between tries, so the panel can't be raked through all thousand codes.
            if (map != null && !map.IsPowered && State.Value == LifeState.Alive && !(rounds != null && rounds.IsGameOver) && NearPanel() && Time.time >= _serverNextCode)
            {
                _serverNextCode = Time.time + CodeRetryDelay;
                ok = map.ServerTryCode(this, code);
            }
            CodeResultRpc(ok || (map != null && map.IsPowered));
        }

        [Rpc(SendTo.Owner)]
        void CodeResultRpc(bool accepted)
        {
            KeypadWaiting = false;
            if (accepted)
            {
                CloseKeypad();
                return;
            }
            KeypadWrongTime = Time.time;
            ClearKeypad();
            if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.Denied, 0.8f);
        }

        bool NearPanel()
        {
            var map = GameConfig.Map;
            if (map == null) return false;
            foreach (var b in map.Buyables)
            {
                if (b.Kind != BuyableKind.PowerPanel) continue;
                Vector3 d = b.Position - transform.position;
                if (Mathf.Abs(d.y - 1f) > 2.2f) continue;
                d.y = 0f;
                if (d.magnitude <= b.Range + 1f) return true;
            }
            return false;
        }

        /// <summary>What reading a note or using the panel would do, for PriceFor.</summary>
        long PowerPrice(Buyable b, out string what)
        {
            what = null;
            var map = MapState.Instance;
            if (map == null) return -1;
            if (b.Kind == BuyableKind.Note)
            {
                if (map.UnreadNoteAt(b.Index) < 0) return -1;
                what = "read the note";
                return 0;
            }
            if (map.IsPowered) return -1;
            what = "enter the generator's start-up code";
            return 0;
        }
    }
}
