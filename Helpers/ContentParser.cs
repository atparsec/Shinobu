using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using System.Text;

namespace Shinobu.Helpers
{
    public class ImageContent
    {
        public int Id { get; set; }
        public int Offset { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public byte[] ImageData { get; set; } = [];
        public string Extension { get; set; } = ".jpg";
    }

    public class BookContent
    {
        public string TextContent { get; set; } = string.Empty;
        public List<ImageContent> Images { get; set; } = [];
        public List<HeadingNode> TableOfContents { get; set; } = [];
    }

    public class HeadingNode
    {
        public required string Title { get; set; }
        public required int StartOffset { get; set; }
        public required int Level { get; set; }
        public List<HeadingNode> Children { get; } = [];
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
                var textBuilder = new StringBuilder();
                var images = new List<ImageContent>();
                int offset = 0;
                int imgId = 0;
                foreach (var page in document.GetPages())
                {
                    var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
                    var pageText = string.Join(" ", words.Where(w => w.Text.Length > 0).Select(w => w.Text));
                    if (pageText.Length > 30)
                    {
                        textBuilder.AppendLine();
                        textBuilder.AppendLine();
                    }
                    textBuilder.Append(pageText);

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

                    offset += pageText.Length;
                }
                return new BookContent { TextContent = textBuilder.ToString(), Images = images };
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
