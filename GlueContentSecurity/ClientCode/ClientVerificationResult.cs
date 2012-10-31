using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlueContentSecurity.ClientCode
{
    public enum ClientVerificationResultType { ValidMath, IncorrectHash, FileMissing };

    public class ClientVerificationResult
    {
        public string FilePath { get; set; }
        public ClientVerificationResultType ResultType { get; set; }
    }
}
