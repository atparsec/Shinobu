using Windows.Storage;

namespace Shinobu.Helpers
{
    public static class ReaderSessionManager
    {
        private const string BookHashKey = "ReaderSessionBookHash";
        private const string CurrentPageKey = "ReaderSessionCurrentPage";

        public static void SaveSession(string bookHash, int currentPage)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[BookHashKey] = bookHash;
            settings.Values[CurrentPageKey] = currentPage;
        }

        public static (string? BookHash, int PageNumber) GetSession()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var bookHash = settings.Values.TryGetValue(BookHashKey, out var bh) ? bh as string : null;
            var pageNumber = settings.Values.TryGetValue(CurrentPageKey, out var pn) && pn is int pni ? pni : 0;
            return (bookHash, pageNumber);
        }

        public static void ClearSession()
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values.Remove(BookHashKey);
            settings.Values.Remove(CurrentPageKey);
        }
    }
}
