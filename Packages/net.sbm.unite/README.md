# p5.Unite

p5.js風のクリエイティブコーディングをUnityで楽しむためのライブラリです。

`UCanvas`を使って、図形を描けます。

## 動作環境

- Unity 6000.2 以降
- **URP（Universal Render Pipeline）が必要です。** 描画シェーダーがURPのシェーダーライブラリをインクルードしています
- 依存パッケージ（`com.unity.burst`, `com.unity.mathematics`, `com.unity.render-pipelines.core`, `com.unity.render-pipelines.universal`）はUPMが自動で解決します

## インストール

Package Managerの「Install package from git URL...」に次を入力します。

```
https://github.com/popsy-y/p5.Unite.git?path=Packages/net.sbm.unite#v0.2.1
```

`#` の後ろはタグです。最新のタグに置き換えてください。

## 使い方

`UCanvas` をGameObjectに追加し、`CreateCanvas()` を呼んでから図形を描きます。

```csharp
using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;

[RequireComponent(typeof(UCanvas))]
public sealed class Sketch : MonoBehaviour
{
    private UCanvas _canvas;

    private void Start()
    {
        _canvas = GetComponent<UCanvas>();
        _canvas.CreateCanvas(DrawingMode.Screen);
    }

    private void Update()
    {
        _canvas.Fill(new Color(0.15f, 0.75f, 0.95f));
        _canvas.Circle(new Vector3(400f, 300f, 0f), 120f);
    }
}
```

`UCanvas` をアタッチすると、描画に使うシェーダー（`UniteShaderLibrary`）が自動で設定されます。通常は意識する必要はありません。

### 座標系（DrawingMode）

| モード | 説明 |
| --- | --- |
| `ScreenOverlay` | ピクセル座標。深度を使わず最前面に描画 |
| `Screen` | ピクセル座標。深度を使う |
| `NormalizedScreenOverlay` | 0..1 の正規化座標。深度を使わず最前面に描画 |
| `NormalizedScreen` | 0..1 の正規化座標。深度を使う |
| `World` | ワールド座標 |
| `CanvasLocal` | `UCanvas` のGameObjectにローカルな座標 |

## サンプル

Package Managerで `p5.Unite` を開き、**Samples** の「Basic Drawing」を **Import** すると、そのまま再生できるサンプルシーンが入ります。

```
Assets/Samples/p5.Unite/<バージョン>/BasicDrawing/BasicDrawing.unity
```

## asmdefを使う場合

`Assembly Definition` でコードを分割している場合は、`Unite.Runtime` への参照を追加してください。

## 開発者向け

リポジトリのルートが開発用のUnityプロジェクトです。パッケージ本体は `Packages/net.sbm.unite` に埋め込みパッケージとして置かれているため、エディタでそのまま編集できます。

- 開発用スクリプト: `Assets/Scripts/Dev`
- サンプル（常時コンパイル）: `Assets/Scripts/Examples`

パッケージ構成とリリース手順は、リポジトリのルートにある `PACKAGE_MIGRATION.md` を参照してください。
