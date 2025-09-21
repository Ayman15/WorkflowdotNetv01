using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace AF_SCADA_Comparison
{
    public class ChildElement
    {
        public string Name { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string WebId { get; set; } = "";
        public ChildElementLinks Links { get; set; } = new();
    }

    public class ChildElementLinks
    {
        public string Attributes { get; set; } = "";
    }

    public class ChildElementsResponse
    {
        public List<ChildElement>? Items { get; set; }
    }

    public class PiChildElementsClient
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions =
            new() { PropertyNameCaseInsensitive = true };

        public PiChildElementsClient(string baseUrl, bool useDefaultCredentials = true)
        {
            // Set up HttpClient with Kerberos Authentication (Windows default credentials)
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = useDefaultCredentials, // Use Kerberos via default credentials
                // By pass SSL certificate validation for local development
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") // Ensure the base URL ends with a slash
            };

            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<ChildElement>> GetChildElementsAsync(string parentWebId, CancellationToken ct = default)
        {
            // Construct the URL to query child elements
            string url = $"elements/{parentWebId}/elements?selectedFields=items.Name;items.TemplateName";

            // Log the full URL for debugging purposes
            string fullUrl = _http.BaseAddress + url;
            Console.WriteLine($"Requesting URL: {fullUrl}");

            try
            {
                // Make the HTTP request to PI Web API
                using var resp = await _http.GetAsync(url, ct);

                // Ensure we received a successful response (status code 2xx)
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to get child elements. Status Code: {resp.StatusCode} - {resp.ReasonPhrase}");
                    return new List<ChildElement>();
                }

                // Read the response and deserialize it into a list of ChildElement objects
                var json = await resp.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<ChildElementsResponse>(json, _jsonOptions);

                // Return the list of child elements or an empty list
                return data?.Items ?? new List<ChildElement>();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HttpRequestException: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                else
                {
                    Console.WriteLine("No inner exception available.");
                }
                return new List<ChildElement>();  // Return an empty list in case of an error
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                return new List<ChildElement>();  // Return an empty list for unexpected errors
            }
        }
    }
}
