using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Shinobu.Helpers.Dictionary;
using Shinobu.Helpers.Books;
using Windows.UI;

namespace Shinobu.Helpers.Content
{
    public static class HtmlRenderer
    {
        public static async Task<string> RenderPageAsync(
            PageContent page,
            FuriganaGenerator furiganaGenerator,
            JlptLevel jlptLevel,
            bool isVertical,
            double fontSize,
            double lineHeight,
            string fontFamily,
            BookTheme theme,
            double margin,
            double webViewWidth,
            double webViewHeight)
        {
            var annotatedNodes = await AnnotateAsync(page.Nodes, furiganaGenerator, jlptLevel);

            string bodyContent = BuildBodyContent(annotatedNodes);

            string css = BuildPageCss(fontSize, lineHeight, fontFamily, theme, isVertical, margin, webViewWidth, webViewHeight);

            string html = $$"""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                    <style>
                        {{css}}
                    </style>
                </head>
                <body>
                    <div id="page" class="{{(isVertical ? "vertical" : "horizontal")}}">
                        {{bodyContent}}
                    </div>

                    <script>
                        (function() {
                            const isVertical = {{isVertical.ToString().ToLower()}};

                            document.addEventListener('mouseup', () => {
                                const sel = window.getSelection();
                                if (!sel || !sel.rangeCount) return;

                                const range = sel.getRangeAt(0);
                                const fragment = range.cloneContents();
                                fragment.querySelectorAll('rt').forEach(e => e.remove());

                                const text = fragment.textContent.trim();
                                if (!text) return;

                                // Compute offset within this page's visible text
                                let offset = 0;
                                const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                                let node;
                                while ((node = walker.nextNode())) {
                                    if (node === sel.anchorNode) {
                                        offset += sel.anchorOffset;
                                        break;
                                    }
                                    offset += node.textContent.length;
                                }

                                window.chrome.webview.postMessage('selected:' + offset + ':' + text);
                            });

                            // Keyboard / wheel navigation (per-page)
                            document.addEventListener('keydown', (e) => {
                                if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
                                    window.chrome.webview.postMessage('nav: next');
                                    e.preventDefault();
                                } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
                                    window.chrome.webview.postMessage('nav: prev');
                                    e.preventDefault();
                                }
                            });

                            document.addEventListener('wheel', (e) => {
                                if (e.deltaY > 0) {
                                    window.chrome.webview.postMessage('nav: next');
                                } else if (e.deltaY < 0) {
                                    window.chrome.webview.postMessage('nav: prev');
                                }
                            });

                            // Image clicks
                            document.querySelectorAll('img[data-image-id]').forEach(img => {
                                img.addEventListener('click', () => {
                                    const id = img.getAttribute('data-image-id');
                                    window.chrome.webview.postMessage('image:' + id);
                                });
                            });

                            // Signal that this page is ready (for any future paging logic)
                            window.chrome.webview.postMessage('page-ready');
                        })();
                    </script>
                </body>
                </html>
                """;

            return html;
        }

        private static async Task<IReadOnlyList<DocumentNode>> AnnotateAsync(
            IReadOnlyList<DocumentNode> nodes,
            FuriganaGenerator generator,
            JlptLevel level)
        {
            var result = new List<DocumentNode>(nodes.Count);

            foreach (var node in nodes)
            {
                result.Add(await AnnotateNodeAsync(node, generator, level));
            }

            return result;
        }

        private static async Task<DocumentNode> AnnotateNodeAsync(
            DocumentNode node,
            FuriganaGenerator generator,
            JlptLevel level)
        {
            switch (node)
            {
                case TextNode text:
                    if (!ContainsKanji(text.Text))
                        return text;

                    string annotatedHtml = await generator.GenerateHtmlFuriganaAsync(EscapeForSingleText(text.Text), level);

                    return ParseSimpleRubyHtml(annotatedHtml);

                case ParagraphNode p:
                    var children = new List<DocumentNode>();
                    foreach (var child in p.Children)
                    {
                        children.Add(await AnnotateNodeAsync(child, generator, level));
                    }
                    return new ParagraphNode(children);

                case HeadingNode h:
                    var hChildren = new List<DocumentNode>();
                    foreach (var child in h.Children)
                    {
                        hChildren.Add(await AnnotateNodeAsync(child, generator, level));
                    }
                    return new HeadingNode(h.Title, h.Level, hChildren);

                case RubyNode r:
                    return r; // already annotated

                case ImageNode img:
                    return img;

                case LineBreakNode lb:
                    return lb;

                default:
                    return node;
            }
        }

        private static bool ContainsKanji(string s) => s.Any(c => c is >= '\u4E00' and <= '\u9FFF');

        private static string EscapeForSingleText(string text)
        {
            return System.Net.WebUtility.HtmlEncode(text);
        }
        private static (byte[]? Bytes, string Extension) PrepareImageForEmbedding(ImageNode img)
        {
            if (img.Data == null || img.Data.Length == 0)
                return (null, img.Extension);
            return (img.Data, img.Extension);
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

        private static DocumentNode ParseSimpleRubyHtml(string htmlFragment)
        {
            string decoded = System.Net.WebUtility.HtmlDecode(htmlFragment);

            var children = new List<DocumentNode>();
            int i = 0;

            while (i < decoded.Length)
            {
                if (decoded[i] == '<')
                {
                    // Look for <ruby>...</ruby>
                    if (decoded.IndexOf("<ruby>", i, StringComparison.Ordinal) == i)
                    {
                        int endRuby = decoded.IndexOf("</ruby>", i, StringComparison.Ordinal);
                        if (endRuby > i)
                        {
                            string inside = decoded.Substring(i + 6, endRuby - (i + 6));
                            int rtStart = inside.IndexOf("<rt", StringComparison.Ordinal);
                            if (rtStart >= 0)
                            {
                                string baseText = inside[..rtStart].Trim();
                                int rtEnd = inside.IndexOf("</rt>", rtStart, StringComparison.Ordinal);
                                string ruby = rtEnd > rtStart
                                    ? inside.Substring(rtStart, rtEnd - rtStart).Split('>')[^1]
                                    : "";
                                children.Add(new RubyNode(baseText, ruby));
                                i = endRuby + 7;
                                continue;
                            }
                        }
                    }

                    int tagEnd = decoded.IndexOf('>', i);
                    if (tagEnd > i) i = tagEnd + 1;
                    else i++;
                    continue;
                }
                int nextTag = decoded.IndexOf('<', i);
                string run = nextTag > i ? decoded[i..nextTag] : decoded[i..];
                if (!string.IsNullOrEmpty(run))
                {
                    children.Add(new TextNode(System.Net.WebUtility.HtmlDecode(run)));
                }
                i = nextTag > i ? nextTag : decoded.Length;
            }

            if (children.Count == 1) return children[0];
            if (children.Count == 0) return new TextNode("");
            return new ParagraphNode(children);
        }

        private static string BuildBodyContent(IReadOnlyList<DocumentNode> nodes)
        {
            var sb = new StringBuilder();
            foreach (var node in nodes)
            {
                AppendNode(sb, node);
            }
            return sb.ToString();
        }

        private static void AppendNode(StringBuilder sb, DocumentNode node)
        {
            switch (node)
            {
                case TextNode t:
                    sb.Append(System.Net.WebUtility.HtmlEncode(t.Text));
                    break;

                case RubyNode r:
                    sb.Append($"<ruby>{System.Net.WebUtility.HtmlEncode(r.BaseText)}<rt class=\"furigana\">{System.Net.WebUtility.HtmlEncode(r.RubyText)}</rt></ruby>");
                    break;

                case ParagraphNode p:
                    sb.Append("<p>");
                    foreach (var c in p.Children) AppendNode(sb, c);
                    sb.Append("</p>");
                    break;

                case HeadingNode h:
                    string tag = h.Level switch { 1 => "h1", 2 => "h2", 3 => "h3", _ => "h4" };
                    sb.Append($"<{tag}>");
                    sb.Append(System.Net.WebUtility.HtmlEncode(h.Title));
                    foreach (var c in h.Children) AppendNode(sb, c);
                    sb.Append($"</{tag}>");
                    break;

                case ImageNode img:
                    var (imgBytes, imgExt) = PrepareImageForEmbedding(img);
                    if (imgBytes == null || imgBytes.Length == 0)
                    {
                        break;
                    }
                    string b64 = Convert.ToBase64String(imgBytes);
                    string mime = imgExt.Equals(".png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpeg";
                    sb.Append($"<img src=\"data:image/{mime};base64,{b64}\" data-image-id=\"{img.Id}\" style=\"cursor:pointer; display:block; max-width:100%; height:auto; margin:0.25em 0; break-inside:avoid; page-break-inside:avoid;\"/>");
                    break;

                case LineBreakNode:
                    sb.Append("<br/>");
                    break;
            }
        }

        private static string BuildPageCss(
            double fontSize,
            double lineHeight,
            string fontFamily,
            BookTheme theme,
            bool isVertical,
            double margin,
            double webViewWidth,
            double webViewHeight)
        {
            string bg = theme.Background;
            string fg = theme.Foreground;
            Color accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            string accentHex = $"#{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}";

            string baseStyle = $$"""
                html, body { margin:0; padding:0; background: {{bg}}; color: {{fg}}; font-family: {{fontFamily}}; font-size: {{fontSize}}px; line-height: {{lineHeight * fontSize}}px; overflow: hidden; }
                #page { padding: {{margin}}px; box-sizing: border-box; }
                rt { user-select: none; pointer-events: none; font-size: 0.6em; }
                ::selection { background: {{accentHex}}; color: #000; }
                p { margin: 0.4em 0; }
                img { display: block; max-width: 100%; height: auto; margin: 0.25em 0; break-inside: avoid; page-break-inside: avoid; }
            """;

            if (isVertical)
            {
                baseStyle += $$"""
                    #page {
                        writing-mode: vertical-rl;
                        text-orientation: mixed;
                        column-width: {{webViewHeight - (margin * 2)}}px;
                        column-gap: {{margin * 2}}px;
                        height: calc(100% - {{margin * 2}}px);
                    }
                """;
            }
            else
            {
                baseStyle += $$"""
                    #page {
                        column-width: {{webViewWidth - (margin * 2)}}px;
                        column-gap: {{margin * 2}}px;
                        max-height: {{webViewHeight - 50 - (margin * 2)}}px;
                    }
                """;
            }

            return baseStyle;
        }
    }
}


