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

namespace PhotoCat2.ViewModels
{

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ImageModel> Items { get; } = new ObservableCollection<ImageModel>();
        int PrefetchCount = 0;
        const int MAX_PREFETCH = 10;
        const int LOAD_CONCURRENCY = 4;

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
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void Notify(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        void ItemLoadCompleted(ImageModel loaded)
        {
            var startIndex = Items.IndexOf(loaded) + 1;
            var num = Math.Min(MAX_PREFETCH, Items.Count - startIndex);
            TotalImages = num;
            LoadedImagesCount = 0;

            Task.Run(() =>
            {
                StartPrefetchAll(startIndex, num);
            });
        }

        void ItemPrefetchCompleted(ImageModel loaded)
        {
            LoadedImagesCount++;
        }

        private void LoadNext(ImageModel loaded)
        {
            PrefetchCount++;
            var index = Items.IndexOf(loaded);
            Debug.WriteLine("Item " + index + " prefetch completed");
            if (PrefetchCount < MAX_PREFETCH && (index + 1) < Items.Count)
            {
                Task.Run(() =>
                {
                    Items[index + 1].StartPrefetch();
                });
            }
        }

        public void StartPrefetchAll(int startIndex, int num)
        {
            var options = new ParallelOptions() { MaxDegreeOfParallelism = LOAD_CONCURRENCY };

            var items = new List<ImageModel>(num);

            for (int i = 0; i < num; i++)
            {
                items.Add(Items[startIndex + i]);
            }

            Parallel.ForEach(items, options, (item) =>
            {
                item.StartPrefetch();
            });
        }


        public void AddItem(ImageModel item)
        {
            item.LoadFinished += ItemLoadCompleted;
            item.PrefetchFinished += ItemPrefetchCompleted;
            Items.Add(item);
        }
    }
}
