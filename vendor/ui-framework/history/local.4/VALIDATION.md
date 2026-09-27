# UI-framework local package validation

## Current package

`0.1.0-alpha.3-local.1` packages framework revision
`2db7a9a6cdde0fa52c458a9206219348f4ae0bcd`. It is a local integration
candidate, not a NuGet.org release or a performance-cleared version.

The framework revision avoids redundant native subtree detachments during
forward keyed row moves. Its focused regression preserves native identity,
inherited properties, final order, and the existing fallback for other edits.
Framework validation reported 98 tests, 15 visual checks, and 21 stress
assertions passing.

The app uses the exact WPF pin `[0.1.0-alpha.3-local.1]`; core resolves
transitively to the same version. Three native `Dock` enum references are fully
qualified because the package's static `UI.Dock` API otherwise shadows them.
Existing native layout adapters remain in place, and application-owned
experimental COM cleanup remains default-off.

## Consumer package comparison

The authoritative comparison is:

`C:\Users\iotero\Dev\personal\UI Framework\artifacts\consumer-package-alpha.3-local.1-samples15\summary.json`

It compares `0.1.0-alpha.2-local.3` with `0.1.0-alpha.3-local.1` in isolated
consumer snapshots at revision
`3c5aea06eaac8b7f2587691c13b748c29c7a0c58`. The consumer worktree was dirty
because identical integration fixes were applied to both sides. SDK 10.0.401
was used, cleanup was disabled, one warmup pair per scenario was discarded, and
15 measured pairs per scenario were retained.

All 28 checks passed:

| Scenario | Startup time | Mount time | Update time | Process time |
| --- | ---: | ---: | ---: | ---: |
| Window | +4.01% | +2.10% | -2.45% | -0.29% |
| Splitter | +0.08% | -0.30% | +1.46% | -1.04% |
| Full tree | -0.13% | +0.55% | -0.96% | -0.81% |
| Virtualized tree | +0.83% | +1.90% | -3.93% | +0.51% |

Startup, mount, and update allocations also passed the unchanged 2% allowance
for every scenario. Full-tree realization remains alongside virtualized-tree
measurement. The comparison does not claim reduced component work because the
consumer harness does not instrument component build counts.

Package hashes:

| Package | SHA256 |
| --- | --- |
| `SignalNotNoise.UI.0.1.0-alpha.3-local.1.nupkg` | `B338FD1FFD03529F07B2D8F70A37B6477A43240EFFB7C8F1A657F805C02D5A7E` |
| `SignalNotNoise.UI.Wpf.0.1.0-alpha.3-local.1.nupkg` | `0FC3B2CEB0BFBD55EF83C2EADF6FEE4B0693BF7D411F61D21613B4354580E127` |

## Publication status

Public NuGet publication remains paused. The separate normal editor diagnostic
uses 1,000 WPF text editors, 50 operations, and 10,000 text writes under
ordinary cleanup. Three completed runs took 29.19-30.21 seconds and reproduced
late-update stalls:

| Run | Total | Step 40 | Step 50 |
| --- | ---: | ---: | ---: |
| 1 | 29.85 s | 5.07 s | 18.05 s |
| 2 | 29.19 s | 2.01 s | 14.12 s |
| 3 | 30.21 s | 0.87 s | 16.41 s |

Evidence:

`C:\Users\iotero\Dev\personal\UI Framework\artifacts\editor-timeout-normal-2026-09-25-rerun-2\`

These diagnostic processes completed successfully, but their several-second
late updates leave the ordinary editor release gate unresolved. Passing the
consumer comparison does not clear that blocker, authorize cleanup enablement,
or justify advancing the accepted performance baseline.

## Rollback

The prior `0.1.0-alpha.2-local.3` packages remain beside the candidate, and
their metadata is preserved under `history/local.3`. Restore the prior exact
package pin to roll back; do not replace package bytes under an existing
version.
