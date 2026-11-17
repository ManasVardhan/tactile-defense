using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TactileWars.EditorTools
{
    /// <summary>
    /// Runs the scripted play mode capture (PlayMode test category "Screenshots") which writes
    /// Docs/Screenshots/battle_*.png at 1280x720. In batchmode the editor exits when the run finishes.
    /// Equivalent command line: -runTests -testPlatform PlayMode -testCategory Screenshots
    /// </summary>
    [InitializeOnLoad]
    public static class Screenshotter
    {
        const string Flag = "TW_ScreenshotterRunning";

        static Screenshotter()
        {
            if (SessionState.GetBool(Flag, false)) Register();
        }

        [MenuItem("TactileWars/Capture Screenshots")]
        public static void CaptureAll() => Run("Screenshots");

        /// <summary>Every UI screen at 1280x720 plus tall phone captures (PlayMode category "UIScreenshots").</summary>
        [MenuItem("TactileWars/Capture UI Screenshots")]
        public static void CaptureUI() => Run("UIScreenshots");

        [MenuItem("TactileWars/Measure Frame Time")]
        public static void MeasurePerf() => Run("Perf");

        static void Run(string category)
        {
            SessionState.SetBool(Flag, true);
            Register();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter { testMode = TestMode.PlayMode, categoryNames = new[] { category } };
            api.Execute(new ExecutionSettings(filter));
        }

        static Callbacks callbacks;

        static void Register()
        {
            if (callbacks != null) return;
            callbacks = new Callbacks();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(callbacks);
        }

        class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren) Debug.Log($"[Screenshotter] {result.Name}: {result.TestStatus} {result.Message}");
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                SessionState.SetBool(Flag, false);
                Debug.Log($"[Screenshotter] Run finished: passed {result.PassCount}, failed {result.FailCount}");
                if (Application.isBatchMode) EditorApplication.Exit(result.FailCount > 0 ? 1 : 0);
            }
        }
    }
}
