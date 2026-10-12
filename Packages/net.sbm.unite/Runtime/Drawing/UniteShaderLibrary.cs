using UnityEngine;

namespace Unite.Drawing
{
    /// <summary>
    /// Holds the shader references used by the drawing context.
    /// </summary>
    /// <remarks>
    /// Shaders that are only located through <see cref="Shader.Find(string)"/> are stripped from
    /// player builds because nothing references them. The library asset keeps a serialized
    /// reference to every drawing shader, and lives under <c>Runtime/Resources</c> so that it is
    /// always included in a build. As a result the shaders survive the build without the user
    /// having to touch the project settings.
    /// </remarks>
    [CreateAssetMenu(fileName = "UniteShaderLibrary", menuName = "p5.Unite/Shader Library")]
    public sealed class UniteShaderLibrary : ScriptableObject
    {
        /// <summary>
        /// Name used by <see cref="Resources.Load{T}(string)"/> for the library shipped with the package.
        /// </summary>
        public const string ResourceName = "UniteShaderLibrary";

        /// <summary>
        /// Asset path of the library shipped with the package.
        /// </summary>
        public const string AssetPath =
            "Packages/net.sbm.unite/Runtime/Resources/UniteShaderLibrary.asset";

        [SerializeField] private Shader _screenDrawing;
        [SerializeField] private Shader _worldDrawing;
        [SerializeField] private Shader _canvasLocalDrawing;

        private static UniteShaderLibrary _instance;

        /// <summary>
        /// The library shipped with the package, loaded on demand from Resources.
        /// </summary>
        public static UniteShaderLibrary Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<UniteShaderLibrary>(ResourceName);

                return _instance;
            }
        }

        /// <summary>
        /// Returns the shader used for the given drawing mode. May be null when the
        /// library asset has no shader assigned.
        /// </summary>
        public Shader GetShader(DrawingMode mode) => mode switch
        {
            DrawingMode.World => _worldDrawing,
            DrawingMode.CanvasLocal => _canvasLocalDrawing,
            _ => _screenDrawing
        };

        /// <summary>
        /// Returns the shader name used for the given drawing mode.
        /// </summary>
        public static string ShaderNameFor(DrawingMode mode) => mode switch
        {
            DrawingMode.World => "Hidden/Unite/WorldDrawing",
            DrawingMode.CanvasLocal => "Hidden/Unite/CanvasLocalDrawing",
            _ => "Hidden/Unite/ScreenDrawing"
        };
    }
}
