# Local UI-framework packages

These MIT-licensed packages are pinned migration inputs. The current version is
0.1.0-alpha.2-local.3; provenance.json records its source revision and SHA256 hashes.
NuGet.Config restores from this directory without a separate framework checkout.

The current package fixes rich Button content and passes functional/package checks.
Its performance gate FAILED themed update allocations (+7.81%, allowed 6%); themed
update time is +9.87%. See VALIDATION.md and performance.json. Local integration only;
no public publication or performance-cleared release is claimed.

Historical local.2 validation, including its initial failure and successful repeat,
is preserved under history/local.2. Superseded binary packages are removed only after
replacement restore/build/tests succeed. Preserve history, provenance and LICENSE.
Never overwrite an existing package version with different bytes.
