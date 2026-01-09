using Kawazu;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shinobu.Pages
{
    public class FuriganaGenerator
    {
        private readonly KawazuConverter _converter;

        public FuriganaGenerator()
        {
            _converter = new KawazuConverter();
        }

        public async Task<string> GenerateHtmlFuriganaAsync(string text)
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

                    if (kanjiLength == surface.Length)
                    {
                        // No okurigana → whole thing
                        sb.Append($"<ruby>{surface}<rt>{hira}</rt></ruby>");
                    }
                    else if (kanjiLength > 0)
                    {
                        string kanjiPart = surface.Substring(0, kanjiLength);
                        string okuriPart = surface.Substring(kanjiLength);

                        // Approximation: reading for kanji part is total reading minus okurigana reading
                        string kanjiReading = hira.Substring(0, hira.Length - okuriPart.Length);

                        sb.Append($"<ruby>{kanjiPart}<rt>{kanjiReading}</rt></ruby>");
                        sb.Append(okuriPart);
                    }
                    else
                    {
                        sb.Append($"<ruby>{surface}<rt>{hira}</rt></ruby>");
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
    }
}
