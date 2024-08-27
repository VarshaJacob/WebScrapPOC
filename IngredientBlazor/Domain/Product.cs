namespace IngredientBlazor.Domain
{
    public class Product
    {
        public string ProductName { get; set; }
        public List<string> BingUrls { get; set; }
        public List<ScrapedResult> ScrapedResults { get; set; }
        public string Answer { get; set; }
    }
}
