using Windows.Storage;

namespace Shinobu.Helpers
{
    public static class ReaderSessionManager
    {
        private const string FilePathKey = "ReaderSessionFilePath";
        private const string CurrentPageKey = "ReaderSessionCurrentPage";

        public static void SaveSession(string filePath, int currentPage)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[FilePathKey] = filePath;
            settings.Values[CurrentPageKey] = currentPage;
        }

        public static (string? FilePath, int PageNumber) GetSession()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var filePath = settings.Values.TryGetValue(FilePathKey, out var fp) ? fp as string : null;
            var pageNumber = settings.Values.TryGetValue(CurrentPageKey, out var pn) && pn is int pni ? pni : 0;
            return (filePath, pageNumber);
        }

        public static void ClearSession()
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values.Remove(FilePathKey);
            settings.Values.Remove(CurrentPageKey);
        }
    }
}
