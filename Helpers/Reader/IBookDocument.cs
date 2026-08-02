using Shinobu.Helpers.Books;
using Shinobu.Helpers.Content;
using System;
using System.Threading.Tasks;

namespace Shinobu.Helpers.Reader
{
    /// <summary>
    /// Streaming document abstraction. Implementations load/parse/annotate only on demand.
    /// The reader must depend only on this interface.
    /// </summary>
    public interface IBookDocument : IAsyncDisposable
    {
        string FilePath { get; }

        /// <summary>
        /// Load a page starting at or near the given location. The document decides granularity.
        /// </summary>
        ValueTask<PageContent> LoadPageAsync(BookLocation location);

        ValueTask<BookLocation?> GetNextLocationAsync(BookLocation current);
        ValueTask<BookLocation?> GetPreviousLocationAsync(BookLocation current);

        ValueTask<BookMetadata> GetMetadataAsync();
    }
}


