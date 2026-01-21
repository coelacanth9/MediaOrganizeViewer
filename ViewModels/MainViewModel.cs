using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MediaOrganizeViewer.Messages;
using MediaViewer.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaOrganizeViewer.ViewModels
{
    public partial class MainViewModel : ObservableObject, IRecipient<FolderSelectedMessage>
    {
        [ObservableProperty]
        private FolderTreeViewModel _sourceFolderTree;

        [ObservableProperty]
        private FolderTreeViewModel _destinationFolderTree;

        [ObservableProperty]
        private MediaContent? _currentMedia;

        // XAMLにバインドする画像プロパティ
        public System.Windows.Media.Imaging.BitmapSource? LeftImage => (CurrentMedia as IImageMedia)?.LeftImage;
        public System.Windows.Media.Imaging.BitmapSource? RightImage => (CurrentMedia as IImageMedia)?.RightImage;
        public MainViewModel()
        {
            // 実際には SettingsService からパスを取得しますが、まずは動作確認用に直接指定
            SourceFolderTree = new FolderTreeViewModel(@"E:\18\comics\byDate", true);
            DestinationFolderTree = new FolderTreeViewModel(@"E:\18\comics\testAuther", false);

            WeakReferenceMessenger.Default.RegisterAll(this);

        }

        public async void Receive(FolderSelectedMessage message)
        {
            if (message.IsSource)
            {
                // フォルダ内の最初のzipを探す
                var firstZip = System.IO.Directory.EnumerateFiles(message.Value, "*.zip").FirstOrDefault();
                if (firstZip != null)
                {
                    await LoadMediaAsync(firstZip);
                }
            }
        }

        private async Task LoadMediaAsync(string path)
        {
            if (CurrentMedia != null)
            {
                CurrentMedia.PropertyChanged -= OnMediaPropertyChanged; // 以前の購読を解除
                CurrentMedia.Dispose();
            }

            CurrentMedia = MediaFactory.Create(path);
            CurrentMedia.PropertyChanged += OnMediaPropertyChanged; // ページ更新を検知するため購読

            await CurrentMedia.LoadAsync();

            // 初回表示のために通知
            OnPropertyChanged(nameof(LeftImage));
            OnPropertyChanged(nameof(RightImage));
        }

        public async Task MoveNextMediaAsync(bool forward)
        {
            if (CurrentMedia == null) return;

            try
            {
                var directory = System.IO.Path.GetDirectoryName(CurrentMedia.Path);
                if (string.IsNullOrEmpty(directory)) return;

                // 以前のように、今のフォルダから対象ファイルをリストアップ
                var files = System.IO.Directory.EnumerateFiles(directory, "*.zip")
                                .OrderBy(f => f)
                                .ToList();

                var currentIndex = files.IndexOf(CurrentMedia.Path);
                int nextIndex = forward ? currentIndex + 1 : currentIndex - 1;

                if (nextIndex >= 0 && nextIndex < files.Count)
                {
                    // ツリーの選択を変えず、このメソッド内で直接ロードを完結させる
                    await LoadMediaAsync(files[nextIndex]);
                }
            }
            catch (Exception ex)
            {
                // 以前のコードのようにデバッグ出力やステータス更新を行う
                System.Diagnostics.Debug.WriteLine($"書庫移動エラー: {ex.Message}");
            }
        }

        private void OnMediaPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IImageMedia.LeftImage) ||
                e.PropertyName == nameof(IImageMedia.RightImage))
            {
                OnPropertyChanged(nameof(LeftImage));
                OnPropertyChanged(nameof(RightImage));
            }
        }
    }
}
