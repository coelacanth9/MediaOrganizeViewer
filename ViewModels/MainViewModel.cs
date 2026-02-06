using CommunityToolkit.Mvvm.ComponentModel;
using MediaViewer.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaOrganizeViewer.ViewModels
{
    /// <summary>
    /// ファイル移動の履歴レコード
    /// </summary>
    public record MoveRecord(string SourcePath, string DestinationPath);

    public partial class MainViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly Stack<MoveRecord> _moveHistory = new();

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

        [ObservableProperty]
        private ObservableCollection<FileItem> _fileList = new();

        [ObservableProperty]
        private FileItem? _selectedFileItem;

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
                    var folder = System.IO.Path.GetDirectoryName(lastPath);
                    if (!string.IsNullOrEmpty(folder))
                        RefreshFileList(folder);
                    await LoadMediaAsync(lastPath);
                    return;
                }

                // ステップ2: 最終閲覧ファイルのフォルダが存在する？
                var lastFolder = System.IO.Path.GetDirectoryName(lastPath);
                if (!string.IsNullOrEmpty(lastFolder) && System.IO.Directory.Exists(lastFolder))
                {
                    // そのフォルダ内の対応ファイル（全種類）の最初のものを開く
                    RefreshFileList(lastFolder);
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

        async partial void OnSelectedFileItemChanged(FileItem? value)
        {
            if (value != null && (CurrentMedia == null || CurrentMedia.Path != value.Path))
            {
                await LoadMediaAsync(value.Path);
            }
        }

        public void RefreshFileList(string folderPath)
        {
            FileList.Clear();
            if (string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
                return;

            foreach (var f in System.IO.Directory.EnumerateFiles(folderPath)
                .Where(f => IsSupportedFile(f))
                .OrderBy(f => f))
            {
                FileList.Add(new FileItem(f));
            }
        }

        private void SyncSelectedFileItem()
        {
            if (CurrentMedia == null)
            {
                SelectedFileItem = null;
                return;
            }
            SelectedFileItem = FileList.FirstOrDefault(f => f.Path == CurrentMedia.Path);
        }

        private async Task OnSourceFolderSelectedAsync(string folderPath)
        {
            RefreshFileList(folderPath);

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

            SyncSelectedFileItem();
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
                   ext == ".mp3" || ext == ".wav" || ext == ".flac" ||
                   ext == ".pdf";
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
        public async Task<string?> QuickMoveToFolderAsync(string? shortcutKey, Action<string> updateStatus)
        {
            if (string.IsNullOrEmpty(shortcutKey))
            {
                updateStatus("移動するファイルがありません");
                return null;
            }

            // ショートカットからフォルダパス取得
            var destinationFolder = DestinationFolderTree.GetFolderByShortcut(shortcutKey, _settingsService);

            if (string.IsNullOrEmpty(destinationFolder))
            {
                updateStatus($"ショートカットキー 'Alt+{shortcutKey}' に対応するフォルダが見つかりません");
                return null;
            }

            // チェック済みファイルがあれば一括移動
            if (HasCheckedFiles)
                return await MoveCheckedToFolderAsync(destinationFolder, updateStatus);

            if (CurrentMedia == null)
            {
                updateStatus("移動するファイルがありません");
                return null;
            }

            return await MoveToFolderAsync(destinationFolder, updateStatus);
        }

        /// <summary>
        /// 指定フォルダへファイルを移動
        /// </summary>
        public async Task<string?> MoveToFolderAsync(string destinationFolder, Action<string> updateStatus)
        {
            if (CurrentMedia == null)
            {
                updateStatus("移動するファイルがありません");
                return null;
            }

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
                var sourceFiles = System.IO.Directory.EnumerateFiles(sourceFolder!)
                    .Where(f => IsSupportedFile(f))
                    .OrderBy(f => f)
                    .ToList();
                var currentIndex = sourceFiles.IndexOf(currentFilePath);

                // 重要: ファイル移動前にCurrentMediaを解放してファイルロックを解除
                CurrentMedia.PropertyChanged -= OnCurrentMediaPropertyChanged;
                CurrentMedia.Dispose();
                CurrentMedia = null;

                // ファイル移動実行（非同期）
                await Task.Run(() => System.IO.File.Move(currentFilePath, destinationPath));
                _moveHistory.Push(new MoveRecord(currentFilePath, destinationPath));

                // ファイルリストから移動済みアイテムを削除
                var movedItem = FileList.FirstOrDefault(f => f.Path == currentFilePath);
                if (movedItem != null)
                    FileList.Remove(movedItem);

                // フォルダ名を取得して表示
                var folderName = System.IO.Path.GetFileName(destinationFolder);
                updateStatus($"ファイルを{folderName}に移動しました: {fileName}");

                // 前回閲覧ファイルパスをクリア
                _settingsService.LastViewedFilePath = string.Empty;
                _settingsService.Save();

                // 次のファイルのパスを返す
                if (currentIndex >= 0 && currentIndex < sourceFiles.Count)
                {
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
        /// <summary>
        /// チェック済みファイルがあるか
        /// </summary>
        public bool HasCheckedFiles => FileList.Any(f => f.IsChecked);

        /// <summary>
        /// チェック済みファイルを一括移動
        /// </summary>
        public async Task<string?> MoveCheckedToFolderAsync(string destinationFolder, Action<string> updateStatus)
        {
            if (!System.IO.Directory.Exists(destinationFolder))
            {
                updateStatus($"移動先フォルダが存在しません: {destinationFolder}");
                return null;
            }

            var checkedItems = FileList.Where(f => f.IsChecked).ToList();
            if (checkedItems.Count == 0)
            {
                updateStatus("チェックされたファイルがありません");
                return null;
            }

            // 現在表示中のファイルがチェック済みか判定
            bool currentIncluded = CurrentMedia != null && checkedItems.Any(f => f.Path == CurrentMedia.Path);

            // 現在表示中のファイルが含まれる場合は解放
            if (currentIncluded && CurrentMedia != null)
            {
                CurrentMedia.PropertyChanged -= OnCurrentMediaPropertyChanged;
                CurrentMedia.Dispose();
                CurrentMedia = null;
            }

            int movedCount = 0;
            int skippedCount = 0;

            foreach (var item in checkedItems)
            {
                try
                {
                    var fileName = System.IO.Path.GetFileName(item.Path);
                    var destPath = System.IO.Path.Combine(destinationFolder, fileName);

                    if (System.IO.File.Exists(destPath))
                    {
                        skippedCount++;
                        continue;
                    }

                    await Task.Run(() => System.IO.File.Move(item.Path, destPath));
                    _moveHistory.Push(new MoveRecord(item.Path, destPath));
                    FileList.Remove(item);
                    movedCount++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"一括移動エラー: {ex.Message}");
                    skippedCount++;
                }
            }

            var folderName = System.IO.Path.GetFileName(destinationFolder);
            var msg = $"{movedCount}件を{folderName}に移動しました";
            if (skippedCount > 0) msg += $" ({skippedCount}件スキップ)";
            updateStatus(msg);

            _settingsService.LastViewedFilePath = string.Empty;
            _settingsService.Save();

            // 次に表示するファイルを返す
            if (currentIncluded && FileList.Count > 0)
            {
                return FileList[0].Path;
            }

            return currentIncluded ? null : CurrentMedia?.Path;
        }

        /// <summary>
        /// 現在のファイルをリネーム
        /// </summary>
        public async Task<bool> RenameCurrentFileAsync(string newFileName, Action<string> updateStatus)
        {
            if (CurrentMedia == null)
            {
                updateStatus("リネームするファイルがありません");
                return false;
            }

            var currentPath = CurrentMedia.Path;
            var directory = System.IO.Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(directory)) return false;

            var newPath = System.IO.Path.Combine(directory, newFileName);

            if (currentPath == newPath) return false;

            if (System.IO.File.Exists(newPath))
            {
                updateStatus("同名のファイルが既に存在します");
                return false;
            }

            try
            {
                // メディア解放 → リネーム → 再ロード
                CurrentMedia.PropertyChanged -= OnCurrentMediaPropertyChanged;
                CurrentMedia.Dispose();
                CurrentMedia = null;

                System.IO.File.Move(currentPath, newPath);

                RefreshFileList(directory);
                await LoadMediaAsync(newPath);

                updateStatus($"リネームしました: {newFileName}");
                return true;
            }
            catch (Exception ex)
            {
                updateStatus($"リネームエラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 直前のファイル移動を元に戻す
        /// </summary>
        public async Task<bool> UndoMoveAsync(Action<string> updateStatus)
        {
            if (_moveHistory.Count == 0)
            {
                updateStatus("元に戻す履歴がありません");
                return false;
            }

            var record = _moveHistory.Peek();

            // 移動先にファイルがまだあるか
            if (!System.IO.File.Exists(record.DestinationPath))
            {
                _moveHistory.Pop();
                updateStatus("元に戻せません: 移動先のファイルが見つかりません");
                return false;
            }

            // 元のフォルダがまだあるか
            var sourceDir = System.IO.Path.GetDirectoryName(record.SourcePath);
            if (string.IsNullOrEmpty(sourceDir) || !System.IO.Directory.Exists(sourceDir))
            {
                _moveHistory.Pop();
                updateStatus("元に戻せません: 元のフォルダが存在しません");
                return false;
            }

            // 元の場所に同名ファイルがないか
            if (System.IO.File.Exists(record.SourcePath))
            {
                _moveHistory.Pop();
                updateStatus("元に戻せません: 元の場所に同名ファイルが存在します");
                return false;
            }

            try
            {
                // 現在表示中のメディアを解放
                if (CurrentMedia != null)
                {
                    CurrentMedia.PropertyChanged -= OnCurrentMediaPropertyChanged;
                    CurrentMedia.Dispose();
                    CurrentMedia = null;
                }

                System.IO.File.Move(record.DestinationPath, record.SourcePath);
                _moveHistory.Pop();

                var fileName = System.IO.Path.GetFileName(record.SourcePath);
                updateStatus($"元に戻しました: {fileName}");

                // ファイルリストを再構築して戻したファイルを表示
                var folder = System.IO.Path.GetDirectoryName(record.SourcePath);
                if (!string.IsNullOrEmpty(folder))
                    RefreshFileList(folder);

                await LoadMediaAsync(record.SourcePath);
                return true;
            }
            catch (Exception ex)
            {
                updateStatus($"元に戻す際にエラー: {ex.Message}");
                return false;
            }
        }
    }
}
