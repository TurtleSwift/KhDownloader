using HtmlAgilityPack;
using System.Diagnostics.CodeAnalysis;

namespace KhDownloader;

internal class KhClient
{
    public const string BaseUrl = "https://downloads.khinsider.com";
    public const string AlbumBaseUrl = $"{BaseUrl}/game-soundtracks/album/";

    private readonly HttpClient _httpClient;

    public KhClient()
    {
        _httpClient = new HttpClient() { BaseAddress = new Uri(BaseUrl) };
    }

    public async Task<Result<KhAlbum>> GetAlbumInfoAsync(Uri khAlbumUrl)
    {
        var response = await _httpClient.GetAsync(khAlbumUrl);

        if (!response.IsSuccessStatusCode)
            return Result<KhAlbum>.Problem($"Server responded with {response.StatusCode}.");

        var doc = new HtmlDocument();
        doc.LoadHtml(await response.Content.ReadAsStringAsync());

        var h2Node = doc.DocumentNode.SelectSingleNode("//h2");
        var albumTitle = h2Node.InnerText;

        var album = new KhAlbum
        {
            Title = albumTitle
        };

        var rows = doc.DocumentNode.SelectNodes("//table[@id='songlist']/tr");
        if (rows is null || rows.Count == 0)
            return Result<KhAlbum>.Problem($"No tracks in album!");

        var headers = doc.DocumentNode.SelectNodes("//tr[@id='songlist_header']/th");
        var hasFlac = headers.Any(h => h.InnerText.Contains("FLAC", StringComparison.OrdinalIgnoreCase));

        foreach (var row in rows)
        {
            var links = row.SelectNodes(".//a[starts-with(@href, '/game-soundtracks/album/') and substring(@href, string-length(@href) - 3) = '.mp3']");
            if (links is not null)
            {
                var url = links[0].GetAttributeValue("href", "");
                var title = links[0].InnerText;
                var length = links[1].InnerText;
                var trackNumberText = row.SelectSingleNode(".//td[@align='right']").InnerText.Trim('.');

                var sizes = new Dictionary<KhTrackFormat, long>
                { { KhTrackFormat.Mp3, Utils.ToBytes(links[2].InnerText) } }; // MP3 size
                if (hasFlac) 
                    sizes.Add(KhTrackFormat.Flac, Utils.ToBytes(links[3].InnerText)); // FLAC size

                _ = int.TryParse(trackNumberText, out var trackNumber);
                album.Tracks.Add(new (url, title, sizes, length, trackNumber));
            }
        }

        return Result<KhAlbum>.Success(album);
    }

    public async Task<Result<KhTrackContent>> GetTrackStreamAsync(KhTrack track, KhTrackFormat format)
    {
        var trackDetailResponse = await _httpClient.GetAsync(track.Url);
        if (!trackDetailResponse.IsSuccessStatusCode)
            return Result<KhTrackContent>.Problem($"server responded with {trackDetailResponse.StatusCode}.");

        var trackDoc = new HtmlDocument();
        trackDoc.LoadHtml(await trackDetailResponse.Content.ReadAsStringAsync());

        string? audioFileUrl = null;
        switch (format)
        {
            case KhTrackFormat.Mp3: audioFileUrl = trackDoc.DocumentNode.SelectSingleNode("//a[substring(@href, string-length(@href) - 3) = '.mp3']")?.GetAttributeValue("href", ""); break;
            case KhTrackFormat.Flac: audioFileUrl = trackDoc.DocumentNode.SelectSingleNode("//a[substring(@href, string-length(@href) - 4) = '.flac']")?.GetAttributeValue("href", ""); break;
        }

        if (string.IsNullOrEmpty(audioFileUrl))
            return Result<KhTrackContent>.Problem($"Audio file not found!");

        var trackResponse = await _httpClient.GetAsync(audioFileUrl, HttpCompletionOption.ResponseHeadersRead);
        if (!trackResponse.IsSuccessStatusCode)
            return Result<KhTrackContent>.Problem($"server responded with {trackResponse.StatusCode}.");
                
        return Result<KhTrackContent>.Success(new (
            ContentStream: await trackResponse.Content.ReadAsStreamAsync(),
            ContentSize: trackResponse.Content.Headers.ContentLength ?? 0,
            FileName: Path.GetFileName(Uri.UnescapeDataString(audioFileUrl))
            ));
    }

    public static bool TryCreateAlbumUri(string? albumUrlPart, out Uri? albumUri)
    {
        albumUri = null;
        var cleanedEntry = albumUrlPart?.Trim('/');
        var cleanedBase = AlbumBaseUrl.Trim('/');

        if (string.IsNullOrWhiteSpace(cleanedEntry) ||
            !cleanedEntry.StartsWith(cleanedBase, StringComparison.OrdinalIgnoreCase) ||
            cleanedEntry.Equals(cleanedBase, StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(cleanedEntry, UriKind.Absolute, out var uri) ||
             uri.Scheme != Uri.UriSchemeHttps)
            return false;

        albumUri = uri;
        return true;
    }

    internal record KhTrack(string Url, string Title, IReadOnlyDictionary<KhTrackFormat, long> Sizes, string Length, int Number)
    {
        public long GetSize(KhTrackFormat format) => Sizes.TryGetValue(format, out var size) ? size : 0;
    }

    internal record KhAlbum
    {
        public required string Title { get; init; }
        public List<KhTrack> Tracks { get; set; } = [];
        public int TrackCount => Tracks.Count;
        public bool HasFlac => Tracks.Any(t => t.Sizes.ContainsKey(KhTrackFormat.Flac));
        public long GetTotalSize(KhTrackFormat format) => Tracks.Sum(t => t.GetSize(format));
    }

    internal enum KhTrackFormat
    {
        Mp3,
        Flac
    }

    internal record KhTrackContent(Stream ContentStream, long ContentSize, string FileName);

    internal record Result<T>
    {
        public static Result<T> Success(T value) => new() { ResultValue = value };
        public static Result<T> Problem(string details) => new() { Error = details, IsSuccessful = false };

        private Result() { }

        public T? ResultValue { get; init; }
        public string? Error { get; init; }

        [MemberNotNullWhen(true, nameof(ResultValue))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccessful { get; private set; } = true;
    }

}