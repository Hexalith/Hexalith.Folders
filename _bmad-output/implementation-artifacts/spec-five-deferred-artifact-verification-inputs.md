---
title: 'Verify contract and decision artifacts consistently'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Verify the contract and decision artifacts from their canonical inputs and make the document checks' existing expectations explicit.

- DW-160. Given a built contract test assembly outside the checkout, when the workspace-lock checks run, then they read the canonical OpenAPI spine and contract notes embedded at build time.
- DW-174. Given a decision document containing a Microsoft Learn citation, when its URLs are checked, then an HTTPS Markdown citation to the exact documentation host is accepted. Endpoint examples continue to require synthetic `.invalid` hostnames.
- DW-192. Given a displayed recursive setup command, when the scaffold policy scans it, then adjacent warning prose can identify the command as prohibited. A warning separated by intervening ordinary prose does not exempt it.
- DW-194. Given C3 or C4 decision rows, when review dates are checked, then at least one row's ISO review date matches the header while earlier rows can keep their historical review dates.
- DW-210. Given a contract-rule documentation or fixture file, when its tests read it, then valid UTF-8 is accepted with or without a byte order mark, and invalid text is diagnosed by path.

</frozen-after-approval>

## Implementation Notes

- Folders started from the fetched main revision 324d8af762ab9bb756b0121f0644c5817b7e59f6. The initial pull covered Folders and all 13 direct repositories. Commit 071e0c5108b60825add872636644f2c6cb605669 records the seven advanced gitlinks.
- The Contracts test project embeds the canonical YAML spine and workspace-lock notes directly from their source paths.
- The Testing test project uses Markdig to resolve Markdown link destinations and compares their source positions with the URL scan. The dependency is scoped to that test project.
- The pending PD10 candidate inventory was regenerated for the changed inputs and reproduced byte for byte.
- Both Debug test projects built with zero warnings and errors. All 125 Testing tests and 753 Contracts tests passed. All 74 CI tooling tests passed in a clean container with PowerShell and jq.

## Review Triage Log

- Low, verified. All six workspace-lock tests passed from a copied build output outside the repository.
- Medium, fixed. Direct links with titles, angle brackets, and reference definitions now qualify as citations.
- Medium, fixed. The Markdown parser distinguishes real links from examples in inline code and fenced blocks, including inside quotes and lists.
- Medium, fixed. The S2 issuer and JWKS discovery cells require synthetic HTTPS URLs even when the example uses link syntax.
- Low, fixed. The scaffold check recognizes tilde and backtick fences between a warning and its displayed command.
- Medium, fixed. An unrelated adjacent warning cannot exempt a displayed recursive command.
- Medium, fixed. A date in a supplementary table cannot satisfy the decision section's current review date.
