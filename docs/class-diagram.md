# MediaOrganizeViewer クラス構成図

## 全体アーキテクチャ

```mermaid
graph TB
    subgraph MediaOrganizeViewer ["MediaOrganizeViewer (WPFアプリ)"]
        MW[MainWindow]
        MVM[MainViewModel]
        FTVM[FolderTreeViewModel]
        FTI[FolderTreeItem]
        FI[FileItem]
        SS[AppConfigSettingsService]
        FD[FolderDestination]
        TID[TextInputDialog]
    end

    subgraph MediaViewer.Core ["MediaViewer.Core (メディアビューアライブラリ)"]
        MC[MediaContent]
        MF[MediaFactory]
        MMVM[MediaManagerViewModel]
        MVH[MediaViewHost]
        MCB[MediaControlBar]
    end

    MW --> MVM
    MW --> SS
    MVM --> FTVM
    MVM --> MC
    MVM --> MF
    MVM --> FI
    FTVM --> FTI
    SS --> FD
```

## MediaOrganizeViewer — クラス図

```mermaid
classDiagram
    direction TB

    class MainWindow {
        -MainViewModel _viewModel
        +OnPreviewKeyDown() キーボードショートカット処理
        +OnPreviewMouseWheel() マウスホイール処理
        +MoveFileAndLoadNextAsync() ファイル移動+次読込
        -FindMediaControlBar() コントロールバー検索
    }

    class MainViewModel {
        +FolderTreeViewModel SourceFolderTree
        +FolderTreeViewModel DestinationFolderTree
        +MediaContent CurrentMedia
        +ObservableCollection~FileItem~ FileList
        +FileItem SelectedFileItem
        +string StatusText
        -Stack~MoveRecord~ _moveHistory
        -ISettingsService _settingsService
        +LoadMediaAsync(path) メディア読込
        +MoveToFolderAsync(folder) ファイル移動
        +MoveCheckedToFolderAsync(folder) 一括移動
        +QuickMoveToFolderAsync(key) ショートカット移動
        +RenameCurrentFileAsync(newName) リネーム
        +UndoMoveAsync() 元に戻す
        +MoveNextMediaAsync(forward) 前後ナビ
        +RefreshFileList(folder) ファイル一覧更新
        +IsSupportedFile(path) bool
    }

    class FolderTreeViewModel {
        +ObservableCollection~FolderTreeItem~ Items
        +string SelectedPath
        +string RootPath
        -bool _isSource
        -ISettingsService _settingsService
        +SetRoot(path) ルート設定
        +ChangeRootDirectory() フォルダ選択ダイアログ
        +LoadChildren(item) 子フォルダ遅延読込
        +RefreshFolder(path) フォルダ更新
        +SelectNextFolder() 次フォルダ選択
        +SelectPrevFolder() 前フォルダ選択
        +GetFolderByShortcut(key) ショートカット検索
    }

    class FolderTreeItem {
        +string Path
        +string Name
        +bool IsExpanded
        +bool IsSelected
        +string AssignedShortcut
        +string DisplayName
        +ObservableCollection~FolderTreeItem~ Children
        +bool IsRoot
    }

    class FileItem {
        +string Path
        +string Name
        +bool IsChecked
    }

    class ISettingsService {
        <<interface>>
        +string SourceRootPath
        +string DestinationRootPath
        +string LastViewedFilePath
        +List~FolderDestination~ ShortcutFolders
        +int SkipIntervalSeconds
        +Load()
        +Save()
        +AssignFolderShortcut()
        +RemoveFolderShortcut()
        +GetFolderShortcut()
        +GetShortcutFolder()
        +event SettingChanged
    }

    class AppConfigSettingsService {
        -List~FolderDestination~ _shortcutFolders
        JSON直列化でショートカット永続化
    }

    class FolderDestination {
        +string Name
        +string Path
        +string ShortcutKey
    }

    class MoveRecord {
        <<record>>
        +string SourcePath
        +string DestinationPath
    }

    class TextInputDialog {
        +string InputText
    }

    MainWindow --> MainViewModel : 所有
    MainWindow --> AppConfigSettingsService : 設定参照
    MainViewModel --> FolderTreeViewModel : Source/Dest 2つ所有
    MainViewModel --> FileItem : コレクション管理
    MainViewModel --> MoveRecord : Undo履歴(Stack)
    MainViewModel ..> ISettingsService : 依存
    FolderTreeViewModel --> FolderTreeItem : ツリー管理
    FolderTreeViewModel ..> ISettingsService : 依存
    AppConfigSettingsService ..|> ISettingsService : 実装
    AppConfigSettingsService --> FolderDestination : ショートカット管理
    FolderTreeItem --> FolderTreeItem : Children(再帰)
```

## MediaViewer.Core — クラス図

### コンテンツ型の継承階層

```mermaid
classDiagram
    direction TB

    class MediaContent {
        <<abstract>>
        +string Path
        +string Name
        +string StatusText*
        +bool IsLoading
        +LoadAsync()* Task
        +Dispose() void
    }

    class IPageNavigable {
        <<interface>>
        +int CurrentPage
        +int TotalPages
        +NextPage()
        +PrevPage()
    }

    class IImageMedia {
        <<interface>>
        +BitmapSource LeftImage
        +BitmapSource RightImage
        +ImageDisplayMode DisplayMode
    }

    class ImageDisplayMode {
        <<enum>>
        Single
        Spread
    }

    class SingleImageContent {
        +BitmapSource LeftImage
        画像1枚表示
    }

    class GifContent {
        +Uri SourceUri
        アニメGIF表示
    }

    class VideoContent {
        動画再生コンテナ
    }

    class AudioContent {
        音声再生コンテナ
    }

    class ArchiveContent {
        +int CurrentPage
        +int TotalPages
        +ImageDisplayMode DisplayMode
        +BitmapSource LeftImage
        +BitmapSource RightImage
        +NextPage() / PrevPage()
        ZIPアーカイブ内の画像閲覧
    }

    class PdfContent {
        +int CurrentPage
        +int TotalPages
        +ImageDisplayMode DisplayMode
        +BitmapSource LeftImage
        +BitmapSource RightImage
        +NextPage() / PrevPage()
        PDFページ描画(SkiaSharp)
    }

    class TextContent {
        +string Text
        テキストファイル表示
    }

    class UnsupportedContent {
        +string Message
        未対応形式のフォールバック
    }

    MediaContent <|-- SingleImageContent
    MediaContent <|-- GifContent
    MediaContent <|-- VideoContent
    MediaContent <|-- AudioContent
    MediaContent <|-- ArchiveContent
    MediaContent <|-- PdfContent
    MediaContent <|-- TextContent
    MediaContent <|-- UnsupportedContent

    IPageNavigable <|.. ArchiveContent
    IPageNavigable <|.. PdfContent
    IImageMedia <|.. SingleImageContent
    IImageMedia <|.. ArchiveContent
    IImageMedia <|.. PdfContent
    IImageMedia --> ImageDisplayMode
```

### ファクトリ・ViewModel・View

```mermaid
classDiagram
    direction TB

    class MediaFactory {
        <<static>>
        -Dictionary extensions→creators
        +Register(ext, creator)$ 拡張子登録
        +Create(path)$ MediaContent生成
    }

    class MediaManagerViewModel {
        +MediaContent CurrentContent
        +string StatusText
        +bool IsLoading
        +ICommand NextPageCommand
        +ICommand PrevPageCommand
        +OpenFileAsync(path) ファイルを開く
    }

    class IMediaControl {
        <<interface>>
        +string StatusText
        +ICommand NextPageCommand
        +ICommand PrevPageCommand
        +OpenFileAsync(path)
    }

    class MediaViewHost {
        <<UserControl>>
        +MediaContent CurrentMedia (DependencyProperty)
        DataTemplateでコンテンツ型に応じたView切替
    }

    class MediaControlBar {
        <<UserControl>>
        +int SkipSeconds
        +Attach(mediaPlayer) プレーヤー接続
        +Detach() プレーヤー切断
        +SkipBySeconds(sec) シーク
        +event NextMediaRequested
        +event PrevMediaRequested
        +event SkipIntervalChanged
    }

    class VideoContentView {
        <<UserControl>>
        -LibVLC _libVLC
        -MediaPlayer _mediaPlayer
        LibVLCSharpで動画再生
    }

    class AudioContentView {
        <<UserControl>>
        -LibVLC _libVLC
        -MediaPlayer _mediaPlayer
        LibVLCSharpで音声再生
    }

    class ArchiveContentView {
        <<UserControl>>
        XAMLバインディングのみ
    }

    class PdfContentView {
        <<UserControl>>
        XAMLバインディングのみ
    }

    class TextContentView {
        <<UserControl>>
        XAMLバインディングのみ
    }

    MediaManagerViewModel ..|> IMediaControl
    MediaManagerViewModel --> MediaFactory : 生成依頼
    MediaManagerViewModel --> MediaContent : 所有

    MediaViewHost --> MediaContent : DataTemplate切替

    VideoContentView --> MediaControlBar : 操作UI連携
    AudioContentView --> MediaControlBar : 操作UI連携
```

## プロジェクト間の依存関係とデータフロー

```mermaid
graph LR
    subgraph 外部ライブラリ
        CT[CommunityToolkit.Mvvm]
        VLC[LibVLCSharp]
        PDF[PDFtoImage + SkiaSharp]
        GIF[XamlAnimatedGif]
    end

    subgraph MainApp ["MediaOrganizeViewer"]
        MW2[MainWindow]
        MVM2[MainViewModel]
    end

    subgraph Core ["MediaViewer.Core"]
        MF2[MediaFactory]
        MC2[MediaContent群]
        Views[View群]
    end

    MainApp -->|プロジェクト参照| Core
    MainApp --> CT
    Core --> VLC
    Core --> PDF
    Core --> GIF

    MVM2 -->|"MediaFactory.Create(path)"| MF2
    MF2 -->|"適切なMediaContent生成"| MC2
    MC2 -->|"DataTemplate切替"| Views
```

## 主要データフロー

```mermaid
sequenceDiagram
    participant User as ユーザー
    participant MW as MainWindow
    participant VM as MainViewModel
    participant FT as FolderTreeViewModel
    participant MF as MediaFactory
    participant MC as MediaContent
    participant View as MediaViewHost

    User->>MW: フォルダ選択
    MW->>VM: SourceFolderTree.SelectedPath変更
    VM->>VM: RefreshFileList()
    VM-->>MW: FileList更新

    User->>MW: ファイル選択
    MW->>VM: SelectedFileItem変更
    VM->>MF: Create(path)
    MF-->>VM: MediaContent (具象型)
    VM->>MC: LoadAsync()
    MC-->>VM: 読込完了
    VM-->>View: CurrentMedia変更
    View->>View: DataTemplateで適切なView表示

    User->>MW: Alt+1〜9 (ショートカット移動)
    MW->>VM: QuickMoveToFolderAsync(key)
    VM->>VM: File.Move + MoveRecord記録
    VM->>VM: MoveNextMediaAsync(forward)
    VM-->>MW: 次のメディア表示
```

## デザインパターン一覧

| パターン | 適用箇所 | 説明 |
|---------|---------|------|
| **Factory** | `MediaFactory` | ファイル拡張子→適切なMediaContentサブクラス生成 |
| **Template Method** | `MediaContent.LoadAsync()` | 基底クラスで共通フロー、サブクラスで具体的読込 |
| **Strategy** | `IPageNavigable` | ZIP/PDFで共通のページ送りインターフェース |
| **Observer** | `INotifyPropertyChanged` | 全ViewModel・Modelでデータバインディング |
| **MVVM** | 全体 | View↔ViewModel↔Modelの責務分離 |
| **Lazy Loading** | `FolderTreeViewModel` | フォルダ展開時に子要素を読込 |
| **Undo (Memento的)** | `Stack<MoveRecord>` | ファイル移動の取消し履歴 |
