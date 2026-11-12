# Validation record — 2026-11-12

## Current result

Version 0.1.6.0 is registered locally on the development Windows machine.
Source publication is tracked at reyo/contact-palette. No WinGet or Store submission has been made.
The owner confirmed that the original local search build works in Command Palette.

## Passed

- Original source ZIP recovered from the owner's Downloads folder; it was not overwritten.
- .NET 10.0.404 SDK installed through Scoop; Windows SDK packaging tools installed through Scoop.
- Windows SDK reference changed from preview to stable 10.0.26100.87.
- 47 synthetic assertions: profile discovery, Unicode preferences, vCard names/fields/photos,
  search, database byte/timestamp integrity, committed/uncommitted WAL changes, bounded
  lock waits, corrupt databases, missing databases and unsupported schemas.
- Release compilation and self-contained publication for x64 and ARM64.
- MakeAppx package/manifest validation for both architectures; combined MSIX bundle creation.
- Locked NuGet restore succeeds. No known vulnerable direct/transitive packages were reported
  by the configured NuGet audit source at validation time. This is not a security review.
- x64 COM activation, provider, command and list-page interfaces.
- Registered instance reports ShowDetails enabled; populated metadata and decoded local photo
  pixels are readable across the COM boundary. Stable provider/command IDs and command lookup pass.
- Escaped data-URI commas are handled correctly. All discovered local photos passed native
  pixel decoding, and every photo on the first result page passed cross-process decoding.
- One installed Contact Palette package remains. The standalone app entry is hidden and
  the Command Palette host was restarted to remove commands retained from old package versions.
- A fresh unregistered x64 process in the earlier build also exited cleanly on disposal.
  Registered-instance tests deliberately do not dispose the live host's shared extension.
- Opt-in read-only smoke test against an active Thunderbird installation succeeded.
  Contact values, profile paths and database files are not included in this record.
- Store build rejects missing publisher identity; WinGet generator rejects unsigned packages.
- Project XML/JSON and PowerShell script syntax checked.

## Remaining before public distribution

- Visually check the final photo/detail layout, keyboard interaction, clipboard and mailto.
  Earlier automated UI checking was stopped with Escape. The latest popup was not exposed
  by the window automation tool; detail/image behavior was checked through the actual
  registered WinRT/COM interface instead. Host logs confirm one search command after restart.
- Execute the ARM64 application on ARM64 hardware.
- Exercise the GitHub workflows after repository creation; local commands have passed,
  but no GitHub-hosted workflow run has been performed.
- Validate generated WinGet manifests against final trusted, signed packages and real URLs.
  No signing certificate or positive publication test is available yet.
- Register a Partner Center product, supply publisher identity/support/privacy URLs,
  take synthetic-data screenshots, run WACK and complete Store certification.
- Explicit owner approval and appropriate account access are required for publication.

Development packages are unsigned and use a development publisher identity. They are
for local testing, not final public installers.
