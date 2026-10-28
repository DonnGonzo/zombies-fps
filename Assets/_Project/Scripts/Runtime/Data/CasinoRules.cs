using System;
using System.Collections.Generic;
using System.Linq;

namespace ZombiesFPS.Data
{
    /// <summary>Pure casino rules. Credits returned include the original wager. Randomness is supplied by the host.</summary>
    public static class CasinoRules
    {
        public static readonly int[] Bets = { 100, 250, 500, 1000 };
        public static readonly string[] Symbols = { "CHERRY", "LEMON", "BELL", "BAR", "DIAMOND", "SEVEN" };
        public static readonly int[] Weights = { 40, 25, 18, 10, 5, 2 };
        public static readonly int[] Multipliers = { 5, 10, 20, 40, 100, 500 };
        public static int Symbol(int stop)
        {
            if (stop < 0 || stop >= 100) throw new ArgumentOutOfRangeException(nameof(stop));
            for (int i = 0; i < Weights.Length; i++) { if (stop < Weights[i]) return i; stop -= Weights[i]; }
            throw new InvalidOperationException();
        }
        public static int SlotMultiplier(int a, int b, int c) => a == b && b == c ? Multipliers[a] : (a == 0 ? 1 : 0) + (b == 0 ? 1 : 0) + (c == 0 ? 1 : 0) == 2 ? 1 : 0;
        public static int Rank(int card) => card % 13 + 1;
        public static int Value(int card) => Math.Min(Rank(card), 10);
        public static int Total(IEnumerable<int> cards)
        {
            int total = 0; bool ace = false;
            foreach (int c in cards) { total += Value(c); ace |= Rank(c) == 1; }
            return ace && total + 10 <= 21 ? total + 10 : total;
        }
        /// <summary>
        /// What one settled hand returns (bet included): 0 bust or beaten, the bet on a push, double on a win, 2.5x on
        /// a natural (two-card 21 on an unsplit hand). A dealer natural beats everything but a player natural, which pushes.
        /// </summary>
        public static long HandReturn(IList<int> cards, bool split, int bet, IList<int> dealer)
        {
            int v = Total(cards);
            if (v > 21) return 0;
            bool natural = !split && cards.Count == 2 && v == 21;
            bool dealerNatural = dealer.Count == 2 && Total(dealer) == 21;
            int d = Total(dealer);
            if (dealerNatural) return natural ? bet : 0;
            if (natural) return bet * 5L / 2;
            if (d > 21 || v > d) return bet * 2L;
            return v == d ? bet : 0;
        }
        public static string CardName(int card) => (Rank(card) == 1 ? "A" : Rank(card) == 11 ? "J" : Rank(card) == 12 ? "Q" : Rank(card) == 13 ? "K" : Rank(card).ToString()) + new[] { "S", "H", "D", "C" }[card / 13 % 4];
    }

    public sealed class BlackjackGame
    {
        public sealed class Hand
        {
            public readonly List<int> Cards = new List<int>();
            public int Bet; public bool Split, Finished;
            public bool Natural => !Split && Cards.Count == 2 && CasinoRules.Total(Cards) == 21;
        }
        readonly Func<int, int> _next;
        readonly List<int> _shoe = new List<int>();
        public readonly List<Hand> Hands = new List<Hand>();
        public readonly List<int> Dealer = new List<int>();
        public int Active { get; private set; }
        public bool Done { get; private set; }
        public long Return { get; private set; }
        public int Revision { get; private set; }
        public BlackjackGame(int bet, Func<int,int> next)
        {
            if (!CasinoRules.Bets.Contains(bet)) throw new ArgumentOutOfRangeException(nameof(bet));
            _next = next;
            for (int d = 0; d < 6; d++) for (int c = 0; c < 52; c++) _shoe.Add(c);
            for (int i = _shoe.Count - 1; i > 0; i--) { int j = next(i + 1); (_shoe[i], _shoe[j]) = (_shoe[j], _shoe[i]); }
            var hand = new Hand { Bet = bet }; Hands.Add(hand);
            hand.Cards.Add(Draw()); Dealer.Add(Draw()); hand.Cards.Add(Draw()); Dealer.Add(Draw());
            if (hand.Natural || CasinoRules.Total(Dealer) == 21) Settle();
        }
        int Draw() { int c = _shoe[_shoe.Count - 1]; _shoe.RemoveAt(_shoe.Count - 1); return c; }
        public bool CanDouble(long balance) => !Done && Hands[Active].Cards.Count == 2 && balance >= Hands[Active].Bet;
        public bool CanSplit(long balance) => !Done && Hands.Count < 4 && Hands[Active].Cards.Count == 2 && CasinoRules.Value(Hands[Active].Cards[0]) == CasinoRules.Value(Hands[Active].Cards[1]) && balance >= Hands[Active].Bet;
        // 0 hit, 1 stand, 2 double, 3 split. Invalid/replayed commands never draw a card or debit money.
        public bool Act(int action, int revision, long balance, out int debit)
        {
            debit = 0;
            if (Done || revision != Revision || action < 0 || action > 3) return false;
            var h = Hands[Active];
            if (action == 2 && !CanDouble(balance) || action == 3 && !CanSplit(balance)) return false;
            Revision++;
            if (action == 0) { h.Cards.Add(Draw()); h.Finished = CasinoRules.Total(h.Cards) >= 21; }
            if (action == 1) h.Finished = true;
            if (action == 2) { debit = h.Bet; h.Bet *= 2; h.Cards.Add(Draw()); h.Finished = true; }
            if (action == 3)
            {
                debit = h.Bet; var other = new Hand { Bet = h.Bet, Split = true };
                other.Cards.Add(h.Cards[1]); h.Cards.RemoveAt(1); h.Split = true;
                Hands.Insert(Active + 1, other); h.Cards.Add(Draw()); other.Cards.Add(Draw());
                // Split aces receive one card each and cannot be resplit. Split 21 pays even money.
                bool aces = CasinoRules.Rank(h.Cards[0]) == 1;
                h.Finished = aces || CasinoRules.Total(h.Cards) == 21;
                other.Finished = aces || CasinoRules.Total(other.Cards) == 21;
            }
            while (Active < Hands.Count && Hands[Active].Finished) Active++;
            if (Active >= Hands.Count) Settle();
            return true;
        }
        public void StandAll() { if (!Done) { foreach (var h in Hands) h.Finished = true; Settle(); } }
        void Settle()
        {
            if (Done) return;
            bool dealerNatural = Dealer.Count == 2 && CasinoRules.Total(Dealer) == 21;
            if (!dealerNatural && Hands.Any(h => !h.Natural && CasinoRules.Total(h.Cards) <= 21))
                while (CasinoRules.Total(Dealer) < 17) Dealer.Add(Draw());
            foreach (var h in Hands) Return += CasinoRules.HandReturn(h.Cards, h.Split, h.Bet, Dealer);
            Done = true;
        }
    }
}
