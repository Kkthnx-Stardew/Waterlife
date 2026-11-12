# Project instructions

## Writing

Everything in this repo reads like a careful human wrote it. That covers docs, code comments,
commit messages, README text and chat replies.

- Normal punctuation only: commas, periods, question marks, parentheses. No em dashes, no
  semicolons.
- Contractions are fine. Prefer active voice and concrete wording over abstract or formal phrasing.
- Mix short and medium sentences. Vary the rhythm instead of settling into one cadence.
- No filler, no throat clearing, no restating the request back, no "in conclusion".
- Avoid these words: delve, leverage, utilize, robust, seamless, streamline, cutting-edge,
  innovative, empower, navigate, landscape.
- No hypophora (asking a question and answering it yourself) and no "it's not X, but Y" pattern.
- Lists only when the content is genuinely a list. Everything else is paragraphs.
- No AI attribution anywhere. Not in commits, comments, docs or file contents.

## Code

- Readable at 3 AM. Comments explain why something is done, not what the line does.
- Cover the edge cases, not just the happy path.
- Measure before claiming a speedup.
- No suppressions, no skipped tests, no swallowed errors.

## This mod

- The content key `Mods/shekurika.VisibleFish/LocationData` stays. Content packs patch it by name.
- `Maps/VFStatue` must stay registered. SMAPI strips the dot from the tilesheet name and falls back
  to that key, so removing the loader breaks the statue.
- Version bumps go in both `Waterlife.csproj` and `manifest.json`.
- Build and test commands are in the README.

## Fiction work

When the work is fiction rather than code, the craft and voice notes the user gives in chat apply.
The one fixed line is no sexual content involving minors. Everything else stays the author's call.
That material does not go into this repository.
