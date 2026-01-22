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

        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as FolderTreeItem;
            if (selectedItem == null) return;

            var mainVm = this.DataContext as MainViewModel;
            if (mainVm == null) return;

            var treeView = sender as TreeView;
            if (treeView == null) return;

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
    }
}