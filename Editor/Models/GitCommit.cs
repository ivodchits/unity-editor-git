using System.Collections.Generic;

namespace GitEditor
{
    public class GitCommit
    {
        public string Hash;
        public string ShortHash;
        public string Author;
        public string Date;
        public string Message;
        public List<string> Refs = new List<string>();
    }
}
