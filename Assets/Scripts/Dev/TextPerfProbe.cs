using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unite.Text.Fonts;
using Unity.Profiling;
using UnityEngine;

namespace Dev
{
    // Headless text benchmark for development players: start the player with
    // `-textperf <output file> [-textfont <absolute font path>]`. It runs the scenarios below
    // against the CanvasTester, records per-frame time and the Unite.Text profiler markers,
    // writes a summary and quits. Does nothing without the flag.
    public sealed class TextPerfProbe : MonoBehaviour
    {
        private sealed class Scenario
        {
            public string Name;
            public CanvasTester.TestPage Page;
            public bool Animate;
            public int WarmupFrames;
            public int Frames;
        }

        private static readonly (string Label, ProfilerCategory Category, string Name, double Scale)[] Metrics =
        {
            ("Frame ms (unscaled dt)", default, null, 1.0),
            ("Main thread ms", ProfilerCategory.Internal, "Main Thread", 1e-6),
            ("Render thread ms", ProfilerCategory.Internal, "Render Thread", 1e-6),
            ("Text.Draw ms", ProfilerCategory.Scripts, "Unite.Text.Draw", 1e-6),
            ("  Layout ms", ProfilerCategory.Scripts, "Unite.Text.Layout", 1e-6),
            ("    Analyze ms", ProfilerCategory.Scripts, "Unite.Text.Shape.Analyze", 1e-6),
            ("    Shape ms", ProfilerCategory.Scripts, "Unite.Text.Shape", 1e-6),
            ("      Normalize ms", ProfilerCategory.Scripts, "Unite.Text.Shape.Normalize", 1e-6),
            ("      GSUB ms", ProfilerCategory.Scripts, "Unite.Text.Shape.Gsub", 1e-6),
            ("      GPOS ms", ProfilerCategory.Scripts, "Unite.Text.Shape.Gpos", 1e-6),
            ("  Outline (gvar) ms", ProfilerCategory.Scripts, "Unite.Text.Outline", 1e-6),
            ("    Gvar deltas ms", ProfilerCategory.Scripts, "Unite.Text.Outline.Gvar", 1e-6),
            ("    Flatten ms", ProfilerCategory.Scripts, "Unite.Text.Outline.Flatten", 1e-6),
            ("  Allocate ms", ProfilerCategory.Scripts, "Unite.Text.Allocate", 1e-6),
            ("    NewPage ms", ProfilerCategory.Scripts, "Unite.Text.Allocate.NewPage", 1e-6),
            ("    Bake.Dispatch ms", ProfilerCategory.Scripts, "Unite.Text.Bake.Dispatch", 1e-6),
            ("  Enqueue ms", ProfilerCategory.Scripts, "Unite.Text.Enqueue", 1e-6),
            ("Text.Flush ms", ProfilerCategory.Scripts, "Unite.Text.Flush", 1e-6),
            ("  Bake.Edges ms", ProfilerCategory.Scripts, "Unite.Text.Bake.Edges", 1e-6),
            ("  Bake.JobComplete ms", ProfilerCategory.Scripts, "Unite.Text.Bake.JobComplete", 1e-6),
            ("  Maintain ms", ProfilerCategory.Scripts, "Unite.Text.Maintain", 1e-6),
            ("  Upload (Apply) ms", ProfilerCategory.Scripts, "Unite.Text.Upload", 1e-6),
            ("Page Update ms", ProfilerCategory.Scripts, "Dev.CanvasTester.Update", 1e-6),
            ("Draw.Capture ms", ProfilerCategory.Scripts, "Unite.Draw.Capture", 1e-6),
            ("Draw.Layout ms", ProfilerCategory.Scripts, "Unite.Draw.Layout", 1e-6),
            ("Draw.Upload ms", ProfilerCategory.Scripts, "Unite.Draw.Upload", 1e-6),
            ("Draw.Submit ms", ProfilerCategory.Scripts, "Unite.Draw.Submit", 1e-6),
            ("GC alloc bytes/frame", ProfilerCategory.Memory, "GC Allocated In Frame", 1.0),
            ("Draw calls", ProfilerCategory.Render, "Draw Calls Count", 1.0),
            ("SetPass calls", ProfilerCategory.Render, "SetPass Calls Count", 1.0),
        };

        private static string _output;
        private static string _fontPath;
        private static bool _gcBreakdown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == "-textperf")
                    _output = args[index + 1];
                else if (args[index] == "-textfont")
                    _fontPath = args[index + 1];
            }

            _gcBreakdown = Array.IndexOf(args, "-textperf-gc") >= 0;

            if (_output == null)
                return;

            CreateHost();
        }

        /// <summary>
        /// Runs the same scenarios in Editor Play mode (e.g. through
        /// <c>unity command eval "Dev.TextPerfProbe.StartInEditor(@\"...\")"</c>); the report
        /// is written to <paramref name="output"/> and Play mode keeps running.
        /// </summary>
        public static void StartInEditor(string output, bool gcBreakdown = false)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("TextPerfProbe: enter Play mode first.");

            _output = output;
            _fontPath = null; // the tester's project-relative path works in the Editor
            _gcBreakdown = gcBreakdown;
            CreateHost();
        }

        /// <summary>
        /// Editor Play mode helper for visual checks: shows each test page for a few frames
        /// and saves a screenshot of it as &lt;directory&gt;/page-&lt;Page&gt;.png.
        /// </summary>
        public static void CaptureInEditor(string directory, params CanvasTester.TestPage[] pages)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("TextPerfProbe: enter Play mode first.");

            var host = new GameObject("TextPerfProbe.Capture");
            DontDestroyOnLoad(host);
            host.AddComponent<PageCapture>().Begin(directory, pages);
        }

        private sealed class PageCapture : MonoBehaviour
        {
            public void Begin(string directory, CanvasTester.TestPage[] pages) => StartCoroutine(Capture(directory, pages));

            private IEnumerator Capture(string directory, CanvasTester.TestPage[] pages)
            {
                var tester = FindFirstObjectByType<CanvasTester>();
                foreach (var page in pages)
                {
                    tester.ConfigureForProfiling(page, showAtlas: false, animate: true, fontPath: null);
                    for (int frame = 0; frame < 30; frame++)
                        yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"page-{page}.png"));
                    yield return null;
                }
                Debug.Log("TEXTPERF_CAPTURED");
                Destroy(gameObject);
            }
        }

        private static void CreateHost()
        {
            var host = new GameObject("TextPerfProbe");
            DontDestroyOnLoad(host);
            host.AddComponent<TextPerfProbe>();
        }

        private IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var tester = FindFirstObjectByType<CanvasTester>();
            var report = new StringBuilder();
            report.AppendLine($"# TextPerfProbe {DateTime.Now:s}  {SystemInfo.graphicsDeviceName}  {SystemInfo.processorType}");
            report.AppendLine($"# {Screen.width}x{Screen.height}  dev={Debug.isDebugBuild}  api={SystemInfo.graphicsDeviceType}");

            if (tester == null)
            {
                report.AppendLine("ERROR: no CanvasTester in the scene");
                Finish(report);
                yield break;
            }

            if (_gcBreakdown)
            {
                yield return GcBreakdown(report);
                Finish(report);
                yield break;
            }

            var scenarios = new[]
            {
                new Scenario { Name = "VariableFont animated (cold start included)", Page = CanvasTester.TestPage.VariableFont, Animate = true, WarmupFrames = 0, Frames = 900 },
                new Scenario { Name = "VariableFont animated (warm)", Page = CanvasTester.TestPage.VariableFont, Animate = true, WarmupFrames = 120, Frames = 900 },
                new Scenario { Name = "VariableFont static (steady state)", Page = CanvasTester.TestPage.VariableFont, Animate = false, WarmupFrames = 120, Frames = 600 },
                new Scenario { Name = "Stress (all features, animated)", Page = CanvasTester.TestPage.Stress, Animate = true, WarmupFrames = 120, Frames = 600 },
            };

            foreach (var scenario in scenarios)
            {
                tester.ConfigureForProfiling(scenario.Page, showAtlas: false, scenario.Animate, _fontPath);
                yield return Run(scenario, report);
            }

            Finish(report);
        }

        private static IEnumerator Run(Scenario scenario, StringBuilder report)
        {
            var recorders = new ProfilerRecorder[Metrics.Length];
            var samples = new List<double>[Metrics.Length];
            for (int index = 0; index < Metrics.Length; index++)
            {
                samples[index] = new List<double>(scenario.Frames);
                if (Metrics[index].Name == null)
                    continue;

                recorders[index] = ProfilerRecorder.StartNew(
                    Metrics[index].Category, Metrics[index].Name, 1,
                    ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame
                );
            }

            for (int frame = 0; frame < scenario.WarmupFrames; frame++)
                yield return null;

            for (int frame = 0; frame < scenario.Frames; frame++)
            {
                yield return null;
                for (int index = 0; index < Metrics.Length; index++)
                {
                    double value = Metrics[index].Name == null
                        ? Time.unscaledDeltaTime * 1000.0
                        : recorders[index].Valid ? recorders[index].LastValue * Metrics[index].Scale : double.NaN;
                    samples[index].Add(value);
                }
            }

            report.AppendLine();
            report.AppendLine($"## {scenario.Name}  ({scenario.Frames} frames after {scenario.WarmupFrames} warm-up)");
            report.AppendLine($"{"metric",-26}{"mean",11}{"median",11}{"p95",11}{"p99",11}{"max",11}{"sum",13}");
            for (int index = 0; index < Metrics.Length; index++)
            {
                recorders[index].Dispose();
                var list = samples[index];
                list.RemoveAll(double.IsNaN);
                if (list.Count == 0)
                {
                    report.AppendLine($"{Metrics[index].Label,-26}  (marker unavailable)");
                    continue;
                }

                var sorted = new List<double>(list);
                sorted.Sort();
                double sum = 0;
                foreach (double value in list)
                    sum += value;

                report.AppendLine(
                    $"{Metrics[index].Label,-26}{sum / list.Count,11:0.000}{sorted[sorted.Count / 2],11:0.000}"
                    + $"{sorted[(int)(sorted.Count * 0.95)],11:0.000}{sorted[(int)(sorted.Count * 0.99)],11:0.000}"
                    + $"{sorted[sorted.Count - 1],11:0.000}{sum,13:0.0}"
                );

                if (Metrics[index].Name == null)
                    report.AppendLine($"{"  => fps (mean / median)",-26}{1000.0 * list.Count / sum,11:0}{1000.0 / sorted[sorted.Count / 2],11:0}");
            }
        }

        // Allocation breakdown of one animated-axis frame: each experiment runs inside a
        // single frame and the profiler's per-frame GC counter is compared with an empty frame.
        private static IEnumerator GcBreakdown(StringBuilder report)
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            const int count = 100;
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            var face = FontFace.LoadFont(_fontPath, "gc-probe");
            var glyphs = new int[8];
            for (int index = 0; index < glyphs.Length; index++)
                glyphs[index] = face.Default.GetGlyphId("Variable"[index]);

            var loadPoints = typeof(FontFace).GetMethod("LoadPoints", any);
            var tryBake = typeof(FontInstance).GetMethod("TryGetBakeSource", any);
            var settings = new AxisSetting[1];
            float weight = 100f;
            var bakeArgs = new object[2];
            var pointArgs = new object[2];

            FontInstance Next()
            {
                weight = weight > 690f ? 100f : weight + 0.37f;
                settings[0] = new AxisSetting("wght", weight);
                return face.GetInstance(settings);
            }

            var experiments = new (string Name, Action Run)[]
            {
                ("empty frame", () => { }),
                ("reflection only: 8x TryGetBakeSource on a cached instance", () =>
                {
                    var cached = face.Default;
                    for (int n = 0; n < count; n++)
                        foreach (int glyph in glyphs) { bakeArgs[0] = glyph; bakeArgs[1] = null; tryBake.Invoke(cached, bakeArgs); }
                }),
                ("GetInstance(new wght)", () => { for (int n = 0; n < count; n++) Next(); }),
                ("GetInstance + 8x Advance", () =>
                {
                    for (int n = 0; n < count; n++)
                    {
                        var instance = Next();
                        foreach (int glyph in glyphs) instance.Advance(glyph);
                    }
                }),
                ("GetInstance + 8x TryGetBakeSource (incl. reflection)", () =>
                {
                    for (int n = 0; n < count; n++)
                    {
                        var instance = Next();
                        foreach (int glyph in glyphs) { bakeArgs[0] = glyph; bakeArgs[1] = null; tryBake.Invoke(instance, bakeArgs); }
                    }
                }),
                ("LoadPoints x8 on the default master (shared arrays)", () =>
                {
                    for (int n = 0; n < count; n++)
                        foreach (int glyph in glyphs) { pointArgs[0] = glyph; pointArgs[1] = null; loadPoints.Invoke(face, pointArgs); }
                }),
            };

            report.AppendLine();
            report.AppendLine($"## GC breakdown ({count} iterations per experiment; bytes per iteration, empty frame subtracted)");

            // Warm-up pass so JIT and first-use allocations do not count.
            foreach (var experiment in experiments) experiment.Run();
            yield return null;
            yield return null;

            long baseline = 0;
            foreach (var experiment in experiments)
            {
                yield return null;
                experiment.Run();
                yield return null;
                long frameBytes = recorder.LastValue;
                if (experiment.Name == "empty frame")
                    baseline = frameBytes;
                report.AppendLine($"{experiment.Name,-62}{(frameBytes - baseline) / (double)count,10:0.0} B/iter   (frame total {frameBytes} B)");
            }

            var idsField = typeof(FontFace).GetField("_instanceIds", any);
            var instancesField = typeof(FontFace).GetField("_instances", any);
            report.AppendLine(
                $"_instanceIds.Count={((System.Collections.ICollection)idsField.GetValue(face)).Count}  "
                + $"_instances.Count={((System.Collections.ICollection)instancesField.GetValue(face)).Count}"
            );
            recorder.Dispose();
        }

        private static void Finish(StringBuilder report)
        {
            try
            {
                File.WriteAllText(_output, report.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError($"TextPerfProbe: could not write {_output}: {exception.Message}");
            }

            Debug.Log("TEXTPERF_DONE\n" + report);
            if (!Application.isEditor)
                Application.Quit(0);
        }
    }
}
