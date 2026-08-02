using Shinobu.Helpers.Books;
using System.Collections.Generic;

namespace Shinobu.Helpers.Content
{
    public sealed class PageContent
    {
        public IReadOnlyList<DocumentNode> Nodes { get; init; } = [];
        public BookLocation StartLocation { get; init; } = new();
        public BookLocation? EndLocation { get; init; }
        public bool IsLastPage { get; init; }
    }
}


