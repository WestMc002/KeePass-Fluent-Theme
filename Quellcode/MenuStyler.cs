// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Windows-11-Menüs: mehr Innenabstand, höhere Einträge und abgerundete Ecken.
	/// Hängt sich an alle Dropdowns (Menüleiste, Symbolleiste, Kontextmenüs).</summary>
	internal sealed class MenuStyler
	{
		private readonly HashSet<ToolStripDropDown> m_hooked = new HashSet<ToolStripDropDown>();
		private Palette m_p;
		private Font m_dropFont;
		private readonly HashSet<ToolStripSeparator> m_hiddenSeps = new HashSet<ToolStripSeparator>();
		private readonly HashSet<ToolStripItem> m_edgeItems = new HashSet<ToolStripItem>();
		private float m_s = 1f;

		// „Weitere Befehle“ im Kontextmenü öffnet das Menü „Eintrag“ der Menüleiste – KeePass zeigt es
		// dann unter der Menüleiste an. Wir merken uns, wo im Kontextmenü geklickt wurde.
		private Point m_ctxClickPt = Point.Empty;

		/// <summary>Hinweis zu „Weitere Befehle“, falls das Menü nachträglich versetzt werden musste
		/// (erscheint im Fehlerbericht).</summary>
		internal static string MoveInfo = string.Empty;
		private DateTime m_ctxClickTime = DateTime.MinValue;
		private Rectangle m_ctxBounds = Rectangle.Empty; // Bildschirmbereich des angeklickten Kontextmenüs

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		/// <summary>Schrift für die aufklappenden Menüs (bleibt in normaler Größe,
		/// auch wenn die Menüleiste selbst größer ist).</summary>
		public void SetDropDownFont(Font f)
		{
			m_dropFont = f;
		}

		public void Update(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
		}

		/// <summary>Alle Dropdowns einer Leiste (Menüleiste, Symbolleiste) einhängen.</summary>
		public void Attach(ToolStrip ts)
		{
			if(ts == null) return;
			ToolStripDropDown dd = (ts as ToolStripDropDown);
			if(dd != null) HookDropDown(dd);
			foreach(ToolStripItem it in ts.Items) HookItem(it);
		}

		private void HookItem(ToolStripItem it)
		{
			ToolStripDropDownItem ddi = (it as ToolStripDropDownItem);
			if((ddi == null) || !ddi.HasDropDownItems) return;
			HookDropDown(ddi.DropDown);
		}

		public void HookDropDown(ToolStripDropDown dd)
		{
			if((dd == null) || dd.IsDisposed) return;
			if(!m_hooked.Add(dd)) return;
			DisableLegacyShadow(dd);
			dd.Opening += OnOpening;
			dd.ItemClicked += OnItemClicked;
			dd.Opened += OnOpened;
			dd.ItemAdded += OnItemAdded;
			dd.Disposed += OnDisposed;
		}

		/// <summary>Unter Windows 11 zeichnet DWM für abgerundete Popups einen eigenen, weichen Schatten.
		/// Der alte eckige Schlagschatten von WinForms würde an den runden Ecken unten rechts
		/// als dunkles Eck herausragen – daher abschalten.
		/// Wichtig: das Menüfenster dabei NIE neu erzeugen (DropShadowEnabled bei vorhandenem
		/// Fenster) – sonst landet es als Kindfenster mitten in KeePass. Ist das Fenster schon da,
		/// wird nur der Schatten-Stil der Fensterklasse entfernt.</summary>
		private static void DisableLegacyShadow(ToolStripDropDown dd)
		{
			if(!NativeMethods.IsWin11 || dd.IsDisposed) return;
			try
			{
				if(!dd.IsHandleCreated)
				{
					if(dd.DropShadowEnabled) dd.DropShadowEnabled = false;
				}
				else NativeMethods.RemoveClassDropShadow(dd.Handle);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Später hinzugefügte Einträge (z. B. von anderen Plugins) gleich mitgestalten.</summary>
		private void OnItemAdded(object sender, ToolStripItemEventArgs e)
		{
			if((m_p == null) || (e.Item == null)) return;
			try
			{
				StyleItem(e.Item);
				ToolStripDropDown dd = (sender as ToolStripDropDown);
				if(dd != null) UpdateImageMargin(dd);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		public void Detach()
		{
			foreach(ToolStripDropDown dd in m_hooked)
			{
				dd.Opening -= OnOpening;
				dd.ItemClicked -= OnItemClicked;
				dd.Opened -= OnOpened;
				dd.ItemAdded -= OnItemAdded;
				dd.Disposed -= OnDisposed;
			}
			m_hooked.Clear();
		}

		private void OnDisposed(object sender, EventArgs e)
		{
			ToolStripDropDown dd = (sender as ToolStripDropDown);
			if(dd != null) m_hooked.Remove(dd);
		}

		private void OnOpening(object sender, EventArgs e)
		{
			ToolStripDropDown dd = (sender as ToolStripDropDown);
			if((dd == null) || (m_p == null)) return;
			try { StyleItems(dd); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void OnOpened(object sender, EventArgs e)
		{
			ToolStripDropDown dd = (sender as ToolStripDropDown);
			if((dd == null) || (m_p == null) || !dd.IsHandleCreated) return;
			try { CollapseSeparators(dd); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			DisableLegacyShadow(dd);
			NativeMethods.SetPopupStyle(dd.Handle, m_p.Border);
			try
			{
				if(!MoveToContextClick(dd)) KeepOnMonitor(dd);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void StyleItems(ToolStripDropDown dd)
		{
			dd.SuspendLayout();
			try
			{
				Padding ddPad = new Padding(D(6), D(4), D(6), D(4));
				if(dd.Padding != ddPad) dd.Padding = ddPad;

				// Alle Symbole auf 16 px (skaliert) – sonst machen große Plugin-Symbole
				// einzelne Zeilen höher und der Abstand wirkt ungleichmäßig
				Size isz = new Size(D(16), D(16));
				if(dd.ImageScalingSize != isz) dd.ImageScalingSize = isz;

				if((m_dropFont != null) && !dd.Font.Equals(m_dropFont)) dd.Font = m_dropFont;

				foreach(ToolStripItem it in dd.Items) StyleItem(it);
				UpdateImageMargin(dd);
				CollapseSeparators(dd);
			}
			finally { dd.ResumeLayout(); }
		}

		/// <summary>Doppelte Trennstriche (z. B. wenn ein Plugin eigene Trennstriche einfügt oder
		/// Einträge dazwischen ausgeblendet sind) sowie Striche am Anfang/Ende ausblenden.</summary>
		private void CollapseSeparators(ToolStripDropDown dd)
		{
			bool anyBefore = false;
			bool prevSep = false;
			ToolStripSeparator lastSep = null;
			foreach(ToolStripItem it in dd.Items)
			{
				ToolStripSeparator sep = (it as ToolStripSeparator);
				if(sep == null)
				{
					if(it.Available) { anyBefore = true; prevSep = false; lastSep = null; }
					continue;
				}

				bool hide = (!anyBefore || prevSep);
				if(hide)
				{
					if(sep.Available) { sep.Available = false; m_hiddenSeps.Add(sep); }
				}
				else
				{
					if(m_hiddenSeps.Remove(sep)) sep.Available = true;
					if(sep.Available) { prevSep = true; lastSep = sep; }
				}
			}
			// Trennstrich ganz am Ende
			if((lastSep != null) && prevSep && lastSep.Available)
			{
				lastSep.Available = false;
				m_hiddenSeps.Add(lastSep);
			}

			ApplyEdgeSpacing(dd);
		}

		/// <summary>Zusätzlicher Abstand über dem ersten und unter dem letzten sichtbaren Eintrag
		/// (zuverlässiger als das Padding des Menüs).</summary>
		private void ApplyEdgeSpacing(ToolStripDropDown dd)
		{
			ToolStripItem first = null, last = null;
			foreach(ToolStripItem it in dd.Items)
			{
				if(!it.Available) continue;
				if(first == null) first = it;
				last = it;
			}

			foreach(ToolStripItem it in dd.Items)
			{
				if((it == first) || (it == last) || !m_edgeItems.Contains(it)) continue;
				it.Margin = Padding.Empty;
				m_edgeItems.Remove(it);
			}
			if(first == null) return;

			int top = D(6), bottom = D(6); // oben und unten gleich (globale Vorgabe: 6 × Skalierung)
			Padding pf = new Padding(0, top, 0, ((first == last) ? bottom : 0));
			if(first.Margin != pf) first.Margin = pf;
			m_edgeItems.Add(first);
			if(last != first)
			{
				Padding pl = new Padding(0, 0, 0, bottom);
				if(last.Margin != pl) last.Margin = pl;
				m_edgeItems.Add(last);
			}
		}

		private void OnItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			ToolStripDropDown dd = (sender as ToolStripDropDown);
			if((dd == null) || (e.ClickedItem == null)) return;
			// Nur Kontextmenüs (kein Besitzer in einer Menüleiste)
			ToolStripDropDown top = dd;
			while((top.OwnerItem != null) && (top.OwnerItem.Owner is ToolStripDropDown))
				top = (ToolStripDropDown)top.OwnerItem.Owner;
			if(top.OwnerItem != null) return;
			m_ctxClickPt = Cursor.Position;
			m_ctxClickTime = DateTime.Now;
			m_ctxBounds = (dd.IsHandleCreated ? dd.Bounds : Rectangle.Empty);

		}

		/// <summary>Öffnet sich ein Menü der Menüleiste direkt nach einem Klick im Kontextmenü
		/// (z. B. „Weitere Befehle“), erscheint es an der Klickstelle statt unter der Menüleiste.</summary>
		private bool MoveToContextClick(ToolStripDropDown dd)
		{
			if(m_ctxClickPt.IsEmpty) return false;
			double ms = (DateTime.Now - m_ctxClickTime).TotalMilliseconds;
			ToolStripItem owner = dd.OwnerItem;
			string name = ((owner != null) ? owner.Name : "(kein Besitzer)");
			if(ms > 2000)
			{
				m_ctxClickPt = Point.Empty; // abgelaufen – normale Menüs nicht mehr anfassen
				return false;
			}
			if((owner == null) || !(owner.Owner is MenuStrip)) return false;
			// Hat der Nutzer die Menüleiste selbst angeklickt, nichts verschieben
			ToolStrip bar = owner.Owner;
			if(bar.IsHandleCreated && bar.RectangleToScreen(bar.ClientRectangle).Contains(Cursor.Position)) return false;

			// Klickstelle nicht verbrauchen: KeePass/WinForms öffnen das Menü teils zweimal kurz
			// hintereinander – jede Öffnung innerhalb von 2 s kommt an die Klickstelle
			Point pt = m_ctxClickPt;
			Rectangle wa = Screen.FromPoint(pt).WorkingArea;
			// Das Menü ersetzt das Kontextmenü an derselben Stelle: gleiche linke Kante, gleiche
			// Oberkante. Reicht der Platz nach unten nicht, schließt es unten bündig mit dem
			// Kontextmenü ab. Ohne bekannte Lage gilt die Klickstelle.
			int w = dd.Width, h = dd.Height;
			Rectangle c = (m_ctxBounds.IsEmpty ? new Rectangle(pt, Size.Empty) : m_ctxBounds);
			int x = c.Left, y = c.Top;
			if((x + w > wa.Right) && (c.Right - w >= wa.Left)) x = c.Right - w;
			if((y + h > wa.Bottom) && (c.Bottom - h >= wa.Top)) y = c.Bottom - h;
			x = Math.Max(wa.Left, Math.Min(x, wa.Right - w));
			y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - h));
			Point target = new Point(x, y);
			// WinForms setzt Menüs der Menüleiste nach dem Öffnen wieder unter die Leiste – die
			// Position deshalb festhalten, bis das Menü geschlossen wird
			PositionPin.Pin(dd, target);
			dd.Location = target;
			// dd.Location bewegt das Fenster hier nicht (WinForms hält die Position für schon gesetzt) –
			// deshalb sofort direkt über Windows an die Klickstelle setzen
			PositionPin.MoveTo(dd.Handle, target);
			// Nachkontrolle: kurz danach echte Fensterposition prüfen und notfalls direkt setzen
			try
			{
				ToolStripDropDown d = dd;
				Timer t = new Timer();
				t.Interval = 150;
				t.Tick += delegate
				{
					t.Stop();
					t.Dispose();
					if(d.IsDisposed || !d.IsHandleCreated) return;
					if(d.Visible && PositionPin.MoveTo(d.Handle, target))
						MoveInfo = "Menü " + name + ": nach 150 ms nachgesetzt";
				};
				t.Start();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			return true;
		}

		/// <summary>Hält ein Menü auf dem Bildschirm, auf dem es geöffnet wurde. WinForms schiebt ein Menü,
		/// das am Bildschirmrand nicht ganz passt, sonst auf den Nachbarbildschirm (es wählt den Bildschirm,
		/// auf dem der größere Teil des Menüs läge).</summary>
		private void KeepOnMonitor(ToolStripDropDown dd)
		{
			Rectangle b = dd.Bounds; // Bildschirmkoordinaten
			int w = b.Width, h = b.Height;
			if((w <= 0) || (h <= 0)) return;
			Rectangle wa;
			int x, y;
			ToolStripItem owner = dd.OwnerItem;
			ToolStrip parent = ((owner != null) ? owner.Owner : null);
			if((parent != null) && parent.IsHandleCreated)
			{
				// Menü eines Eintrags: maßgeblich ist der Bildschirm, auf dem der Eintrag liegt
				Rectangle ir = parent.RectangleToScreen(owner.Bounds);
				wa = Screen.FromPoint(new Point(ir.Left + ir.Width / 2, ir.Top + ir.Height / 2)).WorkingArea;
				if(wa.Contains(b)) return;
				if(parent is ToolStripDropDown)
				{
					// Untermenü: rechts neben dem Elternmenü, sonst links davon
					Rectangle pr = parent.Bounds;
					x = pr.Right - D(4);
					if(x + w > wa.Right) x = pr.Left - w + D(4);
					y = b.Top;
				}
				else
				{
					// Menüleiste/Symbolleiste: unter dem Eintrag, sonst darüber
					x = ir.Left;
					y = ir.Bottom;
					if((y + h > wa.Bottom) && (ir.Top - h >= wa.Top)) y = ir.Top - h;
				}
			}
			else
			{
				// Kontextmenü: maßgeblich ist der Bildschirm der Klickstelle
				Point cur = Cursor.Position;
				ContextMenuStrip cms = (dd as ContextMenuStrip);
				Control src = ((cms != null) ? cms.SourceControl : null);
				Form f = ((src != null) ? src.FindForm() : Form.ActiveForm);
				bool atCursor = ((f == null) || f.Bounds.Contains(cur));
				if(atCursor) wa = Screen.FromPoint(cur).WorkingArea;
				else wa = Screen.FromControl(f).WorkingArea; // per Tastatur geöffnet
				if(wa.Contains(b)) return;
				if(atCursor)
				{
					// Wie Windows: passt es rechts/unten nicht, öffnet es links/über der Klickstelle
					x = cur.X;
					if(x + w > wa.Right) x = cur.X - w;
					y = cur.Y;
					if(y + h > wa.Bottom) y = cur.Y - h;
				}
				else { x = b.Left; y = b.Top; }
			}
			x = Math.Max(wa.Left, Math.Min(x, wa.Right - w));
			y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - h));
			Point target = new Point(x, y);
			if(target == b.Location) return;
			// Position festhalten, bis das Menü schließt – WinForms setzt sie sonst wieder zurück
			PositionPin.Pin(dd, target);
			dd.Location = target;
			PositionPin.MoveTo(dd.Handle, target);
		}

		/// <summary>Globale Vorgabe: Menüs ohne Symbole bekommen keine leere Symbolspalte
		/// (der Text beginnt dann mit 16 × Skalierung Rand, siehe FluentRenderer).</summary>
		private static void UpdateImageMargin(ToolStripDropDown dd)
		{
			ToolStripDropDownMenu ddm = (dd as ToolStripDropDownMenu);
			if(ddm == null) return;
			bool need = false;
			foreach(ToolStripItem it in ddm.Items)
			{
				if(!it.Available) continue;
				ToolStripMenuItem mi = (it as ToolStripMenuItem);
				if((it.Image != null) || ((mi != null) && (mi.Checked || mi.CheckOnClick)))
				{
					need = true;
					break;
				}
			}
			if(ddm.ShowImageMargin != need) ddm.ShowImageMargin = need;
		}

		private void StyleItem(ToolStripItem it)
		{
			if(it is ToolStripSeparator)
			{
				Padding sepMargin = new Padding(0, D(2), 0, D(2));
				if(it.Margin != sepMargin) it.Margin = sepMargin;
				return;
			}

			ToolStripMenuItem mi = (it as ToolStripMenuItem);
			if(mi != null)
			{
				// rechts etwas mehr, weil der Text für den Abstand Symbol -> Text nach rechts rückt
				Padding itemPad = new Padding(D(4), D(2), D(11), D(2));
				if(mi.Padding != itemPad) mi.Padding = itemPad;
				if(mi.ImageScaling != ToolStripItemImageScaling.SizeToFit)
					mi.ImageScaling = ToolStripItemImageScaling.SizeToFit;
			}
			HookItem(it);
		}
	}

	/// <summary>Hält ein offenes Menü an einer festen Bildschirmposition: jede Verschiebung durch
	/// Windows/WinForms (WM_WINDOWPOSCHANGING) wird auf diese Position umgelenkt, bis es schließt.</summary>
	internal sealed class PositionPin : NativeWindow
	{
		private const int WM_WINDOWPOSCHANGING = 0x0046;
		private const uint SWP_NOMOVE = 0x0002;

		[StructLayout(LayoutKind.Sequential)]
		private struct WINDOWPOS
		{
			public IntPtr hwnd;
			public IntPtr hwndInsertAfter;
			public int x, y, cx, cy;
			public uint flags;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
		}

		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
			uint uFlags);

		/// <summary>Fenster direkt an eine Position setzen, falls es woanders steht.</summary>
		internal static bool MoveTo(IntPtr h, Point pt)
		{
			RECT r;
			if((h == IntPtr.Zero) || !GetWindowRect(h, out r)) return false;
			if((r.Left == pt.X) && (r.Top == pt.Y)) return false;
			const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
			return SetWindowPos(h, IntPtr.Zero, pt.X, pt.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
		}

		private readonly ToolStripDropDown m_dd;
		private Point m_pt;

		private PositionPin(ToolStripDropDown dd, Point pt)
		{
			m_dd = dd;
			m_pt = pt;
			AssignHandle(dd.Handle);
			dd.Closed += OnClosed;
		}

		public static void Pin(ToolStripDropDown dd, Point pt)
		{
			if((dd == null) || !dd.IsHandleCreated) return;
			try { new PositionPin(dd, pt); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void OnClosed(object sender, ToolStripDropDownClosedEventArgs e)
		{
			m_dd.Closed -= OnClosed;
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
		}

		protected override void WndProc(ref Message m)
		{
			if((m.Msg == WM_WINDOWPOSCHANGING) && (m.LParam != IntPtr.Zero))
			{
				try
				{
					WINDOWPOS wp = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
					if(((wp.flags & SWP_NOMOVE) == 0) && ((wp.x != m_pt.X) || (wp.y != m_pt.Y)))
					{
						wp.x = m_pt.X;
						wp.y = m_pt.Y;
						Marshal.StructureToPtr(wp, m.LParam, false);
					}
				}
				catch(Exception ex) { FluentThemeExt.Report(ex); }
			}
			base.WndProc(ref m);
		}
	}
}
