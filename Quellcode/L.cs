// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

using KeePass.Forms;

namespace FluentTheme
{
	/// <summary>Sprache des Plugins. Es folgt der Sprache von KeePass: Deutsch, wenn KeePass auf
	/// Deutsch eingestellt ist, sonst Englisch. Die Texte stehen im Code auf Deutsch; T() liefert
	/// bei Englisch die Übersetzung aus der Tabelle unten (fehlt ein Eintrag, bleibt der deutsche Text).</summary>
	internal static class L
	{
		private static bool s_en;

		public static bool English
		{
			get { return s_en; }
		}

		/// <summary>Sprache von KeePass ermitteln (KeePass.Program.Translation.Properties.Iso6391Code).
		/// Ohne Sprachdatei ist KeePass englisch („en“).</summary>
		public static void Init()
		{
			try
			{
				Type prog = typeof(MainForm).Assembly.GetType("KeePass.Program");
				object tr = Get(prog, null, "Translation");
				object props = ((tr != null) ? Get(tr.GetType(), tr, "Properties") : null);
				string code = ((props != null) ? (Get(props.GetType(), props, "Iso6391Code") as string) : null);
				if(code == null) code = string.Empty;
				s_en = !code.StartsWith("de", StringComparison.OrdinalIgnoreCase);
			}
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				s_en = false;
			}
		}

		private static object Get(Type t, object o, string name)
		{
			if(t == null) return null;
			BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic |
				((o == null) ? BindingFlags.Static : BindingFlags.Instance);
			PropertyInfo pi = t.GetProperty(name, bf);
			if(pi != null) return pi.GetValue(o, null);
			FieldInfo fi = t.GetField(name, bf);
			return ((fi != null) ? fi.GetValue(o) : null);
		}

		/// <summary>Text in der aktuellen Sprache.</summary>
		public static string T(string de)
		{
			if(!s_en || (de == null)) return de;
			string en;
			return (s_map.TryGetValue(de, out en) ? en : de);
		}

		/// <summary>Text mit Platzhaltern {0}, {1} … in der aktuellen Sprache.</summary>
		public static string F(string de, params object[] args)
		{
			return string.Format(CultureInfo.CurrentCulture, T(de), args);
		}

		/// <summary>Datumsformat: deutsch TT.MM.JJJJ, englisch JJJJ-MM-TT (eindeutig).</summary>
		public static string DateFormat
		{
			get { return (s_en ? "yyyy-MM-dd" : "dd.MM.yyyy"); }
		}

		private static readonly Dictionary<string, string> s_map = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			// Menü Fluent-Design
			{ "Fluent-Design", "Fluent Design" },
			{ "Wie Windows (automatisch)", "Like Windows (automatic)" },
			{ "Hell", "Light" },
			{ "Dunkel", "Dark" },
			{ "Akzentfarbe", "Accent color" },
			{ "Windows-Akzentfarbe (Standard)", "Windows accent color (default)" },
			{ "Blau (wie Fensterpositionen)", "Blue" },
			{ "Dunkelblau", "Dark blue" },
			{ "Türkis", "Teal" },
			{ "Grün", "Green" },
			{ "Lila", "Purple" },
			{ "Rot", "Red" },
			{ "Orange", "Orange" },
			{ "Eigene Farbe …", "Custom color …" },
			{ "Zeilenhöhe", "Row height" },
			{ "Kompakt", "Compact" },
			{ "Normal", "Normal" },
			{ "Groß", "Large" },
			{ "Moderne Detailansicht", "Modern entry details" },
			{ "Höhe der Detailansicht automatisch anpassen", "Adjust height of entry details automatically" },
			{ "Angezeigtes Passwort wieder verbergen", "Hide revealed password again" },
			{ "Nie", "Never" },
			{ "Nach {0} Sekunden", "After {0} seconds" },
			{ " (Standard)", " (default)" },
			{ "Hover-Effekt in der Eintragsliste", "Hover effect in the entry list" },
			{ "Ablaufende Einträge hervorheben", "Highlight expiring entries" },
			{ "Nur abgelaufene", "Expired only" },
			{ "Bis {0} Tage vorher", "Up to {0} days before" },
			{ "Moderne Symbole in der Symbolleiste", "Modern toolbar icons" },
			{ "Version in der Statusleiste", "Version in the status bar" },
			{ "Fehlerbericht kopieren", "Copy error report" },
			{ "Der Fehlerbericht wurde in die Zwischenablage kopiert.", "The error report has been copied to the clipboard." },
			{ "Info über Fluent Theme …", "About Fluent Theme …" },
			{ "Info über Fluent Theme", "About Fluent Theme" },

			// Kopfzeilen von Gruppenbaum und Eintragsliste
			{ "Gruppen", "Groups" },
			{ "Einträge", "Entries" },
			{ "Tag „{0}“", "Tag “{0}”" },
			{ "{0} Eintrag", "{0} entry" },
			{ "{0} Einträge", "{0} entries" },
			{ "{0} ausgewählt", "{0} selected" },
			{ "Tag: {0}", "Tag: {0}" },

			// Ablauf-Badges
			{ "Abgelaufen", "Expired" },
			{ "heute", "today" },
			{ "morgen", "tomorrow" },
			{ "in {0} Tagen", "in {0} days" },

			// Detailansicht
			{ "Kopiert · Zwischenablage wird nach {0} s geleert", "Copied · clipboard will be cleared in {0} s" },
			{ "Kopiert · wird nach {0} s geleert", "Copied · cleared in {0} s" },
			{ "Kopiert", "Copied" },
			{ "Detailansicht konnte nicht gezeichnet werden (Extras › Fluent-Design › Fehlerbericht kopieren).",
				"Entry details could not be drawn (Tools › Fluent Design › Copy error report)." },
			{ "{0} Einträge ausgewählt", "{0} entries selected" },
			{ "Kein Eintrag ausgewählt", "No entry selected" },
			{ "Alle Einträge mit dem Tag „{0}“ anzeigen", "Show all entries with the tag “{0}”" },
			{ "(ohne Titel)", "(no title)" },
			{ "Anmeldedaten", "Credentials" },
			{ "Benutzername", "User name" },
			{ "Passwort", "Password" },
			{ "URL", "URL" },
			{ "Benutzername kopieren", "Copy user name" },
			{ "Passwort verbergen", "Hide password" },
			{ "Passwort anzeigen", "Show password" },
			{ "Passwort kopieren", "Copy password" },
			{ "URL öffnen", "Open URL" },
			{ "URL kopieren", "Copy URL" },
			{ "Schwach", "Weak" },
			{ "Mittel", "Medium" },
			{ "Stark", "Strong" },
			{ "Sehr stark", "Very strong" },
			{ "Einmalcode", "One-time code" },
			{ "Einmalcode kopieren", "Copy one-time code" },
			{ "Klicken zum Kopieren", "Click to copy" },
			{ "Weitere Felder", "Other fields" },
			{ "Keine weiteren Felder", "No other fields" },
			{ "Verbergen", "Hide" },
			{ "Anzeigen", "Show" },
			{ "Öffnen", "Open" },
			{ "{0} kopieren", "Copy {0}" },
			{ "Notizen und Info", "Notes and info" },
			{ "Keine Notizen", "No notes" },
			{ "Notizen kopieren", "Copy notes" },
			{ "Weniger anzeigen", "Show less" },
			{ "Mehr anzeigen", "Show more" },
			{ "Erstellt", "Created" },
			{ "Geändert", "Modified" },
			{ "nie", "never" },
			{ " (abgelaufen)", " (expired)" },
			{ "Läuft ab", "Expires" },
			{ "Anhänge", "Attachments" },
			{ "Speichern unter …", "Save as …" },
			{ "Öffnen: {0}", "Open: {0}" },
			{ "Ziehen: Breite ändern · Doppelklick: gleich breit", "Drag: change width · Double-click: equal widths" },

			// Anhang speichern
			{ "Anhang speichern", "Save attachment" },
			{ "Alle Dateien (*.*)|*.*", "All files (*.*)|*.*" },
			{ "Der Anhang konnte nicht gespeichert werden:", "The attachment could not be saved:" },

			// Info-Fenster
			{ "Info über {0}", "About {0}" },
			{ "Version {0}", "Version {0}" },
			{ "Entwickler", "Developer" },
			{ "Farbschema", "Color scheme" },
			{ "Plugin", "Plugin" },
			{ "Webseite", "Website" },
			{ "Plugin-Ordner", "Plugin folder" },
			{ "Schließen", "Close" },
			{ "hell", "light" },
			{ "dunkel", "dark" },
			{ " (folgt Windows)", " (follows Windows)" },
			{ " (fest eingestellt)", " (fixed)" }
		};
	}
}
