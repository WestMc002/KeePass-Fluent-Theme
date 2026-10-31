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
	/// <summary>Suchfeld der Symbolleiste im Windows-11-Stil: abgerundeter Rahmen, schlichter Pfeil
	/// statt des Windows-Pfeilkastens, Akzentlinie unten bei Fokus. Das Textfeld selbst zeichnet
	/// Windows weiterhin (eigenes Fenster), hier wird nur alles drumherum gezeichnet.</summary>
	internal sealed class SearchBoxPainter : NativeWindow
	{
		private const int WM_PAINT = 0x000F;
		private const int WM_ERASEBKGND = 0x0014;

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

		[StructLayout(LayoutKind.Sequential)]
		private struct COMBOBOXINFO
		{
			public int cbSize;
			public RECT rcItem;
			public RECT rcButton;
			public int stateButton;
			public IntPtr hwndCombo;
			public IntPtr hwndItem;
			public IntPtr hwndList;
		}

		private const uint RDW_INVALIDATE = 0x0001, RDW_ALLCHILDREN = 0x0080, RDW_FRAME = 0x0400;

		[DllImport("user32.dll")]
		private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

		[DllImport("user32.dll")]
		private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

		[DllImport("user32.dll")]
		private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

		[DllImport("user32.dll")]
		private static extern bool GetComboBoxInfo(IntPtr hwndCombo, ref COMBOBOXINFO pcbi);

		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		private const int CB_SETITEMHEIGHT = 0x0153, CB_GETITEMHEIGHT = 0x0154;

		[DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
			uint uFlags);

		private readonly ComboBox m_cb;
		private Palette m_p;
		private float m_s = 1f;
		private Color m_back = Color.Empty;
		private bool m_hotButton;

		public SearchBoxPainter(ComboBox cb)
		{
			m_cb = cb;
			cb.HandleCreated += OnHandleCreated;
			cb.HandleDestroyed += OnHandleDestroyed;
			cb.Enter += OnFocusChanged;
			cb.Leave += OnFocusChanged;
			cb.DropDown += OnFocusChanged;
			cb.DropDown += OnDropDown;
			cb.DropDownClosed += OnFocusChanged;
			cb.MouseMove += OnMouseMove;
			cb.MouseLeave += OnMouseLeave;
			cb.DrawItem += OnDrawItem;
			cb.EnabledChanged += OnEnabledChanged;
			// Das Umstellen legt das Feld neu an – dabei hängt sich OnHandleCreated schon ein.
			// Deshalb danach nur einhängen, wenn noch nicht geschehen (sonst Ausnahme).
			cb.DrawMode = DrawMode.OwnerDrawFixed;
			if(cb.IsHandleCreated && (this.Handle == IntPtr.Zero)) AssignHandle(cb.Handle);
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		/// <param name="back">Hintergrund rund um das Feld (Farbe der Symbolleiste).</param>
		public void Update(Palette p, float scale, Color back)
		{
			bool changed = ((m_p != p) || (m_s != scale) || (m_back != back));
			m_p = p;
			m_s = scale;
			m_back = back;
			// Höhere Zeilen nur in der Liste – das Suchfeld selbst behält seine Höhe
			int ih = Math.Max(m_cb.Font.Height + D(10), D(28));
			if((m_cb.ItemHeight != ih) && m_cb.IsHandleCreated)
			{
				IntPtr selH = SendMessage(m_cb.Handle, CB_GETITEMHEIGHT, new IntPtr(-1), IntPtr.Zero);
				m_cb.ItemHeight = ih;
				if(selH.ToInt32() > 0) SendMessage(m_cb.Handle, CB_SETITEMHEIGHT, new IntPtr(-1), selH);
			}
			CenterEdit();
			if(changed) m_cb.Invalidate();
		}

		public void Detach()
		{
			m_cb.HandleCreated -= OnHandleCreated;
			m_cb.HandleDestroyed -= OnHandleDestroyed;
			m_cb.Enter -= OnFocusChanged;
			m_cb.Leave -= OnFocusChanged;
			m_cb.DropDown -= OnFocusChanged;
			m_cb.DropDown -= OnDropDown;
			m_cb.DropDownClosed -= OnFocusChanged;
			m_cb.MouseMove -= OnMouseMove;
			m_cb.MouseLeave -= OnMouseLeave;
			m_cb.DrawItem -= OnDrawItem;
			m_cb.EnabledChanged -= OnEnabledChanged;
			m_cb.DrawMode = DrawMode.Normal;
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
			m_cb.Invalidate();
		}

		private void OnHandleCreated(object sender, EventArgs e)
		{
			if(this.Handle == IntPtr.Zero) AssignHandle(m_cb.Handle);
			CenterEdit();
		}

		private void OnHandleDestroyed(object sender, EventArgs e)
		{
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
		}

		private void OnFocusChanged(object sender, EventArgs e)
		{
			m_cb.Invalidate();
		}

		/// <summary>Nach Sperren/Entsperren (KeePass schaltet das Feld ab und wieder an) neu zeichnen,
		/// samt Eingabefeld darin.</summary>
		private void OnEnabledChanged(object sender, EventArgs e)
		{
			if(this.Handle == IntPtr.Zero) return;
			RedrawWindow(this.Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_FRAME | RDW_ALLCHILDREN);
		}

		/// <summary>Liste der früheren Suchen: leer gar nicht erst öffnen, sonst abgerundet,
		/// mit Windows-11-Schatten und etwas Abstand unter dem Feld.</summary>
		private void OnDropDown(object sender, EventArgs e)
		{
			if(m_p == null) return;
			try
			{
				m_cb.BeginInvoke(new MethodInvoker(delegate
				{
					try
					{
						if(m_cb.Items.Count == 0)
						{
							m_cb.DroppedDown = false;
							return;
						}
						COMBOBOXINFO ci = new COMBOBOXINFO();
						ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
						if(!GetComboBoxInfo(m_cb.Handle, ref ci) || (ci.hwndList == IntPtr.Zero)) return;

						NativeMethods.RemoveClassDropShadow(ci.hwndList);
						NativeMethods.SetPopupStyle(ci.hwndList, m_p.Border);

						RECT rl, rc;
						if(GetWindowRect(ci.hwndList, out rl) && GetWindowRect(m_cb.Handle, out rc) &&
							(rl.Top >= rc.Bottom - 1)) // nur wenn die Liste unter dem Feld aufgeht
						{
							const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
							SetWindowPos(ci.hwndList, IntPtr.Zero, rc.Left, rc.Bottom + D(4), 0, 0,
								SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
						}
					}
					catch(Exception ex) { FluentThemeExt.Report(ex); }
				}));
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Einträge der Liste: Abstand links, Auswahl als helle abgerundete Fläche mit
		/// Akzentstreifen (wie Windows-11-Listen) statt des blauen Balkens.</summary>
		private void OnDrawItem(object sender, DrawItemEventArgs e)
		{
			if((m_p == null) || (e.Index < 0)) return;
			try
			{
				Graphics g = e.Graphics;
				Rectangle r = e.Bounds;
				using(SolidBrush b = new SolidBrush(m_p.Card)) { g.FillRectangle(b, r); }

				bool sel = ((e.State & DrawItemState.Selected) != 0);
				if(sel)
				{
					Rectangle hr = Rectangle.FromLTRB(r.Left + D(4), r.Top + 1, r.Right - D(4), r.Bottom - 1);
					Gfx.Fill(g, hr, D(4), m_p.CardHover);
					int ph = Math.Max(D(8), Math.Min(D(16), r.Height - D(10)));
					Gfx.Fill(g, new Rectangle(hr.Left, r.Top + (r.Height - ph) / 2, D(3), ph), D(2), m_p.Pill);
				}

				string text = m_cb.GetItemText(m_cb.Items[e.Index]);
				Rectangle tr = Rectangle.FromLTRB(r.Left + D(14), r.Top, r.Right - D(8), r.Bottom);
				TextRenderer.DrawText(g, text, m_cb.Font, tr, m_p.Text, TextFormatFlags.Left |
					TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
					TextFormatFlags.NoPrefix);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			bool hot = ButtonRect().Contains(e.Location);
			if(hot != m_hotButton)
			{
				m_hotButton = hot;
				m_cb.Invalidate();
			}
		}

		private void OnMouseLeave(object sender, EventArgs e)
		{
			if(!m_hotButton) return;
			m_hotButton = false;
			m_cb.Invalidate();
		}

		private Rectangle ButtonRect()
		{
			if(!m_cb.IsHandleCreated) return Rectangle.Empty;
			COMBOBOXINFO ci = new COMBOBOXINFO();
			ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
			if(!GetComboBoxInfo(m_cb.Handle, ref ci)) return Rectangle.Empty;
			return Rectangle.FromLTRB(ci.rcButton.Left, ci.rcButton.Top, ci.rcButton.Right, ci.rcButton.Bottom);
		}

		protected override void WndProc(ref Message m)
		{
			if(m_p != null)
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
					try { Paint(hdc); }
					catch(Exception ex) { FluentThemeExt.Report(ex); }
					finally { EndPaint(m.HWnd, ref ps); }
					m.Result = IntPtr.Zero;
					return;
				}
			}
			base.WndProc(ref m);
			// Windows setzt das Textfeld bei jeder Größenänderung oben an – danach wieder mittig setzen
			if((m_p != null) && ((m.Msg == WM_SIZE) || (m.Msg == WM_WINDOWPOSCHANGED))) CenterEdit();
		}

		private const int WM_SIZE = 0x0005, WM_WINDOWPOSCHANGED = 0x0047;

		/// <summary>Textfeld (eigenes Fenster im Suchfeld) senkrecht mittig setzen, damit die Schrift
		/// nicht oben klebt.</summary>
		private void CenterEdit()
		{
			try
			{
				if(!m_cb.IsHandleCreated) return;
				COMBOBOXINFO ci = new COMBOBOXINFO();
				ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
				if(!GetComboBoxInfo(m_cb.Handle, ref ci) || (ci.hwndItem == IntPtr.Zero)) return;
				Rectangle er = EditRect();
				if(er.IsEmpty) return;
				// Höhe auf eine Textzeile begrenzen (das Textfeld schreibt immer oben in sein Fenster)
				int h = Math.Min(er.Height, m_cb.Font.Height + D(2));
				int top = (m_cb.ClientSize.Height - h) / 2;
				if((Math.Abs(top - er.Top) < 1) && (h == er.Height)) return;
				const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
				SetWindowPos(ci.hwndItem, IntPtr.Zero, er.Left, top, er.Width, h, SWP_NOZORDER | SWP_NOACTIVATE);
				m_cb.Invalidate();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void Paint(IntPtr hdc)
		{
			Rectangle cr = m_cb.ClientRectangle;
			if((cr.Width <= 0) || (cr.Height <= 0)) return;

			using(BufferedGraphics bg = BufferedGraphicsManager.Current.Allocate(hdc, cr))
			{
				Graphics g = bg.Graphics;
				g.Clear(m_back.IsEmpty ? m_p.Bg : m_back);

				bool focused = (m_cb.ContainsFocus || m_cb.DroppedDown);
				int rad = D(4);
				Rectangle box = new Rectangle(cr.Left, cr.Top, cr.Width, cr.Height);
				Gfx.Fill(g, box, rad, m_p.Card);
				Gfx.Stroke(g, box, rad, focused ? Palette.Blend(m_p.Border, m_p.Text, 0.15) : m_p.Border);

				// Pfeil-Bereich: schlichter Chevron, beim Überfahren leicht hinterlegt
				Rectangle rb = ButtonRect();
				if(!rb.IsEmpty)
				{
					Rectangle hb = Rectangle.FromLTRB(rb.Left + D(2), cr.Top + D(3), cr.Right - D(3), cr.Bottom - D(3));
					// wie das Auswahlfeld im Monitor Dimmer: nur ein feiner Pfeil, ohne Kasten
					Color cc = (m_hotButton ? m_p.Text : m_p.Sub);
					Gfx.Chevron(g, hb.Left + hb.Width / 2f, hb.Top + hb.Height / 2f, 7f * m_s, m_cb.DroppedDown,
						cc, Math.Max(1f, 1.1f * m_s));
				}

				// Fokus: Akzentlinie an der Unterkante (wie Windows-11-Eingabefelder)
				// 2 px hoch und ganz unten, damit das Textfeld (eigenes Fenster) sie nicht teilweise
				// überdeckt; auf die abgerundete Form zugeschnitten wie in Windows 11
				if(focused)
				{
					const int lh = 2;
					System.Drawing.Drawing2D.GraphicsState st = g.Save();
					using(System.Drawing.Drawing2D.GraphicsPath gp = Gfx.Round(
						new Rectangle(box.X, box.Y, box.Width - 1, box.Height - 1), rad))
					{
						g.SetClip(gp);
						g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
						using(SolidBrush b = new SolidBrush(m_p.Pill))
						{
							g.FillRectangle(b, box.Left, box.Bottom - lh - 1, box.Width, lh + 1);
						}
					}
					g.Restore(st);
				}

				// Den Bereich des Textfelds (eigenes Fenster) nicht übermalen – sonst zeichnet Windows
				// es gleich danach neu darüber und das Feld flackert
				Rectangle er = EditRect();
				using(Graphics tg = Graphics.FromHdc(hdc))
				{
					if(!er.IsEmpty) tg.ExcludeClip(er);
					bg.Render(tg);
				}
			}
		}

		private Rectangle EditRect()
		{
			if(!m_cb.IsHandleCreated) return Rectangle.Empty;
			COMBOBOXINFO ci = new COMBOBOXINFO();
			ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
			if(!GetComboBoxInfo(m_cb.Handle, ref ci) || (ci.hwndItem == IntPtr.Zero)) return Rectangle.Empty;
			RECT wr;
			if(!GetWindowRect(ci.hwndItem, out wr)) return Rectangle.Empty;
			return m_cb.RectangleToClient(Rectangle.FromLTRB(wr.Left, wr.Top, wr.Right, wr.Bottom));
		}
	}
}
