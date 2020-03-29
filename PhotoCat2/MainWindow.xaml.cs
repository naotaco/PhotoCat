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
                    fs.Add(f.FullName);
                }
                return fs;
            });
            Debug.WriteLine("File list loaded in ms: " + loadSw.ElapsedMilliseconds);

            await Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                foreach (var f in files)
                {
                    GetVM().Items.Add(new ImageModel(f)
                    {
                        OpenRequested = (bmp) =>
                        {
                            Dispatcher.BeginInvoke(new ThreadStart(delegate
                            {
                                MainImage.Source = bmp;
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
                }
                Debug.WriteLine("File list items added in ms: " + loadSw.ElapsedMilliseconds);
            }), System.Windows.Threading.DispatcherPriority.Background, null);
        }

        private void MainImage_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void MainImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var image = sender as Image;
            var source = image.Source as BitmapImage;
            if (image != null && source != null)
            {
                image.Stretch = Stretch.None;
                Canvas.SetLeft(image, source.Width / 2);
                Canvas.SetTop(image, source.Height / 2);
            }
        }

        private void MainImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                image.Stretch = Stretch.Uniform;
            }
        }
    }
}
