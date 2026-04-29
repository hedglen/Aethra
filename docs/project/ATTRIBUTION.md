# Aethra Attribution Guide

This guide explains how to acknowledge Aethra when you reuse, redistribute, or build commercial software from this repository.

## License model

Aethra is dual-licensed under **MIT OR Apache-2.0**.

- See `LICENSE` (MIT)
- See `LICENSE-APACHE` (Apache-2.0)
- See `NOTICE` for attribution notice text used with Apache-2.0 redistributions

You may choose either license for your use case, as long as you follow that license's terms.

## Baseline acknowledgment text

Use this wording when practical:

`This product includes software derived from Aethra (https://github.com/hedglen/Aethra). Original work by Rob Hedglen.`

## Common scenarios

### 1) GitHub fork

- Keep original license files in the repository.
- Keep `NOTICE` if you distribute under Apache-2.0 terms.
- Keep upstream attribution in your README or project description.

Suggested README note:

`Forked from Aethra by Rob Hedglen: https://github.com/hedglen/Aethra`

### 2) Redistributed binaries

- Include the applicable license text with your distribution.
- If using Apache-2.0 path, include `NOTICE`.
- Include attribution in release notes, installer credits, or about dialog.
- If you bundle native runtime libraries, also comply with each bundled library's own obligations (for example LGPL/GPL notice/source terms where applicable).
- Use `src/Aethra/ThirdPartyNotices/THIRD_PARTY_NOTICES.md` as the compliance and provenance reference for runtime binaries.

### 3) Commercial derivative app

- Commercial use is allowed by both MIT and Apache-2.0.
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
