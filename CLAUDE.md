## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `./update-graph.ps1` (PowerShell, from the project root) to keep the graph current (AST-only, no API cost). It runs `graphify update Assets` and moves the output to graphify-out/ at the project root, because graphify writes inside Assets/ where Unity would import it. Never run `graphify update .`: that would scan Unity's huge Library/ folder.
