  # MediaOrganizeViewer

  メディアファイルを閲覧しながら、キーボード操作でフォルダへ振り分け・整理できるWindows向けデスクトップアプリケーションです。

<img width="986" height="693" alt="スクリーンショット 2026-02-06 172907" src="https://github.com/user-attachments/assets/b27fb4c3-4d50-4acc-b7fe-382189e828dc" />

<img width="986" height="693" alt="スクリーンショット 2026-02-06 172859" src="https://github.com/user-attachments/assets/87dc52ae-d727-490a-9343-d3cc9953dd68" />


  ## 主な機能

  ### 描画領域（右側）
  - **多彩なメディア形式に対応**:
    - 画像: JPG, PNG, BMP, WebP
    - アニメーションGIF
    - ZIP書庫内の画像（見開き表示対応）
    - 動画: MP4（LibVLCによる再生）
    - 音声: MP3, WAV, FLAC（LibVLCによる再生）
    - PDF（見開き・ページ送り対応）
  - **再生コントロール**: 動画・音声共通の再生/一時停止、シークバー、音量調整
  - **ファイルドロップ**: ファイルをドラッグ＆ドロップで直接開ける

  ### サイドペイン（左側）
  - **移動先フォルダツリー（上段）**: 整理先フォルダを表示。右クリックでショートカットキー（Alt+1〜9）を割り当て可能
  - **移動元フォルダツリー（中段）**: 閲覧対象のフォルダを表示。フォルダ選択で自動的にメディアを読み込み
  - **ファイル一覧（下段）**: 選択フォルダ内のメディアファイルをリスト表示。クリックで直接ファイルを開ける
    - チェックボックスで複数選択し、ショートカットキーやSpaceで一括移動が可能

  ### 操作
  - **キーボード主体**: Alt+1〜9やSpaceでワンキー振り分け、F2でリネーム、Ctrl+Zでundo
  - **マウスホイール**: 描画領域ではページ送り、サイドペインではスクロール

  ## 使い方

  ### 画面構成

 |領域|説明|
|:----|:----|
|左上ツリー|移動先（整理先）フォルダ。ルートをクリックでフォルダ選択。右クリックでショートカットキー（Alt+1〜9）を割り当て可能|
|左中ツリー|移動元フォルダ。フォルダを選択すると、その中の最初のメディアが表示される|
|左下リスト|ファイル一覧。選択フォルダ内のメディアファイルを表示。チェックで複数選択可能|
|右側|メディア表示領域。ファイルをドロップして開くことも可能|

<img width="341" height="548" alt="image" src="https://github.com/user-attachments/assets/034f1e95-f4d2-4020-b717-ce894de6884e" />

  ### 基本的な操作フロー

  1. 左下ツリーで閲覧したいフォルダを選択（または右側にファイルをドロップ）
  2. 左上ツリーで移動先フォルダを選択、またはショートカットキーを設定、または、Filesペインでアイテムをクリック
  3. 画像を確認しながら、スペースキーまたはAlt+1〜9で振り分け（チェック済みファイルがあれば一括移動）
  4. 自動的に次のファイルが表示される

  ### キーボードショートカット

  | キー | 動作 |
  |------|------|
  | Space | 表示中のファイルを、移動先ツリーで選択中のフォルダへ移動（チェック済みがあれば一括移動） |
  | Alt+1〜9 | 表示中のファイルを、割り当てたフォルダへ移動（チェック済みがあれば一括移動） |
  | F2 | 表示中のファイルをリネーム |
  | Ctrl+Z | ファイル移動を元に戻す（Undo） |
  | ↑ / ↓ | 同じフォルダ内の前 / 次のファイルを表示 |
  | PageUp / PageDown | 同上 |
  | ← / → | 書庫/PDF内の次ページ / 前ページ、動画/音声のスキップ |
  | マウスホイール | 書庫/PDF内のページ送り（サイドペイン上ではスクロール） |
  | Ctrl+↑ / Ctrl+↓ | ツリー上のフォルダ移動 |
  | Ctrl+← / Ctrl+→ | ツリーの展開 / 縮小 |
  | Esc | 表示中のファイルを閉じる |

  ## 動作環境

  - Windows 10 / 11
  - .NET 8.0

## 設計のポイント

  本アプリケーションは、拡張性と保守性を重視した設計を採用しています。

  ### アーキテクチャ

  MediaOrganizeViewer (アプリケーション本体)　<br>
　  └── MediaViewer.Core (再利用可能なライブラリ)

-   ファイルを整理する為のUIとメディア処理部分を分離してCoreライブラリとして独立させています。
- Coreは表示エリアのViewを含みますが（密結合）、用途限定のためあえてセットにしたほうが使いやすいと判断しました。

  ### Factoryパターンによるメディア対応

  新しいファイル形式への対応は、以下の2ステップで完了します。


1. MediaFactoryへの登録
```csharp
// 拡張子と生成ロジックをDictionaryで管理
MediaFactory.Register(".mp4", path => new VideoContent(path));
```
2. MediaTemplates.xamlへのDataTemplate追加
   
  シンプルな場合は、対応するコントロールを直接配置するだけで完了します。
  ```xml
  <DataTemplate DataType="{x:Type local:VideoContent}">
      <MediaElement Source="{Binding Path}" />
  </DataTemplate>
```
　複雑な場合は、専用のUserControlを作成して参照します。
  ```xml
  <DataTemplate DataType="{x:Type local:VideoContent}">
      <local:VideoContentView />
  </DataTemplate>
```

これにより、既存コードを変更することなく、対応形式を拡張できる設計です（Open-Closed Principle）。


#### MVVMパターンと疎結合

  - CommunityToolkit.Mvvm を採用し、ViewとViewModelを分離
  - WeakReferenceMessenger によるViewModel間の疎結合な通信
  - DataTemplate によるデータ型と表示Viewの自動マッピング


#### 主要なクラス構成
|クラス|役割|
|:----|:----|
|MediaContent|全メディアの基底クラス。非同期ロード、リソース解放を共通化|
|MediaFactory|拡張子に応じた具象クラスを生成するファクトリ|
|MediaViewHost|データ型に応じてViewを自動切替するホストコントロール|
|MediaControlBar|動画・音声で共用する再生コントロールUI|
|FolderTreeViewModel|元/移動先で共用可能なツリー管理|

#### リソース管理

  - IDisposable による確実なリソース解放
  - BitmapSource.Freeze() によるスレッド間共有とメモリ最適化
  - 非同期ロード（LoadAsync）でUIスレッドをブロックしない設計

### 今後の予定
- 整理先フォルダの選択自由度向上

### ライセンス

  本リポジトリのソースコードは [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) ライセンスの下で公開しています。
  学習・参考・改変は自由ですが、商用利用は禁止です。
  詳細は [LICENSE](LICENSE) ファイルを参照してください。

  使用しているサードパーティライブラリのライセンスについては
  [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
