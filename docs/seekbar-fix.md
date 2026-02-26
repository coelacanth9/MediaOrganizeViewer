# WPF Slider + LibVLCSharp シークバー問題と解決策

## 問題の概要

WPF の `Slider` コントロールをシークバーとして使い、LibVLCSharp の `MediaPlayer` と組み合わせた場合、
**スライダーの任意の位置をクリックしてもその位置にジャンプできず、元の再生位置に戻ってしまう**という問題が発生した。

## うまくいかない理由

### 1. WPF Slider の内部イベント処理が干渉する

WPF の `Slider` は内部に `Track`・`Thumb`・`RepeatButton` を持つ複合コントロールで、
マウス操作に対して独自の内部処理を行う。

- `IsMoveToPointEnabled="True"` を設定するとクリック位置にジャンプはするが、
  Slider は同時に Thumb のドラッグ処理も開始する
- Slider はクラスレベルのイベントハンドラ（`EventManager.RegisterClassHandler`）で
  `Thumb.DragStarted` / `DragCompleted` 等を処理しており、
  **`e.Handled = true` を設定しても内部処理を完全には抑止できない**
- この内部処理が、プログラムから設定した `Value` を遅延的に上書きすることがある

### 2. VLC の Position 非同期問題が複合する

LibVLCSharp の `MediaPlayer.Position` セッターは非同期で動作する:

- セット直後にゲッターを読むと、セットした値がそのままエコーバックされる（~800ms）
- エコー期間後に初めて VLC の実際のデコード位置が返る
- シーク自体は成功しているが、タイマーで `Position` を読んでスライダーに反映する際、
  古い値を読んでしまうとスライダーが元の位置に戻る

### 3. 両方が組み合わさって解決困難になる

```
ユーザーがクリック
  → Slider 内部が Value を設定（正しい位置）
  → Slider 内部の Thumb 処理が Value を戻す（干渉）
  → タイマーが VLC Position を読む（まだ古い値の場合がある）
  → スライダーが元の位置に戻る
```

この問題は VLC 側とWPF Slider 側の両方に原因があるため、
片方だけ対策しても完全には解決しなかった。

## 試行した対策と結果

| # | アプローチ | 結果 |
|---|-----------|------|
| 1 | `IsMoveToPointEnabled` + PreviewMouse イベント | クリックで一瞬移動するが戻る |
| 2 | 500ms DispatcherTimer で遅延解除 | 改善せず |
| 3 | `_seekTarget` 方式（シーク到達検知） | 少し改善、まだ戻る |
| 4 | 完全手動マウス制御 + `e.Handled = true` | 改善せず（Slider の内部クラスハンドラを抑止できない） |
| 5 | Position 直接指定 + 連続確認ガード | 体感あまり変わらず |
| 6 | エコー待ち 800ms | VLC は成功しているのに UI が戻るケースが残る |
| 7 | シークリトライ + スピナー UI | リトライが即成功判定され発動しない |
| 8 | エコー待ち + 確認 + リトライ全部盛り | ガード中は正しいがガード解除後に戻る |

**共通の失敗パターン**: VLC のシーク自体は成功しているのに、Slider が元に戻る。

## 解決策: Slider を表示専用にする

### 発想の転換

`SkipBySeconds`（秒スキップボタン）は `_mediaPlayer.Time` を直接設定するだけで正常に動作していた。
このメソッドでは Slider のマウスイベントが一切絡まない。

→ **Slider の入力処理が根本原因**と判断し、Slider からマウス入力を完全に切り離した。

### 実装

```xml
<Grid Grid.Column="4" Margin="4,0,4,0">
    <!-- 表示専用（入力を受け付けない） -->
    <Slider x:Name="SeekBar"
            VerticalAlignment="Center"
            Minimum="0" Maximum="1000"
            IsHitTestVisible="False"/>
    <!-- 透明オーバーレイでマウス入力を処理 -->
    <Border x:Name="SeekOverlay" Background="Transparent"
            MouseLeftButtonDown="OnSeekOverlayMouseDown"
            MouseLeftButtonUp="OnSeekOverlayMouseUp"
            MouseMove="OnSeekOverlayMouseMove"/>
</Grid>
```

**ポイント:**

1. **Slider に `IsHitTestVisible="False"`** を設定し、マウスイベントを一切受け付けない表示専用にする
2. **透明な Border をオーバーレイ**として同じグリッドセルに配置し、クリック・ドラッグを処理する
3. マウス座標から再生位置を計算し、**`_mediaPlayer.Time` で直接シーク**（SkipBySeconds と同じ方式）
4. シーク後 **1.5 秒間のクールダウン**でスライダーを目標値にピン留めし、VLC の非同期問題を回避

### なぜこれで解決するか

```
ユーザーがクリック
  → Border（オーバーレイ）がマウスイベントを受ける
  → Slider の内部処理は一切動かない（IsHitTestVisible=False）
  → _mediaPlayer.Time で直接シーク（実績のある方式）
  → 1.5秒間スライダーを目標値にピン留め（VLC の非同期を待つ）
  → クールダウン後、通常のタイマー更新に復帰
```

Slider の内部イベント処理が完全にバイパスされるため、干渉が起きない。

## 教訓

- WPF の `Slider` はクラスレベルハンドラを持つため、`e.Handled = true` では内部動作を完全に抑止できない
- 複合コントロールを入力デバイスとして使うとき、内部実装と競合する場合は
  **表示専用にして入力を別要素で処理する**のが確実
- 問題の切り分けとして「正常に動く類似機能（SkipBySeconds）との差分は何か」に着目すると根本原因が見える
