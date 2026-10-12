# パッケージ構成と開発フロー

p5.Unite を「Unity パッケージとして配布できる形」と「エディタで普通に開発できる形」の
両立として、**リポジトリのルートを開発用 Unity プロジェクト、ライブラリ本体を埋め込み
パッケージ**という構成にしています。

```
p5.Unite/                          ← リポジトリのルート = Unity 開発プロジェクト
├─ Assets/
│   ├─ Scenes/Unite.unity          （開発用シーン）
│   ├─ Scripts/Dev/                （開発用スクリプト: CanvasTester など。配布物には入らない）
│   ├─ Scripts/Examples/           （常時コンパイルされるサンプル置き場）
│   └─ DevResources/
├─ Packages/
│   ├─ manifest.json               （testables に net.sbm.unite を登録）
│   └─ net.sbm.unite/              ← ライブラリ本体（= 配布されるパッケージ）
│       ├─ package.json
│       ├─ README.md
│       ├─ CHANGELOG.md
│       ├─ Runtime/                （アセンブリ Unite.Runtime / 名前空間 Unite.*）
│       │   ├─ Canvas/ Coordinate/ Drawing/ Shaders/
│       │   ├─ Resources/UniteShaderLibrary.asset   ← シェーダー参照を保持
│       │   ├─ AssemblyInfo.cs
│       │   └─ Unite.Runtime.asmdef
│       └─ Tests/EditMode/         （アセンブリ Unite.Runtime.Tests）
└─ ProjectSettings/
```

`Packages/` の直下に置いたフォルダは Unity が**埋め込みパッケージ**として扱うため、
`manifest.json` に依存を書く必要はありません。エディタからは通常のアセットと同じように
編集・再生できます。

## シェーダーがビルドから消えない仕組み

`Shader.Find` はビルド時に依存関係として記録されないため、シェーダーは Player ビルドから
ストリップされます（エディタでは見つかるので気付きにくい不具合でした）。

対策として、パッケージ内に **`UniteShaderLibrary`（ScriptableObject）** を置いています。

```
Packages/net.sbm.unite/Runtime/Resources/UniteShaderLibrary.asset
    └ 参照 ─ ScreenDrawing.shader / WorldDrawing.shader / CanvasLocalDrawing.shader
```

- このアセットは `Resources` フォルダ内にあるため**必ずビルドに含まれ**、参照先の
  シェーダーも一緒に含まれます。ユーザー側の設定作業は不要です。
- `UCanvas` はシリアライズフィールド `_shaderLibrary` でこのアセットを参照します。
  `Reset()` / `OnValidate()` で自動的に設定されるので、**UCanvas をアタッチするだけで
  参照が入ります**。
- 実行時に `UCanvas` を `AddComponent` した場合など、参照が空でも
  `UniteShaderLibrary.Instance`（`Resources.Load`）が解決します。
- シェーダーを追加・変更したときは `UniteShaderLibrary.asset` の参照も更新してください。
  `ShaderLibraryTests` が全 `DrawingMode` にシェーダーが割り当てられているか検証します。

`_UNITE_DEPTH_PREPASS` は `#pragma multi_compile_local` なので全バリアントがビルドに
含まれます。将来 `shader_feature` に変更する場合は ShaderVariantCollection が必要です。

## 日常の開発

| やりたいこと | 置き場所 |
| --- | --- |
| ライブラリ本体の実装 | `Packages/net.sbm.unite/Runtime` |
| 手元で試すスクリプト（配布しない） | `Assets/Scripts/Dev` |
| 常にコンパイルされるサンプル | `Assets/Scripts/Examples` |
| 配布用サンプル | `Packages/net.sbm.unite/Samples~`（後述） |

`Assets/Scripts/Dev` と `Assets/Scripts/Examples` の asmdef は `Unite.Runtime` を
参照しています。アセンブリ名を変えない限り、この参照は書き換え不要です。

### サンプル（Samples~）

`Samples~` は末尾のチルダのため Unity のアセットパイプラインが**フォルダごと無視**します。
つまり:

- 中の `.cs` はコンパイルされない（エディタを開いていても検証されない）
- Package Manager の「Samples」→ **Import** を押したときに
  `Assets/Samples/<表示名>/<バージョン>/` へ**コピー**され、そこで初めてコンパイルされる

このため、**壊れたサンプルは誰かが Import するまで気付けません**。運用は次のとおりです。

1. まず `Assets/Scripts/Examples` で動くことを確認する（常時コンパイルされる）
2. 問題ないことを確認したら `Samples~/<サンプル名>` へコピーする
3. `.meta` も一緒にコピーする（Import 時に GUID を保つため。Unity 公式パッケージも同様）
4. `package.json` の `samples` 配列に `displayName` / `description` / `path` を追加する
5. 追加後は、新しいプロジェクトで Import して動作確認する（CI で検証する場合は
   Unity を起動する前に `Samples~` を `Assets/` へコピーし、コンパイルエラーで落とす）

Import 先はバージョン付きのパスなので、パッケージを更新して再 Import すると古いコピーが
残ります。CI で扱う場合はコピー先を毎回消してから実行してください。

## リリース手順

1. `packages/net.sbm.unite/package.json` の `version` を上げる（Semantic Versioning）
2. `CHANGELOG.md` に変更点を追記する
3. 変更をコミットし、`vX.Y.Z` のタグを打って push する
4. 利用側は次のようにインストールする

```
https://github.com/popsy-y/p5.Unite.git?path=Packages/net.sbm.unite#vX.Y.Z
```

## 検証コマンド

```powershell
$unity = 'F:\Unity\UnityEngine\6000.2.6f2\Editor\Unity.exe'
$project = 'F:\Unity_Projects\p5.Unite'

# EditMode テスト（インポートとコンパイルも同時に検証される）
& $unity -batchmode -nographics -projectPath $project `
  -runTests -testPlatform EditMode `
  -testResults "$project\Logs\TestResults-EditMode.xml" `
  -logFile "$project\Logs\editmode-tests.log"

# Player ビルドを作り、シェーダーが実際に含まれているか検証する
& $unity -batchmode -nographics -quit -projectPath $project `
  -executeMethod P5Unite.EditorTools.BuildShaderCheck.Verify `
  -logFile "$project\Logs\shader-check.log"
```

`BuildShaderCheck`（`Assets/Editor/BuildShaderCheck.cs`、開発プロジェクト専用で配布物には入らない）
は `Assets/Scenes/Unite.unity` をビルドし、ビルドレポートのアセット一覧に3つのシェーダーと
`UniteShaderLibrary.asset` が入っているかを確認します。エディタからは
**Tools > p5.Unite > Verify Shader Inclusion In Build** でも実行できます。失敗時は
`SHADERCHECK_MISSING=...` をログに出し、バッチモードでは終了コード 1 で落ちます。

## 移行メモ（2026-10-12 実施）

- `Assets/Unite/` 配下を `Packages/net.sbm.unite/Runtime/`（および `Tests/`）へ `git mv` で移動。
  `.meta` も一緒に移動したため GUID は不変で、`Assets/Scenes/Unite.unity` などの参照は壊れていない。
- `Assets/Unite.meta` は削除（パッケージのルートはアセットではないため不要）。
- 3つのシェーダーの `#include` を `Assets/Unite/Shaders/...` から
  `Packages/net.sbm.unite/Runtime/Shaders/...` へ変更した。ここは移動で必ず壊れる箇所で、
  実際に移動直後はすべてのシェーダーで `Couldn't open include file` エラーが出ていた。
- その他の変更: `DrawingContext` のシェーダー解決、`UCanvas` のシェーダーライブラリ参照、
  `package.json` / `README.md` / `CHANGELOG.md` の追加、`manifest.json` への `testables` 追加。
- `Packages/packages-lock.json` に `net.sbm.unite` が `"source": "embedded"` として追加される
  （埋め込みパッケージの依存解決のため。コミット対象）。

### 既知の問題（移行とは無関係）

EditMode テストは 34 件中 32 件パス。失敗している 2 件は移行前から存在する未修正のもの:

- `Unite.Tests.CurveApiTests.BezierTangentMatchesFiniteDifference`（許容誤差 0.001 に対して差分 0.0019）
- `Unite.Tests.TransformApiTests.ScaleOverloadsFollowP5DefaultsAndPreserveNegativeScale`（期待 9.0 / 実際 3.0）

移行で変更したのは `DrawingContext.cs` と `UCanvas.cs` のみで、どちらのテストが触る
`Curve.cs` / `Transform.cs` は無変更（リネームのみ）。
