using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace Shinobu.Helpers.Content
{
    public class SupportedFileTypes
    {
        public static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".txt", "Plain Text" },
            { ".pdf", "PDF" },
            // TODO: Add epub
        };
    }

    public interface IDocumentParser
    {
        Task<IReadOnlyList<DocumentNode>> ParseAsync(string filePath);
    }

    public static class DocumentParserFactory
    {
        public static IDocumentParser GetParser(string fileExtension)
        {
            string ext = fileExtension.ToLowerInvariant();
            return ext switch
            {
                ".txt" => new TextDocumentParser(),
                ".pdf" => new PdfDocumentParser(),
                _ => throw new NotSupportedException($"File extension {fileExtension} is not supported."),
            };
        }
    }

    internal sealed class TextDocumentParser : IDocumentParser
    {
        public async Task<IReadOnlyList<DocumentNode>> ParseAsync(string filePath)
        {
            string text = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            var nodes = new List<DocumentNode>();

            string[] paragraphs = text.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries);

            if (paragraphs.Length <= 1 && text.Contains('\n'))
            {
                paragraphs = text.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
            }

            foreach (var raw in paragraphs)
            {
                string p = raw.Trim();
                if (p.Length == 0) continue;
                const int MaxChunk = 900;
                if (p.Length <= MaxChunk)
                {
                    nodes.Add(new ParagraphNode([new TextNode(p)]));
                }
                else
                {
                    for (int i = 0; i < p.Length; i += MaxChunk)
                    {
                        string chunk = p.Substring(i, Math.Min(MaxChunk, p.Length - i));
                        nodes.Add(new ParagraphNode([new TextNode(chunk)]));
                    }
                }
            }
            return nodes;
        }
    }

    internal sealed class PdfDocumentParser : IDocumentParser
    {
        public async Task<IReadOnlyList<DocumentNode>> ParseAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var nodes = new List<DocumentNode>();
                using var document = PdfPigDocument.Open(filePath);

                int pageIndex = 0;
                foreach (var page in document.GetPages())
                {
                    if (pageIndex > 0)
                    {
                        nodes.Add(new LineBreakNode());
                    }

                    // Text content for the page
                    var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
                    var pageText = string.Join(" ", words.Where(w => w.Text.Length > 0).Select(w => w.Text));

                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        // Split page text into rough paragraphs on double spaces / sentence groups
                        var paras = SplitIntoParagraphs(pageText);
                        foreach (var para in paras)
                        {
                            if (!string.IsNullOrWhiteSpace(para))
                            {
                                nodes.Add(new ParagraphNode([new TextNode(para.Trim())]));
                            }
                        }
                    }

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

                        string ext = ".jpg";
                        // try figuring out extension from magic bytes (PNG or JPEG)
                        if (imageBytes.Length > 4)
                        {
                            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                                ext = ".png";
                            else if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                                ext = ".jpg";
                        }

                        nodes.Add(new ImageNode(
                            imageBytes,
                            ext,
                            image.WidthInSamples,
                            image.HeightInSamples));
                    }

                    pageIndex++;
                }

                return (IReadOnlyList<DocumentNode>)nodes;
            }).ConfigureAwait(false);
        }

        private static List<string> SplitIntoParagraphs(string pageText)
        {
            var result = new List<string>();
            var parts = pageText.Split(["\r\n\r\n", "\n\n", "  "], StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                string t = p.Trim();
                if (t.Length > 0)
                {
                    result.Add(t);
                }
            }
            if (result.Count == 0 && pageText.Length > 0)
            {
                result.Add(pageText.Trim());
            }
            return result;
        }
    }
}


