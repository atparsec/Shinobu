using Newtonsoft.Json;

namespace Shinobu.Helpers
{
    public class Bookmark
    {
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("definition")]
        public Definition Definition { get; set; } = new Definition("", "", "", []);

        [JsonProperty("pageNumber")]
        public int PageNumber { get; set; }

        [JsonProperty("filePath")]
        public string FilePath { get; set; } = string.Empty;
    }
}