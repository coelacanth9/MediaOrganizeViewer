namespace MediaOrganizeViewer.ViewModels
{
    /// <summary>
    /// ファイルリスト内の1アイテムを表す軽量モデル
    /// </summary>
    public class FileItem
    {
        public string Path { get; }
        public string Name => System.IO.Path.GetFileName(Path);

        public FileItem(string path)
        {
            Path = path;
        }

        public override bool Equals(object? obj) => obj is FileItem other && Path == other.Path;
        public override int GetHashCode() => Path.GetHashCode();
    }
}
