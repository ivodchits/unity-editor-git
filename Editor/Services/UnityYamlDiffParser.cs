using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace GitEditor
{
    public static class UnityYamlDiffParser
    {
        static readonly Regex ObjectHeaderRegex = new Regex(@"^[ +-]*--- !u!(\d+) &(\d+)", RegexOptions.Compiled);
        static readonly Regex NameRegex = new Regex(@"^\s*m_Name:\s*(.*)", RegexOptions.Compiled);
        static readonly Regex GameObjectRefRegex = new Regex(@"^\s*m_GameObject:\s*\{fileID:\s*(\d+)", RegexOptions.Compiled);
        static readonly Regex FatherRefRegex = new Regex(@"^\s*m_Father:\s*\{fileID:\s*(\d+)", RegexOptions.Compiled);
        static readonly Regex ScriptRefRegex = new Regex(@"^\s*m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);
        static readonly Regex ComponentRefRegex = new Regex(@"^\s*-\s*component:\s*\{fileID:\s*(\d+)", RegexOptions.Compiled);
        static readonly Regex KeyValueRegex = new Regex(@"^(\s*)([\w.]+):\s*(.*)", RegexOptions.Compiled);
        static readonly Regex ChildrenRefRegex = new Regex(@"^\s*-\s*\{fileID:\s*(\d+)\}", RegexOptions.Compiled);

        static readonly Dictionary<int, string> ClassNames = new Dictionary<int, string>
        {
            { 1, "GameObject" }, { 4, "Transform" }, { 20, "Camera" },
            { 23, "MeshRenderer" }, { 25, "Renderer" }, { 33, "MeshFilter" },
            { 50, "Rigidbody2D" }, { 54, "Rigidbody" },
            { 58, "CircleCollider2D" }, { 61, "BoxCollider2D" },
            { 65, "BoxCollider" }, { 82, "AudioSource" },
            { 95, "Animator" }, { 102, "TextMesh" },
            { 108, "Light" }, { 111, "Animation" },
            { 114, "MonoBehaviour" }, { 120, "LineRenderer" },
            { 135, "SphereCollider" }, { 136, "CapsuleCollider" },
            { 137, "SkinnedMeshRenderer" },
            { 198, "ParticleSystem" },
            { 212, "SpriteRenderer" },
            { 222, "Canvas" }, { 223, "CanvasGroup" },
            { 224, "RectTransform" }, { 225, "CanvasRenderer" },
            { 226, "CanvasScaler" }
        };

        public static List<AssetDiffEntry> Parse(List<GitFileDiff> diffs, string filePath = null, string commitHash = null)
        {
            // Collect all diff lines with their prefixes
            var allLines = new List<string>();
            foreach (var fileDiff in diffs)
                foreach (var hunk in fileDiff.Hunks)
                    allLines.AddRange(hunk.Lines);

            // Check for binary file
            if (allLines.Any(l => l.Contains("Binary files") && l.Contains("differ")))
            {
                return new List<AssetDiffEntry>
                {
                    new AssetDiffEntry { ChangeType = AssetDiffChangeType.Unrecognized, RawLine = "Binary file changed" }
                };
            }

            // Build context from the full file if available
            var fileContext = BuildFileContext(filePath, commitHash);

            // Pass 1: Build context maps from diff lines
            var diffContext = BuildDiffContext(allLines);

            // Merge file context into diff context
            MergeContexts(diffContext, fileContext);

            // Pass 2: Classify changes
            return ClassifyChanges(allLines, diffContext);
        }

        static DiffContext BuildFileContext(string filePath, string commitHash)
        {
            var ctx = new DiffContext();
            string content = null;

            if (!string.IsNullOrEmpty(filePath))
            {
                if (string.IsNullOrEmpty(commitHash))
                {
                    // Working copy: try reading HEAD version for context
                    var result = GitCommandRunner.Run("show", "HEAD:" + filePath);
                    if (result.Success)
                        content = result.Output;
                }
                else
                {
                    var result = GitCommandRunner.Run("show", commitHash + ":" + filePath);
                    if (result.Success)
                        content = result.Output;
                }
            }

            if (string.IsNullOrEmpty(content))
                return ctx;

            long currentFileID = 0;
            int currentClassID = 0;
            string currentTypeName = null;
            bool inChildren = false;
            List<long> currentChildren = null;

            foreach (string line in content.Split('\n'))
            {
                var headerMatch = ObjectHeaderRegex.Match(line);
                if (headerMatch.Success)
                {
                    // Save children list from previous Transform
                    if (currentChildren != null && currentFileID != 0)
                        ctx.ChildrenMap[currentFileID] = currentChildren;

                    int classID = int.Parse(headerMatch.Groups[1].Value);
                    long fileID = long.Parse(headerMatch.Groups[2].Value);
                    currentFileID = fileID;
                    currentClassID = classID;
                    currentTypeName = null;
                    inChildren = false;
                    currentChildren = null;
                    ctx.ClassMap[fileID] = classID;
                    continue;
                }

                if (currentFileID == 0) continue;

                // Type name line (e.g. "MonoBehaviour:" or "Transform:")
                if (currentTypeName == null && !line.StartsWith(" ") && line.EndsWith(":") && line.Trim().Length > 1)
                {
                    currentTypeName = line.Trim().TrimEnd(':');
                    if (!string.IsNullOrEmpty(currentTypeName))
                        ctx.TypeNameMap[currentFileID] = currentTypeName;
                }

                var nameMatch = NameRegex.Match(line);
                if (nameMatch.Success)
                {
                    string name = nameMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(name))
                        ctx.NameMap[currentFileID] = name;
                }

                var goMatch = GameObjectRefRegex.Match(line);
                if (goMatch.Success)
                {
                    long goID = long.Parse(goMatch.Groups[1].Value);
                    if (goID != 0)
                        ctx.GameObjectMap[currentFileID] = goID;
                }

                var fatherMatch = FatherRefRegex.Match(line);
                if (fatherMatch.Success)
                {
                    long fatherID = long.Parse(fatherMatch.Groups[1].Value);
                    ctx.ParentMap[currentFileID] = fatherID;
                }

                var scriptMatch = ScriptRefRegex.Match(line);
                if (scriptMatch.Success)
                {
                    ctx.ScriptGuidMap[currentFileID] = scriptMatch.Groups[1].Value;
                }

                // Track m_Children list
                if (line.Trim() == "m_Children:")
                {
                    inChildren = true;
                    currentChildren = new List<long>();
                    continue;
                }
                if (inChildren)
                {
                    var childMatch = ChildrenRefRegex.Match(line);
                    if (childMatch.Success)
                    {
                        currentChildren?.Add(long.Parse(childMatch.Groups[1].Value));
                    }
                    else if (!string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("-"))
                    {
                        inChildren = false;
                        if (currentChildren != null && currentFileID != 0)
                            ctx.ChildrenMap[currentFileID] = currentChildren;
                        currentChildren = null;
                    }
                }
            }

            // Save last children list
            if (currentChildren != null && currentFileID != 0)
                ctx.ChildrenMap[currentFileID] = currentChildren;

            return ctx;
        }

        static DiffContext BuildDiffContext(List<string> lines)
        {
            var ctx = new DiffContext();
            long currentFileID = 0;
            int currentClassID = 0;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Length > 0 && (rawLine[0] == '+' || rawLine[0] == '-' || rawLine[0] == ' ')
                    ? rawLine.Substring(1)
                    : rawLine;

                var headerMatch = ObjectHeaderRegex.Match(line);
                if (headerMatch.Success)
                {
                    int classID = int.Parse(headerMatch.Groups[1].Value);
                    long fileID = long.Parse(headerMatch.Groups[2].Value);
                    currentFileID = fileID;
                    currentClassID = classID;
                    ctx.ClassMap[fileID] = classID;
                    continue;
                }

                if (currentFileID == 0) continue;

                var nameMatch = NameRegex.Match(line);
                if (nameMatch.Success)
                {
                    string name = nameMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(name))
                        ctx.NameMap[currentFileID] = name;
                }

                var goMatch = GameObjectRefRegex.Match(line);
                if (goMatch.Success)
                {
                    long goID = long.Parse(goMatch.Groups[1].Value);
                    if (goID != 0)
                        ctx.GameObjectMap[currentFileID] = goID;
                }

                var fatherMatch = FatherRefRegex.Match(line);
                if (fatherMatch.Success)
                {
                    long fatherID = long.Parse(fatherMatch.Groups[1].Value);
                    ctx.ParentMap[currentFileID] = fatherID;
                }

                var scriptMatch = ScriptRefRegex.Match(line);
                if (scriptMatch.Success)
                {
                    ctx.ScriptGuidMap[currentFileID] = scriptMatch.Groups[1].Value;
                }
            }

            return ctx;
        }

        static void MergeContexts(DiffContext target, DiffContext source)
        {
            foreach (var kv in source.ClassMap)
                if (!target.ClassMap.ContainsKey(kv.Key))
                    target.ClassMap[kv.Key] = kv.Value;

            foreach (var kv in source.NameMap)
                if (!target.NameMap.ContainsKey(kv.Key))
                    target.NameMap[kv.Key] = kv.Value;

            foreach (var kv in source.GameObjectMap)
                if (!target.GameObjectMap.ContainsKey(kv.Key))
                    target.GameObjectMap[kv.Key] = kv.Value;

            foreach (var kv in source.ParentMap)
                if (!target.ParentMap.ContainsKey(kv.Key))
                    target.ParentMap[kv.Key] = kv.Value;

            foreach (var kv in source.ScriptGuidMap)
                if (!target.ScriptGuidMap.ContainsKey(kv.Key))
                    target.ScriptGuidMap[kv.Key] = kv.Value;

            foreach (var kv in source.TypeNameMap)
                if (!target.TypeNameMap.ContainsKey(kv.Key))
                    target.TypeNameMap[kv.Key] = kv.Value;

            foreach (var kv in source.ChildrenMap)
                if (!target.ChildrenMap.ContainsKey(kv.Key))
                    target.ChildrenMap[kv.Key] = kv.Value;
        }

        static List<AssetDiffEntry> ClassifyChanges(List<string> lines, DiffContext ctx)
        {
            var entries = new List<AssetDiffEntry>();
            var processedIndices = new HashSet<int>();
            long currentFileID = 0;
            int currentClassID = 0;
            bool currentBlockIsAdded = false;
            bool currentBlockIsRemoved = false;
            long currentBlockStartFileID = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                if (processedIndices.Contains(i)) continue;

                string rawLine = lines[i];
                if (string.IsNullOrWhiteSpace(rawLine)) continue;

                char prefix = rawLine.Length > 0 ? rawLine[0] : ' ';
                string line = rawLine.Length > 1 ? rawLine.Substring(1) : "";

                // Track current object block
                var headerMatch = ObjectHeaderRegex.Match(line);
                if (headerMatch.Success)
                {
                    int classID = int.Parse(headerMatch.Groups[1].Value);
                    long fileID = long.Parse(headerMatch.Groups[2].Value);
                    currentFileID = fileID;
                    currentClassID = classID;

                    if (prefix == '+')
                    {
                        currentBlockIsAdded = true;
                        currentBlockIsRemoved = false;
                        currentBlockStartFileID = fileID;
                        processedIndices.Add(i);

                        if (classID == 1) // GameObject added
                        {
                            entries.Add(new AssetDiffEntry
                            {
                                ChangeType = AssetDiffChangeType.GameObjectAdded,
                                ObjectName = BuildObjectPath(fileID, ctx)
                            });
                        }
                        else // Component added
                        {
                            string goName = BuildObjectPath(fileID, ctx);
                            entries.Add(new AssetDiffEntry
                            {
                                ChangeType = AssetDiffChangeType.ComponentAdded,
                                ObjectName = goName,
                                ComponentName = ResolveComponentName(fileID, classID, ctx)
                            });
                        }
                        // Skip remaining lines in this added block
                        SkipBlock(lines, i + 1, '+', processedIndices);
                        continue;
                    }
                    else if (prefix == '-')
                    {
                        currentBlockIsAdded = false;
                        currentBlockIsRemoved = true;
                        currentBlockStartFileID = fileID;
                        processedIndices.Add(i);

                        if (classID == 1) // GameObject removed
                        {
                            entries.Add(new AssetDiffEntry
                            {
                                ChangeType = AssetDiffChangeType.GameObjectRemoved,
                                ObjectName = BuildObjectPath(fileID, ctx)
                            });
                        }
                        else // Component removed
                        {
                            string goName = BuildObjectPath(fileID, ctx);
                            entries.Add(new AssetDiffEntry
                            {
                                ChangeType = AssetDiffChangeType.ComponentRemoved,
                                ObjectName = goName,
                                ComponentName = ResolveComponentName(fileID, classID, ctx)
                            });
                        }
                        SkipBlock(lines, i + 1, '-', processedIndices);
                        continue;
                    }
                    else
                    {
                        currentBlockIsAdded = false;
                        currentBlockIsRemoved = false;
                    }
                    continue;
                }

                // Skip context lines
                if (prefix == ' ') continue;

                // Handle component reference changes in m_Component array
                var compRefLine = line.TrimStart();
                var compRefMatch = ComponentRefRegex.Match(compRefLine);
                if (compRefMatch.Success)
                {
                    long compFileID = long.Parse(compRefMatch.Groups[1].Value);
                    string goName = BuildObjectPath(currentFileID, ctx);

                    int compClassID = 0;
                    ctx.ClassMap.TryGetValue(compFileID, out compClassID);

                    if (prefix == '+')
                    {
                        processedIndices.Add(i);
                        entries.Add(new AssetDiffEntry
                        {
                            ChangeType = AssetDiffChangeType.ComponentAdded,
                            ObjectName = goName,
                            ComponentName = ResolveComponentName(compFileID, compClassID, ctx)
                        });
                        continue;
                    }
                    else if (prefix == '-')
                    {
                        processedIndices.Add(i);
                        entries.Add(new AssetDiffEntry
                        {
                            ChangeType = AssetDiffChangeType.ComponentRemoved,
                            ObjectName = goName,
                            ComponentName = ResolveComponentName(compFileID, compClassID, ctx)
                        });
                        continue;
                    }
                }

                // Handle property value changes: look for consecutive -/+ pairs with same key
                if (prefix == '-')
                {
                    var kvMatch = KeyValueRegex.Match(line);
                    if (kvMatch.Success && i + 1 < lines.Count)
                    {
                        string nextRaw = lines[i + 1];
                        if (nextRaw.Length > 0 && nextRaw[0] == '+')
                        {
                            string nextLine = nextRaw.Substring(1);
                            var nextKvMatch = KeyValueRegex.Match(nextLine);
                            if (nextKvMatch.Success && kvMatch.Groups[2].Value == nextKvMatch.Groups[2].Value)
                            {
                                processedIndices.Add(i);
                                processedIndices.Add(i + 1);

                                string propName = kvMatch.Groups[2].Value;
                                string oldVal = kvMatch.Groups[3].Value.Trim();
                                string newVal = nextKvMatch.Groups[3].Value.Trim();

                                string objName = ResolveGameObjectName(currentFileID, ctx);
                                if (string.IsNullOrEmpty(objName))
                                    objName = ResolveName(currentFileID, ctx);

                                string compName = ResolveComponentName(currentFileID, currentClassID, ctx);

                                entries.Add(new AssetDiffEntry
                                {
                                    ChangeType = AssetDiffChangeType.PropertyChanged,
                                    ObjectName = BuildObjectPath(currentFileID, ctx),
                                    ComponentName = compName,
                                    PropertyName = propName,
                                    OldValue = oldVal,
                                    NewValue = newVal
                                });
                                continue;
                            }
                        }
                    }
                }

                // Handle standalone added/removed lines that are property-like
                if (prefix == '+' || prefix == '-')
                {
                    var kvMatch = KeyValueRegex.Match(line);
                    if (kvMatch.Success)
                    {
                        processedIndices.Add(i);
                        string propName = kvMatch.Groups[2].Value;
                        string value = kvMatch.Groups[3].Value.Trim();

                        string objName = BuildObjectPath(currentFileID, ctx);
                        string compName = ResolveComponentName(currentFileID, currentClassID, ctx);

                        if (prefix == '+')
                        {
                            entries.Add(new AssetDiffEntry
                            {
                                ChangeType = AssetDiffChangeType.PropertyChanged,
                                ObjectName = objName,
                                ComponentName = compName,
                                PropertyName = propName,
                                OldValue = "(none)",
                                NewValue = value
                            });
                        }
                        else
                        {
                            entries.Add(new AssetDiffEntry
                            {
                                ChangeType = AssetDiffChangeType.PropertyChanged,
                                ObjectName = objName,
                                ComponentName = compName,
                                PropertyName = propName,
                                OldValue = value,
                                NewValue = "(removed)"
                            });
                        }
                        continue;
                    }

                    // Truly unrecognized
                    processedIndices.Add(i);
                    entries.Add(new AssetDiffEntry
                    {
                        ChangeType = AssetDiffChangeType.Unrecognized,
                        RawLine = rawLine
                    });
                }
            }

            // Deduplicate and consolidate
            return ConsolidateEntries(entries);
        }

        static void SkipBlock(List<string> lines, int startIndex, char expectedPrefix, HashSet<int> processed)
        {
            for (int i = startIndex; i < lines.Count; i++)
            {
                if (lines[i].Length == 0) continue;
                char prefix = lines[i][0];
                if (prefix == expectedPrefix)
                {
                    processed.Add(i);
                }
                else
                {
                    break;
                }
            }
        }

        static string ResolveName(long fileID, DiffContext ctx)
        {
            if (fileID == 0) return null;
            string name;
            if (ctx.NameMap.TryGetValue(fileID, out name) && !string.IsNullOrEmpty(name))
                return name;
            return null;
        }

        static string ResolveGameObjectName(long componentFileID, DiffContext ctx)
        {
            long goFileID;
            if (ctx.GameObjectMap.TryGetValue(componentFileID, out goFileID))
                return ResolveName(goFileID, ctx);

            // If this IS a GameObject, return its own name
            int classID;
            if (ctx.ClassMap.TryGetValue(componentFileID, out classID) && classID == 1)
                return ResolveName(componentFileID, ctx);

            return null;
        }

        static string ResolveComponentName(long fileID, int classID, DiffContext ctx)
        {
            // For MonoBehaviour, try to resolve script name
            if (classID == 114)
            {
                string guid;
                if (ctx.ScriptGuidMap.TryGetValue(fileID, out guid))
                {
                    string scriptName = ResolveScriptName(guid);
                    if (!string.IsNullOrEmpty(scriptName))
                        return scriptName;
                }
            }

            // Try type name from file context
            string typeName;
            if (ctx.TypeNameMap.TryGetValue(fileID, out typeName) && !string.IsNullOrEmpty(typeName))
                return typeName;

            // Fall back to class ID lookup
            string className;
            if (classID > 0 && ClassNames.TryGetValue(classID, out className))
                return className;

            if (classID > 0)
                return $"Component({classID})";

            return "Unknown";
        }

        static string ResolveScriptName(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;

            try
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                    string className = monoScript?.GetClass()?.Name;
                    if (!string.IsNullOrEmpty(className))
                        return className;

                    // Fall back to file name if the class couldn't be reflected
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    if (!string.IsNullOrEmpty(fileName))
                        return fileName;
                }
            }
            catch
            {
                // Silently fall back
            }

            return null;
        }

        static string BuildObjectPath(long fileID, DiffContext ctx)
        {
            if (fileID == 0) return null;

            // Find the GameObject for this fileID
            long goFileID = fileID;
            int classID;
            if (ctx.ClassMap.TryGetValue(fileID, out classID) && classID != 1)
            {
                // This is a component, find its GameObject
                long goID;
                if (ctx.GameObjectMap.TryGetValue(fileID, out goID) && goID != 0)
                    goFileID = goID;
                else
                    return ResolveName(fileID, ctx);
            }

            // Build path by walking Transform hierarchy
            return BuildGameObjectPath(goFileID, ctx);
        }

        static string BuildGameObjectPath(long goFileID, DiffContext ctx)
        {
            var pathParts = new List<string>();
            var visited = new HashSet<long>();
            long currentGO = goFileID;

            while (currentGO != 0 && !visited.Contains(currentGO))
            {
                visited.Add(currentGO);
                string name = ResolveName(currentGO, ctx);
                if (!string.IsNullOrEmpty(name))
                    pathParts.Insert(0, name);
                else
                    break;

                // Find Transform for this GameObject
                long transformFileID = FindTransformForGameObject(currentGO, ctx);
                if (transformFileID == 0) break;

                // Find parent Transform
                long parentTransformID;
                if (!ctx.ParentMap.TryGetValue(transformFileID, out parentTransformID) || parentTransformID == 0)
                    break;

                // Find parent Transform's GameObject
                long parentGO;
                if (ctx.GameObjectMap.TryGetValue(parentTransformID, out parentGO) && parentGO != 0)
                    currentGO = parentGO;
                else
                    break;
            }

            return pathParts.Count > 0 ? string.Join("/", pathParts) : null;
        }

        static long FindTransformForGameObject(long goFileID, DiffContext ctx)
        {
            // Find a Transform or RectTransform that references this GameObject
            foreach (var kv in ctx.GameObjectMap)
            {
                if (kv.Value == goFileID)
                {
                    int classID;
                    if (ctx.ClassMap.TryGetValue(kv.Key, out classID) && (classID == 4 || classID == 224 || classID == 183))
                        return kv.Key;
                }
            }
            return 0;
        }

        static List<AssetDiffEntry> ConsolidateEntries(List<AssetDiffEntry> entries)
        {
            var result = new List<AssetDiffEntry>();
            var seen = new HashSet<string>();

            // Build suppression sets so PropertyChanged noise for fully added/removed
            // components and GameObjects is filtered out.
            var addedOrRemovedComponents = new HashSet<string>(); // "objectName:componentName"
            var addedOrRemovedObjects    = new HashSet<string>(); // "objectName"
            foreach (var entry in entries)
            {
                if (entry.ChangeType == AssetDiffChangeType.ComponentAdded ||
                    entry.ChangeType == AssetDiffChangeType.ComponentRemoved)
                    addedOrRemovedComponents.Add($"{entry.ObjectName}:{entry.ComponentName}");
                else if (entry.ChangeType == AssetDiffChangeType.GameObjectAdded ||
                         entry.ChangeType == AssetDiffChangeType.GameObjectRemoved)
                    addedOrRemovedObjects.Add(entry.ObjectName);
            }

            foreach (var entry in entries)
            {
                // Skip duplicate component add/remove entries (can happen from both header and m_Component list)
                if (entry.ChangeType == AssetDiffChangeType.ComponentAdded ||
                    entry.ChangeType == AssetDiffChangeType.ComponentRemoved)
                {
                    string key = $"{entry.ChangeType}:{entry.ObjectName}:{entry.ComponentName}";
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                }

                // Suppress property changes that belong to a fully added/removed component or object
                if (entry.ChangeType == AssetDiffChangeType.PropertyChanged)
                {
                    if (addedOrRemovedComponents.Contains($"{entry.ObjectName}:{entry.ComponentName}"))
                        continue;
                    if (addedOrRemovedObjects.Contains(entry.ObjectName))
                        continue;
                }

                // Skip internal Unity property changes that aren't meaningful to users
                if (entry.ChangeType == AssetDiffChangeType.PropertyChanged && IsInternalProperty(entry.PropertyName))
                    continue;

                result.Add(entry);
            }

            return result;
        }

        static bool IsInternalProperty(string propName)
        {
            if (string.IsNullOrEmpty(propName)) return false;
            // These properties change frequently but aren't meaningful user changes
            return propName == "serializedVersion" || propName == "m_ObjectHideFlags";
        }

        class DiffContext
        {
            public Dictionary<long, int> ClassMap = new Dictionary<long, int>();         // fileID -> classID
            public Dictionary<long, string> NameMap = new Dictionary<long, string>();    // fileID -> m_Name
            public Dictionary<long, long> GameObjectMap = new Dictionary<long, long>();  // fileID -> gameObject fileID
            public Dictionary<long, long> ParentMap = new Dictionary<long, long>();      // transform fileID -> parent transform fileID
            public Dictionary<long, string> ScriptGuidMap = new Dictionary<long, string>(); // fileID -> script GUID
            public Dictionary<long, string> TypeNameMap = new Dictionary<long, string>();   // fileID -> type name from YAML
            public Dictionary<long, List<long>> ChildrenMap = new Dictionary<long, List<long>>(); // transform fileID -> children transform fileIDs
        }
    }
}
