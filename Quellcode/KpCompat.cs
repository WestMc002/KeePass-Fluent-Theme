// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

using KeePass.Forms;
using KeePass.Plugins;

using KeePassLib;
using KeePassLib.Security;

namespace FluentTheme
{
	/// <summary>Ruft KeePass-Funktionen per Reflection auf, damit das Plugin mit
	/// möglichst vielen KeePass-2.x-Versionen funktioniert.</summary>
	/// <summary>Ergebnis eines Kopierens über KeePass.</summary>
	internal enum CopyResult
	{
		/// <summary>Kopiert, die Löschfrist von KeePass läuft.</summary>
		Ok,
		/// <summary>Nicht kopiert; der Wert liegt nachweislich nicht in der Zwischenablage.</summary>
		Failed,
		/// <summary>Fehlgeschlagen, und das Leeren ließ sich nicht bestätigen: Der Wert kann noch in
		/// der Zwischenablage liegen.</summary>
		FailedMayRemain
	}

	internal static class KpCompat
	{
		private static Assembly KpAsm
		{
			get { return typeof(MainForm).Assembly; }
		}

		private static bool CanSupply(Type t)
		{
			return ((t == typeof(ProtectedString)) || (t == typeof(string)) || (t == typeof(bool)) ||
				(t == typeof(PwEntry)) || (t == typeof(PwDatabase)) || (t == typeof(IntPtr)) ||
				typeof(Form).IsAssignableFrom(t) || (t == typeof(IWin32Window)));
		}

		private static MethodInfo FindStatic(Type t, string name, Type firstParam)
		{
			if(t == null) return null;
			MethodInfo best = null;
			int bestCount = -1;
			foreach(MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				if(mi.Name != name) continue;
				ParameterInfo[] pars = mi.GetParameters();
				if((pars.Length == 0) || (pars[0].ParameterType != firstParam)) continue;

				bool ok = true;
				foreach(ParameterInfo pi in pars)
				{
					if(!CanSupply(pi.ParameterType) && !pi.IsOptional) { ok = false; break; }
				}
				if(ok && (pars.Length > bestCount))
				{
					best = mi;
					bestCount = pars.Length;
				}
			}
			return best;
		}

		private static MethodInfo FindExact(Type t, string name, Type[] sig)
		{
			if(t == null) return null;
			try
			{
				return t.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, sig, null);
			}
			catch(AmbiguousMatchException) { return null; }
		}

		private static object[] BuildArgs(MethodInfo mi, IPluginHost host, ProtectedString ps,
			string str, PwEntry pe)
		{
			ParameterInfo[] pars = mi.GetParameters();
			object[] args = new object[pars.Length];
			for(int i = 0; i < pars.Length; ++i)
			{
				Type t = pars[i].ParameterType;
				if(t == typeof(ProtectedString)) args[i] = ps;
				else if(t == typeof(string)) args[i] = str;
				else if(t == typeof(bool)) args[i] = true;
				else if(t == typeof(PwEntry)) args[i] = pe;
				else if(t == typeof(PwDatabase)) args[i] = host.Database;
				else if(t == typeof(IntPtr)) args[i] = host.MainWindow.Handle;
				else if(typeof(Form).IsAssignableFrom(t) || (t == typeof(IWin32Window))) args[i] = host.MainWindow;
				else if(pars[i].IsOptional) args[i] = pars[i].DefaultValue;
				else args[i] = null;
			}
			return args;
		}

		// Kopieren, Löschfrist und Öffnen von Adressen rufen KeePass mit festen Signaturen auf (Stand
		// KeePass 2.62, gegen das gebaut wird) – genau so, wie es das Hauptfenster von KeePass selbst
		// tut. Jeder Aufruf steht in einer eigenen, nicht eingebetteten Methode: Fehlt die Methode in
		// einer künftigen KeePass-Version, scheitert nur dieser Aufruf und wird hier abgefangen.

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static bool KpCopy(IPluginHost host, ProtectedString ps, PwEntry pe)
		{
			return KeePass.Util.ClipboardUtil.CopyAndMinimize(ps, true, host.MainWindow, pe, host.Database);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void KpStartCountdown(IPluginHost host)
		{
			host.MainWindow.StartClipboardCountdown();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void KpClearClipboard()
		{
			KeePass.Util.ClipboardUtil.Clear();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void KpOpenEntryUrl(PwEntry pe)
		{
			KeePass.Util.WinUtil.OpenEntryUrl(pe);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void KpOpenUrl(string url, PwEntry pe)
		{
			KeePass.Util.WinUtil.OpenUrl(url, pe);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static int KpEstimateBits(char[] pw)
		{
			return (int)KeePassLib.Cryptography.QualityEstimation.EstimatePasswordBits(pw);
		}

		/// <summary>Zwischenablage leeren und nachprüfen. true nur, wenn danach nachweislich kein Text
		/// mehr darin liegt.</summary>
		private static bool ClearClipboardVerified()
		{
			try { KpClearClipboard(); }
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				try { Clipboard.Clear(); }
				catch(Exception ex2) { FluentThemeExt.Report(ex2); }
			}
			try { return !Clipboard.ContainsText(); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return false;
		}

		/// <summary>Kopiert wie KeePass selbst (Platzhalter, Auto-Löschen der Zwischenablage).
		/// Das Plugin schreibt nie selbst in die Zwischenablage – ein Geheimnis läge dort sonst ohne
		/// Löschfrist. Das Ergebnis sagt auch, ob der Wert nach einem Fehler noch dort liegen kann.</summary>
		public static CopyResult Copy(IPluginHost host, ProtectedString ps, PwEntry pe)
		{
			if(ps == null) return CopyResult.Failed;
			try
			{
				// false: KeePass hat nichts in die Zwischenablage geschrieben
				if(!KpCopy(host, ps, pe)) return CopyResult.Failed;
			}
			catch(Exception ex)
			{
				// Unklar, wie weit KeePass gekommen ist – vorsichtshalber leeren
				FluentThemeExt.Report(ex);
				return (ClearClipboardVerified() ? CopyResult.Failed : CopyResult.FailedMayRemain);
			}

			try
			{
				KpStartCountdown(host);
				return CopyResult.Ok;
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }

			// Die Löschfrist von KeePass ließ sich nicht starten: lieber sofort leeren als liegen lassen
			return (ClearClipboardVerified() ? CopyResult.Failed : CopyResult.FailedMayRemain);
		}

		/// <summary>Öffnet die URL des Eintrags über KeePass. Rückgabe false: nicht möglich.</summary>
		public static bool OpenEntryUrl(IPluginHost host, PwEntry pe)
		{
			if(pe == null) return false;
			try
			{
				KpOpenEntryUrl(pe);
				return true;
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return false;
		}

		/// <summary>Öffnet eine Adresse ausschließlich über KeePass (URL-Überschreibungen, Platzhalter).
		/// Es gibt bewusst keinen eigenen Start über Windows: Der Inhalt eines Eintrags soll nie
		/// ungeprüft als Programm oder Protokoll-Aufruf ausgeführt werden. Rückgabe false: nicht möglich.</summary>
		public static bool OpenUrl(IPluginHost host, string url, PwEntry pe)
		{
			if(string.IsNullOrEmpty(url)) return false;
			try
			{
				KpOpenUrl(url, pe);
				return true;
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return false;
		}

		/// <summary>Nach wie vielen Sekunden KeePass die Zwischenablage leert
		/// (Optionen › Sicherheit). 0 oder weniger = nie / unbekannt.</summary>
		public static int ClipboardClearSeconds()
		{
			try
			{
				Type tp = KpAsm.GetType("KeePass.Program");
				object cfg = GetMember(tp, null, "Config");
				object sec = GetMember((cfg != null) ? cfg.GetType() : null, cfg, "Security");
				object v = GetMember((sec != null) ? sec.GetType() : null, sec, "ClipboardClearAfterSeconds");
				if(v != null) return Convert.ToInt32(v);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return 0;
		}

		private static object GetMember(Type t, object obj, string name)
		{
			if(t == null) return null;
			BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic |
				((obj == null) ? BindingFlags.Static : BindingFlags.Instance);
			PropertyInfo pi = t.GetProperty(name, bf);
			if(pi != null) return pi.GetValue(obj, null);
			FieldInfo fi = t.GetField(name, bf);
			if(fi != null) return fi.GetValue(obj);
			return null;
		}

		/// <summary>Anhang so öffnen, wie KeePass es selbst tut (interner Betrachter bzw.
		/// zugeordnetes Programm). Gibt false zurück, wenn das nicht möglich war.</summary>
		public static bool OpenBinary(IPluginHost host, string name, ProtectedBinary pb)
		{
			if(pb == null) return false;
			try
			{
				Type t = KpAsm.GetType("KeePass.Util.BinaryDataUtil");
				if(t != null)
				{
					foreach(MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
					{
						if(mi.Name != "Open") continue;
						ParameterInfo[] pars = mi.GetParameters();
						if((pars.Length < 2) || (pars[0].ParameterType != typeof(string)) ||
							(pars[1].ParameterType != typeof(ProtectedBinary))) continue;
						object[] args = new object[pars.Length];
						args[0] = name;
						args[1] = pb;
						for(int i = 2; i < pars.Length; ++i)
							args[i] = (pars[i].IsOptional ? pars[i].DefaultValue : null);
						mi.Invoke(null, args);
						return true;
					}
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return false;
		}

		/// <summary>Anhang über „Speichern unter“ als Datei ablegen.</summary>
		public static void SaveBinary(IWin32Window owner, string name, ProtectedBinary pb)
		{
			if(pb == null) return;
			using(SaveFileDialog dlg = new SaveFileDialog())
			{
				dlg.Title = L.T("Anhang speichern");
				dlg.FileName = name;
				dlg.Filter = L.T("Alle Dateien (*.*)|*.*");
				dlg.OverwritePrompt = true;
				if(dlg.ShowDialog(owner) != DialogResult.OK) return;
				try { File.WriteAllBytes(dlg.FileName, pb.ReadData()); }
				catch(Exception ex)
				{
					// Technische Details nur in den Fehlerbericht, nicht in die Meldung
					FluentThemeExt.Report(ex);
					MessageBox.Show(owner, L.T("Der Anhang konnte nicht gespeichert werden. Bitte prüfen, ob der Ordner beschreibbar ist und die Datei nicht gerade geöffnet ist."),
						"Fluent Theme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
		}

		public static bool HasTag(PwEntry pe, string tag)
		{
			if((pe == null) || string.IsNullOrEmpty(tag)) return false;
			foreach(string t in pe.Tags)
			{
				if(string.Equals(t, tag, StringComparison.CurrentCultureIgnoreCase)) return true;
			}
			return false;
		}

		/// <summary>Zeigt alle Einträge mit diesem Tag in der Liste – wie KeePass über
		/// Bearbeiten › Einträge mit Tag anzeigen. Fällt sonst auf eine eigene Ergebnisgruppe zurück.</summary>
		public static bool ShowEntriesByTag(IPluginHost host, string tag)
		{
			MainForm mf = host.MainWindow;
			if((mf == null) || (host.Database == null) || !host.Database.IsOpen) return false;

			// 1. KeePass-eigene Funktion (je nach Version privat)
			try
			{
				foreach(MethodInfo mi in typeof(MainForm).GetMethods(BindingFlags.Instance | BindingFlags.Public |
					BindingFlags.NonPublic))
				{
					if(mi.Name != "ShowEntriesByTag") continue;
					ParameterInfo[] pars = mi.GetParameters();
					if((pars.Length == 1) && (pars[0].ParameterType == typeof(string)))
					{
						mi.Invoke(mf, new object[] { tag });
						return true;
					}
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }

			// 2. Eigene virtuelle Gruppe mit den Treffern
			try
			{
				PwGroup pg = new PwGroup(true, true, L.F("Tag: {0}", tag), PwIcon.Key);
				pg.IsVirtual = true;
				foreach(PwEntry pe in host.Database.RootGroup.GetEntries(true))
				{
					if(HasTag(pe, tag)) pg.AddEntry(pe, false);
				}
				MethodInfo up = typeof(MainForm).GetMethod("UpdateEntryList", BindingFlags.Instance |
					BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(PwGroup), typeof(bool) }, null);
				if(up != null)
				{
					up.Invoke(mf, new object[] { pg, false });
					return true;
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return false;
		}

		/// <summary>Passwortstärke in Bit nach der Bewertung von KeePass. −1: Bewertung nicht verfügbar
		/// (es gibt bewusst keine eigene Ersatzrechnung – sie würde schwache Passwörter stark nennen).</summary>
		public static int EstimateBits(char[] pw)
		{
			if((pw == null) || (pw.Length == 0)) return 0;
			try { return KpEstimateBits(pw); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return -1;
		}
	}
}
