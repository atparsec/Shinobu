using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Shinobu.Helpers
{
    public interface IJapaneseDictionary
    {
        Task<Definition> GetDefinitionAsync(string word);
    }

    public class Definition(string word, string reading, string meaning, List<string> tags)
    {
        [JsonProperty("word")]
        public string Word { get; set; } = word;

        [JsonProperty("reading")]
        public string Reading { get; set; } = reading;

        [JsonProperty("meaning")]
        public string Meaning { get; set; } = meaning;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = tags;
    }
}
