using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// Batch-mode WebGL build into TactileWarsWeb/ using the enabled scenes from the build settings and the current
    /// Player settings (compression included). Logs "[WebBuild] ok &lt;total size&gt;" on success and exits with code 1
    /// on failure.
    /// Command line: -buildTarget WebGL -executeMethod TactileWars.EditorTools.WebBuild.Build -quit
    /// </summary>
    public static class WebBuild
    {
        const string OutputFolder = "TactileWarsWeb";

        [MenuItem("TactileWars/Build Web")]
        public static void Build()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebBuild] failed: no enabled scenes in build settings");
                ExitOnFailure();
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[WebBuild] failed: {summary.result}, {summary.totalErrors} errors");
                ExitOnFailure();
                return;
            }

            Debug.Log($"[WebBuild] ok {FormatSize(summary.totalSize)} ({summary.totalSize} bytes, {scenes.Length} scenes, {summary.totalTime.TotalSeconds:F0} s)");
        }

        static void ExitOnFailure()
        {
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        static string FormatSize(ulong bytes)
        {
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}
