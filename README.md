# カウントダウン

作業画面の手前に残り時間を表示する、Windows用のカウントダウンアプリです。

## インストール

最新リリースの `Countdown-Setup.msi` を開いてインストールすると、スタートメニューから「カウントダウン」を起動できます。アンインストールはWindowsの設定から行えます。[最新リリース](https://github.com/sny0/countdown/releases/latest)

## 使い方

1. `Countdown.exe` を起動すると、最前面の表示ウィンドウと操作ウィンドウが開きます。操作ウィンドウで「開始」を押します。表示側の数字と操作側の上部をそれぞれドラッグして移動できます。
2. 操作ウィンドウの「設定」→「時間・背景」で、時間と表示側の背景の不透明度を変更できます。背景を完全に透過するには、不透明度を0%にします。
3. 「数字の見た目」で、数字の色・太さ・大きさと影を調整できます。数字の大きさは50〜250%の範囲で変更できます。

操作ウィンドウを閉じると表示ウィンドウも閉じ、設定を自動保存します。次回起動時は設定した時間で待機します。

## 更新

起動時と起動中6時間ごとに、GitHubの最新リリースを確認します。「更新あり」または「設定」→「更新」を開き、「更新する」を押すと、MSIをダウンロードしてSHA-256を照合します。照合後にアプリが閉じ、セットアップが自動で進みます。更新が完了すると、新しいバージョンが自動で起動します。

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
