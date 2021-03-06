﻿using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        public Action<BitmapImage> OpenedAsMainImage = null;
        public Action<ImageModel> ItemSelected = null;
        public Action<ImageModel> LoadFinished = null;
        public Action<ImageModel> PrefetchFinished = null;
        public CancellationTokenSource DecodeCancellationTokensource { get; set; } = null;

        public Visibility LoadingProgressVisibility
        {
            get
            {
                switch (ImageState)
                {
                    case State.Loading:
                    case State.Decoding:
                        return Visibility.Visible;
                    default:
                        return Visibility.Collapsed;
                }
            }
        }

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

        void Notify(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        bool TransitState(State current, State next)
        {
            lock (this)
            {
                if (current == ImageState)
                {
                    ImageState = next;
                    Notify(nameof(LoadingProgressVisibility));
                    return true;
                }
                Debug.WriteLine(string.Format("Wrong transition from {0} to {1} (current: {3}) : {2} ", current, next, FullPath, ImageState));
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

        public void OpenImage()
        {
            Debug.WriteLine("Open!");
            ItemSelected?.Invoke(this);
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
                DecodeCancellationTokensource = null;
            }

            Notify(nameof(Bitmap));
            Notify(nameof(LoadingProgressVisibility));
        }

        public async Task<bool> Load(CancellationToken ct)
        {
            if (ImageState != State.NotLoaded) { return true; }
            Debug.WriteLine("Load requested: " + FullPath);

            if (!TransitState(State.NotLoaded, State.Loading))
            {
                return true;
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

            if (PreLoadData == null || PreLoadData.Length < 2)
            {
                PreLoadData = null;
                Debug.WriteLine("Something wrong: failed to load.");
                TransitState(State.Loading, State.NotLoaded);
                return false;
            }

            Debug.WriteLine("Loaded in " + sw.ElapsedMilliseconds + "ms. " + FullPath);
            TransitState(State.Loading, State.Loaded);
            return true;
        }

        public bool IsDecotable()
        {
            return ImageState == State.Loaded;
        }

        public void Decode()
        {
            if (ImageState != State.Loaded) { return; }

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

            try
            {
                Bitmap = DecodeImage(PreLoadData);
            }
            catch (NotSupportedException e)
            {
                Debug.WriteLine("Caught NotSupportedException on opening image. " + e.Message);
                Bitmap = null;
            }
            catch (FileFormatException e)
            {
                Debug.WriteLine("Caught FileFormatException on opening image. " + e.Message);
                Bitmap = null;
            }

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

            //PreLoadData?.Dispose();
            //PreLoadData = null;
            // todo: Free memorystream after decode finished.
        }

        public bool IsDisplayable
        {
            get { return ImageState == State.Decoded; }
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
            catch (NotSupportedException e)
            {
                Debug.WriteLine("Caught NotSupportedException on opening image. " + e.Message);
                return null;
            }
            catch (FileFormatException e)
            {
                Debug.WriteLine("Caught FileFormatException on opening image. " + e.Message);
                Bitmap = null;
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
            var length = new FileInfo(FullPath).Length;
            using (var fs = new FileStream(FullPath, FileMode.Open))
            {
                await fs.CopyToAsync(ms, 1024 * 1024, ct);
                ms.Seek(0, SeekOrigin.Begin);
            }

            if (length != ms.Length)
            {
                Debug.WriteLine("Size unmatched. " + length + " " + ms.Length);
                ms.Dispose();
                return null;
            }
            return ms;
        }
    }
}
