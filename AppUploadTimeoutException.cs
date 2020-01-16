using System;
using System.Net.Http;
using System.Runtime.Serialization;

namespace qlik_qv_export
{
    [Serializable]
    public class AppUploadTimeoutException : WorkflowException
    {
        public AppUploadTimeoutException()
        {
        }

        public AppUploadTimeoutException(string message)
           : base(message)
        {
        }

        public AppUploadTimeoutException(string message, HttpRequestException inner)
           : base(message, inner)
        {
        }

        protected AppUploadTimeoutException(SerializationInfo info, StreamingContext context)
           : base(info, context)
        {
        }
    }
}