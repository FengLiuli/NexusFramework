using System.IO;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;

public static class TestRunHelper
{
    private const string ResultFile = "TestResults_Capture.txt";

    public static void RunEditModeAndSave()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new CaptureCallbacks());
        api.Execute(new ExecutionSettings());
    }

    private class CaptureCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor testResults)
        {
            var resultPath = Path.GetFullPath(ResultFile);
            var failures = new System.Collections.Generic.List<string>();
            CollectFailures(testResults, failures);
            File.WriteAllText(resultPath,
                "Total: " + (testResults.PassCount + testResults.FailCount + testResults.SkipCount + testResults.InconclusiveCount) + "\n" +
                "Passed: " + testResults.PassCount + "\n" +
                "Failed: " + testResults.FailCount + "\n" +
                "Skipped: " + testResults.SkipCount + "\n" +
                "Inconclusive: " + testResults.InconclusiveCount + "\n" +
                (failures.Count > 0 ? "--- Failures ---\n" + string.Join("\n", failures.ToArray()) : ""));
        }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        private void CollectFailures(ITestResultAdaptor result, System.Collections.Generic.List<string> failures)
        {
            if (result.HasChildren)
            {
                foreach (var child in result.Children)
                    CollectFailures(child, failures);
            }
            else if (result.TestStatus != TestStatus.Passed)
            {
                failures.Add(result.FullName + ": " + (result.Message ?? "(no message)"));
            }
        }
    }
}
