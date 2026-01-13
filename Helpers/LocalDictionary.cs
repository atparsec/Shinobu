using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Shinobu.Helpers
{
    class LocalDictionary : IJapaneseDictionary
    {
        private readonly string _dictionaryPath = Path.Combine(AppContext.BaseDirectory, "jmdict-eng-3.6.1.json");
        private readonly Dictionary<string, Word> _lookup;
        private Dictionary<string, string> _tagsLookup;
        private readonly Task _initTask;

        public LocalDictionary()
        {
            _lookup = [];
            _tagsLookup = [];
            _initTask = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            string json = await File.ReadAllTextAsync(_dictionaryPath);

            // Move expensive deserialization and dictionary building to thread pool
            await Task.Run(() => DeserializeAndBuildLookup(json));
        }

        private void DeserializeAndBuildLookup(string json)
        {
            JMDict? dict = JsonConvert.DeserializeObject<JMDict>(json);

            _tagsLookup = dict?.Tags ?? [];
            if (dict?.Words != null)
            {
                foreach (Word word in dict.Words)
                {
                    if (word.Kanji != null)
                    {
                        foreach (Kanji kanji in word.Kanji)
                        {
                            if (!string.IsNullOrEmpty(kanji.Text))
                            {
                                _lookup[kanji.Text] = word;
                            }
                        }
                    }
                    if (word.Kana != null)
                    {
                        foreach (Kana kana in word.Kana)
                        {
                            if (!string.IsNullOrEmpty(kana.Text))
                            {
                                _lookup[kana.Text] = word;
                            }
                        }
                    }
                }
            }
        }

        public async Task<Definition> GetDefinitionAsync(string word)
        {
            await _initTask;
            if (_lookup.TryGetValue(word, out Word? wordEntry))
            {
                string reading = string.Join(", ", wordEntry.Kana?.Select(k => k.Text) ?? []);
                IEnumerable<string> meanings = wordEntry.Sense?.SelectMany(s => s.Gloss?.Where(g => g.Lang == "eng").Select(g => g.Text) ?? []) ?? [];
                string meaning = string.Join("; ", meanings);
                IEnumerable<string> allTags = wordEntry.Sense?.SelectMany(s => (s.Field ?? []).Concat(s.Misc ?? []).Concat(s.PartOfSpeech ?? [])) ?? [];
                List<string> tags = allTags.Distinct().ToList().Select(t => _tagsLookup.TryGetValue(t, out string? value) ? value : t).ToList();
                return new Definition(word, reading, meaning, tags);
            }
            else
            {
                return new Definition(word, "", "Not found", []);
            }
        }
    }

    public class JMDict
    {
        [JsonProperty("version")]
        public required string Version { get; set; }

        [JsonProperty("languages")]
        public required List<string> Languages { get; set; }

        [JsonProperty("commonOnly")]
        public required bool CommonOnly { get; set; }

        [JsonProperty("dictDate")]
        public required string DictDate { get; set; }

        [JsonProperty("dictRevisions")]
        public required List<string> DictRevisions { get; set; }

        [JsonProperty("tags")]
        public required Dictionary<string, string> Tags { get; set; }

        [JsonProperty("words")]
        public required List<Word> Words { get; set; }
    }

    public class Word
    {
        [JsonProperty("id")]
        public required string Id { get; set; }

        [JsonProperty("kanji")]
        public required List<Kanji> Kanji { get; set; }

        [JsonProperty("kana")]
        public required List<Kana> Kana { get; set; }

        [JsonProperty("sense")]
        public required List<Sense> Sense { get; set; }
    }

    public class Kanji
    {
        [JsonProperty("common")]
        public bool Common { get; set; }

        [JsonProperty("text")]
        public required string Text { get; set; }

        [JsonProperty("tags")]
        public required List<string> Tags { get; set; }
    }

    public class Kana
    {
        [JsonProperty("common")]
        public bool Common { get; set; }

        [JsonProperty("text")]
        public required string Text { get; set; }

        [JsonProperty("tags")]
        public required List<string> Tags { get; set; }

        [JsonProperty("appliesToKanji")]
        public required List<string> AppliesToKanji { get; set; }
    }

    public class Sense
    {
        [JsonProperty("partOfSpeech")]
        public required List<string> PartOfSpeech { get; set; }

        [JsonProperty("appliesToKanji")]
        public required List<string> AppliesToKanji { get; set; }

        [JsonProperty("appliesToKana")]
        public required List<string> AppliesToKana { get; set; }

        [JsonProperty("related")]
        public required List<List<string>> Related { get; set; }

        [JsonProperty("antonym")]
        public required List<List<string>> Antonym { get; set; }

        [JsonProperty("field")]
        public required List<string> Field { get; set; }

        [JsonProperty("dialect")]
        public required List<string> Dialect { get; set; }

        [JsonProperty("misc")]
        public required List<string> Misc { get; set; }

        [JsonProperty("info")]
        public required List<string> Info { get; set; }

        [JsonProperty("languageSource")]
        public required List<LanguageSource> LanguageSource { get; set; }

        [JsonProperty("gloss")]
        public required List<Gloss> Gloss { get; set; }
    }

    public class Gloss
    {
        [JsonProperty("lang")]
        public required string Lang { get; set; }

        [JsonProperty("gender")]
        public required string Gender { get; set; }

        [JsonProperty("type")]
        public required string Type { get; set; }

        [JsonProperty("text")]
        public required string Text { get; set; }
    }

    public class LanguageSource
    {
        [JsonProperty("lang")]
        public required string Lang { get; set; }

        [JsonProperty("type")]
        public required string Type { get; set; }

        [JsonProperty("wasei")]
        public bool? Wasei { get; set; }

        [JsonProperty("text")]
        public required string Text { get; set; }
    }
}
