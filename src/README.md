### KhClient.cs

Contains the business logic, parsing from khinsider and album/track models. Should be adaptable to any other app if needed since it doesn't rely on console or anything else. Methods return handy `Result<T>` records.

### Program.cs

The application entry point. Contains the console UI and app flow, which is basically: 

`enter URI or take it from clipboard` -> `fetch album data` -> `select format and tracks` -> `download tracks`

Rich experience provided with *Spectre.Console*. This should probably be moved to its own class, but it should be fine for now since the app doesn't really do anything else.

### Utils.cs

Contains a few static helpers.

#### Anything else?

Clipboard on linux requires `xsel`. `Utils.IsClipboardAvailable()` checks this.