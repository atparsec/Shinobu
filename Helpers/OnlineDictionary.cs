using JishoNET.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Shinobu.Helpers
{
    class OnlineDictionary : IJapaneseDictionary
    {
        private readonly JishoClient client;
        public OnlineDictionary()
        {
            client = new();
        }

        public async Task<Definition> GetDefinitionAsync(string word)
        {
            JishoResult<JishoDefinition[]> result = await client.GetDefinitionAsync(word);
            if (result.Data != null && result.Data.Length > 0)
            {
                HashSet<string> allWords = [];
                HashSet<string> allReadings = [];
                List<string> allDefinitions = [];
                HashSet<string> allTags = [];

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

                Definition definition = new(
                    string.Join(", ", allWords),
                    string.Join(", ", allReadings),
                    string.Join("; ", allDefinitions),
                    [.. allTags]
                );
                return definition;
            }
            return new Definition("", "", "Not found", []);
        }
    }
}
