using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Shinobu.Helpers
{
    public class ReaderThemeManager
    {
        public List<BookTheme> Themes { get; } = [];

        public async Task LoadAsync()
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync("reader_themes.json");
                string json = await FileIO.ReadTextAsync(file);
                List<BookTheme> themes = JsonConvert.DeserializeObject<List<BookTheme>>(json) ?? [];
                Themes.Clear();
                foreach (BookTheme theme in themes)
                {
                    Themes.Add(theme);
                }
            }
            catch (FileNotFoundException)
            {
                Themes.Add(new BookTheme { Name = "Default", Background = "auto", Foreground = "auto" });
                Themes.Add(new BookTheme { Name = "Light", Background = "#FFF", Foreground = "#000" });
                Themes.Add(new BookTheme { Name = "Dark", Background = "#000", Foreground = "#FFF" });
                Themes.Add(new BookTheme { Name = "Sepia", Background = "#fdf6e3", Foreground = "#5f4b32" });
                await SaveAsync();
            }
        }

        public async Task SaveAsync()
        {
            string json = JsonConvert.SerializeObject(Themes, Formatting.Indented);
            StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync("reader_themes.json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, json);
        }

        public BookTheme? GetTheme(string name)
        {
            return Themes.FirstOrDefault(t => t.Name == name);
        }

        public void AddOrUpdateTheme(BookTheme theme)
        {
            Themes.RemoveAll(t => t.Name == theme.Name);
            Themes.Add(theme);
        }

        public void RemoveTheme(string name)
        {
            Themes.RemoveAll(t => t.Name == name);
        }
    }
}