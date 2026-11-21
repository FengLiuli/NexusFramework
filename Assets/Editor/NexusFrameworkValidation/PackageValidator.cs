using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NexusFrameworkValidation
{
    /// <summary>
    /// 包元数据校验器（D009 §9）。
    /// 覆盖不依赖编译产物的文本检查，作为首次发布前的最低保障；
    /// 完整检查项（依赖 tag、版本常量生成等）见 D009 §9 清单。
    /// </summary>
    public static class PackageValidator
    {
        private const string MenuRoot = "Tools/NexusFramework";

        private static readonly Regex SemVerRegex = new Regex(
            @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-(exp|pre|rc)\.(0|[1-9]\d*))?$",
            RegexOptions.Compiled);

        private static readonly Regex StringFieldRegex = new Regex(
            "\"(?<key>[^\"]+)\"\\s*:\\s*\"(?<value>[^\"]*)\"", RegexOptions.Compiled);

        private static readonly Regex DependenciesBlockRegex = new Regex(
            "\"dependencies\"\\s*:\\s*\\{(?<body>[^}]*)\\}", RegexOptions.Compiled);

        private static readonly Regex SamplesBlockRegex = new Regex(
            "\"samples\"\\s*:\\s*\\[(?<body>.*?)\\]", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex AsmdefReferencesRegex = new Regex(
            "\"references\"\\s*:\\s*\\[(?<body>[^\\]]*)\\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex AsmdefNameRegex =
            new Regex("\"name\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        private sealed class PackageInfo
        {
            public string Dir;
            public string Name;
            public string Version;
            public string Json;
        }

        [MenuItem(MenuRoot + "/Validate Packages")]
        public static void ValidateFromMenu()
        {
            var errors = ValidateAll();
            if (errors.Count == 0)
            {
                Debug.Log("[NexusFrameworkValidation] 包校验通过。");
                EditorUtility.DisplayDialog("NexusFramework", "包校验通过。", "OK");
                return;
            }

            foreach (var error in errors)
                Debug.LogError("[NexusFrameworkValidation] " + error);

            Debug.LogError("[NexusFrameworkValidation] 包校验失败，共 " + errors.Count + " 个问题。");
            EditorUtility.DisplayDialog("NexusFramework",
                "包校验失败，共 " + errors.Count + " 个问题。详见 Console。", "OK");
        }

        /// <summary>执行全部文本检查，返回错误列表；空列表表示通过。</summary>
        public static List<string> ValidateAll()
        {
            var errors = new List<string>();
            var packages = LoadPackages(errors);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var package in packages)
            {
                ValidatePackageJson(package, names, errors);
                ValidateReadmeAndChangelog(package, errors);
                ValidateAsmdefs(package, packages, errors);
                ValidateSamples(package, errors);
                ValidateDocumentation(package, errors);
                ValidateTransitionalApis(package, errors);
            }

            return errors;
        }

        private static List<PackageInfo> LoadPackages(List<string> errors)
        {
            var packages = new List<PackageInfo>();
            foreach (var dir in TransitionalApiScanner.FindPackageDirectories())
            {
                var jsonPath = Path.Combine(dir, "package.json");
                var json = File.ReadAllText(jsonPath);
                var nameMatch = new Regex("\"name\"\\s*:\\s*\"([^\"]+)\"").Match(json);
                var versionMatch = new Regex("\"version\"\\s*:\\s*\"([^\"]+)\"")
                    .Match(json);

                var package = new PackageInfo
                {
                    Dir = dir,
                    Json = json,
                    Name = nameMatch.Success ? nameMatch.Groups[1].Value : null,
                    Version = versionMatch.Success ? versionMatch.Groups[1].Value : null
                };

                if (package.Name == null)
                    errors.Add(RelativePath(jsonPath) + ": package.json 缺少 name。");
                if (package.Version == null)
                    errors.Add(RelativePath(jsonPath) + ": package.json 缺少 version。");

                packages.Add(package);
            }

            if (packages.Count == 0)
                errors.Add("Assets 下没有找到任何包含 package.json 的包目录。");

            return packages;
        }

        private static void ValidatePackageJson(PackageInfo package, Dictionary<string, string> names,
            List<string> errors)
        {
            var jsonPath = Path.Combine(package.Dir, "package.json");

            if (!string.IsNullOrEmpty(package.Name))
            {
                if (names.ContainsKey(package.Name))
                {
                    errors.Add(RelativePath(jsonPath) + ": 包名重复 " + package.Name + "（同时出现在 " +
                               names[package.Name] + "）。");
                }
                else
                {
                    names.Add(package.Name, RelativePath(package.Dir));
                }
            }

            if (!string.IsNullOrEmpty(package.Version) && !SemVerRegex.IsMatch(package.Version))
            {
                errors.Add(RelativePath(jsonPath) + ": version 不是合法 SemVer：" + package.Version);
            }
        }

        private static void ValidateReadmeAndChangelog(PackageInfo package, List<string> errors)
        {
            var readme = Path.Combine(package.Dir, "README.md");
            var changelog = Path.Combine(package.Dir, "CHANGELOG.md");

            if (!File.Exists(readme))
                errors.Add(RelativePath(readme) + ": 缺少 README.md。");

            if (!File.Exists(changelog))
            {
                errors.Add(RelativePath(changelog) + ": 缺少 CHANGELOG.md。");
                return;
            }

            if (!string.IsNullOrEmpty(package.Version))
            {
                var entryRegex = new Regex(
                    @"^##\s*\[?" + Regex.Escape(package.Version) + @"\]?",
                    RegexOptions.Multiline);
                if (!entryRegex.IsMatch(File.ReadAllText(changelog)))
                {
                    errors.Add(RelativePath(changelog) + ": 缺少当前版本 " + package.Version + " 的条目。");
                }
            }
        }

        private static void ValidateAsmdefs(PackageInfo package, List<PackageInfo> packages,
            List<string> errors)
        {
            var asmdefNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var asmdefOwner = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var file in Directory.GetFiles(Application.dataPath, "*.asmdef",
                         SearchOption.AllDirectories))
            {
                var json = File.ReadAllText(file);
                var match = AsmdefNameRegex.Match(json);
                if (!match.Success)
                    continue;

                var name = match.Groups[1].Value;
                if (!asmdefNames.ContainsKey(name))
                    asmdefNames.Add(name, file);

                foreach (var candidate in packages)
                {
                    if (IsInside(candidate.Dir, file))
                    {
                        asmdefOwner[name] = candidate.Name;
                        break;
                    }
                }
            }

            var dependencies = ParseDependencies(package.Json);

            foreach (var file in Directory.GetFiles(package.Dir, "*.asmdef",
                         SearchOption.AllDirectories))
            {
                var json = File.ReadAllText(file);
                var referencesMatch = AsmdefReferencesRegex.Match(json);
                if (!referencesMatch.Success)
                    continue;

                foreach (Match item in Regex.Matches(referencesMatch.Groups["body"].Value, "\"([^\"]+)\""))
                {
                    var reference = item.Groups[1].Value;
                    if (reference.StartsWith("GUID:", StringComparison.Ordinal))
                    {
                        errors.Add(RelativePath(file) + ": 使用了 GUID 引用 " + reference +
                                   "，包内引用必须使用程序集名称（D009 §3.2）。");
                        continue;
                    }

                    if (reference.StartsWith("Unity.", StringComparison.Ordinal) ||
                        reference.StartsWith("UnityEngine", StringComparison.Ordinal))
                        continue;

                    if (asmdefNames.ContainsKey(reference))
                    {
                        if (asmdefOwner.TryGetValue(reference, out var owner))
                        {
                            if (owner == package.Name || dependencies.ContainsKey(owner))
                                continue;

                            errors.Add(RelativePath(file) + ": 引用了未声明的包外程序集 " + reference +
                                       "（所属包 " + owner + "）。");
                        }
                        else
                        {
                            errors.Add(RelativePath(file) + ": 引用了包外程序集 " + reference +
                                       "（不属于任何 Nexus 包）。");
                        }
                    }
                    else
                    {
                        errors.Add(RelativePath(file) + ": 引用了无法识别的程序集 " + reference + "。");
                    }
                }
            }
        }

        private static void ValidateSamples(PackageInfo package, List<string> errors)
        {
            var samplesDir = Path.Combine(package.Dir, "Samples~");
            var declared = new HashSet<string>(StringComparer.Ordinal);

            var block = SamplesBlockRegex.Match(package.Json);
            if (block.Success)
            {
                foreach (Match item in Regex.Matches(block.Groups["body"].Value,
                             "\"path\"\\s*:\\s*\"([^\"]+)\""))
                {
                    var path = item.Groups[1].Value.Replace('\\', '/');
                    const string prefix = "Samples~/";
                    if (path.StartsWith(prefix, StringComparison.Ordinal))
                        path = path.Substring(prefix.Length);
                    declared.Add(path.TrimEnd('/'));
                }
            }

            var actual = new HashSet<string>(StringComparer.Ordinal);
            if (Directory.Exists(samplesDir))
            {
                foreach (var dir in Directory.GetDirectories(samplesDir))
                    actual.Add(Path.GetFileName(dir));
            }

            foreach (var path in declared)
            {
                if (!actual.Contains(path))
                    errors.Add(RelativePath(package.Dir) + ": samples 声明的目录不存在 Samples~/" + path + "。");
            }

            foreach (var path in actual)
            {
                if (!declared.Contains(path))
                    errors.Add(RelativePath(package.Dir) + ": Samples~/" + path +
                               " 未在 package.json 的 samples 中登记。");
            }
        }

        private static void ValidateDocumentation(PackageInfo package, List<string> errors)
        {
            var docDir = Path.Combine(package.Dir, "Documentation~");
            if (!Directory.Exists(docDir))
                return;

            var index = Path.Combine(docDir, "index.md");
            if (!File.Exists(index))
                errors.Add(RelativePath(index) + ": Documentation~ 存在时必须提供 index.md。");
        }

        private static void ValidateTransitionalApis(PackageInfo package, List<string> errors)
        {
            var specs = TransitionalApiScanner.ScanPackage(package.Dir);
            TransitionalApiScanner.ValidateSpecs(package.Dir, package.Version, specs, errors);

            var readme = Path.Combine(package.Dir, "README.md");
            if (!File.Exists(readme))
                return;

            var text = File.ReadAllText(readme);
            if (text.IndexOf("<!-- BEGIN TRANSITIONAL APIS", StringComparison.Ordinal) < 0 ||
                text.IndexOf("<!-- END TRANSITIONAL APIS -->", StringComparison.Ordinal) < 0)
            {
                errors.Add(RelativePath(readme) +
                           ": 缺少过渡 API 受控区块标记（BEGIN/END TRANSITIONAL APIS）。");
            }
        }

        private static Dictionary<string, string> ParseDependencies(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var block = DependenciesBlockRegex.Match(json);
            if (!block.Success)
                return result;

            foreach (Match item in StringFieldRegex.Matches(block.Groups["body"].Value))
                result[item.Groups["key"].Value] = item.Groups["value"].Value;
            return result;
        }

        private static bool IsInside(string directory, string path)
        {
            var root = Path.GetFullPath(directory).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(path);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private static string RelativePath(string path)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var full = Path.GetFullPath(path);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                full = full.Substring(root.Length).TrimStart('/', '\\');
            return full.Replace('\\', '/');
        }
    }
}
