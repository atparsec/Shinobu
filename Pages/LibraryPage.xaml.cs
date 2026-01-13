using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Shinobu.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace Shinobu.Pages
{
    public sealed partial class LibraryPage : Page
    {
        private ObservableCollection<BookItem> AllBooks { get; } = [];
        private ObservableCollection<BookItem> FavoriteBooks { get; } = [];

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
            string libraryPath = GetLibraryPath();
            if (!Directory.Exists(libraryPath))
            {
                return;
            }

            string[] files = await Task.Run(() => Directory.GetFiles(libraryPath, "*.txt", SearchOption.TopDirectoryOnly));
            List<string> favorites = LoadFavorites();

            foreach (string? file in files)
            {
                FileInfo info = new(file);
                BookItem item = new()
                {
                    FileName = Path.GetFileName(file),
                    FileSize = info.Length,
                    DateModified = info.LastWriteTime.ToShortDateString(),
                    Path = file,
                    IsFavorite = favorites.Contains(file)
                };
                AllBooks.Add(item);
                if (item.IsFavorite)
                {
                    FavoriteBooks.Add(item);
                }
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
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            string? path = settings.Values.TryGetValue("LibraryFolder", out object? v) ? v as string : null;
            return string.IsNullOrEmpty(path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") : path;
        }

        private List<string> LoadFavorites()
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            string? json = settings.Values.TryGetValue("Favorites", out object? v) ? v as string : "[]";
            return JsonSerializer.Deserialize<List<string>>(json!) ?? [];
        }

        private void SaveFavorites()
        {
            List<string> favs = AllBooks.Where(b => b.IsFavorite).Select(b => b.Path).ToList();
            string json = JsonSerializer.Serialize(favs);
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
                    _ = FavoriteBooks.Remove(item);
                }
                UpdateFavoritesVisibility();
                SaveFavorites();
            }
        }

        private void BookCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BookItem item)
            {
                _ = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", fe);
                _ = Frame.Navigate(typeof(ReaderPage), item.Path, new SuppressNavigationTransitionInfo());
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            AllBooks.Clear();
            FavoriteBooks.Clear();
            _ = LoadBooksAsync();
        }

    }

    internal class BookItem
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string DateModified { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
        public string PreviewText { get; set; } = string.Empty;

        public string FileNameStripped => System.IO.Path.GetFileNameWithoutExtension(FileName);

        public string BookColor => "#22" + UIColorHelper.HashStringToColor(FileName)[1..];
    }
}
