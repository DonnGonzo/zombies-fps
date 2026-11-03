using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The casino's faces: the card atlas and reel strip drawn by tools/casino/make_casino_faces.py
    /// (Resources/Casino), on the Backlit material so they read in the Depot's gloom. Without the art the cards and
    /// reels fall back to plain colours.
    /// </summary>
    public static class CasinoFaces
    {
        /// <summary>The playing card, 1.35x a real one so it reads from where the player stands.</summary>
        public const float CardWidth = 0.086f, CardLength = 0.112f;
        const int Columns = 13, Rows = 5;
        static Material s_Cards, s_Reel;
        static readonly Dictionary<int, Mesh> s_CardMeshes = new Dictionary<int, Mesh>();

        static Material Make(string name, string texture, float intensity, Color fallback)
        {
            var tex = Resources.Load<Texture2D>(texture);
            var template = Resources.Load<Material>("Materials/Backlit");
            Material mat;
            if (template != null && template.shader != null && template.shader.isSupported)
            {
                mat = new Material(template);
                mat.SetFloat("_Intensity", intensity);
            }
            else mat = PropModels.Textured(name, null, null, 0f, 0.2f);
            mat.name = name;
            if (tex != null) mat.mainTexture = tex;
            else mat.color = fallback;
            return mat;
        }

        public static Material Cards => s_Cards != null ? s_Cards : s_Cards = Make("Casino cards", "Casino/cards", 0.92f, Color.white);
        public static Material Reel => s_Reel != null ? s_Reel : s_Reel = Make("Slot reel", "Casino/slot_reel", 1.05f, new Color(0.95f, 0.92f, 0.85f));

        static Rect CardCell(int card)
        {
            int col, row;
            if (card < 0) { col = 0; row = 4; }
            else { col = card % 13; row = card / 13 % 4; }
            // Rows run top to bottom in the image; UVs from the bottom. A pixel's inset keeps neighbours from bleeding.
            float w = 1f / Columns, h = 1f / Rows, inset = 1.5f / 2048f;
            return new Rect(col * w + inset, 1f - (row + 1) * h + inset * 2f, w - inset * 2f, h - inset * 4f);
        }

        /// <summary>
        /// A card lying face up in its own space: the face on top (+y), the back underneath, the card's top edge
        /// towards -z (away from a player standing on the +z side). <paramref name="card"/> -1 is a hidden card (back on both sides).
        /// </summary>
        public static Mesh CardMesh(int card)
        {
            if (s_CardMeshes.TryGetValue(card, out var cached) && cached != null) return cached;
            float x = CardWidth / 2f, z = CardLength / 2f, lift = 0.0004f;
            var face = CardCell(card);
            var back = CardCell(-1);
            var mesh = new Mesh { name = card < 0 ? "Card back" : "Card " + CasinoRules.CardName(card) };
            // Face (seen from above): u runs from the player's left (+x) to right (-x), v from the near edge (+z) to the far one.
            mesh.vertices = new[]
            {
                new Vector3(x, lift, z), new Vector3(x, lift, -z), new Vector3(-x, lift, -z), new Vector3(-x, lift, z),
                new Vector3(x, -lift, z), new Vector3(x, -lift, -z), new Vector3(-x, -lift, -z), new Vector3(-x, -lift, z),
            };
            mesh.uv = new[]
            {
                new Vector2(face.xMin, face.yMin), new Vector2(face.xMin, face.yMax), new Vector2(face.xMax, face.yMax), new Vector2(face.xMax, face.yMin),
                new Vector2(back.xMax, back.yMin), new Vector2(back.xMax, back.yMax), new Vector2(back.xMin, back.yMax), new Vector2(back.xMin, back.yMin),
            };
            // Clockwise seen from the side each face shows to.
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            s_CardMeshes[card] = mesh;
            return mesh;
        }
    }

    /// <summary>
    /// Three reels behind a slot machine's window that really turn: the host's spin result (SlotSpunRpc) arrives on
    /// every machine, the reels whirl and stop left to right on those symbols in step with the HUD and the slot sounds
    /// (1.30, 1.65 and 2.00 s), and a win lights the cabinet and shows the payout above the reels for everyone nearby.
    /// The reel window was measured on the a-slot-machine model scaled to 2 m: centred 5 cm right of the model's
    /// middle, 0.79-0.95 m up, its glass face 13.6 cm in front of the base's centre.
    /// </summary>
    public sealed class SlotReels : MonoBehaviour
    {
        static readonly SlotReels[] s_Stations = new SlotReels[ZoneMap.BlackjackStation];

        /// <summary>Each reel's 12 faces in order up the reel (each face is 30 degrees); every symbol shows at least once.</summary>
        static readonly int[] Strip = { 0, 1, 2, 0, 3, 1, 0, 4, 2, 1, 5, 0 };
        const int Faces = 12, SlicesPerFace = 3;
        const float FaceAngle = 360f / Faces;
        const float Radius = 0.2f, ReelWidth = 0.145f, ReelGap = 0.162f;
        static readonly Vector3 WindowCentre = new Vector3(0.05f, 0.868f, 0.136f);
        static Mesh s_ReelMesh;

        readonly Transform[] _reels = new Transform[3];
        readonly float[] _angle = new float[3], _from = new float[3], _to = new float[3], _start = new float[3], _stop = new float[3];
        TextMesh _label;
        Light _light;
        float _revealAt = -1f, _labelUntil, _flashUntil;
        int _multiplier, _bet;
        bool _revealed = true;

        /// <summary>Builds the reels into the machine standing on <paramref name="floor"/> (the model's base centre), front facing +z.</summary>
        public static SlotReels Build(Transform parent, int station, Vector3 floor)
        {
            var go = new GameObject("Slot " + station + " reels");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(floor, Quaternion.identity);
            var slot = go.AddComponent<SlotReels>();
            slot.Assemble(station);
            if (station >= 0 && station < s_Stations.Length) s_Stations[station] = slot;
            return slot;
        }

        void Assemble(int station)
        {
            var axisCentre = WindowCentre - new Vector3(0f, 0f, Radius);
            var mat = CasinoFaces.Reel;
            for (int i = 0; i < 3; i++)
            {
                var reel = new GameObject("Reel " + i);
                reel.transform.SetParent(transform, false);
                reel.transform.localPosition = axisCentre + Vector3.right * (i - 1) * ReelGap;
                reel.AddComponent<MeshFilter>().sharedMesh = ReelMesh();
                var r = reel.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _reels[i] = reel.transform;
                // Idle machines show a different mix each; no two reels line up at random.
                _angle[i] = FaceAngle * ((station * 5 + i * 4 + 1) % Faces);
                Turn(i);
            }
            // Dark partitions between and beside the reels hide the drums' edges and the painted reels behind.
            for (int k = 0; k < 4; k++)
            {
                float x = WindowCentre.x + (k - 1.5f) * ReelGap;
                var wall = Greybox.Box("Reel divider", transform, new Vector3(x, WindowCentre.y, WindowCentre.z - 0.04f),
                    new Vector3(ReelGap - ReelWidth + 0.004f, 0.24f, 0.09f), new Color(0.05f, 0.04f, 0.04f), collider: false);
                wall.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // The pay line across the middle row.
            var line = Greybox.Prim(PrimitiveType.Cube, "Pay line", transform, WindowCentre + new Vector3(0f, 0f, 0.006f),
                new Vector3(ReelGap * 3f, 0.004f, 0.002f), new Color(1f, 0.15f, 0.1f), collider: false, unlit: true);
            line.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _label = Greybox.Label("Slot payout", transform, new Vector3(WindowCentre.x, 1.13f, 0.32f), "", new Color(1f, 0.82f, 0.3f), 0.1f);
            _label.gameObject.SetActive(false);
            var lightGo = new GameObject("Slot win light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(WindowCentre.x, 1.05f, 0.55f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 2.6f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
            _light.enabled = false;
        }

        /// <summary>A drum of <see cref="Faces"/> flat faces round the local x axis; face k is centred k*30 degrees up from the front (+z).</summary>
        static Mesh ReelMesh()
        {
            if (s_ReelMesh != null) return s_ReelMesh;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            float half = ReelWidth / 2f;
            for (int f = 0; f < Faces; f++)
            {
                // The symbol's cell in the strip (cells run top to bottom; UVs from the bottom).
                float v0 = 1f - (Strip[f] + 1) / 6f, v1 = 1f - Strip[f] / 6f;
                for (int s = 0; s < SlicesPerFace; s++)
                {
                    float a0 = (f - 0.5f + (float)s / SlicesPerFace) * FaceAngle, a1 = a0 + FaceAngle / SlicesPerFace;
                    float t0 = (float)s / SlicesPerFace, t1 = (float)(s + 1) / SlicesPerFace;
                    Vector3 P(float deg) => new Vector3(0f, Mathf.Sin(deg * Mathf.Deg2Rad), Mathf.Cos(deg * Mathf.Deg2Rad)) * Radius;
                    int b = verts.Count;
                    var lo = P(a0); var hi = P(a1);
                    // u from the left as seen from the front (+x is the viewer's left when facing -z).
                    verts.Add(new Vector3(half, lo.y, lo.z)); uvs.Add(new Vector2(0f, Mathf.Lerp(v0, v1, t0)));
                    verts.Add(new Vector3(half, hi.y, hi.z)); uvs.Add(new Vector2(0f, Mathf.Lerp(v0, v1, t1)));
                    verts.Add(new Vector3(-half, hi.y, hi.z)); uvs.Add(new Vector2(1f, Mathf.Lerp(v0, v1, t1)));
                    verts.Add(new Vector3(-half, lo.y, lo.z)); uvs.Add(new Vector2(1f, Mathf.Lerp(v0, v1, t0)));
                    var n = P((a0 + a1) / 2f).normalized;
                    for (int k = 0; k < 4; k++) normals.Add(n);
                    tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
            }
            s_ReelMesh = new Mesh { name = "Slot reel" };
            s_ReelMesh.SetVertices(verts);
            s_ReelMesh.SetUVs(0, uvs);
            s_ReelMesh.SetNormals(normals);
            s_ReelMesh.SetTriangles(tris, 0);
            s_ReelMesh.RecalculateBounds();
            return s_ReelMesh;
        }

        /// <summary>A spin's result at <paramref name="station"/>: the reels spin up now and stop on these symbols.</summary>
        public static void Spin(int station, int a, int b, int c, int multiplier, int bet)
        {
            if (station < 0 || station >= s_Stations.Length || s_Stations[station] == null) return;
            s_Stations[station].Begin(new[] { a, b, c }, multiplier, bet);
        }

        void Begin(int[] symbols, int multiplier, int bet)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < 3; i++)
            {
                // Reel i lands on a face showing its symbol after a few full turns; the further right, the longer it runs.
                float stopIn = CasinoAudio.RevealDelay - (2 - i) * 0.35f;
                int face = FaceFor(symbols[i], i);
                float current = Mathf.Repeat(_angle[i], 360f), target = face * FaceAngle;
                float travel = Mathf.Repeat(target - current, 360f) + 360f * Mathf.Ceil(stopIn * 1.6f);
                _from[i] = _angle[i] = current;
                _to[i] = current + travel;
                _start[i] = now;
                _stop[i] = now + stopIn;
            }
            _revealAt = now + CasinoAudio.RevealDelay;
            _revealed = false;
            _multiplier = multiplier; _bet = bet;
            _label.gameObject.SetActive(false);
        }

        /// <summary>One of the faces carrying <paramref name="symbol"/>; which one varies by reel so wins don't always look the same.</summary>
        static int FaceFor(int symbol, int reel)
        {
            int first = -1, count = 0;
            for (int f = 0; f < Faces; f++) if (Strip[f] == symbol) { if (first < 0) first = f; count++; }
            if (count <= 1) return Mathf.Max(0, first);
            int pick = UnityEngine.Random.Range(0, count);
            for (int f = 0; f < Faces; f++) if (Strip[f] == symbol && pick-- == 0) return f;
            return first;
        }

        void Turn(int i) => _reels[i].localRotation = Quaternion.Euler(_angle[i], 0f, 0f);

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < 3; i++)
            {
                if (_stop[i] <= 0f) continue;
                if (now < _stop[i])
                {
                    // Fast at once, easing into the stop.
                    float u = Mathf.InverseLerp(_start[i], _stop[i], now);
                    _angle[i] = Mathf.Lerp(_from[i], _to[i], 1f - Mathf.Pow(1f - u, 3f));
                }
                else
                {
                    // A little kick past the symbol and back, as a real reel's brake catches.
                    float t = now - _stop[i];
                    _angle[i] = _to[i] + 5f * Mathf.Exp(-t * 14f) * Mathf.Sin(t * 30f);
                    if (t > 0.45f) { _angle[i] = Mathf.Repeat(_to[i], 360f); _stop[i] = 0f; }
                }
                Turn(i);
            }

            if (!_revealed && now >= _revealAt)
            {
                _revealed = true;
                long net = (long)_bet * _multiplier - _bet;
                if (_multiplier > 1)
                {
                    _label.text = (_multiplier >= CasinoAudio.JackpotMultiplier ? "JACKPOT  +" : "WIN  +") + net.ToString("N0");
                    _label.color = _multiplier >= CasinoAudio.JackpotMultiplier ? new Color(1f, 0.35f, 0.9f) : new Color(1f, 0.82f, 0.3f);
                    _flashUntil = now + (_multiplier >= CasinoAudio.JackpotMultiplier ? 6f : 2.5f);
                }
                else
                {
                    _label.text = _multiplier == 1 ? "BET BACK" : "NO WIN";
                    _label.color = new Color(0.75f, 0.75f, 0.78f);
                }
                _label.gameObject.SetActive(true);
                _labelUntil = now + 4f;
            }
            if (_label.gameObject.activeSelf && now > _labelUntil) _label.gameObject.SetActive(false);

            bool flashing = now < _flashUntil;
            if (_light.enabled != flashing) _light.enabled = flashing;
            if (flashing)
            {
                bool jackpot = _multiplier >= CasinoAudio.JackpotMultiplier;
                _light.color = jackpot ? Color.HSVToRGB(Mathf.Repeat(now * 0.6f, 1f), 0.7f, 1f) : new Color(1f, 0.75f, 0.3f);
                _light.intensity = 1.6f + 1.4f * Mathf.Sin(now * (jackpot ? 18f : 11f));
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < s_Stations.Length; i++) if (s_Stations[i] == this) s_Stations[i] = null;
        }
    }

    /// <summary>
    /// A blackjack hand as everyone sees it on the table, sent by the host (BlackjackTableRpc) after every deal and
    /// move. The dealer's hole card is -1 until the hand settles, so no client is told it early.
    /// </summary>
    [Serializable]
    public sealed class BlackjackTableState
    {
        public int owner = -1, deal;
        public bool playing;
        public int active;
        public int[] dealer = Array.Empty<int>();
        /// <summary>Every hand's cards one after the other; <see cref="counts"/> says how many belong to each hand.</summary>
        public int[] cards = Array.Empty<int>();
        public int[] counts = Array.Empty<int>();
        public int[] bets = Array.Empty<int>();
        /// <summary>Bit h set when hand h came from a split (no natural possible).</summary>
        public int splitMask;

        public List<int> Hand(int h)
        {
            var list = new List<int>();
            int start = 0;
            for (int i = 0; i < h && i < counts.Length; i++) start += counts[i];
            for (int i = 0; h < counts.Length && i < counts[h]; i++) list.Add(cards[start + i]);
            return list;
        }
    }

    /// <summary>
    /// Real cards on Ivan's blackjack table: dealt out of a shoe at the dealer's side, sliding to the player's spot
    /// and the dealer's row, the hole card face down until the hand ends, splits sliding apart, totals and the result
    /// of each hand in small lettering on the felt, and the cards swept away before the next deal. Every client
    /// plays the same animation from the host's state, so anyone at the casino can watch.
    /// Table space: the model's base centre, the player on the +z side, the felt 0.765 m up (measured on the model).
    /// </summary>
    public sealed class BlackjackTable : MonoBehaviour
    {
        public static BlackjackTable Instance { get; private set; }

        const float DealGap = 0.36f, FlightTime = 0.3f, FlipTime = 0.28f, SweepAfter = 12f;
        static readonly Color Gold = new Color(1f, 0.82f, 0.3f), Win = new Color(0.45f, 1f, 0.5f), Lose = new Color(1f, 0.4f, 0.35f), Plain = new Color(0.92f, 0.9f, 0.85f);

        float _felt;
        Vector3 _shoe, _discard, _dealerRow, _playerRow, _dealerLabel, _handLabelRow;
        readonly List<Card> _cards = new List<Card>();
        readonly List<TextMesh> _handLabels = new List<TextMesh>();
        TextMesh _dealerText;
        BlackjackTableState _state = new BlackjackTableState();
        float _settledAt, _sweepAt = -1f;

        /// <summary>Until this time (unscaled) the latest hand is still being dealt or turned over.</summary>
        public float SettledAt => _settledAt;
        public BlackjackTableState State => _state;

        sealed class Card
        {
            public GameObject Go;
            public int Value;
            /// <summary>-1 for the dealer's row, otherwise the hand it belongs to.</summary>
            public int Hand, Index;
            public Vector3 From, To;
            public Quaternion FromRot, ToRot;
            public float Start, End, Arc;
            public bool Leaving;
            public bool Landed(float now) => now >= End;
            public bool FaceUp => Value >= 0 && Quaternion.Angle(ToRot, Quaternion.identity) < 90f;
        }

        /// <summary>
        /// The table whose base centre is <paramref name="floor"/>. <paramref name="feltHeight"/> is where cards lie;
        /// <paramref name="modelLayout"/> false for the stand-in hazard table, which has no measured felt.
        /// </summary>
        public static BlackjackTable Build(Transform parent, Vector3 floor, float feltHeight, bool modelLayout)
        {
            var go = new GameObject("Blackjack cards");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(floor, Quaternion.identity);
            var table = go.AddComponent<BlackjackTable>();
            table._felt = feltHeight + 0.004f;
            // The felt has raised chip pads round its middle; the rows sit where it's flat (see the class summary).
            float dealerZ = modelLayout ? -0.2f : -0.25f, playerZ = modelLayout ? 0.53f : 0.25f;
            table._shoe = new Vector3(-0.38f, table._felt, dealerZ);
            table._discard = new Vector3(0.4f, table._felt, dealerZ - 0.02f);
            table._dealerRow = new Vector3(0.1f, table._felt, dealerZ);
            table._playerRow = new Vector3(0f, table._felt, playerZ);
            table._dealerLabel = new Vector3(0f, table._felt + 0.03f, dealerZ - 0.13f);
            table._handLabelRow = new Vector3(0f, modelLayout ? 0.835f : table._felt + 0.01f, playerZ + 0.1f);
            // The shoe: a dark box with the back of the next card showing.
            var shoe = Greybox.Box("Card shoe", go.transform, table._shoe + new Vector3(0f, 0.035f, 0f), new Vector3(0.1f, 0.07f, 0.15f),
                new Color(0.12f, 0.06f, 0.04f), collider: false);
            shoe.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
            var top = new GameObject("Shoe card");
            top.transform.SetParent(go.transform, false);
            top.transform.localPosition = table._shoe + new Vector3(0f, 0.071f, 0f);
            top.transform.localRotation = Quaternion.Euler(0f, -12f, 180f);
            top.AddComponent<MeshFilter>().sharedMesh = CasinoFaces.CardMesh(-1);
            top.AddComponent<MeshRenderer>().sharedMaterial = CasinoFaces.Cards;
            table._dealerText = table.Label("Dealer total", table._dealerLabel, 0.05f);
            Instance = table;
            return table;
        }

        TextMesh Label(string name, Vector3 at, float height)
        {
            // Tipped back to face a player standing at the table.
            var pivot = new GameObject(name);
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = at;
            pivot.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            var text = Greybox.Label(name + " text", pivot.transform, Vector3.zero, "", Plain, height);
            text.gameObject.SetActive(false);
            return text;
        }

        /// <summary>A new state from the host; the same state twice changes nothing.</summary>
        public void Show(BlackjackTableState next)
        {
            if (next == null) return;
            float now = Time.unscaledTime;
            bool newDeal = next.deal != _state.deal || next.owner != _state.owner;
            // Last hand's cards go to the discard pile before the new ones come out.
            float at = newDeal && Sweep(now) ? now + 0.45f : now;

            // Where every card in the new state lies.
            int hands = next.counts.Length;
            var claimed = new HashSet<Card>();
            var playerSlots = new List<(int hand, int index, int value)>();
            for (int h = 0; h < hands; h++)
            {
                var cards = next.Hand(h);
                for (int i = 0; i < cards.Count; i++) playerSlots.Add((h, i, cards[i]));
            }

            if (newDeal)
            {
                // Player, dealer, player, dealer (hole card face down), then anything the hand already settled on.
                var order = new List<(int hand, int index, int value)>();
                for (int round = 0; round < 2; round++)
                {
                    foreach (var slot in playerSlots) if (slot.hand == 0 && slot.index == round) order.Add(slot);
                    if (round < next.dealer.Length) order.Add((-1, round, next.dealer[round]));
                }
                foreach (var slot in order)
                {
                    bool hole = slot.hand < 0 && slot.index == 1;
                    // A hand that settles on the deal turns its hole card over after it lands.
                    var card = Deal(slot.hand, slot.index, hole ? -1 : slot.value, at, next);
                    at += DealGap;
                    if (hole && slot.value >= 0) { at += 0.25f; Flip(card, slot.value, at); at += DealGap; }
                }
                for (int i = 2; i < next.dealer.Length; i++) { Deal(-1, i, next.dealer[i], at, next); at += DealGap; }
                CasinoAudio.TableSound(CasinoAudio.TableCue.Chips, WorldOf(_playerRow));
            }
            else
            {
                // Cards already out keep their place or slide to a new one (a split); new ones come from the shoe.
                var dealtNew = new List<(int hand, int index, int value)>();
                foreach (var slot in playerSlots)
                {
                    Card match = null;
                    foreach (var c in _cards)
                        if (!c.Leaving && c.Hand >= 0 && !claimed.Contains(c) && c.Value == slot.value && (match == null || c.Hand == slot.hand && match.Hand != slot.hand))
                            match = c;
                    if (match == null) { dealtNew.Add(slot); continue; }
                    claimed.Add(match);
                    var target = PlayerSpot(slot.hand, slot.index, hands);
                    if (match.Hand != slot.hand || match.Index != slot.index) Slide(match, target, at);
                    match.Hand = slot.hand; match.Index = slot.index;
                }
                foreach (var slot in dealtNew) { Deal(slot.hand, slot.index, slot.value, at, next); at += DealGap; }
                // The hole card turns over, then the dealer draws.
                for (int i = 0; i < next.dealer.Length; i++)
                {
                    var existing = _cards.Find(c => !c.Leaving && c.Hand < 0 && c.Index == i);
                    if (existing == null) { Deal(-1, i, next.dealer[i], at, next); at += DealGap * 1.2f; }
                    else if (existing.Value < 0 && next.dealer[i] >= 0) { at += 0.2f; Flip(existing, next.dealer[i], at); at += DealGap * 1.2f; }
                }
            }
            _state = next;
            _settledAt = Mathf.Max(_settledAt, at);
            foreach (var c in _cards) _settledAt = Mathf.Max(_settledAt, c.End);
            _sweepAt = next.playing ? -1f : _settledAt + SweepAfter;
        }

        Vector3 WorldOf(Vector3 local) => transform.TransformPoint(local);

        /// <summary>The spot for card <paramref name="index"/> of hand <paramref name="hand"/> when <paramref name="hands"/> hands are out.</summary>
        Vector3 PlayerSpot(int hand, int index, int hands)
        {
            // Hands side by side from the player's left (+x); each fans out to the player's right as it takes cards.
            float spacing = hands >= 4 ? 0.2f : 0.24f;
            float centre = ((hands - 1) / 2f - hand) * spacing;
            return _playerRow + new Vector3(centre + 0.035f - index * 0.03f, index * 0.0012f, 0f);
        }

        Vector3 DealerSpot(int index) => _dealerRow + new Vector3(-index * 0.062f, index * 0.0012f, 0f);

        static Quaternion Settle(int seed, bool faceUp) =>
            Quaternion.Euler(0f, (seed * 37 % 7 - 3) * 0.8f, faceUp ? 0f : 180f);

        Card Deal(int hand, int index, int value, float start, BlackjackTableState state)
        {
            var go = new GameObject(hand < 0 ? "Dealer card" : "Player card");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = CasinoFaces.CardMesh(value);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = CasinoFaces.Cards;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            var to = hand < 0 ? DealerSpot(index) : PlayerSpot(hand, index, Mathf.Max(1, state.counts.Length));
            var card = new Card
            {
                Go = go, Value = value, Hand = hand, Index = index,
                From = _shoe + new Vector3(0f, 0.07f, 0f), To = to,
                FromRot = Quaternion.Euler(0f, -12f, 180f), ToRot = Settle(_cards.Count + index, value >= 0),
                Start = start, End = start + FlightTime, Arc = 0.05f,
            };
            _cards.Add(card);
            return card;
        }

        void Slide(Card card, Vector3 to, float start)
        {
            card.From = card.Go.transform.localPosition; card.FromRot = card.Go.transform.localRotation;
            card.To = to; card.Start = start; card.End = start + 0.25f; card.Arc = 0.01f;
        }

        void Flip(Card card, int value, float start)
        {
            card.From = card.To; card.FromRot = card.ToRot;
            card.ToRot = Settle(card.Index + 11, true);
            card.Start = Mathf.Max(start, card.End); card.End = card.Start + FlipTime; card.Arc = 0.035f;
            // The face is on the card's underside until it turns over, so it can go on now.
            card.Value = value;
            card.Go.GetComponent<MeshFilter>().sharedMesh = CasinoFaces.CardMesh(value);
        }

        bool Sweep(float now)
        {
            int k = 0;
            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                // Cards still waiting in the shoe never come out.
                if (_cards[i].Go.activeSelf) continue;
                Destroy(_cards[i].Go);
                _cards.RemoveAt(i);
            }
            foreach (var c in _cards)
            {
                if (c.Leaving) continue;
                c.Leaving = true;
                c.From = c.Go.transform.localPosition;
                c.FromRot = c.Go.transform.localRotation;
                c.To = _discard + Vector3.up * (0.002f * k);
                c.ToRot = Quaternion.Euler(0f, 8f, 180f);
                c.Start = now + 0.03f * k++; c.End = c.Start + 0.35f; c.Arc = 0.04f;
            }
            if (k > 0) CasinoAudio.TableSound(CasinoAudio.TableCue.Sweep, WorldOf(_discard));
            _settledAt = now;
            return k > 0;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (_sweepAt > 0f && now >= _sweepAt)
            {
                _sweepAt = -1f;
                Sweep(now);
                _state = new BlackjackTableState { owner = _state.owner, deal = _state.deal };
            }
            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                var c = _cards[i];
                if (now < c.Start) continue;
                if (!c.Go.activeSelf)
                {
                    c.Go.SetActive(true);
                    CasinoAudio.TableSound(CasinoAudio.TableCue.Deal, WorldOf(c.From));
                }
                float u = c.End > c.Start ? Mathf.Clamp01((now - c.Start) / (c.End - c.Start)) : 1f;
                float e = u * u * (3f - 2f * u);
                var pos = Vector3.Lerp(c.From, c.To, e) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * c.Arc);
                var rot = Quaternion.Slerp(c.FromRot, c.ToRot, e);
                c.Go.transform.localPosition = pos;
                c.Go.transform.localRotation = rot;
                if (u >= 1f && c.End > c.Start)
                {
                    c.Start = c.End = now;
                    if (c.Leaving) { Destroy(c.Go); _cards.RemoveAt(i); continue; }
                    CasinoAudio.TableSound(CasinoAudio.TableCue.Place, WorldOf(c.To));
                }
            }
            UpdateLabels(now);
        }

        /// <summary>Totals of the cards that have landed face up, and each hand's result once everything is on the table.</summary>
        void UpdateLabels(float now)
        {
            var s = _state;
            bool settled = now >= _settledAt;
            var dealerShown = new List<int>();
            foreach (var c in _cards) if (!c.Leaving && c.Hand < 0 && c.Landed(now) && c.FaceUp) dealerShown.Add(c.Value);
            SetLabel(_dealerText, dealerShown.Count == 0 ? "" : DealerText(dealerShown, settled && !s.playing), Plain);

            int hands = s.counts.Length;
            while (_handLabels.Count < hands) _handLabels.Add(Label("Hand total", _handLabelRow, 0.042f));
            for (int h = 0; h < _handLabels.Count; h++)
            {
                if (h >= hands) { SetLabel(_handLabels[h], "", Plain); continue; }
                var spot = PlayerSpot(h, 0, hands);
                var pivot = _handLabels[h].transform.parent;
                var want = new Vector3(spot.x - 0.035f, _handLabelRow.y, _handLabelRow.z);
                pivot.localPosition = Vector3.MoveTowards(pivot.localPosition, want, Time.unscaledDeltaTime * 0.6f);
                var shown = new List<int>();
                foreach (var c in _cards) if (!c.Leaving && c.Hand == h && c.Landed(now) && c.FaceUp) shown.Add(c.Value);
                if (shown.Count == 0) { SetLabel(_handLabels[h], "", Plain); continue; }
                int total = CasinoRules.Total(shown);
                string text = total > 21 ? "BUST" : total.ToString();
                var color = s.playing && h == s.active && hands > 1 ? Gold : Plain;
                if (settled && !s.playing && h < s.bets.Length)
                {
                    bool split = (s.splitMask & (1 << h)) != 0;
                    long back = CasinoRules.HandReturn(s.Hand(h), split, s.bets[h], s.dealer);
                    long net = back - s.bets[h];
                    bool natural = !split && shown.Count == 2 && total == 21;
                    text = net > 0 ? (natural ? "BLACKJACK  +" : total + "  WIN  +") + net.ToString("N0")
                        : net == 0 ? total + "  PUSH" : (total > 21 ? "BUST  -" : total + "  -") + s.bets[h].ToString("N0");
                    color = net > 0 ? Win : net == 0 ? Plain : Lose;
                }
                SetLabel(_handLabels[h], text, color);
            }
        }

        static string DealerText(List<int> shown, bool final)
        {
            int total = CasinoRules.Total(shown);
            if (final && shown.Count == 2 && total == 21) return "DEALER BLACKJACK";
            return total > 21 ? "DEALER BUSTS" : "DEALER " + total;
        }

        static void SetLabel(TextMesh label, string text, Color color)
        {
            bool on = !string.IsNullOrEmpty(text);
            if (label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
            if (label.text != text) label.text = text;
            if (label.color != color) label.color = color;
        }

        /// <summary>The totals a player at the table can see right now, for the HUD: "You 15  ·  Dealer 10".</summary>
        public string Summary()
        {
            float now = Time.unscaledTime;
            var dealer = new List<int>();
            var parts = new List<string>();
            int hands = _state.counts.Length;
            for (int h = 0; h < hands; h++)
            {
                var shown = new List<int>();
                foreach (var c in _cards) if (!c.Leaving && c.Hand == h && c.Landed(now) && c.FaceUp) shown.Add(c.Value);
                if (shown.Count > 0) parts.Add((hands > 1 ? "Hand " + (h + 1) + " " : "You ") + CasinoRules.Total(shown));
            }
            foreach (var c in _cards) if (!c.Leaving && c.Hand < 0 && c.Landed(now) && c.FaceUp) dealer.Add(c.Value);
            if (dealer.Count > 0) parts.Add("Dealer " + CasinoRules.Total(dealer));
            return string.Join("   ·   ", parts);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
