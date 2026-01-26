using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Shinobu.Helpers
{
    public class Bookmark
    {
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("note")]
        public string Note { get; set; } = string.Empty;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = [];

        [JsonProperty("pageNumber")]
        public int PageNumber { get; set; }

        [JsonProperty("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonProperty("dateAdded")]
        public DateTime DateAdded { get; set; }

        [JsonProperty("offset")]
        public (int Start, int End) Offset { get; set; }
    }
}