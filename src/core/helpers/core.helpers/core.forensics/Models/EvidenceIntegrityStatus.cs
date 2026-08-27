using System;
using System.Collections.Generic;

namespace VideoForensics.Forensics.Models
{
    public class EvidenceIntegrityStatus
    {
        public bool IsIntact { get; set; }
        public List<string> IntegrityIssues { get; set; } = new();
        public DateTime? LastVerified { get; set; }
    }
}
