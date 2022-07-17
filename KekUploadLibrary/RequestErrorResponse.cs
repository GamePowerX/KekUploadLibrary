using Newtonsoft.Json;

namespace KekUploadLibrary;

public class RequestErrorResponse
{
    public string? Generic { get; set; }
    public string? Error { get; set; }
    public string? Field { get; set; }

    public static RequestErrorResponse? ParseErrorResponse(HttpResponseMessage? response)
    {
        if (response == null) return null;
        var responseString = response.Content.ReadAsStringAsync().Result;
        var responseObject =
            new JsonSerializer().Deserialize<RequestErrorResponse>(
                new JsonTextReader(new StringReader(responseString)));
        return responseObject;
    }

    public override string ToString()
    {
        return $"RequestErrorResponse: {Generic} \n {Error} \n {Field}";
    }
}