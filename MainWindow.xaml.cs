using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            // DataContext をセットすることで XAML と ViewModel が繋がる
            this.DataContext = new MainViewModel(settingsService);
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
                HandleShortcutKey(e.Key.ToString());
                this.Focus();
                return;
            }

            switch (e.Key)
            {   // フォルダ内移動
                case Key.PageUp:
                case Key.PageDown:
                    e.Handled = true;
                    await vm.MoveNextMediaAsync(e.Key == Key.PageDown);
                    this.Focus();
                    return;

                // 書庫内移動
                case Key.Left:
                case Key.Right:
                    if (vm.CurrentMedia is IPageNavigable navigable)
                    {
                        e.Handled = true;
                        if (e.Key == Key.Right) navigable.NextPage();
                        else navigable.PrevPage();
                        this.Focus();
                    }
                    break;
            }
        }

        private async void HandleShortcutKey(string shortcutKey)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            // ファイルを移動して次のファイルパスを取得
            var nextFilePath = vm.QuickMoveToFolder(shortcutKey, (status) =>
            {
                StatusBarText.Text = status;
            });

            // 次のファイルがある場合はロード
            if (!string.IsNullOrEmpty(nextFilePath))
            {
                await vm.LoadMediaAsync(nextFilePath);
            }
            else
            {
                // ファイルがなくなった場合はクリア
                vm.CurrentMedia?.Dispose();
                vm.CurrentMedia = null;
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
            StatusBarText.Text = $"フォルダ '{selectedItem.Name}' に {shortcutKey} を割り当てました";
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
            StatusBarText.Text = $"フォルダ '{selectedItem.Name}' のショートカットを解除しました";
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
    }
}