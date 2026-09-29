using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
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
        private static readonly Color TeamUs = new Color(0.30f, 0.80f, 0.88f);
        private static readonly Color TeamThem = new Color(0.98f, 0.60f, 0.28f);

        private const float TrickCardScale = 0.8f;

        [SerializeField] private CardView cardPrefab;

        private HandView _hand;
        private HudView _hud;
        private BidPanel _bidPanel;
        private TrumpPicker _trumpPicker;
        private RoundSummaryView _roundSummary;
        private GameOverView _gameOver;
        private RectTransform _cardLayer, _deckAnchor;
        private readonly SeatView[] _seats = new SeatView[4];
        private readonly Transform[] _slots = new Transform[4];
        private readonly List<CardView>[] _trickCards = { new List<CardView>(), new List<CardView>(), new List<CardView>(), new List<CardView>() };
        private readonly int[] _counts = new int[4];
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
            foreach (ViewPos p in Enum.GetValues(typeof(ViewPos)))
            {
                _seats[(int)p] = UiBind.Get<SeatView>(transform, "Seats/Seat_" + p);
                _slots[(int)p] = transform.Find("Table/TrickArea/Slot_" + p);
            }
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
                SeatFor(s).SetCardCount(0);
            }
            _hud.SetScores(0, 0);
            _hud.SetTricks(0, 0);
            _hud.SetTrump(null);
            _hud.SetContract("Dealing...");
            _hud.SetStatus("");
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
                    _hud.SetScores(re.Summary.TeamScoresAfter[LocalTeam], re.Summary.TeamScoresAfter[1 - LocalTeam]);
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
                SeatFor(s).SetCardCount(0);
                SeatFor(s).HideBubble();
                _counts[s] = 0;
            }
            _tricksByTeam[0] = _tricksByTeam[1] = 0;
            _hud.SetTricks(0, 0);
            _hud.SetTrump(null);
            _hud.SetContract("Bidding");

            IReadOnlyList<Card> mine = _engine.State.Hands[_map.LocalSeat].Cards;
            int total = mine.Count;
            Vector3 deck = _deckAnchor.position;
            var flights = new List<IEnumerator>();
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
                        CardView cv = Spawn(mine[idx], true, deck, 0.6f);
                        cv.transform.SetParent(_hand.transform, true);
                        _hand.Add(cv);
                        cv.transform.SetSiblingIndex(idx);
                        Vector3 world = _hand.SlotWorld(idx, total);
                        float rot = _hand.SlotRotation(idx, total);
                        StartCoroutine(Tween.Fly(cv.transform, world, rot, 1f, 0.32f));
                    }
                    else
                    {
                        CardView back = Spawn(default, false, deck, 0.5f);
                        int s = target;
                        StartCoroutine(DealBackCard(back, s));
                    }
                    yield return Tween.Wait(0.017f);
                }
                if (round % 4 == 0) AudioManager.Play(Sfx.CardFlip);
            }
            yield return Tween.Wait(0.35f);
            _hand.ArrangeImmediate();
        }

        private IEnumerator DealBackCard(CardView back, int seat)
        {
            yield return Tween.Fly(back.transform, SeatFor(seat).HandAnchor.position, 0f, 0.35f, 0.3f);
            if (back != null) Destroy(back.gameObject);
            _counts[seat]++;
            SeatFor(seat).SetCardCount(_counts[seat]);
        }

        private void OnTurn(TurnChanged t)
        {
            for (int s = 0; s < 4; s++) SeatFor(s).SetTurn(s == t.Seat);
            bool mine = t.Seat == _map.LocalSeat;
            switch (t.Phase)
            {
                case GamePhase.Bidding: _hud.SetStatus(mine ? "Your bid" : _names[t.Seat] + " is bidding..."); break;
                case GamePhase.ChoosingTrump: _hud.SetStatus(mine ? "Choose the trump suit" : _names[t.Seat] + " is choosing trump..."); break;
                case GamePhase.Playing: _hud.SetStatus(mine ? "Your turn" : _names[t.Seat] + " is thinking..."); break;
            }
        }

        private IEnumerator OnBid(BidPlaced b)
        {
            SeatFor(b.Seat).ShowBubble("Bid " + b.Amount);
            _hud.SetContract("High bid " + b.Amount + " (" + _names[b.Seat] + ")");
            AudioManager.Play(Sfx.Click);
            yield return Tween.Wait(0.35f);
        }

        private IEnumerator OnPass(PlayerPassed p)
        {
            SeatFor(p.Seat).ShowBubble("Pass");
            yield return Tween.Wait(0.25f);
        }

        private IEnumerator OnBiddingWon(BiddingWon w)
        {
            _hud.SetContract(_names[w.Declarer] + " bid " + w.Bid);
            yield return Tween.Wait(0.5f);
            for (int s = 0; s < 4; s++) SeatFor(s).HideBubble();
        }

        private IEnumerator OnTrump(TrumpChosen tc)
        {
            _hud.SetTrump(tc.Suit);
            _hud.Toast("Trump: " + tc.Suit, 1.0f);
            _hud.SetContract(_names[tc.Seat] + " bid " + _engine.State.HighBid);
            yield return Tween.Wait(0.7f);
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
                SeatFor(cp.Seat).SetCardCount(_counts[cp.Seat]);
                cv = Spawn(cp.Card, true, SeatFor(cp.Seat).HandAnchor.position, 0.4f);
            }
            _trickCards[vp].Add(cv);
            AudioManager.Play(Sfx.CardPlace);
            float rot = UnityEngine.Random.Range(-7f, 7f);
            yield return Tween.Fly(cv.transform, slot.position, rot, TrickCardScale, 0.3f, Ease.OutBack);
            if (cv != null) cv.transform.SetParent(slot, true);
        }

        private IEnumerator OnTrickWon(TrickWon tw)
        {
            yield return Tween.Wait(0.5f);
            Vector3 target = SeatFor(tw.Winner).HandAnchor.position;
            var flights = new List<IEnumerator>();
            for (int i = 0; i < 4; i++)
            {
                foreach (CardView cv in _trickCards[i])
                {
                    if (cv == null) continue;
                    cv.transform.SetParent(_cardLayer, true);
                    flights.Add(FlyAndDestroy(cv, target));
                }
                _trickCards[i].Clear();
            }
            AudioManager.Play(Sfx.TrickWin);
            _tricksByTeam[TarneebRules.TeamOf(tw.Winner)]++;
            _hud.SetTricks(_tricksByTeam[LocalTeam], _tricksByTeam[1 - LocalTeam]);
            yield return Tween.All(this, flights);
        }

        private static IEnumerator FlyAndDestroy(CardView cv, Vector3 target)
        {
            yield return Tween.Fly(cv.transform, target, 0f, 0.3f, 0.3f, Ease.InOutCubic);
            if (cv != null) Destroy(cv.gameObject);
        }

        // ------------------------------------------------------------ input widgets

        public void ShowBidPanel(int minimumBid, Action<int> onBid, Action onPass) => _bidPanel.Show(minimumBid, onBid, onPass);
        public void ShowTrumpPicker(Action<Suit> onPick) => _trumpPicker.Show(onPick);
        public void BeginPlayInput(ICollection<Card> legal, Action<Card> onPlay) => _hand.BeginInput(legal, onPlay);

        public void CancelInput()
        {
            _bidPanel.Hide();
            _trumpPicker.Hide();
            _hand.EndInput();
        }

        // ------------------------------------------------------------ popups

        public IEnumerator ShowRoundSummary(RoundSummary s, bool gameOver, float autoDismissSeconds = 0f)
        {
            bool weWon = s.ContractMade == (s.DeclaringTeam == LocalTeam);
            AudioManager.Play(weWon ? Sfx.RoundWin : Sfx.RoundLose);
            _hud.SetStatus("");
            yield return Tween.Wait(0.4f);
            yield return _roundSummary.Show(s, LocalTeam, _names[s.Declarer], gameOver, gameOver ? "See result" : "Next round", autoDismissSeconds);
        }

        public IEnumerator ShowGameOver(int winningTeam, int[] scores, Action<GameOverView.Choice> result, float autoDismissSeconds = 0f)
        {
            bool won = winningTeam == LocalTeam;
            AudioManager.Play(won ? Sfx.RoundWin : Sfx.RoundLose);
            yield return _gameOver.Show(won, scores[LocalTeam], scores[1 - LocalTeam], result, autoDismissSeconds);
        }
    }
}
