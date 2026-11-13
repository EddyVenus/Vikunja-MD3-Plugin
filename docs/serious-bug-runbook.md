# What to do when a serious bug arrives

A bug is **serious** when it can hurt a user's data or account, not just look wrong:

- the plugin marks the wrong task as done, or marks tasks done without a press;
- the API token or the server address shows up in a log, an error message or a file;
- the plugin crashes or freezes Macro Deck;
- it works against another user's Vikunja account.

## Steps

1. **Read and reproduce.** Reproduce with the numbers from the issue (plugin, Macro Deck and Vikunja versions). If the
   report includes a token or an address, edit it out of the issue right away and tell the reporter to delete the token.
2. **Warn users first.** Pin an issue that says what is affected, which versions, and the workaround: disable the plugin
   in Macro Deck, and delete the API token in Vikunja (Settings, API Tokens) if the account may be at risk.
3. **Stop new installs of the affected version.** Withdraw that version in the Creator Portal if it was published.
   Only the owner can do this.
4. **Fix it with a test.** Write the failing test first, then the fix. Run the checks in CONTRIBUTING.md.
5. **Ship a new version.** Never reuse a version number: the Store refuses it. Mark the entry in CHANGELOG.md as a fix
   for the problem, and as **Breaking** if users have to do something.
6. **Close the loop.** Say in the pinned issue which version fixes it, and thank the reporter.

## Aim (a suggestion; the owner decides the real numbers)

- Acknowledge a serious report within 3 days.
- Put a warning in the README or a pinned issue the same day the problem is confirmed.
