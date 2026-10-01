using System.Collections.Generic;
using System.Text;

namespace Tarneeb.Localization
{
    /// <summary>
    /// Makes Arabic text displayable in TextMeshPro, which neither joins letters nor reorders right-to-left text:
    /// <list type="number">
    /// <item>contextual shaping: every letter is replaced by its isolated / initial / medial / final presentation form (U+FE70..U+FEFC) and lam + alef
    /// become one ligature glyph, so the letters connect;</item>
    /// <item>visual ordering: the line is reordered for a right-to-left paragraph, keeping numbers and Latin words (names, digits) left-to-right
    /// inside it.</item>
    /// </list>
    /// Input is logical order (as typed or stored); output is what TMP should be given. Plain text only (no rich-text tags) and no diacritics are
    /// expected; diacritics are passed through and ignored when joining.
    /// </summary>
    public static class ArabicShaper
    {
        private readonly struct Forms
        {
            public readonly char Iso, Fin, Ini, Med;
            public Forms(int iso, int fin, int ini = 0, int med = 0) { Iso = (char)iso; Fin = (char)fin; Ini = (char)ini; Med = (char)med; }
            public bool Dual => Ini != 0;
        }

        private static readonly Dictionary<char, Forms> Map = new Dictionary<char, Forms>
        {
            { 'ء', new Forms(0xFE80, 0xFE80) },
            { 'آ', new Forms(0xFE81, 0xFE82) }, { 'أ', new Forms(0xFE83, 0xFE84) }, { 'ؤ', new Forms(0xFE85, 0xFE86) },
            { 'إ', new Forms(0xFE87, 0xFE88) }, { 'ئ', new Forms(0xFE89, 0xFE8A, 0xFE8B, 0xFE8C) }, { 'ا', new Forms(0xFE8D, 0xFE8E) },
            { 'ب', new Forms(0xFE8F, 0xFE90, 0xFE91, 0xFE92) }, { 'ة', new Forms(0xFE93, 0xFE94) },
            { 'ت', new Forms(0xFE95, 0xFE96, 0xFE97, 0xFE98) }, { 'ث', new Forms(0xFE99, 0xFE9A, 0xFE9B, 0xFE9C) },
            { 'ج', new Forms(0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0) }, { 'ح', new Forms(0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4) },
            { 'خ', new Forms(0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8) }, { 'د', new Forms(0xFEA9, 0xFEAA) }, { 'ذ', new Forms(0xFEAB, 0xFEAC) },
            { 'ر', new Forms(0xFEAD, 0xFEAE) }, { 'ز', new Forms(0xFEAF, 0xFEB0) },
            { 'س', new Forms(0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4) }, { 'ش', new Forms(0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8) },
            { 'ص', new Forms(0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC) }, { 'ض', new Forms(0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0) },
            { 'ط', new Forms(0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4) }, { 'ظ', new Forms(0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8) },
            { 'ع', new Forms(0xFEC9, 0xFECA, 0xFECB, 0xFECC) }, { 'غ', new Forms(0xFECD, 0xFECE, 0xFECF, 0xFED0) },
            { 'ف', new Forms(0xFED1, 0xFED2, 0xFED3, 0xFED4) }, { 'ق', new Forms(0xFED5, 0xFED6, 0xFED7, 0xFED8) },
            { 'ك', new Forms(0xFED9, 0xFEDA, 0xFEDB, 0xFEDC) }, { 'ل', new Forms(0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0) },
            { 'م', new Forms(0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4) }, { 'ن', new Forms(0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8) },
            { 'ه', new Forms(0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC) }, { 'و', new Forms(0xFEED, 0xFEEE) }, { 'ى', new Forms(0xFEEF, 0xFEF0) },
            { 'ي', new Forms(0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4) },
        };

        /// <summary>True if the text has Arabic letters in logical (unshaped) form, i.e. it still needs shaping.</summary>
        public static bool NeedsShaping(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++) if (s[i] >= '\u0620' && s[i] <= '\u06FF') return true;       // letters; the Arabic comma/semicolon/question mark (below U+0620) need no shaping
            return false;
        }

        /// <summary>Logical text to the string TMP should display (shaped and in visual order). Lines are handled separately.</summary>
        public static string ToVisual(string logical)
        {
            if (!NeedsShaping(logical)) return logical;
            string[] lines = logical.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Reorder(Shape(lines[i]));
            return string.Join("\n", lines);
        }

        // ------------------------------------------------------------ shaping

        private static bool IsDiacritic(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ' || (c >= 'ۖ' && c <= 'ۭ');

        private static bool PrevJoins(string s, int i)
        {
            for (int k = i - 1; k >= 0; k--)
            {
                if (IsDiacritic(s[k])) continue;
                if (s[k] == 'ـ') return true;                                   // tatweel
                return Map.TryGetValue(s[k], out Forms f) && f.Dual;
            }
            return false;
        }

        private static int NextLetter(string s, int i)
        {
            int j = i + 1;
            while (j < s.Length && IsDiacritic(s[j])) j++;
            return j;
        }

        private static bool NextAccepts(string s, int i)
        {
            int j = NextLetter(s, i);
            if (j >= s.Length) return false;
            if (s[j] == 'ـ') return true;
            return s[j] != 'ء' && Map.ContainsKey(s[j]);                      // hamza never connects to its left
        }

        private static char LamAlef(char alef, bool joinedToPrev)
        {
            switch (alef)
            {
                case 'آ': return joinedToPrev ? 'ﻶ' : 'ﻵ';
                case 'أ': return joinedToPrev ? 'ﻸ' : 'ﻷ';
                case 'إ': return joinedToPrev ? 'ﻺ' : 'ﻹ';
                default: return joinedToPrev ? 'ﻼ' : 'ﻻ';
            }
        }

        private static bool IsAlef(char c) => c == 'آ' || c == 'أ' || c == 'إ' || c == 'ا';

        /// <summary>Contextual forms, still in logical order.</summary>
        public static string Shape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!Map.TryGetValue(c, out Forms f)) { sb.Append(c); continue; }
                bool prev = PrevJoins(s, i);
                if (c == 'ل')
                {
                    int j = NextLetter(s, i);
                    if (j < s.Length && IsAlef(s[j])) { sb.Append(LamAlef(s[j], prev)); i = j; continue; }
                }
                bool next = f.Dual && NextAccepts(s, i);
                char o = f.Dual ? (prev ? (next ? f.Med : f.Fin) : (next ? f.Ini : f.Iso)) : (prev ? f.Fin : f.Iso);
                sb.Append(o);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ visual order (right-to-left paragraph)

        private enum K { R, L, N }

        private static bool IsRtlChar(char c) => (c >= '֐' && c <= 'ࣿ') || (c >= 'יִ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿');

        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')'; case ')': return '(';
                case '[': return ']'; case ']': return '[';
                case '{': return '}'; case '}': return '{';
                case '<': return '>'; case '>': return '<';
                default: return c;
            }
        }

        /// <summary>Reorders one shaped logical line for display from left to right, treating the paragraph as right-to-left.</summary>
        public static string Reorder(string s)
        {
            int n = s.Length;
            if (n == 0) return s;
            var t = new K[n];
            for (int i = 0; i < n; i++)
            {
                char c = s[i];
                if (IsRtlChar(c)) t[i] = K.R;
                else if (char.IsLetterOrDigit(c)) t[i] = K.L;
                else t[i] = K.N;
            }
            // a sign or separator that belongs to a number stays with it
            for (int i = 0; i < n; i++)
            {
                if (t[i] != K.N) continue;
                char c = s[i];
                bool digitBefore = i > 0 && char.IsDigit(s[i - 1]), digitAfter = i + 1 < n && char.IsDigit(s[i + 1]);
                if ((c == '+' || c == '-') && digitAfter) t[i] = K.L;
                else if ((c == '.' || c == ',' || c == ':' || c == '/') && digitBefore && digitAfter) t[i] = K.L;
                else if (c == '%' && digitBefore) t[i] = K.L;
            }
            // neutrals take the direction of their neighbours when both agree on LTR, otherwise the paragraph's (RTL)
            for (int i = 0; i < n;)
            {
                if (t[i] != K.N) { i++; continue; }
                int j = i;
                while (j < n && t[j] == K.N) j++;
                K left = i > 0 ? t[i - 1] : K.R, right = j < n ? t[j] : K.R;
                K res = left == K.L && right == K.L ? K.L : K.R;
                for (int k = i; k < j; k++) t[k] = res;
                i = j;
            }
            var runs = new List<(int start, int len, K kind)>();
            for (int i = 0; i < n;)
            {
                int j = i;
                while (j < n && t[j] == t[i]) j++;
                runs.Add((i, j - i, t[i]));
                i = j;
            }
            var sb = new StringBuilder(n);
            for (int r = runs.Count - 1; r >= 0; r--)
            {
                var run = runs[r];
                if (run.kind == K.L) sb.Append(s, run.start, run.len);
                else for (int k = run.start + run.len - 1; k >= run.start; k--) sb.Append(Mirror(s[k]));
            }
            return sb.ToString();
        }
    }
}
