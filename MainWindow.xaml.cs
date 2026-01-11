using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shinobu.Helpers;
using Shinobu.Pages;
using System;
using System.Linq;
using Windows.Graphics;

namespace Shinobu
{
    public sealed partial class MainWindow : Window
    {
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
                    case "words":
                        //navFrame.Navigate(typeof(WordsPage));
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
    }
}
