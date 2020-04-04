using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        public Action<ImageModel> LoadStarted = null;
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
            Loading,
            Loaded,
            Decoding,
            Decoded,
        }

        void SetState(State s)
        {
            lock (this)
            {
                ImageState = s;
            }
        }

        bool TransitState(State current, State next)
        {
            lock (this)
            {
                if (current == ImageState)
                {
                    ImageState = next;
                    return true;
                }
                return false;
            }
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
            LoadStarted?.Invoke(this);

            if (TransitState(State.NotLoaded, State.Loading))
            {
                Bitmap = await _OpenImage(true);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));
            }
            else if (TransitState(State.Loaded, State.Loading))
            {
                Bitmap = await _OpenImage(true);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));
            }
            else if (TransitState(State.Decoded, State.Loading))
            {
                // In case it's already decoded state, check image data.
                if (Bitmap == null || Bitmap.PixelWidth == 0 || Bitmap.PixelHeight == 0)
                {
                    // Maybe previous loading failed.
                    Debug.WriteLine("Reload image: " + FullPath);
                    Bitmap = await _OpenImage(true);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));
                }
                else
                {
                    Debug.WriteLine("Not to open. in " + ImageState);
                }
            }
            OpenRequested?.Invoke(Bitmap);
            LoadFinished?.Invoke(this);
            TransitState(State.Loading, State.Decoded);
        }

        public void Clear()
        {
            if (ImageState == State.NotLoaded)
            {
                return;
            }

            Debug.WriteLine("Clear:" + FullPath);
            lock (this)
            {
                ImageState = State.NotLoaded;
                LoadedPath = "";

                Bitmap = null;
                PreLoadData?.Dispose();
                PreLoadData = null;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));
        }

        public async Task<bool> Load(CancellationToken ct)
        {
            Debug.WriteLine("Load requested: " + FullPath);

            if (!TransitState(State.NotLoaded, State.Loading))
            {
                return false;
            }

            var sw = new Stopwatch();
            sw.Start();

            try
            {
                PreLoadData = await LoadDataAsync(ct);
            }
            catch (IOException e)
            {
                Debug.WriteLine("Caught IOException: " + e.Message);
                TransitState(State.Loading, State.NotLoaded);
                return false;
            }

            Debug.WriteLine("Loaded in " + sw.ElapsedMilliseconds + "ms. " + FullPath);
            TransitState(State.Loading, State.Loaded);
            return true;
        }

        public void Decode()
        {
            Debug.WriteLine("Decode requested: " + FullPath);
            if (!TransitState(State.Loaded, State.Decoding))
            {
                Debug.WriteLine("Maybe failed to load : " + FullPath);
                PrefetchFinished?.Invoke(this);

                TransitState(State.Decoding, State.NotLoaded);
                return;
            }

            if (PreLoadData == null)
            {
                Debug.WriteLine("Maybe failed to load : " + FullPath);
                PrefetchFinished?.Invoke(this);

                TransitState(State.Decoding, State.NotLoaded);
                return;
            }

            var sw = new Stopwatch();
            sw.Start();

            Bitmap = DecodeImage(PreLoadData);

            if (Bitmap == null)
            {
                // failed to load.
                Debug.WriteLine("Failed to decode in " + sw.ElapsedMilliseconds + "ms : " + FullPath);
                PrefetchFinished?.Invoke(this);
                TransitState(State.Decoding, State.NotLoaded);
            }
            else
            {
                LoadedPath = FullPath;
                Debug.WriteLine("Decoded in " + sw.ElapsedMilliseconds + "ms : " + FullPath);

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bitmap)));
                PrefetchFinished?.Invoke(this);

                TransitState(State.Decoding, State.Decoded);
            }

            PreLoadData?.Dispose();
            PreLoadData = null;
            // todo: Free memorystream after decode finished.
        }

        private async Task<BitmapImage> _OpenImage(bool is_async)
        {
            Debug.WriteLine("Start Loading: " + FullPath);
            BitmapImage bmp = null;
            try
            {
                if (is_async)
                {
                    bmp = await Task.Run(() =>
                    {
                        return LoadAndDecode();
                    });
                }
                else
                {
                    bmp = LoadAndDecode();
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


            return bmp;
        }

        private BitmapImage LoadAndDecode()
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

        async Task<MemoryStream> LoadDataAsync(CancellationToken ct)
        {
            var ms = new MemoryStream();
            using (var fs = new FileStream(FullPath, FileMode.Open))
            {
                await fs.CopyToAsync(ms, 1024 * 1024, ct);
                ms.Seek(0, SeekOrigin.Begin);
            }
            return ms;
        }
    }
}
