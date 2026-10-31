// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Dezente Versionsanzeige „Fluent Theme x.y“ unten rechts in der Statusleiste.
	/// Ein Klick öffnet das Info-Fenster (dort stehen Logo, Autor und Webseite).
	/// Als Plugin ist das Fluent Theme Gast in KeePass – deshalb kein Logo und keine Werbung
	/// in der Statusleiste; über Fluent-Design › Version in der Statusleiste ausblendbar.</summary>
	internal sealed class FooterBadge : Control
	{
		private static Image s_logo;
		private static bool s_logoLoaded;

		private readonly ToolTip m_tip = new ToolTip();
		private Palette m_p;
		private float m_s = 1f;
		private Font m_font;
		private Font m_fontHot;
		private bool m_hot;

		/// <summary>Klick auf die Versionsanzeige.</summary>
		public event EventHandler InfoRequested;

		public FooterBadge()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
				ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			this.Cursor = Cursors.Hand;
			m_tip.SetToolTip(this, "Info über Fluent Theme");
		}

		private static string VersionText
		{
			get { return "Fluent Theme " + AboutDialog.ShortVersion; }
		}

		/// <summary>GS-Logo (eingebettete Ressource), wird im Info-Fenster angezeigt.</summary>
		internal static Image Logo
		{
			get
			{
				if(s_logoLoaded) return s_logo;
				s_logoLoaded = true;
				try
				{
					Assembly asm = typeof(FooterBadge).Assembly;
					using(Stream st = asm.GetManifestResourceStream("GS-Logo.png"))
					{
						if(st != null)
						{
							using(Image img = Image.FromStream(st)) { s_logo = new Bitmap(img); }
						}
					}
				}
				catch(Exception ex) { FluentThemeExt.Report(ex); }
				return s_logo;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing)
			{
				DisposeFonts();
				m_tip.Dispose();
			}
			base.Dispose(disposing);
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		private void DisposeFonts()
		{
			if(m_font != null) { m_font.Dispose(); m_font = null; }
			if(m_fontHot != null) { m_fontHot.Dispose(); m_fontHot = null; }
		}

		private const TextFormatFlags Tf = TextFormatFlags.Left | TextFormatFlags.NoPrefix |
			TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;

		public void SetPalette(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
			DisposeFonts();
			m_font = new Font("Segoe UI", 8.5f);
			m_fontHot = new Font("Segoe UI", 8.5f, FontStyle.Underline);
			this.BackColor = p.Bg;
			Size ts = TextRenderer.MeasureText(VersionText, m_font, Size.Empty, Tf);
			this.Size = new Size(ts.Width + D(2), Math.Max(ts.Height, D(16)) + D(2));
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if((m_p == null) || (m_font == null)) return;
			try
			{
				e.Graphics.Clear(m_p.Bg);
				TextRenderer.DrawText(e.Graphics, VersionText, m_hot ? m_fontHot : m_font, this.ClientRectangle,
					m_hot ? m_p.Link : m_p.Sub, Tf);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			base.OnMouseEnter(e);
			m_hot = true;
			Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			m_hot = false;
			Invalidate();
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if(e.Button != MouseButtons.Left) return;
			EventHandler h = InfoRequested;
			if(h != null) h(this, EventArgs.Empty);
		}
	}
}
