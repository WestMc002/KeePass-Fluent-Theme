// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

using KeePass.Plugins;

using KeePassLib;
using KeePassLib.Security;

namespace FluentTheme
{
	/// <summary>Moderne Detailansicht unter der Eintragsliste: Anmeldedaten, weitere Felder
	/// (z. B. URL 2) sowie Notizen und Info als Karten.</summary>
	internal sealed class DetailsPanel : Panel
	{
		private enum Glyph
		{
			Copy,
			Eye,
			EyeOff,
			Open,
			Save,
			Check
		}

		private sealed class Btn
		{
			public readonly Glyph Glyph;
			public readonly string Tip;
			public readonly Action Click;

			public Btn(Glyph g, string tip, Action click)
			{
				this.Glyph = g;
				this.Tip = tip;
				this.Click = click;
			}
		}

		private sealed class Hit
		{
			public Rectangle Rect;
			public Action Click;
			public string Tip;
			public bool IsCopy;
		}

		private const TextFormatFlags TfLine = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
			TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
		private const string Mask = "\u25CF\u25CF\u25CF\u25CF\u25CF\u25CF\u25CF\u25CF\u25CF\u25CF";

		// Schriftgrößen in Punkt (werden von Windows mit der Anzeigeskalierung vergrößert)
		private const float SizeBase = 9.75f;
		private const float SizeLabel = 9f;
		private const float SizeTitle = 13f;
		private const float SizeHead = 10.5f;

		private readonly IPluginHost m_host;
		private readonly ListView m_lv;
		private readonly EntryListPainter m_painter;
		private readonly List<Hit> m_hits = new List<Hit>();
		private readonly ToolTip m_tip = new ModernToolTip();

		private Palette m_p;
		private float m_s = 1f;
		private Font m_fBase;
		private Font m_fLink;
		private Font m_fLabel;
		private Font m_fTitle;
		private Font m_fHead;
		private Font m_fMono;
		private int m_rowH = 32;

		private PwEntry m_entry;
		private DateTime m_entryMod = DateTime.MinValue;
		private int m_selCount = -1;
		private bool m_reveal;
		private int m_bits = -1;
		private Rectangle m_hotRect = Rectangle.Empty;
		private string m_tipText = string.Empty;
		private int m_contentH;

		// Passwort automatisch wieder verbergen, Rückmeldung beim Kopieren, Notizen aufklappen
		private readonly Timer m_timer = new Timer();
		private int m_revealSeconds = 15;
		private DateTime m_revealUntil = DateTime.MaxValue;
		private Rectangle m_copiedRect = Rectangle.Empty;
		private Rectangle m_shotHead = Rectangle.Empty;   // Kopfzeile (Symbol, Titel) – für Screenshots
		private Rectangle m_shotCard = Rectangle.Empty;   // erste Karte „Anmeldedaten“
		private DateTime m_copiedUntil = DateTime.MinValue;
		private int m_clearSeconds;
		private bool m_notesExpanded;
		private NotesBox m_notesBox;      // Notizen als markierbarer Text (einzelne Teile kopieren)
		private bool m_notesPlaced;       // beim Zeichnen gesetzt; sonst wird das Feld ausgeblendet
		private Totp m_totp;
		private long m_totpTick = -1;

		/// <summary>Klick auf einen Tag-Chip im Kopf der Detailansicht.</summary>
		public event EventHandler<TagEventArgs> TagClicked;

		/// <summary>Sekunden, nach denen ein angezeigtes Passwort wieder verborgen wird (0 = nie).</summary>
		public int RevealSeconds
		{
			get { return m_revealSeconds; }
			set
			{
				m_revealSeconds = Math.Max(0, value);
				if(m_reveal) m_revealUntil = ((m_revealSeconds > 0) ? DateTime.Now.AddSeconds(m_revealSeconds) :
					DateTime.MaxValue);
				UpdateTimer();
			}
		}

		// Breiten der drei Karten (Anteile), per Maus zwischen den Karten verstellbar
		private readonly float[] m_colRatio = new float[] { 1f / 3f, 1f / 3f, 1f / 3f };
		private readonly Rectangle[] m_splitters = new Rectangle[2];
		private int m_splitHot = -1;
		private int m_splitDrag = -1;
		private int m_splitAvail;   // Breite aller Karten ohne Lücken beim Ziehen
		private int m_splitLeft;    // linker Rand der ersten Karte beim Ziehen

		/// <summary>Kartenbreiten wurden mit der Maus geändert (zum Speichern).</summary>
		public event EventHandler ColumnsChanged;

		/// <summary>Anteile der drei Karten als Text „a;b;c“ (invariant), z. B. zum Speichern.</summary>
		public string ColumnRatios
		{
			get
			{
				return string.Join(";", new string[] {
					m_colRatio[0].ToString("0.####", CultureInfo.InvariantCulture),
					m_colRatio[1].ToString("0.####", CultureInfo.InvariantCulture),
					m_colRatio[2].ToString("0.####", CultureInfo.InvariantCulture) });
			}
			set
			{
				float[] r = new float[] { 1f / 3f, 1f / 3f, 1f / 3f };
				try
				{
					string[] parts = (value ?? string.Empty).Split(';');
					if(parts.Length == 3)
					{
						float a, b, c;
						if(float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a) &&
							float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b) &&
							float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c) &&
							(a > 0.05f) && (b > 0.05f) && (c > 0.05f))
						{
							float sum = a + b + c;
							r = new float[] { a / sum, b / sum, c / sum };
						}
					}
				}
				catch(Exception) { }
				for(int i = 0; i < 3; ++i) m_colRatio[i] = r[i];
				Invalidate();
			}
		}

		/// <summary>Benötigte Höhe des Inhalts hat sich geändert (für die automatische Höhe).</summary>
		public event EventHandler ContentHeightChanged;

		/// <summary>Benötigte Höhe für den aktuellen Eintrag (0 = kein Eintrag ausgewählt).</summary>
		public int ContentHeight
		{
			get { return m_contentH; }
		}

		public DetailsPanel(IPluginHost host, ListView lv, EntryListPainter painter)
		{
			m_host = host;
			m_lv = lv;
			m_painter = painter;

			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
				ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			this.AutoScroll = true;
			this.Dock = DockStyle.Fill;
			this.Name = "FluentThemeDetails";

			m_timer.Interval = 250;
			m_timer.Tick += OnTimerTick;
		}

		private void UpdateTimer()
		{
			bool need = ((m_reveal && (m_revealSeconds > 0)) || !m_copiedRect.IsEmpty || (m_totp != null));
			if(need != m_timer.Enabled) m_timer.Enabled = need;
		}

		/// <summary>Für Screenshots: keine Aktualisierung (Countdown des Einmalcodes) zwischen den
		/// beiden Aufnahmen vor schwarzem und weißem Hintergrund.</summary>
		public bool Frozen;

		private void OnTimerTick(object sender, EventArgs e)
		{
			if(this.Frozen) return;
			try
			{
				DateTime now = DateTime.Now;
				if(m_reveal && (m_revealSeconds > 0) && (now >= m_revealUntil))
				{
					m_reveal = false;
					m_revealUntil = DateTime.MaxValue;
					Invalidate();
				}
				if(m_totp != null)
				{
					long tick = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
					if(tick != m_totpTick)
					{
						m_totpTick = tick;
						Invalidate();
					}
				}
				if(!m_copiedRect.IsEmpty && (now >= m_copiedUntil))
				{
					m_copiedRect = Rectangle.Empty;
					Invalidate();
				}
				UpdateTimer();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing)
			{
				DisposeFonts();
				m_timer.Dispose();
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
			if(m_fBase != null) { m_fBase.Dispose(); m_fBase = null; }
			if(m_fLink != null) { m_fLink.Dispose(); m_fLink = null; }
			if(m_fLabel != null) { m_fLabel.Dispose(); m_fLabel = null; }
			if(m_fTitle != null) { m_fTitle.Dispose(); m_fTitle = null; }
			if(m_fHead != null) { m_fHead.Dispose(); m_fHead = null; }
			if(m_fMono != null) { m_fMono.Dispose(); m_fMono = null; }
		}

		private static Font MakeFont(string family, float size, FontStyle style, FontStyle fallbackStyle)
		{
			try
			{
				using(FontFamily ff = new FontFamily(family)) { }
				return new Font(family, size, style);
			}
			catch(ArgumentException)
			{
				return new Font(SystemFonts.MessageBoxFont.FontFamily, size, style | fallbackStyle);
			}
		}

		public void SetPalette(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
			this.BackColor = p.Bg;
			this.ForeColor = p.Text;

			DisposeFonts();
			m_fBase = MakeFont("Segoe UI", SizeBase, FontStyle.Regular, FontStyle.Regular);
			m_fLink = MakeFont("Segoe UI", SizeBase, FontStyle.Underline, FontStyle.Regular);
			m_fLabel = MakeFont("Segoe UI", SizeLabel, FontStyle.Regular, FontStyle.Regular);
			m_fTitle = MakeFont("Segoe UI Semibold", SizeTitle, FontStyle.Regular, FontStyle.Bold);
			m_fHead = MakeFont("Segoe UI Semibold", SizeHead, FontStyle.Regular, FontStyle.Bold);
			m_fMono = MakeFont("Consolas", SizeBase + 0.5f, FontStyle.Regular, FontStyle.Regular);
			m_rowH = Math.Max(D(30), m_fBase.Height + D(14));
			if(m_notesBox != null) StyleNotesBox();
			Invalidate();
		}

		public void RefreshIfChanged(bool force)
		{
			PwEntry pe = null;
			int cnt = m_lv.SelectedIndices.Count;
			if(cnt == 1)
			{
				try { pe = m_host.MainWindow.GetSelectedEntry(true); }
				catch(Exception) { pe = null; }
			}

			DateTime mod = ((pe != null) ? pe.LastModificationTime : DateTime.MinValue);
			if(!force && (pe == m_entry) && (mod == m_entryMod) && (cnt == m_selCount)) return;

			if(pe != m_entry)
			{
				m_reveal = false;
				m_notesExpanded = false;
				m_copiedRect = Rectangle.Empty;
				UpdateTimer();
				this.AutoScrollPosition = new Point(0, 0);
			}
			m_entry = pe;
			m_entryMod = mod;
			m_totp = Totp.FromEntry(pe);
			UpdateTimer();
			m_selCount = cnt;
			m_bits = -1;
			Invalidate();
		}

		private void ToggleReveal()
		{
			m_reveal = !m_reveal;
			m_revealUntil = ((m_reveal && (m_revealSeconds > 0)) ? DateTime.Now.AddSeconds(m_revealSeconds) :
				DateTime.MaxValue);
			UpdateTimer();
			Invalidate();
		}

		private void OnTagClick(string tag)
		{
			EventHandler<TagEventArgs> h = TagClicked;
			if(h != null) h(this, new TagEventArgs(tag));
		}

		private void CopyTotp()
		{
			if((m_totp == null) || (m_entry == null)) return;
			int left;
			string code = m_totp.Generate(DateTime.UtcNow, out left);
			KpCompat.Copy(m_host, new ProtectedString(true, code), m_entry);
		}

		private void ToggleNotes()
		{
			m_notesExpanded = !m_notesExpanded;
			Invalidate();
		}

		/// <summary>Setzt das markierbare Notizenfeld an die gezeichnete Stelle. false = nicht möglich,
		/// dann wird der Text wie bisher gezeichnet.</summary>
		private bool PlaceNotesBox(Rectangle rn, string notes)
		{
			try
			{
				if(m_notesBox == null)
				{
					m_notesBox = new NotesBox();
					StyleNotesBox();
					this.Controls.Add(m_notesBox);
				}
				// Zeilenumbrüche wie im Eingabefeld (CRLF), sonst zeigt das Feld sie nicht an
				string t = notes.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
				if(m_notesBox.Text != t)
				{
					m_notesBox.Text = t;
					m_notesBox.Select(0, 0);
				}
				if(m_notesBox.Bounds != rn)
				{
					bool hChanged = (m_notesBox.Height != rn.Height);
					m_notesBox.Bounds = rn;
					// Auf-/Zuklappen: wieder ab der ersten Zeile anzeigen
					if(hChanged && (m_notesBox.SelectionLength == 0)) { m_notesBox.Select(0, 0); m_notesBox.ScrollToCaret(); }
				}
				if(!m_notesBox.Visible) m_notesBox.Visible = true;
				m_notesPlaced = true;
				return true;
			}
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				return false;
			}
		}

		private void StyleNotesBox()
		{
			m_notesBox.Font = m_fBase;
			m_notesBox.BackColor = m_p.Card;
			m_notesBox.ForeColor = m_p.Text;
			m_notesBox.SetNoMargins();
		}

		/// <summary>Schreibgeschütztes, randloses Textfeld für die Notizen: Text mit der Maus markieren
		/// und mit Strg+C oder dem Kontextmenü kopieren.</summary>
		private sealed class NotesBox : TextBox
		{
			private const int EM_SETMARGINS = 0xD3, EC_LEFTMARGIN = 1, EC_RIGHTMARGIN = 2;

			[System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
			private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

			public NotesBox()
			{
				this.ReadOnly = true;
				this.Multiline = true;
				this.WordWrap = true;
				this.ScrollBars = ScrollBars.None;
				this.BorderStyle = BorderStyle.None;
				this.TabStop = false;
				this.Cursor = Cursors.IBeam;
				this.HideSelection = true;
				this.Visible = false;
			}

			public void SetNoMargins()
			{
				if(this.IsHandleCreated)
					SendMessage(this.Handle, EM_SETMARGINS, new IntPtr(EC_LEFTMARGIN | EC_RIGHTMARGIN), IntPtr.Zero);
			}

			protected override void OnHandleCreated(EventArgs e)
			{
				base.OnHandleCreated(e);
				SetNoMargins();
			}

			protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
			{
				// Strg+C/Strg+A hier selbst behandeln – sonst greifen die Tastenkürzel von KeePass
				// (z. B. Passwort kopieren) und nicht die Markierung
				if((keyData == (Keys.Control | Keys.C)) || (keyData == (Keys.Control | Keys.Insert)))
				{
					if(this.SelectionLength > 0) Copy();
					return true;
				}
				if(keyData == (Keys.Control | Keys.A))
				{
					SelectAll();
					return true;
				}
				return base.ProcessCmdKey(ref msg, keyData);
			}
		}

		private void CopyField(PwEntry pe, string key)
		{
			if(pe == null) return;
			ProtectedString ps = pe.Strings.Get(key);
			if(ps == null) return;
			KpCompat.Copy(m_host, ps, pe);
		}

		private void SetContentHeight(int h)
		{
			h = Math.Max(0, h);
			Size s = new Size(0, h);
			if(this.AutoScrollMinSize != s) this.AutoScrollMinSize = s;
			if(h == m_contentH) return;
			m_contentH = h;
			EventHandler eh = ContentHeightChanged;
			if((eh != null) && this.IsHandleCreated)
				BeginInvoke(new MethodInvoker(delegate
				{
					try { eh(this, EventArgs.Empty); }
					catch(Exception ex) { FluentThemeExt.Report(ex); }
				}));
		}

		private void AddHit(Rectangle r, Action click, string tip)
		{
			AddHit(r, click, tip, false);
		}

		private void AddHit(Rectangle r, Action click, string tip, bool isCopy)
		{
			Hit h = new Hit();
			h.Rect = r;
			h.Click = click;
			h.Tip = tip;
			h.IsCopy = isCopy;
			m_hits.Add(h);
		}

		private int ButtonSize
		{
			get { return Math.Max(D(28), m_fBase.Height + D(8)); }
		}

		/// <summary>Symbol-Knopf (Kopieren, Anzeigen, Öffnen …) inkl. Hover und „Kopiert“-Häkchen.</summary>
		private void DrawIconButton(Graphics g, Rectangle rb, Btn b)
		{
			bool copied = ((b.Glyph == Glyph.Copy) && (rb == m_copiedRect));
			bool hot = (rb == m_hotRect);
			Color bg = m_p.Card;
			if(copied)
			{
				bg = Palette.Blend(m_p.Card, m_p.Good, m_p.Dark ? 0.25 : 0.14);
				Gfx.Fill(g, rb, D(6), bg);
				Gfx.Stroke(g, rb, D(6), Palette.Blend(m_p.Card, m_p.Good, 0.45));
			}
			else if(hot)
			{
				bg = m_p.BtnBg;
				Gfx.Fill(g, rb, D(6), bg);
				Gfx.Stroke(g, rb, D(6), m_p.Border);
			}
			DrawGlyph(g, copied ? Glyph.Check : b.Glyph, rb, copied ? m_p.Good : m_p.Text, bg);
			AddHit(rb, b.Click, b.Tip, b.Glyph == Glyph.Copy);
		}

		/// <summary>„Kopiert ✓ · wird nach x s geleert“ links neben dem gedrückten Kopieren-Knopf.</summary>
		private void DrawCopiedLabel(Graphics g, Rectangle rb, int minX)
		{
			// Lange Fassung, wenn Platz ist, sonst kürzer – nie über den Wert schreiben oder abschneiden
			List<string> variants = new List<string>();
			if(m_clearSeconds > 0)
			{
				variants.Add(L.F("Kopiert · Zwischenablage wird nach {0} s geleert", m_clearSeconds));
				variants.Add(L.F("Kopiert · wird nach {0} s geleert", m_clearSeconds));
			}
			variants.Add(L.T("Kopiert"));

			int avail = rb.Left - D(6) - minX;
			string txt = null;
			int tw = 0;
			foreach(string v in variants)
			{
				tw = TextWidth(g, v, m_fLabel) + D(16);
				if(tw <= avail) { txt = v; break; }
			}
			if(txt == null) return; // nicht einmal „Kopiert“ passt – der Haken am Knopf genügt

			int h = m_fLabel.Height + D(8);
			Rectangle r = new Rectangle(rb.Left - D(6) - tw, rb.Top + (rb.Height - h) / 2, tw, h);
			Color bg = Palette.Blend(m_p.Card, m_p.Good, m_p.Dark ? 0.25 : 0.14);
			Gfx.Fill(g, r, h / 2, bg);
			TextRenderer.DrawText(g, txt, m_fLabel, r, m_p.Dark ? m_p.Good : Palette.Blend(m_p.Good, Color.Black, 0.15),
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
				TextFormatFlags.NoPrefix);
		}

		private Hit HitAt(Point pt)
		{
			foreach(Hit h in m_hits)
			{
				if((h.Click != null) && h.Rect.Contains(pt)) return h;
			}
			foreach(Hit h in m_hits)
			{
				if(h.Rect.Contains(pt)) return h;
			}
			return null;
		}

		private static int TextWidth(Graphics g, string text, Font f)
		{
			if(string.IsNullOrEmpty(text)) return 0;
			return TextRenderer.MeasureText(g, text, f, Size.Empty, TextFormatFlags.NoPrefix |
				TextFormatFlags.SingleLine).Width;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if((m_p == null) || (m_fBase == null))
			{
				base.OnPaint(e);
				return;
			}

			Graphics g = e.Graphics;
			g.Clear(m_p.Bg);
			m_hits.Clear();
			m_notesPlaced = false;
			try { PaintContent(g); }
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				TextRenderer.DrawText(g, L.T("Detailansicht konnte nicht gezeichnet werden " +
					"(Extras › Fluent-Design › Fehlerbericht kopieren)."), m_fBase,
					this.ClientRectangle, m_p.Sub, TextFormatFlags.HorizontalCenter |
					TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
			}
			if(!m_notesPlaced && (m_notesBox != null) && m_notesBox.Visible) m_notesBox.Visible = false;
		}

		private void PaintContent(Graphics g)
		{
			int pad = D(6);
			int x0 = pad;
			int w = this.ClientSize.Width - 2 * pad;
			int y = D(10) + this.AutoScrollPosition.Y;

			if(m_entry == null)
			{
				string msg = ((m_selCount > 1) ? L.F("{0} Einträge ausgewählt", m_selCount) :
					L.T("Kein Eintrag ausgewählt"));
				TextRenderer.DrawText(g, msg, m_fBase, this.ClientRectangle, m_p.Sub,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
				SetContentHeight(0);
				return;
			}
			if(w < D(160)) return;

			PwEntry pe = m_entry;

			// Kopfzeile: Symbol, Titel, Gruppenpfad, Tags
			int titleH = m_fTitle.Height + D(2);
			int pathH = m_fLabel.Height + D(2);
			int tile = Math.Max(D(40), titleH + pathH);
			Rectangle rTile = new Rectangle(x0, y, tile, tile);
			m_shotHead = rTile;
			Gfx.Fill(g, rTile, D(8), m_p.Card);
			Gfx.Stroke(g, rTile, D(8), m_p.Border);
			DrawEntryIcon(g, rTile);

			int tx = rTile.Right + D(12);
			int cx = x0 + w;
			int minX = tx + D(160);
			int chipH = m_fLabel.Height + D(10);
			List<string> tags = new List<string>();
			foreach(string t in pe.Tags) tags.Add(t);
			for(int i = tags.Count - 1; i >= 0; --i)
			{
				int cw = TextWidth(g, tags[i], m_fLabel) + D(14);
				if(cx - cw < minX) break;
				Rectangle rc = new Rectangle(cx - cw, y + (tile - chipH) / 2, cw, chipH);
				bool hotChip = (rc == m_hotRect);
				Gfx.Fill(g, rc, chipH / 2, hotChip ? Palette.Blend(m_p.BadgeBg, m_p.Accent, 0.15) : m_p.BadgeBg);
				Gfx.Stroke(g, rc, chipH / 2, hotChip ? m_p.Pill : m_p.BadgeBorder);
				TextRenderer.DrawText(g, tags[i], m_fLabel, rc, m_p.BadgeText, TextFormatFlags.HorizontalCenter |
					TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				string tag = tags[i];
				AddHit(rc, delegate { OnTagClick(tag); }, L.F("Alle Einträge mit dem Tag „{0}“ anzeigen", tag));
				cx = rc.Left - D(6);
			}

			int titleW = Math.Max(D(40), cx - tx - D(8));
			int ty = y + (tile - titleH - pathH) / 2;
			string title = pe.Strings.ReadSafe(PwDefs.TitleField);
			if(title.Length == 0) title = L.T("(ohne Titel)");
			TextRenderer.DrawText(g, title, m_fTitle, new Rectangle(tx, ty, titleW, titleH), m_p.Text, TfLine);
			TextRenderer.DrawText(g, GroupPath(pe), m_fLabel, new Rectangle(tx, ty + titleH, titleW, pathH),
				m_p.Sub, TfLine);
			y += tile + D(12);

			// Karten
			int gap = D(12);
			int minW = D(300);
			int cols = Math.Max(1, Math.Min(3, (w + gap) / (minW + gap)));
			int cardW = (w - (cols - 1) * gap) / cols;
			m_splitters[0] = Rectangle.Empty;
			m_splitters[1] = Rectangle.Empty;

			if(cols == 3)
			{
				// Eine Reihe: Breiten nach den gespeicherten Anteilen, dazwischen ziehbare Lücken
				int avail = w - 2 * gap;
				int[] cw = ColumnWidths(avail);
				int[] hs = new int[3];
				for(int i = 0; i < 3; ++i) hs[i] = DrawCard(g, i, new Rectangle(0, 0, cw[i], 0), false);
				int rowH = Math.Max(hs[0], Math.Max(hs[1], hs[2]));
				int x = x0;
				for(int k = 0; k < 3; ++k)
				{
					DrawCard(g, k, new Rectangle(x, y, cw[k], rowH), true);
					if(k == 0) m_shotCard = new Rectangle(x, y, cw[k], rowH);
					x += cw[k];
					if(k < 2)
					{
						m_splitters[k] = new Rectangle(x, y, gap, rowH);
						if((m_splitHot == k) || (m_splitDrag == k))
						{
							// Griff als Hinweis: schmaler, abgerundeter Balken mitten in der Lücke
							int gh = Math.Min(D(40), rowH - D(16));
							Color gc = ((m_splitDrag == k) ? m_p.Pill : Palette.Blend(m_p.Sub, m_p.Bg, 0.35));
							Gfx.Fill(g, new Rectangle(x + gap / 2 - D(2), y + (rowH - gh) / 2, D(4), gh), D(2), gc);
						}
						x += gap;
					}
				}
				m_splitAvail = avail;
				m_splitLeft = x0;
				y += rowH + gap;
			}
			else
			{
				int[] hs = new int[3];
				for(int i = 0; i < 3; ++i) hs[i] = DrawCard(g, i, new Rectangle(0, 0, cardW, 0), false);

				for(int i = 0; i < 3; i += cols)
				{
					int last = Math.Min(3, i + cols);
					int rowH = 0;
					for(int k = i; k < last; ++k) rowH = Math.Max(rowH, hs[k]);
					for(int k = i; k < last; ++k)
					{
						Rectangle rk = new Rectangle(x0 + (k - i) * (cardW + gap), y, cardW, rowH);
						DrawCard(g, k, rk, true);
						if(k == 0) m_shotCard = rk;
					}
					y += rowH + gap;
				}
			}

			SetContentHeight(y - this.AutoScrollPosition.Y - gap + D(8));
		}

		private void DrawEntryIcon(Graphics g, Rectangle rTile)
		{
			ImageList il = ((m_painter != null) ? m_painter.OriginalImages : null);
			if((il == null) || (m_lv.SelectedItems.Count != 1)) return;
			int idx = m_lv.SelectedItems[0].ImageIndex;
			if((idx < 0) || (idx >= il.Images.Count)) return;
			Size isz = il.ImageSize;
			il.Draw(g, rTile.Left + (rTile.Width - isz.Width) / 2, rTile.Top + (rTile.Height - isz.Height) / 2, idx);
		}

		private static string GroupPath(PwEntry pe)
		{
			PwGroup pg = pe.ParentGroup;
			if(pg == null) return string.Empty;
			try { return pg.GetFullPath(" › ", true); }
			catch(Exception) { return pg.Name; }
		}

		private int MinCardWidth
		{
			get { return D(220); }
		}

		/// <summary>Breiten der drei Karten aus den Anteilen, jede mindestens MinCardWidth.</summary>
		private int[] ColumnWidths(int avail)
		{
			int min = Math.Min(MinCardWidth, avail / 3);
			int w0 = Math.Max(min, (int)Math.Round(avail * m_colRatio[0]));
			int w1 = Math.Max(min, (int)Math.Round(avail * m_colRatio[1]));
			if(w0 + w1 > avail - min)
			{
				int over = w0 + w1 - (avail - min);
				int take0 = Math.Min(over, Math.Max(0, w0 - min));
				w0 -= take0;
				w1 = Math.Max(min, w1 - (over - take0));
			}
			return new int[] { w0, w1, Math.Max(0, avail - w0 - w1) };
		}

		private int SplitterAt(Point pt)
		{
			for(int i = 0; i < 2; ++i)
			{
				Rectangle r = m_splitters[i];
				if(!r.IsEmpty && Rectangle.Inflate(r, D(2), 0).Contains(pt)) return i;
			}
			return -1;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if(e.Button != MouseButtons.Left) return;
			int s = SplitterAt(e.Location);
			if(s < 0) return;
			m_splitDrag = s;
			this.Capture = true;
			Invalidate();
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if(m_splitDrag < 0) return;
			m_splitDrag = -1;
			this.Capture = false;
			Invalidate();
			EventHandler h = ColumnsChanged;
			if(h != null) h(this, EventArgs.Empty);
		}

		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			base.OnMouseDoubleClick(e);
			if((e.Button != MouseButtons.Left) || (SplitterAt(e.Location) < 0)) return;
			// Doppelklick auf die Lücke: alle Karten wieder gleich breit
			for(int i = 0; i < 3; ++i) m_colRatio[i] = 1f / 3f;
			Invalidate();
			EventHandler h = ColumnsChanged;
			if(h != null) h(this, EventArgs.Empty);
		}

		/// <summary>Beim Ziehen: Grenze zwischen Karte k und k+1 auf die Mausposition setzen.</summary>
		private void DragSplitter(int x)
		{
			int avail = m_splitAvail;
			if(avail <= 0) return;
			int gap = D(12);
			int min = Math.Min(MinCardWidth, avail / 3);
			int[] cw = ColumnWidths(avail);
			int pos = x - m_splitLeft - gap / 2; // Mausposition relativ zur ersten Karte
			if(m_splitDrag == 0)
			{
				int w0 = Math.Max(min, Math.Min(pos, cw[0] + cw[1] - min));
				cw[1] = cw[0] + cw[1] - w0;
				cw[0] = w0;
			}
			else
			{
				int left = cw[0] + gap;
				int w1 = Math.Max(min, Math.Min(pos - left, cw[1] + cw[2] - min));
				cw[2] = cw[1] + cw[2] - w1;
				cw[1] = w1;
			}
			for(int i = 0; i < 3; ++i) m_colRatio[i] = (float)cw[i] / avail;
			Invalidate();
		}

		private int DrawCard(Graphics g, int index, Rectangle r, bool draw)
		{
			if(index == 0) return CardLogin(g, r, draw);
			if(index == 1) return CardFields(g, r, draw);
			return CardInfo(g, r, draw);
		}

		private int DrawCardFrame(Graphics g, Rectangle r, string title, bool draw)
		{
			int hh = Math.Max(D(38), m_fHead.Height + D(20));
			if(draw)
			{
				Gfx.Fill(g, r, D(8), m_p.Card);
				Gfx.Stroke(g, r, D(8), m_p.Border);
				TextRenderer.DrawText(g, title, m_fHead, new Rectangle(r.Left + D(16), r.Top + D(2),
					r.Width - D(32), hh), m_p.Text, TfLine);
			}
			return r.Top + hh;
		}

		/// <summary>Breite der Beschriftungsspalte: so breit wie die längste Beschriftung,
		/// höchstens 40 % der Karte.</summary>
		private int LabelWidth(Graphics g, Rectangle r, IEnumerable<string> labels)
		{
			int mx = 0;
			foreach(string s in labels) mx = Math.Max(mx, TextWidth(g, s, m_fLabel));
			return Math.Max(D(90), Math.Min(mx + D(14), (r.Width * 2) / 5));
		}

		private int Row(Graphics g, Rectangle r, int y, int lw, string label, string value, Font font,
			Color color, bool draw, bool lineBelow, string valueTip, Action valueClick, Btn[] btns)
		{
			int rowH = m_rowH;
			if(!draw) return y + rowH;

			int lx = r.Left + D(16);
			TextRenderer.DrawText(g, label, m_fLabel, new Rectangle(lx, y, lw - D(8), rowH), m_p.Sub, TfLine);
			if(TextWidth(g, label, m_fLabel) > lw - D(8)) AddHit(new Rectangle(lx, y, lw - D(8), rowH), null, label);

			int bs = this.ButtonSize;
			int bx = r.Right - D(10);
			Rectangle copiedBtn = Rectangle.Empty;
			for(int i = btns.Length - 1; i >= 0; --i)
			{
				bx -= bs;
				Rectangle rb = new Rectangle(bx, y + (rowH - bs) / 2, bs, bs);
				DrawIconButton(g, rb, btns[i]);
				if((btns[i].Glyph == Glyph.Copy) && (rb == m_copiedRect)) copiedBtn = rb;
				bx -= D(2);
			}

			int vx = lx + lw;
			Rectangle rv = new Rectangle(vx, y, Math.Max(0, bx - vx - D(6)), rowH);
			string shown = (value ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
			if(shown.Length == 0) TextRenderer.DrawText(g, "–", font, rv, m_p.Sub, TfLine);
			else if(valueClick != null)
			{
				// Anklickbarer Link: unterstrichen beim Überfahren
				int tw = Math.Min(rv.Width, TextWidth(g, shown, font));
				Rectangle rl = new Rectangle(rv.Left, y + D(4), tw, rowH - D(8));
				bool hotLink = (rl == m_hotRect);
				TextRenderer.DrawText(g, shown, hotLink ? m_fLink : font, rv, color, TfLine);
				AddHit(rl, valueClick, valueTip);
			}
			else
			{
				TextRenderer.DrawText(g, shown, font, rv, color, TfLine);
				if(!string.IsNullOrEmpty(valueTip) && (TextWidth(g, shown, font) > rv.Width))
					AddHit(rv, null, valueTip);
			}

			if(!copiedBtn.IsEmpty)
			{
				// Rückmeldung erst hinter dem Wert – der Wert bleibt ganz lesbar. Reicht der Platz nicht,
				// wählt DrawCopiedLabel eine kürzere Fassung oder lässt nur den Haken am Knopf stehen
				int valueW = TextWidth(g, (shown.Length > 0) ? shown : "–", font);
				int minX = vx + Math.Min(rv.Width, valueW) + D(8);
				DrawCopiedLabel(g, new Rectangle(bx + D(2), copiedBtn.Top, copiedBtn.Width, copiedBtn.Height), minX);
			}

			if(lineBelow)
			{
				using(Pen p = new Pen(m_p.RowLine))
				{
					g.DrawLine(p, r.Left + D(10), y + rowH - 1, r.Right - D(10), y + rowH - 1);
				}
			}
			return y + rowH;
		}

		private int CardLogin(Graphics g, Rectangle r, bool draw)
		{
			PwEntry pe = m_entry;
			int y = DrawCardFrame(g, r, L.T("Anmeldedaten"), draw);
			int lw = LabelWidth(g, r, new string[] { L.T("Benutzername"), L.T("Passwort"), L.T("URL") });

			string user = pe.Strings.ReadSafe(PwDefs.UserNameField);
			string pw = pe.Strings.ReadSafe(PwDefs.PasswordField);
			string url = pe.Strings.ReadSafe(PwDefs.UrlField);

			y = Row(g, r, y, lw, L.T("Benutzername"), user, m_fBase, m_p.Text, draw, true, null, null, new Btn[] {
				new Btn(Glyph.Copy, L.T("Benutzername kopieren"), delegate { CopyField(pe, PwDefs.UserNameField); }) });

			string pwShown = ((pw.Length == 0) ? string.Empty : (m_reveal ? pw : Mask));
			y = Row(g, r, y, lw, L.T("Passwort"), pwShown, m_reveal ? m_fMono : m_fBase, m_p.Text, draw, true, null, null,
				new Btn[] {
					new Btn(m_reveal ? Glyph.EyeOff : Glyph.Eye, m_reveal ? L.T("Passwort verbergen") :
						L.T("Passwort anzeigen"), ToggleReveal),
					new Btn(Glyph.Copy, L.T("Passwort kopieren"), delegate { CopyField(pe, PwDefs.PasswordField); }) });

			Action openUrl = null;
			if(url.Trim().Length > 0) openUrl = delegate { KpCompat.OpenEntryUrl(m_host, pe); };
			y = Row(g, r, y, lw, L.T("URL"), url, m_fBase, m_p.Link, draw, (pw.Length > 0) || (m_totp != null), url, openUrl,
				new Btn[] {
				new Btn(Glyph.Open, L.T("URL öffnen"), delegate { KpCompat.OpenEntryUrl(m_host, pe); }),
				new Btn(Glyph.Copy, L.T("URL kopieren"), delegate { CopyField(pe, PwDefs.UrlField); }) });

			if(m_totp != null) y = TotpRow(g, r, y, lw, draw, pw.Length > 0);

			if(pw.Length > 0)
			{
				int sh = Math.Max(D(32), m_fLabel.Height + D(14));
				if(draw)
				{
					if(m_bits < 0) m_bits = KpCompat.EstimateBits(pw);
					int bits = m_bits;
					Color c;
					string lbl;
					if(bits < 50) { c = m_p.Weak; lbl = L.T("Schwach"); }
					else if(bits < 80) { c = m_p.Mid; lbl = L.T("Mittel"); }
					else if(bits < 112) { c = m_p.Good; lbl = L.T("Stark"); }
					else { c = m_p.Good; lbl = L.T("Sehr stark"); }

					string txt = lbl + " · " + bits.ToString(CultureInfo.CurrentCulture) + " Bit";
					int tw = TextWidth(g, txt, m_fLabel) + D(4);
					int lx = r.Left + D(16) + lw;
					int textX = r.Right - D(14) - tw;
					Rectangle track = new Rectangle(lx, y + sh / 2 - D(2), Math.Max(0, textX - D(12) - lx), D(4));
					Gfx.Fill(g, track, D(2), m_p.Border);
					Rectangle fill = track;
					fill.Width = (int)(track.Width * Math.Min(1.0, bits / 128.0));
					Gfx.Fill(g, fill, D(2), c);
					TextRenderer.DrawText(g, txt, m_fLabel, new Rectangle(textX, y, tw, sh), c,
						TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
						TextFormatFlags.NoPrefix);
				}
				y += sh;
			}
			return y + D(4) - r.Top;
		}

		/// <summary>Einmalcode (TOTP) mit Countdown-Ring und Kopieren-Knopf.</summary>
		private int TotpRow(Graphics g, Rectangle r, int y, int lw, bool draw, bool lineBelow)
		{
			int rowH = m_rowH + D(8);
			if(!draw) return y + rowH;

			int left;
			string code;
			try { code = m_totp.Generate(DateTime.UtcNow, out left); }
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				code = "–";
				left = 0;
			}

			int lx = r.Left + D(16);
			TextRenderer.DrawText(g, L.T("Einmalcode"), m_fLabel, new Rectangle(lx, y, lw - D(8), rowH), m_p.Sub, TfLine);

			int bs = this.ButtonSize;
			int bx = r.Right - D(10) - bs;
			Rectangle rb = new Rectangle(bx, y + (rowH - bs) / 2, bs, bs);
			DrawIconButton(g, rb, new Btn(Glyph.Copy, L.T("Einmalcode kopieren"), CopyTotp));

			// Code
			int vx = lx + lw;
			string shown = Totp.Pretty(code);
			int codeRight = vx;
			using(Font fc = MakeFont("Consolas", SizeTitle - 1f, FontStyle.Bold, FontStyle.Bold))
			{
				int cw = TextWidth(g, shown, fc);
				Rectangle rc = new Rectangle(vx, y, Math.Min(cw + D(4), Math.Max(0, bx - vx)), rowH);
				TextRenderer.DrawText(g, shown, fc, rc, m_p.Text, TfLine);
				AddHit(rc, CopyTotp, L.T("Klicken zum Kopieren"));
				codeRight = rc.Right;

				// Countdown-Ring
				int rs = D(18);
				int rx = rc.Right + D(14);
				if(rx + rs + D(40) < bx)
				{
					Rectangle ring = new Rectangle(rx, y + (rowH - rs) / 2, rs, rs);
					SmoothingMode old = g.SmoothingMode;
					g.SmoothingMode = SmoothingMode.AntiAlias;
					float pw = Math.Max(2f, 2.5f * m_s);
					RectangleF rf = new RectangleF(ring.X + pw / 2f, ring.Y + pw / 2f, ring.Width - pw, ring.Height - pw);
					using(Pen bg = new Pen(m_p.Border, pw)) { g.DrawEllipse(bg, rf); }
					Color rcol = ((left <= 5) ? m_p.Mid : m_p.Pill);
					float sweep = 360f * left / Math.Max(1, m_totp.Period);
					using(Pen fg = new Pen(rcol, pw))
					{
						fg.StartCap = LineCap.Round;
						fg.EndCap = LineCap.Round;
						if(sweep > 0.5f) g.DrawArc(fg, rf, -90f, sweep);
					}
					g.SmoothingMode = old;
					string sec = left.ToString(CultureInfo.CurrentCulture) + " s";
					TextRenderer.DrawText(g, sec, m_fLabel, new Rectangle(ring.Right + D(6), y, D(40), rowH),
						(left <= 5) ? m_p.Mid : m_p.Sub, TfLine);
				}
			}

			if(rb == m_copiedRect) DrawCopiedLabel(g, rb, codeRight + D(8)); // Code bleibt lesbar

			if(lineBelow)
			{
				using(Pen p = new Pen(m_p.RowLine))
				{
					g.DrawLine(p, r.Left + D(10), y + rowH - 1, r.Right - D(10), y + rowH - 1);
				}
			}
			return y + rowH;
		}

		private static bool LooksLikeUrl(string s)
		{
			if(string.IsNullOrEmpty(s)) return false;
			s = s.Trim();
			if((s.IndexOf(' ') >= 0) || (s.IndexOf('\n') >= 0)) return false;
			return ((s.IndexOf("://", StringComparison.Ordinal) > 0) ||
				s.StartsWith("www.", StringComparison.OrdinalIgnoreCase));
		}

		private int CardFields(Graphics g, Rectangle r, bool draw)
		{
			PwEntry pe = m_entry;
			int y = DrawCardFrame(g, r, L.T("Weitere Felder"), draw);

			List<KeyValuePair<string, ProtectedString>> list = new List<KeyValuePair<string, ProtectedString>>();
			foreach(KeyValuePair<string, ProtectedString> kvp in pe.Strings)
			{
				if(PwDefs.IsStandardField(kvp.Key)) continue;
				if((m_totp != null) && m_totp.SourceFields.Contains(kvp.Key)) continue;
				list.Add(kvp);
			}
			list.Sort(delegate(KeyValuePair<string, ProtectedString> a, KeyValuePair<string, ProtectedString> b)
			{
				return string.Compare(a.Key, b.Key, StringComparison.CurrentCultureIgnoreCase);
			});

			if(list.Count == 0)
			{
				if(draw)
					TextRenderer.DrawText(g, L.T("Keine weiteren Felder"), m_fLabel, new Rectangle(r.Left + D(16), y,
						r.Width - D(32), m_rowH), m_p.Sub, TfLine);
				return y + m_rowH + D(4) - r.Top;
			}

			List<string> labels = new List<string>();
			foreach(KeyValuePair<string, ProtectedString> kvp in list) labels.Add(kvp.Key);
			int lw = LabelWidth(g, r, labels);

			for(int i = 0; i < list.Count; ++i)
			{
				string key = list[i].Key;
				ProtectedString ps = list[i].Value;
				string val = ps.ReadString();
				bool prot = ps.IsProtected;
				bool masked = (prot && !m_reveal);
				bool isUrl = (!masked && LooksLikeUrl(val));

				List<Btn> bl = new List<Btn>();
				if(prot)
					bl.Add(new Btn(m_reveal ? Glyph.EyeOff : Glyph.Eye, m_reveal ? L.T("Verbergen") : L.T("Anzeigen"),
						ToggleReveal));
				Action open = null;
				if(isUrl)
				{
					string u = val.Trim();
					open = delegate { KpCompat.OpenUrl(m_host, u, pe); };
					bl.Add(new Btn(Glyph.Open, L.T("Öffnen"), open));
				}
				string k = key;
				bl.Add(new Btn(Glyph.Copy, L.F("{0} kopieren", key), delegate { CopyField(pe, k); }));

				y = Row(g, r, y, lw, key, masked ? ((val.Length > 0) ? Mask : string.Empty) : val,
					(prot && m_reveal) ? m_fMono : m_fBase, isUrl ? m_p.Link : m_p.Text, draw,
					i < list.Count - 1, masked ? null : val, open, bl.ToArray());
			}
			return y + D(4) - r.Top;
		}

		private static string FmtDate(DateTime utc)
		{
			return utc.ToLocalTime().ToString(L.DateFormat, CultureInfo.CurrentCulture);
		}

		private static string FmtDateTime(DateTime utc)
		{
			return utc.ToLocalTime().ToString(L.DateFormat + " HH:mm", CultureInfo.CurrentCulture);
		}

		private int MetaHeight
		{
			get { return D(10) + m_fLabel.Height + D(4) + m_fBase.Height + D(12); }
		}

		private void MetaCell(Graphics g, int x, int y, int w, string label, string value, Color c, string tip)
		{
			int ly = y + D(10);
			TextRenderer.DrawText(g, label, m_fLabel, new Rectangle(x, ly, w - D(4), m_fLabel.Height + D(2)),
				m_p.Sub, TfLine);
			int vy = ly + m_fLabel.Height + D(4);
			TextRenderer.DrawText(g, value, m_fBase, new Rectangle(x, vy, w - D(4), m_fBase.Height + D(2)), c, TfLine);
			if(!string.IsNullOrEmpty(tip)) AddHit(new Rectangle(x, y, w, this.MetaHeight), null, tip);
		}

		private int CardInfo(Graphics g, Rectangle r, bool draw)
		{
			PwEntry pe = m_entry;
			int y = DrawCardFrame(g, r, L.T("Notizen und Info"), draw);
			int innerX = r.Left + D(16);
			int innerW = Math.Max(D(40), r.Width - D(32));

			string notes = pe.Strings.ReadSafe(PwDefs.NotesField);
			TextFormatFlags nf = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak |
				TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.EndEllipsis;
			if(notes.Length == 0)
			{
				if(draw)
					TextRenderer.DrawText(g, L.T("Keine Notizen"), m_fLabel, new Rectangle(innerX, y, innerW, m_rowH),
						m_p.Sub, TfLine);
				y += m_rowH;
			}
			else
			{
				// Kopieren-Knopf rechts neben den Notizen
				int bs = this.ButtonSize;
				Rectangle rb = new Rectangle(r.Right - D(10) - bs, y, bs, bs);
				int textW = Math.Max(D(40), rb.Left - D(8) - innerX);

				Size ns = TextRenderer.MeasureText(g, notes, m_fBase, new Size(textW, int.MaxValue), nf);
				int collapsed = m_fBase.Height * 6;
				bool canExpand = (ns.Height > collapsed);
				int nh = ((canExpand && !m_notesExpanded) ? collapsed : ns.Height);
				if(draw)
				{
					Rectangle rn = new Rectangle(innerX, y + D(4), textW, nh);
					// Als markierbares Textfeld, damit sich auch einzelne Teile kopieren lassen
					if(!PlaceNotesBox(rn, notes)) TextRenderer.DrawText(g, notes, m_fBase, rn, m_p.Text, nf);
					DrawIconButton(g, rb, new Btn(Glyph.Copy, L.T("Notizen kopieren"),
						delegate { CopyField(pe, PwDefs.NotesField); }));
					if(rb == m_copiedRect)
						DrawCopiedLabel(g, rb, innerX + D(40));
				}
				y += nh + D(8);

				if(canExpand)
				{
					string more = (m_notesExpanded ? L.T("Weniger anzeigen") : L.T("Mehr anzeigen"));
					int lh = m_fLabel.Height + D(6);
					if(draw)
					{
						int mw = TextWidth(g, more, m_fLabel) + D(18);
						Rectangle rm = new Rectangle(innerX, y, mw, lh);
						bool hot = (rm == m_hotRect);
						TextRenderer.DrawText(g, more, m_fLabel, new Rectangle(rm.Left, rm.Top, rm.Width - D(14), rm.Height),
							m_p.Link, TfLine);
						Gfx.Chevron(g, rm.Right - D(7), rm.Top + rm.Height / 2f, D(8), m_notesExpanded, m_p.Link,
							Math.Max(1f, 1.3f * m_s));
						if(hot)
							using(Pen p = new Pen(m_p.Link))
							{
								int tw = TextWidth(g, more, m_fLabel);
								g.DrawLine(p, rm.Left, rm.Bottom - D(3), rm.Left + tw, rm.Bottom - D(3));
							}
						AddHit(rm, ToggleNotes, null);
					}
					y += lh;
				}
				y += D(6);
			}

			if(draw)
			{
				using(Pen p = new Pen(m_p.RowLine)) { g.DrawLine(p, r.Left + D(10), y, r.Right - D(10), y); }

				int colW = innerW / 3;
				MetaCell(g, innerX, y, colW, L.T("Erstellt"), FmtDate(pe.CreationTime), m_p.Text,
					FmtDateTime(pe.CreationTime));
				MetaCell(g, innerX + colW, y, colW, L.T("Geändert"), FmtDate(pe.LastModificationTime), m_p.Text,
					FmtDateTime(pe.LastModificationTime));

				string exp = L.T("nie");
				string expTip = null;
				Color ec = m_p.Text;
				if(pe.Expires)
				{
					exp = FmtDate(pe.ExpiryTime);
					expTip = FmtDateTime(pe.ExpiryTime);
					if(pe.ExpiryTime <= DateTime.UtcNow)
					{
						ec = m_p.Weak;
						expTip += L.T(" (abgelaufen)");
					}
				}
				MetaCell(g, innerX + 2 * colW, y, innerW - 2 * colW, L.T("Läuft ab"), exp, ec, expTip);
			}
			y += this.MetaHeight;

			if(pe.Binaries.UCount > 0)
			{
				int lineH = m_fBase.Height + D(8);
				if(draw)
				{
					using(Pen p = new Pen(m_p.RowLine)) { g.DrawLine(p, r.Left + D(10), y, r.Right - D(10), y); }
					TextRenderer.DrawText(g, L.T("Anhänge"), m_fLabel, new Rectangle(innerX, y + D(8), innerW,
						m_fLabel.Height + D(2)), m_p.Sub, TfLine);
				}
				y += D(12) + m_fLabel.Height;
				int bs = this.ButtonSize;
				lineH = Math.Max(lineH, bs + D(4));
				foreach(KeyValuePair<string, ProtectedBinary> kvp in pe.Binaries)
				{
					if(draw)
					{
						string name = kvp.Key;
						ProtectedBinary pb = kvp.Value;
						Btn[] ab = new Btn[] {
							new Btn(Glyph.Open, L.T("Öffnen"), delegate { OpenAttachment(name, pb); }),
							new Btn(Glyph.Save, L.T("Speichern unter …"), delegate { KpCompat.SaveBinary(this.FindForm(), name, pb); }) };
						int bx = r.Right - D(10);
						for(int i = ab.Length - 1; i >= 0; --i)
						{
							bx -= bs;
							DrawIconButton(g, new Rectangle(bx, y + (lineH - bs) / 2, bs, bs), ab[i]);
							bx -= D(2);
						}
						string sz = FormatSize(pb.Length);
						int sw = TextWidth(g, sz, m_fLabel);
						Rectangle rName = new Rectangle(innerX, y, Math.Max(0, bx - D(10) - sw - D(10) - innerX), lineH);
						TextRenderer.DrawText(g, name, m_fBase, rName, m_p.Text, TfLine);
						TextRenderer.DrawText(g, sz, m_fLabel, new Rectangle(bx - D(10) - sw, y, sw + D(2), lineH), m_p.Sub, TfLine);
						AddHit(rName, delegate { OpenAttachment(name, pb); }, L.F("Öffnen: {0}", name));
					}
					y += lineH;
				}
				y += D(6);
			}
			return y + D(2) - r.Top;
		}

		private void OpenAttachment(string name, ProtectedBinary pb)
		{
			if(!KpCompat.OpenBinary(m_host, name, pb)) KpCompat.SaveBinary(this.FindForm(), name, pb);
		}

		private static string FormatSize(uint bytes)
		{
			if(bytes < 1024) return bytes.ToString(CultureInfo.CurrentCulture) + " B";
			double kb = bytes / 1024.0;
			if(kb < 1024) return kb.ToString("0.#", CultureInfo.CurrentCulture) + " KB";
			return (kb / 1024.0).ToString("0.#", CultureInfo.CurrentCulture) + " MB";
		}

		private void DrawGlyph(Graphics g, Glyph gl, Rectangle r, Color c, Color bg)
		{
			SmoothingMode old = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			float u = 13f * m_s;
			float cx = r.Left + r.Width / 2f;
			float cy = r.Top + r.Height / 2f;
			float h = u / 2f;

			using(Pen p = new Pen(c, Math.Max(1f, 1.3f * m_s)))
			{
				p.LineJoin = LineJoin.Round;
				p.StartCap = LineCap.Round;
				p.EndCap = LineCap.Round;

				if(gl == Glyph.Copy)
				{
					float s = u * 0.64f;
					RectangleF back = new RectangleF(cx - h, cy - h, s, s);
					RectangleF front = new RectangleF(cx - h + u * 0.36f, cy - h + u * 0.36f, s, s);
					g.DrawRectangle(p, back.X, back.Y, back.Width, back.Height);
					using(SolidBrush b = new SolidBrush(bg)) { g.FillRectangle(b, front); }
					g.DrawRectangle(p, front.X, front.Y, front.Width, front.Height);
				}
				else if((gl == Glyph.Eye) || (gl == Glyph.EyeOff))
				{
					g.DrawEllipse(p, cx - h, cy - u * 0.32f, u, u * 0.64f);
					float rr = u * 0.15f;
					using(SolidBrush b = new SolidBrush(c)) { g.FillEllipse(b, cx - rr, cy - rr, 2f * rr, 2f * rr); }
					if(gl == Glyph.EyeOff) g.DrawLine(p, cx - h, cy + h, cx + h, cy - h);
				}
				else if(gl == Glyph.Check)
				{
					g.DrawLines(p, new PointF[] { new PointF(cx - h * 0.8f, cy + h * 0.05f),
						new PointF(cx - h * 0.2f, cy + h * 0.65f), new PointF(cx + h * 0.85f, cy - h * 0.6f) });
				}
				else if(gl == Glyph.Save)
				{
					// Pfeil nach unten in eine Ablage
					g.DrawLine(p, cx, cy - h, cx, cy + h * 0.35f);
					g.DrawLines(p, new PointF[] { new PointF(cx - h * 0.5f, cy - h * 0.1f), new PointF(cx, cy + h * 0.4f),
						new PointF(cx + h * 0.5f, cy - h * 0.1f) });
					g.DrawLines(p, new PointF[] { new PointF(cx - h, cy + h * 0.35f), new PointF(cx - h, cy + h),
						new PointF(cx + h, cy + h), new PointF(cx + h, cy + h * 0.35f) });
				}
				else if(gl == Glyph.Open)
				{
					g.DrawLines(p, new PointF[] { new PointF(cx - h * 0.1f, cy - h), new PointF(cx - h, cy - h),
						new PointF(cx - h, cy + h), new PointF(cx + h, cy + h), new PointF(cx + h, cy + h * 0.1f) });
					g.DrawLine(p, cx - h * 0.05f, cy + h * 0.05f, cx + h, cy - h);
					g.DrawLines(p, new PointF[] { new PointF(cx + h * 0.25f, cy - h), new PointF(cx + h, cy - h),
						new PointF(cx + h, cy - h * 0.25f) });
				}
			}
			g.SmoothingMode = old;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if(m_splitDrag >= 0)
			{
				DragSplitter(e.X);
				this.Cursor = Cursors.VSplit;
				return;
			}
			int sp = SplitterAt(e.Location);
			if(sp != m_splitHot)
			{
				m_splitHot = sp;
				Invalidate();
			}
			if(sp >= 0)
			{
				this.Cursor = Cursors.VSplit;
				if(m_tipText != L.T("Ziehen: Breite ändern · Doppelklick: gleich breit"))
				{
					m_tipText = L.T("Ziehen: Breite ändern · Doppelklick: gleich breit");
					m_tip.SetToolTip(this, m_tipText);
				}
				return;
			}
			Hit h = HitAt(e.Location);
			bool clickable = ((h != null) && (h.Click != null));
			Rectangle hot = (clickable ? h.Rect : Rectangle.Empty);
			if(hot != m_hotRect)
			{
				Rectangle old = m_hotRect;
				m_hotRect = hot;
				if(!old.IsEmpty) Invalidate(old);
				if(!hot.IsEmpty) Invalidate(hot);
			}
			this.Cursor = (clickable ? Cursors.Hand : Cursors.Default);

			string tip = (((h != null) && (h.Tip != null)) ? h.Tip : string.Empty);
			if(tip != m_tipText)
			{
				m_tipText = tip;
				m_tip.SetToolTip(this, tip);
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			if((m_splitHot >= 0) && (m_splitDrag < 0))
			{
				m_splitHot = -1;
				Invalidate();
			}
			if(!m_hotRect.IsEmpty)
			{
				Rectangle old = m_hotRect;
				m_hotRect = Rectangle.Empty;
				Invalidate(old);
			}
		}

		protected override void OnMouseClick(MouseEventArgs e)
		{
			base.OnMouseClick(e);
			if(e.Button != MouseButtons.Left) return;
			Hit h = HitAt(e.Location);
			if((h == null) || (h.Click == null)) return;
			try
			{
				h.Click();
				if(h.IsCopy)
				{
					m_clearSeconds = KpCompat.ClipboardClearSeconds();
					m_copiedRect = h.Rect;
					m_copiedUntil = DateTime.Now.AddSeconds(2.5);
					UpdateTimer();
					Invalidate();
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Bereich für den Screenshot der Detailansicht: Kopfzeile und erste Karte.</summary>
		public Rectangle ScreenshotArea
		{
			get
			{
				if((m_entry == null) || m_shotHead.IsEmpty || m_shotCard.IsEmpty) return Rectangle.Empty;
				Rectangle r = Rectangle.FromLTRB(m_shotCard.Left, m_shotHead.Top, m_shotCard.Right, m_shotCard.Bottom);
				r.Inflate(D(8), D(8));
				r.Intersect(this.ClientRectangle);
				return r;
			}
		}

		/// <summary>Für Screenshots: „Kopiert“-Hinweis am Passwort zeigen, ohne etwas in die
		/// Zwischenablage zu kopieren.</summary>
		public void ShowCopiedDemo(int clearSeconds)
		{
			int n = 0;
			foreach(Hit h in m_hits)
			{
				if(!h.IsCopy) continue;
				if(++n < 2) continue; // 1. Kopieren-Knopf = Benutzername, 2. = Passwort
				m_clearSeconds = clearSeconds;
				m_copiedRect = h.Rect;
				m_copiedUntil = DateTime.Now.AddSeconds(4);
				UpdateTimer();
				Invalidate();
				return;
			}
		}

		/// <summary>Beim Klick in die Notizen nicht automatisch scrollen (sonst springt die Ansicht).</summary>
		protected override Point ScrollToControl(Control activeControl)
		{
			return this.DisplayRectangle.Location;
		}

		protected override void OnScroll(ScrollEventArgs se)
		{
			base.OnScroll(se);
			Invalidate();
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			base.OnMouseWheel(e);
			Invalidate();
		}
	}

	internal sealed class TagEventArgs : EventArgs
	{
		public readonly string Tag;

		public TagEventArgs(string tag)
		{
			this.Tag = tag;
		}
	}
}
