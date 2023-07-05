using System.IO;
using System.Net.Http;
using Newtonsoft.Json;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class is used to parse/deserialize the error response of a request.
    /// </summary>
    public class RequestErrorResponse
    {
        /// <summary>
        /// The generic error message. Example: "<c>NOT_FOUND</c>"
        /// </summary>
        public string? Generic { get; set; }
        
        /// <summary>
        /// The field that caused the error. Example: "<c>ID</c>"
        /// </summary>
        public string? Field { get; set; }
        
        /// <summary>
        /// The error message. It is more specific and readable than the <see cref="Generic"/> error message. Example: "<c>File with id not found</c>"
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// This parses the error response of a request.
        /// </summary>
        /// <param name="response">The response of the request.</param>
        /// <returns>The <see cref="RequestErrorResponse"/> of the request if it was successfully deserialized, otherwise null.</returns>
        public static RequestErrorResponse? ParseErrorResponse(HttpResponseMessage? response)
        {
            if (response == null) return null;
            var responseString = response.Content.ReadAsStringAsync().Result;
            var responseObject =
                new JsonSerializer().Deserialize<RequestErrorResponse>(
                    new JsonTextReader(new StringReader(responseString)));
            return responseObject;
        }

        /// <summary>
        /// This returns a human readable string of the <see cref="RequestErrorResponse"/>.
        /// Example: "<c>RequestErrorResponse: NOT_FOUND ID File with id not found</c>"
        /// </summary>
        /// <returns>A human readable string of the <see cref="RequestErrorResponse"/>.</returns>
        public override string ToString()
        {
            return $"RequestErrorResponse: {Generic} \n{Field} \n{Error}";
        }
    }
}