using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace Shinobu.Helpers
{
    public class ImageContent
    {
        public int Offset { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Base64Data { get; set; } = string.Empty;
    }

    public class BookContent
    {
        public string TextContent { get; set; } = string.Empty;
        public List<ImageContent> Images { get; set; } = new();
    }

    public interface IContentParser { Task<BookContent> ParseContentAsync(string filePath); }

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
        public async Task<BookContent> ParseContentAsync(string filePath)
        {
            string text = await File.ReadAllTextAsync(filePath);
            return new BookContent { TextContent = text };
        }
    }

    public class PdfContentParser : IContentParser
    {
        public async Task<BookContent> ParseContentAsync(string filePath)
        {
            using (var document = PdfDocument.Open(filePath))
            {
                var text = string.Empty;
                foreach (var page in document.GetPages())
                {
                    text += page.Text;
                }
                var images = new List<ImageContent>();
                int offset = 0;
                foreach (var page in document.GetPages())
                {
                    foreach (var image in page.GetImages())
                    {
                        byte[] imageBytes;
                        if (image.TryGetBytesAsMemory(out var memory))
                        {
                            imageBytes = memory.ToArray();
                        }
                        else
                        {
                            imageBytes = image.RawMemory.ToArray();
                        }
                        string base64Data = Convert.ToBase64String(imageBytes);
                        images.Add(new ImageContent
                        {
                            Offset = offset,
                            Width = image.WidthInSamples,
                            Height = image.HeightInSamples,
                            Base64Data = base64Data
                        });
                    }
                    offset += page.Text.Length;
                }
                return new BookContent { TextContent = text, Images = images };
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
