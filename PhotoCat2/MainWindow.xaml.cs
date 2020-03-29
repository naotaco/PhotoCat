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

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            var f = GetFirstOrDefaultTargetFile(e);
            if (f != null)
            {
                Debug.WriteLine("drop: " + f);
                if (DndGuide.Visibility == Visibility.Visible) { DndGuide.Visibility = Visibility.Collapsed; }
                MainImageProgress.Visibility = Visibility.Visible;
                var loaded = await LoadSingleImage(f, MainImage);
                LoadFileList(System.IO.Path.GetDirectoryName(f));
            }
        }

        async Task<bool> LoadSingleImage(string path, Image img)
        {
            var load0 = new Stopwatch();
            load0.Start();

            return await Task.Run(() =>
            {
                var bmp = new BitmapImage();
                var fs = new FileStream(path, FileMode.Open);

                bmp.BeginInit();
                bmp.StreamSource = fs;
                bmp.EndInit();
                bmp.Freeze();

                Debug.WriteLine("Init in ms: " + load0.ElapsedMilliseconds);

                Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    img.Source = bmp;
                    img.Unloaded += delegate
                    {
                        fs.Close();
                        fs.Dispose();
                    };
                    Debug.WriteLine("Loaded in ms: " + load0.ElapsedMilliseconds);
                    MainImageProgress.Visibility = Visibility.Collapsed;
                }));
                return true;
            });
        }

        async void LoadFileList(string dir)
        {
            //   var files = System.IO.File
        }

    }
}
