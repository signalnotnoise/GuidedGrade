# Repository maintenance

- Keep `KNOWLEDGE_GRAPH.md` current in the same change when adding, removing,
  or changing components, dependencies, call paths, UI workflows, persistence,
  or memory ownership. Update affected Mermaid diagrams and descriptions.
- Verify graph statements against the source. Distinguish implemented behavior,
  limitations, and proposed work; do not describe planned features as complete.
- Update the graph's source-review date after checking the affected paths.
- Keep related behavior documents, including `FEEDBACK_WORKSPACE.md`,
  `CONSOLE_INTERACTION.md`, and `MEMORY_LIMITS.md`, consistent when applicable.
- For changes that do not affect the graph, no diagram edit is necessary.

# UI framework coordination

- Use the pinned custom UI framework for new application UI; preserve specialized
  WPF controls through its native adapter where a declarative primitive is missing.
- Report framework bugs and missing features discovered during this work to the
  CUI task `Find better Windows projects` (`01a0b5ac-e353-71c0-99ee-be4bb74a6007`).
  The user explicitly authorized coordination with that framework agent.
- Keep package provenance and validation limitations in `vendor/ui-framework`.
  Do not replace an existing package version with different bytes or describe a
  local integration package as a performance-cleared public release.
- Remove superseded UI files and packages only after checking references and
  validating their replacements; preserve user data and unrelated working changes.
