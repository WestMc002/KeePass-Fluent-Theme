// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

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
		private EmptyBelow m_empty;

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
			m_empty = new EmptyBelow(this);
		}

		public void Detach()
		{
			if(!m_attached) return;
			m_attached = false;
			m_tv.DrawNode -= OnDrawNode;
			if(m_empty != null) { m_empty.Detach(); m_empty = null; }
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
				// Pfeil nur bei Untergruppen; die Wurzel hat keinen (KeePass blendet die Wurzellinien aus)
				bool chev = ((node.Nodes.Count > 0) && (m_tv.ShowRootLines || (node.Parent != null)));
				float chevX = imgLeft - m_tv.Indent / 2f + D(2); // Mitte des Pfeils
				int minLeft = row.Left + D(4);
				int selLeft;
				if(chev) selLeft = Math.Max(minLeft, imgLeft - m_tv.Indent - D(2));
				else
				{
					// Ohne Pfeil: Streifen genauso weit vor dem Symbol wie sonst vor dem Pfeil
					int pillGap = (int)Math.Round(m_tv.Indent / 2f + D(1) - 3.5f * m_s);
					selLeft = imgLeft + shift - pillGap - D(3);
					if(selLeft < minLeft)
					{
						// Ganz links (Wurzel): Symbol und Text nach rechts rücken statt den Abstand zu verlieren
						shift += minLeft - selLeft;
						selLeft = minLeft;
					}
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

				if(chev)
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

		/// <summary>Malt die leere Fläche unter der letzten Gruppe in Kartenfarbe aus. Solange die
		/// Datenbank gesperrt ist, zeichnet Windows sie weiß, und nach dem Öffnen werden nur die
		/// Zeilen mit Gruppen neu gezeichnet.</summary>
		private sealed class EmptyBelow : NativeWindow
		{
			private const int WM_PAINT = 0x000F;
			private readonly TreePainter m_owner;

			public EmptyBelow(TreePainter owner)
			{
				m_owner = owner;
				TreeView tv = owner.m_tv;
				tv.HandleCreated += OnHandleCreated;
				tv.HandleDestroyed += OnHandleDestroyed;
				if(tv.IsHandleCreated && (this.Handle == IntPtr.Zero)) AssignHandle(tv.Handle);
			}

			public void Detach()
			{
				TreeView tv = m_owner.m_tv;
				tv.HandleCreated -= OnHandleCreated;
				tv.HandleDestroyed -= OnHandleDestroyed;
				if(this.Handle != IntPtr.Zero) ReleaseHandle();
			}

			private void OnHandleCreated(object sender, EventArgs e)
			{
				if(this.Handle == IntPtr.Zero) AssignHandle(m_owner.m_tv.Handle);
			}

			private void OnHandleDestroyed(object sender, EventArgs e)
			{
				if(this.Handle != IntPtr.Zero) ReleaseHandle();
			}

			protected override void WndProc(ref Message m)
			{
				base.WndProc(ref m);
				if(m.Msg != WM_PAINT) return;
				try { Paint(); }
				catch(Exception ex) { FluentThemeExt.Report(ex); }
			}

			private void Paint()
			{
				TreeView tv = m_owner.m_tv;
				Palette p = m_owner.m_p;
				if((p == null) || !m_owner.m_attached) return;

				// Letzte sichtbare Gruppe: letzter Knoten, solange aufgeklappt jeweils das letzte Kind
				int top = 0;
				TreeNode n = ((tv.Nodes.Count > 0) ? tv.Nodes[tv.Nodes.Count - 1] : null);
				while((n != null) && n.IsExpanded && (n.Nodes.Count > 0)) n = n.Nodes[n.Nodes.Count - 1];
				if(n != null)
				{
					Rectangle b = n.Bounds;
					if(b.Height <= 0) return; // Lage unbekannt – nichts übermalen
					top = b.Bottom;
				}

				Size cs = tv.ClientSize;
				if((top < 0) || (top >= cs.Height)) return;
				using(Graphics g = Graphics.FromHwnd(tv.Handle))
				using(SolidBrush br = new SolidBrush(p.Card))
				{
					g.FillRectangle(br, 0, top, cs.Width, cs.Height - top);
				}
			}
		}
	}
}
