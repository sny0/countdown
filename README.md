# カウントダウン

作業画面の手前に残り時間を表示する、Windows用のカウントダウンアプリです。

## 使い方

1. `Countdown.exe` を起動し、「開始」を押します。数字をドラッグするとウィンドウを移動できます。
2. 右上の「設定」→「時間・背景」で、時間と背景の不透明度を変更できます。背景を完全に透過するには、不透明度を0%にします。
3. 「数字の見た目」で、数字の色・太さ・影の濃さ・影のぼかしを調整できます。

## ビルド

Windowsに.NET SDKと.NET Framework 4.7.1 Targeting Packを用意し、リポジトリのルートで実行します。

```powershell
dotnet build Countdown.csproj -c Release
```

実行ファイルは `bin/Release/net471/Countdown.exe` に生成されます。
