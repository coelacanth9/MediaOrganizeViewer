using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MediaOrganizeViewer.ViewModels
{
    /// <summary>
    /// フォルダツリーのロジックを担当するViewModel
    /// </summary>
    public partial class FolderTreeViewModel : ObservableObject
    {
        private readonly bool _isSource;
        private readonly ISettingsService? _settingsService;

        // ツリーのルートアイテム
        public ObservableCollection<FolderTreeItem> Items { get; } = new();

        // 選択されたフォルダのパス
        [ObservableProperty]
        private string? _selectedPath;

        // ルートパス（設定保存用）
        [ObservableProperty]
        private string? _rootPath;

        public FolderTreeViewModel(string rootPath, bool isSource, ISettingsService? settingsService = null)
        {
            _isSource = isSource;
            _settingsService = settingsService;
            _rootPath = rootPath;
            SetRoot(rootPath);
        }

        /// <summary>
        /// ルートフォルダを設定し、最初の階層を読み込む
        /// </summary>
        public void SetRoot(string path)
        {
            // コレクションを空にする（ZipImageViewer の RootItems.Clear() 相当）
            Items.Clear();

            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                // パスが実在する場合の処理
                var root = CreateItem(path);
                root.Name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(root.Name)) root.Name = path;

                root.IsRoot = true;
                root.IsExpanded = true;
                Items.Add(root);
            }
            else
            {
                // ★ここが ZipImageViewer の else 節の完全な再現です
                // 実在しない場合、"クリックして設定" 用のダミーアイテムを生成して必ず Add する
                var selectRootItem = new FolderTreeItem(string.Empty)
                {
                    Name = _isSource ? "ここをクリックして移動元を設定" : "ここをクリックして移動先を設定",
                    IsRoot = true,
                    IsExpanded = true
                };

                // これを Add しないと、TreeView は「空（冴えない状態）」になります
                Items.Add(selectRootItem);
            }
        }

        [RelayCommand]
        public void ChangeRootDirectory()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = Items.FirstOrDefault()?.Path,
                Title = _isSource ? "Sourceルートを選択" : "Destinationルートを選択"
            };

            if (dialog.ShowDialog() == true)
            {
                // パスが今のルートと違う場合だけ更新する
                if (Items.FirstOrDefault()?.Path != dialog.FolderName)
                {
                    SetRoot(dialog.FolderName);
                    RootPath = dialog.FolderName;
                }
            }
        }

        /// <summary>
        /// フォルダアイテムを生成し、イベントをフックする
        /// </summary>
        private FolderTreeItem CreateItem(string path)
        {
            var item = new FolderTreeItem(path);

            // 保存されているショートカットを復元
            if (_settingsService != null && !string.IsNullOrEmpty(path))
            {
                var shortcut = _settingsService.GetFolderShortcut(path);
                if (!string.IsNullOrEmpty(shortcut))
                {
                    item.AssignedShortcut = shortcut;
                }
            }

            // プロパティ変更通知を購読（展開時と選択時のアクション）
            item.PropertyChanged += (s, e) =>
            {
                // [1] 展開された時：まだ読み込んでいなければ子階層をスキャン
                if (e.PropertyName == nameof(FolderTreeItem.IsExpanded) && item.IsExpanded)
                {
                    LoadChildren(item);
                }

                // [2] 選択された時：SelectedPathプロパティを更新
                if (e.PropertyName == nameof(FolderTreeItem.IsSelected) && item.IsSelected)
                {
                    SelectedPath = item.Path;
                }
            };

            // 子ディレクトリが存在するか確認し、あれば「ダミー」を追加して[+]を表示させる
            try
            {
                if (Directory.EnumerateDirectories(path).Any())
                {
                    // Pathが空のアイテムをダミーとして追加
                    item.Children.Add(new FolderTreeItem(null) { Name = "Loading..." });
                }
            }
            catch (UnauthorizedAccessException) { /* アクセス拒否は無視 */ }

            return item;
        }

        /// <summary>
        /// 指定されたアイテムの下にあるサブフォルダを1階層分だけ読み込む
        /// </summary>
        private void LoadChildren(FolderTreeItem parent)
        {
            // すでに本物のフォルダが読み込まれている（ダミーではない）場合は何もしない
            if (parent.Children.Count > 0 && parent.Children[0].Path != null) return;

            parent.Children.Clear();
            try
            {
                var dirs = Directory.GetDirectories(parent.Path!)
                                    .OrderBy(d => d);

                foreach (var dir in dirs)
                {
                    parent.Children.Add(CreateItem(dir));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"フォルダ読み込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ショートカットキーに割り当てられたフォルダパスを取得
        /// </summary>
        public string? GetFolderByShortcut(string shortcutKey, ISettingsService settingsService)
        {
            return settingsService.GetShortcutFolder(shortcutKey);
        }

        /// <summary>
        /// 指定パスのフォルダを選択状態にする
        /// </summary>
        public void SelectFolder(string path)
        {
            // 現在の選択を解除
            var current = EnumerateVisible(Items).FirstOrDefault(i => i.IsSelected);
            if (current != null)
            {
                current.IsSelected = false;
            }

            // 対象を選択
            var target = FindItemByPath(Items, path);
            if (target != null)
            {
                target.IsSelected = true;
            }
        }

        /// <summary>
        /// 指定されたパスの親アイテムを再読み込み（新規フォルダ作成後などに使用）
        /// </summary>
        public void RefreshFolder(string parentPath)
        {
            var parentItem = FindItemByPath(Items, parentPath);
            if (parentItem != null)
            {
                // 子アイテムをクリアして再読み込みフラグを立てる
                parentItem.Children.Clear();
                parentItem.Children.Add(new FolderTreeItem(null) { Name = "Loading..." });

                // 再度展開させることで LoadChildren が呼ばれる
                parentItem.IsExpanded = false;
                parentItem.IsExpanded = true;
            }
        }

        /// <summary>
        /// 次の可視フォルダを選択（Ctrl+Down用）
        /// </summary>
        public void SelectNextFolder()
        {
            var visible = EnumerateVisible(Items).ToList();
            var current = visible.FindIndex(i => i.IsSelected);
            if (current >= 0 && current < visible.Count - 1)
            {
                visible[current].IsSelected = false;
                visible[current + 1].IsSelected = true;
            }
        }

        /// <summary>
        /// 前の可視フォルダを選択（Ctrl+Up用）
        /// </summary>
        public void SelectPrevFolder()
        {
            var visible = EnumerateVisible(Items).ToList();
            var current = visible.FindIndex(i => i.IsSelected);
            if (current > 0)
            {
                visible[current].IsSelected = false;
                visible[current - 1].IsSelected = true;
            }
        }

        /// <summary>
        /// 選択中のフォルダを展開（Ctrl+Right用）
        /// </summary>
        public void ExpandSelectedFolder()
        {
            var selected = EnumerateVisible(Items).FirstOrDefault(i => i.IsSelected);
            if (selected != null && selected.Children.Count > 0)
            {
                selected.IsExpanded = true;
            }
        }

        /// <summary>
        /// 選択中のフォルダを縮小（Ctrl+Left用）
        /// </summary>
        public void CollapseSelectedFolder()
        {
            var selected = EnumerateVisible(Items).FirstOrDefault(i => i.IsSelected);
            if (selected != null)
            {
                selected.IsExpanded = false;
            }
        }

        /// <summary>
        /// 展開済みの可視ツリーアイテムをフラット順に列挙
        /// </summary>
        private IEnumerable<FolderTreeItem> EnumerateVisible(ObservableCollection<FolderTreeItem> items)
        {
            foreach (var item in items)
            {
                yield return item;
                if (item.IsExpanded && item.Children.Count > 0)
                {
                    foreach (var child in EnumerateVisible(item.Children))
                    {
                        yield return child;
                    }
                }
            }
        }

        private FolderTreeItem? FindItemByPath(System.Collections.ObjectModel.ObservableCollection<FolderTreeItem> items, string path)
        {
            foreach (var item in items)
            {
                if (item.Path == path) return item;
                var found = FindItemByPath(item.Children, path);
                if (found != null) return found;
            }
            return null;
        }
    }
}