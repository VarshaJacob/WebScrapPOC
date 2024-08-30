#pragma warning disable OPENAI001

using Azure;
using Azure.AI.OpenAI;
using IngredientBlazor.Data.Options;
using IngredientBlazor.Domain;
using OpenAI.Assistants;
using OpenAI.Files;
using OpenAI.VectorStores;

namespace IngredientBlazor.Data
{
    public class OpenAiService
    {
        private OpenAiOptions openAiOptions;
        private AzureOpenAIClient openAIClient;
        private FileClient fileClient;
        private VectorStoreClient vectorStoreClient;
        private AssistantClient assistantClient;
        private AssistantCreationOptions assistantCreationOptions = new AssistantCreationOptions()
        {
            Name = "IngredientAssistant",
            Tools = { new FileSearchToolDefinition() },
            Temperature = 0.5f,
            Instructions = "You will be provided with data files containing content about specific product. Your task is to extract and list the ingredients of the specified product from these data files. If ingredients are not available, start answer with Failed"
        };
        private Assistant assistant;
        public OpenAiService(OpenAiOptions openAiOptions)
        {
            this.openAiOptions = openAiOptions;
            openAIClient = new AzureOpenAIClient(new Uri(openAiOptions.Endpoint), new AzureKeyCredential(openAiOptions.ApiKey));
            fileClient = openAIClient.GetFileClient();
            vectorStoreClient = openAIClient.GetVectorStoreClient();
            assistantClient = openAIClient.GetAssistantClient();
            assistant = assistantClient.CreateAssistant(openAiOptions.DeploymentName, assistantCreationOptions);
        }

        public async Task<OpenAIResult> GetIngredients(List<ScrapedResult> scrapedResults, string product)
        {

            var fileIds = await UploadFilesAsync(scrapedResults);

            var (vectorStoreId, filesAdded) = await AddFilesToVectorStore(fileIds, product);

            var answer = await AskAssistanceApi(vectorStoreId, product);
            answer.FilesAddedToVectorStore = filesAdded;

            return answer;
        }

        public async Task<List<string>> UploadFilesAsync(List<ScrapedResult> scrapedResults)
        {
            var fileIds = new List<string>();
            foreach (var scrapedResult in scrapedResults.Where(x => x.Status != ScrapedStatusEnum.Failed))
            {
                try
                {
                    //max 20 files, 512 Mb, 50,00,000 tokens
                    var chunks = GetChunks(scrapedResult.Content, 1000000);

                    foreach (var chunk in chunks)
                    {
                        var stream = GenerateStreamFromString(chunk);


                        var temp1 = stream.Length;
                        var fileName = Guid.NewGuid();

                        var fileId = await fileClient.UploadFileAsync(stream, $"{fileName}.docx", FileUploadPurpose.Assistants);
                        fileIds.Add(fileId.Value.Id);
                    }


                }
                catch (Exception ex)
                {
                    Console.Write($"Failed to upload content of {scrapedResult.Url} due to {ex.Message}" + "\n");
                }
            }

            return fileIds;
        }

        public static Stream GenerateStreamFromString(string stringValue)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(stringValue);
            var memoryStream = new MemoryStream(bytes);
            return memoryStream;
        }

        public static IEnumerable<string> GetChunks(string content, int chunkSize)
        {
            return content.Chunk(chunkSize).Select(s => new string(s)).ToList();
        }

        public async Task DeleteVectorStore()
        {
            try
            {
                var vectorStoreClient = openAIClient.GetVectorStoreClient();
                var fileClient = openAIClient.GetFileClient();

                await foreach (var vectorStore in vectorStoreClient.GetVectorStoresAsync())
                {
                    Console.WriteLine(vectorStore.Name);
                    await vectorStoreClient.DeleteVectorStoreAsync(vectorStore.Id);
                }

                var files = await fileClient.GetFilesAsync(OpenAIFilePurpose.Assistants);
                foreach (var file in files.Value.ToList())
                {
                    await fileClient.DeleteFileAsync(file.Id);
                }
            }
            catch (Exception ex)
            {

            }


        }

        public async Task GetFileDetails(string vectorStoreId)
        {
            var vectorStoreClient = openAIClient.GetVectorStoreClient();
            var vectorStore = await vectorStoreClient.GetVectorStoreAsync(vectorStoreId);

            await foreach (var file in vectorStoreClient.GetFileAssociationsAsync(vectorStore))
            {
                var error = file.LastError.Value.Message;

            }
        }

        public async Task<(string,string)> AddFilesToVectorStore(List<string> fileIds, string vectorName)
        {
            var vectorStore = await vectorStoreClient.CreateVectorStoreAsync(new VectorStoreCreationOptions { Name = vectorName });

            var uploadbatch = await vectorStoreClient.CreateBatchFileJobAsync(vectorStore.Value.Id, fileIds);

            // To avoid exceeding rate limits per minuete of OpenAI, pause implementation can be added.
            // Thread.Sleep(5000);

            // TO DO: handle failed cases
            while (uploadbatch.Value.Status == VectorStoreBatchFileJobStatus.InProgress)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                uploadbatch = await vectorStoreClient.GetBatchFileJobAsync(uploadbatch);
            }
            

            // NOTE: All files may not be added to vector store.
            // GetBatchFileJobAsync method does not give file details.
            // GetFileDetails method above can be used to get file details.
            // Can implement a retry mechanism to add failed files to vector store. (does increase time)

            Console.WriteLine($"Add file to vector store Progress: {uploadbatch.Value.FileCounts.Completed}/{fileIds.Count}");

            return (vectorStore.Value.Id, $"{uploadbatch.Value.FileCounts.Completed}/{fileIds.Count}");
        }

        public async Task<OpenAIResult> AskAssistanceApi(string vectorStoreId, string product)
        {
            // NOTE: One assistant is created and vector store is added to the thread.
            // Creating a new assistant for each product exceeds rate limit. Can implement grouping if needed.

            var threadCreationOptions = new ThreadCreationOptions
            {
                ToolResources = new ToolResources
                {
                    FileSearch = new FileSearchToolResources
                    {
                        VectorStoreIds = { vectorStoreId }
                    }
                }
            };

            var thread = await assistantClient.CreateThreadAsync(threadCreationOptions);
            var result = await assistantClient.CreateMessageAsync(thread.Value, [$"What are the ingredients of a {product}?"]);

            // Retry implementation for getting response from assistant
            var answer = string.Empty;
            var maxRetry = 3;
            var tries = 0;

            while ((string.IsNullOrEmpty(answer)) && tries < maxRetry)
            {
                tries++;
                var run = await assistantClient.CreateRunAsync(thread.Value, assistant);
                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    run = await assistantClient.GetRunAsync(thread.Value.Id, run.Value.Id);
                }
                while (run.Value.Status == RunStatus.Queued || run.Value.Status == RunStatus.InProgress);


                var afterRunMessages = assistantClient.GetMessagesAsync(thread.Value);
                await foreach (var message in afterRunMessages)
                {
                    if (message.Role != MessageRole.Assistant) continue;
                    answer = message.Content.First().ToString();

                    break;
                }
            }

            if (string.IsNullOrEmpty(answer) || answer.Contains("Failed"))
            {
                return new OpenAIResult { Answer = answer, Status = AnswerStatusEnum.Failed };
            }
            else
            {
                return new OpenAIResult { Answer = answer, Status = AnswerStatusEnum.Success };
            }

        }
    }
}
