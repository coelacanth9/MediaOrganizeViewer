using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MediaOrganizeViewer.ViewModels
{
    /// <summary>
    /// フォルダツリーの各ノードを表すデータモデル
    /// </summary>
    public partial class FolderTreeItem : ObservableObject
    {
        // フォルダのフルパス
        public string? Path { get; }


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))] // Nameが変わったらDisplayNameも通知
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isSelected;

        // エラー回避および整理用：ディレクトリかどうかの判定フラグ
        public bool IsDirectory { get; set; } = true;

        // 割り当てられたショートカット（F1〜F5など）を表示するためのプロパティ
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private string _assignedShortcut = string.Empty;

        // TreeViewでの表示名（ショートカットがある場合は [F1] などを付与）
        public string DisplayName => string.IsNullOrEmpty(AssignedShortcut)
            ? Name
            : $"{Name} [{AssignedShortcut}]";

        // 子要素のコレクション
        public ObservableCollection<FolderTreeItem> Children { get; } = new();

        public FolderTreeItem(string? path)
        {
            Path = path;
            // パスからフォルダ名を取得（ルートや特殊フォルダの場合はそのまま保持）
            Name = string.IsNullOrEmpty(path) ? "" : System.IO.Path.GetFileName(path);

            if (string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(path))
            {
                Name = path; // ドライブ直下などの場合
            }
        }
    }
}