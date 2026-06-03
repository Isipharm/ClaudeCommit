# ClaudeCommit — Domain Glossary

## Git Changes View
The built-in Visual Studio panel showing all current repository changes (staged and unstaged). The extension adds UI elements to this view.

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

## Toolbar Button
The primary UI entry point in the Git Changes View toolbar that triggers Generation. Disabled and shows a spinner during active Generation; replaced by a Cancel Button while generating.

## Cancel Button
Replaces the Toolbar Button during Generation. Clicking it kills the Claude CLI process and restores the Toolbar Button.

## InfoBar
A non-blocking, dismissible notification bar shown in the Git Changes View when Generation fails (e.g., Claude CLI not installed, process error). May contain actionable links.
