using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Shinobu.Helpers
{
    public class BookmarksManager
    {
        private readonly string _filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "bookmarks.json");

        public ObservableCollection<Bookmark> Bookmarks { get; } = [];

        public async Task LoadAsync()
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync("bookmarks.json");
                string json = await FileIO.ReadTextAsync(file);
                List<Bookmark> bookmarks = JsonConvert.DeserializeObject<List<Bookmark>>(json) ?? new List<Bookmark>();
                Bookmarks.Clear();
                foreach (Bookmark bookmark in bookmarks)
                {
                    Bookmarks.Add(bookmark);
                }
            }
            catch (FileNotFoundException)
            {
            }
            return;
        }

        public async Task SaveAsync()
        {
            string json = JsonConvert.SerializeObject(Bookmarks, Formatting.Indented);
            StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync("bookmarks.json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, json);
            return;
        }

        public async Task AddBookmarkAsync(Bookmark bookmark)
        {
            Bookmarks.Add(bookmark);
            await SaveAsync();
            return;
        }

        public async Task RemoveBookmarkAsync(Bookmark bookmark)
        {
            Bookmarks.Remove(bookmark);
            await SaveAsync();
            return;
        }

        public async Task UpdateBookmarkNoteAsync(Bookmark bookmark, string note)
        {
            int index = Bookmarks.IndexOf(bookmark);
            if (index >= 0)
            {
                Bookmarks[index].Note = note;
                await SaveAsync();
            }
            return;
        }

        internal List<Bookmark>? BookmarksSearch(string text)
        {
            return [.. Bookmarks.Where(
                b => (b.Text + b.Note + string.Join(",",b.Tags)+b.FilePath)
                .Contains(text, StringComparison.OrdinalIgnoreCase
                ))];
        }
    }
}