using System;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Host-owned map state everyone sees: which doors are open, as a bit per door, and the facility's power.
    /// Every machine builds the same ZoneMap locally, so only these values are networked; each machine opens its
    /// own doors and lights its own lamps to match.
    ///
    /// The power starts off and no door can be bought until it's on. The start-up code is three random digits,
    /// one on each of three notes the host hides among the Gate Yard's note spots; entering it at the generator
    /// panel starts the generator and powers every door.
    /// </summary>
    public class MapState : NetworkBehaviour
    {
        public static MapState Instance { get; private set; }
        /// <summary>Every machine: a note was read (which code digit it holds, the reader's client id).</summary>
        public static event Action<int, ulong> NoteRead;
        /// <summary>Every machine: the generator started (the client id of whoever entered the code, or ulong.MaxValue from the console).</summary>
        public static event Action<ulong> PowerRestored;

        public NetworkVariable<int> OpenDoors = new NetworkVariable<int>(0);
        /// <summary>Whether the generator is running, so doors can be bought.</summary>
        public NetworkVariable<bool> Powered = new NetworkVariable<bool>(false);
        /// <summary>The start-up code, 000-999.</summary>
        public NetworkVariable<int> Code = new NetworkVariable<int>(0);
        /// <summary>Which note spot holds each digit of the code, four bits per digit.</summary>
        public NetworkVariable<int> NoteLayout = new NetworkVariable<int>(0);
        /// <summary>A bit per code digit whose note someone has read.</summary>
        public NetworkVariable<int> NotesRead = new NetworkVariable<int>(0);

        public int OpenDoorMask => OpenDoors.Value;
        public bool IsDoorOpen(int door) => (OpenDoors.Value & (1 << door)) != 0;
        public bool IsPowered => Powered.Value;

        /// <summary>One digit of the code (0 is the first), 0-9.</summary>
        public int Digit(int index) => index == 0 ? Code.Value / 100 : index == 1 ? Code.Value / 10 % 10 : Code.Value % 10;
        public bool IsNoteRead(int digit) => (NotesRead.Value & (1 << digit)) != 0;
        public int NoteSpotOf(int digit) => (NoteLayout.Value >> (digit * 4)) & 15;
        public int NotesFound
        {
            get
            {
                int n = 0;
                for (int k = 0; k < ZoneMap.CodeDigits; k++)
                    if (IsNoteRead(k)) n++;
                return n;
            }
        }

        /// <summary>The code digit whose note lies at a spot, or -1 if no note is there (or it's been read).</summary>
        public int UnreadNoteAt(int spot)
        {
            for (int k = 0; k < ZoneMap.CodeDigits; k++)
                if (NoteSpotOf(k) == spot && !IsNoteRead(k)) return k;
            return -1;
        }

        /// <summary>The code as the squad knows it so far, e.g. "4 _ 7".</summary>
        public string KnownCode
        {
            get
            {
                var parts = new string[ZoneMap.CodeDigits];
                for (int k = 0; k < parts.Length; k++) parts[k] = IsNoteRead(k) ? Digit(k).ToString() : "_";
                return string.Join(" ", parts);
            }
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            OpenDoors.OnValueChanged += OnDoorsChanged;
            Powered.OnValueChanged += OnPoweredChanged;
            NoteLayout.OnValueChanged += OnNotesChanged;
            NotesRead.OnValueChanged += OnNotesChanged;
            if (IsServer) Shuffle();
            ApplyPower(Powered.Value, instant: true);
            ApplyDoors(OpenDoors.Value, instant: true);
            ApplyNotes();
        }

        public override void OnNetworkDespawn()
        {
            OpenDoors.OnValueChanged -= OnDoorsChanged;
            Powered.OnValueChanged -= OnPoweredChanged;
            NoteLayout.OnValueChanged -= OnNotesChanged;
            NotesRead.OnValueChanged -= OnNotesChanged;
            if (Instance == this) Instance = null;
        }

        void OnDoorsChanged(int previous, int current) => ApplyDoors(current, instant: false);
        void OnPoweredChanged(bool previous, bool current) => ApplyPower(current, instant: false);
        void OnNotesChanged(int previous, int current) => ApplyNotes();

        static void ApplyDoors(int mask, bool instant)
        {
            var map = GameConfig.Map;
            if (map == null) return;
            foreach (var door in map.Doors) door.SetOpen((mask & (1 << door.Index)) != 0, instant);
        }

        static void ApplyPower(bool powered, bool instant)
        {
            var map = GameConfig.Map;
            if (map == null) return;
            foreach (var door in map.Doors) door.SetPowered(powered);
            map.ShowPower(powered, instant);
        }

        void ApplyNotes()
        {
            var map = GameConfig.Map;
            if (map == null) return;
            for (int s = 0; s < ZoneMap.NoteSpotCount; s++)
            {
                int digit = -1;
                for (int k = 0; k < ZoneMap.CodeDigits; k++)
                    if (NoteSpotOf(k) == s) digit = k;
                map.ShowNote(s, digit >= 0, digit >= 0 && IsNoteRead(digit));
            }
        }

        // ---------------------------------------------------------------- server

        /// <summary>A new code, and three different note spots for its digits.</summary>
        void Shuffle()
        {
            Code.Value = UnityEngine.Random.Range(0, 1000);
            var spots = new System.Collections.Generic.List<int>();
            for (int s = 0; s < ZoneMap.NoteSpotCount; s++) spots.Add(s);
            int layout = 0;
            for (int k = 0; k < ZoneMap.CodeDigits; k++)
            {
                int pick = UnityEngine.Random.Range(0, spots.Count);
                layout |= spots[pick] << (k * 4);
                spots.RemoveAt(pick);
            }
            NoteLayout.Value = layout;
            NotesRead.Value = 0;
        }

        /// <summary>Host only. Opens a door (the buyer has already paid).</summary>
        public void ServerOpenDoor(int door)
        {
            if (!IsServer) return;
            OpenDoors.Value |= 1 << door;
        }

        /// <summary>Host only. A player read the note at a spot: its digit is now known to everyone.</summary>
        public void ServerReadNote(NetPlayer reader, int spot)
        {
            int digit = UnreadNoteAt(spot);
            if (!IsServer || digit < 0) return;
            NotesRead.Value |= 1 << digit;
            NoteReadRpc(digit, reader != null ? reader.OwnerClientId : ulong.MaxValue);
        }

        /// <summary>Host only. Whether a code is the start-up code; the right one starts the generator.</summary>
        public bool ServerTryCode(NetPlayer player, int code)
        {
            if (!IsServer || Powered.Value) return false;
            if (code != Code.Value) return false;
            ServerPowerOn(player);
            return true;
        }

        /// <summary>Host only. Starts the generator (the right code, or the test console).</summary>
        public void ServerPowerOn(NetPlayer by)
        {
            if (!IsServer || Powered.Value) return;
            Powered.Value = true;
            PowerRestoredRpc(by != null ? by.OwnerClientId : ulong.MaxValue);
        }

        /// <summary>Host only. Closes every door, cuts the power and hides a new code for a fresh game.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            OpenDoors.Value = 0;
            Powered.Value = false;
            Shuffle();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void NoteReadRpc(int digit, ulong reader) => NoteRead?.Invoke(digit, reader);

        [Rpc(SendTo.ClientsAndHost)]
        void PowerRestoredRpc(ulong by) => PowerRestored?.Invoke(by);
    }
}
