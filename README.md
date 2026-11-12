# Vikunja Tasks for Macro Deck 3

Show your open [Vikunja](https://vikunja.io) tasks on a Macro Deck widget and mark them as done from the deck.

## Widgets

| Widget | Behavior |
| --- | --- |
| Vikunja tasks (read only) | Lists your open tasks. Pressing does nothing. |
| Vikunja tasks (checklist) | Lists your open tasks. Pressing a task marks it as done in Vikunja and removes it from the list. |

Both widgets show up to `maxTasks` tasks (default 5, at most 50). Change it in the widget's JSON view:

```json
{ "maxTasks": 8 }
```

Tasks are the open ones across every project you can access: dated tasks first, soonest due first, then undated ones. Only the
first 200 open tasks are read. The list refreshes once a minute.
A checked task disappears from the tile immediately. A repeating task stays on the list, because Vikunja only moves its
dates forward. If Vikunja cannot be reached the last known list stays on screen and the failure is
logged once per episode.

## Setup

1. In Vikunja open Settings, API Tokens and create a token with only these permissions under Tasks: Read all, Read one
   and Update. The plugin needs nothing else.
2. In Macro Deck add the Vikunja integration and enter the address of your instance (for example
   `https://vikunja.example.com`) and the token. The connection is verified before it is saved.
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

## License

MIT, see [LICENSE](LICENSE). Packages shipped with the plugin are listed in
[THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES).
