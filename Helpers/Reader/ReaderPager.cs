using Shinobu.Helpers.Books;
using Shinobu.Helpers.Content;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shinobu.Helpers.Reader
{
    public sealed class ReaderPager : IDisposable
    {
        private readonly IBookDocument _document;
        private readonly PageCache _cache;

        public PageContent? Previous { get; private set; }
        public PageContent? Current { get; private set; }
        public PageContent? Next { get; private set; }

        public PageContent? CurrentPage => Current;

        public BookLocation CurrentLocation { get; private set; } = new();

        private CancellationTokenSource? _prefetchCts;

        public ReaderPager(IBookDocument document, PageCache? cache = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _cache = cache ?? new PageCache();
        }

        public async Task InitializeAsync(BookLocation? startLocation = null)
        {
            CancelPrefetch();

            var loc = startLocation ?? new BookLocation();
            Current = await LoadPageWithCacheAsync(loc);
            CurrentLocation = Current?.StartLocation ?? loc;

            _ = PrefetchNeighborsAsync();
        }

        public async Task<PageContent?> MoveNextAsync()
        {
            if (Current == null) return null;

            CancelPrefetch();

            var nextLoc = await _document.GetNextLocationAsync(Current.StartLocation);
            if (nextLoc is null)
            {
                _ = PrefetchNeighborsAsync();
                return Current;
            }

            Previous = Current;
            Current = await LoadPageWithCacheAsync(nextLoc);
            Next = null;

            CurrentLocation = Current.StartLocation;

            _ = PrefetchNeighborsAsync();
            return Current;
        }

        public async Task<PageContent?> MovePreviousAsync()
        {
            if (Current == null) return null;

            CancelPrefetch();

            var prevLoc = await _document.GetPreviousLocationAsync(Current.StartLocation);
            if (prevLoc is null)
            {
                _ = PrefetchNeighborsAsync();
                return Current;
            }

            Next = Current;
            Current = await LoadPageWithCacheAsync(prevLoc);
            Previous = null;

            CurrentLocation = Current.StartLocation;

            _ = PrefetchNeighborsAsync();
            return Current;
        }

        public async Task<PageContent?> GoToLocationAsync(BookLocation location)
        {
            CancelPrefetch();

            // Try cache first
            if (_cache.TryGet(location, out var cached) && cached != null)
            {
                Previous = null;
                Current = cached;
                Next = null;
                CurrentLocation = cached.StartLocation;
                _ = PrefetchNeighborsAsync();
                return Current;
            }

            Current = await LoadPageWithCacheAsync(location);
            Previous = null;
            Next = null;
            CurrentLocation = Current?.StartLocation ?? location;

            _ = PrefetchNeighborsAsync();
            return Current;
        }

        private async Task<PageContent> LoadPageWithCacheAsync(BookLocation location)
        {
            if (_cache.TryGet(location, out var cached) && cached != null)
            {
                return cached;
            }

            var page = await _document.LoadPageAsync(location);
            _cache.Add(page.StartLocation, page);
            return page;
        }

        private async Task PrefetchNeighborsAsync()
        {
            CancelPrefetch();
            _prefetchCts = new CancellationTokenSource();
            var token = _prefetchCts.Token;

            try
            {
                if (Current != null)
                {
                    var nextLoc = await _document.GetNextLocationAsync(Current.StartLocation);
                    if (nextLoc is not null && !token.IsCancellationRequested)
                    {
                        if (!_cache.TryGet(nextLoc, out _))
                        {
                            var nextPage = await _document.LoadPageAsync(nextLoc);
                            if (!token.IsCancellationRequested)
                            {
                                _cache.Add(nextPage.StartLocation, nextPage);
                                Next = nextPage;
                            }
                        }
                    }

                    var prevLoc = await _document.GetPreviousLocationAsync(Current.StartLocation);
                    if (prevLoc is not null && !token.IsCancellationRequested)
                    {
                        if (!_cache.TryGet(prevLoc, out _))
                        {
                            var prevPage = await _document.LoadPageAsync(prevLoc);
                            if (!token.IsCancellationRequested)
                            {
                                _cache.Add(prevPage.StartLocation, prevPage);
                                Previous = prevPage;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch
            {
                // idk
            }
        }

        private void CancelPrefetch()
        {
            try { _prefetchCts?.Cancel(); } catch { }
            _prefetchCts?.Dispose();
            _prefetchCts = null;
        }

        public void Dispose()
        {
            CancelPrefetch();
            _cache?.Dispose();
        }
    }
}


