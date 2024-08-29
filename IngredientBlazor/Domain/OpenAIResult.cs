namespace IngredientBlazor.Domain
{
    public class OpenAIResult
    {
        public string Answer { get; set; }
        public AnswerStatusEnum Status { get; set; }

        // For Debug purposes
        public string FilesAddedToVectorStore { get; set; }
    }

    public enum AnswerStatusEnum
    {
        Success,
        Failed
    }
}
