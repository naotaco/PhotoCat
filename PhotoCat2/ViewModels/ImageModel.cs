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

        public ICommand OpenCommand { get; set; }
        public ICommand LoadedCommand { get; set; }
        public Action<BitmapImage> OpenRequested = null;
        public Action LoadStarted = null;
        public Action<ImageModel> LoadFinished = null;
        public Action<ImageModel> PrefetchFinished = null;

        public string FullPath { get; }
        string LoadedPath = "";
        public string Title { get; }
        public string Date { get; }

        public BitmapImage Bitmap { get; set; }

        public ImageModel(string path)
        {
            OpenCommand = new RelayCommand(new Action(OpenImage));
            // LoadedCommand = new RelayCommand(new Action(Loaded));

            FullPath = path;
            Title = Path.GetFileName(path);
            var created = File.GetCreationTime(path);
            Date = created.ToString();
        }

        async void OpenImage()
        {

            Debug.WriteLine("Open!");

            LoadStarted?.Invoke();

            var bitmap = await _OpenImage(true);

            OpenRequested?.Invoke(bitmap);
            LoadFinished?.Invoke(this);
        }

        public async void StartPrefetch()
        {
            var _bitmap = await _OpenImage(false);
            PrefetchFinished?.Invoke(this);
        }

        private async Task<BitmapImage> _OpenImage(bool foreground)
        {
            if (!PrefetchRequired())
            {
                return Bitmap;
            }

            Debug.WriteLine("Start Loading: " + FullPath);

            try
            {
                if (foreground)
                {
                    Bitmap = await Task.Run(() =>
                    {
                        return LoadBitmap();
                    });
                }
                else
                {
                    Bitmap = LoadBitmap();
                }
            }
            catch (IOException e)
            {
                Debug.WriteLine("Caught IOExeption on opening image. " + e.Message);
                return null;
            }

            LoadedPath = FullPath;
            Debug.WriteLine("Loaded: " + FullPath);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));

            return Bitmap;
        }

        private BitmapImage LoadBitmap()
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
        }

        public bool PrefetchRequired()
        {
            return LoadedPath != FullPath;
        }
    }
}
