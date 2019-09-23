// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitHub.IssueLabeler;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Github.IssueLabeler
{
    public static class Train
    {
        public static void SaveFromXToY(string input, string output, int numToSkip, int length = -1)
        {
            var lines = File.ReadAllLines(input);
            var span = lines.AsSpan();
            var header = span.Slice(0, 1).ToArray(); // include header
            File.WriteAllLines(output, header);
            span = span.Slice(numToSkip + 1, span.Length - (numToSkip + 1));
            if (length != -1)
            {
                span = span.Slice(0, length); // include header
            }
            lines = span.ToArray();
            File.AppendAllLines(output, lines);
        }

        public static void AddOrRemoveColumns(string input, string output)
        {
            var githubIssue = new GitHubIssue();
            var sb = new StringBuilder();
            var lines = File.ReadAllLines(input);
            Debug.Assert(lines[0].Equals("ID\tArea\tTitle\tDescription\tIsPR\tFilePaths\tPossibleOwners", StringComparison.OrdinalIgnoreCase));
            string newHeader = "Area\tTitle\tDescription\tIsPR\tFilenames\tPopularFolders\tPossibleOwners";
            string[] newLines = new string[lines.Length];
            newLines[0] = newHeader;
            string line; // current line
            for (int i = 1; i < lines.Length; i++) // skipping header
            {
                sb.Clear();
                githubIssue.Clear();
                line = lines[i];
                string[] lineSplitByTab = line.Split('\t');
                Debug.Assert(int.TryParse(lineSplitByTab[0], out int _)); // skip ID
                githubIssue.Area = lineSplitByTab[1];
                githubIssue.Title = lineSplitByTab[2];
                githubIssue.Body = lineSplitByTab[3];
                githubIssue.IsPR = lineSplitByTab[4].Equals(true.ToString(), StringComparison.OrdinalIgnoreCase);
                sb.Append(githubIssue.Area)
                    .Append('\t').Append(githubIssue.Title)
                    .Append('\t').Append(githubIssue.Body)
                    .Append('\t').Append(githubIssue.IsPR);
                if (githubIssue.IsPR)
                {
                    var pathAnalyzer = new FilePathAnalyzer();
                    string[] filePaths = lineSplitByTab[5].Split(';');
                    pathAnalyzer.FillRestFromFilePaths(githubIssue, filePaths);

                    sb.Append('\t').Append(githubIssue.Filenames)
                        .Append('\t').Append(githubIssue.PopularFolders);
                }
                else
                {
                    sb.Append('\t').Append('\t');
                }
                if (string.IsNullOrEmpty(githubIssue.PossibleOwners))
                {
                    githubIssue.PossibleOwners = lineSplitByTab[6];
                }
                sb.Append('\t').Append(githubIssue.PossibleOwners);
                newLines[i] = sb.ToString().Replace('"', '`');
            }
            File.WriteAllLines(output, newLines);
        }
    }
}
