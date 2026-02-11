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
    /// ツリーの展開・選択状態を保持するDTO
    /// </summary>
    public class TreeState
    {
        public List<string> ExpandedPaths { get; set; } = new();
        public string? SelectedPath { get; set; }
    }

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

        /// <summary>
        /// ルート追加/変更/削除時に発火するイベント
        /// </summary>
        public event Action? RootsChanged;

        public FolderTreeViewModel(List<string> rootPaths, bool isSource, ISettingsService? settingsService = null)
        {
            _isSource = isSource;
            _settingsService = settingsService;
            SetRoots(rootPaths);
        }

        /// <summary>
        /// 複数ルートを設定し、各ルートの最初の階層を読み込む
        /// </summary>
        public void SetRoots(List<string> paths)
        {
            Items.Clear();

            var validPaths = paths.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).ToList();

            if (validPaths.Count > 0)
            {
                foreach (var path in validPaths)
                {
                    var root = CreateItem(path);
                    root.Name = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(root.Name)) root.Name = path;
                    root.IsRoot = true;
                    root.IsExpanded = true;
                    Items.Add(root);
                }
            }
            else
            {
                Items.Add(CreateDummyRoot());
            }
        }

        /// <summary>
        /// ダイアログで新規ルートを追加
        /// </summary>
        public void AddRootViaDialog()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = _isSource ? "追加するSourceルートを選択" : "追加するDestinationルートを選択"
            };

            if (dialog.ShowDialog() == true)
            {
                AddRoot(dialog.FolderName);
            }
        }

        /// <summary>
        /// ルートアイテムを追加（重複チェック付き）
        /// </summary>
        public void AddRoot(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            if (Items.Any(i => i.Path == path)) return;

            // ダミーが表示中なら除去
            if (Items.Count == 1 && string.IsNullOrEmpty(Items[0].Path))
            {
                Items.Clear();
            }

            var root = CreateItem(path);
            root.Name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(root.Name)) root.Name = path;
            root.IsRoot = true;
            root.IsExpanded = true;
            Items.Add(root);

            RootsChanged?.Invoke();
        }

        /// <summary>
        /// 指定ルートを除外（最後の1つなら「右クリックして設定」ダミーに）
        /// </summary>
        public void RemoveRoot(FolderTreeItem root)
        {
            Items.Remove(root);

            if (Items.Count == 0)
            {
                Items.Add(CreateDummyRoot());
            }

            RootsChanged?.Invoke();
        }

        /// <summary>
        /// ダイアログで特定ルートのパスを変更
        /// </summary>
        public void ChangeRootPath(FolderTreeItem root)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = _isSource ? "Sourceルートを選択" : "Destinationルートを選択"
            };

            if (!string.IsNullOrEmpty(root.Path))
            {
                dialog.InitialDirectory = root.Path;
            }

            if (dialog.ShowDialog() == true)
            {
                var newPath = dialog.FolderName;
                if (root.Path == newPath) return;
                if (Items.Any(i => i.Path == newPath)) return;

                var index = Items.IndexOf(root);
                if (index < 0) return;

                Items.RemoveAt(index);

                var newRoot = CreateItem(newPath);
                newRoot.Name = Path.GetFileName(newPath);
                if (string.IsNullOrEmpty(newRoot.Name)) newRoot.Name = newPath;
                newRoot.IsRoot = true;
                newRoot.IsExpanded = true;
                Items.Insert(index, newRoot);

                RootsChanged?.Invoke();
            }
        }

        /// <summary>
        /// 現在の全ルートパスをリストで返す（設定保存用）
        /// </summary>
        public List<string> GetRootPaths()
        {
            return Items.Where(i => !string.IsNullOrEmpty(i.Path))
                        .Select(i => i.Path!)
                        .ToList();
        }

        /// <summary>
        /// 未設定時のダミーアイテムを生成
        /// </summary>
        private FolderTreeItem CreateDummyRoot()
        {
            return new FolderTreeItem(string.Empty)
            {
                Name = _isSource ? "右クリックして移動元を設定" : "右クリックして移動先を設定",
                IsRoot = true,
                IsExpanded = true
            };
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

        /// <summary>
        /// ツリーの展開・選択状態を取得
        /// </summary>
        public TreeState GetTreeState()
        {
            var state = new TreeState { SelectedPath = SelectedPath };
            CollectExpandedPaths(Items, state.ExpandedPaths);
            return state;
        }

        private void CollectExpandedPaths(ObservableCollection<FolderTreeItem> items, List<string> paths)
        {
            foreach (var item in items)
            {
                if (item.IsExpanded && !string.IsNullOrEmpty(item.Path))
                    paths.Add(item.Path);
                if (item.IsExpanded)
                    CollectExpandedPaths(item.Children, paths);
            }
        }

        /// <summary>
        /// 保存された展開・選択状態を復元
        /// </summary>
        public void RestoreTreeState(TreeState? state)
        {
            if (state == null) return;

            // 全ルートを一旦折りたたむ
            foreach (var item in Items)
                item.IsExpanded = false;

            // 保存されたパスを親→子の順に展開（展開で子がLazy Loadされる）
            foreach (var path in state.ExpandedPaths.OrderBy(p => p.Length))
            {
                var item = FindItemByPath(Items, path);
                if (item != null)
                    item.IsExpanded = true;
            }

            // 選択を復元
            if (!string.IsNullOrEmpty(state.SelectedPath))
                SelectFolder(state.SelectedPath);
        }
    }
}