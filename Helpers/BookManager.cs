using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shinobu.Helpers;

namespace Shinobu.Helpers
{

    public class BookEntry
    {
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string OriginalFilePath { get; set; } = string.Empty;
        public string? PreviewImagePath { get; set; }
    }

    public static class BookManager
    {
        private static readonly string TempPath = Path.Combine(Path.GetTempPath(), "shinobu");
        private static readonly string BooksPath = Path.Combine(TempPath, "books");
        private static readonly string OriginalFilesPath = Path.Combine(TempPath, "originalfiles");
        private static readonly string IndexPath = Path.Combine(TempPath, "index.json");
        private static readonly Dictionary<string, BookEntry> _index = new();
        private static readonly Dictionary<string, BookContent> _contentCache = new();

        static BookManager()
        {
            Directory.CreateDirectory(BooksPath);
            Directory.CreateDirectory(OriginalFilesPath);
            LoadIndex();
        }

        public static async Task<BookEntry?> CreateBookAsync(string filePath)
        {
            if (!File.Exists(filePath) || !SupportedFileTypes.Extensions.ContainsKey(Path.GetExtension(filePath).ToLower()))
            {
                return null;
            }

            string title = Path.GetFileNameWithoutExtension(filePath);
            if (_index.ContainsKey(title))
            {
                // Already exists
                return _index[title];
            }

            string fileHash = await ComputeFileHashAsync(filePath);
            string originalFileCopy = Path.Combine(OriginalFilesPath, $"{fileHash}{Path.GetExtension(filePath)}");
            if (!File.Exists(originalFileCopy))
            {
                File.Copy(filePath, originalFileCopy);
            }

            string bookDir = Path.Combine(BooksPath, fileHash);
            Directory.CreateDirectory(bookDir);
            string imagesDir = Path.Combine(bookDir, "images");
            Directory.CreateDirectory(imagesDir);

            BookContent content;
            if (!_contentCache.TryGetValue(fileHash, out content))
            {
                content = await ContentParserFactory
                    .GetParser(Path.GetExtension(filePath))
                    .ParseContentAsync(originalFileCopy);
                _contentCache[fileHash] = content;
            }

            // Save images
            foreach (var img in content.Images)
            {
                byte[] data = img.ImageData;
                string imgPath = Path.Combine(imagesDir, $"{img.Id}{img.Extension}");
                await File.WriteAllBytesAsync(imgPath, data);
                img.ImageData = Array.Empty<byte>(); // Clear after saving
            }

            BookEntry entry = new()
            {
                Title = title,
                Hash = fileHash,
                OriginalFilePath = originalFileCopy,
                PreviewImagePath = null
            };
            _index[title] = entry;
            SaveIndex();
            return entry;
        }

        public static IEnumerable<BookEntry> GetBooks()
        {
            return _index.Values;
        }

        public static BookEntry? GetBook(string title)
        {
            return _index.TryGetValue(title, out var entry) ? entry : null;
        }

        public static BookEntry? GetBookByHash(string hash)
        {
            return _index.Values.FirstOrDefault(e => e.Hash == hash);
        }

        public static async Task<BookContent> LoadBookContentAsync(string hash)
        {
            if (_contentCache.TryGetValue(hash, out var content))
            {
                return content;
            }

            // Re-parse if not cached
            var entry = _index.Values.FirstOrDefault(e => e.Hash == hash);
            if (entry == null || !File.Exists(entry.OriginalFilePath))
            {
                throw new InvalidOperationException("Book not found or original file missing.");
            }

            content = await ContentParserFactory
                .GetParser(Path.GetExtension(entry.OriginalFilePath))
                .ParseContentAsync(entry.OriginalFilePath);
            _contentCache[hash] = content;
            return content;
        }

        public static string GetBookDirectory(string hash)
        {
            return Path.Combine(BooksPath, hash);
        }

        private static void LoadIndex()
        {
            if (File.Exists(IndexPath))
            {
                string json = File.ReadAllText(IndexPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, BookEntry>>(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        _index[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        public static void SaveIndex()
        {
            string json = JsonSerializer.Serialize(_index, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(IndexPath, json);
        }

        private static async Task<string> ComputeFileHashAsync(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = await sha.ComputeHashAsync(stream);
            return Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-");
        }

        public static string HTMLError(string message)
        {
            return $@"
                <div style='
                position: fixed;
                top: 0; left: 0; right: 0;
                z-index: 10000;
                background: #ef4444 !important;
                color: white !important;
                padding: 16px;
                font-family: system-ui, sans-serif;
                font-size: 15px;
                font-weight: 500;
                text-align: center;
                box-shadow: 0 2px 10px rgba(0,0,0,0.2);
                border: 2px solid #dc2626;'>
                <strong>{message}</strong>
            </div><br/>";
        }

        public static string InjectImages(string text, List<ImageContent> images)
        {
            var sb = new StringBuilder(text);
            foreach (var img in images.OrderByDescending(i => i.Offset))
            {
                sb.Insert(img.Offset,
                    $"<img src='images/{img.Id}{img.Extension}' style='max-width:100%;display:block;margin:1em auto;'/>");
            }
            return sb.ToString();
        }

        public static async Task<string> BuildHtml(BookContent content, FuriganaGenerator furiganaGenerator, JlptLevel jlpt, bool isVertical, double margin)
        {
            string textWithImages;
            try
            {
                textWithImages = InjectImages(content.TextContent, content.Images);
            }
            catch (Exception ex)
            {
                textWithImages = HTMLError("Could not inject images " + ex.Message) + content.TextContent;
            }
            string furiganaHtml;
            try
            {
                furiganaHtml = await furiganaGenerator.GenerateHtmlFuriganaAsync(textWithImages, jlpt);
            }
            catch (Exception ex)
            {
                furiganaHtml = HTMLError("Could not generate furigana " + ex.Message) + textWithImages;
            }

            return $@"
                    <html>
                    <head>
                    <link rel='stylesheet' href='../../shinobu_styles.css'>
                    </head>
                    <body>
                    <div id='pager'>
                    {furiganaHtml.Replace("\n", "<br/>")}
                    </div>

                    <script>
                    function paginate() {{
                        const isVertical = {isVertical.ToString().ToLower()};
                        let totalPages;
                        let lengths;
                        if (isVertical) {{
                            const pageHeight = document.documentElement.clientHeight;
                            totalPages = Math.ceil(pager.scrollHeight / pageHeight);
                            lengths = new Array(totalPages).fill(0);
                        }} else {{
                            const pageWidth = document.documentElement.clientWidth;
                            totalPages = Math.ceil(pager.scrollWidth / pageWidth);
                            lengths = new Array(totalPages).fill(0);
                        }}
                        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null, false);
                        let node;
                        while (node = walker.nextNode()) {{
                            const range = document.createRange();
                            range.selectNodeContents(node);
                            const rect = range.getBoundingClientRect();
                            let page;
                            if (isVertical) {{
                                const pageHeight = document.documentElement.clientHeight;
                                page = Math.floor(rect.top / pageHeight);
                            }} else {{
                                const pageWidth = document.documentElement.clientWidth;
                                page = Math.floor(rect.left / pageWidth);
                            }}
                            if (page >= 0 && page < totalPages) {{
                                lengths[page] += node.textContent.length;
                            }}
                        }}
                        window.chrome.webview.postMessage('pages:' + JSON.stringify(lengths));
                    }}

                    function goToPage(p) {{
                        if ({isVertical.ToString().ToLower()}) {{
                            const pageHeight = document.documentElement.clientHeight;
                            window.scrollTo({{ top: p * (pageHeight + {margin - 30}), behavior: 'smooth' }});
                            return;
                        }}
                        const pageWidth = document.documentElement.clientWidth;
                        window.scrollTo({{ left: p * pageWidth, behavior: 'smooth' }});
                    }}

                    document.addEventListener('mouseup', () => {{
                        const sel = window.getSelection();
                        if (!sel.rangeCount) return;

                        const range = sel.getRangeAt(0).cloneContents();
                        range.querySelectorAll('rt').forEach(e => e.remove());

                        const text = range.textContent.trim();
                        if (!text) return;

                        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                        let offset = 0;
                        let node;
                        while (node = walker.nextNode()) {{
                            if (node === sel.anchorNode) {{
                                offset += sel.anchorOffset;
                                break;
                            }}
                            offset += node.textContent.length;
                        }}

                        window.chrome.webview.postMessage('selected:' + offset + ':' + text);
                    }});

                    window.addEventListener('resize', paginate);
                    paginate();
                    </script>
                    </body>
                    </html>";
        }

        public static string BuildCss(double fontSize, double lineHeight, string fontFamily, BookTheme theme, bool isVertical, double margin, double webViewWidth, double webViewHeight)
        {
            string backgroundColor = theme.Background;
            string textColor = theme.Foreground;
            string accentColor = "#0078D4"; // Default accent, or get from settings
            string accentHex = accentColor;

            string bodyStyle = $@"
                background-color: {backgroundColor};
                color: {textColor}; 
                font-size: {fontSize}px; 
                line-height: {lineHeight * fontSize}px; 
                font-family: {fontFamily}; 
                overflow: hidden;
                padding: 0;
                margin: 0;
                overflow-wrap: normal;
                ";

            string pagerStyle = $@"
                column-width: {webViewWidth}px;
                text-align: justify;
                padding: {margin}px;
                text-combine-upright: digits 2;
                hanging-punctuation: allow-end;
                line-break: strict;
            ";
            if (isVertical)
            {
                pagerStyle += $@"
                    
                    width: calc(100% - {margin * 2}px);
                    column-gap: {margin * 2 + 40}px;
                    margin-bottom: 40px;
                    writing-mode: vertical-rl;
                    text-orientation: mixed;
                ";
            }
            else
            {
                pagerStyle += $@"
                    max-height: {webViewHeight - 120}px;
                    column-gap: {margin * 2}px;
                    box-sizing: border-box;
                    position: relative; 
                    margin: 0px;
                ";
            }

            return $@"
                    body {{ {bodyStyle} }}
                    #pager {{
                        {pagerStyle}
                    }}
                    rt {{
                        user-select: none;
                        pointer-events: none;
                    }}
                    ::selection {{ 
                            background: {accentHex};
                            box-shadow: inset 0 0 12px rgba(255, 190, 40, 0.35);
                            text-shadow: 0 0 5px rgba(255, 220, 60, 0.6);
                        }}";
        }

        public static void SetPreviewImagePath(string hash, string imagePath)
        {
            var entry = _index.Values.FirstOrDefault(e => e.Hash == hash);
            if (entry != null)
            {
                entry.PreviewImagePath = imagePath;
                SaveIndex();
            }
        }
    }
}