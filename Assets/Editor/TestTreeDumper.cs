using System.Collections.Generic;
using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class TestTreeDumper
{
    public static void DumpEditMode()
    {
        Dump(TestMode.EditMode, "TestTree_EditMode.txt");
    }

    public static void DumpPlayMode()
    {
        Dump(TestMode.PlayMode, "TestTree_PlayMode.txt");
    }

    static void Dump(TestMode mode, string filename)
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RetrieveTestList(mode, (root) =>
        {
            var lines = new List<string>();
            DumpNode(root, lines, 0);
            File.WriteAllLines(Application.dataPath + "/../" + filename, lines.ToArray());
            Debug.Log("Dumped " + filename + " with " + root.TestCaseCount + " tests");
        });
    }

    static void DumpNode(ITestAdaptor node, List<string> lines, int depth)
    {
        var indent = new string(' ', depth * 2);
        lines.Add(indent + node.FullName + " | suite=" + node.IsSuite + " children=" + (node.HasChildren ? System.Linq.Enumerable.Count(node.Children).ToString() : "0") + " testCount=" + node.TestCaseCount + " mode=" + node.TestMode + " isAssembly=" + node.IsTestAssembly);
        if (node.HasChildren)
        {
            foreach (var child in node.Children)
                DumpNode(child, lines, depth + 1);
        }
    }
}
