using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Config;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using Tarneeb.Game;
using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// Renders engine events (one at a time, in order) and exposes the input widgets. Never mutates game state.
    /// Expected hierarchy under this object: Hand, HUD, BidPanel, TrumpPicker, RoundSummary, GameOver, CardLayer,
    /// Table/DeckAnchor, Table/TrickArea/Slot_{Bottom,Right,Top,Left}, Seats/Seat_{Bottom,Right,Top,Left}.
    /// </summary>
    public sealed class TableView : MonoBehaviour
    {
        private static readonly Color TeamUs = new Color(0.30f, 0.86f, 0.92f);
        private static readonly Color TeamThem = new Color(0.98f, 0.62f, 0.30f);

        private const float TrickCardScale = 0.92f;

        [SerializeField] private CardView cardPrefab;

        private HandView _hand;
        private HudView _hud;
        private BidPanel _bidPanel;
        private TrumpPicker _trumpPicker;
        private RoundSummaryView _roundSummary;
        private GameOverView _gameOver;
        private RectTransform _cardLayer, _deckAnchor;
        private GameObject _pile;
        private readonly SeatView[] _seats = new SeatView[4];
        private readonly Transform[] _slots = new Transform[4];
        private readonly List<CardView>[] _trickCards = { new List<CardView>(), new List<CardView>(), new List<CardView>(), new List<CardView>() };
        private readonly int[] _counts = new int[4];
        private readonly int[] _tricksBySeat = new int[4];
        private readonly int[] _tricksByTeam = new int[2];

        private TarneebEngine _engine;
        private SeatMapper _map;
        private string[] _names;
        private bool _bound;

        public HandView Hand { get { Bind(); return _hand; } }
        public string NameOf(int seat) => _names[seat];
        public int LocalTeam => TarneebRules.TeamOf(_map.LocalSeat);

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _hand = UiBind.Get<HandView>(transform, "Hand");
            _hud = UiBind.Get<HudView>(transform, "HUD");
            _bidPanel = UiBind.Get<BidPanel>(transform, "BidPanel");
            _trumpPicker = UiBind.Get<TrumpPicker>(transform, "TrumpPicker");
            _roundSummary = UiBind.Get<RoundSummaryView>(transform, "RoundSummary");
            _gameOver = UiBind.Get<GameOverView>(transform, "GameOver");
            _cardLayer = UiBind.Get<RectTransform>(transform, "CardLayer");
            _deckAnchor = UiBind.Get<RectTransform>(transform, "Table/DeckAnchor");
            Transform pile = _deckAnchor.Find("Pile");
            if (pile != null) { _pile = pile.gameObject; _pile.SetActive(false); }
            foreach (ViewPos p in Enum.GetValues(typeof(ViewPos)))
            {
                _seats[(int)p] = UiBind.Get<SeatView>(transform, "Seats/Seat_" + p);
                _slots[(int)p] = transform.Find("Table/TrickArea/Slot_" + p);
            }
            _hand.DropZone = transform.Find("Table/DropZone") as RectTransform;
            if (cardPrefab == null) cardPrefab = Resources.Load<CardView>("Prefabs/CardView");
        }

        // ------------------------------------------------------------ setup

        public void BeginGame(TarneebEngine engine, SeatMapper map, string[] names)
        {
            Bind();
            _engine = engine;
            _map = map;
            _names = names;
            ClearCards();
            _bidPanel.Hide();
            _trumpPicker.Hide();
            _gameOver.Hide();
            for (int s = 0; s < 4; s++)
            {
                int team = TarneebRules.TeamOf(s);
                SeatFor(s).Setup(names[s], team == LocalTeam ? TeamUs : TeamThem);
                SeatFor(s).SetTurn(false);
                SeatFor(s).SetDealer(false);
                SeatFor(s).HideBubble();
                SeatFor(s).SetHandCount(0);
                SeatFor(s).SetTricks(0);
            }
            _hud.SetScores(0, 0);
            _hud.SetTricks(0, 0);
            _hud.SetTrump(null);
            _hud.SetBid(0, false);
            _hud.SetStatus("");
            if (UiFx.Instance != null) UiFx.Instance.SetShakeTarget((RectTransform)transform);
            AudioManager.PlayMusic(MusicTrack.Table);
        }

        /// <summary>Shows a chat line or emoji beside the given engine seat (ignored before a game has begun).</summary>
        public void ShowChat(int seat, ChatMessage m)
        {
            Bind();
            if (_map != null) SeatFor(seat).ShowChat(m);
        }

        private SeatView SeatFor(int seat) => _seats[(int)_map.ToView(seat)];
        private Transform SlotFor(int seat) => _slots[(int)_map.ToView(seat)];

        private void ClearCards()
        {
            _hand.Clear();
            for (int i = 0; i < 4; i++)
            {
                foreach (CardView cv in _trickCards[i]) if (cv != null) Destroy(cv.gameObject);
                _trickCards[i].Clear();
                _counts[i] = 0;
                _tricksBySeat[i] = 0;
            }
            for (int i = _cardLayer.childCount - 1; i >= 0; i--) Destroy(_cardLayer.GetChild(i).gameObject);
        }

        private CardView Spawn(Card card, bool faceUp, Vector3 worldPos, float scale)
        {
            CardView cv = Instantiate(cardPrefab, _cardLayer);
            cv.transform.position = worldPos;
            cv.transform.localScale = Vector3.one * scale;
            cv.Show(card, faceUp);
            cv.SetInteractable(false);
            cv.SetDimmed(false);
            return cv;
        }

        // ------------------------------------------------------------ event presentation

        public IEnumerator Present(GameEvent e)
        {
            switch (e)
            {
                case CardsDealt d: return OnDealt(d);
                case TurnChanged t: OnTurn(t); return null;
                case BidPlaced b: return OnBid(b);
                case PlayerPassed p: return OnPass(p);
                case BiddingWon w: return OnBiddingWon(w);
                case TrumpChosen tc: return OnTrump(tc);
                case CardPlayed cp: return OnCardPlayed(cp);
                case TrickWon tw: return OnTrickWon(tw);
                case RoundRedealt rr: return OnRedeal();
                case RoundEnded re:
                    _hud.SetScores(re.Summary.TeamScoresAfter[LocalTeam], re.Summary.TeamScoresAfter[1 - LocalTeam], true);
                    return null;
                default: return null;
            }
        }

        private IEnumerator OnDealt(CardsDealt d)
        {
            ClearCards();
            for (int s = 0; s < 4; s++)
            {
                SeatFor(s).SetDealer(s == d.Dealer);
                SeatFor(s).SetHandCount(0);
                SeatFor(s).SetTricks(0);
                SeatFor(s).HideBubble();
            }
            _tricksByTeam[0] = _tricksByTeam[1] = 0;
            _hud.SetTricks(0, 0);
            _hud.SetTrump(null);
            _hud.SetBid(0, false);
            _hud.SetStatus("");

            IReadOnlyList<Card> mine = _engine.State.Hands[_map.LocalSeat].Cards;
            int total = mine.Count;
            Vector3 deck = _deckAnchor.position;
            if (_pile != null) { _pile.SetActive(true); StartCoroutine(Tween.Pop(_pile.transform, 0.3f, 0.5f)); }
            AudioManager.Play(Sfx.CardSlide, 0.8f);
            yield return Tween.Wait(0.25f);
            int seat = TarneebRules.NextSeat(d.Dealer, _engine.Config);
            int localIndex = 0;
            for (int round = 0; round < 13; round++)
            {
                for (int k = 0; k < 4; k++)
                {
                    int target = seat;
                    seat = TarneebRules.NextSeat(seat, _engine.Config);
                    if (target == _map.LocalSeat)
                    {
                        int idx = localIndex++;
                        CardView cv = Spawn(mine[idx], false, deck, 0.55f);
                        cv.transform.SetParent(_hand.transform, true);
                        _hand.Add(cv);
                        cv.transform.SetSiblingIndex(idx);
                        Vector3 world = _hand.SlotWorld(idx, total);
                        float rot = _hand.SlotRotation(idx, total);
                        StartCoroutine(FlyToHand(cv, world, rot));
                    }
                    else
                    {
                        CardView back = Spawn(default, false, deck, 0.5f);
                        StartCoroutine(DealBackCard(back, target));
                    }
                    if (k == 0 && round % 2 == 0) AudioManager.PlayVaried(Sfx.CardDeal, 0.55f, 0.12f);
                    yield return Tween.Wait(0.018f);
                }
            }
            yield return Tween.Wait(0.4f);
            if (_pile != null) _pile.SetActive(false);
            _hand.ArrangeImmediate();
            // flip the local hand face-up, left to right
            for (int i = 0; i < _hand.Count; i++)
            {
                StartCoroutine(_hand.Cards[i].Flip(true, 0.2f));
                if (i % 3 == 0) AudioManager.PlayVaried(Sfx.CardFlip, 0.45f, 0.1f);
                yield return Tween.Wait(0.03f);
            }
            yield return Tween.Wait(0.25f);
        }

        private IEnumerator FlyToHand(CardView cv, Vector3 world, float rot)
        {
            yield return Tween.Fly(cv.transform, world, rot, 1f, 0.34f, Ease.OutCubic);
        }

        private IEnumerator DealBackCard(CardView back, int seat)
        {
            yield return Tween.Fly(back.transform, SeatFor(seat).HandAnchor.position, 0f, 0.38f, 0.3f);
            if (back != null) Destroy(back.gameObject);
            _counts[seat]++;
            SeatFor(seat).SetHandCount(_counts[seat]);
        }

        private void OnTurn(TurnChanged t)
        {
            bool mine = t.Seat == _map.LocalSeat;
            for (int s = 0; s < 4; s++)
            {
                float timer = s == t.Seat && mine ? GameSettings.TurnTimerSeconds : 0f;
                SeatFor(s).SetTurn(s == t.Seat, timer);
            }
            if (mine)
            {
                AudioManager.Play(Sfx.TurnPing, 0.8f);
                UiFx.Instance?.Ring(SeatFor(t.Seat).Ring.position, UiArt.Gold, 80f, 200f, 0.7f);
            }
            switch (t.Phase)
            {
                case GamePhase.Bidding: if (mine) _hud.SetStatus("Your bid"); else _hud.SetStatus("{0} is bidding...", _names[t.Seat]); break;
                case GamePhase.ChoosingTrump: if (mine) _hud.SetStatus("Choose the tarneeb suit"); else _hud.SetStatus("{0} is choosing tarneeb...", _names[t.Seat]); break;
                case GamePhase.Playing: if (mine) _hud.SetStatus("Your turn"); else _hud.SetStatus("{0} is thinking...", _names[t.Seat]); break;
            }
        }

        private IEnumerator OnBid(BidPlaced b)
        {
            SeatFor(b.Seat).ShowBubble("BID {0}", b.Amount);
            _hud.SetBid(b.Amount);
            AudioManager.Play(Sfx.Bid, 0.8f, 0.9f + (b.Amount - 7) * 0.04f);
            UiFx.Instance?.Sparkles(SeatFor(b.Seat).Ring.position, 4, 60f, UiArt.Gold, 0.6f, 24f);
            yield return Tween.Wait(0.45f);
        }

        private IEnumerator OnPass(PlayerPassed p)
        {
            SeatFor(p.Seat).ShowBubble("PASS");
            AudioManager.Play(Sfx.Pass, 0.7f);
            yield return Tween.Wait(0.3f);
        }

        private IEnumerator OnBiddingWon(BiddingWon w)
        {
            _hud.SetBid(w.Bid);
            _hud.Toast("{0} BIDS {1}", 1.3f, _names[w.Declarer], w.Bid);
            AudioManager.Play(Sfx.Sparkle, 0.7f);
            UiFx.Instance?.Burst(SeatFor(w.Declarer).Ring.position, 14, UiArt.Gold, 300f, 0.7f, 26f);
            yield return Tween.Wait(0.6f);
            for (int s = 0; s < 4; s++) SeatFor(s).HideBubble();
        }

        private IEnumerator OnTrump(TrumpChosen tc)
        {
            _hud.SetTrump(tc.Suit);
            _hud.Toast("TARNEEB  {0}", 1.2f, tc.Suit.ToString().ToUpperInvariant());
            AudioManager.Play(Sfx.Sparkle, 0.8f);
            yield return Tween.Wait(0.8f);
        }

        private IEnumerator OnRedeal()
        {
            _hud.Toast("Everyone passed - redealing", 1.2f);
            yield return Tween.Wait(0.9f);
        }

        private IEnumerator OnCardPlayed(CardPlayed cp)
        {
            int vp = (int)_map.ToView(cp.Seat);
            Transform slot = _slots[vp];
            CardView cv;
            bool local = cp.Seat == _map.LocalSeat;
            if (local)
            {
                cv = _hand.Remove(cp.Card);
                if (cv == null) cv = Spawn(cp.Card, true, _hand.transform.position, 1f);
                cv.SetInteractable(false);
                cv.SetDimmed(false);
                cv.transform.SetParent(_cardLayer, true);
                StartCoroutine(_hand.Arrange(0.22f));
            }
            else
            {
                _counts[cp.Seat] = Mathf.Max(0, _counts[cp.Seat] - 1);
                SeatFor(cp.Seat).SetHandCount(_counts[cp.Seat]);
                cv = Spawn(cp.Card, false, SeatFor(cp.Seat).HandAnchor.position, 0.42f);
                StartCoroutine(cv.Flip(true, 0.24f));
            }
            _trickCards[vp].Add(cv);
            AudioManager.PlayVaried(Sfx.CardSlide, 0.5f);
            float rot = UnityEngine.Random.Range(-6f, 6f);
            yield return Tween.Fly(cv.transform, slot.position, rot, TrickCardScale, 0.32f, Ease.OutBack);
            if (cv != null)
            {
                cv.transform.SetParent(slot, true);
                AudioManager.PlayVaried(Sfx.CardPlace, 0.9f, 0.08f);
                UiFx.Instance?.Ring(slot.position, UiArt.GoldLight, 50f, 190f, 0.4f);
                UiFx.Instance?.Burst(slot.position, 7, UiArt.Cream, 150f, 0.4f, 16f, -60f);
                StartCoroutine(Tween.Punch(cv.transform, 0.05f, 0.2f));
            }
        }

        private IEnumerator OnTrickWon(TrickWon tw)
        {
            yield return Tween.Wait(0.35f);
            int winnerView = (int)_map.ToView(tw.Winner);
            foreach (CardView cv in _trickCards[winnerView]) if (cv != null) cv.SetHighlight(true);
            AudioManager.Play(Sfx.Sparkle, 0.6f);
            UiFx.Instance?.Sparkles(_slots[winnerView].position, 8, 70f, UiArt.GoldLight, 0.9f);
            yield return Tween.Wait(0.5f);
            Vector3 target = SeatFor(tw.Winner).Ring.position;
            var flights = new List<IEnumerator>();
            for (int i = 0; i < 4; i++)
            {
                foreach (CardView cv in _trickCards[i])
                {
                    if (cv == null) continue;
                    cv.SetHighlight(false);
                    cv.transform.SetParent(_cardLayer, true);
                    flights.Add(FlyAndDestroy(cv, target));
                }
                _trickCards[i].Clear();
            }
            AudioManager.Play(Sfx.Whoosh, 0.6f);
            yield return Tween.All(this, flights);
            _tricksBySeat[tw.Winner]++;
            _tricksByTeam[TarneebRules.TeamOf(tw.Winner)]++;
            SeatFor(tw.Winner).SetTricks(_tricksBySeat[tw.Winner], true);
            _hud.SetTricks(_tricksByTeam[LocalTeam], _tricksByTeam[1 - LocalTeam], true);
            AudioManager.Play(Sfx.TrickWin, 0.85f);
            UiFx.Instance?.FloatText(target + new Vector3(0f, 30f, 0f), "+1", UiArt.Gold, 56f, 90f, 0.9f);
            UiFx.Instance?.Burst(target, 12, UiArt.Gold, 260f, 0.6f, 24f);
            UiFx.Instance?.Shake(4f, 0.18f);
        }

        private static IEnumerator FlyAndDestroy(CardView cv, Vector3 target)
        {
            yield return Tween.Fly(cv.transform, target, 0f, 0.25f, 0.34f, Ease.InOutCubic);
            if (cv != null) Destroy(cv.gameObject);
        }

        // ------------------------------------------------------------ input widgets

        public void ShowBidPanel(int minimumBid, Action<int> onBid, Action onPass) => _bidPanel.Show(minimumBid, onBid, onPass);
        public void ShowTrumpPicker(Action<Suit> onPick) => _trumpPicker.Show(onPick);
        public void BeginPlayInput(ICollection<Card> legal, Action<Card> onPlay) => _hand.BeginInput(legal, onPlay);

        public void Notify(string message) => _hud.Toast(message, 1.6f);                // an English key (translated when known) or plain text

        public void CancelInput()
        {
            _bidPanel.Hide();
            _trumpPicker.Hide();
            _hand.EndInput();
        }

        // ------------------------------------------------------------ popups

        public IEnumerator ShowRoundSummary(RoundSummary s, bool gameOver, float autoDismissSeconds = 0f)
        {
            bool weDeclared = s.DeclaringTeam == LocalTeam;
            bool good = s.ContractMade == weDeclared;
            for (int i = 0; i < 4; i++) SeatFor(i).SetTurn(false);
            AudioManager.Play(good ? Sfx.RoundWin : Sfx.RoundLose);
            AudioManager.Duck(1.6f);
            _hud.SetStatus("");
            if (good) { UiFx.Instance?.Confetti(s.Kaboot ? 110 : 45); UiFx.Instance?.Sparkles(_deckAnchor.position, 14, 200f, UiArt.GoldLight, 1.2f, 40f); }
            else UiFx.Instance?.Shake(9f, 0.35f);
            yield return Tween.Wait(0.5f);
            yield return _roundSummary.Show(s, LocalTeam, _names[s.Declarer], gameOver, gameOver ? "See result" : "Next round", autoDismissSeconds);
        }

        public IEnumerator ShowGameOver(int winningTeam, int[] scores, Action<GameOverView.Choice> result, float autoDismissSeconds = 0f)
        {
            bool won = winningTeam == LocalTeam;
            AudioManager.Play(won ? Sfx.GameWin : Sfx.GameLose);
            AudioManager.Duck(3.4f, 0.25f);
            if (won) StartCoroutine(FireworkShow());
            yield return _gameOver.Show(won, scores[LocalTeam], scores[1 - LocalTeam], result, autoDismissSeconds);
        }

        private IEnumerator FireworkShow()
        {
            for (int i = 0; i < 6; i++)
            {
                UiFx.Instance?.Confetti(40);
                Vector3 p = _deckAnchor.position + new Vector3(UnityEngine.Random.Range(-260f, 260f), UnityEngine.Random.Range(-100f, 420f), 0f) * transform.lossyScale.x;
                UiFx.Instance?.Burst(p, 26, i % 2 == 0 ? UiArt.Gold : UiArt.Teal, 380f, 0.9f, 30f, -220f);
                UiFx.Instance?.Sparkles(p, 10, 60f, UiArt.GoldLight, 1.1f, 40f);
                AudioManager.Play(Sfx.Sparkle, 0.6f);
                yield return new WaitForSeconds(0.55f);
            }
        }
    }
}
