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

		public TabStripPainter(TabControl tc)
		{
			m_tc = tc;
			m_tc.HandleCreated += OnHandleCreated;
			m_tc.HandleDestroyed += OnHandleDestroyed;
			m_tc.MouseMove += OnMouseMove;
			m_tc.MouseLeave += OnMouseLeave;
			if(m_tc.IsHandleCreated) AssignHandle(m_tc.Handle);
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

		protected override void WndProc(ref Message m)
		{
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

		private void PaintUnderline(Graphics g)
		{
			const TextFormatFlags tf = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
				TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
			int strip = 0;
			for(int i = 0; i < m_tc.TabCount; ++i)
			{
				Rectangle r = m_tc.GetTabRect(i);
				strip = Math.Max(strip, r.Bottom);
				bool sel = (i == m_tc.SelectedIndex);
				bool hot = (i == m_hot);

				// Text etwas oberhalb der Mitte, darunter Platz für den Akzentstrich
				Rectangle tr = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom - D(5));
				if(hot && !sel)
					Gfx.Fill(g, Rectangle.FromLTRB(r.Left + D(2), r.Top + D(2), r.Right - D(2), r.Bottom - D(6)), D(4),
						Palette.Blend(m_p.Bg, m_p.Text, m_p.Dark ? 0.08 : 0.05));
				string text = m_tc.TabPages[i].Text;
				TextRenderer.DrawText(g, text, sel ? m_fontSel : m_font, tr, sel ? m_p.Text : m_p.Sub, tf);

				if(sel)
				{
					int w = D(16), h = Math.Max(2, D(3));
					Gfx.Fill(g, new Rectangle(r.Left + (r.Width - w) / 2, r.Bottom - D(4), w, h), h / 2, m_p.Pill);
				}
			}
			// feine Trennlinie unter der Tab-Leiste
			if(strip > 0)
				using(Pen p = new Pen(m_p.Border)) { g.DrawLine(p, 0, strip, m_tc.ClientSize.Width, strip); }
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
					Rectangle pill = Rectangle.FromLTRB(r.Left + D(2), r.Top + D(2), r.Right - D(2), r.Bottom - D(1));

					if(sel)
					{
						Gfx.Fill(g, pill, D(6), m_p.Card);
						Gfx.Stroke(g, pill, D(6), m_p.Border);
						Gfx.Fill(g, new Rectangle(pill.Left + pill.Width / 2 - D(10), pill.Bottom - D(4), D(20), D(3)),
							D(1), m_p.Pill);
					}
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