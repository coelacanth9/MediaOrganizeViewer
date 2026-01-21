using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MediaOrganizeViewer.Messages;

namespace MediaOrganizeViewer.ViewModels
{
    /// <summary>
    /// フォルダツリーのロジックを担当するViewModel
    /// </summary>
    public partial class FolderTreeViewModel : ObservableObject
    {
        private readonly bool _isSource;

        // ツリーのルートアイテム
        public ObservableCollection<FolderTreeItem> Items { get; } = new();

        public FolderTreeViewModel(string rootPath, bool isSource)
        {
            _isSource = isSource;
            if (!string.IsNullOrEmpty(rootPath))
            {
                SetRoot(rootPath);
            }
        }

        /// <summary>
        /// ルートフォルダを設定し、最初の階層を読み込む
        /// </summary>
        public void SetRoot(string path)
        {
            Items.Clear();
            if (!Directory.Exists(path)) return;

            // ルートアイテムの作成
            var root = CreateItem(path);
            Items.Add(root);

            // ルートは最初から展開しておく
            root.IsExpanded = true;
        }

        /// <summary>
        /// フォルダアイテムを生成し、イベントをフックする
        /// </summary>
        private FolderTreeItem CreateItem(string path)
        {
            var item = new FolderTreeItem(path);

            // プロパティ変更通知を購読（展開時と選択時のアクション）
            item.PropertyChanged += (s, e) =>
            {
                // [1] 展開された時：まだ読み込んでいなければ子階層をスキャン
                if (e.PropertyName == nameof(FolderTreeItem.IsExpanded) && item.IsExpanded)
                {
                    LoadChildren(item);
                }

                // [2] 選択された時：Messengerでアプリ全体に通知
                if (e.PropertyName == nameof(FolderTreeItem.IsSelected) && item.IsSelected)
                {
                    if (item.Path != null)
                    {
                        WeakReferenceMessenger.Default.Send(new FolderSelectedMessage(item.Path, _isSource));
                    }
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
    }
}