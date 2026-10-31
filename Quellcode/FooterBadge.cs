using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Versionsnummer, Logo, Name und Webseite unten rechts in der Statusleiste
	/// (wie in „Monitor Dimmer“ und „Fensterpositionen“, für KeePass einzeilig).
	/// Klick auf die Versionsnummer öffnet das Info-Fenster, der Rest die Webseite.</summary>
	internal sealed class FooterBadge : Control
	{
		private const string Website = "www.westmc002.de";
		private const string WebsiteUrl = "https://www.westmc002.de";
		private const string Author = "Günther Schoder";

		private static Image s_logo;
		private static bool s_logoLoaded;

		private readonly ToolTip m_tip = new ToolTip();
		private Palette m_p;
		private float m_s = 1f;
		private Font m_fName;
		private Font m_fUrl;
		private Font m_fUrlHot;
		private Font m_fVer;
		private Font m_fVerHot;
		private Rectangle m_urlRect = Rectangle.Empty;
		private Rectangle m_verRect = Rectangle.Empty;
		private bool m_hot;
		private bool m_verHot;

		/// <summary>Klick auf die Versionsnummer.</summary>
		public event EventHandler InfoRequested;

		public FooterBadge()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
				ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			this.Cursor = Cursors.Hand;
			m_tip.SetToolTip(this, WebsiteUrl);
		}

		private string VersionText
		{
			get { return "Fluent Theme " + AboutDialog.ShortVersion; }
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

		private void DisposeFonts()
		{
			if(m_fName != null) { m_fName.Dispose(); m_fName = null; }
			if(m_fUrl != null) { m_fUrl.Dispose(); m_fUrl = null; }
			if(m_fUrlHot != null) { m_fUrlHot.Dispose(); m_fUrlHot = null; }
			if(m_fVer != null) { m_fVer.Dispose(); m_fVer = null; }
			if(m_fVerHot != null) { m_fVerHot.Dispose(); m_fVerHot = null; }
		}

		public void SetPalette(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
			DisposeFonts();
			try { m_fName = new Font("Segoe UI Semibold", 8.5f); }
			catch(ArgumentException) { m_fName = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold); }
			m_fUrl = new Font("Segoe UI", 8.5f);
			m_fUrlHot = new Font("Segoe UI", 8.5f, FontStyle.Underline);
			m_fVer = new Font("Segoe UI", 8.5f);
			m_fVerHot = new Font("Segoe UI", 8.5f, FontStyle.Underline);
			this.BackColor = p.Bg;
			this.Size = MeasureBadge();
			Invalidate();
		}

		// Kompakte Variante für KeePass: alles in einer Zeile, damit die Statusleiste
		// ihre normale Höhe behält.
		private int LogoHeight
		{
			get { return D(16); }
		}

		private int LogoWidth
		{
			get
			{
				Image img = Logo;
				if((img == null) || (img.Height == 0)) return 0;
				return (int)Math.Round(this.LogoHeight * (double)img.Width / img.Height);
			}
		}

		private const string Sep = "  \u00B7  ";
		private const TextFormatFlags Tf = TextFormatFlags.Left | TextFormatFlags.NoPrefix |
			TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

		private Size MeasureBadge()
		{
			int vw = TextRenderer.MeasureText(VersionText, m_fVer, Size.Empty, Tf).Width + D(16);
			int tw = vw + TextRenderer.MeasureText(Author, m_fName, Size.Empty, Tf).Width +
				TextRenderer.MeasureText(Sep, m_fUrl, Size.Empty, Tf).Width +
				TextRenderer.MeasureText(Website, m_fUrl, Size.Empty, Tf).Width;
			int lw = this.LogoWidth;
			int w = ((lw > 0) ? (lw + D(6)) : 0) + tw + D(2);
			int h = Math.Max(this.LogoHeight, m_fName.Height) + D(2);
			return new Size(w, h);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if((m_p == null) || (m_fName == null)) return;
			try
			{
				Graphics g = e.Graphics;
				g.Clear(m_p.Bg);

				int x = 0;
				int y = (this.Height - m_fName.Height) / 2;

				// Versionsnummer (Klick -> Info-Fenster)
				Font fv = (m_verHot ? m_fVerHot : m_fVer);
				Size vs = TextRenderer.MeasureText(g, VersionText, fv, Size.Empty, Tf);
				m_verRect = new Rectangle(0, 0, vs.Width, this.Height);
				TextRenderer.DrawText(g, VersionText, fv, new Point(0, y), m_verHot ? m_p.Link : m_p.Sub, Tf);
				x = vs.Width + D(16);

				Image img = Logo;
				int lw = this.LogoWidth;
				if((img != null) && (lw > 0))
				{
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					g.PixelOffsetMode = PixelOffsetMode.HighQuality;
					int lh = this.LogoHeight;
					g.DrawImage(img, new Rectangle(x, (this.Height - lh) / 2, lw, lh));
					x += lw + D(6);
				}

				TextRenderer.DrawText(g, Author, m_fName, new Point(x, y), m_p.Text, Tf);
				x += TextRenderer.MeasureText(g, Author, m_fName, Size.Empty, Tf).Width;
				TextRenderer.DrawText(g, Sep, m_fUrl, new Point(x, y), m_p.Sub, Tf);
				x += TextRenderer.MeasureText(g, Sep, m_fUrl, Size.Empty, Tf).Width;
				bool siteHot = (m_hot && !m_verHot);
				Font fu = (siteHot ? m_fUrlHot : m_fUrl);
				Size us = TextRenderer.MeasureText(g, Website, fu, Size.Empty, Tf);
				m_urlRect = new Rectangle(x, y, us.Width, us.Height);
				TextRenderer.DrawText(g, Website, fu, m_urlRect.Location, siteHot ? m_p.Link : m_p.Sub, Tf);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			base.OnMouseEnter(e);
			m_hot = true;
			Invalidate();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			bool v = m_verRect.Contains(e.Location);
			if(v != m_verHot)
			{
				m_verHot = v;
				m_tip.SetToolTip(this, v ? "Info über Fluent Theme" : WebsiteUrl);
				Invalidate();
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			m_hot = false;
			m_verHot = false;
			Invalidate();
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if(e.Button != MouseButtons.Left) return;
			if(m_verRect.Contains(e.Location))
			{
				EventHandler h = InfoRequested;
				if(h != null) h(this, EventArgs.Empty);
				return;
			}
			try { Process.Start(WebsiteUrl); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}
	}
}