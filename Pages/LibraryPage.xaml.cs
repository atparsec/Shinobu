using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Shinobu.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using WinRT.Interop;

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
            AllBooks.Clear();
            FavoriteBooks.Clear();

            List<string> favorites = LoadFavorites();

            foreach (var entry in BookManager.GetBooks())
            {
                BookItem item = new()
                {
                    FileName = entry.Title,
                    Path = entry.Hash,
                    Extension = System.IO.Path.GetExtension(entry.OriginalFilePath).ToLower(),
                    IsFavorite = favorites.Contains(entry.Title),
                    PreviewImagePath = entry.PreviewImagePath
                };

                if (File.Exists(entry.OriginalFilePath))
                {
                    FileInfo info = new(entry.OriginalFilePath);
                    item.FileSize = info.Length;
                    item.DateModified = info.LastWriteTime.ToShortDateString();
                }

                // Load preview text
                try
                {
                    var content = await BookManager.LoadBookContentAsync(entry.Hash);
                    item.PreviewText = content.TextContent.Length > 100 ? content.TextContent[..100] + "..." : content.TextContent;
                }
                catch
                {
                    item.PreviewText = "Preview unavailable";
                }

                if (entry.PreviewImagePath != null)
                {
                    item.BackgroundBrush = new Microsoft.UI.Xaml.Media.ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(entry.PreviewImagePath)),
                        Stretch = Stretch.UniformToFill,
                        Opacity = 0.2
                    };
                }
                else
                {
                    string colorHex = UIColorHelper.HashStringToColor(item.FileName);
                    byte r = byte.Parse(colorHex[1..3], NumberStyles.HexNumber);
                    byte g = byte.Parse(colorHex[3..5], NumberStyles.HexNumber);
                    byte b = byte.Parse(colorHex[5..7], NumberStyles.HexNumber);
                    var gradientColor = Windows.UI.Color.FromArgb(255, r, g, b);
                    item.BackgroundBrush = new LinearGradientBrush
                    {
                        StartPoint = new Windows.Foundation.Point(0, 0),
                        EndPoint = new Windows.Foundation.Point(0, 1),
                        GradientStops =
                        {
                            new GradientStop { Color = Windows.UI.Color.FromArgb(255, 238, 238, 238), Offset = 0 }, // #EEEEEE
                            new GradientStop { Color = gradientColor, Offset = 1 }
                        }
                    };
                }

                AllBooks.Add(item);
                if (item.IsFavorite)
                {
                    FavoriteBooks.Add(item);
                }
            }

            BooksGrid.ItemsSource = AllBooks;
            FavoritesGrid.ItemsSource = FavoriteBooks;
            UpdateFavoritesVisibility();
            EmptyLibraryPanel.Visibility = AllBooks.Any() ? Visibility.Collapsed : Visibility.Visible;
            LibraryActionsPanel.Visibility = AllBooks.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateFavoritesVisibility()
        {
            FavoritesSection.Visibility = FavoriteBooks.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private List<string> LoadFavorites()
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            string? json = settings.Values.TryGetValue("Favorites", out object? v) ? v as string : "[]";
            return JsonSerializer.Deserialize<List<string>>(json!) ?? [];
        }

        private void SaveFavorites()
        {
            List<string> favs = AllBooks.Where(b => b.IsFavorite).Select(b => b.FileName).ToList();
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

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            await ImportBooksAsync();
        }

        private async void ImportFolderButton_Click(object sender, RoutedEventArgs e)
        {
            await ImportFolderAsync();
        }

        private async Task ImportBooksAsync()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();

            var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
            InitializeWithWindow.Initialize(picker, hwnd);

            foreach (var ext in SupportedFileTypes.Extensions.Keys)
            {
                picker.FileTypeFilter.Add(ext);
            }

            var files = await picker.PickMultipleFilesAsync();
            if (files?.Count > 0)
            {
                foreach (var file in files)
                {
                    await BookManager.CreateBookAsync(file.Path);
                }
                await LoadBooksAsync();
            }
        }
        private async Task ImportFolderAsync()
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var files = await GetSupportedFilesAsync(folder);
                foreach (var file in files)
                {
                    await BookManager.CreateBookAsync(file.Path);
                }
                await LoadBooksAsync();
            }
        }

        private async Task<List<StorageFile>> GetSupportedFilesAsync(StorageFolder folder)
        {
            var files = new List<StorageFile>();
            var items = await folder.GetItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file && SupportedFileTypes.Extensions.ContainsKey(file.FileType.ToLower()))
                {
                    files.Add(file);
                }
                else if (item is StorageFolder subfolder)
                {
                    files.AddRange(await GetSupportedFilesAsync(subfolder));
                }
            }
            return files;
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
        public string? PreviewImagePath { get; set; }
        public bool ShowInfoText => string.IsNullOrEmpty(PreviewImagePath);
        public Brush BackgroundBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        public string Extension { get; set; } = string.Empty;
        public string ExtensionName => SupportedFileTypes.Extensions.TryGetValue(Extension, out string? name) ? name : "Unknown";
        public string BookColor => "#22" + UIColorHelper.HashStringToColor(FileName)[1..];
    }
}
