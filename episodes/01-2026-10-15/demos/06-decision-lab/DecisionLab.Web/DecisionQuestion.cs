/*
 * Goal: Give both models identical public-safe classification tasks.
 * A. Define three labels with descriptions.
 * B. Offer a small predefined set, including an ambiguous message.
 * C. Keep task identity and model response separate from evaluation claims.
 */
namespace DecisionLab.Web;

public sealed record DecisionQuestion(string Title, string Message)
{
    public const string Instructions = "Which team should handle this support message?";
    public static readonly IReadOnlyDictionary<string, string?> Choices =
        new Dictionary<string, string?>
        {
            ["billing"] = "Invoices, duplicate charges, and subscription payments",
            ["technical_support"] = "Software errors and account-access problems",
            ["sales"] = "Product selection, pricing, and buying a plan"
        };
    public static readonly IReadOnlyList<DecisionQuestion> Examples =
    [
        new("Duplicate charge", "I was charged twice for my subscription."),
        new("Application crash", "The application crashes every time I open the settings screen."),
        new("Buying a plan", "Which plan should I buy for a team of ten developers?"),
        new("Ambiguous upgrade", "I paid for an upgrade but cannot access the new features.")
    ];
}

public sealed record ModelResult(
    string Status, string? Label, double ElapsedMs,
    long? InputTokens = null, long? OutputTokens = null, long? TotalTokens = null,
    string? Error = null, string? TraceId = null)
{
    public bool Succeeded => Status == "completed";
    public long? DisplayTokens => TotalTokens ??
        (InputTokens.HasValue && OutputTokens.HasValue ? checked(InputTokens.Value + OutputTokens.Value) : null);
    public bool HasCalculatedTotal => !TotalTokens.HasValue && InputTokens.HasValue && OutputTokens.HasValue;
}
