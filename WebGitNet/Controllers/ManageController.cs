﻿//-----------------------------------------------------------------------
// <copyright file="ManageController.cs" company="(none)">
//  Copyright © 2011 John Gietzen. All rights reserved.
// </copyright>
// <author>John Gietzen</author>
//-----------------------------------------------------------------------

namespace WebGitNet.Controllers
{
    using System.IO;
    using System.Linq;
    using System.Web.Mvc;
    using WebGitNet.Models;
    using io = System.IO;
    using System.Web.Configuration;

    public class ManageController : SharedControllerBase
    {
        public ManageController()
        {
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(CreateRepoRequest request)
        {
            if (!bool.Parse(WebConfigurationManager.AppSettings["AllowCreateRepo"]))
                return View();

            var invalid = Path.GetInvalidFileNameChars();
            if (request.RepoName.Any(c => invalid.Contains(c)))
            {
                ModelState.AddModelError("RepoName", "Repository name must be a valid folder name.");
            }

            var resourceInfo = this.FileManager.GetResourceInfo(request.RepoName);
            if (resourceInfo.FileSystemInfo == null)
            {
                ModelState.AddModelError("RepoName", "You do not have permission to create this repository.");
            }

            if (resourceInfo.Type != ResourceType.NotFound)
            {
                ModelState.AddModelError("RepoName", "There is already an object at that location.");
            }

            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var repoPath = resourceInfo.FullPath;

            try
            {
                GitUtilities.CreateRepo(repoPath);
            }
            catch (GitErrorException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(request);
            }

            io::File.WriteAllText(Path.Combine(repoPath, "description"), request.Description);

            GitUtilities.ExecutePostCreateHook(repoPath);

            return RedirectToAction("ViewRepo", "Browse", new { repo = request.RepoName });
        }
    }
}
