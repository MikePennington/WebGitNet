namespace WebGitNet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class RepoInfo
    {
        public string Name { get; set; }

        private string _displayName;
        public string DisplayName {
            get
            {
                return _displayName == null ? Name : _displayName;
            }
            set
            {
                _displayName = value;
            }
        }

        public string Description { get; set; }

        public bool IsGitRepo { get; set; }
    }
}
