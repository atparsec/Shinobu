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
        private Dictionary<string, Word> _lookup;
        private Dictionary<string, string> _tagsLookup;
        private Task _initTask;

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
            var dict = JsonConvert.DeserializeObject<JMDict>(json);

            _tagsLookup = dict?.Tags ?? [];
            if (dict?.Words != null)
            {
                foreach (var word in dict.Words)
                {
                    if (word.Kanji != null)
                    {
                        foreach (var kanji in word.Kanji)
                        {
                            if (!string.IsNullOrEmpty(kanji.Text))
                            {
                                _lookup[kanji.Text] = word;
                            }
                        }
                    }
                    if (word.Kana != null)
                    {
                        foreach (var kana in word.Kana)
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
            if (_lookup.TryGetValue(word, out var wordEntry))
            {
                string reading = string.Join(", ", wordEntry.Kana?.Select(k => k.Text) ?? []);
                var meanings = wordEntry.Sense?.SelectMany(s => s.Gloss?.Where(g => g.Lang == "eng").Select(g => g.Text) ?? []) ?? [];
                string meaning = string.Join("; ", meanings);
                var allTags = wordEntry.Sense?.SelectMany(s => (s.Field ?? []).Concat(s.Misc ?? []).Concat(s.PartOfSpeech ?? [])) ?? [];
                var tags = allTags.Distinct().ToList().Select(t => _tagsLookup.TryGetValue(t, out string? value) ? value : t).ToList();
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
        public string Version { get; set; }

        [JsonProperty("languages")]
        public List<string> Languages { get; set; }

        [JsonProperty("commonOnly")]
        public bool CommonOnly { get; set; }

        [JsonProperty("dictDate")]
        public string DictDate { get; set; }

        [JsonProperty("dictRevisions")]
        public List<string> DictRevisions { get; set; }

        [JsonProperty("tags")]
        public Dictionary<string, string> Tags { get; set; }

        [JsonProperty("words")]
        public List<Word> Words { get; set; }
    }

    public class Word
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("kanji")]
        public List<Kanji> Kanji { get; set; }

        [JsonProperty("kana")]
        public List<Kana> Kana { get; set; }

        [JsonProperty("sense")]
        public List<Sense> Sense { get; set; }
    }

    public class Kanji
    {
        [JsonProperty("common")]
        public bool Common { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }
    }

    public class Kana
    {
        [JsonProperty("common")]
        public bool Common { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("appliesToKanji")]
        public List<string> AppliesToKanji { get; set; }
    }

    public class Sense
    {
        [JsonProperty("partOfSpeech")]
        public List<string> PartOfSpeech { get; set; }

        [JsonProperty("appliesToKanji")]
        public List<string> AppliesToKanji { get; set; }

        [JsonProperty("appliesToKana")]
        public List<string> AppliesToKana { get; set; }

        [JsonProperty("related")]
        public List<List<string>> Related { get; set; }

        [JsonProperty("antonym")]
        public List<List<string>> Antonym { get; set; }

        [JsonProperty("field")]
        public List<string> Field { get; set; }

        [JsonProperty("dialect")]
        public List<string> Dialect { get; set; }

        [JsonProperty("misc")]
        public List<string> Misc { get; set; }

        [JsonProperty("info")]
        public List<string> Info { get; set; }

        [JsonProperty("languageSource")]
        public List<LanguageSource> LanguageSource { get; set; }

        [JsonProperty("gloss")]
        public List<Gloss> Gloss { get; set; }
    }

    public class Gloss
    {
        [JsonProperty("lang")]
        public string Lang { get; set; }

        [JsonProperty("gender")]
        public string Gender { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class LanguageSource
    {
        [JsonProperty("lang")]
        public string Lang { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("wasei")]
        public bool? Wasei { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
