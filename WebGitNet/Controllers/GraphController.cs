﻿//-----------------------------------------------------------------------
// <copyright file="GraphController.cs" company="(none)">
//  Copyright © 2012 John Gietzen. All rights reserved.
// </copyright>
// <author>John Gietzen</author>
//-----------------------------------------------------------------------

using System.Web.Configuration;

namespace WebGitNet.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Web.Mvc;
    using System.Web.Routing;
    using WebGitNet.Models;

    public class GraphController : SharedControllerBase
    {
        public ActionResult ViewGraph(string repo, string branch, int page = 1)
        {
            var resourceInfo = this.FileManager.GetResourceInfo(repo);
            if (resourceInfo.Type != ResourceType.Directory || page < 1)
            {
                return HttpNotFound();
            }

            int PageSize = 50;
            int skip = PageSize * (page - 1);
            var count = GitUtilities.CountCommits(resourceInfo.FullPath, allRefs: true);

            if (skip >= count)
            {
                return HttpNotFound();
            }

            this.BreadCrumbs.Append("Browse", "Index", "Browse");
            AddRepoBreadCrumb(new RepoInfo(repo, branch));
            this.BreadCrumbs.Append("Graph", "ViewGraph", "View Graph", new { repo });

            var commits = GetLogEntries(resourceInfo.FullPath, skip + PageSize).Skip(skip).ToList();

            ViewBag.Page = page;
            ViewBag.PageCount = (count / PageSize) + (count % PageSize > 0 ? 1 : 0);
            ViewBag.RepoName = resourceInfo.Name;

            return View(commits);
        }

        public List<GraphEntry> GetLogEntries(string path, int count)
        {
            var nodeColors = new Dictionary<string, int>();
            var colorNumber = 0;

            var entries = GitUtilities.GetLogEntries(path, count + 1000, allRefs: true);
            var refs = GitUtilities.GetAllRefs(path);

            var results = new List<GraphEntry>();

            var incoming = new List<string>();
            foreach (var entry in entries.Take(count))
            {
                var color = ColorNode(entry, nodeColors, ref colorNumber);

                results.Add(new GraphEntry
                {
                    LogEntry = entry,
                    Refs = refs.Where(r => r.ShaId == entry.CommitHash).ToList(),

                    Node = new NodeInfo { Hash = entry.CommitHash, Color = color },
                    IncomingNodes = incoming.Select(i => new NodeInfo { Hash = i, Color = nodeColors[i] }).ToList(),
                    ParentNodes = entry.Parents.Select(i => new NodeInfo { Hash = i, Color = nodeColors[i] }).ToList(),
                });

                incoming = BuildOutgoing(incoming, entry);
            }

            return results;
        }

        private static int ColorNode(LogEntry entry, Dictionary<string, int> nodeColors, ref int colorNumber)
        {
            int color;
            if (!nodeColors.TryGetValue(entry.CommitHash, out color))
            {
                nodeColors[entry.CommitHash] = color = colorNumber++;
            }

            bool transferedColor = false;
            foreach (var parent in entry.Parents)
            {
                if (!nodeColors.ContainsKey(parent))
                {
                    if (!transferedColor)
                    {
                        transferedColor = true;
                        nodeColors[parent] = color;
                    }
                    else
                    {
                        nodeColors[parent] = colorNumber++;
                    }
                }
            }

            return color;
        }

        private List<string> BuildOutgoing(List<string> incoming, LogEntry entry)
        {
            var outgoing = incoming.ToList();
            var col = outgoing.IndexOf(entry.CommitHash);
            if (col == -1)
            {
                col = outgoing.Count;
                outgoing.Add(entry.CommitHash);
            }

            var replaced = false;
            for (var p = 0; p < entry.Parents.Count; p++)
            {
                if (outgoing.IndexOf(entry.Parents[p]) == -1)
                {
                    if (!replaced)
                    {
                        outgoing[col] = entry.Parents[p];
                        replaced = true;
                    }
                    else
                    {
                        outgoing.Add(entry.Parents[p]);
                    }
                }
            }

            if (!replaced)
            {
                outgoing.RemoveAt(col);
            }

            return outgoing;
        }

        private class LogEntryComparer : IComparer<LogEntry>
        {
            private static readonly LogEntryComparer instance = new LogEntryComparer();

            private LogEntryComparer()
            {
            }

            public static LogEntryComparer Instance
            {
                get { return instance; }
            }

            public int Compare(LogEntry x, LogEntry y)
            {
                var c = x.CommitterDate.CompareTo(y.CommitterDate);

                if (c == 0)
                {
                    c = x.CommitHash.CompareTo(y.CommitHash);
                }

                return c;
            }
        }

        public class RouteRegisterer : IRouteRegisterer
        {
            public void RegisterRoutes(RouteCollection routes)
            {
                routes.MapRoute(
                    "View Graph",
                    "browse/{repo}/{branch}/graph",
                    new { controller = "Graph", action = "ViewGraph", routeName = "View Graph" });

                routes.MapResource("Content/graph.css", "text/css");
                routes.MapResource("Scripts/graph.js", "text/javascript");
            }
        }
    }
}