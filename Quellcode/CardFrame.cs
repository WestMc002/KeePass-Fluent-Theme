using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Macht aus einem Container (z. B. SplitterPanel) eine weiße Karte mit
	/// abgerundetem Rahmen und Überschrift, wie in „Fensterpositionen“.</summary>
	internal sealed class CardFrame
	{
		private readonly Control m_host;
		private Palette m_p;
		private float m_s = 1f;
		private Font m_fTitle;
		private string m_title = string.Empty;
		private string m_sub = string.Empty;
		private bool m_attached;

		public CardFrame(Control host)
		{
			m_host = host;
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		private int Margin { get { return D(6); } }
		private int Inner { get { return D(6); } }
		private int HeaderH { get { return D(34); } }

		public void Apply(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;

			if(m_fTitle != null) m_fTitle.Dispose();
			float sz = m_host.Font.SizeInPoints;
			try { m_fTitle = new Font("Segoe UI Semibold", sz); }
			catch(ArgumentException) { m_fTitle = new Font(m_host.Font, FontStyle.Bold); }

			if(!m_attached)
			{
				m_attached = true;
				m_host.Paint += OnPaint;
				m_host.Resize += OnResize;
				try
				{
					PropertyInfo pi = typeof(Control).GetProperty("DoubleBuffered",
						BindingFlags.Instance | BindingFlags.NonPublic);
					if(pi != null) pi.SetValue(m_host, true, null);
				}
				catch(Exception ex) { FluentThemeExt.Report(ex); }
			}

			m_host.BackColor = p.Bg;
			int m = this.Margin;
			int i = this.Inner;
			m_host.Padding = new Padding(m + i, m + this.HeaderH, m + i, m + i);
			m_host.Invalidate();
		}

		public void Detach()
		{
			if(!m_attached) return;
			m_attached = false;
			m_host.Paint -= OnPaint;
			m_host.Resize -= OnResize;
			m_host.Padding = Padding.Empty;
			m_host.Invalidate();
		}

		public void SetHeader(string title, string sub)
		{
			title = (title ?? string.Empty);
			sub = (sub ?? string.Empty);
			if((title == m_title) && (sub == m_sub)) return;
			m_title = title;
			m_sub = sub;
			int m = this.Margin;
			m_host.Invalidate(new Rectangle(m, m, m_host.ClientSize.Width - 2 * m, this.HeaderH));
		}

		private void OnResize(object sender, EventArgs e)
		{
			m_host.Invalidate();
		}

		private void OnPaint(object sender, PaintEventArgs e)
		{
			if((m_p == null) || !m_attached) return;
			try
			{
				Graphics g = e.Graphics;
				int m = this.Margin;
				Rectangle card = new Rectangle(m, m, m_host.ClientSize.Width - 2 * m,
					m_host.ClientSize.Height - 2 * m);
				if((card.Width <= 0) || (card.Height <= 0)) return;

				Gfx.Fill(g, card, D(8), m_p.Card);
				Gfx.Stroke(g, card, D(8), m_p.Border);

				Rectangle head = new Rectangle(card.Left + D(14), card.Top, card.Width - D(28), this.HeaderH);
				TextFormatFlags tf = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
					TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
				TextRenderer.DrawText(g, m_title, m_fTitle, head, m_p.Text, tf);

				if(m_sub.Length > 0)
				{
					int tw = TextRenderer.MeasureText(g, m_title, m_fTitle, Size.Empty,
						TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
					Rectangle rs = new Rectangle(head.Left + tw + D(14), head.Top,
						Math.Max(0, head.Right - head.Left - tw - D(14)), head.Height);
					TextRenderer.DrawText(g, m_sub, m_host.Font, rs, m_p.Sub, tf);
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}
	}
}