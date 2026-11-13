# Security policy

## Reporting a vulnerability

Please do **not** open a public issue for a security problem. Use GitHub's private reporting instead: open the
[Security tab](https://github.com/EddyVenus/Vikunja-MD3-Plugin/security) of this repository and choose "Report a
vulnerability". Say what you found, how to reproduce it and which plugin version you used.

This is a beta maintained by one person, so replies are best effort. You will get an acknowledgement as soon as
possible, and a fix goes out as a new version.

## What the plugin handles

The plugin stores your Vikunja API token in Macro Deck's secret store, sends it only as a bearer header to the address
you configured, and never logs it. If you think a token leaked, delete it in Vikunja (Settings, API Tokens) first, then
report it here.

## Supported versions

Only the latest release is supported.
