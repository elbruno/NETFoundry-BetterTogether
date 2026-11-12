// Goal: Expose two fixed RSS feeds as read-only MCP tools.
// A: Fetch without redirects. B: Parse recent posts. C: Return links and excerpts.
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ModelContextProtocol.Server;

[McpServerToolType]
public sealed class BlogTools
{
    private const string PersonalBlogFeed = "https://brunocapuano.wordpress.com/feed/";
    private const string DotNetBlogFeed = "https://devblogs.microsoft.com/dotnet/feed/";
    private const int PostsToRead = 3;
    private const int MaxExcerptCharacters = 1200;

    public const string TelemetrySource = "BetterTogether.ApplicationGuardrails";
    private static readonly ActivitySource Activities = new(TelemetrySource);
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("DotNetConfBlogDemo", "1.0") } }
    };

    [McpServerTool(Name = nameof(GetLatestPersonalBlogPosts))]
    [Description("Read the three newest posts from Bruno Capuano's fixed personal blog feed.")]
    public static Task<string> GetLatestPersonalBlogPosts() =>
        ReadLatestPostsAsync("Bruno Capuano's personal blog", PersonalBlogFeed);

    [McpServerTool(Name = nameof(GetLatestDotNetBlogPosts))]
    [Description("Read the three newest posts from the fixed .NET Blog feed.")]
    public static Task<string> GetLatestDotNetBlogPosts() =>
        ReadLatestPostsAsync("the .NET Blog", DotNetBlogFeed);

    private static async Task<string> ReadLatestPostsAsync(string sourceName, string feedUrl)
    {
        using var activity = Activities.StartActivity("tool.ReadBlogFeed");
        activity?.SetTag("tool.source", sourceName);
        Console.Error.WriteLine($"Blog tool started: {sourceName}");
        using var response = await Client.GetAsync(feedUrl);
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Blog tool HTTP error: {sourceName}; {(int)response.StatusCode}");
            return $"ERROR: The request to {sourceName} returned HTTP " +
                $"{(int)response.StatusCode} ({response.ReasonPhrase}). No posts were retrieved.";
        }

        var feed = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var posts = feed.Descendants()
            .Where(element => element.Name.LocalName == "item")
            .Take(PostsToRead)
            .Select(ParsePost)
            .ToArray();
        if (posts.Length == 0)
            throw new InvalidDataException($"The RSS feed for {sourceName} contained no posts.");

        var output = new List<string> { $"Latest posts from {sourceName}:" };
        for (var index = 0; index < posts.Length; index++)
        {
            var post = posts[index];
            output.Add(
                $"{index + 1}. {post.Title}\nPublished: {post.Published}\n" +
                $"Link: {post.Link}\nExcerpt: {post.Excerpt}");
        }
        Console.Error.WriteLine($"Blog tool returned {posts.Length} posts: {sourceName}");
        return string.Join(Environment.NewLine + Environment.NewLine, output);
    }

    private static BlogPost ParsePost(XElement item)
    {
        var title = GetChildValue(item, "title");
        var link = GetChildValue(item, "link");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
            throw new InvalidDataException("An RSS post was missing its title or link.");
        var content = GetChildValue(item, "encoded");
        if (string.IsNullOrWhiteSpace(content))
            content = GetChildValue(item, "description");
        var excerpt = ToPlainText(content);
        if (excerpt.Length > MaxExcerptCharacters)
            excerpt = excerpt[..MaxExcerptCharacters] + "...";
        return new BlogPost(title, GetChildValue(item, "pubDate"), link, excerpt);
    }

    private static string GetChildValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?
            .Value.Trim() ?? string.Empty;

    private static string ToPlainText(string html)
    {
        var withoutMarkup = Regex.Replace(html, "<[^>]*>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(withoutMarkup), @"\s+", " ").Trim();
    }

    private sealed record BlogPost(string Title, string Published, string Link, string Excerpt);
}
