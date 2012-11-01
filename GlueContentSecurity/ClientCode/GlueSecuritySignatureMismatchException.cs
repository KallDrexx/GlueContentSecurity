using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlueContentSecurity.ClientCode
{
    public class GlueSecuritySignatureMismatchException : Exception
    {
        public GlueSecuritySignatureMismatchException(IEnumerable<ClientVerificationResult> results, bool signatureMismatch, bool hashFileNotFound)
            : base("Content did not pass verification checks")
        {
            Results = results;
            SignatureMismatch = signatureMismatch;
            HashFileNotFound = hashFileNotFound;
        }

        public IEnumerable<ClientVerificationResult> Results { get; protected set; }
        public bool SignatureMismatch { get; protected set; }
        public bool HashFileNotFound { get; protected set; }
    }
}
