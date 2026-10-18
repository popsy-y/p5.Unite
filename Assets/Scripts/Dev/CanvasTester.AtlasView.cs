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

        private bool IsTextPage => _page is TestPage.Text or TestPage.TextLayout or TestPage.VariableFont;

        private void OnGUI()
        {
            if (!_showAtlas || !IsTextPage)
                return;

            var font = _font;
            if (font == null)
                return;

            var pages = font.AtlasPages;
            int live = 0;
            foreach (var page in pages)
                live += page != null ? 1 : 0;

            if (live == 0)
                return;

            // Many pages (animated variable axes) shrink to keep the row within the screen.
            float size = Mathf.Clamp(
                (Screen.width - AtlasMargin) / live - AtlasMargin, 48f, _atlasPageSize
            );
            float x = Screen.width - AtlasMargin;
            float top = Screen.height - AtlasMargin - size - AtlasLabelHeight;

            for (int index = pages.Count - 1; index >= 0; index--)
            {
                var texture = pages[index];
                if (texture == null)
                    continue;

                x -= size;
                var area = new Rect(x, top, size, size + AtlasLabelHeight);
                DrawAtlasPage(area, texture, $"{font.Name} {index + 1}/{pages.Count}");
                x -= AtlasMargin;
            }
        }

        private void DrawAtlasPage(Rect area, Texture2D texture, string label)
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(area.x + 4f, area.y, area.width - 8f, AtlasLabelHeight), label);

            var image = new Rect(area.x, area.y + AtlasLabelHeight, area.width, area.height - AtlasLabelHeight);
            if (Event.current.type == EventType.Repaint)
            {
                var material = AtlasMaterial();
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

        private void OnDestroy()
        {
            if (_atlasMaterial != null)
                Destroy(_atlasMaterial);

            _font?.Dispose();
        }
    }
}
