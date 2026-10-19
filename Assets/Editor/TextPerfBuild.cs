using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace P5Unite.EditorTools
{
    /// <summary>
    /// Development-only: builds a development player for the text benchmark
    /// (<c>Dev.TextPerfProbe</c>). Run it with <c>-textperf &lt;output file&gt;</c>.
    /// </summary>
    public static class TextPerfBuild
    {
        private const string OutputPath = "Builds/TextPerf/TextPerf.exe";
        private const string ScenePath = "Assets/Scenes/Unite.unity";

        [MenuItem("Tools/p5.Unite/Build Text Perf Player")]
        public static string Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                }
            );

            string summary = $"TEXTPERF_BUILD result={report.summary.result} errors={report.summary.totalErrors} "
                + $"time={report.summary.totalTime.TotalSeconds:0}s path={Path.GetFullPath(OutputPath)}";
            Debug.Log(summary);
            return summary;
        }
    }
}
