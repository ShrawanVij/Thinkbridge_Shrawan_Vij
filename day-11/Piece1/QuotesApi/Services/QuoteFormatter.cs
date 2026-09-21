namespace QuotesApi.Services;

public sealed class QuoteFormatter : IQuoteFormatter
{
    public string Format(string text)
    {
        return text.Trim();
    }
}