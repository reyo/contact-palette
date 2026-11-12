# Publication preparation

The source repository is reyo/contact-palette. Store and WinGet publication are pending;
no Partner Center product registration has been performed as part of this project.

## GitHub (owner: reyo)

The source is MIT licensed. Before creating a public repository, review the source,
LICENSE, privacy/security policies, THIRD-PARTY-NOTICES, and VALIDATION.md. Choose the
repository name (suggestion: contact-palette), default branch, support contact and
public privacy policy URL. Never upload `work/`, `bin/`, `obj/`, real Thunderbird
profiles, local validation output, certificates or credentials.

CI only tests/builds and uploads workflow artifacts. There is deliberately no action
that creates a public release, submits a WinGet PR or sends anything to Partner Center.

After explicit publication approval, create/push the repository and attach tested,
signed packages to a reviewed release. A development publisher identity must not ship.

## Microsoft Store

1. Register a Partner Center developer account and reserve the product name.
2. Record the exact Identity Name, Publisher and Publisher Display Name.
3. Build a candidate without changing the development source manifest:

```powershell
.\build-extension.ps1 -IdentityName '<Partner Center name>' -Publisher '<Partner Center publisher>' -PublisherDisplayName '<publisher display name>' -Version 0.1.6.0
```

The script uses those values only in generated manifests. It creates an unsigned
self-contained x64/ARM64 bundle for Store ingestion. Its last version component must
be zero. A manual GitHub workflow can perform the same build using repository variables
STORE_IDENTITY_NAME, STORE_PUBLISHER and STORE_PUBLISHER_DISPLAY_NAME.

4. Test installation/activation, refresh, accented names, clipboard, mailto, missing
   profiles and an active Thunderbird instance. Execute the ARM64 build on ARM64 hardware.
5. Run the Windows App Certification Kit on the final Store candidate. MakeAppx validation
   checks package structure and manifest rules; it does not replace WACK or Store certification.
6. Supply actual installed-app screenshots using synthetic contacts, support details,
   a public privacy policy URL, age rating and the listing in STORE-LISTING.md.
7. Explain runFullTrust: read the current user's local Thunderbird SQLite files and provide
   clipboard/mailto actions. No contact upload, telemetry or Thunderbird add-on is needed.
8. Only with owner approval and Partner Center access: upload and submit the reviewed build.

## WinGet

Direct MSIX distribution needs a certificate trusted by Windows. An unsigned development
MSIX is not suitable for the community repository. Store publication can be completed first;
the Microsoft Store source and the community WinGet source are separate distribution paths.

Once signed x64 and ARM64 MSIX files and permanent HTTPS release URLs exist, run
`packaging/new-winget-manifests.ps1`. Its required parameters are PackageIdentifier,
Publisher, RepositoryUrl, PackageFamilyName, X64Package, Arm64Package, X64Url and Arm64Url.
It rejects unsigned/untrusted/development packages, checks matching package identities,
and calculates package and signature SHA256 hashes. It adds the discoverability tag
`windows-commandpalette-extension`. The runtime is bundled; Windows App SDK is not used.

Validate generated files with `winget validate --manifest <directory>`, test installation
on clean Windows, review metadata and only then submit a PR to microsoft/winget-pkgs with
owner approval. The generator does not submit anything or validate remote URL contents.

## Gallery

After a public installation source is live, request a listing in the Command Palette
Extension Gallery. This is a separate curated directory, not an installer host.

Official references (checked 2026-11-12):
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension-store
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension-winget
- https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/schema/1.12.0/installer.md



