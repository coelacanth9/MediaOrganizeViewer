# MediaOrganizeViewer 設計仕様書

## 1. アプリケーション概要
本アプリケーションは `MediaViewer.Core` を基盤とし、ローカルストレージ内のメディアファイルを効率的に閲覧・整理（フォルダ移動等）するためのツールである。

- **2画面ツリー構成**: 「移動元（Source）」と「移動先（Destination）」の2つのフォルダツリーを保持し、直感的な整理を可能にする。
- **メッセンジャーによる疎結合**: `WeakReferenceMessenger` を活用し、ViewModel 間の通信（フォルダ選択通知等）を疎結合に行う。
- **キーボード主体の操作**: ページ送りやファイル移動をキーボードショートカットで行えるよう設計されている。

## 2. 主要コンポーネントの役割

### 2.1 MainViewModel (全体制御)
アプリケーションのメインハブとなる ViewModel。
- **メディア管理**: `CurrentMedia` (MediaContent) を保持し、表示内容を管理する。
- **メッセージ受信**: `IRecipient<FolderSelectedMessage>` を実装し、ツリーでフォルダが選択された際のロード処理を行う。
- **ファイルナビゲーション**: フォルダ内の「次/前」のファイルへ移動する `MoveNextMediaAsync` などのロジックを実装。

### 2.2 FolderTreeViewModel / FolderTreeItem (ツリー管理)
Windowsのエクスプローラー形式のツリー構造を管理。
- **遅延ロード**: `LoadChildren` メソッドにより、展開されたタイミングでサブフォルダを読み込む。
- **多目的利用**: `_isSource` フラグにより、移動元（Source）と移動先（Destination）のどちらの役割でも動作する。
- **ショートカット表示**: `FolderTreeItem` は `AssignedShortcut` プロパティを持ち、整理用のショートカットキーとの紐付けを表示する。

### 2.3 TextInputDialog (汎用ダイアログ)
フォルダ作成時などに使用する、テキスト入力用のカスタムウィンドウ。
- `DialogResult` 方式で、ViewModel 側から入力を受け取ることが可能。

## 3. 実装のポイント

### 3.1 メッセージング (CommunityToolkit.Mvvm)
- `FolderSelectedMessage`: ツリーから送信され、`MainViewModel` が受信してファイルロードを開始する。
- 送信元が「移動元」か「移動先」かを判別するためのプロパティを持つ。

### 3.2 ユーザー操作のハンドリング
- **PreviewKeyDown**: ウィンドウレベルでキー入力をフックし、ページ送り（左右キー）やファイル送り（PageUp/Down）を制御。
- **フォーカス管理**: ツリー操作後に `this.Focus()` を実行し、メディア操作のキーボードイベントが途切れないよう制御。

## 4. 実装ガイドライン
- **MVVMパターンの遵守**: `ObservableProperty` や `RelayCommand` を活用し、UIとロジックを分離する。
- **Coreとの連携**: メディアの表示は `Core.View.MediaViewHost` を通じて行い、表示ロジックは Core 側に委ねる。
- **リソース解放**: コンテンツの切り替え時には `CurrentMedia` の `Dispose()` を適切に呼び出す。

## 5. ディレクトリ構造 (MediaOrganizeViewer)
MediaOrganizeViewer/
├── Messages/             # メッセンジャー用定義 (FolderSelectedMessage.cs)
├── ViewModels/           # ビジネスロジック (MainViewModel, FolderTreeViewModel)
├── Views/                # 画面定義 (MainWindow, TextInputDialog)
├── Models/               # データ構造 (FolderTreeItem.cs)
└── App.xaml              # エントリポイント