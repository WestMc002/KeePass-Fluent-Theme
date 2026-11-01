// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
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
		private EmptyAreaPainter m_empty;

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
			m_empty = new EmptyAreaPainter(this);
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
			if(m_empty != null) { m_empty.Detach(); m_empty = null; }

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
				// Ohne Windows-Design: Die Liste zeichnen wir komplett selbst. Jedes Windows-Design
				// legt sonst eigene Effekte darüber – beim Überfahren eine Hover-Animation, die in der
				// Gruppenansicht (Suchergebnisse) Zeilen kurz leer zeigt und flackert, im Dunkelmodus
				// eine eckige Auswahl. Die senkrechte Scrollleiste ersetzt OverlayScroll ohnehin.
				NativeMethods.ThemeControl(m_lv, p.Dark, " ", " ");
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
			// Die Zeile wird zweimal gezeichnet: hier und noch einmal in OnDrawSubItem (Spalte 0).
			// Nur hier reicht nicht – das dunkle Windows-Design legt danach eine eckige Auswahl
			// darüber. Nur dort reicht nicht – in der Gruppenansicht (Suchergebnisse) zeichnet
			// Windows beim Überfahren manchmal nur diesen Schritt neu, die Zeile bliebe leer.
			if((m_p == null) || (m_lv.View != View.Details)) { e.DrawDefault = true; return; }
			if(!PaintRow(e.Graphics, e.Item, e.ItemIndex)) e.DrawDefault = true;
		}

		private void OnDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
		{
			if((m_p == null) || (m_lv.View != View.Details)) { e.DrawDefault = true; return; }
			if(e.ColumnIndex != 0) return; // schon mit Spalte 0 gezeichnet
			if(!PaintRow(e.Graphics, e.Item, e.ItemIndex)) e.DrawDefault = true;
		}

		/// <summary>Ganze Zeile in einem Durchgang (Hintergrund + alle Spalten), damit beim
		/// Überfahren nichts zwischenzeitlich leer aufblitzt.</summary>
		private bool PaintRow(Graphics g, ListViewItem item, int itemIndex)
		{
			try
			{
				Rectangle row = item.Bounds;

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
				else if(m_hoverEnabled && (itemIndex == m_hot))
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
						badge = L.T("Abgelaufen");
						badgeColor = m_p.Weak;
					}
					else if(m_expiryDays > 0)
					{
						int days = (int)Math.Floor((pe.ExpiryTime.ToLocalTime().Date - DateTime.Now.Date).TotalDays);
						if(days <= m_expiryDays)
						{
							badge = ((days <= 0) ? L.T("heute") : ((days == 1) ? L.T("morgen") :
								L.F("in {0} Tagen", days)));
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
						// Breite mit denselben Flags wie beim Zeichnen messen (inkl. Innenabstand),
						// sonst kürzt Windows den Titel mit „…“, obwohl er hineinpasst
						int tw = TextRenderer.MeasureText(g, text, f, Size.Empty,
							TfCell & ~TextFormatFlags.EndEllipsis).Width;
						// Badge direkt hinter dem Titel, bei wenig Platz am rechten Rand der Spalte
						int bx = Math.Min(tx + tw + D(6), cell.Right - D(6) - bw);
						if(bx > tx + D(30))
						{
							Rectangle rb = new Rectangle(bx, cell.Top + (cell.Height - bh) / 2, bw, bh);
							Gfx.Fill(g, rb, bh / 2, Palette.Blend(bg, badgeColor, m_p.Dark ? 0.28 : 0.16));
							Color btc = (m_p.Dark ? badgeColor : Palette.Blend(badgeColor, Color.Black, 0.1));
							TextRenderer.DrawText(g, badge, m_badgeFont, rb, btc, TextFormatFlags.HorizontalCenter |
								TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
							textRight = bx - D(2);
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
				return true;
			}
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				return false;
			}
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

		/// <summary>Malt die leere Fläche unter dem letzten Eintrag in der Listenfarbe aus. Im dunklen
		/// Windows-Design zeichnet die Liste dort sonst senkrechte Spaltenlinien bis zum unteren Rand.</summary>
		private sealed class EmptyAreaPainter : NativeWindow
		{
			private const int WM_PAINT = 0x000F;

			[StructLayout(LayoutKind.Sequential)]
			private struct RECT
			{
				public int Left, Top, Right, Bottom;
			}

			[DllImport("user32.dll")]
			private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

			[DllImport("user32.dll")]
			private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

			[DllImport("user32.dll", EntryPoint = "SendMessageW")]
			private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

			[DllImport("user32.dll", EntryPoint = "SendMessageW")]
			private static extern IntPtr SendRect(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

			private const int HDM_GETITEMCOUNT = 0x1200, HDM_GETITEMRECT = 0x1207;

			private readonly EntryListPainter m_owner;
			private readonly HeaderFiller m_header;

			public EmptyAreaPainter(EntryListPainter owner)
			{
				m_owner = owner;
				m_header = new HeaderFiller(this);
				ListView lv = owner.m_lv;
				lv.HandleCreated += OnHandleCreated;
				lv.HandleDestroyed += OnHandleDestroyed;
				if(lv.IsHandleCreated && (this.Handle == IntPtr.Zero)) AssignHandle(lv.Handle);
				m_header.Attach(NativeMethods.GetListHeader(lv));
			}

			public void Detach()
			{
				ListView lv = m_owner.m_lv;
				lv.HandleCreated -= OnHandleCreated;
				lv.HandleDestroyed -= OnHandleDestroyed;
				m_header.Detach();
				if(this.Handle != IntPtr.Zero) ReleaseHandle();
			}

			private void OnHandleCreated(object sender, EventArgs e)
			{
				if(this.Handle == IntPtr.Zero) AssignHandle(m_owner.m_lv.Handle);
				m_header.Attach(NativeMethods.GetListHeader(m_owner.m_lv));
			}

			private void OnHandleDestroyed(object sender, EventArgs e)
			{
				m_header.Detach();
				if(this.Handle != IntPtr.Zero) ReleaseHandle();
			}

			/// <summary>Spaltenkopf rechts neben der letzten Spalte: Windows malt die freie Fläche dort
			/// im dunklen Design als schwarzen Block – in Listenfarbe mit Linie unten übermalen.</summary>
			private sealed class HeaderFiller : NativeWindow
			{
				private const int WM_PAINT = 0x000F;
				private readonly EmptyAreaPainter m_parent;

				public HeaderFiller(EmptyAreaPainter parent)
				{
					m_parent = parent;
				}

				public void Attach(IntPtr hdr)
				{
					if((hdr == IntPtr.Zero) || (this.Handle == hdr)) return;
					if(this.Handle != IntPtr.Zero) ReleaseHandle();
					AssignHandle(hdr);
				}

				public void Detach()
				{
					if(this.Handle != IntPtr.Zero) ReleaseHandle();
				}

				protected override void WndProc(ref Message m)
				{
					base.WndProc(ref m);
					if(m.Msg != WM_PAINT) return;
					try { m_parent.PaintHeaderFiller(this.Handle); }
					catch(Exception ex) { FluentThemeExt.Report(ex); }
				}
			}

			private void PaintHeaderFiller(IntPtr hdr)
			{
				Palette p = m_owner.m_p;
				if((p == null) || !m_owner.m_attached) return;
				RECT cr;
				if(!GetClientRect(hdr, out cr)) return;
				int n = SendMessage(hdr, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
				int right = 0;
				for(int i = 0; i < n; ++i)
				{
					RECT ir = new RECT();
					SendRect(hdr, HDM_GETITEMRECT, new IntPtr(i), ref ir);
					right = Math.Max(right, ir.Right);
				}
				int w = cr.Right - cr.Left, h = cr.Bottom - cr.Top;
				if((right >= w) || (h <= 0)) return;
				using(Graphics g = Graphics.FromHwnd(hdr))
				{
					using(SolidBrush b = new SolidBrush(p.Pane)) { g.FillRectangle(b, right, 0, w - right, h); }
					using(Pen pen = new Pen(p.Border)) { g.DrawLine(pen, right, h - 1, w, h - 1); }
				}
			}

			protected override void WndProc(ref Message m)
			{
				// Gruppenüberschriften (Suchergebnisse) selbst zeichnen
				if((m.Msg == WM_REFLECT_NOTIFY) && (m.LParam != IntPtr.Zero))
				{
					try { if(DrawGroupHeader(ref m)) return; }
					catch(Exception ex) { FluentThemeExt.Report(ex); }
				}
				base.WndProc(ref m);
				if(m.Msg != WM_PAINT) return;
				try { Paint(); }
				catch(Exception ex) { FluentThemeExt.Report(ex); }
			}

			private const int WM_REFLECT_NOTIFY = 0x204E; // von WinForms an die Liste zurückgereichtes WM_NOTIFY
			private const int NM_CUSTOMDRAW = -12;
			private const int CDDS_PREPAINT = 0x0001;
			private const int CDRF_SKIPDEFAULT = 0x0004;
			private const int LVCDI_GROUP = 0x0001;
			private const int LVM_GETGROUPRECT = 0x1062;
			private const int LVGGR_HEADER = 1;

			[StructLayout(LayoutKind.Sequential)]
			private struct NMHDR
			{
				public IntPtr hwndFrom;
				public UIntPtr idFrom;
				public int code;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct NMCUSTOMDRAW
			{
				public NMHDR hdr;
				public int dwDrawStage;
				public IntPtr hdc;
				public RECT rc;
				public UIntPtr dwItemSpec;
				public uint uItemState;
				public IntPtr lItemlParam;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct NMLVCUSTOMDRAW
			{
				public NMCUSTOMDRAW nmcd;
				public int clrText;
				public int clrTextBk;
				public int iSubItem;
				public int dwItemType;
			}

			private static PropertyInfo s_groupId;

			/// <summary>Überschrift einer Gruppe (z. B. „Privat → Internet“) im Stil der Spaltenköpfe:
			/// grau, halbfett, ohne Linie. Statt des Windows-Blaus, das im Dunkelmodus kaum lesbar ist.</summary>
			private bool DrawGroupHeader(ref Message m)
			{
				Palette p = m_owner.m_p;
				if((p == null) || !m_owner.m_attached) return false;
				NMHDR hdr = (NMHDR)Marshal.PtrToStructure(m.LParam, typeof(NMHDR));
				if(hdr.code != NM_CUSTOMDRAW) return false;
				NMLVCUSTOMDRAW cd = (NMLVCUSTOMDRAW)Marshal.PtrToStructure(m.LParam, typeof(NMLVCUSTOMDRAW));
				if((cd.nmcd.dwDrawStage != CDDS_PREPAINT) || (cd.dwItemType != LVCDI_GROUP)) return false;

				ListView lv = m_owner.m_lv;
				int id = (int)cd.nmcd.dwItemSpec.ToUInt32();
				RECT r = new RECT();
				r.Top = LVGGR_HEADER;
				if(SendRect(lv.Handle, LVM_GETGROUPRECT, new IntPtr(id), ref r) == IntPtr.Zero) return false;
				Rectangle rc = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
				if((rc.Width <= 0) || (rc.Height <= 0) || (cd.nmcd.hdc == IntPtr.Zero)) return false;

				string text = GroupText(lv, id);
				using(Graphics g = Graphics.FromHdc(cd.nmcd.hdc))
				{
					using(SolidBrush b = new SolidBrush(p.Pane)) { g.FillRectangle(b, rc); }
					Font f;
					try { f = new Font("Segoe UI Semibold", lv.Font.SizeInPoints); }
					catch(ArgumentException) { f = new Font(lv.Font, FontStyle.Bold); }
					using(f)
					{
						Rectangle tr = Rectangle.FromLTRB(rc.Left + m_owner.D(10), rc.Top, rc.Right - m_owner.D(10), rc.Bottom);
						TextRenderer.DrawText(g, text, f, tr, p.Sub, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
							TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
					}
				}
				m.Result = new IntPtr(CDRF_SKIPDEFAULT);
				return true;
			}

			/// <summary>Text der Gruppe mit der Windows-ID (WinForms führt die ID intern).</summary>
			private static string GroupText(ListView lv, int id)
			{
				if(s_groupId == null)
					s_groupId = typeof(ListViewGroup).GetProperty("ID", BindingFlags.Instance | BindingFlags.NonPublic);
				foreach(ListViewGroup grp in lv.Groups)
				{
					if((s_groupId != null) && (Convert.ToInt32(s_groupId.GetValue(grp, null)) == id)) return grp.Header;
				}
				return string.Empty;
			}

			private void Paint()
			{
				ListView lv = m_owner.m_lv;
				Palette p = m_owner.m_p;
				if((p == null) || !m_owner.m_attached || (lv.View != View.Details)) return;

				int top = 0;
				IntPtr hdr = NativeMethods.GetListHeader(lv);
				RECT hr;
				if((hdr != IntPtr.Zero) && GetWindowRect(hdr, out hr)) top = Math.Max(0, hr.Bottom - hr.Top);
				// Unterkante des untersten Eintrags. In der Gruppenansicht (Suchergebnisse) ist der
				// letzte Eintrag der Liste nicht der unterste auf dem Bildschirm – dort alle prüfen,
				// sonst würden weitere Gruppen übermalt. Gruppenüberschriften liegen über ihren Einträgen.
				int n = lv.Items.Count;
				if(n > 0)
				{
					if(lv.ShowGroups && (lv.Groups.Count > 0))
					{
						for(int i = 0; i < n; ++i) top = Math.Max(top, lv.GetItemRect(i).Bottom);
					}
					else top = Math.Max(top, lv.GetItemRect(n - 1).Bottom);
				}

				Size cs = lv.ClientSize;
				if(top >= cs.Height) return;
				using(Graphics g = Graphics.FromHwnd(lv.Handle))
				using(SolidBrush b = new SolidBrush(p.Pane))
				{
					g.FillRectangle(b, 0, top, cs.Width, cs.Height - top);
				}
			}
		}
	}
}
