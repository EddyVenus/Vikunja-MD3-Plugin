# Vikunja Tasks for Macro Deck 3

Show your open [Vikunja](https://vikunja.io) tasks on a Macro Deck widget and mark them as done from the deck.

> **Beta.** This is the first beta (1.0.0-beta.1). Expect rough edges, and please
> [report problems](#reporting-problems).

## Requirements

- **Macro Deck** 3.0.0 or newer, on Windows (x64). macOS and Linux are not built or tested. Macro Deck provides the .NET
  runtime, so there is nothing else to install.
- **Vikunja** 0.24.0 or newer, with API tokens available. Tested with Vikunja 2.4.0. The plugin uses the query-based task
  filter introduced in 0.24.0.
- The computer that runs Macro Deck must be able to reach your Vikunja address.

## Widgets

| Widget | Behavior |
| --- | --- |
| Vikunja tasks (read only) | Lists your open tasks. Pressing does nothing. |
| Vikunja tasks (checklist) | Lists your open tasks. Pressing a task marks it as done in Vikunja and removes it from the list. |

Each widget draws as many rows as fit its tile: a taller tile shows more tasks, and long titles are cut with an ellipsis.
Open the widget's settings in Macro Deck to change:

- **Text size (%)**: 50 to 200, default 100.
- **Card size (%)**: 50 to 200, default 100. Scales the padding and spacing of each task card.
- **Square tiles show**: List (default) or Counter. A square tile (1x1, 2x2...) can show only how many tasks are open; other shapes always show the list.

When there are more tasks than rows, swipe up for the next page and down for the previous one. It wraps around.
Every list tile shows the page in its header, like "2/4". With one page it shows the open count.
To fit more rows, lower the text or card size. Swiping needs a touch screen, so a hardware deck cannot change page.

Tasks are the open ones across every project you can access: dated tasks first, soonest due first, then undated ones. Only the
first 200 open tasks are read and kept, so the counter stops at 200. The list refreshes once a minute.
A checked task disappears from the tile immediately. A repeating task stays on the list, because Vikunja only moves its
dates forward. If Vikunja cannot be reached the last known list stays on screen and the failure is
logged once per episode.

## Install

1. Download `com.eddyvenus.vikunja-1.0.0-beta.1.macroDeckPlugin` from the
   [Releases](https://github.com/EddyVenus/Vikunja-MD3-Plugin/releases) page.
2. In Macro Deck open the **Integrations** tab and install the downloaded file from there.

Once the plugin is listed in the Macro Deck Store, install it from the Store instead.

## Setup

1. In Vikunja open Settings, API Tokens and create a token with only these permissions under Tasks: Read all, Read one
   and Update. The plugin needs nothing else. An expiry date is optional: when it passes, the tiles show "Vikunja
   rejected the API token" and you create a new token and enter it again. To cut the plugin's access, delete the token
   in Vikunja.
2. In Macro Deck add the Vikunja integration and enter the address of your instance (for example
   `https://vikunja.example.com`) and the token. Use `https` unless Vikunja is on your own network. The connection is verified before it is saved.
3. Add a "Vikunja tasks" widget to a deck.

## Privacy

The plugin only talks to the Vikunja instance you configure. It sends the API token as a bearer header and reads and
updates your tasks there. The token is stored in Macro Deck's encrypted secret store and is never logged. Nothing is
sent anywhere else. An `http://` address is accepted for local networks, but then the token travels unencrypted.

## Limitations

- Checking a task sends the whole task back with `done` set, as the Vikunja API requires. A task edited elsewhere
  between the read and the write is overwritten with the values just read.
- Only checking is supported. Unchecking, creating and editing tasks are not.
- The list is shared by all widgets. Per-widget project or filter selection is not available yet.
- Vikunja behind a login proxy that intercepts API calls (Authelia, Cloudflare Access and similar) is not supported: the
  API must answer requests that carry the token.
- At most 200 open tasks are read. A hardware deck cannot change page, because it has no swipe.
- Beta updates are manual. A plugin installed from a file gets no update notice: watch the repository's Releases
  (Watch, Custom, Releases) and install each new `.macroDeckPlugin` over the old one.
- No support policy yet. A future Macro Deck release may break the plugin until a new version is published.

## Development

Requires the .NET 10 SDK.

```bash
dotnet build -c Release -warnaserror
dotnet test -c Release
macrodeck-plugin test --project src/VikunjaPlugin
macrodeck-plugin build --source src/VikunjaPlugin --output ./artifacts
```

To try it in a real Macro Deck, use the "Macro Deck - Real Host" launch profile. `AGENTS.md` has the rules for changing
the code and the Store gate every release has to pass.

## Reporting problems

Open an [issue](https://github.com/EddyVenus/Vikunja-MD3-Plugin/issues/new/choose) and say which versions of the plugin,
Macro Deck and Vikunja you use and what you did. **Remove your server's address and never paste your token** in an
issue or a log. For a security problem, see [SECURITY.md](SECURITY.md). More in [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT, see [LICENSE](LICENSE). Packages shipped with the plugin are listed in
[THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES).
