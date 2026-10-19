using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Perk tonics and armour plates for one player. The host owns both: perks are a bitmask that clears when
    /// the player goes down, and armour soaks up damage before health does.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>Owned perks, a bit per PerkKind. Only the host changes it.</summary>
        public NetworkVariable<int> Perks = new NetworkVariable<int>(0);
        /// <summary>Armour left, in health points (plates x plateArmor).</summary>
        public NetworkVariable<float> Armor = new NetworkVariable<float>(0f);

        static PerkData PerkTable => GameConfig.Perks;

        // Server.
        float _selfReviveAt = -1f;

        /// <summary>Owner only: when a perk was last bought and which, for the HUD banner.</summary>
        public float LastPerkTime { get; private set; } = -100f;
        public int LastPerk { get; private set; } = -1;

        public bool HasPerk(PerkKind perk) => PerkData.Has(Perks.Value, perk);
        public float MaxArmor => PerkTable != null ? PerkTable.maxPlates * PerkTable.plateArmor : 0f;
        public int Plates => PerkTable == null || PerkTable.plateArmor <= 0f ? 0 : Mathf.CeilToInt(Armor.Value / PerkTable.plateArmor - 0.001f);
        /// <summary>Owner: whether solo Second Wind is counting down to getting back up.</summary>
        public bool SelfReviving => SelfReviveRemaining.Value > 0f;
        public NetworkVariable<float> SelfReviveRemaining = new NetworkVariable<float>(0f);

        float PerkHealthScale => PerkTable != null && HasPerk(PerkKind.Ironhide) ? PerkTable.ironhideHealth : 1f;
        float PerkReloadScale => PerkTable != null && HasPerk(PerkKind.SlickFingers) ? PerkTable.slickReload : 1f;
        float PerkStaminaScale => PerkTable != null && HasPerk(PerkKind.FleetFoot) ? PerkTable.fleetFootStamina : 1f;
        float PerkSpeedScale => PerkTable != null && HasPerk(PerkKind.FleetFoot) ? PerkTable.fleetFootSpeed : 1f;
        float PerkFireRateScale => PerkTable != null && HasPerk(PerkKind.HairTrigger) ? PerkTable.hairTriggerFireRate : 1f;
        float PerkSpreadScale => PerkTable != null && HasPerk(PerkKind.SteadyHand) ? PerkTable.steadyHandSpread : 1f;
        float PerkReviveScale => PerkTable != null && HasPerk(PerkKind.SecondWind) ? PerkTable.secondWindRevive : 1f;
        float RegenDelayNow => PerkTable != null && HasPerk(PerkKind.Bloodline) ? PerkTable.bloodlineRegenDelay : RegenDelay;

        void SubscribePerks()
        {
            Perks.OnValueChanged += OnPerksChanged;
        }

        void UnsubscribePerks()
        {
            Perks.OnValueChanged -= OnPerksChanged;
        }

        void OnPerksChanged(int previous, int current)
        {
            ApplyOwnerBonuses();
            int gained = current & ~previous;
            if (gained == 0) return;
            for (int i = 0; i < PerkData.MaxPerks; i++)
            {
                if ((gained & (1 << i)) == 0) continue;
                LastPerk = i;
                LastPerkTime = Time.time;
                if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.PerkJingle, 0.8f);
                break;
            }
        }

        /// <summary>What a perk machine, armour station or reroll shrine would cost right now. -1 if there's nothing to buy.</summary>
        long PerkStationPrice(World.Buyable b, out string what)
        {
            what = null;
            if (PerkTable == null) return -1;
            int round = RoundManager.Instance != null ? RoundManager.Instance.DisplayRound : 1;
            switch (b.Kind)
            {
                case World.BuyableKind.Perk:
                    var perk = PerkTable.At(b.Index);
                    if (perk == null) return -1;
                    if (PerkData.Has(Perks.Value, (PerkKind)b.Index))
                    {
                        what = $"You have {perk.name}. Going down loses it.";
                        return -1;
                    }
                    what = $"drink {perk.name}: {perk.description}";
                    return perk.price;
                case World.BuyableKind.Armor:
                    if (Armor.Value >= MaxArmor - 0.01f)
                    {
                        what = $"Armour full ({PerkTable.maxPlates} plates)";
                        return -1;
                    }
                    what = $"fit an armour plate ({Plates}/{PerkTable.maxPlates})";
                    return PerkTable.PlatePrice(round);
                case World.BuyableKind.Reroll:
                    what = $"buy a power-up reroll (you have {PowerRerolls.Value})";
                    return PerkTable.RerollPrice(round);
            }
            return -1;
        }

        /// <summary>Host only, after payment: gives what the station sells.</summary>
        void ServerBuyFromStation(World.Buyable b)
        {
            switch (b.Kind)
            {
                case World.BuyableKind.Perk:
                    float oldMax = MaxHealthNow;
                    Perks.Value = PerkData.With(Perks.Value, (PerkKind)b.Index);
                    if (MaxHealthNow > oldMax) Health.Value = Mathf.Min(MaxHealthNow, Health.Value + (MaxHealthNow - oldMax));
                    break;
                case World.BuyableKind.Armor:
                    Armor.Value = Mathf.Min(MaxArmor, Armor.Value + PerkTable.plateArmor);
                    PlateRpc();
                    break;
                case World.BuyableKind.Reroll:
                    PowerRerolls.Value++;
                    break;
            }
        }

        [Rpc(SendTo.Owner)]
        void PlateRpc()
        {
            if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.ArmorPlate, 0.9f);
        }

        /// <summary>Host: armour takes the hit first, then perks reduce what's left.</summary>
        float ServerAbsorb(float amount, bool acid)
        {
            if (PerkTable != null && HasPerk(PerkKind.HazardWard))
            {
                if (acid) return 0f;
                amount *= PerkTable.hazardDamageTaken;
            }
            if (Armor.Value > 0f && !acid)
            {
                float soaked = Mathf.Min(Armor.Value, amount);
                float before = Plates;
                Armor.Value -= soaked;
                amount -= soaked;
                if (Plates < before) PlateBreakRpc();
            }
            return amount;
        }

        [Rpc(SendTo.Owner)]
        void PlateBreakRpc()
        {
            if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.KnifeHit, 1f);
        }

        /// <summary>Host: Bloodline heals a little on every kill.</summary>
        void OnServerKillHeal(DamageResult result)
        {
            if (!result.Killed || PerkTable == null || !HasPerk(PerkKind.Bloodline) || State.Value != LifeState.Alive) return;
            Health.Value = Mathf.Min(MaxHealthNow, Health.Value + PerkTable.bloodlineHealPerKill);
        }

        /// <summary>Host: going down. Solo Second Wind gets you back up on your own; then every perk is lost.</summary>
        void ServerOnDowned()
        {
            bool solo = s_All.Count == 1;
            _selfReviveAt = solo && PerkTable != null && HasPerk(PerkKind.SecondWind) ? Time.time + PerkTable.secondWindSelfRevive : -1f;
            SelfReviveRemaining.Value = _selfReviveAt > 0f ? PerkTable.secondWindSelfRevive : 0f;
            Perks.Value = 0;
            Armor.Value = 0f;
        }

        /// <summary>Host, while downed: counts down a solo self-revive.</summary>
        bool ServerSelfReviveTick()
        {
            if (_selfReviveAt < 0f) return false;
            SelfReviveRemaining.Value = Mathf.Max(0f, _selfReviveAt - Time.time);
            if (Time.time < _selfReviveAt) return true;
            _selfReviveAt = -1f;
            SelfReviveRemaining.Value = 0f;
            State.Value = LifeState.Alive;
            Health.Value = MaxHealthNow * 0.5f;
            ReviveProgress.Value = 0f;
            _lastDamageTime = Time.time;
            return true;
        }

        /// <summary>Host only, test console: every perk and full armour.</summary>
        public void DebugGiveAllPerks()
        {
            if (!IsServer || PerkTable == null) return;
            int mask = 0;
            for (int i = 0; i < PerkTable.Count; i++) mask |= 1 << i;
            float oldMax = MaxHealthNow;
            Perks.Value = mask;
            if (MaxHealthNow > oldMax) Health.Value = Mathf.Min(MaxHealthNow, Health.Value + (MaxHealthNow - oldMax));
            Armor.Value = MaxArmor;
        }

        void ServerResetPerks()
        {
            Perks.Value = 0;
            Armor.Value = 0f;
            _selfReviveAt = -1f;
            SelfReviveRemaining.Value = 0f;
        }
    }
}
