# Release description workflow

`1.1.0.md` is the English body for the 1.1.0 GitHub Release; do not edit the published 1.0.0 release to imply that it includes these changes.

The first four short English bullets are intentionally compatible with older Thermiqra updaters, which extract the first four non-heading lines. Existing clients will therefore get a compact English summary for this transition release.

The 1.1.0 updater reads `thermiqra-summary:en` / `thermiqra-summary:ru` HTML-comment blocks. These keep the visible GitHub page English-only and provide localized in-app highlights in subsequent releases. Keep summaries within four lines and 500 characters. Retain visible highlights first: old clients do not understand the new markers.

After final validation, record the exact automated-test count in the overview, finalize any scope changes, sign/verify the installer bundle as described in `Tools/ReleaseSigning/README.md`, and publish the release body with the matching installer, signature and checksum. This documentation does not publish anything itself.
