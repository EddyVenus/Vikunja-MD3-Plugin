# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/) and versions
follow [Semantic Versioning](https://semver.org/). A change that breaks existing widgets or settings is marked
**Breaking**.

## [2.0.0] - 2026-11-22

### Changed

- **Breaking**: the plugin now uses Vikunja's API v2 and needs Vikunja 2.7.1 or newer. Setup checks the version and
  explains when Vikunja is too old, and a tile connected to an older Vikunja says so. For an older Vikunja, update
  Vikunja or stay on 1.0.0. The saved address and token are kept: nothing has to be set up again.
- Checking a task now changes only its done state. A task edited elsewhere is no longer overwritten with older values.
- The token needs only Tasks: Read All and Update. Read One is no longer used; a 1.0.0 token keeps working.
- Built against Macro Deck SDK 3.0.5.

### Fixed

- A Vikunja that returns fewer than 50 tasks per page is now read to the end instead of stopping after the first page.

## [1.0.0] - 2026-11-13

First stable release, for the Macro Deck Store. The code is the same as 1.0.0-beta.2.

### Changed

- The plugin description now also states the Vikunja version, the token access, the 200-task limit and Windows x64.
- README: beta wording removed, install points to the Store, "Testers wanted" is now "Feedback".

## [1.0.0-beta.2] - 2026-11-13

### Changed

- New plugin icon: a task list with one task done, and a V.
- The plugin description now says a Vikunja server and an API token are required.
- README: the install name points to this version and there is a "Testers wanted" section.

## [1.0.0-beta.1] - 2026-11-13

First beta.

### Added

- Two widgets: a read-only list of your open Vikunja tasks and a checklist where pressing a task marks it as done.
- Priority dot, due date, header and swipe paging on list tiles; a counter option for square tiles.
- Text size, card size and "square tiles show" settings.
- Tasks are dimmed with an orange "!" when Vikunja cannot be reached, and a rejected token has its own message.
- Setup checks that the address answers like Vikunja before the token is saved.

### Known limitations

See the Limitations section of the [README](README.md).

[1.0.0]: https://github.com/EddyVenus/Vikunja-MD3-Plugin/releases/tag/v1.0.0
[1.0.0-beta.2]: https://github.com/EddyVenus/Vikunja-MD3-Plugin/releases/tag/v1.0.0-beta.2
[1.0.0-beta.1]: https://github.com/EddyVenus/Vikunja-MD3-Plugin/releases/tag/v1.0.0-beta.1
