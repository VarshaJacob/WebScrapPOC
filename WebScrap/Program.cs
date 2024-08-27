// See https://aka.ms/new-console-template for more information
#pragma warning disable OPENAI001
using Azure;
using Azure.AI.OpenAI;
using HtmlAgilityPack;
using OpenAI.Assistants;
using OpenAI.Files;
using System.Text.Json;
using OpenAI.VectorStores;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Give the product name");
        string userInput = Console.ReadLine();
        Console.WriteLine($"Processing ingredient of {userInput}");

        //Enter bing search credentials
        string bingEndpoint = "";
        string bingApiKey = "";
        var httpClient = new HttpClient() { DefaultRequestHeaders = { { "Ocp-Apim-Subscription-Key", bingApiKey } } };

        var response = await httpClient.GetAsync($"{bingEndpoint}?q={Uri.EscapeDataString(userInput)}");
        var searchResponse = await response.Content.ReadAsStringAsync();

        JsonDocument doc = JsonDocument.Parse(searchResponse);
        var webPages = doc.RootElement.GetProperty("webPages").GetProperty("value");

        List<string> urlResults = [];
        if (webPages.ValueKind == JsonValueKind.Array)
        {
            foreach (var page in webPages.EnumerateArray())
            {
                var urlResult = page.GetProperty("url").GetString();
                urlResults.Add(urlResult);
                Console.Write(urlResult + "\n");
            }
        }

        //web scraping
        var scrapedResults = new List<(string url, string content)>();
        foreach (var url in urlResults)
        {
            try
            {
                var web = new HtmlWeb();
                var scrapedDocument = await web.LoadFromWebAsync(url);
                scrapedResults.Add((url, scrapedDocument?.DocumentNode.InnerText.Trim().Replace(" +", "").Replace("\n", "") ?? string.Empty));
                Console.Write($"Content scraping from {url}: Success" + "\n");
            }
            catch (Exception ex) 
            { 
                scrapedResults.Add((url, $"Failed: {ex.Message}"));
                Console.Write($"Content scraping from {url}: Failed" + "\n");
            }
        }

        //Enter open ai credentials
        var openAIResource = "";
        var openAiKey = "";
        var deploymentName = "";

        var openAIClient = new AzureOpenAIClient(new Uri(openAIResource), new AzureKeyCredential(openAiKey));
        var fileClient = openAIClient.GetFileClient();
        var vectorStoreClient = openAIClient.GetVectorStoreClient();
        var assistantClient = openAIClient.GetAssistantClient();
        var vectorStore = await vectorStoreClient.CreateVectorStoreAsync(new VectorStoreCreationOptions { Name = userInput });

        foreach (var scrapedResult in scrapedResults.Where(x => !x.content.StartsWith("Failed:") ||
        string.IsNullOrWhiteSpace(x.content)))
        {
            Console.Write($"Uploading scraped content of {scrapedResult.url}" + "\n");
            var stream = GenerateStreamFromString(scrapedResult.content);

            var fileId = await fileClient.UploadFileAsync(stream, $"{scrapedResult.url.TrimEnd('/')}.txt", FileUploadPurpose.Assistants);
            await vectorStoreClient.AddFileToVectorStoreAsync(vectorStore.Value.Id,fileId.Value.Id);
            
        }

        var assistantOptions = new AssistantCreationOptions()
        {
            Name = "IngredientAssistant",
            Tools =
            {
                new FileSearchToolDefinition()
            },
            ToolResources = new()
            {
                FileSearch = new FileSearchToolResources()
                {
                    VectorStoreIds = { vectorStore.Value.Id }
                }
            },
            Temperature = 0.5f,
            Instructions = "Given content about a food item, provide a distinct list of ingredients of the specified food item.",
        };

        var assistant = await assistantClient.CreateAssistantAsync(deploymentName, assistantOptions);
        var thread = await assistantClient.CreateThreadAsync();
        var result = await assistantClient.CreateMessageAsync(thread.Value, [$"What are the ingredients of a {userInput}?"]);

        var run = await assistantClient.CreateRunAsync(thread.Value, assistant.Value);
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            run = await assistantClient.GetRunAsync(thread.Value.Id, run.Value.Id);
        }
        while (run.Value.Status == RunStatus.Queued|| run.Value.Status == RunStatus.InProgress);

        var afterRunMessages = assistantClient.GetMessagesAsync(thread.Value);
        await foreach (var message in afterRunMessages)
        {
            if (message.Role != MessageRole.Assistant) continue;
            Console.WriteLine(message.Content.First());
            break;
        }


    }
    public static Stream GenerateStreamFromString(string stringValue)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(stringValue);
        var memoryStream = new MemoryStream(bytes);
        return memoryStream;
    }
}

