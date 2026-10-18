using Unite.Text.Fonts;
using UnityEngine;

namespace Dev
{
    // IMGUI viewer for the glyph atlas pages of the font the current text page draws with.
    public sealed partial class CanvasTester
    {
        private const float AtlasMargin = 8f;
        private const float AtlasLabelHeight = 20f;

        [Header("Atlas View")]
        [SerializeField] private bool _showAtlas = true;
        [SerializeField][Min(64f)] private float _atlasPageSize = 220f;

        private Material _atlasMaterial;
        private Material _spareAtlasMaterial;
        private readonly System.Collections.Generic.Dictionary<int, string> _atlasLabels = new();

        private bool IsTextPage => _page is TestPage.Text or TestPage.TextLayout or TestPage.VariableFont;

        private void OnGUI()
        {
            if (!_showAtlas || !IsTextPage)
                return;

            var font = _font;
            if (font == null)
                return;

            // Pages in use are drawn normally, spare pages (kept for reuse) grayed out.
            var pages = font.AtlasPages;
            var spares = font.SpareAtlasPages;
            int shown = 0;
            for (int index = 0; index < pages.Count; index++)
                shown += PageTexture(pages, spares, index, out _) != null ? 1 : 0;

            if (shown == 0)
                return;

            // Many pages (animated variable axes) shrink to keep the row within the screen.
            float size = Mathf.Clamp(
                (Screen.width - AtlasMargin) / shown - AtlasMargin, 48f, _atlasPageSize
            );
            float x = Screen.width - AtlasMargin;
            float top = Screen.height - AtlasMargin - size - AtlasLabelHeight;

            for (int index = pages.Count - 1; index >= 0; index--)
            {
                var texture = PageTexture(pages, spares, index, out bool spare);
                if (texture == null)
                    continue;

                x -= size;
                var area = new Rect(x, top, size, size + AtlasLabelHeight);
                DrawAtlasPage(area, texture, AtlasLabel(font.Name, index, pages.Count, spare), spare);
                x -= AtlasMargin;
            }
        }

        private static Texture2D PageTexture(
            System.Collections.Generic.IReadOnlyList<Texture2D> pages,
            System.Collections.Generic.IReadOnlyList<Texture2D> spares,
            int index,
            out bool spare
        )
        {
            spare = pages[index] == null && index < spares.Count && spares[index] != null;
            return spare ? spares[index] : pages[index];
        }

        private string AtlasLabel(string fontName, int index, int count, bool spare)
        {
            int key = (index * 4096 + count) * 2 + (spare ? 1 : 0);
            if (!_atlasLabels.TryGetValue(key, out string label))
            {
                label = spare ? $"{fontName} {index + 1}/{count} (spare)" : $"{fontName} {index + 1}/{count}";
                _atlasLabels.Add(key, label);
            }
            return label;
        }

        private void DrawAtlasPage(Rect area, Texture2D texture, string label, bool spare)
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = spare ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
            GUI.Label(new Rect(area.x + 4f, area.y, area.width - 8f, AtlasLabelHeight), label);

            var image = new Rect(area.x, area.y + AtlasLabelHeight, area.width, area.height - AtlasLabelHeight);
            if (Event.current.type == EventType.Repaint)
            {
                var material = spare ? SpareAtlasMaterial() : AtlasMaterial();
                if (material != null)
                    Graphics.DrawTexture(image, texture, material);
                else
                    GUI.DrawTexture(image, texture, ScaleMode.StretchToFill, alphaBlend: false);
            }

            GUI.color = previous;
        }

        // R8 pages render as red without it; falls back to the plain texture if the dev
        // shader cannot be found.
        private Material AtlasMaterial()
        {
            if (_atlasMaterial != null)
                return _atlasMaterial;

            var shader = Shader.Find("Hidden/Dev/AtlasPreview");
            if (shader != null)
                _atlasMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            return _atlasMaterial;
        }

        private Material SpareAtlasMaterial()
        {
            if (_spareAtlasMaterial != null || AtlasMaterial() == null)
                return _spareAtlasMaterial;

            _spareAtlasMaterial = new Material(_atlasMaterial) { hideFlags = HideFlags.HideAndDontSave };
            _spareAtlasMaterial.SetFloat("_Brightness", 0.35f);
            return _spareAtlasMaterial;
        }

        private void OnDestroy()
        {
            if (_atlasMaterial != null)
                Destroy(_atlasMaterial);

            if (_spareAtlasMaterial != null)
                Destroy(_spareAtlasMaterial);

            _font?.Dispose();
        }
    }
}
