#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MediaOrganizeViewer.Properties;

namespace MediaOrganizeViewer
{
    /// <summary>
    /// app.config（Properties.Settings）を使用した設定サービス実装
    /// </summary>
    public class AppConfigSettingsService : ISettingsService, INotifyPropertyChanged
    {
        public event EventHandler<SettingChangedEventArgs>? SettingChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        private List<string> _sourceRootPaths = new List<string>();
        private List<string> _destinationRootPaths = new List<string>();
        private string _lastViewedFilePath = string.Empty;

        // 「ショトカのやつ」であることが明確な名前に変更
        private List<FolderDestination> _shortcutFolders = new List<FolderDestination>();
        private int _skipIntervalSeconds = 10;
        private string _sourceTreeState = string.Empty;
        private string _destinationTreeState = string.Empty;
        private bool _autoPlayMedia = true;

        public AppConfigSettingsService()
        {
            Load();
        }

        public List<string> SourceRootPaths
        {
            get => _sourceRootPaths;
            set => SetProperty(ref _sourceRootPaths, value);
        }

        public List<string> DestinationRootPaths
        {
            get => _destinationRootPaths;
            set => SetProperty(ref _destinationRootPaths, value);
        }

        public string LastViewedFilePath
        {
            get => _lastViewedFilePath;
            set => SetProperty(ref _lastViewedFilePath, value);
        }

        /// <summary>
        /// F1~F5等のショートカットキーに割り当てられたフォルダリスト
        /// </summary>
        public List<FolderDestination> ShortcutFolders
        {
            get => _shortcutFolders;
            set => SetProperty(ref _shortcutFolders, value);
        }

        public int SkipIntervalSeconds
        {
            get => _skipIntervalSeconds;
            set => SetProperty(ref _skipIntervalSeconds, value);
        }

        public string SourceTreeState
        {
            get => _sourceTreeState;
            set => SetProperty(ref _sourceTreeState, value);
        }

        public string DestinationTreeState
        {
            get => _destinationTreeState;
            set => SetProperty(ref _destinationTreeState, value);
        }

        public bool AutoPlayMedia
        {
            get => _autoPlayMedia;
            set => SetProperty(ref _autoPlayMedia, value);
        }

        public void Load()
        {
            SourceRootPaths = ParsePathList(Settings.Default.SourceRootPath);
            DestinationRootPaths = ParsePathList(Settings.Default.DestinationRootPath);
            LastViewedFilePath = Settings.Default.LastViewedFilePath;
            SkipIntervalSeconds = Settings.Default.SkipIntervalSeconds;
            SourceTreeState = Settings.Default.SourceTreeState;
            DestinationTreeState = Settings.Default.DestinationTreeState;
            AutoPlayMedia = Settings.Default.AutoPlayMedia;

            // Settings.Default.FolderShortcuts (JSON) から _shortcutFolders への復元
            var json = Settings.Default.FolderShortcuts;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<List<FolderDestination>>(json);
                    if (loaded != null)
                    {
                        _shortcutFolders = loaded;
                    }
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ショートカット設定の読み込みエラー: {ex.Message}");
                    _shortcutFolders = new List<FolderDestination>();
                }
            }
        }

        /// <summary>
        /// JSON配列文字列をパースし、旧形式（単一パス）からの自動マイグレーションも行う
        /// </summary>
        private static List<string> ParsePathList(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new List<string>();

            // JSON配列としてパースを試みる
            if (raw.TrimStart().StartsWith("["))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(raw);
                    if (list != null) return list;
                }
                catch (JsonException) { }
            }

            // 旧形式（単一パス文字列）として1要素リストに変換
            return new List<string> { raw };
        }

        public void Save()
        {
            Settings.Default.SourceRootPath = JsonSerializer.Serialize(SourceRootPaths);
            Settings.Default.DestinationRootPath = JsonSerializer.Serialize(DestinationRootPaths);
            Settings.Default.LastViewedFilePath = LastViewedFilePath;
            Settings.Default.SkipIntervalSeconds = SkipIntervalSeconds;
            Settings.Default.SourceTreeState = SourceTreeState;
            Settings.Default.DestinationTreeState = DestinationTreeState;
            Settings.Default.AutoPlayMedia = AutoPlayMedia;

            // _shortcutFolders から Settings.Default.FolderShortcuts (JSON) への保存
            try
            {
                var json = JsonSerializer.Serialize(_shortcutFolders);
                Settings.Default.FolderShortcuts = json;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"ショートカット設定の保存エラー: {ex.Message}");
            }

            Settings.Default.Save();
        }

        // --- ショートカット管理メソッド群も名前を合わせて整理 ---

        public string? GetShortcutFolder(string shortcutKey)
        {
            return _shortcutFolders.FirstOrDefault(d => d.ShortcutKey == shortcutKey)?.Path;
        }

        public void AssignFolderShortcut(string folderPath, string shortcutKey)
        {
            var existing = _shortcutFolders.FirstOrDefault(d => d.ShortcutKey == shortcutKey);
            if (existing != null)
            {
                existing.Path = folderPath;
                existing.Name = System.IO.Path.GetFileName(folderPath);
            }
            else
            {
                _shortcutFolders.Add(new FolderDestination(System.IO.Path.GetFileName(folderPath), folderPath, shortcutKey));
            }
            OnPropertyChanged(nameof(ShortcutFolders));
        }

        public void RemoveFolderShortcut(string folderPath)
        {
            var item = _shortcutFolders.FirstOrDefault(d => d.Path == folderPath);
            if (item != null)
            {
                _shortcutFolders.Remove(item);
                OnPropertyChanged(nameof(ShortcutFolders));
            }
        }

        public string GetFolderShortcut(string folderPath)
        {
            return _shortcutFolders.FirstOrDefault(d => d.Path == folderPath)?.ShortcutKey ?? string.Empty;
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, value))
            {
                T oldValue = field;
                field = value;
                OnPropertyChanged(propertyName);
                OnSettingChanged(propertyName, oldValue, value);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnSettingChanged(string? propertyName, object? oldValue, object? newValue)
        {
            SettingChanged?.Invoke(this, new SettingChangedEventArgs(propertyName ?? string.Empty, oldValue, newValue));
        }
    }
}