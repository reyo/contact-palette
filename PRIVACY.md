# Privacy policy

Contact Palette searches Thunderbird address books locally on your PC.

- It reads the current Windows user's Thunderbird profile files using SQLite read-only
  and query-only connections. It keeps parsed contacts in memory without a separate index.
- It does not upload, sync or log contacts to remote services and contains no telemetry,
  advertising SDK or remote API calls.
- Choosing a copy action puts only the selected value on the Windows clipboard. Windows
  clipboard history or cloud clipboard, if enabled by you, follows your Windows settings.
- Choosing Compose email passes a mailto link to your default mail app. That app controls
  drafting, sending and its own network access; Contact Palette does not send the email.
- Contact changes/deletion and CardDAV synchronization remain under Thunderbird's control.
- Diagnostic tests print counts only. Never share real databases or contact screenshots
  in issue reports. Public screenshots should use synthetic contacts.

The extension requests runFullTrust to read files outside its MSIX package. It does not
request internetClient, but that omission is not a network restriction for a full-trust
process. The offline behavior is implemented by the application code.

This policy must be reviewed before adding network features or data collection.
