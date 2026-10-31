Fluent Theme for KeePass 2.x
============================

Author: Günther Schoder · www.westmc002.de
© 2026 Günther Schoder


What is it?
-----------
Fluent Theme is a plugin for KeePass 2.x that gives KeePass a modern
Windows 11 look. The familiar layout stays the same: group tree on the
left, entry list on the right, entry details below.


Installation
------------
1. Close KeePass.
2. Copy FluentTheme.dll into the "Plugins" folder of KeePass,
   e.g. C:\Program Files (x86)\KeePass\Plugins
   (administrator rights required).
3. Start KeePass.

Uninstall: close KeePass and delete FluentTheme.dll from the
"Plugins" folder.

Building from source: run Erstellen.bat in the project folder.
It compiles the plugin, backs up the project to Backup, copies the
DLL to the Plugins folder and restarts KeePass.


Features
--------
- Windows 11 colors, light and dark mode (follows Windows or fixed)
- Windows accent color or a custom accent color
- Modern menus, toolbar with colored Fluent icons, tabs and slim
  scroll bars
- Restyled KeePass dialogs (edit entry, options etc.): content on a
  card, consistent input fields with an accent line
- Group tree and entry list with rounded selection, accent bar and
  hover effect; adjustable row height
- Expiring entries are marked in the list: red badge "Expired",
  orange badge "in x days" (time span adjustable, default 14 days)
- Entry details as cards:
  - credentials with copy, show and open buttons
  - one-time code (TOTP) with countdown and copy button for entries
    with a 2FA secret (KeePass fields "TimeOtp-Secret-..." or an "otp"
    field in KeePassXC/KeeOtp format). Two-factor login itself is still
    set up in KeePass or a TOTP plugin.
  - clickable tags: shows all entries with that tag
    (clicking a group removes the filter again)
  - password strength as a bar
  - other fields (e.g. "URL 2"), links are clickable
  - notes with copy button, long notes can be expanded
  - open or save attachments with one click
  - created / modified / expires
- After copying, "Copied" is shown briefly together with the number of
  seconds after which KeePass clears the clipboard
- A revealed password is hidden again after an adjustable time
  (default: 15 seconds)
- The height of the entry details adapts to the entry automatically
- About window: click "Fluent Theme x.y" at the bottom right of the
  status bar (or Tools > Fluent Design > About Fluent Theme)


Settings
--------
All settings are in the menu Tools > Fluent Design:

- Like Windows / Light / Dark
- Accent color (default: Windows accent color)
- Row height of the entry list (24, 28 or 32 pixels)
- Modern entry details (off = standard KeePass view)
- Adjust height of entry details automatically
- Hide revealed password again (10, 15, 30, 60 seconds or never)
- Hover effect in the entry list and group tree
- Highlight expiring entries (7, 14, 30 days before or expired only)
- Modern toolbar icons
- Version in the status bar (show or hide)
- Copy error report (only in case of problems)
- About Fluent Theme

KeePass stores the settings in its configuration file
(KeePass.config.xml); keys start with "FluentTheme.".


Notes
-----
- Language: the plugin follows the language of KeePass. If KeePass is
  set to German, it appears in German, otherwise in English.
- The plugin does not change any data in the database, only the
  appearance.
- How long KeePass keeps copied data in the clipboard is set in KeePass
  itself: Tools > Options > Security.
- In case of problems: Tools > Fluent Design > Copy error report and
  send the text to Günther Schoder.
