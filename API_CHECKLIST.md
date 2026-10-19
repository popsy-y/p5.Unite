# p5.js API Compatibility Manifest

p5.Unite の公開 API と [p5.js 公式リファレンス](https://p5js.org/reference/) の対応状況を、AI エージェントが必要なカテゴリだけ読めるよう分割した Manifest です。

## Snapshot

- Baseline: p5.js official reference
- Last checked: 2026-10-18
- Implemented: 71 / 660
- Partial support: 5
- Reference directory: [`references/api-checklist/`](references/api-checklist/)

## Status semantics

- `[x]`: p5.Unite の公開 API として利用可能
- `[ ]`: 未実装、または一部のみ対応
- `部分対応`: API の一部の振る舞いだけを提供
- Unity 標準 API に類似機能があっても、p5.Unite の公開 API でなければ未対応として扱う
- p5.js 公式リファレンスの「Foundation」は JavaScript 基礎項目のため対象外

## Agent navigation

1. この Manifest で対象カテゴリを特定する。
2. 対応するカテゴリ Markdown だけを読む。
3. 実装状況を変更した場合は、カテゴリ文書とこの Manifest の集計を同時に更新する。

## Categories

| Category | Scope | Implemented | Partial | Total |
| --- | --- | ---: | ---: | ---: |
| [Shape](references/api-checklist/shape.md) | 2D/3D primitives, attributes, curves, and custom geometry | 30 | 1 | 65 |
| [Color](references/api-checklist/color.md) | Color creation, reading, fill, stroke, and p5.Color | 4 | 0 | 31 |
| [Typography](references/api-checklist/typography.md) | Text rendering, font metrics, and p5.Font | 22 | 3 | 25 |
| [Image](references/api-checklist/image.md) | Images, pixels, loading, display, and p5.Image | 0 | 0 | 39 |
| [Transform](references/api-checklist/transform.md) | Matrix stack and geometric transforms | 12 | 0 | 12 |
| [Environment](references/api-checklist/environment.md) | Canvas, display, frame, cursor, and window state | 2 | 0 | 28 |
| [3D](references/api-checklist/3d.md) | Camera, interaction, lighting, materials, shaders, and GPU data | 1 | 0 | 100 |
| [Rendering](references/api-checklist/rendering.md) | Graphics buffers, framebuffers, and renderer APIs | 1 | 0 | 28 |
| [Math](references/api-checklist/math.md) | Calculations, noise, random, trigonometry, quaternion, and vectors | 0 | 0 | 75 |
| [IO](references/api-checklist/io.md) | Input, time/date, tables, XML, and writer APIs | 0 | 0 | 77 |
| [Events](references/api-checklist/events.md) | Acceleration, keyboard, and pointer events | 0 | 0 | 49 |
| [DOM](references/api-checklist/dom.md) | DOM elements, files, and media elements | 0 | 0 | 85 |
| [Data](references/api-checklist/data.md) | Conversions, local storage, and data utilities | 0 | 0 | 19 |
| [Structure](references/api-checklist/structure.md) | Sketch lifecycle and loop control | 0 | 0 | 10 |
| [Constants](references/api-checklist/constants.md) | p5.js constants and mode values | 0 | 0 | 17 |

## Source-of-truth rule

各 API 項目のチェック状態、p5.js 公式リンク、p5.Unite 側の対応 API 名、部分対応の注記は、カテゴリ別 Markdown を正とします。この Manifest は入口と集計のみを保持します。

