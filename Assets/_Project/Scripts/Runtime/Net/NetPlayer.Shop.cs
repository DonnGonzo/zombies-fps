using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.World;

namespace ZombiesFPS.Net
{
    public enum AttachmentStatus
    {
        Locked,
        ForSale,
        Owned,
    }

    /// <summary>
    /// XP, levels and the spawn shop for one player. The host awards XP during the run (RunXp); each player
    /// saves their own total on their PC and publishes what they brought in (ProfileXp). Attachments are
    /// bought per gun slot with in-run points and last until the gun is replaced or the run ends.
    /// </summary>
    public partial class NetPlayer
    {
        /// <summary>XP earned this run. Only the host changes it.</summary>
        public NetworkVariable<long> RunXp = new NetworkVariable<long>(0);
        /// <summary>XP the player brought from earlier runs, published by the owner from their save.</summary>
        public NetworkVariable<long> ProfileXp = new NetworkVariable<long>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Attachments on the gun in each slot, a bit per ShopData attachment.</summary>
        public NetworkVariable<int> Att0 = new NetworkVariable<int>(0);
        public NetworkVariable<int> Att1 = new NetworkVariable<int>(0);
        /// <summary>The camo picked for each gun in the main menu (4 bits per gun id), published by the owner.</summary>
        public NetworkVariable<ulong> CamoPicks = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>The prestige title picked in the main menu, published by the owner.</summary>
        public NetworkVariable<int> TitleId = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Prestige rank (0-10), published by the owner from their save.</summary>
        public NetworkVariable<int> Prestige = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>The main menu's camo and title picks for the local player. Fixed once the match starts.</summary>
        public static ulong LocalCamoPicks;
        public static int LocalTitle;

        long _savedXp;
        int _lastLevel = -1;

        /// <summary>Owner only: the shop menu is open (number keys buy).</summary>
        public bool ShopOpen { get; private set; }
        /// <summary>Owner only: the level reached last and when, for the HUD banner.</summary>
        public int LevelUpLevel { get; private set; }
        public float LevelUpTime { get; private set; } = -100f;

        static ShopData Shop => GameConfig.Shop;

        public long TotalXp => ProfileXp.Value + RunXp.Value;
        public int Level => Shop != null ? Shop.LevelFor(TotalXp) : 1;

        public string TitleName => Shop != null && Shop.TitleAt(TitleId.Value) != null ? Shop.TitleAt(TitleId.Value).name : "";
        /// <summary>Name with prestige rank and title, for name tags and the squad list, e.g. "P3 Ivan [Veteran]".</summary>
        public string TaggedName
        {
            get
            {
                string name = Prestige.Value > 0 ? $"P{Prestige.Value} {DisplayName}" : DisplayName;
                return string.IsNullOrEmpty(TitleName) ? name : $"{name} [{TitleName}]";
            }
        }

        /// <summary>XP still needed for the next level (0 at the level cap).</summary>
        public long XpToNextLevel => Shop == null || Level >= Shop.maxLevel ? 0 : Shop.XpToNext(Level) - Shop.XpIntoLevel(TotalXp);
        /// <summary>How far through the current level, 0-1 (1 at the level cap).</summary>
        public float LevelProgress => Shop == null || Level >= Shop.maxLevel ? 1f : (float)Shop.XpIntoLevel(TotalXp) / Shop.XpToNext(Level);

        /// <summary>The camo colour of the gun in a slot, or null for the gun's own colours.</summary>
        public Color? CamoColorFor(int slot)
        {
            int gunId = GunInSlot(slot);
            return Shop != null && gunId >= 0 ? Shop.CamoColor(ShopData.CamoOf(CamoPicks.Value, gunId)) : null;
        }

        public int AttachmentsInSlot(int slot) => slot == 0 ? Att0.Value : slot == 1 ? Att1.Value : 0;
        NetworkVariable<int> AttVariable(int slot) => slot == 0 ? Att0 : Att1;

        /// <summary>The gun in a slot with its attachments applied (null for an empty slot).</summary>
        public GunData EffectiveGun(int slot)
        {
            var gun = GameConfig.GunById(GunInSlot(slot));
            return Shop != null ? Shop.Apply(gun, AttachmentsInSlot(slot)) : gun;
        }

        /// <summary>Whether this player could buy an attachment for the gun in their hands right now.</summary>
        public AttachmentStatus StatusOf(int id)
        {
            var a = Shop != null ? Shop.At(id) : null;
            if (a == null || Level < a.unlockLevel) return AttachmentStatus.Locked;
            return ShopData.Has(AttachmentsInSlot(ActiveSlot.Value), id) ? AttachmentStatus.Owned : AttachmentStatus.ForSale;
        }

        // ---------------------------------------------------------------- owner

        void SubscribeShop()
        {
            RunXp.OnValueChanged += OnRunXpChanged;
            Att0.OnValueChanged += OnAtt0Changed;
            Att1.OnValueChanged += OnAtt1Changed;
            _savedXp = Shop != null && Shop.saveBetweenRuns ? ProfileSave.LoadXp() : 0;
            ProfileXp.Value = _savedXp;
            Prestige.Value = Shop != null && Shop.saveBetweenRuns ? Mathf.Min(ProfileSave.LoadPrestige(), Shop.MaxPrestige) : 0;
            _lastLevel = Level;
            if (Shop != null && Shop.saveBetweenRuns) ProfileSave.SaveBestLevel(_lastLevel);
            PublishCosmetics();
        }

        /// <summary>Sends the menu's picks, dropping any the player's level hasn't unlocked.</summary>
        void PublishCosmetics()
        {
            if (Shop == null) return;
            int level = Mathf.Max(Level, Shop.saveBetweenRuns ? ProfileSave.LoadBestLevel() : 1);
            ulong picks = 0;
            for (int gun = 0; gun < ShopData.MaxCamoGuns; gun++)
            {
                int camo = ShopData.CamoOf(LocalCamoPicks, gun);
                if (Shop.IsCamoUnlocked(camo, level, Prestige.Value)) picks = ShopData.WithCamo(picks, gun, camo);
            }
            CamoPicks.Value = picks;
            TitleId.Value = Shop.IsTitleUnlocked(LocalTitle, level) ? LocalTitle : 0;
        }

        void UnsubscribeShop()
        {
            RunXp.OnValueChanged -= OnRunXpChanged;
            Att0.OnValueChanged -= OnAtt0Changed;
            Att1.OnValueChanged -= OnAtt1Changed;
        }

        void OnRunXpChanged(long previous, long current)
        {
            bool persist = Shop != null && Shop.saveBetweenRuns;
            if (current < previous)
            {
                // A restart zeroes the run: bank what was earned (if levels carry over) and start counting again.
                if (persist) _savedXp += previous;
                ProfileXp.Value = _savedXp;
                _lastLevel = Level;
                return;
            }
            if (persist) ProfileSave.SaveXp(_savedXp + current);
            int level = Level;
            if (persist) ProfileSave.SaveBestLevel(level);
            if (_lastLevel >= 0 && level > _lastLevel)
            {
                LevelUpLevel = level;
                LevelUpTime = Time.time;
            }
            _lastLevel = level;
        }

        void OnAtt0Changed(int previous, int current) => OnAttachmentsChanged(0);
        void OnAtt1Changed(int previous, int current) => OnAttachmentsChanged(1);

        /// <summary>New attachments: put the gun back in hand with its new stats, keeping its ammo.</summary>
        void OnAttachmentsChanged(int slot)
        {
            if (slot == ActiveSlot.Value && GunInSlot(slot) >= 0) EquipSlot(slot, saveCurrent: true);
        }

        /// <summary>Owner, every frame: opening and closing the shop, and buying from it with the number keys.</summary>
        void OwnerShopTick(bool canAct, bool picking)
        {
            bool atShop = FocusedBuyable != null && FocusedBuyable.Kind == BuyableKind.Shop;
            if (!canAct || !atShop)
            {
                ShopOpen = false;
                return;
            }
            if (GameInput.InteractPressed) ShopOpen = !ShopOpen;
            if (!ShopOpen || picking) return;
            // On a controller the d-pad picks a row and A buys it.
            GameInput.ClaimPadMenu();
            int step = GameInput.DpadStep.y;
            if (step != 0) ShopCursor = NextShopRow(ShopCursor, -step);
            int choice = GameInput.NumberPressed;
            if (choice < 0 && GameInput.SubmitPressed) choice = ShopCursor;
            if (choice >= 0 && Shop != null && choice < Shop.Count) BuyAttachmentRpc(choice);
        }

        /// <summary>The highlighted shop row for controller players.</summary>
        public int ShopCursor { get; private set; }

        /// <summary>The next row up or down that isn't still locked, wrapping round; stays put if none is.</summary>
        int NextShopRow(int from, int dir)
        {
            int n = Shop != null ? Shop.Count : 0;
            if (n == 0) return 0;
            for (int k = 1; k <= n; k++)
            {
                int i = ((from + dir * k) % n + n) % n;
                if (StatusOf(i) != AttachmentStatus.Locked) return i;
            }
            return Mathf.Clamp(from, 0, n - 1);
        }

        // ---------------------------------------------------------------- server

        /// <summary>Host only. XP for this run.</summary>
        public void ServerAddXp(long xp)
        {
            if (IsServer && xp > 0) RunXp.Value += xp;
        }

        [Rpc(SendTo.Server)]
        void BuyAttachmentRpc(int id)
        {
            var rounds = RoundManager.Instance;
            var a = Shop != null ? Shop.At(id) : null;
            if (a == null || State.Value != LifeState.Alive || (rounds != null && rounds.IsGameOver)) return;
            if (!NearShop()) return;
            int slot = ActiveSlot.Value;
            if (GunInSlot(slot) < 0 || StatusOf(id) != AttachmentStatus.ForSale) return;
            if (Points.Value < a.price)
            {
                PurchaseResultRpc(false);
                return;
            }
            Points.Value -= a.price;
            AttVariable(slot).Value = Shop.WithAttachment(AttachmentsInSlot(slot), id);
            PurchaseResultRpc(true);
        }

        bool NearShop()
        {
            var map = GameConfig.Map;
            if (map == null) return false;
            foreach (var b in map.Buyables)
            {
                if (b.Kind != BuyableKind.Shop) continue;
                Vector3 d = b.Position - transform.position;
                d.y = 0f;
                if (d.magnitude <= b.Range + 1f) return true;
            }
            return false;
        }
    }
}
