using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Weapons;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Run progression for one player: stacking power-ups (pick 1 of 3 every few rounds) and the upgrade
    /// machine tier of each gun slot. The host decides everything; the owner applies the bonuses to its rig.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>No power-up in that card of an offer.</summary>
        public const int NoPowerUp = 0xF;

        /// <summary>Upgrade machine tier of the gun in each slot (0 = as bought).</summary>
        public NetworkVariable<int> Tier0 = new NetworkVariable<int>(0);
        public NetworkVariable<int> Tier1 = new NetworkVariable<int>(0);
        /// <summary>Power-up levels, 8 bits each (see UpgradeData.LevelOf).</summary>
        public NetworkVariable<ulong> PowerLevels = new NetworkVariable<ulong>(0);
        /// <summary>The pending offer: -1 for none, else three 4-bit power-up ids and an empowered flag at bit 12.</summary>
        public NetworkVariable<int> PowerOffer = new NetworkVariable<int>(-1);
        /// <summary>The machine window this player last upgraded in (-1 = never).</summary>
        public NetworkVariable<int> MachineWindowUsed = new NetworkVariable<int>(-1);

        readonly Collider[] _overlap = new Collider[64];
        readonly HashSet<NetZombie> _splashed = new HashSet<NetZombie>();
        readonly List<int> _draw = new List<int>();

        /// <summary>Owner only: the power-up picked last and when, for the HUD banner.</summary>
        public string LastPickText { get; private set; }
        public float LastPickTime { get; private set; } = -100f;

        static UpgradeData Upgrades => GameConfig.Upgrades;

        public int TierInSlot(int slot) => slot == 0 ? Tier0.Value : slot == 1 ? Tier1.Value : 0;
        NetworkVariable<int> TierVariable(int slot) => slot == 0 ? Tier0 : Tier1;

        public int PowerLevel(int index) => UpgradeData.LevelOf(PowerLevels.Value, index);
        public float Bonus(PowerStat stat) => Upgrades != null ? Upgrades.StatBonus(PowerLevels.Value, stat) : 0f;
        public float MaxHealthNow => MaxHealth * (1f + Bonus(PowerStat.MaxHealth));

        public bool HasPowerUpOffer => PowerOffer.Value >= 0;
        public bool OfferIsEmpowered => HasPowerUpOffer && (PowerOffer.Value & (1 << 12)) != 0;

        /// <summary>The power-up id on card 0-2 of the pending offer, or NoPowerUp.</summary>
        public int OfferedPowerUp(int card) => HasPowerUpOffer && card >= 0 && card < 3 ? (PowerOffer.Value >> (card * 4)) & 0xF : NoPowerUp;

        /// <summary>Levels a pick from the pending offer adds.</summary>
        public int OfferLevels => OfferIsEmpowered ? Mathf.Max(1, Upgrades.empoweredLevels) : 1;

        public float CapacityScale(int slot) => (1f + Bonus(PowerStat.AmmoCapacity)) * (Upgrades != null ? Upgrades.TierCapacity(TierInSlot(slot)) : 1f);
        public float GunDamageMultiplier(int slot) => (Upgrades != null ? Upgrades.TierDamage(TierInSlot(slot)) : 1f) * (1f + Bonus(PowerStat.Damage));

        public string GunName(int slot)
        {
            var data = GameConfig.GunById(GunInSlot(slot));
            if (data == null) return "";
            var tier = Upgrades != null ? Upgrades.Tier(TierInSlot(slot)) : null;
            return tier != null ? data.displayName + " " + tier.suffix : data.displayName;
        }

        /// <summary>The machine recharges each time a power-up arrives, so a window is the rounds cleared between two offers.</summary>
        static int CurrentMachineWindow()
        {
            var rounds = RoundManager.Instance;
            if (rounds == null || Upgrades == null) return 0;
            int cleared = rounds.Phase.Value == GamePhase.RoundActive ? rounds.Round.Value - 1 : rounds.Round.Value;
            return Upgrades.WindowOf(cleared + 1);
        }

        /// <summary>What the machine would charge to upgrade the gun in hand. -1 (with a reason in what) when it can't.</summary>
        long MachinePrice(out string what)
        {
            what = null;
            int slot = ActiveSlot.Value;
            if (Upgrades == null || GunInSlot(slot) < 0) return -1;
            int tier = TierInSlot(slot);
            if (tier >= Upgrades.MaxTier)
            {
                what = $"The {GunName(slot)} is fully upgraded";
                return -1;
            }
            if (Upgrades.machineOncePerWindow && MachineWindowUsed.Value == CurrentMachineWindow())
            {
                what = "The machine recharges with the next power-up";
                return -1;
            }
            what = $"upgrade the {GunName(slot)} to {Upgrades.Tier(tier + 1).suffix}";
            return GameConfig.Prices.UpgradeCost(tier + 1);
        }

        // ---------------------------------------------------------------- owner

        void SubscribeProgression()
        {
            PowerLevels.OnValueChanged += OnPowerLevelsChanged;
            Tier0.OnValueChanged += OnTier0Changed;
            Tier1.OnValueChanged += OnTier1Changed;
        }

        void UnsubscribeProgression()
        {
            PowerLevels.OnValueChanged -= OnPowerLevelsChanged;
            Tier0.OnValueChanged -= OnTier0Changed;
            Tier1.OnValueChanged -= OnTier1Changed;
        }

        void OnPowerLevelsChanged(ulong previous, ulong current)
        {
            ApplyOwnerBonuses();
            if (Upgrades == null || current == 0) return;
            for (int i = 0; i < Upgrades.PowerUpCount; i++)
            {
                int level = UpgradeData.LevelOf(current, i);
                if (level <= UpgradeData.LevelOf(previous, i)) continue;
                var p = Upgrades.PowerUpAt(i);
                LastPickText = $"{p.name} {ToRoman(level)}: {p.Describe(level)}";
                LastPickTime = Time.time;
                if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.Purchase, 0.8f);
                break;
            }
        }

        void ApplyOwnerBonuses()
        {
            if (_motor != null) _motor.SpeedMultiplier = 1f + Bonus(PowerStat.MoveSpeed);
            if (_gun == null) return;
            _gun.ReloadSpeed = 1f + Bonus(PowerStat.ReloadSpeed);
            if (_gun.Data != null) _gun.SetCapacityScale(CapacityScale(ActiveSlot.Value));
        }

        void OnTier0Changed(int previous, int current) => OnTierChanged(0, current > previous);
        void OnTier1Changed(int previous, int current) => OnTierChanged(1, current > previous);

        /// <summary>A gun came out of the machine (or was replaced): it comes back full, and in hand if it was.</summary>
        void OnTierChanged(int slot, bool upgraded)
        {
            if (GunInSlot(slot) < 0) return;
            FillSlot(slot);
            if (slot == ActiveSlot.Value) EquipSlot(slot, saveCurrent: false);
            if (upgraded && _uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.RoundEnd, 0.7f);
        }

        /// <summary>A slot's saved ammo to a full magazine and full reserve at its current capacity.</summary>
        void FillSlot(int slot)
        {
            var data = GameConfig.GunById(GunInSlot(slot));
            if (data == null) return;
            float scale = CapacityScale(slot);
            _slotMagazine[slot] = HitscanGun.ScaledMagazine(data, scale);
            _slotReserve[slot] = HitscanGun.ScaledReserve(data.maxReserve, scale);
        }

        public static string ToRoman(int n)
        {
            if (n <= 0 || n > 39) return n.ToString();
            string[] tens = { "", "X", "XX", "XXX" };
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            return tens[n / 10] + ones[n % 10];
        }

        // ---------------------------------------------------------------- server

        /// <summary>Host only. Deals this player three power-ups to pick from. An unpicked earlier offer takes its first card.</summary>
        public void ServerOfferPowerUps(bool empowered)
        {
            if (!IsServer || Upgrades == null || Upgrades.PowerUpCount == 0) return;
            if (HasPowerUpOffer) ServerPick(0);

            // Empowered offers come from what the player already owns, topped up with the rest.
            _draw.Clear();
            if (empowered)
                for (int i = 0; i < Upgrades.PowerUpCount; i++)
                    if (PowerLevel(i) > 0) _draw.Add(i);
            Shuffle(_draw);
            if (_draw.Count < 3)
            {
                var rest = new List<int>();
                for (int i = 0; i < Upgrades.PowerUpCount; i++)
                    if (!_draw.Contains(i)) rest.Add(i);
                Shuffle(rest);
                foreach (int i in rest)
                    if (_draw.Count < 3) _draw.Add(i);
            }

            int offer = empowered ? 1 << 12 : 0;
            for (int card = 0; card < 3; card++)
                offer |= (card < _draw.Count ? _draw[card] : NoPowerUp) << (card * 4);
            PowerOffer.Value = offer;
        }

        static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        void ServerPick(int card)
        {
            int id = OfferedPowerUp(card);
            if (id == NoPowerUp) id = OfferedPowerUp(0);
            int levels = OfferLevels;
            PowerOffer.Value = -1;
            if (id == NoPowerUp || Upgrades.PowerUpAt(id) == null) return;

            float oldMax = MaxHealthNow;
            PowerLevels.Value = UpgradeData.WithLevel(PowerLevels.Value, id, PowerLevel(id) + levels);
            // More max health arrives as health, so Thick Skin helps straight away.
            float gained = MaxHealthNow - oldMax;
            if (gained > 0f && State.Value == LifeState.Alive) Health.Value = Mathf.Min(MaxHealthNow, Health.Value + gained);
        }

        [Rpc(SendTo.Server)]
        void PickPowerUpRpc(int card)
        {
            if (!HasPowerUpOffer || card < 0 || card > 2 || OfferedPowerUp(card) == NoPowerUp) return;
            ServerPick(card);
        }

        void ServerUpgradeGunInHand()
        {
            int slot = ActiveSlot.Value;
            TierVariable(slot).Value = Mathf.Min(Upgrades.MaxTier, TierInSlot(slot) + 1);
            MachineWindowUsed.Value = CurrentMachineWindow();
        }

        /// <summary>Host only. No power-ups, no pending offer and a charged machine (a fresh game).</summary>
        public void ServerResetProgression()
        {
            PowerLevels.Value = 0;
            PowerOffer.Value = -1;
            MachineWindowUsed.Value = -1;
        }

        /// <summary>Chain rounds: the hit jumps to the nearest other zombie in range.</summary>
        void ServerChain(NetZombie primary, Vector3 end, double baseDamage, Vector3 direction)
        {
            int count = Physics.OverlapSphereNonAlloc(end, Upgrades.chainRange, _overlap, ~0, QueryTriggerInteraction.Ignore);
            NetZombie best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var z = _overlap[i].GetComponentInParent<NetZombie>();
                if (z == null || z == primary || !z.IsAlive) continue;
                float sqr = (z.transform.position - end).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = z;
                    bestSqr = sqr;
                }
            }
            if (best == null) return;

            Vector3 point = best.transform.position + Vector3.up * 1.2f;
            long damage = System.Math.Max(1L, (long)System.Math.Round(baseDamage * Upgrades.chainDamage));
            var result = best.TakeDamage(new DamageInfo(damage, point, (point - end).normalized, false, OwnerClientId));
            AwardPoints(result, false);
            ChainFxRpc(end, point);
        }

        /// <summary>Explosive rounds: every zombie near the impact (except the one hit directly) takes splash damage.</summary>
        void ServerExplode(NetZombie primary, Vector3 end, double baseDamage, Vector3 direction)
        {
            int count = Physics.OverlapSphereNonAlloc(end, Upgrades.explosiveRadius, _overlap, ~0, QueryTriggerInteraction.Ignore);
            _splashed.Clear();
            long damage = System.Math.Max(1L, (long)System.Math.Round(baseDamage * Upgrades.explosiveDamage));
            for (int i = 0; i < count; i++)
            {
                var z = _overlap[i].GetComponentInParent<NetZombie>();
                if (z == null || z == primary || !z.IsAlive || !_splashed.Add(z)) continue;
                var result = z.TakeDamage(new DamageInfo(damage, z.transform.position + Vector3.up, direction, false, OwnerClientId));
                AwardPoints(result, false);
            }
        }

        [Rpc(SendTo.Everyone)]
        void ChainFxRpc(Vector3 from, Vector3 to)
        {
            FxSystem.Instance?.ShowTracer(from, to);
            FxSystem.Instance?.Impact(to, (from - to).normalized, FxSystem.Spark, 10, 4f, 0.07f);
        }
    }
}
