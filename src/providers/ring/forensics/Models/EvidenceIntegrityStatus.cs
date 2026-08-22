using System;
using System.Collections.Generic;

namespace VideoForensics.Providers.Ring.Forensics.Models
{
    public class EvidenceIntegrityStatus
    {
        public bool IsIntact { get; set; }
        public List<string> IntegrityIssues { get; set; } = new();
        public DateTime? LastVerified { get; set; }
    }
}
