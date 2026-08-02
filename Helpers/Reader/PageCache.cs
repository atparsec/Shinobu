using Shinobu.Helpers.Books;
using Shinobu.Helpers.Content;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shinobu.Helpers.Reader
{
    /// <summary>
    /// Simple bounded LRU-style cache for PageContent.
    /// Limits both count and approximate memory (very rough).
    /// </summary>
    public sealed class PageCache : IDisposable
    {
        private readonly int _maxPages;
        private readonly LinkedList<(BookLocation Key, PageContent Value)> _list = new();
        private readonly Dictionary<BookLocation, LinkedListNode<(BookLocation, PageContent)>> _map = new();
        private long _approxBytes;
        private readonly long _maxBytes;

        public PageCache(int maxPages = 12, long maxBytes = 40 * 1024 * 1024)
        {
            _maxPages = Math.Max(3, maxPages);
            _maxBytes = Math.Max(8 * 1024 * 1024, maxBytes);
        }

        public bool TryGet(BookLocation key, out PageContent? page)
        {
            if (_map.TryGetValue(key, out var node)) // moving
            {
                _list.Remove(node);
                _list.AddFirst(node);
                page = node.Value.Item2;
                return true;
            }
            page = null;
            return false;
        }

        public void Add(BookLocation key, PageContent page)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _list.Remove(existing);
                _list.AddFirst(existing);
                return;
            }

            var node = _list.AddFirst((key, page));
            _map[key] = node;

            long nodeBytes = EstimateSize(page);
            _approxBytes += nodeBytes;

            EvictIfNeeded();
        }

        public void Clear()
        {
            _list.Clear();
            _map.Clear();
            _approxBytes = 0;
        }

        private void EvictIfNeeded()
        {
            while ((_list.Count > _maxPages || _approxBytes > _maxBytes) && _list.Count > 0)
            {
                var last = _list.Last!;
                _list.RemoveLast();
                _map.Remove(last.Value.Key);

                _approxBytes -= EstimateSize(last.Value.Value);
                if (_approxBytes < 0) _approxBytes = 0;
            }
        }

        private static long EstimateSize(PageContent page)
        {
            long size = 64; // base
            foreach (var n in page.Nodes)
            {
                size += n switch
                {
                    TextNode t => t.Text.Length * 2 + 32,
                    ParagraphNode p => 64 + p.Children.Count * 16,
                    HeadingNode h => h.Title.Length * 2 + 64,
                    RubyNode r => (r.BaseText.Length + r.RubyText.Length) * 2 + 32,
                    ImageNode i => i.Data.Length + 64,
                    _ => 32
                };
            }
            return Math.Max(128, size);
        }

        public void Dispose() => Clear();
    }
}


