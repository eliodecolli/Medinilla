---
name: ocpp-module-researcher
description: Research OCPP 2.0.1 protocol behaviour from the local PDF spec. Use when the user asks about the flow of an OCPP request/message (e.g. "flow of the SetVariablesRequest", "what does TriggerMessage do", "how does BootNotification work"), OCPP 2.0.1 use cases, message PDU definitions, data types, enumerations, configuration variables, or any other question that requires reading the OCPP 2.0.1 Part 2 specification. Do NOT use for OCPP 1.6 questions or for implementation work in the codebase.
---

# OCPP Module Researcher

Read the local OCPP 2.0.1 Part 2 specification PDF and extract the protocol-level
behaviour the user is asking about (a message, use case, configuration variable, etc.).

## Inputs
- **Topic**: which OCPP module / message / use case to research. The user may give
  it in any of these forms:
  - An action name like `SetVariablesRequest`, `TriggerMessage`, `BootNotification`.
  - A use case id like `B05`, `A01`, `J02`.
  - A free-form description like "charging station password update flow".
- **PDF location**: defaults to `docs/OCPP-2.0.1_part2_specification_edition2.pdf`
  (relative to the repo root). Override only if the user points elsewhere.

## Workflow

1. **Run the search script** with one or more relevant keyword variations. The
   script extracts every page that mentions any of the terms and writes the
   results to a file under `C:\Users\elio_\AppData\Local\Temp\opencode\` so the
   full output is still available even if the chat truncates it.

   ```powershell
   python .opencode/skills/ocpp-module-researcher/scripts/search_ocpp_pdf.py `
       "SetVariablesRequest" "SetVariablesResponse" "Set Variables" `
       --output "C:\Users\elio_\AppData\Local\Temp\opencode\ocpp_setvariables.txt"
   ```

   Tips for choosing keywords:
   - Search for the **Request** and **Response** names (camelCase, no spaces).
   - Also search for the **human-readable name** with spaces (e.g. `"Set Variables"`)
     because the spec uses that in use-case titles and sequence diagrams.
   - Add the **use case id** (e.g. `B05`) once you've seen it referenced; that
     narrows results to the canonical use case.
   - Add related data types and enums (`SetVariableDataType`,
     `SetVariableStatusEnumType`, `AttributeEnumType`) to capture the full
     payload contract.

2. **Read the script output** to map the topic onto the spec sections:
   - **Use case** (functional block + sequence diagram) — usually highlighted as
     a `Figure X` with an `ID` and `Scenario description`.
   - **Message PDU definition** — tables under "Messages, Datatypes & Enumerations"
     with field/cardinality/description.
   - **Data types** — classes like `SetVariableDataType`, `SetVariableResultType`.
   - **Enumerations** — `SetVariableStatusEnumType`, `AttributeEnumType`, etc.
   - **Configuration variables** — `ItemsPerMessageSetVariables`,
     `BytesPerMessageSetVariables`, etc., under "Referenced Components and Variables".
   - **Other use cases that reference it** — look for cross-references (A01,
     A05, B02, B10, J01, J02, …) for the broader picture.

3. **Synthesize the answer** in this order, citing page numbers from the PDF:
   1. **Direction and purpose** (CSMS ↔ Charging Station, what it achieves).
   2. **Sequence diagram** — show the call/response pair, optionally with
      preconditions and postconditions.
   3. **Request payload** — fields, types, cardinality, defaults.
   4. **Response payload** — fields, types, status values.
   5. **Key requirements** — the `FR.xx` rules that govern the behaviour.
   6. **Related configuration variables / size limits**.
   7. **Cross-references** — other use cases where this message appears.

   Keep the summary under ~4 paragraphs unless the user asks for detail.

4. **Citation rules** — these are mandatory, not optional:
   - **Every factual claim** must be followed by a source in parentheses,
     pointing at the PDF page. Use the `file_path:page` style, e.g.
     `(docs/OCPP-2.0.1_part2_specification_edition2.pdf:53)`.
   - The cite goes **inline after the claim**, not gathered at the end of
     the paragraph or section. The user must be able to trace each fact back
     to a page without re-reading the whole answer.
   - **Tables count.** A cell that contains a claim must end with the source
     in parentheses, just like prose. Add a `Source` column when every cell
     in a row would share the same citation, or append the cite to each
     cell when citations differ.
   - If a claim cannot be traced to a specific page, **do not make the
     claim**. Either drop it or flag it explicitly as
     `(inferred from <context>, not directly stated in spec)`.
   - If multiple pages support the same claim, cite the most specific one
     first, e.g. `(p. 53, Figure 14)` and only add others if the user asks.

5. **Neutrality rules** — these are also mandatory:
   - **Do not make assumptions about what's better or worse.** The spec
     describes protocol behaviour; it does not endorse one approach over
     another. Stick to what the spec says and what the spec requires.
   - Do not use comparative language that is not in the spec. Avoid phrases
     like "this is the recommended/best/preferred way", "you should
     really…", "it would be better to…", "this is more secure/flexible/
     modern…", unless the spec itself uses those words (e.g. "RECOMMENDED",
     "SHOULD"). When the spec uses a normative keyword, quote it
     (`SHALL` / `SHOULD` / `MAY` / `RECOMMENDED`) and cite the page.
   - Do not editorialize about design choices, market trends, or what other
     vendors do. If the user wants an opinion, they will ask.
   - When the spec actually contains a recommendation (e.g.
     `B05.FR.04` "The Charging Station SHALL respond with
     `SetVariablesResponse(Accepted)`"), report it neutrally as a requirement
     of the spec, not as a personal opinion.

6. **If the topic is not in Part 2**, tell the user — Part 1 (Architecture &
   Topology) and Part 4 (JSON over WebSockets) cover material that isn't in
   Part 2. Don't guess.

## Reference

- `scripts/search_ocpp_pdf.py` — extracts pages from the PDF that match any of
  the given search terms. See `REFERENCE.md` for full CLI usage.
- `docs/OCPP-2.0.1_part2_specification_edition2.pdf` — the source PDF (Part 2,
  Edition 2, 2022-12-15).
