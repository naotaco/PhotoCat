using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoCat2.ViewModels
{

    public class ImageModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _IsChecked;
        public bool IsChecked
        {
            get => _IsChecked;
            set
            {
                if (_IsChecked != value)
                {
                    _IsChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public ICommand OpenCommand { get; set; }
        public ICommand LoadedCommand { get; set; }
        public Action<BitmapImage> OpenRequested = null;
        public Action LoadStarted = null;
        public Action LoadFinished = null;

        public string FullPath { get; }
        string LoadedPath = "";
        public string Title { get; }

        public BitmapImage Bitmap { get; set; }

        public ImageModel(string path)
        {
            OpenCommand = new RelayCommand(new Action(OpenImage));
            LoadedCommand = new RelayCommand(new Action(ImageLoaded));

            FullPath = path;
            Title = System.IO.Path.GetFileName(path);
        }

        async void OpenImage()
        {
            Debug.WriteLine("Open!");

            LoadStarted?.Invoke();

            if (LoadedPath != FullPath)
            {
                Bitmap = await Task.Run(() =>
                {
                    var bmp = new BitmapImage()
                    {
                        DecodePixelHeight = 200,
                        DecodePixelWidth = 300,
                    };
                    var ms = new MemoryStream();
                    using (var fs = new FileStream(FullPath, FileMode.Open))
                    {
                        fs.CopyTo(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                    }

                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    return bmp;
                });
                LoadedPath = FullPath;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));

            }

            OpenRequested?.Invoke(Bitmap);
            LoadFinished?.Invoke();
        }

        public void ImageLoaded()
        {
            Debug.WriteLine("Image loaded");
        }
    }
}
