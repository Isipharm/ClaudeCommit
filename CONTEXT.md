# ClaudeCommit — Domain Glossary

## Git Changes View
The built-in Visual Studio panel showing all current repository changes (staged and unstaged). The extension injects a Generate Commit Message button directly above the commit message text field in this view.

## Pending Changes View
The Visual Studio panel for TFVC (Team Foundation Version Control) pending changes and check-in comments. The extension injects a Generate Commit Message button directly above the check-in comment text field in this view.

## Changes
All modified, added, or deleted files in the working tree — both staged and unstaged. The full set of Changes is what feeds Generation, not just staged files.

## Diff
The combined output of staged and unstaged Changes, including file status (added/modified/deleted) and content delta. This is what gets sent to Claude during Generation.

## Commit Message
The text a developer writes before committing. The extension populates this field in the Git Changes View with the output of Generation.

## Generation
The async process of invoking the Claude CLI with a Prompt and the current Diff, then capturing the output as a Commit Message candidate.

## Claude CLI
The `claude` command-line tool that must be installed on the developer's machine. The extension shells out to it — it is a hard dependency. The extension does not call the Claude API directly.

## Prompt Template
A user-configurable text template, editable in VS Tools > Options, that defines what is sent to the Claude CLI. Contains a `{diff}` placeholder which is replaced with the current Diff at generation time. A default template ships with the extension.

## Generate Button
The primary UI entry point injected above the commit message (or check-in comment) text field in the Git Changes View and Pending Changes View. Clicking it triggers Generation. Replaced by the Cancel Button while Generation is active.

## Cancel Button
Replaces the Generate Button during active Generation. Clicking it cancels the Claude CLI process and restores the Generate Button.

## InfoBar
A non-blocking, dismissible notification bar shown in the Git Changes View when Generation fails (e.g., Claude CLI not installed, process error). May contain actionable links.
