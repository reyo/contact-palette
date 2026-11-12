# Contact Palette

Search local Mozilla Thunderbird contacts from Microsoft PowerToys Command Palette.
An independent MIT-licensed project. Not affiliated with Mozilla or Microsoft.

**[Visit the project website](https://reyo.github.io/contact-palette/)** · [Privacy policy](https://reyo.github.io/contact-palette/privacy.html)

## Local preview 0.1.6

- Automatically discovers standard Thunderbird profiles and local/CardDAV SQLite books.
- Searches names, email addresses and phone numbers. Name matching ignores accents.
- Enter copies the first email, or the phone/name when no email is available.
- Right arrow requests a field chooser for the selected contact: Up/Down selects a field, Enter copies it, Escape goes back. It is also available under Ctrl+K as Choose field to copy.
- Context actions copy other addresses and phones, or open a mailto link in your default mail app.
- Selecting a contact opens a right-hand details pane with all contact channels and available organization, job title, address, website, birthday and notes.
- Shows embedded vCard photos and local Thunderbird Photos files; no remote photo downloads or separate photo cache.
- Reads local data with SQLite read-only, query-only connections. No telemetry or server calls.
- Loads in the background and refreshes manually or when the page/query is revisited after 20 seconds.

## Try it locally on Windows 11

PowerToys with Command Palette and Thunderbird with local address books are required.
Install the build tools using Scoop:

```powershell
scoop install versions/dotnet-sdk-lts windows-sdk-buildtools
```

Open a new PowerShell after installing, enable **Developer Mode** in Windows Settings,
and run this from the project directory:

```powershell
.\install-local.ps1
```

Then open Command Palette, run **Reload Command Palette extensions**, and choose
**Search Thunderbird contacts**. Enter copies a contact value; the context menu offers
copy/compose actions. This is a local development installation, not a Store release.

Keep the generated `work/build-...` directory while the extension is installed.
To remove just this test extension:

```powershell
Get-AppxPackage -Name ContactPalette.Development | Remove-AppxPackage
```

You can disable Developer Mode after finishing local development; future unsigned
registrations will require enabling it again.

## Build and test without installing

```powershell
dotnet run --project tests/contact-palette-tests -c Release
.\build-extension.ps1 -Development
```

The script publishes self-contained x64 and ARM64 applications, validates/packages
with MakeAppx, and writes unsigned `.msix` files plus a `.msixbundle` to `artifacts/`.
It never signs or publishes. Full Visual Studio is optional for this path.
For Visual Studio deployment, install Windows/MSIX workloads and a Windows 11 SDK.

The 47 synthetic assertions cover vCard, photos (including escaped data-URI commas), additional contact fields, Unicode preferences, profile discovery, read-only
integrity, WAL snapshots, locks, corrupt/unsupported databases and search ranking.
They never use real contact data. A separate opt-in diagnostic reads your own books
and prints counts only:

```powershell
dotnet run --project tests/contact-palette-tests -c Release -- --local-smoke
```

An x64 Windows integration test starts the published COM server, activates it through
WinRT, reads the command/list page interfaces and verifies clean shutdown:

```powershell
dotnet run --project tests/contact-palette-windows-tests -c Release -p:Platform=x64 -p:EnableMsixTooling=false -- <published-contact-palette.exe>
```

If the extension is already registered, use `-- --installed` instead of the executable
path. That mode checks the shared instance and image streams without shutting it down.

## Structure

- `src/contact-palette-core`: portable .NET 10 discovery, SQLite, vCard and search.
- `src/contact-palette`: out-of-process COM extension using the official CmdPal SDK.
- `tests/`: synthetic integration tests and Windows COM smoke test.
- `packaging/`: local-only publication preparation tools.
- `.github/workflows/`: CI and manually triggered unsigned Store candidate builds.

NuGet dependencies are pinned in `packages.lock.json`; use `dotnet restore --locked-mode`
when checking dependency reproducibility. Both build modes retain the same dependency graph.

## Limitations

- Windows UI integration, clipboard and mailto must be checked on the target machine.
- Profile photos must already be local; remote-only PHOTO URLs show the person icon instead.
- ARM64 is cross-compiled; execution requires an ARM64 machine.
- LDAP, mailing-list expansion and arbitrary portable profile locations are unsupported.
- Only contacts already synchronized locally by Thunderbird can be searched.
- There is no continuous file watcher. The interface is currently English only.
- No settings UI for selecting profiles or changing the primary contact action yet.

## Publication

Source repository: [reyo/contact-palette](https://github.com/reyo/contact-palette).
This is a development preview; Microsoft Store and WinGet publication are pending.
Store identity, signing and screenshots must be supplied before distributing a production
installer. See STORE-RELEASE.md, STORE-LISTING.md and VALIDATION.md.

Contact Palette uses `runFullTrust` to read profile files outside its package. Omitting
`internetClient` does not restrict a full-trust desktop process; the offline behavior
comes from the application code. Choosing mailto deliberately launches your mail app.

See PRIVACY.md, SECURITY.md, LICENSE and THIRD-PARTY-NOTICES.txt.

## Official references

- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension
- https://source-docs.thunderbird.net/en/latest/addrbook/directories.html
