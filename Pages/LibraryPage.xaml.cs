using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Shinobu.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibraryPage : Page
    {
        private ObservableCollection<BookItem> AllBooks { get; } = new();
        private ObservableCollection<BookItem> FavoriteBooks { get; } = new();

        public LibraryPage()
        {
            InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadBooksAsync();
        }

        private async Task LoadBooksAsync()
        {
            var libraryPath = GetLibraryPath();
            if (!Directory.Exists(libraryPath)) return;

            var files = await Task.Run(() => Directory.GetFiles(libraryPath, "*.txt", SearchOption.TopDirectoryOnly));
            var favorites = LoadFavorites();

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var item = new BookItem
                {
                    FileName = Path.GetFileName(file),
                    FileSize = info.Length,
                    DateModified = info.LastWriteTime.ToShortDateString(),
                    Path = file,
                    IsFavorite = favorites.Contains(file)
                };
                AllBooks.Add(item);
                if (item.IsFavorite)
                    FavoriteBooks.Add(item);
            }

            BooksGrid.ItemsSource = AllBooks;
            FavoritesGrid.ItemsSource = FavoriteBooks;
            UpdateFavoritesVisibility();
        }

        private void UpdateFavoritesVisibility()
        {
            FavoritesSection.Visibility = FavoriteBooks.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetLibraryPath()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var path = settings.Values.TryGetValue("LibraryFolder", out var v) ? v as string : null;
            return string.IsNullOrEmpty(path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") : path;
        }

        private List<string> LoadFavorites()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var json = settings.Values.TryGetValue("Favorites", out var v) ? v as string : "[]";
            return JsonSerializer.Deserialize<List<string>>(json!) ?? [];
        }

        private void SaveFavorites()
        {
            var favs = AllBooks.Where(b => b.IsFavorite).Select(b => b.Path).ToList();
            var json = JsonSerializer.Serialize(favs);
            ApplicationData.Current.LocalSettings.Values["Favorites"] = json;
        }

        private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.FindName("FavoriteButton") is Button btn)
            {
                btn.Visibility = Visibility.Visible;
            }
        }

        private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.FindName("FavoriteButton") is Button btn)
            {
                btn.Visibility = Visibility.Collapsed;
            }
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BookItem item)
            {
                item.IsFavorite = !item.IsFavorite;
                if (item.IsFavorite)
                {
                    FavoriteBooks.Add(item);
                }
                else
                {
                    FavoriteBooks.Remove(item);
                }
                UpdateFavoritesVisibility();
                SaveFavorites();
            }
        }
    }
}
