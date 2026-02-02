using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Shinobu.Helpers;
using Shinobu.Pages;
using System;
using System.ComponentModel;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;

namespace Shinobu
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

        private bool _isDragOver;
        public bool IsDragOver
        {
            get => _isDragOver;
            set
            {
                if (_isDragOver != value)
                {
                    _isDragOver = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDragOver)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(1200, 1600));

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(titleBar);
            navFrame.Navigate(typeof(LibraryPage));
            navView.SelectedItem = navView.MenuItems[0];
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                navFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.InvokedItemContainer is NavigationViewItem item)
            {
                string? tag = item.Tag as string;
                switch (tag)
                {
                    case "library":
                        navFrame.Navigate(typeof(LibraryPage));
                        break;
                    case "reader":
                        navFrame.Navigate(typeof(ReaderPage));
                        break;
                    case "bookmarks":
                        navFrame.Navigate(typeof(BookmarksPage));
                        break;
                    case "imageocr":
                        navFrame.Navigate(typeof(ImageViewerPage));
                        break;

                }
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (navFrame.CanGoBack)
            {
                navFrame.GoBack();
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (navFrame.Content is ISearchProvider provider)
            {
                provider.OnQuerySubmitted(sender, args);
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (navFrame.Content is ISearchProvider provider)
            {
                provider.OnTextChanged(sender, args);
            }
        }

        public void SelectReaderNavigation()
        {
            var readerItem = navView.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Tag as string == "reader");
            if (readerItem != null)
            {
                navView.SelectedItem = readerItem;
            }
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            IsDragOver = false;
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                LoadingRing.Visibility = Visibility.Visible;
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var storageFile = items[0] as StorageFile;
                    if (storageFile != null)
                    {
                        if (SupportedFileTypes.Extensions.ContainsKey(storageFile.FileType.ToLower()))
                        {
                            var entry = await BookManager.CreateBookAsync(storageFile.Path);
                            if (entry != null)
                            {
                                if (navFrame.Content is LibraryPage)
                                {
                                    navFrame.Navigate(typeof(ReaderPage), entry.Hash);
                                }
                            }
                        }
                    }
                }
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            IsDragOver = true;
        }

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            IsDragOver = false;
        }
    }
}
