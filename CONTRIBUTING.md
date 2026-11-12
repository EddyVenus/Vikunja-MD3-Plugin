# Contributing

Thanks for helping. This is a beta maintained by one person, so replies are best effort.

## Reporting a bug

Use the [bug report form](https://github.com/EddyVenus/Vikunja-MD3-Plugin/issues/new/choose). It asks for the plugin,
Macro Deck and Vikunja versions, the widget and tile size, and what happened. Two rules:

- **Remove your server's address** from anything you paste.
- **Never paste your API token.** If you did by mistake, delete the token in Vikunja (Settings, API Tokens) and create a
  new one. Then edit or delete the issue.

For a security problem, follow [SECURITY.md](SECURITY.md) instead of opening an issue.

## Suggesting something

Use the "Idea or request" form. The plugin only lists and checks off tasks on purpose (see Limitations in the README), so
requests that add more are weighed against keeping it small.

## Changing the code

Read [AGENTS.md](AGENTS.md) first: it has the rules for changing the code. Before you open a pull request:

```bash
dotnet build VikunjaPlugin.slnx -c Release -warnaserror
dotnet test -c Release
macrodeck-plugin test --project src/VikunjaPlugin --required-only
```

A change needs a test that fails without it, and a line in [CHANGELOG.md](CHANGELOG.md). Never commit a token, a server
address or a personal email.
