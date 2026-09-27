# Local UI-framework packages

These MIT-licensed packages are pinned migration inputs. The current version is
`0.1.0-alpha.3-local.1`, built from framework revision
`2db7a9a6cdde0fa52c458a9206219348f4ae0bcd`. `provenance.json` records the
source revision, validation status, and SHA256 hashes. `NuGet.Config` restores
from this directory without a separate framework checkout.

The application pins `SignalNotNoise.UI.Wpf` exactly to
`[0.1.0-alpha.3-local.1]`; the core package resolves transitively to the same
version. The package adoption retains the application's native layout adapters
and keeps its experimental COM cleanup policy default-off.

The 15-sample paired consumer comparison against `0.1.0-alpha.2-local.3` passed
all 28 startup, mount, update, allocation, and process checks. This does not
authorize publication: ordinary-cleanup editor diagnostics still show
multi-second late-update stalls, including 14.12-18.05 second final updates.
See `VALIDATION.md` for the evidence and release status.

The previous `0.1.0-alpha.2-local.3` metadata is preserved under
`history/local.3`, and both old packages remain in this directory for rollback.
Preserve history, provenance, and `LICENSE`. Never overwrite an existing package
version with different bytes.
