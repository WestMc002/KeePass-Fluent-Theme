// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Zeichnet die Datenbank-Tabs komplett selbst (ersetzt das Windows-Zeichnen),
	/// damit kein eckiger Balken hinter den abgerundeten Tabs bleibt.</summary>
	internal sealed class TabStripPainter : NativeWindow
	{
		private const int WM_PAINT = 0x000F;
		private const int WM_ERASEBKGND = 0x0014;
		// Nachrichten, mit denen sich die Tab-Leiste ändert (Tab hinzugefügt/entfernt, Symbolliste …)
		private const int TCM_SETIMAGELIST = 0x1303;
		private const int TCM_SETITEMA = 0x1306;
		private const int TCM_INSERTITEMA = 0x1307;
		private const int TCM_DELETEITEM = 0x1308;
		private const int TCM_DELETEALLITEMS = 0x1309;
		private const int TCM_ADJUSTRECT = 0x1328;
		private const int TCM_SETITEMW = 0x133D;
		private const int TCM_INSERTITEMW = 0x133E;

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct PAINTSTRUCT
		{
			public IntPtr hdc;
			public int fErase;
			public RECT rcPaint;
			public int fRestore;
			public int fIncUpdate;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
			public byte[] rgbReserved;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

		[DllImport("user32.dll")]
		private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

		private readonly TabControl m_tc;
		private Palette m_p;
		private float m_s = 1f;
		private Font m_font;
		private Font m_fontSel;
		private int m_hot = -1;
		private bool m_active = true;

		/// <summary>Dialog-Stil wie Windows 11: kein Kasten, nur Text und ein kurzer Akzentstrich
		/// unter dem aktiven Tab (statt der abgerundeten Tabs im Hauptfenster).</summary>
		public bool Underline;

		/// <summary>So weit (bei 100 %) liegen Text, Akzentstrich und Trennlinie im Dialog-Stil
		/// höher als die Unterkante der Tabs.</summary>
		private const int LineUp = 3;

		/// <summary>Höhe der Trennlinie im Steuerelement (-1 = keine). Das übergeordnete Fenster
		/// verlängert sie links und rechts bis zum Fensterrand, wie die Linie über den Schaltflächen.</summary>
		private int m_lineY = -1;

		/// <summary>Zusätzlicher Abstand (Pixel) zwischen Tab-Leiste und Seiten. Die Seiten beginnen
		/// um diesen Wert tiefer; wer ihn setzt, macht die Registerkarten entsprechend höher.</summary>
		public int ExtraTop;
		private Control m_parent;

		public TabStripPainter(TabControl tc)
		{
			m_tc = tc;
			m_tc.HandleCreated += OnHandleCreated;
			m_tc.HandleDestroyed += OnHandleDestroyed;
			m_tc.MouseMove += OnMouseMove;
			m_tc.MouseLeave += OnMouseLeave;
			m_tc.ParentChanged += OnParentChanged;
			m_tc.LocationChanged += OnBoundsChanged;
			m_tc.SizeChanged += OnBoundsChanged;
			m_tc.VisibleChanged += OnBoundsChanged;
			AttachParent();
			if(m_tc.IsHandleCreated) AssignHandle(m_tc.Handle);
		}

		private void AttachParent()
		{
			if(m_parent != null) m_parent.Paint -= OnParentPaint;
			m_parent = m_tc.Parent;
			if(m_parent != null) m_parent.Paint += OnParentPaint;
		}

		private void OnParentChanged(object sender, EventArgs e)
		{
			AttachParent();
		}

		private void OnBoundsChanged(object sender, EventArgs e)
		{
			InvalidateParentLine();
		}

		private void InvalidateParentLine()
		{
			try
			{
				if((m_parent != null) && !m_parent.IsDisposed) m_parent.Invalidate();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Trennlinie links und rechts neben den Registerkarten bis zum Rand fortsetzen.</summary>
		private void OnParentPaint(object sender, PaintEventArgs e)
		{
			if(!m_active || !this.Underline || (m_p == null) || (m_lineY < 0) || !m_tc.Visible) return;
			try
			{
				Control par = (sender as Control);
				if(par == null) return;
				int y = m_tc.Top + m_lineY;
				using(Pen p = new Pen(m_p.Border)) { e.Graphics.DrawLine(p, 0, y, par.ClientSize.Width, y); }
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		public void Update(Palette p, float scale, Font font, Font fontSel)
		{
			m_p = p;
			m_s = scale;
			m_font = font;
			m_fontSel = fontSel;
			m_active = true;
			m_tc.Invalidate();
		}

		public void Detach()
		{
			m_active = false;
			m_tc.HandleCreated -= OnHandleCreated;
			m_tc.HandleDestroyed -= OnHandleDestroyed;
			m_tc.MouseMove -= OnMouseMove;
			m_tc.MouseLeave -= OnMouseLeave;
			m_tc.ParentChanged -= OnParentChanged;
			m_tc.LocationChanged -= OnBoundsChanged;
			m_tc.SizeChanged -= OnBoundsChanged;
			m_tc.VisibleChanged -= OnBoundsChanged;
			if(m_parent != null) { m_parent.Paint -= OnParentPaint; m_parent.Invalidate(); m_parent = null; }
			m_lineY = -1;
			this.ExtraTop = 0;
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
			m_tc.Invalidate();
		}

		private void OnHandleCreated(object sender, EventArgs e)
		{
			if(this.Handle == IntPtr.Zero) AssignHandle(m_tc.Handle);
		}

		private void OnHandleDestroyed(object sender, EventArgs e)
		{
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			int hot = -1;
			for(int i = 0; i < m_tc.TabCount; ++i)
			{
				if(m_tc.GetTabRect(i).Contains(e.Location)) { hot = i; break; }
			}
			if(hot != m_hot)
			{
				m_hot = hot;
				m_tc.Invalidate();
			}
		}

		private void OnMouseLeave(object sender, EventArgs e)
		{
			if(m_hot >= 0)
			{
				m_hot = -1;
				m_tc.Invalidate();
			}
		}

		/// <summary>Unterkante der Tab-Leiste (0, wenn noch nicht bekannt).</summary>
		private int StripBottom()
		{
			int strip = 0;
			try
			{
				if(!m_tc.IsHandleCreated) return 0;
				for(int i = 0; i < m_tc.TabCount; ++i) strip = Math.Max(strip, m_tc.GetTabRect(i).Bottom);
			}
			catch(Exception) { return 0; }
			return strip;
		}

		/// <summary>Windows Forms merkt sich den Bereich der Seiten und rechnet ihn nur bei einer
		/// Größenänderung neu aus. Wird die Leiste nachträglich höher (ein Plugin setzt Symbole),
		/// läge die Seite sonst über der Trennlinie – deshalb die Größe einmal kurz anstoßen.</summary>
		private void Relayout()
		{
			try
			{
				if(m_tc.IsDisposed || !m_tc.IsHandleCreated) return;
				Size sz = m_tc.Size;
				m_tc.Size = new Size(sz.Width + 1, sz.Height);
				m_tc.Size = sz;
				m_tc.Invalidate();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		protected override void WndProc(ref Message m)
		{
			if(m_active && this.Underline && (this.ExtraTop > 0) && (m.Msg == TCM_ADJUSTRECT) && (m.LParam != IntPtr.Zero))
			{
				base.WndProc(ref m);
				try
				{
					RECT rc = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
					// wParam 0: Fenster -> Seitenbereich (Seiten tiefer), sonst umgekehrt
					if(m.WParam == IntPtr.Zero) rc.Top += this.ExtraTop;
					else rc.Top -= this.ExtraTop;
					Marshal.StructureToPtr(rc, m.LParam, false);
				}
				catch(Exception ex) { FluentThemeExt.Report(ex); }
				return;
			}
			if(m_active && ((m.Msg == TCM_SETIMAGELIST) || (m.Msg == TCM_SETITEMA) || (m.Msg == TCM_SETITEMW) ||
				(m.Msg == TCM_INSERTITEMA) || (m.Msg == TCM_INSERTITEMW) || (m.Msg == TCM_DELETEITEM) ||
				(m.Msg == TCM_DELETEALLITEMS)))
			{
				// Windows erklärt danach nur einen Teil der Leiste für ungültig; die Trennlinie liefe
				// dann nur bis zum neuen Tab. Deshalb alles neu zeichnen.
				int before = StripBottom();
				base.WndProc(ref m);
				try
				{
					m_tc.Invalidate();
					int after = StripBottom();
					if((before > 0) && (after > 0) && (before != after) && m_tc.IsHandleCreated)
						m_tc.BeginInvoke(new MethodInvoker(Relayout));
				}
				catch(Exception ex) { FluentThemeExt.Report(ex); }
				return;
			}
			if(m_active && (m_p != null) && (m_font != null))
			{
				if(m.Msg == WM_ERASEBKGND)
				{
					m.Result = new IntPtr(1);
					return;
				}
				if(m.Msg == WM_PAINT)
				{
					PAINTSTRUCT ps;
					IntPtr hdc = BeginPaint(m.HWnd, out ps);
					try { PaintAll(hdc); }
					catch(Exception ex) { FluentThemeExt.Report(ex); }
					finally { EndPaint(m.HWnd, ref ps); }
					m.Result = IntPtr.Zero;
					return;
				}
			}
			base.WndProc(ref m);
		}

		private int TabImage(int i)
		{
			try
			{
				ImageList il = m_tc.ImageList;
				if(il == null) return -1;
				TabPage tp = m_tc.TabPages[i];
				int idx = tp.ImageIndex;
				if((idx < 0) && !string.IsNullOrEmpty(tp.ImageKey)) idx = il.Images.IndexOfKey(tp.ImageKey);
				if((idx < 0) || (idx >= il.Images.Count)) return -1;
				return idx;
			}
			catch(Exception) { return -1; }
		}

		private const int IconGap = 4;

		private bool IconsFit(Graphics g, TextFormatFlags tf)
		{
			try
			{
				if(m_tc.ImageList == null) return false;
				int iw = m_tc.ImageList.ImageSize.Width + D(IconGap);
				for(int i = 0; i < m_tc.TabCount; ++i)
				{
					if(TabImage(i) < 0) continue;
					Rectangle r = m_tc.GetTabRect(i);
					int tw = TextRenderer.MeasureText(g, m_tc.TabPages[i].Text, m_fontSel, new Size(int.MaxValue, r.Height), tf).Width;
					if(iw + tw > r.Width - D(2)) return false;
				}
				return true;
			}
			catch(Exception) { return false; }
		}

		private void PaintUnderline(Graphics g)
		{
			const TextFormatFlags tf = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
				TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
			int strip = 0;
			// Symbole nur zeigen, wenn bei allen Tabs Symbol und voller Text Platz haben (gemessen mit
			// der halbfetten Schrift des aktiven Tabs). Sonst – z. B. in den Optionen mit vielen Tabs –
			// lieber nur den vollständigen Text, wie bisher.
			bool icons = IconsFit(g, tf);
			for(int i = 0; i < m_tc.TabCount; ++i)
			{
				Rectangle r = m_tc.GetTabRect(i);
				strip = Math.Max(strip, r.Bottom);
				bool sel = (i == m_tc.SelectedIndex);
				bool hot = (i == m_hot);

				// Text oberhalb der Mitte, darunter der Akzentstrich – mit etwas Luft nach unten,
				// damit der Strich nicht direkt auf der Trennlinie sitzt. Alles samt Trennlinie um
				// LineUp nach oben, damit zwischen Linie und Karte darunter mehr Abstand bleibt.
				Rectangle tr = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom - D(9) - D(LineUp));
				if(hot && !sel)
					Gfx.Fill(g, Rectangle.FromLTRB(r.Left + D(2), r.Top + D(1), r.Right - D(2), r.Bottom - D(9) - D(LineUp)), D(4),
						Palette.Blend(m_p.Bg, m_p.Text, m_p.Dark ? 0.08 : 0.05));
				string text = m_tc.TabPages[i].Text;
				Font tfnt = (sel ? m_fontSel : m_font);
				// Symbol eines Tabs (setzen manche Plugins): links vom Text, beides zusammen mittig
				int img = (icons ? TabImage(i) : -1);
				if(img >= 0)
				{
					Size isz = m_tc.ImageList.ImageSize;
					Size ts = TextRenderer.MeasureText(g, text, tfnt, tr.Size, tf);
					int gap = D(IconGap);
					int total = Math.Min(tr.Width, isz.Width + gap + ts.Width);
					int x = tr.Left + (tr.Width - total) / 2;
					m_tc.ImageList.Draw(g, x, tr.Top + (tr.Height - isz.Height) / 2, img);
					tr = Rectangle.FromLTRB(x + isz.Width + gap, tr.Top, Math.Max(x + isz.Width + gap, x + total), tr.Bottom);
				}
				TextRenderer.DrawText(g, text, tfnt, tr, sel ? m_p.Text : m_p.Sub, tf);

				if(sel)
				{
					int w = D(16), h = Math.Max(2, D(3));
					Gfx.Fill(g, new Rectangle(r.Left + (r.Width - w) / 2, r.Bottom - D(7) - D(LineUp) - h, w, h), h / 2, m_p.Pill);
				}
			}
			// feine Trennlinie unter der Tab-Leiste
			int ly = -1;
			if(strip > 0)
			{
				ly = strip - D(LineUp);
				using(Pen p = new Pen(m_p.Border)) { g.DrawLine(p, 0, ly, m_tc.ClientSize.Width, ly); }
			}
			// Position hat sich geändert: Fortsetzung im Fenster neu zeichnen
			if(ly != m_lineY)
			{
				m_lineY = ly;
				InvalidateParentLine();
			}
		}

		private void PaintAll(IntPtr hdc)
		{
			Rectangle cr = m_tc.ClientRectangle;
			if((cr.Width <= 0) || (cr.Height <= 0)) return;

			using(BufferedGraphics bg = BufferedGraphicsManager.Current.Allocate(hdc, cr))
			{
				Graphics g = bg.Graphics;
				g.Clear(m_p.Bg);

				if(this.Underline)
				{
					PaintUnderline(g);
					bg.Render();
					return;
				}

				for(int i = 0; i < m_tc.TabCount; ++i)
				{
					Rectangle r = m_tc.GetTabRect(i);
					bool sel = (i == m_tc.SelectedIndex);
					// KeePass legt die Höhe der Leiste fest; die Tabs können unten darüber hinausragen.
					// Die Fläche deshalb an der sichtbaren Höhe ausrichten, oben und unten gleicher Abstand.
					int bottom = Math.Min(r.Bottom, m_tc.ClientSize.Height) - D(3);
					Rectangle pill = Rectangle.FromLTRB(r.Left + D(2), r.Top + D(3), r.Right - D(2), bottom);

					// Aktive Datenbank blau hinterlegt wie die Auswahl in Liste und Gruppenbaum
					if(sel) Gfx.Fill(g, pill, D(6), m_p.Sel);
					else if(i == m_hot)
						Gfx.Fill(g, pill, D(6), Palette.Blend(m_p.Bg, m_p.Text, m_p.Dark ? 0.08 : 0.05));

					string text = m_tc.TabPages[i].Text;
					TextRenderer.DrawText(g, text, sel ? m_fontSel : m_font, pill, sel ? m_p.Text : m_p.Sub,
						TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
						TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
				}

				bg.Render();
			}
		}
	}
}
