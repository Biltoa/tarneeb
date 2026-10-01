using System.Collections;
using System.Text;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
using Tarneeb.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Round summary popup. Children: Panel/Title, Panel/Body, Panel/Continue.</summary>
    public sealed class RoundSummaryView : MonoBehaviour
    {
        private CanvasGroup _group;
        private TMP_Text _title, _body;
        private Button _continue;
        private bool _clicked, _bound;
        private Image _suitIcon;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _group = GetComponent<CanvasGroup>();
            _title = UiBind.Get<TMP_Text>(transform, "Panel/Title");
            _body = UiBind.Get<TMP_Text>(transform, "Panel/Body");
            _continue = UiBind.Get<Button>(transform, "Panel/Continue");
            _continue.onClick.AddListener(() => _clicked = true);
            _group.alpha = 0f;
            gameObject.SetActive(false);
        }

        public IEnumerator Show(RoundSummary s, int localTeam, string declarerName, bool lastRound, string continueLabel, float autoDismissSeconds = 0f)
        {
            Bind();
            int mine = s.TeamScoresAfter[localTeam], theirs = s.TeamScoresAfter[1 - localTeam];
            bool weDeclared = s.DeclaringTeam == localTeam;
            int myDelta = weDeclared ? s.DeclaringDelta : s.DefendingDelta;
            int theirDelta = weDeclared ? s.DefendingDelta : s.DeclaringDelta;
            bool good = s.ContractMade == weDeclared;

            Loc.Set(_title, s.Kaboot ? "KABOOT!" : (good ? "Round won" : "Round lost"));
            // bold runs are English-only: rich-text tags would be reordered with the Arabic text
            string B(object v) => Loc.IsRtl ? v.ToString() : "<b>" + v + "</b>";
            var sb = new StringBuilder();
            sb.Append(Loc.Display(Loc.Fmt("{0} bid {1}, tarneeb {2}", Loc.T(declarerName), B(s.Bid), B(Loc.T(s.Trump.ToString()))))).Append("\n");
            sb.Append(Loc.Display(Loc.Fmt("Declarers took {0} tricks, defenders {1}", B(s.DeclaringTricks), B(s.DefendingTricks)))).Append("\n\n");
            sb.Append(Loc.Display(Loc.T("Us") + "  " + Signed(myDelta) + "      " + Loc.T("Them") + "  " + Signed(theirDelta))).Append("\n");
            sb.Append("<size=72><b>").Append(mine).Append("  :  ").Append(theirs).Append("</b></size>");
            _body.text = sb.ToString();
            _title.color = good ? UiArt.Good : UiArt.Bad;
            Transform badge = transform.Find("Panel/Badge");
            if (badge != null)
            {
                var bi = badge.GetComponent<Image>();
                bi.sprite = UiArt.Get(good ? "icon_check" : "icon_close");
                bi.color = good ? UiArt.Good : UiArt.Bad;
            }
            Loc.Set(_continue.GetComponentInChildren<TMP_Text>(), continueLabel);

            _clicked = false;
            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            PlaceSuitIcon(s.Trump);                                              // needs the text laid out, so only once the popup is active
            Transform panel = transform.Find("Panel");
            if (panel != null) panel.localScale = Vector3.one * 0.8f;
            if (panel != null) StartCoroutine(Tween.ScaleTo(panel, 0.8f, 1f, 0.35f, Ease.OutBack));
            AudioManager.Play(Sfx.Whoosh, 0.5f);
            yield return Tween.Fade(_group, 1f, 0.2f);
            for (float t = 0f; !_clicked && (autoDismissSeconds <= 0f || t < autoDismissSeconds); t += Time.deltaTime) yield return null;
            yield return Tween.Fade(_group, 0f, 0.15f);
            gameObject.SetActive(false);
        }

        /// <summary>Puts the tarneeb suit icon at the end of the first body line ("... tarneeb Clubs [icon]"), mirrored for Arabic.</summary>
        private void PlaceSuitIcon(Suit suit)
        {
            if (_suitIcon == null)
            {
                var go = new GameObject("TrumpIcon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_body.transform, false);
                _suitIcon = go.GetComponent<Image>();
                _suitIcon.raycastTarget = false;
                _suitIcon.preserveAspect = true;
            }
            _suitIcon.sprite = UiArt.SuitIcon(suit, false);
            _body.ForceMeshUpdate();
            if (_body.textInfo.lineCount == 0) { _suitIcon.gameObject.SetActive(false); return; }
            float size = _body.fontSize * 1.1f;
            Extents e = _body.textInfo.lineInfo[0].lineExtents;
            float lineY = (_body.textInfo.lineInfo[0].ascender + _body.textInfo.lineInfo[0].descender) * 0.5f;
            var rt = (RectTransform)_suitIcon.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            float x = Loc.IsRtl ? e.min.x - size * 0.7f : e.max.x + size * 0.7f;
            rt.localPosition = new Vector3(x, lineY, 0f);
            _suitIcon.gameObject.SetActive(true);
        }

        private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();
    }
}
