using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MediaOrganizeViewer.ViewModels;
using MediaViewer.Core;

namespace MediaOrganizeViewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var settingsService = new AppConfigSettingsService();
            this.DataContext = new MainViewModel(settingsService);

            AddHandler(MediaControlBar.NextMediaRequestedEvent, new RoutedEventHandler(OnNextMediaRequested));
            AddHandler(MediaControlBar.PrevMediaRequestedEvent, new RoutedEventHandler(OnPrevMediaRequested));
            AddHandler(MediaControlBar.SkipIntervalChangedEvent, new RoutedEventHandler(OnSkipIntervalChanged));

            // 保存されたスキップ間隔を復元
            MediaControlBar.DefaultSkipSeconds = settingsService.SkipIntervalSeconds;
        }

        private async void OnNextMediaRequested(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                await vm.MoveNextMediaAsync(true);
                this.Focus();
            }
        }

        private void OnSkipIntervalChanged(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.SettingsService.SkipIntervalSeconds = MediaControlBar.DefaultSkipSeconds;
                vm.SettingsService.Save();
            }
        }

        private async void OnPrevMediaRequested(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                await vm.MoveNextMediaAsync(false);
                this.Focus();
            }
        }

        protected override async void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // F1-F5によるショートカットジャンプ
            if (e.Key >= Key.F1 && e.Key <= Key.F5)
            {
                e.Handled = true;
                MoveFileAndLoadNext(vm.QuickMoveToFolder(e.Key.ToString(), SetStatusText));
                this.Focus();
                return;
            }

            // Escape: メディアをアンロード
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                vm.UnloadMedia();
                this.Focus();
                return;
            }

            // Ctrl系ショートカット
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    vm.SourceFolderTree.SelectPrevFolder();
                    return;
                }
                if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    vm.SourceFolderTree.SelectNextFolder();
                    return;
                }
                if (e.Key == Key.Right)
                {
                    e.Handled = true;
                    vm.SourceFolderTree.ExpandSelectedFolder();
                    return;
                }
                if (e.Key == Key.Left)
                {
                    e.Handled = true;
                    vm.SourceFolderTree.CollapseSelectedFolder();
                    return;
                }
                if (e.Key == Key.Z)
                {
                    e.Handled = true;
                    _ = vm.UndoMoveAsync(SetStatusText);
                    this.Focus();
                    return;
                }
            }

            // Up/Down/PageUp/PageDown: フォルダ内ファイル送り
            if (e.Key == Key.Up || e.Key == Key.PageUp)
            {
                e.Handled = true;
                await vm.MoveNextMediaAsync(false);
                this.Focus();
                return;
            }
            if (e.Key == Key.Down || e.Key == Key.PageDown)
            {
                e.Handled = true;
                await vm.MoveNextMediaAsync(true);
                this.Focus();
                return;
            }

            // Space: 移動先ツリーで選択中のフォルダへファイル移動
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                var selected = FindSelectedItem(vm.DestinationFolderTree.Items);
                if (selected == null || string.IsNullOrEmpty(selected.Path))
                    SetStatusText("移動先フォルダを選択してください");
                else
                    MoveFileAndLoadNext(vm.MoveToFolder(selected.Path, SetStatusText));
                this.Focus();
                return;
            }

            // Left/Right: 書庫/PDFはページ送り、動画/音声はスキップ
            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                if (vm.CurrentMedia is IPageNavigable navigable)
                {
                    e.Handled = true;
                    if (e.Key == Key.Left) navigable.NextPage();
                    else navigable.PrevPage();
                    this.Focus();
                }
                else if (vm.CurrentMedia is VideoContent or AudioContent)
                {
                    var controlBar = FindMediaControlBar(this);
                    if (controlBar != null)
                    {
                        e.Handled = true;
                        controlBar.SkipBySeconds(e.Key == Key.Right ? controlBar.SkipSeconds : -controlBar.SkipSeconds);
                        this.Focus();
                    }
                }
            }
        }

        /// <summary>
        /// マウスホイールで書庫/PDFのページ送り
        /// </summary>
        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            var vm = DataContext as MainViewModel;
            if (vm?.CurrentMedia is IPageNavigable navigable)
            {
                if (e.Delta > 0)
                    navigable.PrevPage();
                else if (e.Delta < 0)
                    navigable.NextPage();
                e.Handled = true;
            }
        }

        /// <summary>
        /// ファイルを移動して次のファイルをロード（共通処理）
        /// </summary>
        private async void MoveFileAndLoadNext(string? nextFilePath)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            if (!string.IsNullOrEmpty(nextFilePath))
            {
                await vm.LoadMediaAsync(nextFilePath);
            }
            else
            {
                vm.CurrentMedia?.Dispose();
                vm.CurrentMedia = null;
            }
        }

        /// <summary>
        /// VisualTreeからMediaControlBarを探す
        /// </summary>
        private static MediaControlBar? FindMediaControlBar(DependencyObject parent)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is MediaControlBar bar) return bar;
                var found = FindMediaControlBar(child);
                if (found != null) return found;
            }
            return null;
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

        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as FolderTreeItem;
            if (selectedItem == null) return;

            var mainVm = this.DataContext as MainViewModel;
            if (mainVm == null) return;

            // ルートアイテムがクリックされた場合のみ、ルート変更ダイアログを表示
            if (selectedItem.IsRoot)
            {
                FolderTreeViewModel? targetTreeVm = null;

                if (mainVm.DestinationFolderTree.Items.Contains(selectedItem))
                {
                    targetTreeVm = mainVm.DestinationFolderTree;
                }
                else if (mainVm.SourceFolderTree.Items.Contains(selectedItem))
                {
                    targetTreeVm = mainVm.SourceFolderTree;
                }

                if (targetTreeVm != null)
                {
                    targetTreeVm.ChangeRootDirectory();
                }
            }

            this.Focus();
        }

        private void RestoreWindowFocus()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                this.Focus();
                Keyboard.Focus(this);
            }), DispatcherPriority.Input);
        }

        private void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // 選択されているフォルダアイテムを取得
            var selectedItem = FindSelectedItem(vm.DestinationFolderTree.Items);
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.Path))
            {
                SetStatusText("フォルダを選択してください");
                return;
            }

            // ダイアログで新規フォルダ名を入力
            var dialog = new TextInputDialog("新規フォルダ作成", "フォルダ名を入力してください:", "新しいフォルダ");
            if (dialog.ShowDialog() == true)
            {
                var folderName = dialog.InputText.Trim();
                if (string.IsNullOrEmpty(folderName))
                {
                    SetStatusText("フォルダ名が空です");
                    return;
                }

                try
                {
                    var newFolderPath = System.IO.Path.Combine(selectedItem.Path, folderName);
                    if (System.IO.Directory.Exists(newFolderPath))
                    {
                        SetStatusText("同名のフォルダが既に存在します");
                        return;
                    }

                    System.IO.Directory.CreateDirectory(newFolderPath);
                    SetStatusText($"フォルダ '{folderName}' を作成しました");

                    // ツリーを更新
                    vm.DestinationFolderTree.RefreshFolder(selectedItem.Path);
                }
                catch (Exception ex)
                {
                    SetStatusText($"フォルダ作成エラー: {ex.Message}");
                }
            }

            this.Focus();
        }

        private void AssignShortcut_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var shortcutKey = menuItem.Tag as string;
            if (string.IsNullOrEmpty(shortcutKey)) return;

            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // コンテキストメニューが開かれたTreeViewを特定
            var contextMenu = menuItem.Parent as ContextMenu;
            var treeView = contextMenu?.PlacementTarget as TreeView;
            if (treeView == null) return;

            // 選択されているフォルダアイテムを取得
            FolderTreeItem? selectedItem = null;
            if (treeView == DestinationFolderTreeView)
            {
                selectedItem = FindSelectedItem(vm.DestinationFolderTree.Items);
            }

            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.Path)) return;

            // ショートカット割り当て
            vm.SettingsService.AssignFolderShortcut(selectedItem.Path, shortcutKey);
            vm.SettingsService.Save();

            // 表示名を更新
            selectedItem.AssignedShortcut = shortcutKey;

            // ステータスバーに通知
            SetStatusText($"フォルダ '{selectedItem.Name}' に {shortcutKey} を割り当てました");
        }

        private void RemoveShortcut_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // コンテキストメニューが開かれたTreeViewを特定
            var contextMenu = menuItem.Parent as ContextMenu;
            var treeView = contextMenu?.PlacementTarget as TreeView;
            if (treeView == null) return;

            // 選択されているフォルダアイテムを取得
            FolderTreeItem? selectedItem = null;
            if (treeView == DestinationFolderTreeView)
            {
                selectedItem = FindSelectedItem(vm.DestinationFolderTree.Items);
            }

            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.Path)) return;

            // ショートカット解除
            vm.SettingsService.RemoveFolderShortcut(selectedItem.Path);
            vm.SettingsService.Save();

            // 表示名を更新
            selectedItem.AssignedShortcut = string.Empty;

            // ステータスバーに通知
            SetStatusText($"フォルダ '{selectedItem.Name}' のショートカットを解除しました");
        }

        private FolderTreeItem? FindSelectedItem(System.Collections.ObjectModel.ObservableCollection<FolderTreeItem> items)
        {
            foreach (var item in items)
            {
                if (item.IsSelected) return item;
                var found = FindSelectedItem(item.Children);
                if (found != null) return found;
            }
            return null;
        }

        private void SetStatusText(string text)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.StatusText = text;
            }
        }

        private void UnloadMedia_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.UnloadMedia();
            }
            this.Focus();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var vm = DataContext as MainViewModel;

                if (files.Length == 1 && vm != null && vm.IsSupportedFile(files[0]))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var vm = DataContext as MainViewModel;

                if (files.Length == 1 && vm != null && vm.IsSupportedFile(files[0]))
                {
                    await vm.LoadMediaAsync(files[0]);
                }
            }
        }
    }
}