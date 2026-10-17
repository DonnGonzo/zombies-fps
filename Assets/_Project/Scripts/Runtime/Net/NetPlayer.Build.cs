using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Weapons;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    /// <summary>The player's side of buildables: the Scrap Shield they carry, and credit for Buddy Bot kills.</summary>
    public partial class NetPlayer
    {
        /// <summary>What's left of the player's Scrap Shield (0 = none).</summary>
        public NetworkVariable<float> ShieldHealth = new NetworkVariable<float>(0f);
        /// <summary>After a shield breaks, the round a new one can be taken from a station.</summary>
        public NetworkVariable<int> ShieldReadyRound = new NetworkVariable<int>(0);

        /// <summary>Took this game's Scrap Cannon from a build station (one each per game).</summary>
        public NetworkVariable<bool> TookCannon = new NetworkVariable<bool>(false);

        Transform _shieldView;

        public bool HasShield => ShieldHealth.Value > 0f;

        /// <summary>Host only. A fresh Scrap Shield on the player's arm.</summary>
        public void ServerGiveShield()
        {
            ShieldHealth.Value = BuildManager.ShieldMaxHealth;
        }

        /// <summary>
        /// Host: the shield takes most of a hit from in front (not acid, which splashes round it). When it breaks,
        /// a new one can be taken from the next round.
        /// </summary>
        float ServerShieldBlock(float amount, Vector3 from, bool acid)
        {
            if (acid || ShieldHealth.Value <= 0f) return amount;
            Vector3 toAttacker = from - transform.position;
            toAttacker.y = 0f;
            Vector3 facing = transform.forward;
            facing.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f || Vector3.Dot(facing.normalized, toAttacker.normalized) < 0.17f) return amount;
            float blocked = Mathf.Min(ShieldHealth.Value, amount * BuildManager.ShieldBlockShare);
            ShieldHealth.Value -= blocked;
            if (ShieldHealth.Value <= 0.5f)
            {
                ShieldHealth.Value = 0f;
                int round = RoundManager.Instance != null ? RoundManager.Instance.DisplayRound : 1;
                ShieldReadyRound.Value = round + 1;
                ShieldBreakRpc();
            }
            else ShieldHitRpc();
            return amount - blocked;
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ShieldBreakRpc()
        {
            var at = transform.position + Vector3.up * 1.1f + transform.forward * 0.5f;
            FxSystem.Instance?.Impact(at, transform.forward, FxSystem.Dust, 16, 4f, 0.08f);
            if (IsOwner && _uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.KnifeHit, 1f);
        }

        [Rpc(SendTo.Owner)]
        void ShieldHitRpc()
        {
            if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.KnifeHit, 0.4f);
        }

        /// <summary>Host only. The player's Buddy Bot hit a zombie: points and kills count as theirs.</summary>
        public void ServerCreditRobotHit(NetZombie zombie, DamageResult result) => AwardPoints(zombie, result, false);

        /// <summary>Owner only (test console): moves this player.</summary>
        public void DebugTeleport(Vector3 position)
        {
            if (_motor != null) _motor.Teleport(position);
        }

        void ServerResetBuildables()
        {
            ShieldHealth.Value = 0f;
            ShieldReadyRound.Value = 0;
            TookCannon.Value = false;
        }

        /// <summary>Shows the shield on the left arm: in view for the owner, in front of the body for everyone else.</summary>
        void UpdateShieldView()
        {
            bool show = HasShield && State.Value == LifeState.Alive;
            if (_shieldView == null)
            {
                if (!show) return;
                var color = BuildManager.BuildableColors[BuildManager.Shield];
                if (IsOwner && _pivot != null)
                {
                    _shieldView = Greybox.Empty("Shield", _pivot, new Vector3(-0.42f, -0.32f, 0.5f));
                    _shieldView.localRotation = Quaternion.Euler(0f, 28f, 0f);
                    Greybox.Box("Plate", _shieldView, Vector3.zero, new Vector3(0.34f, 0.5f, 0.03f), new Color(0.35f, 0.37f, 0.38f), collider: false);
                    Greybox.Box("Rim", _shieldView, new Vector3(0f, 0.24f, -0.01f), new Vector3(0.36f, 0.04f, 0.04f), color, collider: false);
                    Greybox.DisableShadows(_shieldView.gameObject);
                }
                else if (_body != null)
                {
                    _shieldView = Greybox.Empty("Shield", _body, new Vector3(-0.2f, 1.1f, 0.42f));
                    Greybox.Box("Plate", _shieldView, Vector3.zero, new Vector3(0.6f, 0.9f, 0.05f), new Color(0.35f, 0.37f, 0.38f), collider: false);
                    Greybox.Box("Rim", _shieldView, new Vector3(0f, 0.44f, 0.01f), new Vector3(0.62f, 0.06f, 0.06f), color, collider: false);
                }
                else return;
            }
            if (_shieldView.gameObject.activeSelf != show) _shieldView.gameObject.SetActive(show);
        }
    }
}
