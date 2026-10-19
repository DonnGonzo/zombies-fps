using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// What the easter egg leaves on a player: its ending's boon (Godblood or the Ember Oath), kept for the rest of
    /// the game whatever happens, going down included, and Ashgrave's sword. Also the host's teleport for the egg's
    /// portals, which the owner carries out (movement is the owner's).
    /// </summary>
    public partial class NetPlayer
    {
        public NetworkVariable<EggBoon> Boon = new NetworkVariable<EggBoon>(EggBoon.None);
        /// <summary>Oathbreaker replaces the knife: one-hit kills up to round 55, two up to 100, three after.</summary>
        public NetworkVariable<bool> HasSword = new NetworkVariable<bool>(false);

        float EggDamage => EasterEggData.BoonDamage(Boon.Value);
        float EggHealth => EasterEggData.BoonHealth(Boon.Value);
        float EggReload => EasterEggData.BoonReload(Boon.Value);
        float EggStamina => EasterEggData.BoonStamina(Boon.Value);
        float EggSpeed => EasterEggData.BoonSpeed(Boon.Value);

        void SubscribeEgg()
        {
            Boon.OnValueChanged += OnBoonChanged;
            HasSword.OnValueChanged += OnSwordChanged;
            OnSwordChanged(false, HasSword.Value);
        }

        void UnsubscribeEgg()
        {
            Boon.OnValueChanged -= OnBoonChanged;
            HasSword.OnValueChanged -= OnSwordChanged;
        }

        void OnBoonChanged(EggBoon previous, EggBoon current) => ApplyOwnerBonuses();

        void OnSwordChanged(bool previous, bool current)
        {
            if (_gun != null && _gun.View != null) _gun.View.Sword = current;
        }

        /// <summary>Host only. The egg's reward: a boon, maybe the sword, maybe a gun. Health fills to the new maximum.</summary>
        public void ServerGrantEggReward(EggBoon boon, bool sword, int gunId)
        {
            if (!IsServer) return;
            Boon.Value = boon;
            if (sword) HasSword.Value = true;
            if (gunId >= 0) ServerGiveRewardGun(gunId);
            if (State.Value == LifeState.Alive) Health.Value = MaxHealthNow;
        }

        void ServerResetEgg()
        {
            Boon.Value = EggBoon.None;
            HasSword.Value = false;
        }

        /// <summary>Host only. Sends this player somewhere (an egg portal); the owner moves.</summary>
        public void ServerTeleport(Vector3 position) => EggTeleportRpc(position);

        [Rpc(SendTo.Owner)]
        void EggTeleportRpc(Vector3 position)
        {
            if (_motor != null) _motor.Teleport(position);
        }
    }
}
