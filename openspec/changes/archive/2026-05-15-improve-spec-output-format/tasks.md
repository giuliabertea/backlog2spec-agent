## 1. Update the GeneratedSpec model

- [x] 1.1 Rename `Summary` → `Goal`, `AcceptanceCriteria` → `Behaviour`, `ComponentBreakdown` → `FilesToChange` in `GeneratedSpec.cs` (update `[JsonPropertyName]` attributes to match)

## 2. Update the LLM prompt

- [x] 2.1 Replace the JSON schema block in `spec.txt` with the new five-field schema (`goal`, `behaviour`, `edgeCases`, `outOfScope`, `filesToChange`)
- [x] 2.2 Update the Rules section in `spec.txt`: remove Gherkin rules, add rules for `goal` (1–3 sentences), `behaviour` (plain-English bullets), and `filesToChange` (resolve paths from codebase context, fall back to `ClassName?`)
- [x] 2.3 Replace the Example Output block in `spec.txt` to match the new schema
- [x] 2.4 Update the "Output EXACTLY these five fields in this exact order" rule with the new field names

## 3. Update the console renderer

- [x] 3.1 In `OutputRenderer.RenderSpec`, replace section headers and property references: Summary → Goal, Acceptance Criteria → Behaviour, Component Breakdown → Files to Change
- [x] 3.2 Remove the Gherkin `code block` rendering for behaviour items — render as plain bullets (same style as edge cases but white)

## 4. Update the markdown renderer

- [x] 4.1 In `OutputRenderer.RenderMarkdown`, update section headings and property references to match new field names
- [x] 4.2 Remove the ` ```gherkin ``` ` code block wrapping for behaviour items — render as plain `- ` bullets
- [x] 4.3 In `OutputRenderer.WriteHierarchyToFiles`, apply the same heading and rendering changes as 4.1–4.2

## 5. Verify end-to-end

- [x] 5.1 Build the solution and confirm no compilation errors
- [x] 5.2 Run with `--mock` flag and verify the console output shows Goal / Behaviour / Edge Cases / Out of Scope / Files to Change sections
- [x] 5.3 Run with `--output` flag and verify the generated markdown file uses the new headings and no Gherkin blocks

## 6. Fix verification warnings (W1 + W2)

- [x] 6.1 Fix double-colon bug in `WriteHierarchyToFiles`: change `f[colonIdx..]` → `f[(colonIdx + 1)..]` (same fix already applied to `RenderMarkdown`)
- [x] 6.2 Bold the path portion of `FilesToChange` entries in `RenderSpec` (console renderer) to match the spec scenario
- [x] 6.3 Close the open question in `design.md`: document that plain list with bold path was chosen for markdown output
