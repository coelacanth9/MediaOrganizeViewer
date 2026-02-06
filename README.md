  # MediaOrganizeViewer

  メディアファイルを閲覧しながら、キーボード操作でフォルダへ振り分け・整理できるWindows向けデスクトップアプリケーションです
  。

<img width="986" height="693" alt="スクリーンショット 2026-01-23 121027" src="https://github.com/user-attachments/assets/289eb34d-c4f9-4339-9b91-15e625a54af6" />


  ## 主な機能

  - **キーボード主体の操作**: ショートカットキー（F1〜F5）にフォルダを割り当て、ワンキーでファイルを移動
  - **2ペインのフォルダツリー**: 移動元と移動先を同時に表示し、直感的な整理が可能
  - **多彩なメディア形式に対応**:
    - 画像: JPG, PNG, BMP, WebP
    - アニメーションGIF
    - ZIP書庫内の画像（見開き表示対応）
    - 動画: MP4（LibVLCによる再生）
    - 音声: MP3, WAV, FLAC（LibVLCによる再生）
    - PDF（見開き・ページ送り対応）
  - **再生コントロール**: 動画・音声共通の再生/一時停止、シークバー、音量調整

  ## 使い方

  ### 画面構成

 |領域|説明|
|:----|:----|
|左上ツリー|移動先（整理先）フォルダ。ルートをクリックでフォルダ選択。右クリックでショートカットキー（F1〜F5）を割り当て可能|
|左下ツリー|移動元フォルダ。フォルダを選択すると、その中の最初のメディアが表示される|
|右側|メディア表示領域。ファイルをドロップして開くことも可能|


  <img width="345" height="314" alt="右クリックメニュー"
  src="https://github.com/user-attachments/assets/161c3ccb-32c9-4ff0-a478-b5e39a834d13" />

  ### 基本的な操作フロー

  1. 左下ツリーで閲覧したいフォルダを選択（または右側にファイルをドロップ）
  2. 左上ツリーで移動先フォルダを選択、またはショートカットキーを設定
  3. 画像を確認しながら、スペースキーまたはF1〜F5で振り分け
  4. 自動的に次のファイルが表示される

  ### キーボードショートカット

  | キー | 動作 |
  |------|------|
  | Space | 表示中のファイルを、移動先ツリーで選択中のフォルダへ移動 |
  | F1〜F5 | 表示中のファイルを、割り当てたフォルダへ移動 |
  | Ctrl+Z | ファイル移動を元に戻す（Undo） |
  | ↑ / ↓ | 同じフォルダ内の前 / 次のファイルを表示 |
  | PageUp / PageDown | 同上（後方互換） |
  | ← / → | 書庫/PDF内の次ページ / 前ページを表示 |
  | マウスホイール | 書庫/PDF内のページ送り |
  | Ctrl+↑ / Ctrl+↓ | 移動元ツリーの前 / 次のフォルダを選択 |
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
- ショートカットキーの登録キーを任意のキーに変更可能にする

### ライセンス

  本リポジトリのソースコードは [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) ライセンスの下で公開しています。
  学習・参考・改変は自由ですが、商用利用は禁止です。
  詳細は [LICENSE](LICENSE) ファイルを参照してください。

  使用しているサードパーティライブラリのライセンスについては
  [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
