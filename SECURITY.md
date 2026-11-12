# Security policy

Contact Palette never modifies Thunderbird databases and never stores a secondary copy of contacts.

The application discovers address books only in Thunderbird's own profile locations and only opens database files matching known address book names or the names declared in `prefs.js`. All SQLite access uses read-only mode.

Do not include genuine contacts, private profile paths or raw Thunderbird databases in issue reports. Use a synthetic fixture instead. If GitHub private vulnerability reporting is enabled for this repository, use its Security tab for private reports. Otherwise open an issue asking the maintainer for a private reporting channel without disclosing vulnerability details.

This initial version has not undergone independent security review. Public release requires Windows testing, dependency review and signed MSIX packaging.
