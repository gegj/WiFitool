using System.Collections.Generic;

namespace WiFitool.Models
{
    internal sealed class WorkspaceSession
    {
        public string RootPath { get; set; }
        public string OriginalImagePath { get; set; }
        public Dictionary<string, string> PartitionFiles { get; private set; }
        public Dictionary<string, string> ExtractedDirectories { get; private set; }
        public Dictionary<string, string> RepackedFiles { get; private set; }
        public Dictionary<string, string> MetadataFiles { get; private set; }

        public WorkspaceSession()
        {
            PartitionFiles = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            ExtractedDirectories = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            RepackedFiles = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            MetadataFiles = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        }
    }
}
