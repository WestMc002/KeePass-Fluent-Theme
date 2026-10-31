using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Gruppenbaum im Stil der Eintragsliste: abgerundete Auswahl mit Akzentstreifen,
	/// weicher Hover, schlichte Pfeile statt Plus/Minus. Alle Positionen richten sich nach dem
	/// Layout von Windows, damit Klicks (Aufklappen, Auswahl, Drag &amp; Drop) unverändert funktionieren.</summary>
	internal sealed class TreePainter
	{
		private readonly TreeView m_tv;
		private Palette m_p;
		private float m_s = 1f;
		private bool m_hover = true;
		private bool m_attached;

		private const TextFormatFlags TfText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
			TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

		public TreePainter(TreeView tv)
		{
			m_tv = tv;
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		public void Attach()
		{
			if(m_attached) return;
			m_attached = true;
			m_tv.DrawMode = TreeViewDrawMode.OwnerDrawAll;
			m_tv.DrawNode += OnDrawNode;
		}

		public void Detach()
		{
			if(!m_attached) return;
			m_attached = false;
			m_tv.DrawNode -= OnDrawNode;
			m_tv.DrawMode = TreeViewDrawMode.Normal;
			m_tv.Invalidate();
		}

		public void Update(Palette p, float scale, bool hover)
		{
			m_p = p;
			m_s = scale;
			m_hover = hover;
			if(m_tv.DrawMode != TreeViewDrawMode.OwnerDrawAll) m_tv.DrawMode = TreeViewDrawMode.OwnerDrawAll;
			m_tv.HotTracking = hover;
			m_tv.Invalidate();
		}

		private void OnDrawNode(object sender, DrawTreeNodeEventArgs e)
		{
			TreeNode node = e.Node;
			if((m_p == null) || (node == null) || (e.Bounds.Height <= 0) || (e.Bounds.Width <= 0))
			{
				e.DrawDefault = (m_p == null);
				return;
			}
			try
			{
				Graphics g = e.Graphics;
				Rectangle row = new Rectangle(0, e.Bounds.Top, m_tv.ClientSize.Width, e.Bounds.Height);
				using(SolidBrush b = new SolidBrush(m_p.Card)) { g.FillRectangle(b, row); }

				bool selected = ((e.State & TreeNodeStates.Selected) != 0) || (m_tv.SelectedNode == node);
				bool hot = (m_hover && ((e.State & TreeNodeStates.Hot) != 0));

				// Position des Texts gibt Windows vor (node.Bounds), Symbol und Pfeil liegen links davon
				Rectangle tb = node.Bounds;
				ImageList il = m_tv.ImageList;
				int imgW = ((il != null) ? il.ImageSize.Width : 0);
				int imgLeft = tb.Left - ((il != null) ? (imgW + 3) : 0);
				int shift = D(3); // Symbol und Text etwas weiter rechts: mehr Luft zwischen Pfeil und Symbol

				// Markierung und Akzentstreifen beginnen an der Einrückung der Gruppe (direkt vor dem
				// Pfeil), nicht ganz links – sonst ist bei Untergruppen viel Leerraum bis zum Symbol
				int selLeft = Math.Max(row.Left + D(4), imgLeft - m_tv.Indent - D(2));
				float chevX = imgLeft - m_tv.Indent / 2f + D(2); // Mitte des Pfeils
				if(node.Nodes.Count == 0)
				{
					// Ohne Pfeil: Streifen genauso weit vor dem Symbol wie sonst vor dem Pfeil
					int pillGap = (int)Math.Round(chevX - 3.5f * m_s) - (selLeft + D(3));
					selLeft = Math.Max(row.Left + D(4), imgLeft + shift - pillGap - D(3));
				}
				Rectangle inner = Rectangle.FromLTRB(selLeft, row.Top + 1, row.Right - D(4), row.Bottom - 1);
				if(selected)
				{
					Color sc = (m_tv.Focused ? m_p.Sel : Palette.Blend(m_p.Card, m_p.Sel, 0.7));
					Gfx.Fill(g, inner, D(4), sc);
					int ph = Math.Max(D(8), Math.Min(D(16), row.Height - D(10)));
					Gfx.Fill(g, new Rectangle(inner.Left, row.Top + (row.Height - ph) / 2, D(3), ph), D(2), m_p.Pill);
				}
				else if(hot) Gfx.Fill(g, inner, D(4), m_p.Hover);

				if(node.Nodes.Count > 0)
				{
					float cx = chevX; // Abstand zum Akzentstreifen (Klickbereich bleibt)
					float cy = row.Top + row.Height / 2f;
					DrawExpander(g, cx, cy, node.IsExpanded, hot || selected);
				}

				if(il != null)
				{
					int idx = node.ImageIndex;
					if((idx < 0) && !string.IsNullOrEmpty(node.ImageKey)) idx = il.Images.IndexOfKey(node.ImageKey);
					if(idx < 0) idx = m_tv.ImageIndex;
					if((idx >= 0) && (idx < il.Images.Count))
					{
						int iy = row.Top + (row.Height - il.ImageSize.Height) / 2;
						il.Draw(g, imgLeft + shift, iy, idx);
					}
				}

				Font f = (node.NodeFont ?? m_tv.Font);
				Color fc = m_p.Text;
				if(!node.ForeColor.IsEmpty && (node.ForeColor != m_tv.ForeColor) && (node.ForeColor != SystemColors.WindowText))
					fc = (m_p.Dark ? Palette.Blend(node.ForeColor, Color.White, 0.45) : node.ForeColor);
				Rectangle tr = new Rectangle(tb.Left + 1 + shift, row.Top, Math.Max(0, inner.Right - D(6) - tb.Left - shift),
					row.Height);
				if(selected)
				{
					using(Font fb = new Font(f, f.Style | FontStyle.Bold))
					{
						TextRenderer.DrawText(g, node.Text, fb, tr, fc, TfText);
					}
				}
				else TextRenderer.DrawText(g, node.Text, f, tr, fc, TfText);
			}
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				e.DrawDefault = true;
			}
		}

		private void DrawExpander(Graphics g, float cx, float cy, bool expanded, bool strong)
		{
			SmoothingMode old = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			float h = 3.5f * m_s;
			using(Pen p = new Pen(strong ? m_p.Text : m_p.Sub, Math.Max(1f, 1.3f * m_s)))
			{
				p.StartCap = LineCap.Round;
				p.EndCap = LineCap.Round;
				p.LineJoin = LineJoin.Round;
				if(expanded)
					g.DrawLines(p, new PointF[] { new PointF(cx - h, cy - h / 2f), new PointF(cx, cy + h / 2f),
						new PointF(cx + h, cy - h / 2f) });
				else
					g.DrawLines(p, new PointF[] { new PointF(cx - h / 2f, cy - h), new PointF(cx + h / 2f, cy),
						new PointF(cx - h / 2f, cy + h) });
			}
			g.SmoothingMode = old;
		}
	}
}
