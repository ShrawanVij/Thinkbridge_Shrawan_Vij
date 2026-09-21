namespace QuotesApi.Models;

public class Quote
{
    public int Id { get; set; }

    public string Author { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int UserId { get; set; }

    public DateTime CreatedAt { get; set; }
}