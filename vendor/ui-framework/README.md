# Local UI-framework packages

The current immutable integration version is `0.1.0-alpha.3-local.2`, built
from the dirty working tree at framework revision
`2db7a9a6cdde0fa52c458a9206219348f4ae0bcd`. The app pins
`SignalNotNoise.UI.Wpf` exactly to `[0.1.0-alpha.3-local.2]`; core resolves
transitively to the same version.

This package adds `WpfComCleanupPolicy`. The app owns one instance from startup
through ordered shutdown, replacing its duplicated experimental implementation.
The 1,000-editor diagnostic reduced final updates from 12.73-13.38 seconds to
0.19-0.22 seconds. The seven-sample framework gate passed all 32 checks and the
15-sample consumer comparison passed all 28 checks.

The corresponding public release, `0.1.0-alpha.3`, was published to NuGet.org on September 26, 2026 from committed framework source `520173d`. This app still pins the validated local.2 package; its exact original dirty-source provenance remains historical evidence. No package bytes or package pin changed in this documentation audit. `provenance.json` records hashes and evidence; `VALIDATION.md`
describes the completed public release and the current local integration pin. Prior metadata is preserved under
`history/local.4`, and older packages remain available for rollback. Never
overwrite an existing package version with different bytes.
