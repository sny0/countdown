# カウントダウン

作業画面の手前に残り時間を表示する、Windows用のカウントダウンアプリです。

## インストール

最新リリースの `Countdown-Setup.msi` を開いてインストールすると、スタートメニューから「カウントダウン」を起動できます。アンインストールはWindowsの設定から行えます。[最新リリース](https://github.com/sny0/countdown/releases/latest)

## 使い方

1. `Countdown.exe` を起動し、「開始」を押します。数字をドラッグするとウィンドウを移動できます。
2. 右上の「設定」→「時間・背景」で、時間と背景の不透明度を変更できます。背景を完全に透過するには、不透明度を0%にします。
3. 「数字の見た目」で、数字の色・太さ・影の濃さ・影のぼかしを調整できます。

アプリを閉じると設定を自動保存し、次回起動時に設定した時間で待機します。

## ビルド

Windowsに.NET SDKと.NET Framework 4.7.1 Targeting Packを用意し、リポジトリのルートで実行します。

```powershell
dotnet build Countdown.csproj -c Release
```

実行ファイルは `bin/Release/net471/Countdown.exe` に生成されます。

MSIをビルドする場合は、WiX Toolset 3.14.1のバイナリZIPを展開し、そのパスを指定します。[WiX Toolset 3.14.1](https://github.com/wixtoolset/wix3/releases/tag/wix3141rtm)

```powershell
.\installer\Build-Installer.ps1 -WixDirectory 'C:\path\to\wix'
```

MSIは `bin/Release/v<バージョン>/Countdown-Setup.msi` に生成されます。バージョンは `Countdown.csproj` の `Version` から取得します。GitHubリリースには `Countdown-Setup.msi` を添付します。
