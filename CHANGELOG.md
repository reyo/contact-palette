# Changelog

## 0.1.6 — choose a contact field to copy

- Request Right arrow as the shortcut to a field chooser, also available in the context menu.
- Select a name, email, phone or other populated contact field and press Enter to copy.
- Validate the shortcut and populated chooser through the registered COM interface.

## 0.1.5 — Thunderbird photo data URI fix

- Unescape PHOTO values before reading embedded image data; handle Thunderbird/CardDAV `base64\,` separators.
- Preserve the encoding of the selected first PHOTO when multiple PHOTO properties exist.
- Add regression coverage and decode every local photo in the opt-in Windows test.

## 0.1.4 — local command cleanup

- Use stable provider/search command identifiers and support cached command lookup.
- Hide the extension's non-interactive application entry from the app list.
- Restart only Command Palette after local updates so commands from old package versions are not retained in memory.
- Decode real photo pixels in the registered-instance test, rather than checking stream size alone.

## 0.1.2 — local photo interoperability fix

- Pass contact photos through native WinRT streams so the Command Palette host can read them across processes.
- Test a registered, shared extension without disposing the host's instance.

## 0.1.1 — contact details preview

- Open a right-hand details panel automatically when selecting a contact.
- Show all emails/phones, organization, job title, postal address, website, birthday and notes when available.
- Show embedded vCard 3/4 photos and legacy Thunderbird Photos files, with a person icon fallback.
- Keep photos in memory, ignore remote image URLs and avoid a separate photo cache.
- Extend synthetic coverage to 45 assertions, including photos and additional contact fields.

## 0.1.0 — local development build

- Compile and publish x64 and ARM64 with .NET 10, without requiring full Visual Studio.
- Use stable Windows SDK metadata, NuGet lock files and deterministic compilation.
- Build and validate unsigned MSIX packages and a combined bundle with MakeAppx.
- Load contacts in the background; refresh stale pages when reopened.
- Limit SQLite lock waits, enforce query-only mode and report unsupported schemas.
- Decode escaped Unicode in Thunderbird preferences.
- Parse quoted vCard parameters and all structured-name components.
- Prevent queries containing text and digits from matching unrelated phone numbers.
- Add 33 synthetic assertions and an x64 process/COM/WinRT integration smoke test.
- Add local installation, manual Store candidate builds and a signed-package WinGet manifest generator.

No public release has been made. See VALIDATION.md for evidence and remaining checks.
