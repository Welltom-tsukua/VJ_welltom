# VJ_welltom 説明書

Unityで動作するVJ向け映像レイヤー/Pro DJ Link連携アプリです。メイン8レイヤーの映像合成、ローカル動画再生、Hap/MOV再生、YouTube URL解決、3Dオブジェクト表示、Beat Link Trigger連携、MIDI割り当てを扱います。

この公開版には、ソースコードとUnityプロジェクト設定のみを含めています。有料アセット、動画素材、都市モデル、Skybox素材、ビルド成果物、Unityキャッシュは含めていません。

## 動作環境

- Unity: `ProjectSettings/ProjectVersion.txt` に記載のバージョン、またはそれ以降
- OS: Windows想定
- Git
- 必要に応じて `yt-dlp`
- 必要に応じて `ffmpeg` / `ffprobe`
- 必要に応じて Beat Link Trigger

## セットアップ

1. GitHubからこのリポジトリをcloneします。

```powershell
git clone https://github.com/Welltom-tsukua/VJ_welltom.git
```

2. Unity Hubでcloneしたフォルダを開きます。

3. Unityが `Packages/manifest.json` を読み込み、必要なパッケージを取得します。

主な依存パッケージ:

- KlakHap
- Unity LightBeamPerformance
- Unity Collections
- Unity Timeline

4. `Assets/Scenes/Main.unity` を開きます。

5. Playまたはビルドで起動します。

## 公開版で除外しているもの

以下は公開版に含めていません。

- AVProVideo本体
- 動画素材
- Hap/MOV素材
- Skybox画像/マテリアル素材
- Japanese Otaku Cityなどの都市モデル
- Unityの `Library`, `Temp`, `Logs`, `Build`, `UserSettings`
- ローカルキャッシュ

3Dモードで任意のモデルやSkyboxを使う場合は、次の場所に自分で配置します。

```text
Assets/Resources/Models
Assets/Resources/Skyboxes
```

## 基本機能

### レイヤー

メインレイヤーは8つです。各レイヤーに動画、テキスト、YouTube、3Dオブジェクト、Step Sequencerなどのソースを割り当てます。

主な合成モード:

- Alpha
- Add
- Add50
- Mask

Maskはメインレイヤーでのみ有効です。maskレイヤーの輝度で、下段レイヤー群と上段レイヤー群のブレンド量を決めます。

### カラー調整

各レイヤーに対して以下を調整できます。

- Hue
- Invert
- Monochrome
- Edge

Hue、Invert、Monochromeは連続値として扱います。MIDIノブにも割り当てできます。

### Solo / Mute

SoloとMuteは押している間だけ有効になる操作です。MIDI割り当ても同じくモメンタリ動作です。

## 動画再生

### MP4

通常のローカルMP4はUnityの動画再生経路で読み込みます。読み込み中は既存レイヤーの再生を優先する設計です。

### Hap/MOV

HapコーデックのMOVはKlakHapで再生します。HapではないMOVは変換せず、エラーとして扱います。

`ffprobe` がある場合、MOVのコーデック判定に使います。

### YouTube

YouTube再生は `yt-dlp` によるURL解決を前提にしています。ローカルredirect serverが必要なモードでは、起動していない場合にエラーを出します。

必要な外部ツール:

- `yt-dlp`
- ローカルYouTube redirect server

## Pro DJ Link / Beat Link Trigger

Unity内蔵のPro DJ Link受信に加えて、外部Beat Link Triggerをメタデータ/波形取得用に使うモードがあります。

外部BLTモードでは、Beat Link Trigger側が起動していて、Unityから取得できる状態である必要があります。

取得対象:

- プレイヤー状態
- BPM
- beat
- 楽曲名
- アーティスト
- コメント
- ジャケット
- 波形

CDJ-2000nexusなど一部機材では取得できるメタデータに差が出る場合があります。

## Step Sequencer

Step Sequencerはsourceとして扱います。Shift+クリックで、レイヤーまたはブラウザ素材を最大4つまでqueueに追加できます。

ブラウザ素材を追加した場合、内部のStep Sequencer用レイヤーに素材を割り当てて再生します。選択済み素材にはqueue番号が表示されます。

## 2nd Display

UnityのMulti Displayを使って、Display 2にプレビュー/設定画面を表示できます。

- 2nd display ON: Space/Eで切り替える画面をDisplay 2へ表示
- 2nd display OFF: 既存通りDisplay 1内で画面切り替え

Unity Editorで確認する場合は、GameビューのDisplay選択でDisplay 2を選びます。

## 3D Object

3D Objectには以下のモードがあります。

- オブジェクト中心モード
- トンネルモード
- ライティングモード

公開版では都市モデルなどの素材は含めていません。モデルがない場合はフォールバック形状で動作します。

## ビルド

Unity Editorから通常のBuild Settingsでビルドします。プロジェクトにはビルド用Editorスクリプトも含まれています。

ビルド成果物は公開リポジトリには含めません。

## 注意

この公開版は、ローカル開発版から公開可能なファイルだけを切り出したものです。完全な素材付き実行環境ではありません。映像素材、都市モデル、Skybox、外部ツールは利用者側で用意してください。
