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

        MemoryStream PreLoadData;
        public BitmapImage Bitmap { get; set; }

        public State ImageState = State.NotLoaded;

        public enum State
        {
            NotLoaded,
            Loaded,
            Decoded,
        }

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

        /// <summary>
        /// Load data to memory sync
        /// </summary>
        public bool Load()
        {
            Debug.WriteLine("Load requested: " + FullPath);

            if (!PrefetchRequired())
            {
                return false;
            }

            var sw = new Stopwatch();
            sw.Start();

            try
            {
                PreLoadData = LoadData();
            }
            catch (IOException e)
            {
                Debug.WriteLine("Caught IOException: " + e.Message);
                return false;
            }

            ImageState = State.Loaded;
            Debug.WriteLine("Loaded in " + sw.ElapsedMilliseconds + "ms. " + FullPath);
            return true;
        }

        public void Decode()
        {
            Debug.WriteLine("Decode requested: " + FullPath);
            if (ImageState != State.Loaded) { return; }

            var sw = new Stopwatch();
            sw.Start();

            Bitmap = DecodeImage(PreLoadData);

            LoadedPath = FullPath;
            ImageState = State.Decoded;
            Debug.WriteLine("Decoded in " + sw.ElapsedMilliseconds + "ms : " + FullPath);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));

            PrefetchFinished?.Invoke(this);

            // todo: Free memorystream after decode finished.
        }

        private async Task<BitmapImage> _OpenImage(bool is_async)
        {
            if (!PrefetchRequired())
            {
                return Bitmap;
            }

            Debug.WriteLine("Start Loading: " + FullPath);

            try
            {
                if (is_async)
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
            ImageState = State.Decoded;
            Debug.WriteLine("Loaded&Decoded: " + FullPath);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));

            return Bitmap;
        }

        private BitmapImage LoadBitmap()
        {
            var sw = new Stopwatch();
            sw.Start();

            var ms = LoadData();
            ImageState = State.Loaded;

            var loadDuration = sw.ElapsedMilliseconds;

            var bmp = DecodeImage(ms);

            ms.Dispose();

            var totalDuration = sw.ElapsedMilliseconds;

            Debug.WriteLine(string.Format("Loaded. read in {0}ms, {1}ms in total.", loadDuration, totalDuration));

            return bmp;
        }

        private static BitmapImage DecodeImage(MemoryStream ms)
        {
            var bmp = new BitmapImage()
            {
                DecodePixelHeight = 200,
                DecodePixelWidth = 300,
            };
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private MemoryStream LoadData()
        {
            var ms = new MemoryStream();
            using (var fs = new FileStream(FullPath, FileMode.Open))
            {
                fs.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
            }
            return ms;
        }

        public bool PrefetchRequired()
        {
            return LoadedPath != FullPath || ImageState == State.NotLoaded;
        }
    }
}
