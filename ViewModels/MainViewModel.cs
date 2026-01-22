using CommunityToolkit.Mvvm.ComponentModel;
using MediaViewer.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaOrganizeViewer.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        // SettingsServiceを外部から参照できるように公開
        public ISettingsService SettingsService => _settingsService;

        [ObservableProperty]
        private FolderTreeViewModel _sourceFolderTree;

        [ObservableProperty]
        private FolderTreeViewModel _destinationFolderTree;

        [ObservableProperty]
        private MediaContent? _currentMedia;

        // XAMLにバインドする画像プロパティ
        public System.Windows.Media.Imaging.BitmapSource? LeftImage => (CurrentMedia as IImageMedia)?.LeftImage;
        public System.Windows.Media.Imaging.BitmapSource? RightImage => (CurrentMedia as IImageMedia)?.RightImage;
        public MainViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _settingsService.Load();
            SourceFolderTree = new FolderTreeViewModel(_settingsService.SourceRootPath, true);
            DestinationFolderTree = new FolderTreeViewModel(_settingsService.DestinationRootPath, false);

            // FolderTreeViewModelのSelectedPath変更を直接監視
            SourceFolderTree.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(FolderTreeViewModel.SelectedPath) && SourceFolderTree.SelectedPath != null)
                {
                    await OnSourceFolderSelectedAsync(SourceFolderTree.SelectedPath);
                }
                else if (e.PropertyName == nameof(FolderTreeViewModel.RootPath))
                {
                    _settingsService.SourceRootPath = SourceFolderTree.RootPath ?? string.Empty;
                    _settingsService.Save();
                }
            };

            DestinationFolderTree.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FolderTreeViewModel.RootPath))
                {
                    _settingsService.DestinationRootPath = DestinationFolderTree.RootPath ?? string.Empty;
                    _settingsService.Save();
                }
            };
        }

        private async Task OnSourceFolderSelectedAsync(string folderPath)
        {
            // フォルダ内の最初のzipを探す
            var firstZip = System.IO.Directory.EnumerateFiles(folderPath, "*.zip").FirstOrDefault();
            if (firstZip != null)
            {
                await LoadMediaAsync(firstZip);
            }
        }


        public async Task LoadMediaAsync(string path)
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

        /// <summary>
        /// ショートカットキーを使ってファイルを移動
        /// </summary>
        public string? QuickMoveToFolder(string? shortcutKey, Action<string> updateStatus)
        {
            if (CurrentMedia == null || string.IsNullOrEmpty(shortcutKey))
            {
                updateStatus("移動するファイルがありません");
                return null;
            }

            // ショートカットからフォルダパス取得
            var destinationFolder = DestinationFolderTree.GetFolderByShortcut(shortcutKey, _settingsService);

            if (string.IsNullOrEmpty(destinationFolder))
            {
                updateStatus($"ショートカットキー '{shortcutKey}' に対応するフォルダが見つかりません");
                return null;
            }

            // フォルダが存在しない場合はエラー
            if (!System.IO.Directory.Exists(destinationFolder))
            {
                updateStatus($"移動先フォルダが存在しません: {destinationFolder}");
                return null;
            }

            try
            {
                var currentFilePath = CurrentMedia.Path;
                var fileName = System.IO.Path.GetFileName(currentFilePath);
                var destinationPath = System.IO.Path.Combine(destinationFolder, fileName);

                if (System.IO.File.Exists(destinationPath))
                {
                    updateStatus("移動先に同名ファイルが存在します");
                    return null;
                }

                var sourceFolder = System.IO.Path.GetDirectoryName(currentFilePath);
                var sourceFiles = System.IO.Directory.EnumerateFiles(sourceFolder!, "*.zip")
                    .OrderBy(f => f)
                    .ToList();
                var currentIndex = sourceFiles.IndexOf(currentFilePath);

                // 重要: ファイル移動前にCurrentMediaを解放してファイルロックを解除
                CurrentMedia.PropertyChanged -= OnMediaPropertyChanged;
                CurrentMedia.Dispose();
                CurrentMedia = null;

                // ファイル移動実行
                System.IO.File.Move(currentFilePath, destinationPath);

                // フォルダ名を取得して表示
                var folderName = System.IO.Path.GetFileName(destinationFolder);
                updateStatus($"ファイルを{folderName}に移動しました: {destinationPath}");

                // 前回閲覧ファイルパスをクリア
                _settingsService.LastViewedFilePath = string.Empty;
                _settingsService.Save();

                // 次のファイルのパスを返す
                if (currentIndex >= 0 && currentIndex < sourceFiles.Count)
                {
                    // 移動したファイルの次のファイルを返す
                    if (currentIndex < sourceFiles.Count - 1)
                    {
                        return sourceFiles[currentIndex + 1];
                    }
                    else if (currentIndex > 0)
                    {
                        return sourceFiles[currentIndex - 1];
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                updateStatus($"ファイル移動エラー: {ex.Message}");
                return null;
            }
        }
    }
}
