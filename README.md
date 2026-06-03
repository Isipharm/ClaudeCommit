# ClaudeCommit

**AI-generated commit messages and TFVC check-in comments — directly inside Visual Studio.**

One click in the Git Changes or Pending Changes panel sends your diff to Claude and injects the result straight into the message field. No copy-paste, no context switching, no API key to manage.

---

## Features

- **Git & TFVC support** — works with both Git repositories and Team Foundation Version Control pending changes
- **One-click generation** — toolbar button in the Git Changes / Pending Changes panel triggers generation instantly
- **Conventional Commits by default** — default prompt produces `type(scope): description` messages
- **Fully customizable prompts** — edit the prompt template in *Tools > Options > Claude Commit* using `{diff}` and `{status}` placeholders
- **Cancel mid-generation** — a Cancel button replaces the toolbar button while Claude is running; click it to kill the process and restore the panel
- **Error InfoBar** — if the CLI is missing or fails, a non-blocking notification bar appears with context
- **No API key required** — the extension shells out to the `claude` CLI; authentication is handled by the CLI itself

---

## Requirements

| Requirement | Detail |
|---|---|
| Visual Studio | 2022 (17.x) or 2026 (18.x), x64 |
| Edition | Community, Professional, or Enterprise |
| .NET Framework | 4.7.2 or later |
| [Claude CLI](https://claude.ai/download) | Must be installed and authenticated on the machine |

> **Claude CLI** is a hard dependency. The extension does not call the Anthropic API directly — it spawns the `claude` process and captures its output. This keeps the extension thin and auth-free.

---

## Installation

### From Visual Studio Marketplace *(coming soon)*

Search for **ClaudeCommit** in *Extensions > Manage Extensions* or visit the [Marketplace page](https://marketplace.visualstudio.com/items?itemName=Isipharm.ClaudeCommit).

### From VSIX

1. Download the latest `.vsix` from the [Releases page](https://github.com/Isipharm/ClaudeCommit/releases)
2. Double-click the file — the VSIX installer opens automatically
3. Restart Visual Studio

---

## Usage

1. Make changes in your repository
2. Open the **Tools** menu
3. Click the **Generate Commit Message with Claude** menu option
4. Claude generates a message and injects it into the commit message field
5. Review, edit if needed, then commit

For TFVC, the same process is available.

---

## Configuration

Go to **Tools > Options > Claude Commit**.

### Git Prompt Template

Template sent to Claude for Git commits. Default:

```
Generate a concise git commit message following the Conventional Commits format
(type(scope): description).

Changed files:
{status}

Diff:
{diff}

Output ONLY the commit message. No explanation, no markdown, no quotes.
```

### TFVC Prompt Template

Same structure, used for TFVC check-in comments instead.

### Placeholders

| Placeholder | Replaced with |
|---|---|
| `{diff}` | Full diff content (staged + unstaged) |
| `{status}` | File status summary (added / modified / deleted) |

### Claude CLI Path

Leave empty to use `claude` from your system `PATH`. Set an absolute path if you have a non-standard installation.

---

## Architecture

The extension shells out to the `claude` CLI rather than calling the Anthropic API directly. This means:

- No API key stored in VS settings
- Authentication is managed by the CLI (same session the developer already uses)
- Model selection, rate limiting, and API versioning are handled by the CLI
- The extension stays thin with no Anthropic SDK dependency

Trade-off: the `claude` CLI must be installed. If it is missing, the InfoBar will surface an error explaining how to install it.

See [ADR-0001](docs/adr/0001-claude-cli-over-direct-api.md) for the full decision record.

---

## Building from source

```powershell
git clone https://github.com/Isipharm/ClaudeCommit.git
cd ClaudeCommit
# Open ClaudeCommit.sln in Visual Studio 2022 or later
# Build > Build Solution  (F6)
```

The output VSIX is written to `src/ClaudeCommit/bin/Release/net472/`.

---

## Contributing

Issues and pull requests are welcome. Please open an issue first for significant changes so we can discuss the approach before you invest time in an implementation.

---

## License

See [LICENSE](src/ClaudeCommit/Resources/LICENSE.txt).
