//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;

//namespace AF_SCADA_Comparison
//{
//    // ---- Existing models (tweaked to include WebId + Links.Attributes) ----
//    public class ChildElement
//    {
//        public string Name { get; set; } = "";
//        public string TemplateName { get; set; } = "";
//        public string WebId { get; set; } = "";
//        public ChildElementLinks Links { get; set; } = new();
//    }

//    public class ChildElementLinks
//    {
//        public string Attributes { get; set; } = "";
//    }

//    public class ChildElementsResponse
//    {
//        public List<ChildElement>? Items { get; set; }
//    }

//    // ---- Attribute models (for SCADA Well Status lookup) ----
//    class AttributeItem
//    {
//        public string Name { get; set; } = "";
//        public string WebId { get; set; } = "";
//        public AttributeLinks Links { get; set; } = new();
//    }

//    class AttributeLinks
//    {
//        public string Value { get; set; } = "";
//    }

//    class AttributesResponse
//    {
//        public List<AttributeItem>? Items { get; set; }
//    }

//    class TimedValue
//    {
//        public object? Value { get; set; }
//        public string? Timestamp { get; set; }
//        public string? UnitsAbbreviation { get; set; }
//        public bool? Good { get; set; }
//        public bool? Questionable { get; set; }
//        public bool? Substituted { get; set; }
//    }

//    // ---- Final row you asked for ----
//    public class WellRow
//    {
//        public string WellName { get; set; } = "";        // element name
//        public string WellType { get; set; } = "";        // element template
//        public string ScadaWellStatus { get; set; } = ""; // attribute value
//    }

//    public class PiChildElementsClient
//    {
//        private readonly HttpClient _http;
//        private readonly JsonSerializerOptions _jsonOptions =
//            new() { PropertyNameCaseInsensitive = true };

//        public PiChildElementsClient(string baseUrl, bool useDefaultCredentials = true)
//        {
//            var handler = new HttpClientHandler
//            {
//                UseDefaultCredentials = useDefaultCredentials,
//                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
//            };

//            _http = new HttpClient(handler)
//            {
//                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
//            };

//            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
//        }

//        /// <summary>
//        /// Returns child elements (Name, Template, WebId, Links.Attributes).
//        /// </summary>
//        public async Task<List<ChildElement>> GetChildElementsAsync(string parentWebId, CancellationToken ct = default)
//        {
//            // IMPORTANT: include WebId and Links.Attributes so we can query attributes for each child
//            string url = $"elements/{parentWebId}/elements?selectedFields=Items.Name;Items.TemplateName;Items.WebId;Items.Links.Attributes";
//            string fullUrl = _http.BaseAddress + url;
//            Console.WriteLine($"Requesting URL: {fullUrl}");

//            try
//            {
//                using var resp = await _http.GetAsync(url, ct);
//                if (!resp.IsSuccessStatusCode)
//                {
//                    Console.WriteLine($"Failed to get child elements. Status Code: {resp.StatusCode} - {resp.ReasonPhrase}");
//                    return new List<ChildElement>();
//                }

//                var json = await resp.Content.ReadAsStringAsync(ct);
//                var data = JsonSerializer.Deserialize<ChildElementsResponse>(json, _jsonOptions);
//                return data?.Items ?? new List<ChildElement>();
//            }
//            catch (HttpRequestException ex)
//            {
//                Console.WriteLine($"HttpRequestException: {ex.Message}");
//                Console.WriteLine(ex.InnerException != null ? $"Inner Exception: {ex.InnerException.Message}" : "No inner exception available.");
//                return new List<ChildElement>();
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
//                return new List<ChildElement>();
//            }
//        }

//        /// <summary>
//        /// Convenience method: returns (WellName, WellType, SCADA Well Status value) for each child element.
//        /// </summary>
//        public async Task<List<WellRow>> GetWellsWithScadaStatusAsync(string parentWebId, CancellationToken ct = default)
//        {
//            var children = await GetChildElementsAsync(parentWebId, ct);
//            var result = new List<WellRow>();

//            foreach (var child in children)
//            {
//                var statusValue = await TryGetScadaWellStatusAsync(child, ct);
//                result.Add(new WellRow
//                {
//                    WellName = child.Name,
//                    WellType = child.TemplateName,
//                    ScadaWellStatus = statusValue ?? ""  // empty if not found
//                });
//            }

//            return result;
//        }

//        /// <summary>
//        /// Fetches the "SCADA Well Status" attribute *current* value for a given element.
//        /// Strategy:
//        ///   1) GET /elements/{child.WebId}/attributes?name=SCADA%20Well%20Status&selectedFields=Items.WebId;Items.Links.Value
//        ///   2) If found, GET /attributes/{attrWebId}/value  (or follow Items.Links.Value)
//        /// </summary>
//        private async Task<string?> TryGetScadaWellStatusAsync(ChildElement child, CancellationToken ct)
//        {
//            try
//            {
//                var encodedName = Uri.EscapeDataString("SCADA Well Status");
//                var attrListUrl =
//                    $"elements/{child.WebId}/attributes?name={encodedName}&selectedFields=Items.WebId;Items.Name;Items.Links.Value";

//                using var listResp = await _http.GetAsync(attrListUrl, ct);
//                if (!listResp.IsSuccessStatusCode)
//                    return null;

//                var listJson = await listResp.Content.ReadAsStringAsync(ct);
//                var attrs = JsonSerializer.Deserialize<AttributesResponse>(listJson, _jsonOptions);
//                var attr = attrs?.Items?.FirstOrDefault();
//                if (attr == null)
//                    return null;

//                // Option A: follow link in attr.Links.Value
//                string valueUrl;
//                if (!string.IsNullOrWhiteSpace(attr.Links.Value))
//                {
//                    // `Links.Value` is usually an absolute URL; convert to relative if it points to same base.
//                    var valueUri = new Uri(attr.Links.Value, UriKind.RelativeOrAbsolute);
//                    valueUrl = valueUri.IsAbsoluteUri
//                        ? MakeRelativeToBase(valueUri)
//                        : attr.Links.Value.TrimStart('/');
//                }
//                else
//                {
//                    // Option B: direct by WebId
//                    valueUrl = $"attributes/{attr.WebId}/value";
//                }

//                using var valResp = await _http.GetAsync(valueUrl, ct);
//                if (!valResp.IsSuccessStatusCode)
//                    return null;

//                var valJson = await valResp.Content.ReadAsStringAsync(ct);
//                var tv = JsonSerializer.Deserialize<TimedValue>(valJson, _jsonOptions);

//                // Turn the object into a string safely
//                return tv?.Value?.ToString();
//            }
//            catch
//            {
//                return null;
//            }
//        }

//        /// <summary>
//        /// If PI Web API returned an absolute URL for Links.Value on the same server, convert it to a relative path we can call via our BaseAddress.
//        /// </summary>
//        private string MakeRelativeToBase(Uri absolute)
//        {
//            // If same host as BaseAddress, return relative path; else just use absolute.AbsoluteUri
//            var baseHost = _http.BaseAddress?.Host?.ToLowerInvariant();
//            if (baseHost != null && absolute.Host.ToLowerInvariant() == baseHost)
//            {
//                // Strip scheme/host, keep path and query
//                var pathAndQuery = absolute.PathAndQuery.TrimStart('/');
//                return pathAndQuery;
//            }
//            return absolute.AbsoluteUri;
//        }
//    }
//}
