// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

using KeePass.Forms;
using KeePass.Plugins;

using KeePassLib;
using KeePassLib.Security;

namespace FluentTheme
{
	/// <summary>Ruft KeePass-Funktionen per Reflection auf, damit das Plugin mit
	/// möglichst vielen KeePass-2.x-Versionen funktioniert.</summary>
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

		/// <summary>Kopiert wie KeePass selbst (Platzhalter, Auto-Löschen der Zwischenablage).</summary>
		public static void Copy(IPluginHost host, ProtectedString ps, PwEntry pe)
		{
			if(ps == null) return;
			bool done = false;
			try
			{
				Type t = KpAsm.GetType("KeePass.Util.ClipboardUtil");
				MethodInfo mi = FindStatic(t, "CopyAndMinimize", typeof(ProtectedString));
				if(mi == null) mi = FindStatic(t, "Copy", typeof(ProtectedString));
				if(mi != null)
				{
					mi.Invoke(null, BuildArgs(mi, host, ps, ps.ReadString(), pe));
					done = true;
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }

			if(!done)
			{
				string s = ps.ReadString();
				if(s.Length > 0) Clipboard.SetText(s);
			}

			try
			{
				MethodInfo sc = host.MainWindow.GetType().GetMethod("StartClipboardCountdown",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
					Type.EmptyTypes, null);
				if(sc != null) sc.Invoke(host.MainWindow, null);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		public static void OpenEntryUrl(IPluginHost host, PwEntry pe)
		{
			if(pe == null) return;
			try
			{
				Type t = KpAsm.GetType("KeePass.Util.WinUtil");
				MethodInfo mi = FindExact(t, "OpenEntryUrl", new Type[] { typeof(PwEntry) });
				if(mi == null) mi = FindStatic(t, "OpenEntryUrl", typeof(PwEntry));
				if(mi != null)
				{
					mi.Invoke(null, BuildArgs(mi, host, null, null, pe));
					return;
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }

			OpenUrl(host, pe.Strings.ReadSafe(PwDefs.UrlField), pe);
		}

		public static void OpenUrl(IPluginHost host, string url, PwEntry pe)
		{
			if(string.IsNullOrEmpty(url)) return;
			try
			{
				Type t = KpAsm.GetType("KeePass.Util.WinUtil");
				// Bevorzugt die einfachen Überladungen OpenUrl(string, PwEntry[, bool])
				MethodInfo mi = FindExact(t, "OpenUrl", new Type[] { typeof(string), typeof(PwEntry) });
				if(mi == null) mi = FindExact(t, "OpenUrl", new Type[] { typeof(string), typeof(PwEntry), typeof(bool) });
				if(mi == null) mi = FindStatic(t, "OpenUrl", typeof(string));
				if(mi != null)
				{
					mi.Invoke(null, BuildArgs(mi, host, null, url, pe));
					return;
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }

			try { Process.Start(url); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
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
				dlg.Title = "Anhang speichern";
				dlg.FileName = name;
				dlg.Filter = "Alle Dateien (*.*)|*.*";
				dlg.OverwritePrompt = true;
				if(dlg.ShowDialog(owner) != DialogResult.OK) return;
				try { File.WriteAllBytes(dlg.FileName, pb.ReadData()); }
				catch(Exception ex)
				{
					MessageBox.Show(owner, "Der Anhang konnte nicht gespeichert werden:" + Environment.NewLine +
						ex.Message, "Fluent-Design", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
				PwGroup pg = new PwGroup(true, true, "Tag: " + tag, PwIcon.Key);
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

		public static int EstimateBits(string pw)
		{
			if(string.IsNullOrEmpty(pw)) return 0;
			try
			{
				Type t = typeof(PwEntry).Assembly.GetType("KeePassLib.Cryptography.QualityEstimation");
				if(t != null)
				{
					foreach(MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
					{
						if(mi.Name != "EstimatePasswordBits") continue;
						ParameterInfo[] pars = mi.GetParameters();
						if(pars.Length != 1) continue;
						object arg = null;
						if(pars[0].ParameterType == typeof(char[])) arg = pw.ToCharArray();
						else if(pars[0].ParameterType == typeof(string)) arg = pw;
						else continue;
						return Convert.ToInt32(mi.Invoke(null, new object[] { arg }));
					}
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return pw.Length * 4;
		}
	}
}