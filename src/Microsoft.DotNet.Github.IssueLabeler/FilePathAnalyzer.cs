// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitHub.IssueLabeler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Github.IssueLabeler
{
    internal static class GitHubIssueExtensions
    {
        public static void Clear(this GitHubIssue gitHubIssue)
        {
            gitHubIssue.Area = default;
            gitHubIssue.Body = default;
            gitHubIssue.Filenames = default;
            gitHubIssue.IsPR = default;
            gitHubIssue.IssueOrPr = default;
            gitHubIssue.Labels = default;
            gitHubIssue.Milestone = default;
            gitHubIssue.PopularFolders = default;
            gitHubIssue.PossibleOwners = default;
            gitHubIssue.Title = default;
        }
    }

    internal class FilePathAnalyzer
    {
        private SortedList<FileOrFolderWithDiff, List<string>> _popularFolders = new SortedList<FileOrFolderWithDiff, List<string>>(new PathComparer());
        private Dictionary<string, FileOrFolderWithDiff> _nodes = new Dictionary<string, FileOrFolderWithDiff>();
        private readonly Random _rnd = new Random();
        private readonly Regex _regex = new Regex(@"@[a-zA-Z0-9_//-]+");
        private readonly StringBuilder _sb = new StringBuilder();

        internal void FillRestFromFilePaths(GitHubIssue gitHubIssue, string[] filePaths)
        {
            if (gitHubIssue.IsPR && filePaths != null && !string.IsNullOrEmpty(string.Join(';', filePaths)))
            {
                Setup(filePaths);

                string topItemSpaceSeparated = TopItems(filePaths.Length).Replace(Path.DirectorySeparatorChar, ' ');
                string allSpaceSeparated = AllItems().Replace(Path.DirectorySeparatorChar, ' ');

                gitHubIssue.PopularFolders = Shuffle(_rnd, topItemSpaceSeparated);
                gitHubIssue.Filenames = string.Join(' ', filePaths.Select(filePath => Path.GetFileName(filePath)));
            }
            var possibleOwners = _regex.Matches(gitHubIssue.Body).Select(x => x.Value).ToArray();
            gitHubIssue.PossibleOwners = string.Join(' ', possibleOwners);
        }

        private static string Shuffle(Random rnd, string allPopularPathsSpaceSeparated)
        {
            var popularPathsShuffled = allPopularPathsSpaceSeparated.Split(' ');
            popularPathsShuffled = popularPathsShuffled.OrderBy(x => rnd.Next()).ToArray();
            return string.Join(' ', popularPathsShuffled);
        }

        public void Setup(string[] filePaths)
        {
            Clear();
            string folder, subfolder;
            string[] subfolders;
            int depth;
            string parent;
            var filenames = filePaths.Select(filePath => Path.GetFileName(filePath));
            foreach (var fileWithDiff in filePaths)
            {
                folder = Path.GetDirectoryName(fileWithDiff) ?? string.Empty;
                subfolders = folder.Split(Path.DirectorySeparatorChar);
                subfolder = string.Empty;
                depth = 1;
                parent = null;
                foreach (var curFolder in subfolders)
                {
                    subfolder += curFolder;
                    if (!_nodes.ContainsKey(subfolder))
                    {
                        var node = new FileOrFolderWithDiff()
                        {
                            Depth = depth,
                            Visited = 1,
                            Name = subfolder,
                            ParentName = parent,
                            IsFolder = true
                        };
                        parent = node.Name;
                        _nodes.Add(subfolder, node);
                    }
                    else
                    {
                        parent = _nodes[subfolder].Name;
                        _nodes[subfolder].Visited += 1;
                    }
                    _nodes[parent].NumNestedFiles += 1;
                    subfolder += Path.DirectorySeparatorChar;
                    depth++;
                }
                var fileNode = new FileOrFolderWithDiff()
                {
                    Visited = 1,
                    Depth = depth,
                    ParentName = parent,
                    Name = fileWithDiff
                };
                _nodes.Add(fileWithDiff, fileNode);
                _nodes[parent].NumDirectFiles += 1;
            }
            FindPopularFolder();
        }

        internal void Clear()
        {
            _nodes.Clear();
            _popularFolders.Clear();
        }

        private void FindPopularFolder()
        {
            foreach (var folder in _nodes.Values.Where(f => f.IsFolder))
            {
                if (_popularFolders.ContainsKey(folder))
                {
                    _popularFolders[folder].Add(folder.Name);
                }
                else
                {
                    _popularFolders.Add(folder, new List<string>() { folder.Name });
                }
            }
        }

        internal string AllItems()
        {
            _sb.Clear();
            foreach (var sameRankFolders in _popularFolders.Values)
            {
                foreach (var folder in sameRankFolders)
                {
                    _sb.Append(folder).Append(' ');
                }
            }
            return _sb.ToString();
        }

        internal string TopItems(int total, float percentageToShow = 0.33f, int maxToShow = 10)
        {
            if (total == 0)
                return string.Empty;
            var numToShow = Math.Max(1, (int)(total * percentageToShow));
            numToShow = Math.Min(numToShow, maxToShow);
            _sb.Clear();
            int num = 0;
            foreach (var sameRankFolders in _popularFolders.Values)
            {
                if (num > numToShow)
                {
                    break;
                }
                foreach (var folder in sameRankFolders)
                {
                    _sb.Append(folder).Append(' ');
                }
                num += sameRankFolders.Count;
            }
            return _sb.ToString();
        }
    }

    internal class FileOrFolderWithDiff
    {
        public int Visited { get; set; }
        public string ParentName { get; set; }
        public int Depth { get; set; }
        public string Name { get; set; }
        public int NumNestedFiles { get; set; }
        public int NumDirectFiles { get; set; }
        public bool IsFolder { get; set; }

        public override string ToString()
        {
            return $"V:{Visited} D:{Depth} {Name}";
        }
    }

    internal class PathComparer : IComparer<FileOrFolderWithDiff>
    {
        public int Compare(FileOrFolderWithDiff x, FileOrFolderWithDiff y)
        {
            if (y.NumDirectFiles == x.NumDirectFiles)
            {
                if (y.NumNestedFiles == x.NumNestedFiles)
                {
                    return y.Visited.CompareTo(x.Visited);
                }
                return y.NumNestedFiles.CompareTo(x.NumNestedFiles);
            }
            // [High to low]
            return y.NumDirectFiles.CompareTo(x.NumDirectFiles);
        }
    }
}
