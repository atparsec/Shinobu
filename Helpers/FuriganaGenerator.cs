using Kawazu;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shinobu.Helpers
{
    public class FuriganaGenerator
    {
        private readonly KawazuConverter _converter;
        private static readonly JLPTKanji _jlptKanji = new();

        public FuriganaGenerator()
        {
            _converter = new KawazuConverter();
        }

        public async Task<string> GenerateHtmlFuriganaAsync(string text, JlptLevel level)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (text.Contains("<ruby>")) return text; // already has → skip

            var divisions = await _converter.GetDivisions(
                text,
                To.Hiragana,
                Mode.Furigana,
                RomajiSystem.Hepburn,
                "⦗", "⦘"
            );

            var sb = new StringBuilder();

            foreach (var division in divisions)
            {
                string surface = division.Surface;
                string hira = division.HiraReading;

                if (ContainsKanji(surface))
                {
                    // Try to separate kanji stem vs okurigana tail
                    int kanjiLength = surface.Length;
                    while (kanjiLength > 0 && Utilities.IsKana(surface[kanjiLength - 1]))
                        kanjiLength--;

                    if (IsAllKnownKanji(surface, level))
                    {
                        // All kanji known → skip furigana for this part
                        sb.Append(surface);
                        continue;
                    }

                    if (kanjiLength == surface.Length)
                    {
                        // No okurigana → whole thing
                        sb.Append($"<ruby>{surface}<rt class=\"furigana\" aria-hidden=\"true\">{hira}</rt></ruby>");
                    }
                    else if (kanjiLength > 0)
                    {
                        string kanjiPart = surface[..kanjiLength];
                        string okuriPart = surface[kanjiLength..];

                        // Approximation: reading for kanji part is total reading minus okurigana reading
                        string kanjiReading = hira[..^okuriPart.Length];

                        sb.Append($"<ruby>{kanjiPart}<rt class=\"furigana\" aria-hidden=\"true\">{kanjiReading}</rt></ruby>");
                        sb.Append(okuriPart);
                    }
                    else
                    {
                        sb.Append($"<ruby>{surface}<rt class=\"furigana\" aria-hidden=\"true\">{hira}</rt></ruby>");
                    }
                }
                else
                {
                    sb.Append(surface);
                    continue;
                }
            }

            return sb.ToString().Replace("\r\n", "<br/>").Replace("\n", "<br/>");
        }

        private static bool ContainsKanji(string s)
        {
            return s.Any(c => c >= '\u4E00' && c <= '\u9FFF');
        }

        private static bool IsAllKnownKanji(string surface, JlptLevel level)
        {
            if (level == JlptLevel.N1)
                return true; // If N1 specified, all kanji are considered "known" to be ignored
            var kanjiChars = surface.Where(c => c >= '\u4E00' && c <= '\u9FFF').ToList();
            var knownKanjiSet = _jlptKanji.KanjiLevels[level];
            if (knownKanjiSet == null)
                return false;
            return kanjiChars.All(c => knownKanjiSet.Contains(c));

        }

    }
}
