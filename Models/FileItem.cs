using CommunityToolkit.Mvvm.ComponentModel;

namespace MediaOrganizeViewer.ViewModels
{
    /// <summary>
    /// ファイルリスト内の1アイテムを表す軽量モデル
    /// </summary>
    public partial class FileItem : ObservableObject
    {
        public string Path { get; }
        public string Name => System.IO.Path.GetFileName(Path);

        [ObservableProperty]
        private bool _isChecked;

        public FileItem(string path)
        {
            Path = path;
        }

        public override bool Equals(object? obj) => obj is FileItem other && Path == other.Path;
        public override int GetHashCode() => Path.GetHashCode();
    }
}
