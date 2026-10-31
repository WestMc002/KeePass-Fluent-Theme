using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FluentTheme
{
	internal sealed class FluentColorTable : ProfessionalColorTable
	{
		private readonly Palette m_p;

		public FluentColorTable(Palette p)
		{
			m_p = p;
			UseSystemColors = false;
		}

		public override Color ToolStripDropDownBackground { get { return m_p.Card; } }
		public override Color ImageMarginGradientBegin { get { return m_p.Card; } }
		public override Color ImageMarginGradientMiddle { get { return m_p.Card; } }
		public override Color ImageMarginGradientEnd { get { return m_p.Card; } }
		public override Color MenuBorder { get { return m_p.Border; } }
		public override Color MenuItemBorder { get { return m_p.CardHover; } }
		public override Color MenuItemSelected { get { return m_p.CardHover; } }
		public override Color MenuItemSelectedGradientBegin { get { return m_p.Hover; } }
		public override Color MenuItemSelectedGradientEnd { get { return m_p.Hover; } }
		public override Color MenuItemPressedGradientBegin { get { return m_p.Sel; } }
		public override Color MenuItemPressedGradientMiddle { get { return m_p.Sel; } }
		public override Color MenuItemPressedGradientEnd { get { return m_p.Sel; } }
		public override Color SeparatorDark { get { return m_p.Border; } }
		public override Color SeparatorLight { get { return m_p.Border; } }
		public override Color ToolStripBorder { get { return m_p.Bg; } }
		public override Color ToolStripGradientBegin { get { return m_p.Bg; } }
		public override Color ToolStripGradientMiddle { get { return m_p.Bg; } }
		public override Color ToolStripGradientEnd { get { return m_p.Bg; } }
		public override Color MenuStripGradientBegin { get { return m_p.Bg; } }
		public override Color MenuStripGradientEnd { get { return m_p.Bg; } }
		public override Color StatusStripGradientBegin { get { return m_p.Bg; } }
		public override Color StatusStripGradientEnd { get { return m_p.Bg; } }
		public override Color OverflowButtonGradientBegin { get { return m_p.Bg; } }
		public override Color OverflowButtonGradientMiddle { get { return m_p.Bg; } }
		public override Color OverflowButtonGradientEnd { get { return m_p.Bg; } }
		public override Color ButtonSelectedHighlight { get { return m_p.Hover; } }
		public override Color ButtonSelectedBorder { get { return m_p.Hover; } }
		public override Color ButtonSelectedGradientBegin { get { return m_p.Hover; } }
		public override Color ButtonSelectedGradientMiddle { get { return m_p.Hover; } }
		public override Color ButtonSelectedGradientEnd { get { return m_p.Hover; } }
		public override Color ButtonPressedBorder { get { return m_p.Sel; } }
		public override Color ButtonPressedGradientBegin { get { return m_p.Sel; } }
		public override Color ButtonPressedGradientMiddle { get { return m_p.Sel; } }
		public override Color ButtonPressedGradientEnd { get { return m_p.Sel; } }
		public override Color ButtonCheckedGradientBegin { get { return m_p.Sel; } }
		public override Color ButtonCheckedGradientMiddle { get { return m_p.Sel; } }
		public override Color ButtonCheckedGradientEnd { get { return m_p.Sel; } }
		public override Color CheckBackground { get { return m_p.Sel; } }
		public override Color CheckSelectedBackground { get { return m_p.Sel; } }
		public override Color CheckPressedBackground { get { return m_p.Sel; } }
	}

	/// <summary>Flache Menüs, Symbolleiste und Statusleiste mit abgerundeten Hover-Flächen.</summary>
	internal sealed class FluentRenderer : ToolStripProfessionalRenderer
	{
		private readonly Palette m_p;
		private readonly float m_s;
		private readonly System.Collections.Generic.HashSet<IntPtr> m_styledPopups =
			new System.Collections.Generic.HashSet<IntPtr>();

		public FluentRenderer(Palette p, float scale) : base(new FluentColorTable(p))
		{
			m_p = p;
			m_s = scale;
			this.RoundedEdges = false;
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
		{
			bool isDropDown = (e.ToolStrip is ToolStripDropDown);
			Color c = (isDropDown ? m_p.Card : m_p.Bg);
			using(SolidBrush b = new SolidBrush(c)) { e.Graphics.FillRectangle(b, e.AffectedBounds); }

			// Auch Menüs, die nicht eingehängt wurden (z. B. Tray-Menü), bekommen runde Ecken
			if(isDropDown && e.ToolStrip.IsHandleCreated && m_styledPopups.Add(e.ToolStrip.Handle))
				NativeMethods.SetPopupStyle(e.ToolStrip.Handle, m_p.Border);
		}

		protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
		{
			ToolStrip ts = e.ToolStrip;
			if(ts is ToolStripDropDown)
			{
				if(NativeMethods.IsWin11) return; // Rahmen zeichnet Windows (runde Ecken)
				using(Pen p = new Pen(m_p.Border))
				{
					e.Graphics.DrawRectangle(p, 0, 0, ts.Width - 1, ts.Height - 1);
				}
			}
			else if(ts is StatusStrip)
			{
				using(Pen p = new Pen(m_p.Border))
				{
					e.Graphics.DrawLine(p, 0, 0, ts.Width, 0);
				}
			}
		}

		protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
		{
			using(SolidBrush b = new SolidBrush(m_p.Card)) { e.Graphics.FillRectangle(b, e.AffectedBounds); }
		}

		protected override void OnRenderGrip(ToolStripGripRenderEventArgs e)
		{
			// Keine Griffe
		}

		protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
		{
			ToolStripItem it = e.Item;
			if(!(it.Selected || it.Pressed)) return;

			Rectangle r = new Rectangle(Point.Empty, it.Size);
			Color c;
			if(it.IsOnDropDown)
			{
				if(!it.Enabled) return;
				// Gleicher Abstand zum linken und rechten Menürand (gemessen am Menü, nicht am
				// Eintrag – die Einträge sind nicht symmetrisch im Menü angeordnet)
				int gap = D(4);
				int left = gap, right = it.Width - gap;
				ToolStrip owner = it.Owner;
				if(owner != null)
				{
					left = gap - it.Bounds.Left;
					right = owner.ClientSize.Width - gap - it.Bounds.Left;
				}
				r = Rectangle.FromLTRB(left, r.Top + 1, right, r.Bottom - 1);
				c = m_p.CardHover;
			}
			else
			{
				// Menüleiste: weiche Fläche ohne Rahmen, geöffnet etwas kräftiger
				r = Rectangle.FromLTRB(r.Left + 1, r.Top + D(2), r.Right - 1, r.Bottom - D(2));
				c = Palette.Blend(m_p.Bg, m_p.Text, it.Pressed ? (m_p.Dark ? 0.14 : 0.09) :
					(m_p.Dark ? 0.08 : 0.05));
			}
			Gfx.Fill(e.Graphics, r, D(5), c);
		}

		/// <summary>Hover wie der Sekundär-Button in „Fensterpositionen“:
		/// weiße Fläche mit feinem Rahmen; gedrückt/aktiv mit Akzent-Tönung.</summary>
		private void FillButton(Graphics g, ToolStripItem it, bool isChecked)
		{
			if(!(it.Selected || it.Pressed || isChecked)) return;
			Rectangle r = new Rectangle(1, 1, it.Width - 2, it.Height - 2);
			if(it.Pressed || isChecked)
			{
				Gfx.Fill(g, r, D(6), m_p.Sel);
				Gfx.Stroke(g, r, D(6), m_p.BadgeBorder);
			}
			else
			{
				Gfx.Fill(g, r, D(6), it.IsOnDropDown ? m_p.CardHover : m_p.Card);
				Gfx.Stroke(g, r, D(6), m_p.Border);
			}
		}

		protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
		{
			ToolStripButton btn = (e.Item as ToolStripButton);
			FillButton(e.Graphics, e.Item, (btn != null) && btn.Checked);
		}

		protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
		{
			FillButton(e.Graphics, e.Item, false);
		}

		protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
		{
			FillButton(e.Graphics, e.Item, false);
			ToolStripSplitButton sb = (e.Item as ToolStripSplitButton);
			if(sb != null)
			{
				Rectangle ar = sb.DropDownButtonBounds;
				Gfx.Chevron(e.Graphics, ar.Left + ar.Width / 2f, ar.Top + ar.Height / 2f, 7f * m_s,
					false, sb.Enabled ? m_p.Text : m_p.Sub, Math.Max(1f, 1.2f * m_s));
			}
		}

		protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e)
		{
			FillButton(e.Graphics, e.Item, false);
			Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
			Gfx.Chevron(e.Graphics, r.Left + r.Width / 2f, r.Top + r.Height / 2f, 7f * m_s,
				false, m_p.Text, Math.Max(1f, 1.2f * m_s));
		}

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			Color bg = (e.Item.IsOnDropDown ? m_p.Card : m_p.Bg);
			if(!e.Item.Enabled) e.TextColor = Palette.Blend(m_p.Sub, bg, 0.35);
			else if((e.Item is ToolStripMenuItem) && e.Item.IsOnDropDown && (e.Text != e.Item.Text))
				e.TextColor = m_p.Sub; // Tastenkürzel grau wie in Windows 11
			else e.TextColor = m_p.Text;

			// Globale Vorgabe: Text senkrecht nach der Buchstabenmitte ausrichten (nicht nach der
			// Zeilenhöhe inkl. Unterlängen) – sonst wirkt der Text etwas zu tief
			// Dazu das Textrechteck auf die volle Höhe des Eintrags setzen (WinForms legt es nicht
			// mittig an) und danach um den Abstand Zeilenmitte -> Buchstabenmitte anheben.
			if(e.Item.IsOnDropDown && (e.TextFont != null))
			{
				Rectangle tr = e.TextRectangle;
				int up = TextShiftUp(e.Graphics, e.TextFont);
				int x = tr.X;
				if(e.Text == e.Item.Text) // Haupttext (Tastenkürzel bleiben, wo sie sind)
				{
					ToolStripDropDownMenu ddm = (e.Item.Owner as ToolStripDropDownMenu);
					if((ddm != null) && !ddm.ShowImageMargin && !ddm.ShowCheckMargin)
						x = D(16) - e.Item.Bounds.Left; // Menü ohne Symbole: 16 × Skalierung Rand links
					else
						x = tr.X + D(5); // globale Vorgabe: Abstand Symbol -> Text 8 × Skalierung
				}
				e.TextRectangle = new Rectangle(x, -up, tr.Width, e.Item.Height);
				e.TextFormat = (e.TextFormat & ~(TextFormatFlags.Top | TextFormatFlags.Bottom)) |
					TextFormatFlags.VerticalCenter;
			}
			base.OnRenderItemText(e);
		}

		/// <summary>Um wie viele Pixel die Buchstabenmitte (Mitte der Großbuchstaben) unter der
		/// Mitte der Textzeile liegt.</summary>
		private static int TextShiftUp(Graphics g, Font f)
		{
			try
			{
				FontFamily ff = f.FontFamily;
				float em = ff.GetEmHeight(f.Style);
				if(em <= 0f) return 0;
				float emPx = f.SizeInPoints * g.DpiY / 72f;
				float asc = ff.GetCellAscent(f.Style) * emPx / em;
				float desc = ff.GetCellDescent(f.Style) * emPx / em;
				float capH = 0.70f * emPx; // Versalhöhe Segoe UI ≈ 0,70 em
				float capMid = asc - capH / 2f;
				float boxMid = (asc + desc) / 2f;
				return (int)Math.Round(capMid - boxMid);
			}
			catch(Exception) { return 0; }
		}

		// Sichtbarer Bereich (nicht transparente Pixel) je Symbol, für die optische Zentrierung
		private static readonly ConditionalWeakTable<Image, object> s_visible =
			new ConditionalWeakTable<Image, object>();

		private static Rectangle VisibleBounds(Image img)
		{
			object o;
			if(s_visible.TryGetValue(img, out o)) return (Rectangle)o;

			Rectangle res = new Rectangle(Point.Empty, img.Size);
			try
			{
				Bitmap bmp = (img as Bitmap);
				if((bmp != null) && (bmp.Width <= 128) && (bmp.Height <= 128))
				{
					int x0 = bmp.Width, y0 = bmp.Height, x1 = -1, y1 = -1;
					for(int y = 0; y < bmp.Height; ++y)
					{
						for(int x = 0; x < bmp.Width; ++x)
						{
							if(bmp.GetPixel(x, y).A < 24) continue;
							if(x < x0) x0 = x;
							if(x > x1) x1 = x;
							if(y < y0) y0 = y;
							if(y > y1) y1 = y;
						}
					}
					if(x1 >= x0) res = Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
				}
			}
			catch(Exception) { }
			try { s_visible.Add(img, res); }
			catch(ArgumentException) { }
			return res;
		}

		/// <summary>Verschiebt das Zielrechteck so, dass der sichtbare Teil des Symbols mittig sitzt.</summary>
		private static Rectangle CenterVisible(Image img, Rectangle r)
		{
			if((img == null) || (img.Width <= 0) || (img.Height <= 0)) return r;
			Rectangle vb = VisibleBounds(img);
			float sx = r.Width / (float)img.Width, sy = r.Height / (float)img.Height;
			float vcx = (vb.Left + vb.Width / 2f) * sx, vcy = (vb.Top + vb.Height / 2f) * sy;
			int dx = (int)Math.Round(r.Width / 2f - vcx), dy = (int)Math.Round(r.Height / 2f - vcy);
			int lim = Math.Max(1, r.Width / 4);
			dx = Math.Max(-lim, Math.Min(lim, dx));
			dy = Math.Max(-lim, Math.Min(lim, dy));
			r.Offset(dx, dy);
			return r;
		}

		protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
		{
			if((e.Item != null) && (e.Direction == ArrowDirection.Right))
			{
				Rectangle r = e.ArrowRectangle;
				float cx = r.Left + r.Width / 2f - 6f * m_s; // etwas Abstand zum Rand
				float cy = r.Top + r.Height / 2f;
				float h = 3f * m_s;
				SmoothingMode old = e.Graphics.SmoothingMode;
				e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				using(Pen p = new Pen(e.Item.Enabled ? m_p.Text : m_p.Sub, Math.Max(1f, 1.2f * m_s)))
				{
					p.StartCap = LineCap.Round;
					p.EndCap = LineCap.Round;
					e.Graphics.DrawLines(p, new PointF[] { new PointF(cx - h / 2f, cy - h),
						new PointF(cx + h / 2f, cy), new PointF(cx - h / 2f, cy + h) });
				}
				e.Graphics.SmoothingMode = old;
				return;
			}

			e.ArrowColor = (((e.Item != null) && !e.Item.Enabled) ? m_p.Sub : m_p.Text);
			base.OnRenderArrow(e);
		}

		protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
		{
			Rectangle b = new Rectangle(Point.Empty, e.Item.Size);
			using(Pen p = new Pen(m_p.Border))
			{
				if(e.Vertical)
				{
					int x = b.Width / 2;
					e.Graphics.DrawLine(p, x, b.Top + D(5), x, b.Bottom - D(5));
				}
				else
				{
					int y = b.Height / 2;
					int left = (e.Item.IsOnDropDown ? D(8) : 0);
					e.Graphics.DrawLine(p, b.Left + left, y, b.Right - D(8), y);
				}
			}
		}

		/// <summary>Menü-Symbole etwas weiter vom linken Rand weg.</summary>
		protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
		{
			if((e.Item != null) && e.Item.IsOnDropDown && (e.Image != null))
			{
				Rectangle r = e.ImageRectangle;
				r.Offset(D(7), 0); // gleicher Randabstand wie die Pfeile rechts
				r = CenterVisible(e.Image, r);
				if(e.Item.Enabled) e.Graphics.DrawImage(e.Image, r);
				else ControlPaint.DrawImageDisabled(e.Graphics, e.Image, r.X, r.Y, Color.Transparent);
				return;
			}
			base.OnRenderItemImage(e);
		}

		protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
		{
			Rectangle r = e.ImageRectangle;
			if((e.Item != null) && e.Item.IsOnDropDown) r.Offset(D(7), 0); // gleicher Randabstand wie die Pfeile rechts
			r.Inflate(D(2), D(2));
			Gfx.Fill(e.Graphics, r, D(4), Palette.Blend(m_p.Card, m_p.Pill, m_p.Dark ? 0.30 : 0.18));

			ToolStripMenuItem mi = (e.Item as ToolStripMenuItem);
			if((mi != null) && (mi.Image != null)) return; // Bild wird separat gezeichnet

			float cx = r.Left + r.Width / 2f;
			float cy = r.Top + r.Height / 2f;
			float u = 4f * m_s;
			SmoothingMode old = e.Graphics.SmoothingMode;
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using(Pen p = new Pen(m_p.Text, Math.Max(1.2f, 1.6f * m_s)))
			{
				p.StartCap = LineCap.Round;
				p.EndCap = LineCap.Round;
				p.LineJoin = LineJoin.Round;
				e.Graphics.DrawLines(p, new PointF[] { new PointF(cx - u, cy),
					new PointF(cx - u * 0.25f, cy + u * 0.8f), new PointF(cx + u, cy - u * 0.8f) });
			}
			e.Graphics.SmoothingMode = old;
		}
	}
}