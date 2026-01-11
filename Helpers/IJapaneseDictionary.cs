using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shinobu.Helpers
{
    public interface IJapaneseDictionary
    {
        Task<Definition> GetDefinitionAsync(string word);
    }

    public class Definition(string word, string reading, string meaning, List<string> tags)
    {
        public string Word { get; set; } = word;
        public string Reading { get; set; } = reading;
        public string Meaning { get; set; } = meaning;
        public List<string> Tags { get; set; } = tags;
    }
}
