# Basic Drawing

`UCanvas` の最小サンプルです。画面座標（`DrawingMode.ScreenOverlay`）で、脈打つ円・回転する角丸矩形・ベジェ曲線とスプラインを毎フレーム描画します。

## 使い方

1. Package Manager で `p5.Unite` を開き、**Samples** の「Basic Drawing」を **Import** します
2. `Assets/Samples/p5.Unite/<バージョン>/BasicDrawing/BasicDrawing.unity` を開きます
3. Play を押します

## 中身

| ファイル | 内容 |
| --- | --- |
| `BasicDrawing.unity` | カメラと `UCanvas` を持つシーン。そのまま再生できます |
| `Scripts/BasicDrawing.cs` | スケッチ本体。[UCanvas API](https://github.com/popsy-y/p5.Unite#readme) の基本的な使い方（`CreateCanvas` / `Fill` / `Stroke` / `Push` / `Pop` / `Rotate` / `Rect` / `Circle` / `Bezier` / `Spline`）を一通り使っています |

`BasicDrawing` コンポーネントの **Animate** をオフにするとアニメーションが止まり、1フレームをじっくり確認できます。
描画モードを変えたい場合は `BasicDrawing.cs` の `CreateCanvas(DrawingMode.ScreenOverlay)` を書き換えてください（モードごとに座標系が異なります。詳細はパッケージの README を参照）。
