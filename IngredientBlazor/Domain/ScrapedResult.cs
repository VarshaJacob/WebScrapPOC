namespace IngredientBlazor.Domain
{
    public class ScrapedResult
    {
        public string Url { get; set; }
        public string Content { get; set; }
        public ScrapedStatusEnum Status { get; set; }
    }

    public enum ScrapedStatusEnum
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }
}
