using System.Collections;
using System.Text;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
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

            _title.text = s.Kaboot ? "KABOOT!" : (s.ContractMade ? "Contract made" : "Contract failed");
            var sb = new StringBuilder();
            sb.Append(declarerName).Append(" bid <b>").Append(s.Bid).Append("</b>, trump <b>").Append(s.Trump).Append("</b>\n");
            sb.Append("Declarers took <b>").Append(s.DeclaringTricks).Append("</b> tricks, defenders <b>").Append(s.DefendingTricks).Append("</b>\n\n");
            sb.Append("Us  ").Append(Signed(myDelta)).Append("      Them  ").Append(Signed(theirDelta)).Append("\n");
            sb.Append("<size=64><b>").Append(mine).Append("  -  ").Append(theirs).Append("</b></size>");
            _body.text = sb.ToString();
            _title.color = good ? new Color(0.55f, 0.95f, 0.6f) : new Color(1f, 0.55f, 0.5f);
            _continue.GetComponentInChildren<TMP_Text>().text = continueLabel;

            _clicked = false;
            gameObject.SetActive(true);
            yield return Tween.Fade(_group, 1f, 0.2f);
            for (float t = 0f; !_clicked && (autoDismissSeconds <= 0f || t < autoDismissSeconds); t += Time.deltaTime) yield return null;
            yield return Tween.Fade(_group, 0f, 0.15f);
            gameObject.SetActive(false);
        }

        private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();
    }
}
