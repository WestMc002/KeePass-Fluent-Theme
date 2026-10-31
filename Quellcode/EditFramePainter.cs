// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Einheitlicher Rahmen für Eingabefelder in Dialogen (Textfelder, Kommentarfeld,
	/// Datumsfeld) wie beim Suchfeld: heller Rahmen mit runden Ecken, unten eine etwas dunklere
	/// Linie und bei Fokus eine Akzentlinie. Textfelder bekommen links und rechts etwas Innenabstand.
	/// Gezeichnet wird nur im schmalen Rand (Nicht-Client-Bereich), Scrollleisten bleiben unberührt.</summary>
	internal sealed class EditFramePainter : NativeWindow
	{
		private const int WM_PAINT = 0x000F;
		private const int WM_NCPAINT = 0x0085;
		private const int WM_SETFOCUS = 0x0007;
		private const int WM_KILLFOCUS = 0x0008;
		private const int WM_ENABLE = 0x000A;
		private const int SM_CXEDGE = 45;
		private const int SM_CYEDGE = 46;
		private const uint RDW_INVALIDATE = 0x0001;
		private const uint RDW_FRAME = 0x0400;

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct DATETIMEPICKERINFO
		{
			public int cbSize;
			public RECT rcCheck;
			public int stateCheck;
			public RECT rcButton;
			public int stateButton;
			public IntPtr hwndEdit;
			public IntPtr hwndUD;
			public IntPtr hwndDropDown;
		}

		private const int DTM_GETDATETIMEPICKERINFO = 0x100E;
		private const int SM_CXVSCROLL = 2;

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		private static extern IntPtr SendDtpInfo(IntPtr hWnd, int msg, IntPtr wParam, ref DATETIMEPICKERINFO info);

		[DllImport("user32.dll")]
		private static extern IntPtr GetWindowDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(int nIndex);

		[DllImport("user32.dll")]
		private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

		private const int EM_SETMARGINS = 0x00D3;
		private const int EC_LEFTMARGIN = 0x0001, EC_RIGHTMARGIN = 0x0002;

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		private readonly Control m_c;
		private Palette m_p;
		private float m_s = 1f;

		/// <summary>Felder, die einen eigenen Rahmen brauchen.</summary>
		public static bool Wants(Control c)
		{
			RichTextBox rtb = (c as RichTextBox);
			if(rtb != null) return (rtb.BorderStyle != BorderStyle.None);
			TextBox tb = (c as TextBox);
			if(tb != null) return (tb.BorderStyle != BorderStyle.None);
			return (c is DateTimePicker);
		}

		public EditFramePainter(Control c)
		{
			m_c = c;
			c.HandleCreated += OnHandleCreated;
			c.HandleDestroyed += OnHandleDestroyed;
			if(c.IsHandleCreated && (this.Handle == IntPtr.Zero)) AssignHandle(c.Handle);
		}

		public void Update(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
			SetMargins();
			Redraw();
		}

		/// <summary>Text nicht direkt am Rahmen: links und rechts je 4 px (skaliert) Innenabstand.</summary>
		private void SetMargins()
		{
			if(!(m_c is TextBox) || !m_c.IsHandleCreated) return;
			int px = (int)Math.Round(4 * m_s);
			SendMessage(m_c.Handle, EM_SETMARGINS, new IntPtr(EC_LEFTMARGIN | EC_RIGHTMARGIN),
				new IntPtr((px & 0xFFFF) | ((px & 0xFFFF) << 16)));
		}

		public void Detach()
		{
			m_c.HandleCreated -= OnHandleCreated;
			m_c.HandleDestroyed -= OnHandleDestroyed;
			IntPtr h = this.Handle;
			if(h != IntPtr.Zero)
			{
				ReleaseHandle();
				RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE);
			}
		}

		private void OnHandleCreated(object sender, EventArgs e)
		{
			if(this.Handle == IntPtr.Zero) AssignHandle(m_c.Handle);
			SetMargins();
			Redraw();
		}

		private void OnHandleDestroyed(object sender, EventArgs e)
		{
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
		}

		private void Redraw()
		{
			if(this.Handle != IntPtr.Zero)
				RedrawWindow(this.Handle, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE);
		}

		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);
			try
			{
				// Das Datumsfeld zeichnet seinen Rahmen selbst im Inhalt (ohne Nicht-Client-Rand) –
				// dort nach jedem Neuzeichnen darüber malen
				if((m.Msg == WM_NCPAINT) || ((m.Msg == WM_PAINT) && !HasNcBorder())) Paint();
				else if((m.Msg == WM_SETFOCUS) || (m.Msg == WM_KILLFOCUS) || (m.Msg == WM_ENABLE)) Redraw();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Bereich des Aufklapp-Knopfs im Datumsfeld (Fenster- = Client-Koordinaten,
		/// weil das Datumsfeld keinen Nicht-Client-Rand hat).</summary>
		private Rectangle DtpButton(int w)
		{
			try
			{
				DATETIMEPICKERINFO info = new DATETIMEPICKERINFO();
				info.cbSize = Marshal.SizeOf(typeof(DATETIMEPICKERINFO));
				SendDtpInfo(this.Handle, DTM_GETDATETIMEPICKERINFO, IntPtr.Zero, ref info);
				RECT r = info.rcButton;
				if((r.Right > r.Left) && (r.Bottom > r.Top))
					return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			int bw = GetSystemMetrics(SM_CXVSCROLL);
			return new Rectangle(w - bw - 2, 0, bw, 1);
		}

		private bool HasNcBorder()
		{
			RECT wr, cr;
			if(!GetWindowRect(this.Handle, out wr) || !GetClientRect(this.Handle, out cr)) return true;
			return ((wr.Bottom - wr.Top) - (cr.Bottom - cr.Top) >= 2);
		}

		private void Paint()
		{
			Palette p = m_p;
			if((p == null) || (this.Handle == IntPtr.Zero)) return;
			RECT wr, cr;
			if(!GetWindowRect(this.Handle, out wr) || !GetClientRect(this.Handle, out cr)) return;
			int w = wr.Right - wr.Left, h = wr.Bottom - wr.Top;
			// Randbreite: tatsächlicher Nicht-Client-Rand (1 px bei FixedSingle, 2 px bei Fixed3D);
			// ohne Rand (Datumsfeld) wird über den Inhalt gemalt
			int nc = (h - (cr.Bottom - cr.Top)) / 2;
			int by = ((nc >= 1) ? Math.Min(nc, Math.Max(1, GetSystemMetrics(SM_CYEDGE))) :
				Math.Max(1, GetSystemMetrics(SM_CYEDGE)));
			int bx = by;
			if((w <= 2 * bx) || (h <= 2 * by)) return;

			IntPtr hdc = GetWindowDC(this.Handle);
			if(hdc == IntPtr.Zero) return;
			try
			{
				using(Graphics g = Graphics.FromHdc(hdc))
				{
					// Nur den Rand bemalen, Inhalt und Scrollleisten bleiben unberührt
					g.SetClip(new Rectangle(bx, by, w - 2 * bx, h - 2 * by), CombineMode.Exclude);
					Rectangle all = new Rectangle(0, 0, w, h);
					Color outside = ((m_c.Parent != null) ? m_c.Parent.BackColor : p.Bg);
					using(SolidBrush b = new SolidBrush(outside)) { g.FillRectangle(b, all); }

					int rad = Math.Min(4, bx + 2);
					Color fill = ((m_c is DateTimePicker) ? DialogStyler.FieldBg(p) : m_c.BackColor);
					Gfx.Fill(g, all, rad, fill);
					Gfx.Stroke(g, all, rad, p.Border);

					// Untere Linie wie bei den einzeiligen Feldern, bei Fokus in Akzentfarbe
					bool focus = m_c.ContainsFocus;
					Color line = (focus ? p.Accent : Palette.Blend(p.Border, p.Sub, 0.55));
					int lh = (focus ? Math.Min(2, by) : 1);
					using(SolidBrush b = new SolidBrush(line))
						g.FillRectangle(b, rad, h - lh, w - 2 * rad, lh);

					// Datumsfeld: statt des alten Kalender-Knopfs ein feiner Pfeil wie im Suchfeld
					DateTimePicker dtp = (m_c as DateTimePicker);
					if((dtp != null) && !dtp.ShowUpDown)
					{
						g.ResetClip();
						Rectangle btn = DtpButton(w);
						if(!btn.IsEmpty)
						{
							float s = g.DpiX / 96f;
							Rectangle rb = Rectangle.FromLTRB(Math.Max(bx, btn.Left), by, w - bx, h - by - 1);
							using(SolidBrush b = new SolidBrush(fill)) { g.FillRectangle(b, rb); }

							// Datum selbst zeichnen: Feldfarbe statt Weiß und Abstand zum Rand. Während
							// der Eingabe (Fokus) bleibt Windows' Darstellung mit markiertem Teil.
							if(!focus)
							{
								Rectangle rt = Rectangle.FromLTRB(bx, by, rb.Left, h - by - 1);
								using(SolidBrush b = new SolidBrush(fill)) { g.FillRectangle(b, rt); }
								rt = Rectangle.FromLTRB(rt.Left + (int)Math.Round(4 * s), rt.Top, rt.Right, rt.Bottom);
								TextRenderer.DrawText(g, dtp.Text, dtp.Font, rt, dtp.Enabled ? p.Text : p.Sub,
									TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
									TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
							}
							Gfx.Chevron(g, rb.Left + rb.Width / 2f, by + (h - 2 * by) / 2f, 7f * s, false,
								m_c.Enabled ? p.Sub : Palette.Blend(p.Sub, fill, 0.5), Math.Max(1f, 1.1f * s));
						}
					}
				}
			}
			finally { ReleaseDC(this.Handle, hdc); }
		}
	}
}
