using Shinobu.Helpers.Books;
using Shinobu.Helpers.Reader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shinobu.Helpers.Content
{
    public sealed class TxtDocument : IBookDocument
    {
        private readonly string _filePath;
        private IReadOnlyList<DocumentNode>? _nodes;
        private List<BookLocation> _pageStarts = [];
        private const int TargetNodesPerPage = 4;

        public string FilePath => _filePath;

        public TxtDocument(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        private async Task EnsureParsedAsync()
        {
            if (_nodes != null) return;

            var parser = DocumentParserFactory.GetParser(".txt");
            var parsed = await parser.ParseAsync(_filePath);
            _nodes = parsed;
            _pageStarts = BuildPageStarts(_nodes);
        }

        private static List<BookLocation> BuildPageStarts(IReadOnlyList<DocumentNode> nodes)
        {
            var starts = new List<BookLocation>();
            if (nodes.Count == 0)
            {
                starts.Add(new BookLocation(0, 0, 0));
                return starts;
            }

            int nodeIndex = 0;
            int charOffset = 0;
            int pageIndex = 0;

            while (nodeIndex < nodes.Count)
            {
                starts.Add(new BookLocation(pageIndex, nodeIndex, charOffset));
                int end = Math.Min(nodeIndex + TargetNodesPerPage, nodes.Count);
                for (int i = nodeIndex; i < end; i++)
                {
                    charOffset += EstimateNodeTextLength(nodes[i]);
                }
                nodeIndex = end;
                pageIndex++;
            }
            if (starts.Count == 0)
            {
                starts.Add(new BookLocation(0, 0, 0));
            }

            return starts;
        }

        private int ResolvePageIndex(BookLocation location)
        {
            if (_pageStarts.Count == 0)
            {
                return 0;
            }

            if (location.ChapterIndex >= 0 && location.ChapterIndex < _pageStarts.Count)
            {
                return location.ChapterIndex;
            }

            int byParagraph = FindPageIndexForParagraph(location.ParagraphIndex);
            return byParagraph >= 0 ? byParagraph : 0;
        }

        private static int EstimateNodeTextLength(DocumentNode node)
        {
            return node switch
            {
                TextNode t => t.Text.Length + 1,
                ParagraphNode p => p.Children.Sum(EstimateNodeTextLength) + 2,
                HeadingNode h => h.Title.Length + h.Children.Sum(EstimateNodeTextLength) + 2,
                RubyNode r => r.BaseText.Length + 2,
                _ => 1
            };
        }

        public async ValueTask<PageContent> LoadPageAsync(BookLocation location)
        {
            await EnsureParsedAsync();
            if (_nodes == null || _nodes.Count == 0)
            {
                return new PageContent { Nodes = [], StartLocation = location, IsLastPage = true };
            }

            int pageIndex = ResolvePageIndex(location);
            var startLocation = _pageStarts[Math.Max(0, Math.Min(pageIndex, _pageStarts.Count - 1))];
            int startNodeIndex = Math.Max(0, Math.Min(startLocation.ParagraphIndex, _nodes.Count - 1));

            int endNodeIndex = Math.Min(startNodeIndex + TargetNodesPerPage, _nodes.Count);
            var pageNodes = _nodes.Skip(startNodeIndex).Take(endNodeIndex - startNodeIndex).ToList();

            bool isLast = endNodeIndex >= _nodes.Count;

            BookLocation startLoc = startLocation;
            BookLocation? endLoc = isLast ? null : _pageStarts[Math.Min(pageIndex + 1, _pageStarts.Count - 1)];

            return new PageContent
            {
                Nodes = pageNodes,
                StartLocation = startLoc,
                EndLocation = endLoc,
                IsLastPage = isLast
            };
        }

        public async ValueTask<BookLocation?> GetNextLocationAsync(BookLocation current)
        {
            await EnsureParsedAsync();
            if (_pageStarts.Count == 0) return null;
            int currentPage = ResolvePageIndex(current);
            if (currentPage < 0) currentPage = 0;

            int nextPage = currentPage + 1;
            if (nextPage >= _pageStarts.Count) return null;

            return _pageStarts[nextPage];
        }

        public async ValueTask<BookLocation?> GetPreviousLocationAsync(BookLocation current)
        {
            await EnsureParsedAsync();
            if (_pageStarts.Count == 0) return null;

            int currentPage = ResolvePageIndex(current);
            if (currentPage < 0) currentPage = 0;

            int prevPage = currentPage - 1;
            if (prevPage < 0) return null;

            return _pageStarts[prevPage];
        }

        private int FindPageIndexForParagraph(int paragraphIndex)
        {
            int idx = -1;
            for (int i = 0; i < _pageStarts.Count; i++)
            {
                if (_pageStarts[i].ParagraphIndex <= paragraphIndex)
                    idx = i;
                else
                    break;
            }
            return idx;
        }

        public async ValueTask<BookMetadata> GetMetadataAsync()
        {
            await EnsureParsedAsync();
            string title = System.IO.Path.GetFileNameWithoutExtension(_filePath);
            int totalPages = Math.Max(1, _pageStarts.Count);
            return new BookMetadata(title, null, totalPages);
        }

        public ValueTask DisposeAsync()
        {
            _nodes = null;
            _pageStarts.Clear();
            return ValueTask.CompletedTask;
        }
    }
}


