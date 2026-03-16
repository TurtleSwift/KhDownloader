## Info

**KhDownloader** is a simple app for downloading albums from [khinsider](https://downloads.khinsider.com/). Works on Windows, Linux, and macOS. No dependencies required — the program runs from a single binary.

## Usage

Just run the app and follow the instructions. Requires an interactive shell. Looks best when unicode is supported.

[demo-linux](https://github.com/user-attachments/assets/0e4df70d-89cd-40cb-8ea8-f6ce3fc14ac2)

https://github.com/user-attachments/assets/c5bf729a-0183-45f2-b682-480492d75538

<details>
<summary>How to turn on unicode support in PowerShell</summary>

Modify your powershell profile or simply execute this:

```
if (-not (Test-Path $PROFILE)) { New-Item -Path $PROFILE -ItemType File -Force | Out-Null }
Add-Content -Path $PROFILE -Value "[console]::InputEncoding = [console]::OutputEncoding = [System.Text.UTF8Encoding]::new()"
```

This checks if a profile file exists, creates one if it does not and appends a line which enables unicode on the profile. This gets executed every time a new terminal window is opened. Not required but looks nicer.
</details>

## Development

.NET 10 SDK is needed to build and develop the app. Open a PR if you wish to contribute.

## Khinsider

Not affiliated with khinsider. Please be considerate of their bandwidth and download sparingly. Or better yet, [donate to them](https://downloads.khinsider.com/forums/index.php?account/upgrades).
