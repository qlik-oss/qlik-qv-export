using System.Net;
using System.Net.Http;

namespace qlik_qv_export
{
    public static class WorkflowExceptionStrategy
    {
        public static void ThrowException(HttpResponseMessage response)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (response.StatusCode)
            {
                case (HttpStatusCode)429:
                case HttpStatusCode.RequestTimeout:
                case HttpStatusCode.InternalServerError:
                case HttpStatusCode.BadGateway:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.GatewayTimeout:
                    throw new WorkflowException("Workflow exception was thrown due to unsuccessful status code.");
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }
        }
    }
}