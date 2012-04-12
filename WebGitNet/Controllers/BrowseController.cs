//-----------------------------------------------------------------------
// <copyright file="BrowseController.cs" company="(none)">
//  Copyright © 2011 John Gietzen. All rights reserved.
// </copyright>
// <author>John Gietzen</author>
//-----------------------------------------------------------------------

namespace WebGitNet.Controllers
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Web.Configuration;
    using System.Web.Mvc;
    using System.Web.Routing;
    using WebGitNet.ActionResults;
    using WebGitNet.Models;

    public class BrowseController : SharedControllerBase
    {
        public BrowseController()
        {
            this.BreadCrumbs.Append("Browse", "Index", "Browse");
        }

        public ActionResult Index()
        {
            var directory = this.FileManager.DirectoryInfo;

            var repos = (from dir in directory.EnumerateDirectories()
                         select GitUtilities.GetRepoInfo(dir.FullName)).ToList();

            ViewBag.AllowCreateRepo = bool.Parse(WebConfigurationManager.AppSettings["AllowCreateRepo"]);
            ViewBag.DefaultBranch = WebConfigurationManager.AppSettings["DefaultBranch"];

            return View(repos);
        }

        public ActionResult ViewRepo(string repo, string branch)
        {
            branch = branch == null ? WebConfigurationManager.AppSettings["DefaultBranch"] : branch;
            var repoInfo = new RepoInfo(repo, branch);
            
            var resourceInfo = this.FileManager.GetResourceInfo(repo);
            if (resourceInfo.Type != ResourceType.Directory)
            {
                return HttpNotFound();
            }

            AddRepoBreadCrumb(repoInfo);

            var lastCommit = GitUtilities.GetLogEntries(resourceInfo.FullPath, 1, 0, branch).FirstOrDefault();

            ViewBag.RepoName = resourceInfo.Name;
            ViewBag.LastCommit = lastCommit;
            ViewBag.CurrentTree = lastCommit != null ? GitUtilities.GetTreeInfo(resourceInfo.FullPath, branch) : null;
            ViewBag.Refs = GitUtilities.GetAllRefs(resourceInfo.FullPath);
            ViewBag.Branch = branch;

            ViewBag.CloneUri = PathUtilities.ParseCloneUrl(User, repoInfo);

            return View();
        }

        public ActionResult ViewCommit(string repo, string @object)
        {
            var resourceInfo = this.FileManager.GetResourceInfo(repo);
            if (resourceInfo.Type != ResourceType.Directory)
            {
                return HttpNotFound();
            }

            this.BreadCrumbs.Append("Browse", "ViewCommit", @object, new { repo, @object });

            var commit = GitUtilities.GetLogEntries(resourceInfo.FullPath, 1, 0, @object).FirstOrDefault();
            if (commit == null)
            {
                return HttpNotFound();
            }

            var diffs = GitUtilities.GetDiffInfo(resourceInfo.FullPath, commit.CommitHash);

            ViewBag.RepoName = resourceInfo.Name;
            ViewBag.CommitLogEntry = commit;

            return View(diffs);
        }

        public ActionResult ViewCommits(string repo, string branch, int page = 1)
        {
            var resourceInfo = this.FileManager.GetResourceInfo(repo);
            if (resourceInfo.Type != ResourceType.Directory || page < 1)
            {
                return HttpNotFound();
            }

            const int PageSize = 20;
            int skip = PageSize * (page - 1);
            var count = GitUtilities.CountCommits(resourceInfo.FullPath);

            if (skip >= count)
            {
                return HttpNotFound();
            }

            var repoInfo = new RepoInfo(repo, branch);
            AddRepoBreadCrumb(repoInfo);
            this.BreadCrumbs.Append("Browse", "ViewCommits", "Commit Log", new { repo });

            var commits = GitUtilities.GetLogEntries(resourceInfo.FullPath, PageSize, skip, branch);

            ViewBag.Page = page;
            ViewBag.PageCount = (count / PageSize) + (count % PageSize > 0 ? 1 : 0);
            ViewBag.RepoName = resourceInfo.Name;

            return View(commits);
        }

        public ActionResult ViewTree(string repo, string @object, string path)
        {
            var resourceInfo = this.FileManager.GetResourceInfo(repo);
            if (resourceInfo.Type != ResourceType.Directory)
            {
                return HttpNotFound();
            }

            TreeView items;
            try
            {
                items = GitUtilities.GetTreeInfo(resourceInfo.FullPath, @object, path);
            }
            catch (GitErrorException)
            {
                return HttpNotFound();
            }

            this.BreadCrumbs.Append("Browse", "ViewTree", @object, new { repo, @object, path = string.Empty });
            this.BreadCrumbs.Append("Browse", "ViewTree", BreadCrumbTrail.EnumeratePath(path), p => p.Key, p => new { repo, @object, path = p.Value });

            ViewBag.RepoName = resourceInfo.Name;
            ViewBag.Tree = @object;
            ViewBag.Path = path ?? string.Empty;

            return View(items);
        }

        public ActionResult ViewBlob(string repo, string @object, string path, bool raw = false)
        {
            var resourceInfo = this.FileManager.GetResourceInfo(repo);
            if (resourceInfo.Type != ResourceType.Directory || string.IsNullOrEmpty(path))
            {
                return HttpNotFound();
            }

            var fileName = Path.GetFileName(path);
            var containingPath = path.Substring(0, path.Length - fileName.Length);

            TreeView items;
            try
            {
                items = GitUtilities.GetTreeInfo(resourceInfo.FullPath, @object, containingPath);
            }
            catch (GitErrorException)
            {
                return HttpNotFound();
            }

            if (!items.Objects.Any(o => o.Name == fileName))
            {
                return HttpNotFound();
            }

            var contentType = MimeUtilities.GetMimeType(fileName);

            if (raw)
            {
                return new GitFileResult(resourceInfo.FullPath, @object, path, contentType);
            }

            this.BreadCrumbs.Append("Browse", "ViewTree", @object, new { repo, @object, path = string.Empty });
            var paths = BreadCrumbTrail.EnumeratePath(path, TrailingSlashBehavior.LeaveOffLastTrailingSlash).ToList();
            this.BreadCrumbs.Append("Browse", "ViewTree", paths.Take(paths.Count() - 1), p => p.Key, p => new { repo, @object, path = p.Value });
            this.BreadCrumbs.Append("Browse", "ViewBlob", paths.Last().Key, new { repo, @object, path = paths.Last().Value });

            ViewBag.RepoName = resourceInfo.Name;
            ViewBag.Tree = @object;
            ViewBag.Path = path;
            ViewBag.FileName = fileName;
            ViewBag.ContentType = contentType;
            string model = null;

            if (contentType.StartsWith("text/") || contentType == "application/xml" || Regex.IsMatch(contentType, @"^application/.*\+xml$"))
            {
                using (var blob = GitUtilities.GetBlob(resourceInfo.FullPath, @object, path))
                {
                    using (var reader = new StreamReader(blob, detectEncodingFromByteOrderMarks: true))
                    {
                        model = reader.ReadToEnd();
                    }
                }
            }

            return View((object)model);
        }

        public class RouteRegisterer : IRouteRegisterer
        {
            public void RegisterRoutes(RouteCollection routes)
            {
                routes.MapRoute(
                    "Browse Index",
                    "browse",
                    new { controller = "Browse", action = "Index" });

                routes.MapRoute(
                    "View Repo",
                    "browse/{repo}/{branch}",
                    new { controller = "Browse", action = "ViewRepo" });

                routes.MapRoute(
                    "View Tree",
                    "browse/{repo}/tree/{object}/{*path}",
                    new { controller = "Browse", action = "ViewTree", path = UrlParameter.Optional });

                routes.MapRoute(
                    "View Blob",
                    "browse/{repo}/blob/{object}/{*path}",
                    new { controller = "Browse", action = "ViewBlob", path = UrlParameter.Optional });

                routes.MapRoute(
                    "View Commit",
                    "browse/{repo}/commit/{object}",
                    new { controller = "Browse", action = "ViewCommit" });

                routes.MapRoute(
                    "View Commit Log",
                    "browse/{repo}/commits/{branch}",
                    new { controller = "Browse", action = "ViewCommits", routeName = "View Commit Log" });
            }
        }
    }
}
