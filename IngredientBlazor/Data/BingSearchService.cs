using IngredientBlazor.Data.Options;
using System.Text.Json;

namespace IngredientBlazor.Data
{
    public class BingSearchService
    {
        private BingSearchOptions searchOptions;
        private HttpClient httpClient;

        public BingSearchService(BingSearchOptions searchOptions)
        {
            this.searchOptions = searchOptions;
            httpClient = new HttpClient() { DefaultRequestHeaders = { { "Ocp-Apim-Subscription-Key", searchOptions.ApiKey } } };
        }
        public async Task<List<string>> GetBingSearchUrlsAsync(string product)
        {
            //using azure bing resource
            //REST
            //c# sdk available as well

            var temp = product;
            var t = Uri.EscapeDataString(temp);
            var response = await httpClient.GetAsync($"{searchOptions.Endpoint}?q={Uri.EscapeDataString(temp)}&mkt=en-gb");
            var searchResponse = await response.Content.ReadAsStringAsync();

            JsonDocument doc = JsonDocument.Parse(searchResponse);
            var webPages = doc.RootElement.GetProperty("webPages").GetProperty("value");

            List<string> urlResults = new();
            if (webPages.ValueKind == JsonValueKind.Array)
            {
                foreach (var page in webPages.EnumerateArray())
                {
                    var urlResult = page.GetProperty("url").GetString();
                    urlResults.Add(urlResult);
                }
            }

            var allowedSites = new List<string> { "tesco"};
            var filteredResults = urlResults.Where(x => allowedSites.Any(site => x.Contains(site))).ToList();

            if (filteredResults.Count < 3)
            {
                filteredResults.AddRange(urlResults.Except(filteredResults).Take(3 - filteredResults.Count));
            }

            return filteredResults.Take(3).ToList();

        }
    }
}
