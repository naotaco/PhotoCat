using PhotoCat2.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PhotoCat2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = new MainViewModel();
            InitializeComponent();
        }

        MainViewModel GetVM()
        {
            return DataContext as MainViewModel;
        }

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            var f = GetFirstOrDefaultTargetFile(e);
            if (f != null)
            {
                DndGuide.Visibility = Visibility.Visible;
            }
        }

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            if (DndGuide.Visibility == Visibility.Visible) { DndGuide.Visibility = Visibility.Collapsed; }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            var f = GetFirstOrDefaultTargetFile(e);
            if (f != null)
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private static string GetFirstOrDefaultTargetFile(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                return files?.FirstOrDefault(s => s.EndsWith(".jpg", StringComparison.CurrentCultureIgnoreCase));

            }
            return null;
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            var f = GetFirstOrDefaultTargetFile(e);
            if (f != null)
            {
                Debug.WriteLine("drop: " + f);
                if (DndGuide.Visibility == Visibility.Visible) { DndGuide.Visibility = Visibility.Collapsed; }

                // LoadSingleImage(f, MainImage);

                GetVM().Items.Clear();
                LoadFileList(System.IO.Path.GetDirectoryName(f));
            }
        }

        async Task<long> LoadSingleImage(string path, Image img)
        {
            var loadSw = new Stopwatch();
            loadSw.Start();

            return await Task.Run(() =>
            {
                var bmp = new BitmapImage()
                {
                };
                var ms = new MemoryStream();
                using (var fs = new FileStream(path, FileMode.Open))
                {
                    fs.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                }

                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.DecodePixelHeight = bmp.PixelHeight;
                bmp.DecodePixelWidth = bmp.PixelWidth;
                bmp.EndInit();
                bmp.Freeze();

                Debug.WriteLine("Init in ms: " + loadSw.ElapsedMilliseconds);

                Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    img.Source = bmp;
                    img.Unloaded += delegate
                    {
                        ms.Close();
                        ms.Dispose();
                    };
                    img.Loaded += (e0, e1) =>
                    {
                        MainImageProgress.Visibility = Visibility.Collapsed;
                        Debug.WriteLine("Loaded in ms: " + loadSw.ElapsedMilliseconds);
                    };



                }));
                return loadSw.ElapsedMilliseconds;
            });
        }

        async void LoadFileList(string dir)
        {
            var loadSw = new Stopwatch();

            var files = await Task.Run(() =>
            {
                var dirInfo = new DirectoryInfo(dir);
                var fs = new List<string>();

                var info = dirInfo.GetFiles("*.*");
                foreach (FileInfo f in info)
                {
                    if (f.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        fs.Add(f.FullName);
                    }
                }
                return fs;
            });
            Debug.WriteLine("File list loaded in ms: " + loadSw.ElapsedMilliseconds);

            await Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                GetVM().Items.Clear();
                GetVM().IsLoading = true;
                GetVM().TotalImages = files.Count;
                GetVM().LoadedImagesCount = 0;
            }));


            foreach (var f in files)
            {
                await Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    GetVM().Items.Add(new ImageModel(f)
                    {
                        OpenRequested = (bmp) =>
                        {
                            Dispatcher.BeginInvoke(new ThreadStart(delegate
                            {
                                MainImage.Source = bmp;
                                FitImage();
                            }));
                        },
                        LoadStarted = () =>
                        {
                            Dispatcher.BeginInvoke(new ThreadStart(delegate
                            {
                                MainImageProgress.Visibility = Visibility.Visible;
                            }));
                        },
                        LoadFinished = () =>
                        {
                            Dispatcher.BeginInvoke(new ThreadStart(delegate
                            {
                                MainImageProgress.Visibility = Visibility.Collapsed;
                            }));
                        },
                    });

                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle, null);

            }
            Debug.WriteLine("File list items added in ms: " + loadSw.ElapsedMilliseconds);

        }

        private void FitImage()
        {
            MainImage.Width = ImageGrid.ActualWidth;
            MainImage.Height = ImageGrid.ActualHeight;

            var transforms = new TransformGroup();
            transforms.Children.Add(new ScaleTransform(1, 1));
            transforms.Children.Add(new TranslateTransform(0, 0));
            MainImage.RenderTransform = transforms;
        }

        private void MainImage_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        void dumpSize(FrameworkElement element, string str)
        {
            Debug.WriteLine(str + " Width:" + element.Width);
            Debug.WriteLine(str + " Height:" + element.Height);
            Debug.WriteLine(str + " ActualWidth:" + element.ActualWidth);
            Debug.WriteLine(str + " ActualHeight:" + element.ActualHeight);
        }

        void dumpImageSize(BitmapImage img, string str)
        {
            Debug.WriteLine(str + " PixelWidth: " + img.PixelWidth);
            Debug.WriteLine(str + " PixelHeight\t: " + img.PixelHeight);
        }

        private void MainImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var image = sender as Image;
            var source = image.Source as BitmapImage;
            if (image != null && source != null)
            {
                var RealGridSize = GetElementPixelSize(ImageGrid);
                var pos = e.GetPosition(sender as IInputElement);
                var x = pos.X / image.ActualWidth; // 0-1 pos
                var y = pos.Y / image.ActualHeight;
                var before_w = image.ActualWidth;
                var before_h = image.ActualHeight;

                dumpSize(image, "before");

                var ratio = RealGridSize.Width / image.ActualWidth; // real pixels in 1 virtual pixel. 1.25
                var new_w = source.PixelWidth / ratio; // expected size in virtual pixel
                var new_h = source.PixelHeight / ratio; // expected size in virtual pixel
                Debug.WriteLine("r " + ratio);

                //image.Width = new_w;
                //image.Height = new_h;

                dumpSize(image, "after");

                Debug.WriteLine("w: " + before_w + " " + new_w + " " + x);
                Debug.WriteLine("h: " + before_h + " " + new_h + " " + y);

                var shift_x = (new_w - before_w) / 2 + (before_w * x) - (new_w * x);
                var shift_y = (new_h - before_h) / 2 + (before_h * y) - (new_h * y);

                Debug.WriteLine("shift " + shift_x + " " + shift_y);

                var mag_ratio = new_w / before_w;

                var transforms = new TransformGroup();
                transforms.Children.Add(new ScaleTransform(mag_ratio, mag_ratio));
                transforms.Children.Add(new TranslateTransform(shift_x, shift_y));
                image.RenderTransform = transforms;
            }
        }

        public Size GetElementPixelSize(UIElement element)
        {
            Matrix transformToDevice = new Matrix();
            var source = PresentationSource.FromVisual(element);
            if (source != null)
            {
                transformToDevice = source.CompositionTarget.TransformToDevice;
            }

            if (element.DesiredSize == new Size())
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }

            return (Size)transformToDevice.Transform((Vector)element.DesiredSize);
        }

        private void MainImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            RestoreImage(sender);
        }

        private void RestoreImage(object sender)
        {
            var image = sender as Image;
            if (image != null)
            {
                image.Stretch = Stretch.Uniform;
                FitImage();
            }
        }

        private void MainImage_MouseLeave(object sender, MouseEventArgs e)
        {
            RestoreImage(sender);
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FitImage();
        }


    }
}
