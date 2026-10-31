using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using KeePassLib;

namespace FluentTheme
{
	/// <summary>Zeichnet die Eintragsliste selbst: höhere Zeilen, Auswahl mit Akzentstrich,
	/// flacher Spaltenkopf.</summary>
	internal sealed class EntryListPainter
	{
		private const TextFormatFlags TfCell = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
			TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
			TextFormatFlags.PreserveGraphicsClipping;

		private readonly ListView m_lv;
		private Palette m_p;
		private float m_s = 1f;
		private int m_rowH = 28;
		private ImageList m_orig;     // Symbolliste von KeePass
		private ImageList m_spacer;   // leere Liste, die nur die Zeilenhöhe vorgibt
		private Bitmap m_blank;
		private int m_hot = -1;
		private bool m_attached;
		private bool m_hoverEnabled = true;
		private int m_expiryDays = 14;   // 0 = keine Ablauf-Badges
		private Font m_badgeFont;

		/// <summary>Einträge, die innerhalb dieser Anzahl Tage ablaufen, bekommen ein oranges Badge
		/// (abgelaufene immer ein rotes). 0 = Badges aus.</summary>
		public int ExpiryDays
		{
			get { return m_expiryDays; }
			set
			{
				if(value == m_expiryDays) return;
				m_expiryDays = Math.Max(0, value);
				m_lv.Invalidate();
			}
		}

		private static Type s_tagType;
		private static PropertyInfo s_tagEntryProp;

		public EntryListPainter(ListView lv)
		{
			m_lv = lv;
		}

		public ImageList OriginalImages
		{
			get { return m_orig; }
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		public void Attach()
		{
			if(m_attached) return;
			m_attached = true;

			m_lv.DrawColumnHeader += OnDrawColumnHeader;
			m_lv.DrawItem += OnDrawItem;
			m_lv.DrawSubItem += OnDrawSubItem;
			m_lv.MouseMove += OnMouseMove;
			m_lv.MouseLeave += OnMouseLeave;
			m_lv.ItemSelectionChanged += OnItemSelectionChanged;
			m_lv.GotFocus += OnFocusChanged;
			m_lv.LostFocus += OnFocusChanged;

			try
			{
				PropertyInfo pi = typeof(Control).GetProperty("DoubleBuffered",
					BindingFlags.Instance | BindingFlags.NonPublic);
				if(pi != null) pi.SetValue(m_lv, true, null);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }

			m_lv.OwnerDraw = true;
		}

		public void Detach()
		{
			if(!m_attached) return;
			m_attached = false;

			m_lv.DrawColumnHeader -= OnDrawColumnHeader;
			m_lv.DrawItem -= OnDrawItem;
			m_lv.DrawSubItem -= OnDrawSubItem;
			m_lv.MouseMove -= OnMouseMove;
			m_lv.MouseLeave -= OnMouseLeave;
			m_lv.ItemSelectionChanged -= OnItemSelectionChanged;
			m_lv.GotFocus -= OnFocusChanged;
			m_lv.LostFocus -= OnFocusChanged;

			try
			{
				m_lv.OwnerDraw = false;
				if((m_orig != null) && (m_lv.SmallImageList == m_spacer)) m_lv.SmallImageList = m_orig;
				m_lv.Invalidate();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		public void Update(Palette p, float scale, int rowHeight, bool hover)
		{
			m_hoverEnabled = hover;
			m_p = p;
			m_s = scale;
			m_rowH = rowHeight;

			m_lv.BackColor = p.Pane;
			m_lv.ForeColor = p.Text;
			if(m_badgeFont != null) m_badgeFont.Dispose();
			try { m_badgeFont = new Font("Segoe UI Semibold", Math.Max(7f, m_lv.Font.SizeInPoints - 1f)); }
			catch(ArgumentException) { m_badgeFont = new Font(m_lv.Font.FontFamily, Math.Max(7f, m_lv.Font.SizeInPoints - 1f), FontStyle.Bold); }
			m_lv.GridLines = false;
			m_lv.FullRowSelect = true;
			m_lv.BorderStyle = BorderStyle.None;
			if(m_lv.HotTracking) m_lv.HotTracking = false;

			if(m_lv.IsHandleCreated)
			{
				// Hell: Standard-Design ohne Explorer-Hover-Animation (verursacht Flackern),
				// dunkel: DarkMode_Explorer für dunkle Scrollleisten.
				NativeMethods.ThemeControl(m_lv, p.Dark, "DarkMode_Explorer", null);
				IntPtr hdr = NativeMethods.GetListHeader(m_lv);
				NativeMethods.ThemeHandle(hdr, p.Dark, "DarkMode_ItemsView", "ItemsView");
				NativeMethods.ListDoubleBuffer(m_lv);
			}

			EnsureState();
			m_lv.Invalidate();
		}

		/// <summary>Wird regelmäßig aufgerufen, weil KeePass Liste und Symbole neu setzt.</summary>
		public void EnsureState()
		{
			if(!m_attached || (m_p == null)) return;

			if(!m_lv.OwnerDraw) m_lv.OwnerDraw = true;
			if(m_lv.BackColor != m_p.Pane) m_lv.BackColor = m_p.Pane;
			if(m_lv.GridLines) m_lv.GridLines = false;

			ImageList cur = m_lv.SmallImageList;
			if((cur != null) && (cur != m_spacer)) m_orig = cur;

			int h = Math.Max(16, Math.Min(256, D(m_rowH)));
			int need = Math.Max(1, (m_orig != null) ? m_orig.Images.Count : 1);

			bool rebuild = ((m_spacer == null) || (m_spacer.ImageSize.Height != h));
			if(rebuild)
			{
				ImageList il = new ImageList();
				il.ColorDepth = ColorDepth.Depth32Bit;
				il.ImageSize = new Size(1, h);
				if(m_blank != null) m_blank.Dispose();
				m_blank = new Bitmap(1, h);
				for(int i = 0; i < need; ++i) il.Images.Add(m_blank);

				ImageList old = m_spacer;
				m_spacer = il;
				m_lv.SmallImageList = m_spacer;
				if(old != null) old.Dispose();
				m_lv.Invalidate();
			}
			else
			{
				// Gleiche Anzahl wie KeePass, sonst liefert ImageIndex falsche Werte
				if(m_spacer.Images.Count < need)
				{
					for(int i = m_spacer.Images.Count; i < need; ++i) m_spacer.Images.Add(m_blank);
					m_lv.Invalidate();
				}
				if(m_lv.SmallImageList != m_spacer)
				{
					m_lv.SmallImageList = m_spacer;
					m_lv.Invalidate();
				}
			}
		}

		internal static PwEntry GetEntry(ListViewItem it)
		{
			if(it == null) return null;
			object t = it.Tag;
			if(t == null) return null;
			PwEntry pe = (t as PwEntry);
			if(pe != null) return pe;

			try
			{
				Type tt = t.GetType();
				if(tt != s_tagType)
				{
					s_tagType = tt;
					s_tagEntryProp = tt.GetProperty("Entry", BindingFlags.Instance | BindingFlags.Public);
				}
				if(s_tagEntryProp == null) return null;
				return (s_tagEntryProp.GetValue(t, null) as PwEntry);
			}
			catch(Exception) { return null; }
		}

		private void OnDrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
		{
			if(m_p == null) { e.DrawDefault = true; return; }
			try
			{
				Graphics g = e.Graphics;
				Rectangle r = e.Bounds;
				using(SolidBrush b = new SolidBrush(m_p.Pane)) { g.FillRectangle(b, r); }
				using(Pen p = new Pen(m_p.Border)) { g.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1); }
				using(Pen p = new Pen(m_p.RowLine))
				{
					g.DrawLine(p, r.Right - 1, r.Top + D(6), r.Right - 1, r.Bottom - D(7));
				}

				string text = ((e.Header != null) ? e.Header.Text : string.Empty);
				int pad = D(10);
				Rectangle tr = new Rectangle(r.Left + pad, r.Top, Math.Max(0, r.Width - pad - D(18)), r.Height);
				TextRenderer.DrawText(g, text, m_lv.Font, tr, m_p.Sub, TfCell);

				int sort = NativeMethods.GetHeaderSort(NativeMethods.GetListHeader(m_lv), e.ColumnIndex);
				if(sort != 0)
				{
					int tw = TextRenderer.MeasureText(g, text, m_lv.Font, Size.Empty,
						TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
					float ax = Math.Min(tr.Left + tw + D(10), r.Right - D(10));
					Gfx.Chevron(g, ax, r.Top + r.Height / 2f, 7f * m_s, sort > 0, m_p.Sub,
						Math.Max(1f, 1.2f * m_s));
				}
			}
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				e.DrawDefault = true;
			}
		}

		private void OnDrawItem(object sender, DrawListViewItemEventArgs e)
		{
			if((m_p == null) || (m_lv.View != View.Details)) { e.DrawDefault = true; return; }
			try
			{
				// Ganze Zeile in einem Durchgang zeichnen (Hintergrund + alle Spalten),
				// damit beim Überfahren nichts zwischenzeitlich leer aufblitzt.
				Graphics g = e.Graphics;
				Rectangle row = e.Bounds;
				ListViewItem item = e.Item;

				Color bg = m_p.Pane;
				PwEntry pe = GetEntry(item);
				if((pe != null) && !pe.BackgroundColor.IsEmpty)
					bg = Palette.Blend(m_p.Pane, pe.BackgroundColor, m_p.Dark ? 0.35 : 0.65);
				using(SolidBrush b = new SolidBrush(bg)) { g.FillRectangle(b, row); }

				Rectangle inner = Rectangle.FromLTRB(row.Left + D(4), row.Top + 1, row.Right - D(4), row.Bottom - 1);
				if(item.Selected)
				{
					Color sc = (m_lv.Focused ? m_p.Sel : Palette.Blend(bg, m_p.Sel, 0.7));
					Gfx.Fill(g, inner, D(4), sc);
				}
				else if(m_hoverEnabled && (e.ItemIndex == m_hot))
					Gfx.Fill(g, inner, D(4), m_p.Hover);
				else
				{
					using(Pen p = new Pen(m_p.RowLine))
					{
						g.DrawLine(p, row.Left + D(8), row.Bottom - 1, row.Right - D(8), row.Bottom - 1);
					}
				}

				Color fc = m_p.Text;
				if((pe != null) && !pe.ForegroundColor.IsEmpty)
					fc = (m_p.Dark ? Palette.Blend(pe.ForegroundColor, Color.White, 0.45) : pe.ForegroundColor);
				Font f = (item.Font ?? m_lv.Font);

				// Spalten in Anzeigereihenfolge
				int n = m_lv.Columns.Count;
				ColumnHeader[] cols = new ColumnHeader[n];
				foreach(ColumnHeader ch in m_lv.Columns)
				{
					int di = ch.DisplayIndex;
					if((di >= 0) && (di < n)) cols[di] = ch;
				}

				// Ablauf-Badge in der ersten sichtbaren Spalte
				string badge = null;
				Color badgeColor = Color.Empty;
				if((pe != null) && pe.Expires && (m_badgeFont != null))
				{
					DateTime now = DateTime.UtcNow;
					if(pe.ExpiryTime <= now)
					{
						badge = "Abgelaufen";
						badgeColor = m_p.Weak;
					}
					else if(m_expiryDays > 0)
					{
						int days = (int)Math.Floor((pe.ExpiryTime.ToLocalTime().Date - DateTime.Now.Date).TotalDays);
						if(days <= m_expiryDays)
						{
							badge = ((days <= 0) ? "heute" : ((days == 1) ? "morgen" :
								("in " + days.ToString(System.Globalization.CultureInfo.CurrentCulture) + " Tagen")));
							badgeColor = m_p.Mid;
						}
					}
				}
				ColumnHeader firstCol = null;
				foreach(ColumnHeader ch in cols) { if(ch != null) { firstCol = ch; break; } }

				int x = row.Left;
				foreach(ColumnHeader ch in cols)
				{
					if(ch == null) continue;
					Rectangle cell = new Rectangle(x, row.Top, ch.Width, row.Height);
					x += ch.Width;
					if((cell.Width <= 0) || (cell.Right < 0)) continue;

					System.Drawing.Drawing2D.GraphicsState st = g.Save();
					g.SetClip(cell, System.Drawing.Drawing2D.CombineMode.Intersect);

					int tx = cell.Left + D(10);
					if((ch.Index == 0) && (m_orig != null))
					{
						tx += D(1); // Symbol etwas weiter weg vom Akzentstreifen
						int idx = item.ImageIndex;
						if((idx >= 0) && (idx < m_orig.Images.Count))
						{
							Size isz = m_orig.ImageSize;
							m_orig.Draw(g, tx, cell.Top + (cell.Height - isz.Height) / 2, idx);
							tx += isz.Width + D(8);
						}
					}

					string text = ((ch.Index < item.SubItems.Count) ? item.SubItems[ch.Index].Text : string.Empty);
					int textRight = cell.Right - D(6);
					if((badge != null) && (ch == firstCol))
					{
						Size bsz = TextRenderer.MeasureText(g, badge, m_badgeFont, Size.Empty,
							TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
						int bw = bsz.Width + D(14);
						int bh = Math.Min(row.Height - D(6), bsz.Height + D(6));
						int tw = TextRenderer.MeasureText(g, text, f, Size.Empty,
							TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
						// Badge direkt hinter dem Titel, bei wenig Platz am rechten Rand der Spalte
						int bx = Math.Min(tx + tw + D(8), cell.Right - D(6) - bw);
						if(bx > tx + D(30))
						{
							Rectangle rb = new Rectangle(bx, cell.Top + (cell.Height - bh) / 2, bw, bh);
							Gfx.Fill(g, rb, bh / 2, Palette.Blend(bg, badgeColor, m_p.Dark ? 0.28 : 0.16));
							Color btc = (m_p.Dark ? badgeColor : Palette.Blend(badgeColor, Color.Black, 0.1));
							TextRenderer.DrawText(g, badge, m_badgeFont, rb, btc, TextFormatFlags.HorizontalCenter |
								TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
							textRight = bx - D(6);
						}
					}
					Rectangle tr = new Rectangle(tx, cell.Top, Math.Max(0, textRight - tx), cell.Height);
					TextRenderer.DrawText(g, text, f, tr, fc, TfCell);

					g.Restore(st);
				}

				if(item.Selected)
				{
					int ph = Math.Max(D(8), Math.Min(D(16), row.Height - D(10)));
					Gfx.Fill(g, new Rectangle(inner.Left, row.Top + (row.Height - ph) / 2, D(3), ph),
						D(2), m_p.Pill);
				}
			}
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				e.DrawDefault = true;
			}
		}

		private void OnDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
		{
			// Alles wird bereits in OnDrawItem gezeichnet.
			if((m_p == null) || (m_lv.View != View.Details)) e.DrawDefault = true;
		}

		private void InvalidateRow(int index)
		{
			if((index < 0) || (index >= m_lv.Items.Count)) return;
			try
			{
				Rectangle r = m_lv.Items[index].Bounds;
				m_lv.Invalidate(new Rectangle(0, r.Top, m_lv.ClientSize.Width, r.Height));
			}
			catch(Exception) { }
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			if(!m_hoverEnabled) return;
			ListViewItem it = m_lv.GetItemAt(e.X, e.Y);
			int idx = ((it != null) ? it.Index : -1);
			if(idx == m_hot) return;
			int old = m_hot;
			m_hot = idx;
			InvalidateRow(old);
			InvalidateRow(idx);
		}

		private void OnMouseLeave(object sender, EventArgs e)
		{
			int old = m_hot;
			m_hot = -1;
			InvalidateRow(old);
		}

		private void OnItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			InvalidateRow(e.ItemIndex);
		}

		private void OnFocusChanged(object sender, EventArgs e)
		{
			m_lv.Invalidate();
		}
	}
}