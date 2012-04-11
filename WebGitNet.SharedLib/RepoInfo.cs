namespace WebGitNet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class RepoInfo
    {
        public RepoInfo(string name, string branch)
        {
            Name = name;
            DisplayName = name.Replace(".git", "");
            Branch = branch;
        }

        public string Name { get; set; }

        public string DisplayName { get; set; }
        
        public string Branch { get; set; }

        public string Description { get; set; }

        public bool IsGitRepo { get; set; }
    }
}
