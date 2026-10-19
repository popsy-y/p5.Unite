# Typography API Checklist

[← API checklist manifest](../../API_CHECKLIST.md)

- Category: `Typography`
- Implemented: 22 / 25
- Partial support: 3
- Status source: p5.Unite public API only
- Package: `net.sbm.unite.text`（`Unite.Text` 名前空間。描画系は `UCanvas` の拡張メソッド `TextCanvasExtensions`）
- 文字体系: OpenType シェーピング（GSUB / GPOS、アラビア文字などの結合形、合字、カーニング、マーク配置）、Unicode Bidi、UAX #14 の改行、COLRv0 のカラー絵文字に対応。インド系文字・タイ文字などの並べ替えが必要な体系は既定の feature のみで描画（詳細はパッケージの README）
- 共通事項: Getter は p5.js の引数なし呼び出しに対応するオーバーロード（例: `TextSize()`）。テキスト属性は `Push()` / `Pop()` で保存・復元される。Screen 系モードは p5 と同じ y-down、World / CanvasLocal は y-up（文字は正立、行は -y 方向へ進む）

- [x] [fontAscent()](https://p5js.org/reference/p5/fontAscent/) — `UCanvas.FontAscent()`
- [x] [fontBounds()](https://p5js.org/reference/p5/fontBounds/) — `UCanvas.FontBounds(str, x, y, [maxWidth], [maxHeight])`（`Rect` を返す。min = 最小の x / y）
- [x] [fontDescent()](https://p5js.org/reference/p5/fontDescent/) — `UCanvas.FontDescent()`
- [x] [fontWidth()](https://p5js.org/reference/p5/fontWidth/) — `UCanvas.FontWidth()`
- [ ] [loadFont()](https://p5js.org/reference/p5/loadFont/) — 部分対応: `FontFace.LoadFont(path | bytes)` / `FontFace.LoadFontAsync(path)`（`Awaitable<FontFace>`）、`UCanvas.LoadFont()` / `UCanvas.LoadFontAsync()`。.ttf / .otf（TrueType・CFF）/ .woff / .woff2 / コレクション（先頭のフォント）とバリアブルフォント（gvar）に対応。CFF2、CSS ファイル / `@font-face` 文字列、`options` 引数は未対応
- [x] [text()](https://p5js.org/reference/p5/text/) — `UCanvas.Text(str, x, y, [maxWidth], [maxHeight])`（テキストボックスは `RectMode` に従い、ボックス外の行は描画しない）
- [x] [textAlign()](https://p5js.org/reference/p5/textAlign/) — `UCanvas.TextAlign(TextAlignMode, [TextVerticalAlignMode])` / `UCanvas.TextAlign()`
- [x] [textAscent()](https://p5js.org/reference/p5/textAscent/) — `UCanvas.TextAscent([text])`
- [x] [textBounds()](https://p5js.org/reference/p5/textBounds/) — `UCanvas.TextBounds(str, x, y, [maxWidth], [maxHeight])`（`Rect` を返す）
- [x] [textDescent()](https://p5js.org/reference/p5/textDescent/) — `UCanvas.TextDescent([text])`
- [x] [textDirection()](https://p5js.org/reference/p5/textDirection/) — `UCanvas.TextDirection(TextDirectionMode)` / `UCanvas.TextDirection()`（`Ltr` / `Rtl` / `Auto` / `Inherit`。Unicode Bidi（UAX #9）で並べ替え、括弧は鏡像化。`Auto` は最初の強い文字で段落の向きを決める）
- [x] [textFont()](https://p5js.org/reference/p5/textFont/) — `UCanvas.TextFont(FontFace, [size])` / `UCanvas.TextFont(params FontFace[])` / `UCanvas.TextFont(FontFace[], size)` / `UCanvas.TextFont()`（複数指定は CSS の font-family と同じフォールバック。全フォントに無い文字は OS のフォントを探索し、見つかるまでは Tofu。`UCanvas.TextFonts()` で全体を取得）
- [x] [textLeading()](https://p5js.org/reference/p5/textLeading/) — `UCanvas.TextLeading(leading)` / `UCanvas.TextLeading()`（未設定時は textSize × 1.25）
- [ ] [textProperties()](https://p5js.org/reference/p5/textProperties/) — 部分対応: `UCanvas.TextProperties(IReadOnlyDictionary<string, object>)` / `UCanvas.TextProperties()`。対応プロパティは textProperty() と同じ
- [ ] [textProperty()](https://p5js.org/reference/p5/textProperty/) — 部分対応: `UCanvas.TextProperty(name, value)` / `UCanvas.TextProperty(name)`。textFont（`FontFace[]` でフォールバック指定）/ textSize (fontSize) / textLeading (lineHeight) / textAlign / textBaseline / textStyle (fontStyle) / textWeight (fontWeight) / textWrap / direction / letterSpacing / wordSpacing / fontVariationSettings / fontFeatureSettings / fontKerning / fontVariantLigatures / lang のみ。fontStretch / fontVariantCaps / textRendering は未対応
- [x] [textSize()](https://p5js.org/reference/p5/textSize/) — `UCanvas.TextSize(size)` / `UCanvas.TextSize()`
- [x] [textStyle()](https://p5js.org/reference/p5/textStyle/) — `UCanvas.TextStyle(TextStyleMode)` / `UCanvas.TextStyle()`（バリアブルフォントの `wght` / `ital` / `slnt` 軸に反映。軸が無ければ 1 つのフォントファイルから合成: ITALIC はシアー、BOLD はアウトラインの膨張）
- [x] [textWeight()](https://p5js.org/reference/p5/textWeight/) — `UCanvas.TextWeight(weight)` / `UCanvas.TextWeight()`（バリアブルフォントの `wght` 軸に反映。軸を持たないフォントではアウトラインの膨張で合成。他の軸は `UCanvas.TextVariation(axis, value)`）
- [x] [textWidth()](https://p5js.org/reference/p5/textWidth/) — `UCanvas.TextWidth()`（p5.js 2.x と同じくタイトな幅。前後の空白は含まない）
- [x] [textWrap()](https://p5js.org/reference/p5/textWrap/) — `UCanvas.TextWrap(TextWrapMode)` / `UCanvas.TextWrap()`

## p5.Font

- [x] [p5.Font](https://p5js.org/reference/p5/p5.Font/) — `FontFace`（軸の値ごとの描画用データは `FontInstance`）
- [x] [textToContours()](https://p5js.org/reference/p5.Font/textToContours/) — `FontFace.TextToContours(str, x, y, [TextPathOptions])`（`List<List<TextPoint>>`）
- [x] [textToModel()](https://p5js.org/reference/p5.Font/textToModel/) — `FontFace.TextToModel(str, x, y, width, height, [TextPathOptions])`（`UnityEngine.Mesh` を返す。`Extrude` で押し出し）
- [x] [textToPaths()](https://p5js.org/reference/p5.Font/textToPaths/) — `FontFace.TextToPaths(str, x, y, [width, height], [TextPathOptions])`（`List<PathCommand>`: MoveTo / LineTo / QuadraticTo / Close）
- [x] [textToPoints()](https://p5js.org/reference/p5.Font/textToPoints/) — `FontFace.TextToPoints(str, x, y, [TextPathOptions])`（`List<TextPoint>`。`TextPathOptions.FromCanvas(canvas)` でキャンバスの textSize / textAlign などを引き継ぐ）
