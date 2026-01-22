// Version: 0.1.0
#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MediaOrganizeViewer
{
    public class FolderDestination : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _path = string.Empty;
        private string _shortcutKey = string.Empty;

        /// <summary>
        /// フォルダの表示名
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// フォルダのパス
        /// </summary>
        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ショートカットキー（F4, F5等）
        /// </summary>
        public string ShortcutKey
        {
            get => _shortcutKey;
            set
            {
                _shortcutKey = value;
                OnPropertyChanged();
            }
        }

        public FolderDestination()
        {
        }

        public FolderDestination(string name, string path, string shortcutKey)
        {
            Name = name;
            Path = path;
            ShortcutKey = shortcutKey;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}