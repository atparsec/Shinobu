using Shinobu.Helpers.Books;
using Shinobu.Helpers.Content;
using Shinobu.Helpers.Dictionary;
using System;
using System.Threading.Tasks;

namespace Shinobu.Helpers.Reader
{
    /// <summary>
    /// Owns reading state for a single book.
    /// Coordinates IBookDocument + ReaderPager + HtmlRenderer.
    /// The UI (ReaderPage) talks only to this.
    /// </summary>
    public sealed class ReaderController : IAsyncDisposable
    {
        private readonly IBookDocument _document;
        private readonly ReaderPager _pager;
        private readonly FuriganaGenerator _furiganaGenerator;

        private BookMetadata? _metadata;

        public string FilePath => _document.FilePath;

        public BookLocation CurrentLocation => _pager.CurrentLocation;

        private bool _hasPrev;
        private bool _hasNext;

        public bool CanGoPrev => _hasPrev;
        public bool CanGoNext => _hasNext;

        public int? TotalPages => _metadata?.TotalPages;

        public ReaderController(IBookDocument document, FuriganaGenerator? furiganaGenerator = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _furiganaGenerator = furiganaGenerator ?? new FuriganaGenerator();
            _pager = new ReaderPager(_document);
        }

        public async Task InitializeAsync(BookLocation? start = null)
        {
            _metadata = await _document.GetMetadataAsync();
            await _pager.InitializeAsync(start);
            await UpdateNavigationStateAsync();
        }

        private async Task UpdateNavigationStateAsync()
        {
            var cur = _pager.Current;
            var loc = cur?.StartLocation ?? CurrentLocation;
            var prev = await _document.GetPreviousLocationAsync(loc);
            var next = await _document.GetNextLocationAsync(loc);
            _hasPrev = prev != null;
            _hasNext = next != null;
        }

        public async Task<string> GetCurrentPageHtmlAsync(
            bool isVertical,
            double fontSize,
            double lineHeight,
            string fontFamily,
            BookTheme theme,
            double margin,
            double webViewWidth,
            double webViewHeight,
            JlptLevel jlptLevel)
        {
            var page = _pager.Current;
            if (page == null)
            {
                page = await _pager.GoToLocationAsync(CurrentLocation);
            }

            if (page == null || page.Nodes.Count == 0)
            {
                return "<html><body><p>No content.</p></body></html>";
            }

            return await HtmlRenderer.RenderPageAsync(
                page,
                _furiganaGenerator,
                jlptLevel,
                isVertical,
                fontSize,
                lineHeight,
                fontFamily,
                theme,
                margin,
                webViewWidth,
                webViewHeight);
        }

        public async Task<string?> GoNextAsync(
            bool isVertical,
            double fontSize,
            double lineHeight,
            string fontFamily,
            BookTheme theme,
            double margin,
            double webViewWidth,
            double webViewHeight,
            JlptLevel jlptLevel)
        {
            var nextPage = await _pager.MoveNextAsync();
            if (nextPage == null) return null;

            await UpdateNavigationStateAsync();
            return await GetCurrentPageHtmlAsync(isVertical, fontSize, lineHeight, fontFamily, theme, margin, webViewWidth, webViewHeight, jlptLevel);
        }

        public async Task<string?> GoPreviousAsync(
            bool isVertical,
            double fontSize,
            double lineHeight,
            string fontFamily,
            BookTheme theme,
            double margin,
            double webViewWidth,
            double webViewHeight,
            JlptLevel jlptLevel)
        {
            var prevPage = await _pager.MovePreviousAsync();
            if (prevPage == null) return null;

            await UpdateNavigationStateAsync();
            return await GetCurrentPageHtmlAsync(isVertical, fontSize, lineHeight, fontFamily, theme, margin, webViewWidth, webViewHeight, jlptLevel);
        }

        public async Task<string?> GoToLocationAsync(
            BookLocation location,
            bool isVertical,
            double fontSize,
            double lineHeight,
            string fontFamily,
            BookTheme theme,
            double margin,
            double webViewWidth,
            double webViewHeight,
            JlptLevel jlptLevel)
        {
            var page = await _pager.GoToLocationAsync(location);
            if (page == null) return null;

            await UpdateNavigationStateAsync();
            return await GetCurrentPageHtmlAsync(isVertical, fontSize, lineHeight, fontFamily, theme, margin, webViewWidth, webViewHeight, jlptLevel);
        }

        public async ValueTask DisposeAsync()
        {
            _pager.Dispose();
            await _document.DisposeAsync();
        }
    }
}


