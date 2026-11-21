using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NexusFrameworkValidation
{
    /// <summary>一条过渡 API 的扫描结果。</summary>
    public sealed class TransitionalApiSpec
    {
        public string FilePath;
        public int Line;
        public string MemberName;
        public string Replacement;
        public string RemoveVersion;
        public string Migration;
        public bool HasObsolete;
        public string ObsoleteMessage;
        public bool ObsoleteIsError;
    }

    /// <summary>
    /// 过渡 API 扫描器（D009 §5.4.6 / §9）。
    /// 代码是唯一真源：扫描包内 [TransitionalApi] 与 [Obsolete]，
    /// 校验两者成对且消息一致，并整体重写 README 的受控区块。
    /// </summary>
    public static class TransitionalApiScanner
    {
        private const string MenuRoot = "Tools/NexusFramework";
        private const string BeginMarker = "<!-- BEGIN TRANSITIONAL APIS";
        private const string EndMarker = "<!-- END TRANSITIONAL APIS -->";
        private const string PackagePrefix = "com.nexusframework";

        private static readonly Regex TransitionalAttrRegex = new Regex(
            "\\[TransitionalApi\\s*\\(\\s*\"(?<replacement>[^\"]*)\"\\s*,\\s*\"(?<remove>[^\"]*)\"" +
            "(?:\\s*,\\s*\"(?<migration>[^\"]*)\")?\\s*\\)\\s*\\]",
            RegexOptions.Compiled);

        private static readonly Regex ObsoleteAttrRegex = new Regex(
            "\\[Obsolete\\s*\\(\\s*\"(?<message>[^\"]*)\"(?:\\s*,\\s*(?<isError>true|false))?\\s*\\)\\s*\\]",
            RegexOptions.Compiled);

        private static readonly Regex MemberNameRegex = new Regex(
            @"(\w+)\s*(?:\(|\{|=>|=|;)", RegexOptions.Compiled);

        private static readonly Regex PackageNameRegex =
            new Regex("\"name\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        private static readonly Regex PackageVersionRegex =
            new Regex("\"version\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        [MenuItem(MenuRoot + "/Regenerate Transitional API Lists")]
        public static void RegenerateFromMenu()
        {
            var errors = RegenerateAll();
            if (errors.Count == 0)
            {
                Debug.Log("[NexusFrameworkValidation] 过渡 API 清单已重新生成，无错误。");
                return;
            }

            foreach (var error in errors)
                Debug.LogError("[NexusFrameworkValidation] " + error);
            Debug.LogError("[NexusFrameworkValidation] 过渡 API 清单生成失败，共 " +
                           errors.Count + " 个问题。");
        }

        /// <summary>重新生成所有 Nexus 包的 README 过渡 API 区块，返回错误列表。</summary>
        public static List<string> RegenerateAll()
        {
            var errors = new List<string>();
            foreach (var packageDir in FindPackageDirectories())
            {
                var packageName = ReadPackageName(packageDir);
                if (packageName == null || !packageName.StartsWith(PackagePrefix, StringComparison.Ordinal))
                    continue;

                var specs = ScanPackage(packageDir);
                var version = ReadPackageVersion(packageDir);
                ValidateSpecs(packageDir, version, specs, errors);
                RegenerateReadme(packageDir, specs, errors);
            }
            return errors;
        }

        /// <summary>查找 Assets 下所有包含 package.json 的一级目录。</summary>
        public static List<string> FindPackageDirectories()
        {
            var result = new List<string>();
            var assets = Application.dataPath;
            if (!Directory.Exists(assets))
                return result;

            foreach (var dir in Directory.GetDirectories(assets))
            {
                if (File.Exists(Path.Combine(dir, "package.json")))
                    result.Add(dir);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public static string ReadPackageName(string packageDir)
        {
            var path = Path.Combine(packageDir, "package.json");
            if (!File.Exists(path))
                return null;
            var match = PackageNameRegex.Match(File.ReadAllText(path));
            return match.Success ? match.Groups[1].Value : null;
        }

        public static string ReadPackageVersion(string packageDir)
        {
            var path = Path.Combine(packageDir, "package.json");
            if (!File.Exists(path))
                return null;
            var match = PackageVersionRegex.Match(File.ReadAllText(path));
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>扫描一个包目录内的全部 .cs 文件。</summary>
        public static List<TransitionalApiSpec> ScanPackage(string packageDir)
        {
            var specs = new List<TransitionalApiSpec>();
            foreach (var file in Directory.GetFiles(packageDir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsIgnoredPath(file))
                    continue;

                var text = File.ReadAllText(file);
                foreach (Match match in TransitionalAttrRegex.Matches(text))
                {
                    var spec = new TransitionalApiSpec
                    {
                        FilePath = file,
                        Line = LineOf(text, match.Index),
                        Replacement = match.Groups["replacement"].Value,
                        RemoveVersion = match.Groups["remove"].Value,
                        Migration = match.Groups["migration"].Success
                            ? match.Groups["migration"].Value
                            : null
                    };

                    var tailStart = match.Index + match.Length;
                    var tail = text.Substring(tailStart, Math.Min(2048, text.Length - tailStart));
                    var obsolete = ObsoleteAttrRegex.Match(tail);
                    if (obsolete.Success && !HasDeclarationBefore(tail, obsolete.Index))
                    {
                        spec.HasObsolete = true;
                        spec.ObsoleteMessage = obsolete.Groups["message"].Value;
                        spec.ObsoleteIsError = obsolete.Groups["isError"].Success &&
                                               obsolete.Groups["isError"].Value == "true";
                    }

                    spec.MemberName = ExtractMemberName(tail);
                    specs.Add(spec);
                }
            }
            return specs;
        }

        /// <summary>校验过渡 API 的成对性、消息一致性与到期状态。</summary>
        public static void ValidateSpecs(string packageDir, string version,
            List<TransitionalApiSpec> specs, List<string> errors)
        {
            var currentMajor = ParseMajor(version);
            var isPreRelease = IsNonMajorRelease(version);

            foreach (var spec in specs)
            {
                var location = RelativePath(spec.FilePath) + ":" + spec.Line;

                if (!spec.HasObsolete)
                {
                    errors.Add(location + ": [TransitionalApi] 缺少同时使用的 [Obsolete]。");
                    continue;
                }

                if (spec.ObsoleteMessage.IndexOf("Use " + spec.Replacement + " instead",
                        StringComparison.Ordinal) < 0)
                {
                    errors.Add(location + ": [Obsolete] 消息必须包含 \"Use " + spec.Replacement +
                               " instead\"。");
                }

                if (spec.ObsoleteMessage.IndexOf("Removed in " + spec.RemoveVersion,
                        StringComparison.Ordinal) < 0)
                {
                    errors.Add(location + ": [Obsolete] 消息必须包含 \"Removed in " +
                               spec.RemoveVersion + "\"。");
                }

                var removeMajor = ParseMajor(spec.RemoveVersion);
                if (removeMajor <= 0)
                {
                    errors.Add(location + ": RemoveVersion 不是合法的 MAJOR 版本：" +
                               spec.RemoveVersion);
                }
                else if (currentMajor > 0 && currentMajor >= removeMajor)
                {
                    errors.Add(location + ": 过渡 API 已到期（Remove in " + spec.RemoveVersion +
                               "，当前版本 " + version + "），必须在本 MAJOR 中删除。");
                }

                if (isPreRelease && spec.ObsoleteIsError)
                {
                    errors.Add(location + ": MINOR/PATCH 版本中不得使用 [Obsolete(..., true)]。");
                }
            }
        }

        /// <summary>整体替换 README 中的受控区块。</summary>
        public static void RegenerateReadme(string packageDir, List<TransitionalApiSpec> specs,
            List<string> errors)
        {
            var readmePath = Path.Combine(packageDir, "README.md");
            if (!File.Exists(readmePath))
            {
                errors.Add(RelativePath(readmePath) + ": 缺少 README.md。");
                return;
            }

            var text = File.ReadAllText(readmePath);
            var begin = text.IndexOf(BeginMarker, StringComparison.Ordinal);
            var end = text.IndexOf(EndMarker, StringComparison.Ordinal);
            if (begin < 0 || end < 0 || end < begin)
            {
                errors.Add(RelativePath(readmePath) +
                           ": 缺少过渡 API 受控区块标记（BEGIN/END TRANSITIONAL APIS）。");
                return;
            }

            var beginLineEnd = text.IndexOf('\n', begin);
            if (beginLineEnd < 0)
            {
                errors.Add(RelativePath(readmePath) + ": 受控区块格式错误。");
                return;
            }

            specs.Sort((a, b) =>
            {
                var byPath = string.CompareOrdinal(a.FilePath, b.FilePath);
                return byPath != 0 ? byPath : a.Line.CompareTo(b.Line);
            });

            var block = BuildBlock(specs);
            var newText = text.Substring(0, beginLineEnd + 1) + block + text.Substring(end);
            if (newText != text)
                File.WriteAllText(readmePath, newText);
        }

        private static string BuildBlock(List<TransitionalApiSpec> specs)
        {
            var builder = new StringBuilder();
            builder.AppendLine();
            builder.AppendLine("## Transitional APIs");
            builder.AppendLine();
            builder.AppendLine("| Deprecated API | Replacement | Since | Remove in |");
            builder.AppendLine("|---|---|---|---|");

            foreach (var spec in specs)
            {
                builder.Append("| `");
                builder.Append(spec.MemberName);
                builder.Append("` | `");
                builder.Append(spec.Replacement);
                builder.Append("` | — | ");
                builder.Append(spec.RemoveVersion);
                builder.AppendLine(" |");
            }

            builder.AppendLine();
            return builder.ToString();
        }

        private static bool HasDeclarationBefore(string tail, int end)
        {
            var before = tail.Substring(0, end);
            foreach (var raw in before.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith("[", StringComparison.Ordinal))
                    continue;
                return true;
            }
            return false;
        }

        private static string ExtractMemberName(string tail)
        {
            var builder = new StringBuilder();
            var taken = 0;
            foreach (var raw in tail.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith("[", StringComparison.Ordinal))
                    continue;

                builder.Append(line).Append(' ');
                taken++;
                if (taken >= 4 ||
                    line.IndexOf('{') >= 0 ||
                    line.IndexOf(';') >= 0 ||
                    line.IndexOf("=>", StringComparison.Ordinal) >= 0)
                    break;
            }

            var match = MemberNameRegex.Match(builder.ToString());
            return match.Success ? match.Groups[1].Value : "?";
        }

        private static bool IsIgnoredPath(string path)
        {
            var normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/~", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("~/", StringComparison.Ordinal) >= 0;
        }

        private static int LineOf(string text, int index)
        {
            var line = 1;
            for (var i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                    line++;
            }
            return line;
        }

        private static string RelativePath(string path)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var full = Path.GetFullPath(path);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                full = full.Substring(root.Length).TrimStart('/', '\\');
            return full.Replace('\\', '/');
        }

        private static int ParseMajor(string version)
        {
            if (string.IsNullOrEmpty(version))
                return -1;
            var dot = version.IndexOf('.');
            var head = dot >= 0 ? version.Substring(0, dot) : version;
            return int.TryParse(head, out var major) ? major : -1;
        }

        private static bool IsNonMajorRelease(string version)
        {
            if (string.IsNullOrEmpty(version))
                return false;
            var parts = version.Split('.');
            if (parts.Length < 3)
                return false;
            var minor = int.TryParse(parts[1], out var m) ? m : -1;
            var patchPart = parts[2];
            var dash = patchPart.IndexOf('-');
            var patch = int.TryParse(dash >= 0 ? patchPart.Substring(0, dash) : patchPart,
                out var p) ? p : -1;
            return minor > 0 || patch > 0;
        }
    }
}
