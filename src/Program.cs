using KhDownloader;
using Spectre.Console;
using Spectre.Console.Extensions;
using System.Diagnostics;
using TextCopy;
using static KhDownloader.KhClient;

// Style settings
var accentColor = Color.Teal;
var errorColor = Color.Maroon;
var disabledColor = Color.Grey;
var spinner = AnsiConsole.Profile.Capabilities.Unicode ? Spinner.Known.Dots2 : Spinner.Known.Line;

var client = new KhClient();

try
{
    // Use clipboard or ask for album URL
    Uri? albumEntryUrl = null;
    var useClipboard = false;
    if (Utils.IsClipboardAvailable())
    {
        var clipboardString = await ClipboardService.GetTextAsync();
        if (KhClient.TryCreateAlbumUri(clipboardString, out albumEntryUrl))
        {
            useClipboard = await AnsiConsole.PromptAsync(
                new ConfirmationPrompt($"Use album [link {accentColor}]{clipboardString}[/]"));
        }
    }

    if (!useClipboard)
        await AnsiConsole.PromptAsync(
            new TextPrompt<string>($"Enter [{accentColor}]full album URI[/]:")
            .Validate(str => KhClient.TryCreateAlbumUri(str, out albumEntryUrl) ? ValidationResult.Success() : ValidationResult.Error($"[{errorColor}]Invalid URI given![/]")));

    AnsiConsole.Clear();

    // Get album info
    var khAlbumResult = await AnsiConsole.Status()
        .AutoRefresh(true)
        .Spinner(spinner)
        .SpinnerStyle(new Style(foreground: accentColor))
        .StartAsync("Fetching album info", async ctx => await client.GetAlbumInfoAsync(albumEntryUrl!));

    if (!khAlbumResult.IsSuccessful)
    {
        AnsiConsole.MarkupLine($"[{errorColor}]{khAlbumResult.Error}[/]");
        Environment.Exit(1);
    }

    AnsiConsole.Write(new Panel($"[{accentColor}]{khAlbumResult.ResultValue.Title}[/]"));

    // Select format if needed (MP3 or FLAC)
    var selectedFormat = khAlbumResult.ResultValue.HasFlac ? AnsiConsole.Prompt(
        new SelectionPrompt<KhTrackFormat>()
            .Title("Select format")
            .HighlightStyle(accentColor)
            .UseConverter(format => $"{format} ({Utils.ToStringWithUnit(khAlbumResult.ResultValue.GetTotalSize(format))})")
            .AddChoices(KhTrackFormat.Mp3, KhTrackFormat.Flac)) : KhTrackFormat.Mp3;

    // Ask user which tracks to download
    var prompt = new MultiSelectionPrompt<KhTrack>()
        .Title("Select the tracks you wish to download")
        .HighlightStyle(accentColor)
        .PageSize(15)
        .MoreChoicesText($"[{disabledColor}](Move up and down to reveal more tracks)[/]")
        .InstructionsText($"[{disabledColor}](Press [{accentColor}]<space>[/] to toggle download, [{accentColor}]<enter>[/] to accept)[/]")
        .UseConverter(track => $"{Utils.ToStringWithUnit(track.GetSize(selectedFormat)),-10}{track.Length,-6}{$"{track.Number}.",4} {track.Title}");

    foreach (var track in khAlbumResult.ResultValue.Tracks)
        prompt.AddChoices(track, t => t.Select());

    var selectedTracks = await AnsiConsole.PromptAsync(prompt);
    var selectedSize = Utils.ToStringWithUnit(selectedTracks.Sum(t => t.GetSize(selectedFormat)));

    // Download?
    var confirmDownload = await AnsiConsole.PromptAsync(
        new TextPrompt<bool>($"Download [{accentColor}]{selectedTracks.Count}[/] tracks ([{accentColor}]{selectedSize}[/])")
            .AddChoice(true)
            .AddChoice(false)
            .DefaultValue(true)
            .WithConverter(choice => choice ? "y" : "n"));

    if (!confirmDownload)
        Environment.Exit(0);

    // Make directory if needed
    //var localPath = Path.GetDirectoryName(AppContext.BaseDirectory);
    var downloadDir = Path.Combine(Directory.GetCurrentDirectory(), khAlbumResult.ResultValue.Title);

    if (!Directory.Exists(downloadDir))
        Directory.CreateDirectory(downloadDir);

    var stopWatch = new Stopwatch();
    stopWatch.Start();

    // Start downloading
    var semaphore = new SemaphoreSlim(5);
    await AnsiConsole.Progress()
        .AutoClear(false)
        .Columns(new ProgressColumn[]
        {
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new DownloadedColumn(),
        new SpinnerColumn(spinner)
        })
        .StartAsync(async ctx =>
        {
            var tasks = selectedTracks.Select(async track =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var progressTask = ctx.AddTask(track.Title, autoStart: true).IsIndeterminate(true);

                    var trackStreamResult = await client.GetTrackStreamAsync(track, selectedFormat);
                    if (trackStreamResult.IsSuccessful)
                    {
                        var filePath = Path.Combine(downloadDir, trackStreamResult.ResultValue.FileName);

                        progressTask.IsIndeterminate(false);
                        progressTask.MaxValue(trackStreamResult.ResultValue.ContentSize);

                        using var audioStream = trackStreamResult.ResultValue.ContentStream;
                        using var file = File.Create(filePath);
                        var copyTask = audioStream.CopyToAsync(file);

                        while (!copyTask.IsCompleted)
                        {
                            //double percentage = (double) file.Length / trackStreamResult.ResultValue.Length * 100;
                            progressTask.Value(file.Length);
                            await Task.Delay(100);
                        }

                        progressTask.Value(file.Length);
                    }
                    else
                    {
                        progressTask.Description($"[{errorColor}]{khAlbumResult.Error}[/]");
                        progressTask.Value = 0;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        });

    stopWatch.Stop();

    AnsiConsole.MarkupLine($"Done! Downloaded [{accentColor}]{selectedSize}[/] in [{accentColor}]{Utils.FormatTimeSpan(stopWatch.Elapsed)}[/]");
    await Utils.PressAnyKeyOrWaitAsync(3000);
}
catch (Exception ex)
{
    AnsiConsole.Clear();
    AnsiConsole.MarkupLine($"[{errorColor}]{ex.Message}[/]");
    AnsiConsole.MarkupLine($"[{disabledColor}]Press any key to exit.[/]");
    Console.ReadKey(true);
    Environment.Exit(1);
}