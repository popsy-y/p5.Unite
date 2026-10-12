using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace P5Unite.EditorTools
{
    /// <summary>
    /// Development-only tool that builds the player and verifies that the drawing shaders are
    /// included in it.
    /// </summary>
    /// <remarks>
    /// Shaders that are only located through <see cref="Shader.Find(string)"/> are stripped from
    /// player builds, and that is invisible while working in the Editor. Run this check after
    /// touching anything under <c>Runtime/Shaders</c> or <c>Runtime/Resources</c>.
    /// </remarks>
    public static class BuildShaderCheck
    {
        private const string OutputDirectory = "Builds/ShaderCheck";
        private const string OutputName = "ShaderCheck.exe";
        private const string ScenePath = "Assets/Scenes/Unite.unity";

        private const string ShaderLibraryAsset =
            "Packages/net.sbm.unite/Runtime/Resources/UniteShaderLibrary.asset";

        private static readonly string[] RequiredShaderFiles =
        {
            "ScreenDrawing.shader",
            "WorldDrawing.shader",
            "CanvasLocalDrawing.shader"
        };

        [MenuItem("Tools/p5.Unite/Verify Shader Inclusion In Build")]
        public static void Verify()
        {
            Directory.CreateDirectory(OutputDirectory);

            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(OutputDirectory, OutputName),
                    target = EditorUserBuildSettings.activeBuildTarget,
                    options = BuildOptions.DetailedBuildReport
                }
            );

            var assets = report
                .packedAssets.SelectMany(packed => packed.contents)
                .Select(content => content.sourceAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .ToArray();

            Debug.Log(
                $"SHADERCHECK_BUILD result={report.summary.result} "
                + $"errors={report.summary.totalErrors} assets={assets.Length}"
            );

            var failed = report.summary.result != BuildResult.Succeeded;

            foreach (var fileName in RequiredShaderFiles)
            {
                var included = assets.Any(path => path.EndsWith(fileName));
                failed |= !included;

                Debug.Log(
                    included
                        ? $"SHADERCHECK_OK={fileName}"
                        : $"SHADERCHECK_MISSING={fileName}"
                );
            }

            if (!assets.Contains(ShaderLibraryAsset))
            {
                failed = true;
                Debug.LogError($"SHADERCHECK_MISSING={ShaderLibraryAsset}");
            }

            if (!failed)
            {
                Debug.Log("SHADERCHECK_PASSED");
                return;
            }

            Debug.LogError("SHADERCHECK_FAILED: the drawing shaders are not in the player build.");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
