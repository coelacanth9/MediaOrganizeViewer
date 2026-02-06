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

        [ObservableProperty]
        private string _statusText = "準備完了";

        public MainViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _settingsService.Load();
            SourceFolderTree = new FolderTreeViewModel(_settingsService.SourceRootPath, true, _settingsService);
            DestinationFolderTree = new FolderTreeViewModel(_settingsService.DestinationRootPath, false, _settingsService);

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

            // 最終閲覧位置を復元
            RestoreLastViewedFile();
        }

        private async void RestoreLastViewedFile()
        {
            var lastPath = _settingsService.LastViewedFilePath;
            if (string.IsNullOrEmpty(lastPath))
                return;

            try
            {
                // ステップ1: 最終閲覧ファイルが存在する？
                if (System.IO.File.Exists(lastPath))
                {
                    await LoadMediaAsync(lastPath);
                    return;
                }

                // ステップ2: 最終閲覧ファイルのフォルダが存在する？
                var lastFolder = System.IO.Path.GetDirectoryName(lastPath);
                if (!string.IsNullOrEmpty(lastFolder) && System.IO.Directory.Exists(lastFolder))
                {
                    // そのフォルダ内の対応ファイル（全種類）の最初のものを開く
                    var firstFile = GetFirstSupportedFileInFolder(lastFolder);
                    if (firstFile != null)
                    {
                        await LoadMediaAsync(firstFile);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"最終閲覧位置の復元エラー: {ex.Message}");
            }
        }

        private async Task OnSourceFolderSelectedAsync(string folderPath)
        {
            // フォルダ内の対応ファイル（全種類）の最初のものを開く
            var firstFile = GetFirstSupportedFileInFolder(folderPath);
            if (firstFile != null)
            {
                await LoadMediaAsync(firstFile);
            }
        }


        public async Task LoadMediaAsync(string path)
        {
            if (CurrentMedia != null)
            {
                CurrentMedia.PropertyChanged -= OnCurrentMediaPropertyChanged;
                CurrentMedia.Dispose();
            }

            CurrentMedia = MediaFactory.Create(path);
            CurrentMedia.PropertyChanged += OnCurrentMediaPropertyChanged;
            await CurrentMedia.LoadAsync();

            // ステータスバー更新
            UpdateStatusText();

            // 最終閲覧位置を保存
            _settingsService.LastViewedFilePath = path;
            _settingsService.Save();
        }

        private void OnCurrentMediaPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // ページ情報が変更されたらステータスバーを更新
            if (e.PropertyName == nameof(IPageNavigable.CurrentPage))
            {
                UpdateStatusText();
            }
        }

        private void UpdateStatusText()
        {
            if (CurrentMedia == null)
            {
                StatusText = "準備完了";
                return;
            }

            var fileName = System.IO.Path.GetFileName(CurrentMedia.Path);

            // ZIPの場合はページ情報も表示
            if (CurrentMedia is IPageNavigable navigable)
            {
                StatusText = $"{fileName} - {navigable.CurrentPage + 1}/{navigable.TotalPages} ページ";
            }
            else
            {
                StatusText = fileName;
            }
        }

        /// <summary>
        /// 現在表示中のメディアをアンロード
        /// </summary>
        public void UnloadMedia()
        {
            if (CurrentMedia != null)
            {
                CurrentMedia.PropertyChanged -= OnCurrentMediaPropertyChanged;
                CurrentMedia.Dispose();
                CurrentMedia = null;
            }
            StatusText = "アンロードしました";
        }

        public bool IsSupportedFile(string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLower();
            return ext == ".zip" ||
                   ext == ".jpg" || ext == ".jpeg" ||
                   ext == ".png" || ext == ".bmp" ||
                   ext == ".gif" || ext == ".webp" ||
                   ext == ".mp4" ||
                   ext == ".mp3" || ext == ".wav" || ext == ".flac";
        }

        /// <summary>
        /// 指定フォルダ内の対応ファイル（全種類）の最初のものを取得
        /// </summary>
        private string? GetFirstSupportedFileInFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
                return null;

            return System.IO.Directory.EnumerateFiles(folderPath)
                .Where(f => IsSupportedFile(f))
                .OrderBy(f => f)
                .FirstOrDefault();
        }

        public async Task MoveNextMediaAsync(bool forward)
        {
            if (CurrentMedia == null) return;

            try
            {
                var directory = System.IO.Path.GetDirectoryName(CurrentMedia.Path);
                if (string.IsNullOrEmpty(directory)) return;

                // 対応するすべてのファイル形式を列挙
                var files = System.IO.Directory.EnumerateFiles(directory)
                                .Where(f => IsSupportedFile(f))
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
                System.Diagnostics.Debug.WriteLine($"メディア移動エラー: {ex.Message}");
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
                CurrentMedia.PropertyChanged -= OnCurrentMediaPropertyChanged;
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
