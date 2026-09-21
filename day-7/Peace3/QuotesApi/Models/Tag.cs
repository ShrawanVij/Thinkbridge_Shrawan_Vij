namespace QuotesApi.Models;

public class Tag
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<Quote> Quotes { get; set; } = new();
}
