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

            // ファイルリスト選択変更時にスクロール追従
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.SelectedFileItem) && vm.SelectedFileItem != null)
                    {
                        FileListBox.ScrollIntoView(vm.SelectedFileItem);
                    }
                };
            }
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

            // Alt+1-9によるショートカットジャンプ
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                e.Handled = true;
                var shortcutKey = (e.Key - Key.D0).ToString();
                await MoveFileAndLoadNextAsync(vm.QuickMoveToFolderAsync(shortcutKey, SetStatusText));
                this.Focus();
                return;
            }

            // F2: リネーム
            if (e.Key == Key.F2 && vm.CurrentMedia != null)
            {
                e.Handled = true;
                var currentName = System.IO.Path.GetFileName(vm.CurrentMedia.Path);
                var ext = System.IO.Path.GetExtension(currentName);
                var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(currentName);
                var dialog = new TextInputDialog("リネーム", "新しいファイル名を入力してください:", nameWithoutExt);
                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.InputText.Trim();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        _ = vm.RenameCurrentFileAsync(newName + ext, SetStatusText);
                    }
                }
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
                if (e.Key == Key.C)
                {
                    e.Handled = true;
                    if (vm.CurrentMedia != null)
                    {
                        var fileName = System.IO.Path.GetFileName(vm.CurrentMedia.Path);
                        Clipboard.SetText(fileName);
                        SetStatusText($"ファイル名をコピーしました: {fileName}");
                    }
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
                else if (vm.HasCheckedFiles)
                    await MoveFileAndLoadNextAsync(vm.MoveCheckedToFolderAsync(selected.Path, SetStatusText));
                else
                    await MoveFileAndLoadNextAsync(vm.MoveToFolderAsync(selected.Path, SetStatusText));
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

            // サイドペイン上ではツリー・リストのスクロールを優先
            if (SidePane.IsMouseOver)
                return;

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
        private async Task MoveFileAndLoadNextAsync(Task<string?> moveTask)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var nextFilePath = await moveTask;

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
            this.Focus();
        }

        private void SourceTreeViewItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var tvi = sender as TreeViewItem;
            var item = tvi?.DataContext as FolderTreeItem;
            if (item == null || !item.IsRoot)
            {
                e.Handled = true;
            }
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem tvi && !e.Handled)
            {
                tvi.BringIntoView();
                e.Handled = true;
            }
        }

        private void RestoreWindowFocus()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                this.Focus();
                Keyboard.Focus(this);
            }), DispatcherPriority.Input);
        }

        // --- ルート用コンテキストメニューハンドラ ---

        private void ChangeRootPath_Click(object sender, RoutedEventArgs e)
        {
            var (item, treeVm) = GetContextMenuTarget(sender);
            if (item == null || treeVm == null) return;
            treeVm.ChangeRootPath(item);
            this.Focus();
        }

        private void AddRoot_Click(object sender, RoutedEventArgs e)
        {
            var (_, treeVm) = GetContextMenuTarget(sender);
            if (treeVm == null) return;
            treeVm.AddRootViaDialog();
            this.Focus();
        }

        private void RemoveRoot_Click(object sender, RoutedEventArgs e)
        {
            var (item, treeVm) = GetContextMenuTarget(sender);
            if (item == null || treeVm == null) return;
            // ダミーアイテム（パス未設定）の場合は何もしない
            if (string.IsNullOrEmpty(item.Path)) return;
            treeVm.RemoveRoot(item);
            this.Focus();
        }

        // --- フォルダ操作コンテキストメニューハンドラ ---

        private void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            var (selectedItem, treeVm) = GetContextMenuTarget(sender);
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.Path) || treeVm == null)
            {
                SetStatusText("フォルダを選択してください");
                return;
            }

            var dialog = new TextInputDialog("新規フォルダ作成", "フォルダ名を入力してください:", placeholder: "新しいフォルダ");
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

                    treeVm.RefreshFolder(selectedItem.Path);
                    treeVm.SelectFolder(newFolderPath);
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

            var (selectedItem, _) = GetContextMenuTarget(sender);
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.Path)) return;

            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            vm.SettingsService.AssignFolderShortcut(selectedItem.Path, shortcutKey);
            vm.SettingsService.Save();

            selectedItem.AssignedShortcut = shortcutKey;
            SetStatusText($"フォルダ '{selectedItem.Name}' に Alt+{shortcutKey} を割り当てました");
        }

        private void RemoveShortcut_Click(object sender, RoutedEventArgs e)
        {
            var (selectedItem, _) = GetContextMenuTarget(sender);
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.Path)) return;

            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            vm.SettingsService.RemoveFolderShortcut(selectedItem.Path);
            vm.SettingsService.Save();

            selectedItem.AssignedShortcut = string.Empty;
            SetStatusText($"フォルダ '{selectedItem.Name}' のショートカットを解除しました");
        }

        /// <summary>
        /// コンテキストメニューから対象の FolderTreeItem と FolderTreeViewModel を取得
        /// </summary>
        private (FolderTreeItem? item, FolderTreeViewModel? treeVm) GetContextMenuTarget(object sender)
        {
            var menuItem = sender as MenuItem;
            // MenuItem → ContextMenu → PlacementTarget(TreeViewItem) → DataContext(FolderTreeItem)
            ContextMenu? contextMenu = null;
            DependencyObject? current = menuItem;
            while (current != null)
            {
                if (current is ContextMenu cm) { contextMenu = cm; break; }
                current = LogicalTreeHelper.GetParent(current);
            }

            var treeViewItem = contextMenu?.PlacementTarget as TreeViewItem;
            var folderItem = treeViewItem?.DataContext as FolderTreeItem;

            var vm = DataContext as MainViewModel;
            if (vm == null || treeViewItem == null) return (folderItem, null);

            // TreeViewItem から親の TreeView を探す
            DependencyObject? parent = treeViewItem;
            while (parent != null)
            {
                if (parent is TreeView tv)
                {
                    if (tv == DestinationFolderTreeView) return (folderItem, vm.DestinationFolderTree);
                    if (tv == SourceFolderTreeView) return (folderItem, vm.SourceFolderTree);
                    break;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            return (folderItem, null);
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

        private void FileListBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            this.Focus();
        }

        // --- 描画領域コンテキストメニュー ---

        private void MediaArea_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.CurrentMedia is ArchiveContent archive)
            {
                MenuSinglePage.IsChecked = archive.DisplayMode == ImageDisplayMode.Single;
                MenuSpreadPage.IsChecked = archive.DisplayMode == ImageDisplayMode.Spread;
            }
            else if (vm?.CurrentMedia is PdfContent pdf)
            {
                MenuSinglePage.IsChecked = pdf.DisplayMode == ImageDisplayMode.Single;
                MenuSpreadPage.IsChecked = pdf.DisplayMode == ImageDisplayMode.Spread;
            }
            else
            {
                e.Handled = true; // Archive/PDF以外ではメニューを表示しない
            }
        }

        private void DisplayModeSingle_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.CurrentMedia is ArchiveContent archive)
                archive.DisplayMode = ImageDisplayMode.Single;
            else if (vm?.CurrentMedia is PdfContent pdf)
                pdf.DisplayMode = ImageDisplayMode.Single;
        }

        private void DisplayModeSpread_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.CurrentMedia is ArchiveContent archive)
                archive.DisplayMode = ImageDisplayMode.Spread;
            else if (vm?.CurrentMedia is PdfContent pdf)
                pdf.DisplayMode = ImageDisplayMode.Spread;
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
                    var folder = System.IO.Path.GetDirectoryName(files[0]);
                    if (!string.IsNullOrEmpty(folder))
                        vm.RefreshFileList(folder);
                    await vm.LoadMediaAsync(files[0]);
                }
            }
        }
    }
}