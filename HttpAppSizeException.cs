using System.Net.Http;

namespace qlik_qv_export
{
    public class HttpAppSizeException : HttpRequestException
    {
        public HttpAppSizeException(string message)
            : base(message)
        {
        }

        public HttpAppSizeException(string message, HttpRequestException inner)
           : base(message, inner)
        {
        }
    }
}