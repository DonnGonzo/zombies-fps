using System;
using System.Linq;
using System.Security.Cryptography;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.World;

namespace ZombiesFPS.Net
{
    public partial class NetPlayer
    {
        [Serializable] public sealed class CasinoView
        {
            public int version, station, active, wager; public bool playing, doubleAllowed, splitAllowed;
            public string dealer = "", result = ""; public string[] hands = Array.Empty<string>();
            public int[] reels = Array.Empty<int>();
        }
        public bool CasinoOpen { get; private set; }
        /// <summary>The highlighted bet or move (0-3) for controller players.</summary>
        public int CasinoCursor { get; private set; }
        public CasinoView Casino { get; private set; } = new CasinoView();
        BlackjackGame _blackjack;
        CasinoView _slotView;
        int _casinoVersion, _casinoStation = -1, _blackjackDeal, _tableSentVersion = -1;
        float _casinoDeadline, _casinoNextBet, _casinoSentAt;
        bool _casinoPaid, _casinoPending;
        public float CasinoRevealAt { get; private set; }
        static readonly RandomNumberGenerator s_CasinoRng = RandomNumberGenerator.Create();
        static readonly byte[] s_CasinoBytes = new byte[4];
        static int CasinoRandom(int max)
        {
            // Unbiased rejection sampling on the host; never accept RNG or cards supplied by a client.
            uint value, limit = uint.MaxValue - uint.MaxValue % (uint)max;
            do { s_CasinoRng.GetBytes(s_CasinoBytes); value = BitConverter.ToUInt32(s_CasinoBytes, 0); } while (value >= limit);
            return (int)(value % (uint)max);
        }
        bool AtCasino(int station) => station >= 0 && station < ZoneMap.CasinoPositions.Length
            && Vector3.Distance(transform.position + Vector3.up, ZoneMap.CasinoPositions[station] + Vector3.up) <= 2.4f;
        void OwnerCasinoTick(bool canAct, bool picking)
        {
            bool focus = FocusedBuyable != null && FocusedBuyable.Kind == BuyableKind.Casino;
            if (!canAct || picking || !focus)
            {
                if (CasinoOpen) CasinoLeaveRpc();
                CasinoOpen = false; return;
            }
            if (GameInput.InteractPressed)
            {
                CasinoOpen = !CasinoOpen;
                if (!CasinoOpen) CasinoLeaveRpc();
                return;
            }
            if (!CasinoOpen) return;
            // The slot music starts as soon as a player steps up; spins (SlotSpunRpc) keep it going for everyone nearby.
            CasinoAudio.Keep(FocusedBuyable.Index, 1f);
            // On a controller the d-pad moves along the four bets (or moves) and A takes one.
            GameInput.ClaimPadMenu();
            var step = GameInput.DpadStep;
            int move = step.x != 0 ? step.x : -step.y;
            if (move != 0) CasinoCursor = (CasinoCursor + move + 4) % 4;
            int key = GameInput.NumberPressed;
            if (key < 0 && GameInput.SubmitPressed) key = CasinoCursor;
            if (key < 0 || key > 3 || _casinoPending || Time.unscaledTime < CasinoRevealAt || CasinoDealing) return;
            _casinoPending = true; _casinoSentAt = Time.unscaledTime;
            CasinoCommandRpc(FocusedBuyable.Index, key, Casino.version);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void CasinoCommandRpc(int station, int choice, int version)
        {
            if (!IsAlive || !AtCasino(station) || RoundManager.Instance != null && RoundManager.Instance.IsGameOver
                || choice < 0 || choice > 3 || version != _casinoVersion) { SendCasino(); return; }
            if (_blackjack != null && !_blackjack.Done)
            {
                if (station != _casinoStation || !_blackjack.Act(choice, _blackjack.Revision, Points.Value, out int debit)) { SendCasino(); return; }
                Points.Value -= debit; _casinoVersion++; _casinoDeadline = Time.time + 60f;
                PayBlackjack(); SendCasino(); return;
            }
            int bet = CasinoRules.Bets[choice];
            if (Time.time < _casinoNextBet || Points.Value < bet) { SendCasino(); return; }
            _casinoVersion++; _casinoStation = station; _casinoNextBet = Time.time + 1.8f;
            Points.Value -= bet;
            if (station == ZoneMap.BlackjackStation)
            {
                _blackjack = new BlackjackGame(bet, CasinoRandom); _slotView = null; _blackjackDeal++; _casinoPaid = false; _casinoDeadline = Time.time + 60f;
                PayBlackjack(); SendCasino();
            }
            else
            {
                _blackjack = null;
                int a = CasinoRules.Symbol(CasinoRandom(100)), b = CasinoRules.Symbol(CasinoRandom(100)), c = CasinoRules.Symbol(CasinoRandom(100));
                long payout = bet * (long)CasinoRules.SlotMultiplier(a,b,c); Points.Value += payout;
                _slotView = new CasinoView { version = _casinoVersion, station = station, wager = bet, reels = new[] { a,b,c }, result = $"Bet {bet:N0}  |  Returned {payout:N0}  |  Net {payout-bet:+#,0;-#,0;0}" };
                CasinoViewRpc(JsonUtility.ToJson(_slotView));
                SlotSpunRpc(station, a, b, c, CasinoRules.SlotMultiplier(a,b,c), bet);
            }
        }
        /// <summary>
        /// Every machine sees the spin's reels turn and land on the host's symbols, and hears the reels and, when
        /// they stop, the win or loss (a 1x return is a loss).
        /// </summary>
        [Rpc(SendTo.Everyone)]
        void SlotSpunRpc(int station, int a, int b, int c, int multiplier, int bet)
        {
            CasinoAudio.Spun(station, multiplier);
            SlotReels.Spin(station, a, b, c, multiplier, bet);
        }
        /// <summary>The cards on the table, for everyone: the host's hand with the hole card hidden until it settles.</summary>
        [Rpc(SendTo.Everyone)]
        void BlackjackTableRpc(string json)
        {
            if (BlackjackTable.Instance != null) BlackjackTable.Instance.Show(JsonUtility.FromJson<BlackjackTableState>(json));
        }
        /// <summary>True on the owner while the table is still dealing out its latest hand: the next move waits for the cards.</summary>
        public bool CasinoDealing => BlackjackTable.Instance != null && BlackjackTable.Instance.State.owner == (int)OwnerClientId
            && Time.unscaledTime < BlackjackTable.Instance.SettledAt;
        void SendTable()
        {
            if (_blackjack == null || _tableSentVersion == _casinoVersion) return;
            _tableSentVersion = _casinoVersion;
            var state = new BlackjackTableState
            {
                owner = (int)OwnerClientId, deal = _blackjackDeal, playing = !_blackjack.Done, active = _blackjack.Active,
                dealer = _blackjack.Dealer.Select((c, i) => i == 1 && !_blackjack.Done ? -1 : c).ToArray(),
                cards = _blackjack.Hands.SelectMany(h => h.Cards).ToArray(),
                counts = _blackjack.Hands.Select(h => h.Cards.Count).ToArray(),
                bets = _blackjack.Hands.Select(h => h.Bet).ToArray(),
            };
            for (int h = 0; h < _blackjack.Hands.Count; h++) if (_blackjack.Hands[h].Split) state.splitMask |= 1 << h;
            BlackjackTableRpc(JsonUtility.ToJson(state));
        }
        void PayBlackjack()
        {
            if (_blackjack == null || !_blackjack.Done || _casinoPaid) return;
            _casinoPaid = true; Points.Value += _blackjack.Return;
        }
        void ServerCasinoTick()
        {
            if (_blackjack == null || _blackjack.Done) return;
            if (!IsAlive || !AtCasino(_casinoStation) || Time.time >= _casinoDeadline || RoundManager.Instance != null && RoundManager.Instance.IsGameOver)
            {
                _blackjack.StandAll(); _casinoVersion++; PayBlackjack(); SendCasino();
            }
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void CasinoLeaveRpc()
        {
            if (_blackjack != null && !_blackjack.Done) { _blackjack.StandAll(); _casinoVersion++; PayBlackjack(); SendCasino(); }
        }
        void SendCasino()
        {
            // A refused command re-sends the last spin as it was, so the reels don't blank.
            if (_blackjack == null && _slotView != null) { CasinoViewRpc(JsonUtility.ToJson(_slotView)); return; }
            // The table first: the owner's HUD and move gate look at its dealing, and RPCs arrive in order.
            SendTable();
            var view = new CasinoView { version = _casinoVersion, station = _casinoStation };
            if (_blackjack != null)
            {
                view.playing = !_blackjack.Done; view.active = _blackjack.Active;
                view.doubleAllowed = _blackjack.CanDouble(Points.Value); view.splitAllowed = _blackjack.CanSplit(Points.Value);
                view.dealer = _blackjack.Done ? string.Join("  ", _blackjack.Dealer.Select(CasinoRules.CardName)) + "    [" + CasinoRules.Total(_blackjack.Dealer) + "]" : CasinoRules.CardName(_blackjack.Dealer[0]) + "   [HIDDEN]";
                view.hands = _blackjack.Hands.Select((h,i) => $"{(view.playing && i==view.active ? "> " : "")}{string.Join("  ", h.Cards.Select(CasinoRules.CardName))}    [{CasinoRules.Total(h.Cards)}]    Bet {h.Bet:N0}").ToArray();
                view.wager = _blackjack.Hands.Sum(h => h.Bet);
                view.result = view.playing ? "Choose your action. Dealer's hole card is hidden." : $"Bet {view.wager:N0}  |  Returned {_blackjack.Return:N0}  |  Net {_blackjack.Return-view.wager:+#,0;-#,0;0}";
            }
            CasinoViewRpc(JsonUtility.ToJson(view));
        }
        [Rpc(SendTo.Owner)]
        void CasinoViewRpc(string json)
        {
            var next = JsonUtility.FromJson<CasinoView>(json);
            if (next.version < Casino.version) return;
            if (next.reels.Length == 3 && next.version > Casino.version) CasinoRevealAt = Time.unscaledTime + 2f;
            Casino = next; _casinoPending = false;
        }
    }
}
