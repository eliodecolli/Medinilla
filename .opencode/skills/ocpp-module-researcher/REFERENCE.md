# Reference: `search_ocpp_pdf.py`

Helper script for the `ocpp-module-researcher` skill. Searches the OCPP 2.0.1
Part 2 specification PDF for any of the given keywords and prints the matching
pages.

## Install

```powershell
pip install pypdf
```

The script auto-installs `pypdf` on first run if it's missing.

## Usage

```powershell
python .opencode/skills/ocpp-module-researcher/scripts/search_ocpp_pdf.py <term> [<term> ...] [options]
```

### Positional arguments

| Argument | Description |
|---|---|
| `terms` | One or more case-insensitive substring search terms. |

### Options

| Option | Description |
|---|---|
| `--pdf <path>` | Path to the PDF. Defaults to `docs/OCPP-2.0.1_part2_specification_edition2.pdf` (relative to cwd). |
| `--pages <range>` | Restrict to a page range, e.g. `50-80` or `40,60-65`. |
| `-o, --output <file>` | Also write results to this file. Parent dirs are created. |
| `--context <chars>` | Snippet size around the first match (default: 600). Ignored with `--full`. |
| `--full` | Print the full text of every matching page (no truncation). |
| `--max <n>` | Stop after `n` matching pages. |

### Examples

Search for the SetVariables flow:

```powershell
python .opencode/skills/ocpp-module-researcher/scripts/search_ocpp_pdf.py `
    "SetVariablesRequest" "SetVariablesResponse" "Set Variables" `
    -o "C:\Users\elio_\AppData\Local\Temp\opencode\ocpp_setvariables.txt"
```

Search only the use-case section for a specific use case id:

```powershell
python .opencode/skills/ocpp-module-researcher/scripts/search_ocpp_pdf.py "B05" --pages 50-70
```

Get the full text of every page that mentions a message:

```powershell
python .opencode/skills/ocpp-module-researcher/scripts/search_ocpp_pdf.py "TriggerMessage" --full
```

## Exit codes

- `0` — success (results may be empty).
- `2` — PDF not found.

## How it works

1. Opens the PDF with `pypdf`.
2. Iterates pages (filtered by `--pages` if set).
3. For each page, extracts text and looks for any of the search terms
   (case-insensitive substring).
4. Prints either a snippet around the first hit or, with `--full`, the entire
   page. The matching terms are listed in the page header.
5. Optionally mirrors the output to a file with `-o`.
