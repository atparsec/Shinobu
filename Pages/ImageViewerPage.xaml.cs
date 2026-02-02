using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Shinobu.Pages
{
    public sealed partial class ImageViewerPage : Page
    {
        private List<SoftwareBitmap> _images = [];
        private int _currentIndex = 0;
        private string _ocrText = "";
        private bool _invert = false;
        private bool _isDragging = false;
        private Point _lastPoint;

        public ImageViewerPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string param)
            {
                var file = await StorageFile.GetFileFromPathAsync(param);
                if (file != null)
                {
                    await LoadImages([file]);
                }
            }
        }

        private async void OpenImageButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");

            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                await LoadImages(files.ToList());
            }
        }

        private async void PasteFromClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Bitmap))
            {
                var bitmap = await dataPackageView.GetBitmapAsync();
                var softwareBitmap = await LoadBitmapFromRandomAccessStream(bitmap);
                await LoadImages([softwareBitmap]);
            }
        }

        private async void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                await DisplayCurrentImage();
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _images.Count - 1)
            {
                _currentIndex++;
                await DisplayCurrentImage();
            }
        }

        private async void ImageScrollViewer_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(ImageScrollViewer).Properties.MouseWheelDelta;
            var centerPoint = e.GetCurrentPoint(ImageScrollViewer).Position;
            var factor = delta > 0 ? 1.1f : 1 / 1.1f;
            var newZoom = ImageScrollViewer.ZoomFactor * factor;
            ZoomTo(newZoom, centerPoint);
            e.Handled = true;
        }

        private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (ImageScrollViewer.ZoomFactor != (float)ZoomSlider.Value)
            {
                ZoomTo((float)ZoomSlider.Value);
            }
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            var newZoom = ImageScrollViewer.ZoomFactor * 1.1f;
            ZoomTo(newZoom);
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            var newZoom = ImageScrollViewer.ZoomFactor / 1.1f;
            ZoomTo(newZoom);
        }

        private void InvertButton_Click(object sender, RoutedEventArgs e)
        {
            _invert = !_invert;
        }

        private void CopyTextButton_Click(object sender, RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_ocrText);
            Clipboard.SetContent(dataPackage);
        }

        private async void SaveAnnotatedButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeChoices.Add("JPEG Files", [".jpg",".jpeg"]);
            picker.FileTypeChoices.Add("PNG Files", [".png"]);
            picker.SuggestedFileName = "AnnotatedImage";
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await SaveBitmapToFile(_images[_currentIndex], file);
            }
        }
        private async Task LoadImages(List<StorageFile> files)
        {
            _images.Clear();
            foreach (var file in files)
            {
                var bitmap = await LoadBitmapFromFile(file);
                _images.Add(bitmap);
            }
            _currentIndex = 0;
            await DisplayCurrentImage();
            ShowViewer();
        }

        private async Task LoadImages(List<SoftwareBitmap> bitmaps)
        {
            _images = bitmaps;
            _currentIndex = 0;
            await DisplayCurrentImage();
            ShowViewer();
        }

        private async Task<SoftwareBitmap> LoadBitmapFromFile(StorageFile file)
        {
            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();
            return SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        private async Task<SoftwareBitmap> LoadBitmapFromRandomAccessStream(RandomAccessStreamReference streamRef)
        {
            using var stream = await streamRef.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();
            return SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        private async Task DisplayCurrentImage()
        {
            if (_images.Count != 0)
            {
                var bitmapSource = new SoftwareBitmapSource();
                await bitmapSource.SetBitmapAsync(_images[_currentIndex]);
                ImageControl.Source = bitmapSource;
                _ocrText = await PerformOCR(_images[_currentIndex]);
                OcrTextBlock.Text = _ocrText;
            }
            UpdateNavigationButtons();
            var pageWidth = RootGrid.ActualWidth;
            if (pageWidth > 0 && _images.Count > 0 && _images[_currentIndex].PixelWidth > 0)
            {
                var zoom = 0.8 * pageWidth / _images[_currentIndex].PixelWidth;
                zoom = Math.Clamp(zoom, 0.1, 5.0);
                ImageScrollViewer.ChangeView(null, null, (float)zoom);
                ZoomSlider.Value = zoom;
            }
        }

        private void UpdateNavigationButtons()
        {
            PrevButton.Visibility = _images.Count > 1 && _currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Visibility = _images.Count > 1 && _currentIndex < _images.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowViewer()
        {
            DefaultPanel.Visibility = Visibility.Collapsed;
            ViewerGrid.Visibility = Visibility.Visible;
        }

        private async Task<string> PerformOCR(SoftwareBitmap bitmap)
        {
            // TODO
            await Task.Delay(100);
            return "Text";
        }

        private async Task SaveBitmapToFile(SoftwareBitmap bitmap, StorageFile file)
        {
            using var transactedWrite = await file.OpenTransactedWriteAsync();
            var stream = transactedWrite.Stream;
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
            await transactedWrite.CommitAsync();
        }

        private void ZoomTo(float newZoom, Windows.Foundation.Point? centerPoint = null)
        {
            if (!centerPoint.HasValue)
            {
                centerPoint = new Windows.Foundation.Point(ImageScrollViewer.ActualWidth / 2, ImageScrollViewer.ActualHeight / 2);
            }
            var oldZoom = ImageScrollViewer.ZoomFactor;
            var x = centerPoint.Value.X;
            var y = centerPoint.Value.Y;
            var newHorizontalOffset = (ImageScrollViewer.HorizontalOffset + x) / oldZoom * newZoom - x;
            var newVerticalOffset = (ImageScrollViewer.VerticalOffset + y) / oldZoom * newZoom - y;
            newZoom = Math.Clamp(newZoom, 0.1f, 5f);
            ImageScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, newZoom);
            ZoomSlider.Value = newZoom;
        }

        private void ImageScrollViewer_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse && e.GetCurrentPoint(ImageScrollViewer).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _lastPoint = e.GetCurrentPoint(ImageScrollViewer).Position;
                ImageScrollViewer.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void ImageScrollViewer_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetCurrentPoint(ImageScrollViewer).Position;
                var deltaX = _lastPoint.X - currentPoint.X;
                var deltaY = _lastPoint.Y - currentPoint.Y;
                ImageScrollViewer.ChangeView(ImageScrollViewer.HorizontalOffset + deltaX, ImageScrollViewer.VerticalOffset + deltaY, null);
                _lastPoint = currentPoint;
                e.Handled = true;
            }
        }

        private void ImageScrollViewer_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ImageScrollViewer.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }
    }
}
