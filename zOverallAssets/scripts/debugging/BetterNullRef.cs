// BetterNullRef.cs
// Drop-in tool for Unity to enhance NullReferenceException logs
// Place in Assets/Scripts/ or any runtime folder

using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class BetterNullRef : MonoBehaviour
{
    private static string _lastExceptionKey;
    private const bool EnabledInEditor = true;
    private const bool EnabledInBuild = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Initialize()
    {
#if UNITY_EDITOR
        if (!EnabledInEditor) return;
#else
        if (!EnabledInBuild) return;
#endif

        Application.logMessageReceived += (condition, stackTrace, type) =>
        {
            if (type != LogType.Exception || !condition.StartsWith("NullReferenceException")) return;

            string exceptionKey = condition + stackTrace;
            if (exceptionKey == _lastExceptionKey) return; // prevent duplicate
            _lastExceptionKey = exceptionKey;

            if (TryBuildBetterNREMessage(stackTrace, out string summary, out string pathLine))
            {
                Debug.LogError("\uD83D\uDD34 [BetterNRE] " + summary + $"\n{pathLine}");
            }
        };
    }

    private static bool TryBuildBetterNREMessage(string stackTrace, out string summary, out string pathLine)
    {
        summary = null;
        pathLine = null;

        var lines = stackTrace.Split('\n');
        foreach (var line in lines)
        {
            var match = Regex.Match(line.Trim(), @"^([\w\.]+)\.(\w+)\s*\(.*\)\s+\(at\s+(.+):(\d+)\)$");
            if (!match.Success) continue;

            string className = match.Groups[1].Value;
            string methodName = match.Groups[2].Value;
            string filePath = match.Groups[3].Value;
            int lineNumber = int.Parse(match.Groups[4].Value);

            if (!File.Exists(filePath)) return false;
            var sourceLines = File.ReadAllLines(filePath);
            if (lineNumber <= 0 || lineNumber > sourceLines.Length) return false;

            string codeLine = sourceLines[lineNumber - 1].Trim();
            string guess = GuessNullCause(codeLine);

            summary =
                $"NullReferenceException in {className}.{methodName} at {Path.GetFileName(filePath)}:{lineNumber} \u2192 \uD83D\uDCA5 `{guess}` likely null\n" +
                $"   \uD83E\uDDEE {codeLine}";

            string relativePath = filePath.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            pathLine = $"<a href=\"{relativePath}\" line=\"{lineNumber}\">{relativePath}:{lineNumber}</a>";
            return true;
        }

        return false;
    }

    private static string GuessNullCause(string line)
    {
        int conditionStart = line.IndexOf('(');
        int conditionEnd = line.LastIndexOf(')');
        if (conditionStart >= 0 && conditionEnd > conditionStart)
            line = line.Substring(conditionStart + 1, conditionEnd - conditionStart - 1);

        var matches = Regex.Matches(line, @"([\w\d_]+(\.[\w\d_]+)+)");
        if (matches.Count == 0) return "unknown expression";

        var longest = "";
        foreach (Match m in matches)
            if (m.Value.Length > longest.Length)
                longest = m.Value;

        return longest;
    }
}