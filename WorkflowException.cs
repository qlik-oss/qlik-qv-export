using System;
using System.Runtime.Serialization;

namespace qlik_qv_export
{
    [Serializable]
    public class WorkflowException : Exception
    {
        public WorkflowException()
        {
        }

        public WorkflowException(string message)
           : base(message)
        {
        }

        public WorkflowException(string message, Exception inner)
           : base(message, inner)
        {
        }

        protected WorkflowException(SerializationInfo info, StreamingContext context)
           : base(info, context)
        {
        }
    }
}