using JishoNET.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Shinobu.Helpers
{
    class OnlineDictionary : IJapaneseDictionary
    {
        private readonly JishoClient client;
        public OnlineDictionary()
        {
            client = new JishoClient();
        }

        public async Task<Definition> GetDefinitionAsync(string word)
        {
            JishoResult<JishoDefinition[]> result = await client.GetDefinitionAsync(word);
            if (result.Data != null && result.Data.Length > 0)
            {
                var allWords = new HashSet<string>();
                var allReadings = new HashSet<string>();
                var allDefinitions = new List<string>();
                var allTags = new HashSet<string>();

                foreach (var jishoDef in result.Data)
                {
                    foreach (var jWord in jishoDef.Japanese)
                    {
                        if (!string.IsNullOrEmpty(jWord.Word)) allWords.Add(jWord.Word);
                        if (!string.IsNullOrEmpty(jWord.Reading)) allReadings.Add(jWord.Reading);
                    }
                    foreach (var sense in jishoDef.Senses)
                    {
                        allDefinitions.AddRange(sense.EnglishDefinitions);
                        foreach (var tag in sense.Tags)
                        {
                            allTags.Add(tag);
                        }
                    }
                }

                var definition = new Definition(
                    string.Join(", ", allWords),
                    string.Join(", ", allReadings),
                    string.Join("; ", allDefinitions),
                    [.. allTags]
                );
                Debug.WriteLine($"Tags found: {string.Join(", ", definition.Tags)}");
                return definition;
            }
            return new Definition("", "", "Not found", []);
        }
    }
}
