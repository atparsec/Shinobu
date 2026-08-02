using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Shinobu.Helpers.Books;
using Shinobu.Helpers.Content;
using Shinobu.Helpers.Dictionary;
using Shinobu.Helpers.Reader;
using Shinobu.Helpers.Services;
using Shinobu.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tesseract;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Shinobu.Pages
{
    public sealed partial class ImageViewerPage : Microsoft.UI.Xaml.Controls.Page
    {
        private List<SoftwareBitmap> _images = [];
        private int _currentIndex = 0;
        private string _ocrText = "";
        private bool _invert = false;
        private bool _isDragging = false;
        private Windows.Foundation.Point _lastPoint;
        private List<RecognizedLine> _recognizedLines = [];
        private readonly FuriganaGenerator fg = new();

        public ImageViewerPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string param)
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(param);
                if (file != null)
                {
                    await LoadImages([file]);
                }
            }
            if (App.MainWindowInstance is MainWindow mainWindow)
            {
                mainWindow.SelectNavigation("imageocr");
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
            IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                await LoadImages(files.ToList());
            }
        }

        private async void PasteFromClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackageView dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Bitmap))
            {
                RandomAccessStreamReference bitmap = await dataPackageView.GetBitmapAsync();
                SoftwareBitmap softwareBitmap = await LoadBitmapFromRandomAccessStream(bitmap);
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
            DrawTextOverlays();
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
            picker.FileTypeChoices.Add("JPEG Files", [".jpg", ".jpeg"]);
            picker.FileTypeChoices.Add("PNG Files", [".png"]);
            picker.SuggestedFileName = "AnnotatedImage";
            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await SaveBitmapToFile(_images[_currentIndex], file);
            }
        }

        private async void ShowFuriganaButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowFuriganaDialog();
        }

        private async Task LoadImages(List<StorageFile> files)
        {
            _images.Clear();
            foreach (StorageFile file in files)
            {
                SoftwareBitmap bitmap = await LoadBitmapFromFile(file);
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
            using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync();
            return SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        private async Task<SoftwareBitmap> LoadBitmapFromRandomAccessStream(RandomAccessStreamReference streamRef)
        {
            using IRandomAccessStreamWithContentType stream = await streamRef.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync();
            return SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        private async Task DisplayCurrentImage()
        {
            if (_images.Count != 0)
            {
                var bitmapSource = new SoftwareBitmapSource();
                await bitmapSource.SetBitmapAsync(_images[_currentIndex]);
                ImageControl.Source = bitmapSource;
                await PerformOcrAsync(_images[_currentIndex]);
                DrawTextOverlays();
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
            UpdateNavigationButtons();
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

        private async Task ShowFuriganaDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Annotated Text"
            };
            var webView = new WebView2
            {
                Width = 600,
                Height = 400,
                DefaultBackgroundColor = Microsoft.UI.Colors.Transparent,
            };

            string genOriginalText = _ocrText.Replace(" ", "");
            string annoText = await fg.GenerateHtmlFuriganaAsync(genOriginalText, JlptLevel.N5);
            string bodyStyle = $@"color: white; font-size: 30 px; line-height: 60 px; padding: 20px; background: transparent; max-width: 80vw; overflow-wrap: break-word;";

            string html = $@"
                <html>
                    <head>
                        <style>
                        body {{ {bodyStyle} }}
                        </style>
                    </head>
                    <body>
                       {annoText}
                    </body>
                </html>
            ";
            await webView.EnsureCoreWebView2Async();
            webView.NavigateToString(html);
            dialog.Content = webView;
            dialog.CloseButtonText = "Close";
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }

        private async Task SaveBitmapToFile(SoftwareBitmap bitmap, StorageFile file)
        {
            using StorageStreamTransaction transactedWrite = await file.OpenTransactedWriteAsync();
            IRandomAccessStream stream = transactedWrite.Stream;
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
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
            var newHorizontalOffset = ((ImageScrollViewer.HorizontalOffset + x) / oldZoom * newZoom) - x;
            var newVerticalOffset = ((ImageScrollViewer.VerticalOffset + y) / oldZoom * newZoom) - y;
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

        private async Task PerformOcrAsync(SoftwareBitmap softwareBitmap)
        {
            Debug.WriteLine("Starting Tesseract OCR...");

            await Task.Run(() =>
            {
                try
                {
                    using var bitmap = SoftwareBitmapToBitmap(softwareBitmap);
                    using var pix = PixConverterFromBitmap(bitmap);

                    string tessDataPath = AppDomain.CurrentDomain.BaseDirectory;

                    using var engine = new TesseractEngine(tessDataPath, "jpn", EngineMode.LstmOnly);

                    using var page = engine.Process(pix, PageSegMode.Auto);

                    _recognizedLines.Clear();

                    using (var iter = page.GetIterator())
                    {
                        iter.Begin();
                        do
                        {
                            string lineText = iter.GetText(PageIteratorLevel.TextLine);
                            if (!string.IsNullOrWhiteSpace(lineText))
                            {
                                var line = new RecognizedLine { Text = lineText.Trim(), Words = [] };

                                bool moreWords = true;
                                while (moreWords)
                                {
                                    string wordText = iter.GetText(PageIteratorLevel.Word);
                                    if (!string.IsNullOrWhiteSpace(wordText))
                                    {
                                        if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
                                        {
                                            line.Words.Add(new RecognizedWord
                                            {
                                                Text = wordText,
                                                BoundingBox = new System.Drawing.Rectangle(rect.X1, rect.Y1, rect.Width, rect.Height)
                                            });
                                        }
                                    }
                                    moreWords = iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word);
                                }

                                _recognizedLines.Add(line);
                            }

                        } while (iter.Next(PageIteratorLevel.TextLine));
                    }

                    _ocrText = page.GetText();
                    Debug.WriteLine("Tesseract extracted: " + _ocrText);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Tesseract failed: {ex.Message}");
                    _ocrText = $"OCR failed: {ex.Message}";
                    _recognizedLines.Clear();
                }
            });
        }

        private void DrawTextOverlays()
        {
            TextOverlayCanvas.Children.Clear();

            if (!_recognizedLines.Any() || _images == null || _currentIndex >= _images.Count)
                return;

            var currentImage = _images[_currentIndex];
            if (currentImage == null || ImageControl.ActualWidth <= 0 || ImageControl.ActualHeight <= 0 || currentImage.PixelWidth <= 0 || currentImage.PixelHeight <= 0)
                return;

            double scale = Math.Min(ImageControl.ActualWidth / (double)currentImage.PixelWidth, ImageControl.ActualHeight / (double)currentImage.PixelHeight);

            double renderedWidth = currentImage.PixelWidth * scale;
            double renderedHeight = currentImage.PixelHeight * scale;
            double imageLeft = (ImageContainer.ActualWidth - renderedWidth) / 2;
            double imageTop = (ImageContainer.ActualHeight - renderedHeight) / 2;

            foreach (var line in _recognizedLines)
            {
                if (line.Words.Count == 0)
                    continue;

                var minX = line.Words.Min(w => w.BoundingBox.Left);
                var minY = line.Words.Min(w => w.BoundingBox.Top);
                var maxX = line.Words.Max(w => w.BoundingBox.Right);
                var maxY = line.Words.Max(w => w.BoundingBox.Bottom);
                var lineBounds = new Rectangle(minX, minY, maxX - minX, maxY - minY);

                if (lineBounds.Width <= 0 || lineBounds.Height <= 0)
                    continue;

                double left = imageLeft + lineBounds.Left * scale;
                double top = imageTop + lineBounds.Top * scale;
                double width = lineBounds.Width * scale;
                double height = lineBounds.Height * scale;

                var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = width,
                    Height = height,
                    Stroke = new SolidColorBrush( Microsoft.UI.Colors.LightBlue ),
                    StrokeThickness = 2.0,
                    Fill = new SolidColorBrush( Microsoft.UI.Colors.Black) { Opacity = 0.5 }
                };

                var textBlock = new TextBlock
                {
                    Text = line.Text,
                    Width = width,
                    Height = height,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    FontSize = height * 0.53,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                };

                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top);

                TextOverlayCanvas.Children.Add(rect);
                TextOverlayCanvas.Children.Add(textBlock);
            }
            TextOverlayCanvas.Visibility = Visibility.Visible;
        }

        private Bitmap SoftwareBitmapToBitmap(SoftwareBitmap softwareBitmap)
        {
            SoftwareBitmap converted = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            using var stream = new InMemoryRandomAccessStream();
            var encoderTask = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoderTask.AsTask().Wait();
            var encoder = encoderTask.GetAwaiter().GetResult();

            encoder.SetSoftwareBitmap(converted);
            encoder.FlushAsync().AsTask().Wait();

            stream.Seek(0);
            using var memoryStream = new MemoryStream();
            stream.AsStreamForRead().CopyTo(memoryStream);
            memoryStream.Position = 0;

            return new Bitmap(memoryStream);
        }

        private static Pix PixConverterFromBitmap(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                return Pix.LoadFromMemory(ms.ToArray());
            }
        }

        public class RecognizedLine
        {
            public string Text { get; set; } = "";
            public List<RecognizedWord> Words { get; set; } = [];
        }

        public class RecognizedWord
        {
            public string Text { get; set; } = "";
            public Rectangle BoundingBox { get; set; }
        }
    }
}

