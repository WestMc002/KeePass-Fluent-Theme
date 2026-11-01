// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

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

		/// <summary>Container, auf dem die Karte gezeichnet wird.</summary>
		public Control Host
		{
			get { return m_host; }
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		/// <summary>Abstand der Überschrift vom linken Kartenrand (bei 100 %). Der Gruppenbaum
		/// setzt ihn so, dass „Gruppen“ über dem Symbol der obersten Gruppe beginnt.</summary>
		public int TitleIndent = 14;

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

		/// <summary>Abstand vom oberen Rand der Textzeile bis zur Grundlinie (in Pixeln).</summary>
		private static int Ascent(Graphics g, Font f)
		{
			FontFamily ff = f.FontFamily;
			float em = ff.GetEmHeight(f.Style);
			if(em <= 0) return (int)Math.Round(f.Height * 0.8f);
			float px = f.SizeInPoints * g.DpiY / 72f;
			return (int)Math.Round(px * ff.GetCellAscent(f.Style) / em);
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

				Rectangle head = new Rectangle(card.Left + D(this.TitleIndent), card.Top,
					card.Width - D(this.TitleIndent) - D(14), this.HeaderH);
				TextFormatFlags tf = TextFormatFlags.Left | TextFormatFlags.Top |
					TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

				// Überschrift senkrecht mittig; der graue Zusatz (z. B. Datenbankname) steht auf
				// derselben Grundlinie – einzeln zentriert säße er wegen der anderen Schrift höher
				Font fs = m_host.Font;
				int titleTop = head.Top + (head.Height - m_fTitle.Height) / 2;
				int baseline = titleTop + Ascent(g, m_fTitle);
				TextRenderer.DrawText(g, m_title, m_fTitle,
					new Rectangle(head.Left, titleTop, head.Width, m_fTitle.Height), m_p.Text, tf);

				if(m_sub.Length > 0)
				{
					int tw = TextRenderer.MeasureText(g, m_title, m_fTitle, Size.Empty,
						TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
					int subTop = baseline - Ascent(g, fs);
					Rectangle rs = new Rectangle(head.Left + tw + D(14), subTop,
						Math.Max(0, head.Right - head.Left - tw - D(14)), fs.Height);
					TextRenderer.DrawText(g, m_sub, fs, rs, m_p.Sub, tf);
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}
	}
}
