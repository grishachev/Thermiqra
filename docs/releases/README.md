# Release description workflow

`1.1.1.md` is the current release body; `1.1.0.md` preserves the preceding release description; do not edit the published 1.0.0 release to imply that it includes these changes.

The first four short English bullets are intentionally compatible with older Thermiqra updaters, which extract the first four non-heading lines. For 1.0.0, also include `## English` and `## Русский` headings inside the corresponding hidden comment blocks: the published parser selects those headings from the raw body, while GitHub hides the comments. This keeps both legacy in-app languages localized without duplicating visible page text.

The 1.1.0 updater reads `thermiqra-summary:en` / `thermiqra-summary:ru` HTML-comment blocks. These keep the visible GitHub page English-only and provide localized in-app highlights in subsequent releases. Keep summaries within four lines and 500 characters. Retain visible highlights first for clients with no language-section support. Preserve the hidden legacy language headings as well as the new markers. The compatibility tests execute a verbatim copy of the parser from the published v1.0.0 tag.

After final validation, record the exact automated-test count in the overview, finalize any scope changes, sign/verify the installer bundle as described in `Tools/ReleaseSigning/README.md`, and publish the release body with the matching installer, signature and checksum. This documentation does not publish anything itself.
