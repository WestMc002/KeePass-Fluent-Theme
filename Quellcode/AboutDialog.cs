// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>„Info über Fluent Theme“: einheitliches Info-Fenster wie im Monitor Dimmer
	/// (großes GS-Logo, Name, Version, Karte mit Eckdaten, Knöpfe, Copyright).
	/// Öffnet sich beim Klick auf die Versionsnummer unten in der Statusleiste.</summary>
	internal sealed class AboutDialog : Form
	{
		private const string AppName = "Fluent Theme";
		private const string Author = "Günther Schoder";
		private const string WebsiteUrl = "https://www.westmc002.de";

		private static AboutDialog s_open;

		private readonly Palette m_p;
		private readonly float k;

		public static string Version
		{
			get { return AppInfo.Version; } // von Erstellen.bat in VersionInfo.cs erzeugt
		}

		public static string ShortVersion
		{
			get
			{
				Version v = typeof(AboutDialog).Assembly.GetName().Version;
				return v.Major.ToString() + "." + v.Minor.ToString();
			}
		}

		public static void ShowFor(Form owner, Palette p, string scheme)
		{
			if(s_open != null) { s_open.Activate(); return; }
			if(p == null) return;
			try
			{
				using(AboutDialog d = new AboutDialog(owner, p, scheme))
				{
					s_open = d;
					try
					{
						if((owner != null) && owner.Visible) d.ShowDialog(owner);
						else d.ShowDialog();
					}
					finally { s_open = null; }
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private AboutDialog(Form owner, Palette p, string scheme)
		{
			m_p = p;
			this.Text = L.F("Info über {0}", AppName);
			this.Name = "FluentThemeAbout";
			// Eigenes Icon des Plugins in der Titelleiste (sonst das von KeePass)
			try
			{
				Icon ico = AppIcon.GetIcon(AppIcon.SmallSize);
				if(ico != null) this.Icon = ico;
				else if((owner != null) && (owner.Icon != null)) this.Icon = owner.Icon;
			}
			catch(Exception) { }
			// Normaler Rahmen wie bei den Hauptfenstern, Größe bleibt fest (siehe WndProc)
			this.FormBorderStyle = FormBorderStyle.Sizable;
			this.SizeGripStyle = SizeGripStyle.Hide;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowInTaskbar = false;
			this.StartPosition = (((owner != null) && owner.Visible) ? FormStartPosition.CenterParent :
				FormStartPosition.CenterScreen);
			this.AutoScaleMode = AutoScaleMode.None;
			this.BackColor = p.Bg;
			this.ForeColor = p.Text;
			this.Font = new Font("Segoe UI", 9.5f);
			using(Graphics g = CreateGraphics()) { k = g.DpiX / 96f; }
			int W = S(480);

			int y = S(24);
			PictureBox logo = new PictureBox();
			logo.Image = FooterBadge.Logo;
			logo.SizeMode = PictureBoxSizeMode.Zoom;
			logo.Location = new Point(0, y);
			logo.Size = new Size(W, S(130));
			logo.BackColor = p.Bg;
			this.Controls.Add(logo);
			y += S(146);

			this.Controls.Add(MakeLabel(AppName, new Font("Segoe UI Semibold", 15f), p.Text,
				new Rectangle(0, y, W, S(36))));
			y += S(38);
			this.Controls.Add(MakeLabel(L.F("Version {0}", Version), new Font("Segoe UI", 10.5f), p.Link,
				new Rectangle(0, y, W, S(26))));
			y += S(40);

			// ---------- Karte mit Eckdaten ----------
			InfoCard card = new InfoCard(p, k);
			card.Location = new Point(S(24), y);
			card.Size = new Size(W - S(48), S(140));
			card.Padding = new Padding(S(24), S(18), S(24), S(18));
			TableLayoutPanel table = new TableLayoutPanel();
			table.Dock = DockStyle.Fill;
			table.ColumnCount = 2;
			table.BackColor = p.Card;
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(118)));
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			ToolTip tip = new ModernToolTip();
			this.Disposed += delegate { tip.Dispose(); };

			string dll = string.Empty;
			string built = "-";
			try
			{
				dll = typeof(AboutDialog).Assembly.Location;
				built = File.GetLastWriteTime(dll).ToString(L.DateFormat);
			}
			catch(Exception) { }
			string dir = string.Empty;
			try { dir = Path.GetDirectoryName(dll); }
			catch(Exception) { }

			AddRow(table, tip, L.T("Entwickler"), Author);
			AddRow(table, tip, L.T("Erstellt"), built);
			AddRow(table, tip, L.T("Farbschema"), scheme);
			AddRow(table, tip, L.T("Plugin"), (string.IsNullOrEmpty(dll) ? "-" : dll));
			card.Controls.Add(table);
			this.Controls.Add(card);
			y += card.Height + S(24);

			// ---------- Knöpfe ----------
			InfoButton web = new InfoButton(L.T("Webseite"), "globe", false, p, k);
			web.Click += delegate { Open(WebsiteUrl); };
			InfoButton folder = new InfoButton(L.T("Plugin-Ordner"), "open", false, p, k);
			folder.Enabled = !string.IsNullOrEmpty(dir);
			folder.Click += delegate { Open(dir); };
			InfoButton close = new InfoButton(L.T("Schließen"), null, true, p, k);
			close.DialogResult = DialogResult.Cancel;
			int gap = S(14);
			int total = web.Width + folder.Width + close.Width + 2 * gap;
			int bx = (W - total) / 2;
			web.Location = new Point(bx, y);
			folder.Location = new Point(web.Right + gap, y);
			close.Location = new Point(folder.Right + gap, y);
			this.Controls.Add(web);
			this.Controls.Add(folder);
			this.Controls.Add(close);
			this.CancelButton = close;
			this.AcceptButton = close;
			y += close.Height + S(22);

			this.Controls.Add(MakeLabel("© " + DateTime.Now.Year.ToString() + " " + Author, this.Font, p.Sub,
				new Rectangle(0, y, W, S(24))));
			y += S(24) + S(20);

			this.ClientSize = new Size(W, y);
			this.Shown += delegate { close.Focus(); };
		}

		private int S(float v)
		{
			return (int)Math.Round(v * k);
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			NativeMethods.SetTitleBar(this.Handle, m_p.Dark, m_p.Bg, m_p.Text, m_p.Border);
		}

		// Globale Vorgabe „Fensterrahmen“: normaler Rahmen (Sizable), feste Größe über
		// MinimumSize/MaximumSize und keine Größenänderungs-Pfeile am Rand
		private const int WM_NCHITTEST = 0x0084, HTCLIENT = 1, HTLEFT = 10, HTBOTTOMRIGHT = 17;

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			this.MinimumSize = this.Size;
			this.MaximumSize = this.Size;
		}

		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);
			if(m.Msg == WM_NCHITTEST)
			{
				int hit = m.Result.ToInt32();
				if((hit >= HTLEFT) && (hit <= HTBOTTOMRIGHT)) m.Result = (IntPtr)HTCLIENT;
			}
		}

		private Label MakeLabel(string text, Font f, Color c, Rectangle r)
		{
			Label l = new Label();
			l.Text = text;
			l.Font = f;
			l.ForeColor = c;
			l.BackColor = m_p.Bg;
			l.UseMnemonic = false;
			l.TextAlign = ContentAlignment.MiddleCenter;
			l.Bounds = r;
			return l;
		}

		private void AddRow(TableLayoutPanel t, ToolTip tip, string name, string value)
		{
			int row = t.RowCount;
			t.RowCount = row + 1;
			t.RowStyles.Add(new RowStyle(SizeType.Absolute, S(26)));

			Label n = new Label();
			n.Text = name;
			n.ForeColor = m_p.Sub;
			n.BackColor = m_p.Card;
			n.Dock = DockStyle.Fill;
			n.TextAlign = ContentAlignment.MiddleLeft;
			n.UseMnemonic = false;
			t.Controls.Add(n, 0, row);

			Label v = new Label();
			v.Text = value;
			v.ForeColor = m_p.Text;
			v.BackColor = m_p.Card;
			v.Dock = DockStyle.Fill;
			v.TextAlign = ContentAlignment.MiddleLeft;
			v.AutoEllipsis = true;
			v.UseMnemonic = false;
			tip.SetToolTip(v, value);
			t.Controls.Add(v, 1, row);
		}

		private static void Open(string target)
		{
			if(string.IsNullOrEmpty(target)) return;
			try { Process.Start(target); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Weiße (bzw. dunkle) Karte mit abgerundeten Ecken und feinem Rahmen.</summary>
		private sealed class InfoCard : Panel
		{
			private readonly Palette m_p;
			private readonly float k;

			public InfoCard(Palette p, float scale)
			{
				m_p = p;
				k = scale;
				this.BackColor = p.Card;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
					ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				Graphics g = e.Graphics;
				g.Clear(m_p.Bg);
				Rectangle r = new Rectangle(0, 0, this.Width, this.Height);
				int rad = (int)Math.Round(8 * k);
				Gfx.Fill(g, r, rad, m_p.Card);
				Gfx.Stroke(g, r, rad, m_p.Border);
			}
		}

		/// <summary>Runder Knopf mit Symbol. Primary = Akzentfarbe ausgefüllt, sonst Karte mit Rahmen.
		/// Wird auch vom Meldungsfenster (MsgDialog) verwendet.</summary>
		internal sealed class InfoButton : Button
		{
			private readonly Palette m_p;
			private readonly float k;
			private readonly bool m_primary;
			private readonly string m_icon;
			private bool m_hover;
			private bool m_down;

			public InfoButton(string text, string icon, bool primary, Palette p, float scale)
			{
				m_p = p;
				k = scale;
				m_primary = primary;
				m_icon = icon;
				this.Text = text;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
					ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
				this.FlatStyle = FlatStyle.Flat;
				this.FlatAppearance.BorderSize = 0;
				this.Font = new Font("Segoe UI Semibold", 9.5f);
				this.Cursor = Cursors.Hand;
				this.UseMnemonic = false;
				int tw = TextRenderer.MeasureText(text, this.Font).Width;
				int iw = ((m_icon == null) ? 0 : (int)Math.Round(26 * k));
				this.Size = new Size(tw + iw + (int)Math.Round(36 * k), (int)Math.Round(40 * k));
			}

			private Color PrimaryFill(int state)
			{
				// Wie Windows 11: hell = dunklere Akzentstufe, dunkel = hellere Akzentstufe
				Color a = m_p.Accent;
				if(m_p.Dark)
				{
					double t = ((state == 2) ? 0.30 : ((state == 1) ? 0.40 : 0.50));
					return Palette.Blend(a, Color.White, t);
				}
				double d = ((state == 2) ? 0.40 : ((state == 1) ? 0.30 : 0.20));
				return Palette.Blend(a, Color.Black, d);
			}

			protected override void OnMouseEnter(EventArgs e) { m_hover = true; Invalidate(); base.OnMouseEnter(e); }
			protected override void OnMouseLeave(EventArgs e) { m_hover = false; m_down = false; Invalidate(); base.OnMouseLeave(e); }
			protected override void OnMouseDown(MouseEventArgs e) { m_down = true; Invalidate(); base.OnMouseDown(e); }
			protected override void OnMouseUp(MouseEventArgs e) { m_down = false; Invalidate(); base.OnMouseUp(e); }
			protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
			protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
			protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

			protected override void OnPaint(PaintEventArgs e)
			{
				Graphics g = e.Graphics;
				g.Clear(m_p.Bg);
				g.SmoothingMode = SmoothingMode.AntiAlias;
				Rectangle r = new Rectangle(0, 0, this.Width, this.Height);
				int rad = (int)Math.Round(4 * k);
				int state = (m_down ? 2 : (m_hover ? 1 : 0));
				Color fill, text, border;
				if(m_primary && this.Enabled)
				{
					fill = PrimaryFill(state);
					text = ((Palette.Contrast(fill, Color.White) >= 3.0) ? Color.White : Color.FromArgb(20, 20, 20));
					border = fill;
				}
				else
				{
					fill = (!this.Enabled ? m_p.Card : ((state == 2) ? m_p.CardHover : ((state == 1) ? m_p.Hover : m_p.Card)));
					text = (this.Enabled ? m_p.Text : m_p.Sub);
					border = m_p.Border;
				}
				Gfx.Fill(g, r, rad, fill);
				Gfx.Stroke(g, r, rad, border);

				int tw = TextRenderer.MeasureText(this.Text, this.Font).Width;
				int iw = ((m_icon == null) ? 0 : (int)Math.Round(26 * k));
				int x = (this.Width - tw - iw) / 2;
				if(m_icon != null)
				{
					int gs = (int)Math.Round(16 * k);
					using(Bitmap img = IconFactory.Create(m_icon, gs, text, 1.5f * k))
					{
						g.DrawImage(img, x, (this.Height - gs) / 2, gs, gs);
					}
					x += iw;
				}
				TextRenderer.DrawText(g, this.Text, this.Font, new Rectangle(x, 0, tw + 2, this.Height), text,
					TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding |
					TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

				if(this.Focused && this.ShowFocusCues)
				{
					Rectangle fr = Rectangle.Inflate(r, -(int)Math.Round(3 * k), -(int)Math.Round(3 * k));
					Gfx.Stroke(g, fr, Math.Max(1, rad - 2), m_primary ? text : m_p.Sub);
				}
			}
		}
	}
}
