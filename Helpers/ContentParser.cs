using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace Shinobu.Helpers
{
    public interface IContentParser { Task<string> ParseContentAsync(string filePath); }

    public class SupportedFileTypes
    {
        public static readonly Dictionary<string, string> Extensions = new()
        {
            { ".txt", "Plain Text" },
            { ".pdf", "PDF" }
        };
    }

    public class TextContentParser : IContentParser
    {
        public async Task<string> ParseContentAsync(string filePath) { return await Task.Run(() => File.ReadAllTextAsync(filePath)); }
    }

    public class PdfContentParser : IContentParser
    {
        public async Task<string> ParseContentAsync(string filePath)
        {
            using (var document = PdfDocument.Open(filePath))
            {
                var text = string.Empty;
                foreach (var page in document.GetPages())
                {
                    text += page.Text;
                }
                return await Task.FromResult(text);
            }
        }
    }

    public static class ContentParserFactory
    {
        public static IContentParser GetParser(string fileExtension)
        {
            return fileExtension.ToLower() switch
            {
                ".txt" => new TextContentParser(),
                ".pdf" => new PdfContentParser(),
                _ => throw new NotSupportedException($"File extension {fileExtension} is not supported."),
            };
        }
    }
}
