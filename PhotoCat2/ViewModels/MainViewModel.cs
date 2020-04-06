using PhotoCat2.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoCat2.ViewModels
{

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ImageModel> Items { get; } = new ObservableCollection<ImageModel>();
        public BitmapImage MainImageSource { get; set; } = null;

        public Action<BitmapImage> MainImageUpdated = null;

        CancellationTokenSource LoadCancellationTokenSource = null;

        public int PreFetchNum { get; set; } = 7;

        private int _SelectedIndex = 0;
        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set
            {
                if (_SelectedIndex != value)
                {
                    Debug.WriteLine("SelectedIndex: " + value);
                    _SelectedIndex = value;
                }
            }
        }

        int _DisplayedIndex = 0;
        public int DisplayedIndex
        {
            get { return _DisplayedIndex; }
            set
            {
                if (_DisplayedIndex != value)
                {
                    Debug.WriteLine("Displayedindex: " + value);
                    _DisplayedIndex = value;
                }
            }
        }

        private int _TotalImages = 0;
        public int TotalImages
        {
            get { return _TotalImages; }
            set
            {
                if (value != _TotalImages)
                {
                    _TotalImages = value;
                    Notify(nameof(TotalImages));
                    Notify(nameof(LoadingProgress));
                }
            }
        }


        private int _LoadedImagesCount = 0;
        public int LoadedImagesCount
        {
            get { return _LoadedImagesCount; }
            set
            {
                if (value != _LoadedImagesCount)
                {
                    _LoadedImagesCount = value;
                    Notify(nameof(LoadedImagesCount));
                    Notify(nameof(LoadingProgress));
                }
            }
        }

        private bool _IsLoading = false;
        public bool IsLoading
        {
            get { return _IsLoading; }
            set
            {
                if (value != _IsLoading)
                {
                    _IsLoading = value;
                    Notify(nameof(IsLoading));
                    Notify(nameof(IsLoadingInfoVisible));
                }
            }
        }

        public Visibility IsLoadingInfoVisible
        {
            get { return IsLoading ? Visibility.Visible : Visibility.Collapsed; }
        }

        public double LoadingProgress
        {
            get
            {
                if (TotalImages == 0) { return 0; }
                return (double)LoadedImagesCount / (double)TotalImages;
            }
        }

        public MainViewModel()
        {
            Items.CollectionChanged += Items_CollectionChanged;
            ThreadPool.SetMaxThreads(2, 1);
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void Notify(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void ItemSelected(ImageModel selected)
        {
            SelectedIndex = Items.IndexOf(selected);

            if (selected.IsDisplayable)
            {
                MainImageSource = selected.Bitmap;
                Notify(nameof(MainImageSource));
                MainImageUpdated?.Invoke(selected.Bitmap);
            }
            else
            {
                // not to trigger binding. keep displayed, clear internal flag.
                MainImageSource = null;
            }

            LoadNeighborImages(selected);
        }

        void LoadNeighborImages(ImageModel loadedMainImage)
        {
            var currentIndex = Items.IndexOf(loadedMainImage);


            DisplayedIndex = currentIndex;
            var frontStartIndex = currentIndex;
            var frontNum = Math.Min(Items.Count - frontStartIndex, PreFetchNum);

            var backNum = Math.Min(currentIndex, PreFetchNum);
            var backStartIndex = Math.Max(0, currentIndex - backNum - 1);

            TotalImages = frontNum;
            LoadedImagesCount = 0;

            Task.Run(() =>
            {
                var loadItems = new List<ImageModel>(frontNum + backNum);
                for (int i = frontStartIndex; i < (frontStartIndex + frontNum); i++)
                {
                    loadItems.Add(Items[i]);
                }
                for (int i = (backStartIndex + backNum); i >= backStartIndex; i--)
                {
                    loadItems.Add(Items[i]);
                }
                StartPrefetch(loadItems);
            });

            Task.Run(() =>
            {
                UnloadImages(0, backStartIndex - 1);
                UnloadImages(frontStartIndex + frontNum, Items.Count - (frontStartIndex + frontNum));
                GC.Collect();
            });
        }

        public void ItemPrefetchCompleted(ImageModel loaded)
        {
            LoadedImagesCount++;
            var currentIndex = Items.IndexOf(loaded);

            if (MainImageSource == null && currentIndex == SelectedIndex)
            {
                MainImageSource = loaded.Bitmap;
                Notify(nameof(MainImageSource));
                MainImageUpdated?.Invoke(loaded.Bitmap);
            }
        }

        async void StartPrefetch(List<ImageModel> items)
        {
            // todo: Accept cancel operation using CancellationToken.
            foreach (var item in items)
            {

                // Load sequentially.

                var RETRY_LIMIT = 3;
                for (int i = 0; i < RETRY_LIMIT; i++)
                {
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(3000);

                    var succeed = false;
                    try
                    {
                        succeed = await item.Load(cts.Token);
                        if (succeed)
                        {
                            cts?.Dispose();
                            cts = null;
                            break;
                        }
                        else
                        {
                            Debug.WriteLine("Failed to load. retry count {0} / {1}.", i, RETRY_LIMIT);
                        }
                    }
                    catch (TaskCanceledException e)
                    {
                        Debug.WriteLine("Task cancelled due to timeout.");
                        item.Clear();
                    }
                }

                // Decode parallelly.
                if (item.IsDecotable())
                {
                    lock (item)
                    {
                        var cts = new CancellationTokenSource();
                        item.DecodeCancellationTokensource = cts;

                        var queued = ThreadPool.QueueUserWorkItem(new WaitCallback((a) =>
                        {
                            // todo: sometimes, it stuck in decoding state
                            item.Decode();
                        }), cts.Token);
                        if (!queued)
                        {
                            Debug.WriteLine("Failed to queue decoding ");
                        }
                    }
                }
            }
        }

        void UnloadImages(int startIndex, int num)
        {
            for (int i = startIndex; i < (startIndex + num); i++)
            {
                var item = Items[i];
                lock (item)
                {
                    item.DecodeCancellationTokensource?.Cancel();
                    item.Clear();
                }
            }
        }

        void CancelPrefetchOperations()
        {
            if (LoadCancellationTokenSource != null)
            {
                Debug.WriteLine("Load CTS Cancel request.");
                LoadCancellationTokenSource?.Cancel();
            }

        }

        public void AddItem(ImageModel item)
        {
            Items.Add(item);
        }

        public void AddItem(string fullPath)
        {
            var m = new ImageModel(fullPath)
            {
                ItemSelected = (selected) =>
                {
                    ItemSelected(selected);
                },

                PrefetchFinished = (loaded) =>
                {
                    Application.Current.Dispatcher.Invoke(
                        () =>
                        {
                            ItemPrefetchCompleted(loaded);
                        }, DispatcherPriority.Background);
                },
            };
            Items.Add(m);
        }

        public int NavigateRelative(int shift)
        {
            if (Items.Count == 0) { return 0; }

            var newIndex = Math.Max(0, Math.Min((SelectedIndex + shift), Items.Count - 1));
            Items[newIndex].OpenImage();
            return newIndex;
        }

    }
}
