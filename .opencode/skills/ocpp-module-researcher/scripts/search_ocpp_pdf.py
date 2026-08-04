"""Search the OCPP 2.0.1 Part 2 specification PDF for one or more keywords.

Designed for the `ocpp-module-researcher` opencode skill, but general enough
to be reused for any keyword search against a PDF.

Usage (PowerShell):
    python search_ocpp_pdf.py "SetVariablesRequest" "SetVariablesResponse"
    python search_ocpp_pdf.py "Set Variables" "B05" --pages 50-80
    python search_ocpp_pdf.py "TriggerMessage" -o out.txt

The script is read-only and never modifies the PDF.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from pathlib import Path

# Default location of the OCPP 2.0.1 Part 2 spec relative to the repo root.
DEFAULT_PDF = Path("docs/OCPP-2.0.1_part2_specification_edition2.pdf")


def parse_page_range(spec: str, total: int) -> set[int]:
    """Parse a page range like "50-80" or "50,60-65" into a set of 1-based page numbers."""
    pages: set[int] = set()
    for chunk in spec.split(","):
        chunk = chunk.strip()
        if not chunk:
            continue
        if "-" in chunk:
            start, end = chunk.split("-", 1)
            start_i = max(1, int(start))
            end_i = min(total, int(end))
            pages.update(range(start_i, end_i + 1))
        else:
            n = int(chunk)
            if 1 <= n <= total:
                pages.add(n)
    return pages


def ensure_pypdf() -> None:
    try:
        import pypdf  # noqa: F401
    except ImportError:
        import subprocess

        print("pypdf not installed; installing...", file=sys.stderr)
        subprocess.check_call([sys.executable, "-m", "pip", "install", "pypdf"])


def collect_matches(
    pdf_path: Path,
    terms: list[str],
    page_filter: set[int] | None,
    context_chars: int,
    max_pages: int | None,
) -> list[tuple[int, str, list[str]]]:
    """Return [(page_number, full_page_text, matching_terms)] for each hit."""
    import pypdf

    reader = pypdf.PdfReader(str(pdf_path))
    total = len(reader.pages)
    print(f"PDF: {pdf_path} ({total} pages)", file=sys.stderr)

    lower_terms = [t.lower() for t in terms if t]
    results: list[tuple[int, str, list[str]]] = []
    scanned = 0

    for i, page in enumerate(reader.pages, start=1):
        if page_filter is not None and i not in page_filter:
            continue
        try:
            text = page.extract_text() or ""
        except Exception as exc:  # pragma: no cover
            print(f"  ! page {i}: extract failed: {exc}", file=sys.stderr)
            continue
        scanned += 1
        lower_text = text.lower()
        hits = [t for t in lower_terms if t in lower_text]
        if hits:
            results.append((i, text, [terms[lower_terms.index(h)] for h in hits]))
            if max_pages is not None and len(results) >= max_pages:
                break

    print(f"Scanned {scanned} pages, {len(results)} hit(s).", file=sys.stderr)
    return results


def format_results(
    results: list[tuple[int, str, list[str]]],
    terms: list[str],
    context_chars: int,
    show_full: bool,
) -> str:
    out_lines: list[str] = []
    out_lines.append(f"# Search hits for: {', '.join(terms)}")
    out_lines.append(f"# {len(results)} page(s) matched\n")

    for page_num, text, hits in results:
        out_lines.append(f"--- Page {page_num} (matched: {', '.join(sorted(set(hits)))} ---")
        if show_full:
            out_lines.append(text.rstrip())
        else:
            snippet = extract_snippet(text, terms, context_chars)
            out_lines.append(snippet)
        out_lines.append("")
    return "\n".join(out_lines)


def extract_snippet(text: str, terms: list[str], context_chars: int) -> str:
    """Return up to `context_chars` chars around the first match of any term."""
    lower = text.lower()
    earliest = None
    for t in terms:
        t_lower = t.lower()
        idx = lower.find(t_lower)
        if idx != -1 and (earliest is None or idx < earliest):
            earliest = idx
    if earliest is None:
        return text[:context_chars]
    start = max(0, earliest - context_chars // 2)
    end = min(len(text), earliest + context_chars // 2)
    prefix = "..." if start > 0 else ""
    suffix = "..." if end < len(text) else ""
    return f"{prefix}{text[start:end].rstrip()}{suffix}"


def build_arg_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        description="Search the OCPP 2.0.1 Part 2 specification PDF for keywords.",
    )
    p.add_argument(
        "terms",
        nargs="+",
        help="One or more search terms (case-insensitive substring match).",
    )
    p.add_argument(
        "--pdf",
        type=Path,
        default=DEFAULT_PDF,
        help=f"Path to the PDF (default: {DEFAULT_PDF}).",
    )
    p.add_argument(
        "--pages",
        type=str,
        default=None,
        help="Restrict to a page range, e.g. '50-80' or '40,60-65'.",
    )
    p.add_argument(
        "-o",
        "--output",
        type=Path,
        default=None,
        help="Write results to this file in addition to stdout.",
    )
    p.add_argument(
        "--context",
        type=int,
        default=600,
        help="Snippet context size in characters (ignored with --full).",
    )
    p.add_argument(
        "--full",
        action="store_true",
        help="Print the full text of each matching page (otherwise snippets).",
    )
    p.add_argument(
        "--max",
        type=int,
        default=None,
        help="Stop after this many matching pages.",
    )
    return p


def main(argv: list[str] | None = None) -> int:
    args = build_arg_parser().parse_args(argv)

    if not args.pdf.exists():
        print(f"PDF not found: {args.pdf}", file=sys.stderr)
        print("Pass --pdf <path> to override.", file=sys.stderr)
        return 2

    ensure_pypdf()

    page_filter: set[int] | None = None
    if args.pages:
        import pypdf

        total = len(pypdf.PdfReader(str(args.pdf)).pages)
        page_filter = parse_page_range(args.pages, total)

    results = collect_matches(
        args.pdf,
        args.terms,
        page_filter,
        args.context,
        args.max,
    )

    formatted = format_results(results, args.terms, args.context, args.full)
    print(formatted)

    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(formatted, encoding="utf-8")
        print(f"\nWrote {args.output}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
