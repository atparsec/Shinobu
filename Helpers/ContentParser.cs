using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace Shinobu.Helpers
{
    public class ImageContent
    {
        public int Id { get; set; }
        public int Offset { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public string Extension { get; set; } = ".jpg";
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
            { ".pdf", "PDF" },
            { ".epub", "EPUB" }
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
                int imgId = 0;
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

                        images.Add(new ImageContent
                        {
                            Id = imgId++,
                            Offset = offset,
                            Width = image.WidthInSamples,
                            Height = image.HeightInSamples,
                            ImageData = imageBytes
                        });
                    }
                    offset += page.Text.Length;
                }
                return new BookContent { TextContent = text, Images = images };
            }
        }
    }

    public class EpubContentParser : IContentParser
    {
        public async Task<BookContent> ParseContentAsync(string filePath)
        {
            // TODO
            return new BookContent { TextContent = "EPUB parsing not implemented yet." };
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
                ".epub" => new EpubContentParser(),
                _ => throw new NotSupportedException($"File extension {fileExtension} is not supported."),
            };
        }
    }
}
