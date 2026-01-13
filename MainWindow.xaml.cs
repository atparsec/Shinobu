using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Shinobu.Helpers;
using Shinobu.Pages;
using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;

namespace Shinobu
{
    public sealed partial class MainWindow : Window
    {
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

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
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var storageFile = items[0] as StorageFile;
                    if (storageFile != null)
                    {
                        if (storageFile.FileType.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                        {
                            string? libraryFolder = _settings.Values.TryGetValue("LibraryFolder", out object? v) ? v as string : null;
                            if (string.IsNullOrEmpty(libraryFolder))
                            {
                                libraryFolder = ApplicationData.Current.LocalFolder.Path;
                            }
                            var libStorage = await StorageFolder.GetFolderFromPathAsync(libraryFolder);
                            var newFile = await storageFile.CopyAsync(libStorage, storageFile.Name, NameCollisionOption.ReplaceExisting);
                            if (navFrame.Content is LibraryPage libraryPage)
                            {
                                navFrame.Navigate(typeof(ReaderPage), newFile.Path);
                            }
                        }

                    }
                }
            }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }
}
