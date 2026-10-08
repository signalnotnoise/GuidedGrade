# UI-framework local package validation

## Package and lifecycle

`0.1.0-alpha.3-local.2` adds the package-level `WpfComCleanupPolicy`. The app
creates one owner before `MainWindow`, reports failures without opening a modal
dialog, disposes native/framework owners before final cleanup, and returns a
nonzero exit code if cleanup failed.

The native lifecycle fixture passed 49 assertions: all 1,100 counted COM objects
were released on their owning STA, none remained alive, and no wrong-thread
release occurred. Framework tests cover one-owner enforcement, failure recovery,
selection, undo, and enabled input-method support.

## Editor diagnosis

Three fresh runtime-default and policy processes retained 1,000 WPF TextBoxes,
50 updates, 10,000 checked text writes, input methods, and read-only/undo
changes.

| Mode | Total range | Final-update range |
| --- | ---: | ---: |
| Runtime default | 19.40-21.21 s | 12.73-13.38 s |
| Packaged policy | 7.37-7.67 s | 0.19-0.22 s |

The reproduced late setter stall is resolved under the supported application
lifecycle. Visible keyboard input, installed IME composition, and screen-reader
acceptance remain manual release checks.

## Framework release gate

Seven alternating measured processes per side/scenario plus discarded warmups
compared the working tree with published baseline
`78c88fb901c202e3c2e49b6de300d1ce2369e00b`. Both editor revisions used the
exact candidate cleanup policy source. All 32 checks passed and the report is
marked `releaseEligible: true`.

| Scenario | Mount time | Update time | Mount bytes | Update bytes |
| --- | ---: | ---: | ---: | ---: |
| Full list | +0.51% | -33.47% | -1.01% | -25.97% |
| Virtualized | +1.07% | -0.04% | -1.61% | -5.12% |
| Themed full list | -2.75% | -60.08% | -2.72% | -57.10% |
| Layout editors | +1.89% | -2.08% | +0.13% | -3.29% |

Component build, mount, and unmount work matched in every scenario.

## Consumer comparison

Fifteen measured pairs per scenario compared `0.1.0-alpha.3-local.1` with
`0.1.0-alpha.3-local.2`. Both snapshots used the exact candidate policy source.
All 28 startup, mount, update, allocation, and process checks passed. The app
Release build completed with zero warnings and all 189 tests passed.

| Scenario | Startup | Mount | Update | Process |
| --- | ---: | ---: | ---: | ---: |
| Window | -1.96% | +4.37% | -1.37% | -0.80% |
| Splitter | +1.22% | +5.19% | -2.61% | -0.10% |
| Full tree | -0.97% | -1.60% | +1.53% | +0.05% |
| Virtualized tree | -0.06% | -0.91% | -2.02% | -1.56% |

## Release status and rollback

The technical editor-performance blocker is cleared. The corresponding public
release `0.1.0-alpha.3` was published on September 26, 2026 from committed source
`520173d` through Publish NuGet run 6:
https://github.com/signalnotnoise/ui-framework/actions/runs/36231322862
The framework release record states that visible typing, installed IME,
screen-reader and repeated open/close acceptance passed before publication.
Both NuGet package indices were rechecked on October 7, 2026.

This app still pins `[0.1.0-alpha.3-local.2]`. Its package bytes and recorded
source provenance are unchanged. Moving the app to the public package is a
separate dependency change requiring restore, app tests and paired comparison;
publication alone does not establish that this app has migrated.

Rollback metadata for `0.1.0-alpha.3-local.1` is under `history/local.4`; the
older exact packages remain beside this candidate.

October 7 workspace refinement: the pinned package lacks declarative tab/link styling and explicit vertical text alignment used by toolbar labels. Application-owned native adapters provide these presentations; NavigationTab uses an attached selected property independent of framework identity tags. Scoped centered ContentPresenter button styling preserves rich content. Reported to the authorized CUI task. No package bytes or performance claims changed.
