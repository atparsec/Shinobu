using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Shinobu.Helpers;
using Shinobu.Helpers.Books;
using Shinobu.Helpers.Content;
using Shinobu.Helpers.Dictionary;
using Shinobu.Helpers.Reader;
using Shinobu.Helpers.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using WinRT.Interop;

namespace Shinobu.Pages
{
    public sealed partial class LibraryPage : Page
    {
        private ObservableCollection<BookItem> AllBooks { get; } = [];
        private ObservableCollection<BookItem> FavoriteBooks { get; } = [];
        private CancellationTokenSource? _thumbnailLoadCts;

        public LibraryPage()
        {
            InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadBooksAsync();
        }

        private Task LoadBooksAsync()
        {
            _thumbnailLoadCts?.Cancel();
            _thumbnailLoadCts?.Dispose();
            _thumbnailLoadCts = new CancellationTokenSource();

            AllBooks.Clear();
            FavoriteBooks.Clear();

            List<string> favorites = LoadFavorites();
            List<BookItem> thumbnailCandidates = [];
            IEnumerable<string> libraryFiles = LibraryFolderManager.GetSupportedFiles();

            foreach (string filePath in libraryFiles)
            {
                BookItem item = new()
                {
                    FileName = filePath,
                    Path = filePath,
                    Extension = Path.GetExtension(filePath).ToLowerInvariant(),
                    IsFavorite = favorites.Contains(filePath),
                    PreviewImagePath = null
                };

                if (File.Exists(filePath))
                {
                    FileInfo info = new(filePath);
                    item.FileSize = info.Length;
                    item.DateModified = info.LastWriteTime.ToShortDateString();
                }

                item.BackgroundBrush = CreateFallbackBrush(item.FileName);
                thumbnailCandidates.Add(item);

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

            _ = LoadThumbnailsAsync(thumbnailCandidates, _thumbnailLoadCts.Token);
            return Task.CompletedTask;
        }

        public void ReloadLibrary()
        {
            _ = LoadBooksAsync();
        }

        private async Task LoadThumbnailsAsync(IEnumerable<BookItem> items, CancellationToken cancellationToken)
        {
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                ImageSource? thumbnail = await TryLoadThumbnailAsync(item.Path);
                if (thumbnail == null || cancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        item.BackgroundBrush = new ImageBrush
                        {
                            ImageSource = thumbnail,
                            Stretch = Stretch.UniformToFill,
                            Opacity = 1.0
                        };
                    }
                });
            }
        }

        private static LinearGradientBrush CreateFallbackBrush(string fileName)
        {
            string colorHex = UIColorHelper.HashStringToColor(fileName);
            byte r = byte.Parse(colorHex[1..3], NumberStyles.HexNumber);
            byte g = byte.Parse(colorHex[3..5], NumberStyles.HexNumber);
            byte b = byte.Parse(colorHex[5..7], NumberStyles.HexNumber);
            var gradientColor = Windows.UI.Color.FromArgb(255, r, g, b);
            var lighterGradientColor = Windows.UI.Color.FromArgb(255,
                (byte)Math.Min(r + 50, 255),
                (byte)Math.Min(g + 50, 255),
                (byte)Math.Min(b + 50, 255));
            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Color = gradientColor, Offset = 0 },
                    new GradientStop { Color = lighterGradientColor, Offset = 1 },
                }
            };
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
            List<string> favs = AllBooks.Where(b => b.IsFavorite).Select(b => b.Path).ToList();
            string json = JsonSerializer.Serialize(favs);
            ApplicationData.Current.LocalSettings.Values["Favorites"] = json;
        }

        private static async Task<ImageSource?> TryLoadThumbnailAsync(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".pdf" && ext != ".epub")
            {
                return null;
            }

            return await ThumbnailCacheManager.GetThumbnailAsync(filePath);
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

        private void FavoriteButton_Tapped(object sender, TappedRoutedEventArgs e)
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
            e.Handled = true;
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
            if (App.MainWindowInstance != null)
            {
                await LibraryFolderManager.PickAndCopyFilesAsync(App.MainWindowInstance);
                await LoadBooksAsync();
            }
        }
        private async Task ImportFolderAsync()
        {
            if (App.MainWindowInstance != null)
            {
                await LibraryFolderManager.PickAndCopyFolderAsync(App.MainWindowInstance);
                await LoadBooksAsync();
            }
        }
    }

    internal class BookItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string DateModified { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }
        public string PreviewText { get; set; } = string.Empty;
        public string? PreviewImagePath { get; set; }
        public bool ShowInfoText => string.IsNullOrEmpty(PreviewImagePath);
        private Microsoft.UI.Xaml.Media.Brush _backgroundBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        public Microsoft.UI.Xaml.Media.Brush BackgroundBrush
        {
            get => _backgroundBrush;
            set
            {
                if (!ReferenceEquals(_backgroundBrush, value))
                {
                    _backgroundBrush = value;
                    OnPropertyChanged();
                }
            }
        }
        public string Extension { get; set; } = string.Empty;
        public string ExtensionName => SupportedFileTypes.Extensions.TryGetValue(Extension, out string? name) ? name : "Unknown";
        public string BookColor => "#22" + UIColorHelper.HashStringToColor(FileName)[1..];

        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

