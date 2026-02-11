#nullable enable
using System;
using System.Collections.Generic;

namespace MediaOrganizeViewer
{
    /// <summary>
    /// 設定管理サービスのインターフェース
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// 移動元（下段ツリー）のルートパスリスト
        /// </summary>
        List<string> SourceRootPaths { get; set; }

        /// <summary>
        /// 移動先（上段ツリー）のルートパスリスト
        /// </summary>
        List<string> DestinationRootPaths { get; set; }

        /// <summary>
        /// 前回閲覧していたファイルパス
        /// </summary>
        string LastViewedFilePath { get; set; }

        /// <summary>
        /// ショートカットキー（F1~F5等）に割り当てられたフォルダリスト
        /// </summary>
        List<FolderDestination> ShortcutFolders { get; set; }

        /// <summary>
        /// スキップ間隔（秒）
        /// </summary>
        int SkipIntervalSeconds { get; set; }

        /// <summary>
        /// 物理設定からデータを読み込む
        /// </summary>
        void Load();

        /// <summary>
        /// 物理設定へデータを保存する
        /// </summary>
        void Save();

        /// <summary>
        /// フォルダにショートカットキーを割り当てる
        /// </summary>
        void AssignFolderShortcut(string folderPath, string shortcutKey);

        /// <summary>
        /// フォルダのショートカット割り当てを解除する
        /// </summary>
        void RemoveFolderShortcut(string folderPath);

        /// <summary>
        /// 指定されたフォルダに割り当てられているショートカットキー名を取得する
        /// </summary>
        string GetFolderShortcut(string folderPath);

        /// <summary>
        /// ショートカットキー名から、割り当てられたフォルダパスを取得する
        /// </summary>
        string? GetShortcutFolder(string shortcutKey);

        /// <summary>
        /// 設定値が変更された際に発生するイベント
        /// </summary>
        event EventHandler<SettingChangedEventArgs>? SettingChanged;
    }

    /// <summary>
    /// 設定変更イベントのデータ
    /// </summary>
    public class SettingChangedEventArgs : EventArgs
    {
        public string PropertyName { get; }
        public object? OldValue { get; }
        public object? NewValue { get; }

        public SettingChangedEventArgs(string propertyName, object? oldValue, object? newValue)
        {
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}