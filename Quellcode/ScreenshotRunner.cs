// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using KeePass.Forms;
using KeePass.UI;

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;

namespace FluentTheme
{
	/// <summary>Erzeugt die Screenshots für GitHub (hell und dunkel) aus einer Demo-Datenbank, die nur
	/// im Speicher existiert. Läuft nur, wenn KeePass mit -fluent-screenshots:"Zielordner" gestartet
	/// wurde (gedacht für eine eigene, portable KeePass-Kopie). Vor jedem Bild wird geprüft, dass
	/// ausschließlich die Demo-Datenbank geöffnet ist; sonst wird abgebrochen.</summary>
	internal sealed class ScreenshotRunner
	{
		private readonly FluentThemeExt m_ext;
		private readonly string m_dir;
		private readonly List<KeyValuePair<int, Action>> m_steps = new List<KeyValuePair<int, Action>>();
		private int m_pos;
		private Timer m_timer;
		private bool m_failed;

		private PwDatabase m_db;
		private PwGroup m_internet;
		private Backdrop m_backdrop;
		private Bitmap m_black;
		private Rectangle m_area;
		private PwEntryForm m_dialog;
		private float m_s = 1f;

		public ScreenshotRunner(FluentThemeExt ext, string dir)
		{
			m_ext = ext;
			m_dir = dir.Trim().Trim('"');
		}

		/// <summary>Vor dem Start von KeePass: Trigger aus, keine zuletzt geöffnete Datenbank laden,
		/// keine Update-Prüfung – in der Screenshot-Kopie darf nichts Echtes geöffnet werden.</summary>
		public static void PrepareConfig()
		{
			try
			{
				KeePass.App.Configuration.AceApplication app = KeePass.Program.Config.Application;
				app.TriggerSystem.Enabled = false;
				app.Start.OpenLastFile = false;
				app.Start.CheckForUpdate = false;
				app.MostRecentlyUsed.Items.Clear();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private MainForm Main { get { return m_ext.ShotMainForm; } }

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		// ------------------------------------------------------------ Ablauf

		public void Start()
		{
			try
			{
				Directory.CreateDirectory(m_dir);
				string err = Path.Combine(m_dir, "Fehler.txt");
				if(File.Exists(err)) File.Delete(err);
				using(Graphics g = Main.CreateGraphics()) { m_s = g.DpiX / 96f; }
			}
			catch(Exception ex) { Fail("Zielordner nicht nutzbar", ex); return; }

			Add(0, CreateDemo);
			Add(1500, delegate { SelectEntry("GitHub"); });

			foreach(bool dark in new bool[] { false, true })
			{
				bool d = dark;
				string sfx = (d ? "dunkel" : "hell");
				Add(800, delegate { m_ext.ShotTheme(d); });
				Add(1500, delegate { FitColumns(); SelectEntry("GitHub"); MouseAway(); });

				// Gesamtansicht
				AddWindowShot(delegate { return Main.Handle; }, (d ? "dunkel" : "hell"));

				// Detailansicht mit „Kopiert“-Hinweis am Passwort
				Add(0, delegate { if(m_ext.ShotDetails != null) m_ext.ShotDetails.ShowCopiedDemo(12); });
				Add(700, delegate { SaveCrop(DetailsArea(), "details-" + sfx); });

				// Eintragsliste mit Ablauf-Hinweisen
				Add(0, delegate { SelectEntry("Streaming-Dienst"); MouseAway(); });
				Add(1000, delegate { SaveCrop(ListArea(), "ablauf-" + sfx); });
				Add(0, delegate { SelectEntry("GitHub"); });

				// Menü Extras › Fluent-Design
				Add(800, OpenMenu);
				AddWindowShot(MenuHandle, "menue-" + sfx);
				Add(0, CloseMenu);

				// Dialog „Eintrag bearbeiten“
				Add(800, OpenDialog);
				AddWindowShot(delegate { return ((m_dialog != null) ? m_dialog.Handle : IntPtr.Zero); }, "dialog-" + sfx);
				Add(0, CloseDialog);
				Add(1000, delegate { });
			}

			Add(0, Finish);

			m_timer = new Timer();
			m_timer.Interval = 500;
			m_timer.Tick += OnTick;
			m_timer.Start();
		}

		private void Add(int waitAfter, Action a)
		{
			m_steps.Add(new KeyValuePair<int, Action>(waitAfter, a));
		}

		private void OnTick(object sender, EventArgs e)
		{
			m_timer.Stop();
			if(m_failed || (m_pos >= m_steps.Count)) return;
			KeyValuePair<int, Action> st = m_steps[m_pos++];
			try { st.Value(); }
			catch(Exception ex) { Fail("Schritt " + m_pos.ToString(), ex); return; }
			if(m_failed) return;
			m_timer.Interval = Math.Max(50, st.Key);
			m_timer.Start();
		}

		/// <summary>Fenster mit durchsichtigen runden Ecken und Schatten: einmal vor schwarzem, einmal
		/// vor weißem Hintergrund aufnehmen und daraus die Deckkraft berechnen.</summary>
		private void AddWindowShot(Func<IntPtr> target, string name)
		{
			Add(900, delegate
			{
				IntPtr h = target();
				if(h == IntPtr.Zero) throw new InvalidOperationException("Fenster für " + name + " nicht gefunden");
				// Countdown anhalten: beide Aufnahmen müssen exakt gleich aussehen
				if(m_ext.ShotDetails != null) m_ext.ShotDetails.Frozen = true;
				RECT wr;
				if(!GetWindowRect(h, out wr)) throw new InvalidOperationException("Lage von " + name + " unbekannt");
				m_area = Rectangle.FromLTRB(wr.Left, wr.Top, wr.Right, wr.Bottom);
				m_area.Inflate(D(40), D(40));
				m_area.Intersect(Screen.FromHandle(h).Bounds);
				if(m_backdrop == null) m_backdrop = new Backdrop();
				m_backdrop.BackColor = Color.Black;
				m_backdrop.Bounds = m_area;
				if(!m_backdrop.Visible) m_backdrop.Show();
				// direkt unter das aufzunehmende Fenster legen, ohne es zu aktivieren
				SetWindowPos(m_backdrop.Handle, h, m_area.X, m_area.Y, m_area.Width, m_area.Height, SWP_NOACTIVATE);
				m_backdrop.Refresh();
			});
			Add(700, delegate
			{
				EnsureOnlyDemo();
				m_black = Capture(m_area);
				m_backdrop.BackColor = Color.White;
				m_backdrop.Refresh();
			});
			Add(300, delegate
			{
				EnsureOnlyDemo();
				using(Bitmap white = Capture(m_area))
				using(Bitmap black = m_black)
				using(Bitmap img = Compose(black, white))
				{
					m_black = null;
					Save(img, name);
				}
				// nicht verstecken, nur wegschieben (sonst blendet Windows sie beim nächsten Mal wieder ein)
				m_backdrop.Bounds = new Rectangle(-10000, -10000, 100, 100);
				if(m_ext.ShotDetails != null) m_ext.ShotDetails.Frozen = false;
			});
		}

		// ------------------------------------------------------------ Demo-Datenbank

		private void CreateDemo()
		{
			MainForm mf = this.Main;
			Screen sc = Screen.FromControl(mf);
			Rectangle wa = sc.WorkingArea;
			mf.WindowState = FormWindowState.Normal;
			// Größe wie auf den bisherigen README-Bildern: genug Platz für Liste und alle drei Karten
			int w = Math.Min(D(1280), wa.Width - D(140));
			int h = Math.Min(D(680), wa.Height - D(140));
			mf.Bounds = new Rectangle(wa.Left + D(60), wa.Top + D(60), w, h);

			// Hintergrundfläche schon jetzt anlegen (außerhalb des Bildschirms): beim ersten Anzeigen
			// blendet Windows sie langsam ein, das darf nicht in die erste Aufnahme fallen
			m_backdrop = new Backdrop();
			m_backdrop.Bounds = new Rectangle(-10000, -10000, 100, 100);
			m_backdrop.Show();

			PwDocument doc = mf.DocumentManager.CreateNewDocument(true);
			m_db = doc.Database;
			CompositeKey key = new CompositeKey();
			key.AddUserKey(new KcpPassword("demo"));
			m_db.New(IOConnectionInfo.FromPath("Demo.kdbx"), key);
			m_db.Name = "Demo";
			m_db.RecycleBinEnabled = false;
			FillDemo(m_db);
			m_db.Modified = false;

			mf.UpdateUI(true, doc, true, m_internet, true, m_internet, false);
			mf.Activate();

			// Erste Karte etwas breiter, damit der vollständige „Kopiert“-Hinweis hineinpasst
			if(m_ext.ShotDetails != null) m_ext.ShotDetails.ColumnRatios = "0.4;0.27;0.33";
			FitColumns();
		}

		private static DateTime Days(int n)
		{
			return DateTime.UtcNow.Date.AddHours(10).AddDays(n);
		}

		private void FillDemo(PwDatabase pd)
		{
			PwGroup root = pd.RootGroup;
			root.Name = "Demo";
			root.IsExpanded = true;

			PwGroup allg = Group(root, "Allgemein", 48);
			m_internet = Group(root, "Internet", 1);
			PwGroup mail = Group(root, "E-Mail", 19);
			PwGroup bank = Group(root, "Homebanking", 37);
			PwGroup netz = Group(root, "Netzwerk", 3);
			PwGroup win = Group(root, "Windows", 38);

			string[] totp = new string[] { "TimeOtp-Secret-Base32", "JBSWY3DPEHPK3PXP" };

			Entry(m_internet, "GitHub", "max.mustermann", "q7#Vt9!mR2wLx$4pZk", "https://github.com",
				"Wiederherstellungscodes liegen im Tresor.", 1, "2FA;Entwicklung", null, totp);
			Entry(m_internet, "Online-Shop", "max@example.com", "Sommer2026!Shop", "https://shop.example.com",
				"Kundennummer 100-2345", 1, "Shopping", Days(3), null);
			Entry(m_internet, "Forum", "maxi", "hallo123", "https://forum.example.com", "", 1, "Privat", Days(-10), null);
			Entry(m_internet, "Streaming-Dienst", "max@example.com", "Kx8!pQ2#nW5vTz", "https://tv.example.com", "", 1,
				"Privat;2FA", null, new string[] { "otp",
				"otpauth://totp/Streaming:max@example.com?secret=GEZDGNBVGY3TQOJQ&period=30&digits=6" });
			Entry(m_internet, "Cloud-Speicher", "max@example.com", "Wolke!7mZ#c4Lp9", "https://cloud.example.com", "", 35,
				"Privat", Days(12), new string[] { "URL 2", "https://files.example.com" });

			Entry(mail, "Mail – max@example.com", "max@example.com", "Brief#Taube42!xY", "https://mail.example.com", "", 19,
				"2FA;Privat", null, new string[] { "URL 2", "https://webmail.example.com", "IMAP-Server", "imap.example.com",
				"SMTP-Server", "smtp.example.com", "TimeOtp-Secret-Base32", "JBSWY3DPEHPK3PXP" });
			Entry(mail, "Mail – Verein", "vorstand@verein.example", "Verein!2026#Mail", "https://mail.verein.example", "", 19,
				"Verein", null, null);

			PwEntry pb = Entry(bank, "Beispiel-Bank", "12345678", "Tresor#88!kLm2Qp", "https://bank.example.com",
				"Online-Banking für das Girokonto.\r\n\r\nIBAN: DE00 0000 0000 0000 0000 00 (Muster)\r\nBIC: EXAMDEXXXXX\r\n" +
				"Hotline: 0800 000 0000\r\nSperr-Notruf: 116 116\r\nTAN-Verfahren: App auf dem Smartphone", 37,
				"Finanzen;2FA", null, null);
			pb.Binaries.Set("Kontoauszug-Muster.txt", new ProtectedBinary(false,
				Encoding.UTF8.GetBytes("Muster-Kontoauszug\r\nNur Beispieldaten.\r\n")));
			Entry(bank, "Depot", "12345678", "Aktie!42#Zins7r", "https://depot.example.com", "", 66, "Finanzen", Days(25), null);

			Entry(netz, "WLAN Zuhause", "", "Demo-WLAN-Passwort-2026!", "", "SSID: Demo-WLAN\r\nVerschlüsselung: WPA3", 12,
				"Zuhause", null, null);
			Entry(netz, "Router", "admin", "R0uter!Demo#9", "http://192.168.178.1", "", 3, "Zuhause", null, null);
			Entry(netz, "NAS", "max", "Nas#Speicher!55", "http://nas.local:5000", "", 27, "Zuhause", null, null);

			Entry(win, "Arbeits-PC", "max", "Fenster!11#Pc", "", "", 38, "Arbeit", null, null);

			Entry(allg, "Versicherung", "VN-123456", "Schutz#2026!Vers", "https://kunden.versicherung.example", "", 67,
				"Finanzen", null, null);
			Entry(allg, "Fitnessstudio", "max.mustermann", "fit2026", "https://fit.example.com", "", 0, "Privat", null, null);
		}

		private static PwGroup Group(PwGroup parent, string name, int icon)
		{
			PwGroup pg = new PwGroup(true, true, name, (PwIcon)icon);
			pg.IsExpanded = true;
			parent.AddGroup(pg, true);
			return pg;
		}

		private static PwEntry Entry(PwGroup pg, string title, string user, string pw, string url, string notes,
			int icon, string tags, DateTime? expires, string[] extra)
		{
			PwEntry pe = new PwEntry(true, true);
			pe.IconId = (PwIcon)icon;
			pe.Strings.Set(PwDefs.TitleField, new ProtectedString(false, title));
			pe.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, user));
			pe.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, pw));
			pe.Strings.Set(PwDefs.UrlField, new ProtectedString(false, url));
			pe.Strings.Set(PwDefs.NotesField, new ProtectedString(false, notes));
			if(extra != null)
			{
				for(int i = 0; i + 1 < extra.Length; i += 2)
					pe.Strings.Set(extra[i], new ProtectedString(extra[i].IndexOf("Secret", StringComparison.Ordinal) >= 0,
						extra[i + 1]));
			}
			foreach(string t in tags.Split(';'))
			{
				if(t.Length > 0) pe.Tags.Add(t);
			}
			DateTime now = DateTime.UtcNow;
			pe.CreationTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
			pe.LastModificationTime = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);
			if(expires.HasValue)
			{
				pe.Expires = true;
				pe.ExpiryTime = expires.Value;
			}
			pg.AddEntry(pe, true);
			return pe;
		}

		/// <summary>Letzte Spalte auf die restliche Breite setzen, damit die Liste nicht waagerecht
		/// scrollt (sonst erscheint unten die Scrollleiste im Bild).</summary>
		private void FitColumns()
		{
			ListView lv = m_ext.ShotList;
			if((lv == null) || (lv.Columns.Count == 0)) return;
			int total = 0;
			for(int i = 0; i < lv.Columns.Count - 1; ++i) total += lv.Columns[i].Width;
			int rest = lv.ClientSize.Width - total - D(4);
			if(rest > D(60)) lv.Columns[lv.Columns.Count - 1].Width = rest;
			else lv.Columns[lv.Columns.Count - 1].Width = -2; // LVSCW_AUTOSIZE_USEHEADER
		}

		private void SelectEntry(string title)
		{
			if((m_db == null) || (m_internet == null)) return;
			PwEntry pe = null;
			foreach(PwEntry e in m_internet.Entries)
			{
				if(e.Strings.ReadSafe(PwDefs.TitleField) == title) { pe = e; break; }
			}
			if(pe == null) return;
			PwObjectList<PwEntry> l = new PwObjectList<PwEntry>();
			l.Add(pe);
			Main.SelectEntries(l, true, true);
			if(m_ext.ShotList != null) m_ext.ShotList.Focus();
			Main.UpdateUI(false, null, false, null, false, null, false);
		}

		private void MouseAway()
		{
			Rectangle wa = Screen.FromControl(Main).WorkingArea;
			Cursor.Position = new Point(wa.Right - 2, wa.Bottom - 2);
		}

		// ------------------------------------------------------------ Menü und Dialog

		private void OpenMenu()
		{
			ToolStripMenuItem root = m_ext.ShotMenu;
			if(root == null) throw new InvalidOperationException("Menü Fluent-Design nicht gefunden");
			ToolStripMenuItem tools = (root.OwnerItem as ToolStripMenuItem);
			Main.Activate();
			if(tools != null) tools.ShowDropDown();
			root.ShowDropDown();
		}

		private IntPtr MenuHandle()
		{
			ToolStripMenuItem root = m_ext.ShotMenu;
			if((root == null) || !root.DropDown.Visible) return IntPtr.Zero;
			return root.DropDown.Handle;
		}

		private void CloseMenu()
		{
			ToolStripMenuItem root = m_ext.ShotMenu;
			if(root == null) return;
			root.HideDropDown();
			ToolStripMenuItem tools = (root.OwnerItem as ToolStripMenuItem);
			if(tools != null) tools.HideDropDown();
		}

		private void OpenDialog()
		{
			PwEntry pe = null;
			foreach(PwEntry e in m_internet.Entries)
			{
				if(e.Strings.ReadSafe(PwDefs.TitleField) == "GitHub") { pe = e; break; }
			}
			if(pe == null) throw new InvalidOperationException("Demo-Eintrag fehlt");
			PwEntryForm f = new PwEntryForm();
			f.InitEx(pe, PwEditMode.EditExistingEntry, m_db, Main.ClientIcons, false, false);
			m_dialog = f;
			MainForm mf = this.Main;
			mf.BeginInvoke(new MethodInvoker(delegate
			{
				try { f.ShowDialog(mf); }
				catch(Exception ex) { FluentThemeExt.Report(ex); }
				finally { f.Dispose(); }
			}));
		}

		private void CloseDialog()
		{
			if((m_dialog != null) && !m_dialog.IsDisposed) m_dialog.DialogResult = DialogResult.Cancel;
			m_dialog = null;
			if(m_db != null) m_db.Modified = false;
		}

		// ------------------------------------------------------------ Ausschnitte

		private Rectangle DetailsArea()
		{
			DetailsPanel dp = m_ext.ShotDetails;
			if((dp == null) || !dp.Visible) throw new InvalidOperationException("Detailansicht nicht sichtbar");
			Rectangle r = dp.ScreenshotArea;
			if(r.IsEmpty) throw new InvalidOperationException("Detailansicht leer");
			return dp.RectangleToScreen(r);
		}

		private Rectangle ListArea()
		{
			Control c = m_ext.ShotListCard;
			if(c == null) throw new InvalidOperationException("Eintragsliste nicht gefunden");
			return c.RectangleToScreen(c.ClientRectangle);
		}

		private void SaveCrop(Rectangle screenRect, string name)
		{
			EnsureOnlyDemo();
			using(Bitmap b = Capture(screenRect)) { Save(b, name); }
		}

		// ------------------------------------------------------------ Sicherheit, Speichern, Ende

		/// <summary>Nur die Demo-Datenbank darf geöffnet sein – sonst sofort abbrechen.</summary>
		private void EnsureOnlyDemo()
		{
			List<PwDocument> docs = Main.DocumentManager.Documents;
			if((m_db == null) || (docs.Count != 1) || !object.ReferenceEquals(docs[0].Database, m_db))
				throw new InvalidOperationException("Außer der Demo-Datenbank ist etwas anderes geöffnet – abgebrochen, " +
					"damit keine echten Daten in die Screenshots gelangen.");
		}

		private void Save(Bitmap b, string name)
		{
			b.Save(Path.Combine(m_dir, name + ".png"), ImageFormat.Png);
		}

		private void Finish()
		{
			if(m_timer != null) m_timer.Stop();
			if(m_backdrop != null) { m_backdrop.Close(); m_backdrop = null; }
			CloseAndExit();
		}

		private void Fail(string what, Exception ex)
		{
			m_failed = true;
			try
			{
				File.WriteAllText(Path.Combine(m_dir, "Fehler.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
					Environment.NewLine + what + Environment.NewLine + ex, Encoding.UTF8);
			}
			catch(Exception) { }
			try { CloseDialog(); CloseMenu(); } catch(Exception) { }
			if(m_backdrop != null) { m_backdrop.Close(); m_backdrop = null; }
			CloseAndExit();
		}

		private void CloseAndExit()
		{
			try
			{
				if(m_db != null) m_db.Modified = false;
				KeePass.App.Configuration.AceApplication app = KeePass.Program.Config.Application;
				app.MostRecentlyUsed.Items.Clear();
				app.LastUsedFile = new IOConnectionInfo();
			}
			catch(Exception) { }
			MainForm mf = this.Main;
			mf.BeginInvoke(new MethodInvoker(delegate { mf.Close(); }));
		}

		// ------------------------------------------------------------ Bilder

		private static Bitmap Capture(Rectangle r)
		{
			Bitmap b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
			using(Graphics g = Graphics.FromImage(b))
			{
				g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);
			}
			return b;
		}

		/// <summary>Aus den Aufnahmen vor Schwarz und vor Weiß Farbe und Deckkraft berechnen
		/// (runde Ecken und Schatten werden durchsichtig) und auf den sichtbaren Teil zuschneiden.</summary>
		private static Bitmap Compose(Bitmap black, Bitmap white)
		{
			int w = black.Width, h = black.Height;
			Rectangle all = new Rectangle(0, 0, w, h);
			BitmapData db = black.LockBits(all, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dw = white.LockBits(all, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[] pb = new byte[db.Stride * h];
			byte[] pw = new byte[dw.Stride * h];
			Marshal.Copy(db.Scan0, pb, 0, pb.Length);
			Marshal.Copy(dw.Scan0, pw, 0, pw.Length);
			black.UnlockBits(db);
			white.UnlockBits(dw);

			byte[] po = new byte[w * h * 4];
			int minX = w, minY = h, maxX = -1, maxY = -1;
			for(int y = 0; y < h; ++y)
			{
				for(int x = 0; x < w; ++x)
				{
					int i = y * db.Stride + x * 4;
					int o = (y * w + x) * 4;
					int diff = ((pw[i] - pb[i]) + (pw[i + 1] - pb[i + 1]) + (pw[i + 2] - pb[i + 2])) / 3;
					int a = 255 - Math.Max(0, Math.Min(255, diff));
					if(a <= 3) continue; // ganz durchsichtig
					for(int c = 0; c < 3; ++c)
						po[o + c] = (byte)Math.Min(255, (pb[i + c] * 255 + a / 2) / a);
					po[o + 3] = (byte)a;
					if(a > 8)
					{
						if(x < minX) minX = x;
						if(x > maxX) maxX = x;
						if(y < minY) minY = y;
						if(y > maxY) maxY = y;
					}
				}
			}
			if(maxX < 0) { minX = 0; minY = 0; maxX = w - 1; maxY = h - 1; }

			int ow = maxX - minX + 1, oh = maxY - minY + 1;
			Bitmap res = new Bitmap(ow, oh, PixelFormat.Format32bppArgb);
			BitmapData dr = res.LockBits(new Rectangle(0, 0, ow, oh), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			byte[] line = new byte[ow * 4];
			for(int y = 0; y < oh; ++y)
			{
				Buffer.BlockCopy(po, ((minY + y) * w + minX) * 4, line, 0, line.Length);
				Marshal.Copy(line, 0, new IntPtr(dr.Scan0.ToInt64() + (long)y * dr.Stride), line.Length);
			}
			res.UnlockBits(dr);
			return res;
		}

		// ------------------------------------------------------------ Win32

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
		}

		private const uint SWP_NOACTIVATE = 0x0010;

		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
			uint uFlags);

		/// <summary>Einfarbige Fläche hinter dem aufzunehmenden Fenster; wird nie aktiviert,
		/// damit offene Menüs nicht zugehen.</summary>
		private sealed class Backdrop : Form
		{
			public Backdrop()
			{
				this.FormBorderStyle = FormBorderStyle.None;
				this.ShowInTaskbar = false;
				this.StartPosition = FormStartPosition.Manual;
				this.BackColor = Color.Black;
				this.Text = "Fluent Theme Screenshots";
			}

			protected override bool ShowWithoutActivation
			{
				get { return true; }
			}

			protected override CreateParams CreateParams
			{
				get
				{
					CreateParams cp = base.CreateParams;
					cp.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW
					return cp;
				}
			}
		}
	}
}
