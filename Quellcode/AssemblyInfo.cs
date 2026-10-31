// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Fluent Theme")]
[assembly: AssemblyDescription("Modernes Windows-11-Design für KeePass 2.x: Hell/Dunkel, Akzentfarbe, kompakte Liste und Detailansicht mit eigenen Feldern.")]
[assembly: AssemblyConfiguration("")]
// Ausnahme zur globalen Vorgabe „kein Firmenfeld“: KeePass zeigt AssemblyCompany in
// Extras > Plugins als „Autor“ an – ohne diesen Eintrag bleibt die Spalte leer.
[assembly: AssemblyCompany("Günther Schoder")]
[assembly: AssemblyProduct("KeePass Plugin")]
[assembly: AssemblyCopyright("© 2026 Günther Schoder")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
// Versionsnummer wird von Erstellen.bat in VersionInfo.cs erzeugt (Haupt.Neben.Jahr.MonatTag, dazu AppInfo.Version).