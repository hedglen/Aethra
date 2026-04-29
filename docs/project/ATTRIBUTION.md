# Aethra Attribution Guide

This guide explains how to acknowledge Aethra when you reuse, redistribute, or build commercial software from this repository.

## License model

Aethra is licensed under **GPL-2.0-or-later**.

- See `LICENSE` (GPL-2.0-or-later)
- See `NOTICE` for additional attribution/provenance guidance

## Baseline acknowledgment text

Use this wording when practical:

`This product includes software derived from Aethra (https://github.com/hedglen/Aethra). Original work by Rob Hedglen.`

## Common scenarios

### 1) GitHub fork

- Keep original license files in the repository.
- Keep `NOTICE` and third-party provenance references.
- Keep upstream attribution in your README or project description.

Suggested README note:

`Forked from Aethra by Rob Hedglen: https://github.com/hedglen/Aethra`

### 2) Redistributed binaries

- Include the applicable license text with your distribution.
- Include `NOTICE` and source/provenance references for imported third-party code.
- Include attribution in release notes, installer credits, or about dialog.
- If you bundle native runtime libraries, also comply with each bundled library's own obligations (for example LGPL/GPL notice/source terms where applicable).
- Use `src/Aethra/ThirdPartyNotices/THIRD_PARTY_NOTICES.md` as the compliance and provenance reference for runtime binaries.

### 3) Commercial derivative app

- Commercial use is allowed by GPL (including paid distribution) as long as GPL obligations are met.
- You must still keep required license and notice text.
- Acknowledge upstream source in your docs/about page where practical.
- Commercial distribution does not remove bundled runtime obligations; if your product ships third-party native binaries, those licenses still apply.

## Transparency recommendations

For healthy open-source reuse:

- Keep your fork and change history public when possible.
- Clearly state what you changed from upstream.
- Link back to the original repository in user-facing docs.

## Citation metadata

Machine-readable citation is provided in `CITATION.cff`.
