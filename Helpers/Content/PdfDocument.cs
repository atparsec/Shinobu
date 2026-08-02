using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using PdfPigWord = UglyToad.PdfPig.Content.Word;
using Shinobu.Helpers.Books;
using Shinobu.Helpers.Reader;

namespace Shinobu.Helpers.Content
{
    public sealed class PdfDocument : IBookDocument
    {
        private readonly string _filePath;
        private IReadOnlyList<IReadOnlyList<DocumentNode>> _pages = [];

        private sealed record PositionedNode(double Top, double Left, DocumentNode Node);

        public string FilePath => _filePath;

        public PdfDocument(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        private async Task EnsureParsedAsync()
        {
            if (_pages.Count > 0) return;

            _pages = await Task.Run(() =>
            {
                var result = new List<IReadOnlyList<DocumentNode>>();
                using var doc = PdfPigDocument.Open(_filePath);
                foreach (var page in doc.GetPages())
                {
                    result.Add(ExtractNodesFromPage(page));
                }
                return (IReadOnlyList<IReadOnlyList<DocumentNode>>)result;
            });
        }

        private static IReadOnlyList<DocumentNode> ExtractNodesFromPage(Page page)
        {
            var positionedNodes = new List<PositionedNode>();

            var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
            var lines = GroupWordsToLines(words);

            foreach (var lineWords in lines)
            {
                var sorted = lineWords.OrderBy(w => w.BoundingBox.Left).ToList();
                string text = string.Join(" ", sorted.Select(w => w.Text).Where(t => t.Length > 0));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var bounds = GetLineBounds(lineWords);
                    positionedNodes.Add(new PositionedNode(
                        bounds.Top,
                        bounds.Left,
                        new ParagraphNode([new TextNode(text)])));
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
                try
                {
                    imageBytes = ResizeImageIfNeeded(imageBytes, 1024) ?? imageBytes;
                }
                catch
                {
                }

                string ext = ".jpg";
                if (imageBytes.Length > 4)
                {
                    if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                        ext = ".png";
                    else if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                        ext = ".jpg";
                }

                var (pw, ph) = GetPixelDimensionsFromBytes(imageBytes);
                double width = pw > 0 ? pw : image.WidthInSamples;
                double height = ph > 0 ? ph : image.HeightInSamples;

                positionedNodes.Add(new PositionedNode(
                    image.Bounds.Top,
                    image.Bounds.Left,
                    new ImageNode(
                        imageBytes,
                        ext,
                        width,
                        height)));
            }

            return positionedNodes
                .OrderByDescending(n => n.Top)
                .ThenBy(n => n.Left)
                .Select(n => n.Node)
                .ToList();
        }

        private static UglyToad.PdfPig.Core.PdfRectangle GetLineBounds(IReadOnlyList<PdfPigWord> lineWords)
        {
            double left = lineWords.Min(w => w.BoundingBox.Left);
            double right = lineWords.Max(w => w.BoundingBox.Right);
            double top = lineWords.Max(w => w.BoundingBox.Top);
            double bottom = lineWords.Min(w => w.BoundingBox.Bottom);
            return new UglyToad.PdfPig.Core.PdfRectangle(left, bottom, right, top);
        }

        private static (uint Width, uint Height) GetPixelDimensionsFromBytes(byte[] data)
        {
            if (data == null || data.Length < 8) return (0, 0);
            try
            {
                var stream = new InMemoryRandomAccessStream();
                stream.WriteAsync(data.AsBuffer()).AsTask().GetAwaiter().GetResult();
                stream.Seek(0);
                var decoder = BitmapDecoder.CreateAsync(stream).AsTask().GetAwaiter().GetResult();
                uint w = decoder.PixelWidth;
                uint h = decoder.PixelHeight;
                stream.Dispose();
                return (w, h);
            }
            catch
            {
                return (0, 0);
            }
        }

        private static byte[]? ResizeImageIfNeeded(byte[] data, uint maxDim)
        {
            // fix for goofy issue with some PDFs where the image is huge
            if (data == null || data.Length == 0) return null;
            try
            {
                using var inStream = new InMemoryRandomAccessStream();
                inStream.WriteAsync(data.AsBuffer()).AsTask().GetAwaiter().GetResult();
                inStream.Seek(0);
                var decoder = BitmapDecoder.CreateAsync(inStream).AsTask().GetAwaiter().GetResult();
                uint origW = decoder.PixelWidth;
                uint origH = decoder.PixelHeight;
                if (origW <= maxDim && origH <= maxDim) return data;

                double scale = (double)maxDim / Math.Max(origW, origH);
                uint newW = Math.Max(1u, (uint)(origW * scale));
                uint newH = Math.Max(1u, (uint)(origH * scale));

                var pixelData = decoder.GetPixelDataAsync().AsTask().GetAwaiter().GetResult();
                byte[] pixels = pixelData.DetachPixelData();

                Guid encoderId = BitmapEncoder.JpegEncoderId;
                if (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                    encoderId = BitmapEncoder.PngEncoderId;

                using var outStream = new InMemoryRandomAccessStream();
                var encoder = BitmapEncoder.CreateAsync(encoderId, outStream).AsTask().GetAwaiter().GetResult();
                encoder.BitmapTransform.ScaledWidth = newW;
                encoder.BitmapTransform.ScaledHeight = newH;
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, origW, origH, decoder.DpiX, decoder.DpiY, pixels);
                encoder.FlushAsync().AsTask().GetAwaiter().GetResult();

                outStream.Seek(0);
                using var ms = new MemoryStream();
                outStream.AsStreamForRead().CopyTo(ms);
                return ms.ToArray();
            }
            catch
            {
                return data;
            }
        }

        private static List<List<PdfPigWord>> GroupWordsToLines(List<PdfPigWord> words)
        {
            if (words.Count == 0) return new List<List<PdfPigWord>>();

            var withMid = words
                .Select(w => new { Word = w, MidY = (w.BoundingBox.Top + w.BoundingBox.Bottom) / 2.0 })
                .OrderByDescending(x => x.MidY)
                .ThenBy(x => x.Word.BoundingBox.Left)
                .ToList();

            var lines = new List<List<PdfPigWord>>();
            if (withMid.Count == 0) return lines;

            var current = new List<PdfPigWord> { withMid[0].Word };
            double y = withMid[0].MidY;
            const double yTolerance = 10.0;

            for (int i = 1; i < withMid.Count; i++)
            {
                var w = withMid[i].Word;
                var my = withMid[i].MidY;
                if (Math.Abs(my - y) > yTolerance)
                {
                    if (current.Count > 0) lines.Add(current);
                    current = new List<PdfPigWord> { w };
                    y = my;
                }
                else
                {
                    current.Add(w);
                }
            }
            if (current.Count > 0) lines.Add(current);

            return lines;
        }

        public async ValueTask<PageContent> LoadPageAsync(BookLocation location)
        {
            await EnsureParsedAsync();
            int total = _pages.Count;
            if (total == 0)
            {
                return new PageContent { Nodes = [], StartLocation = location, IsLastPage = true };
            }

            int pageIndex = Math.Max(0, location.ChapterIndex);
            if (pageIndex >= total)
            {
                pageIndex = total - 1;
            }

            var pageNodes = _pages[pageIndex];
            bool isLast = (pageIndex + 1) >= total;

            return new PageContent
            {
                Nodes = pageNodes,
                StartLocation = new BookLocation(pageIndex, 0, 0),
                EndLocation = isLast ? null : new BookLocation(pageIndex + 1, 0, 0),
                IsLastPage = isLast
            };
        }

        public async ValueTask<BookLocation?> GetNextLocationAsync(BookLocation current)
        {
            await EnsureParsedAsync();
            int nextPage = current.ChapterIndex + 1;
            if (nextPage >= _pages.Count) return null;
            return new BookLocation(nextPage, 0, 0);
        }

        public async ValueTask<BookLocation?> GetPreviousLocationAsync(BookLocation current)
        {
            await EnsureParsedAsync();
            int prevPage = current.ChapterIndex - 1;
            if (prevPage < 0) return null;
            return new BookLocation(prevPage, 0, 0);
        }

        public async ValueTask<BookMetadata> GetMetadataAsync()
        {
            await EnsureParsedAsync();
            string title = System.IO.Path.GetFileNameWithoutExtension(_filePath);
            int total = Math.Max(1, _pages.Count);
            return new BookMetadata(title, null, total);
        }

        public ValueTask DisposeAsync()
        {
            _pages = [];
            return ValueTask.CompletedTask;
        }
    }
}


