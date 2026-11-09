// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Meldungen und Rückfragen des Plugins im Fluent-Design statt im Windows-Meldungsfenster
	/// (globale Vorgabe „Dialogfenster“). Gleiche Show-Überladungen wie System.Windows.Forms.MessageBox,
	/// damit vorhandene Aufrufe im Namensraum FluentTheme automatisch dieses Fenster verwenden.</summary>
	internal static class MessageBox
	{
		public static DialogResult Show(string text)
		{
			return Show(null, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		}

		public static DialogResult Show(string text, string caption)
		{
			return Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		}

		public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
		{
			return Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		}

		public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
		{
			return Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
		}

		public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
			MessageBoxDefaultButton def)
		{
			return Show(null, text, caption, buttons, icon, def);
		}

		public static DialogResult Show(IWin32Window owner, string text)
		{
			return Show(owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		}

		public static DialogResult Show(IWin32Window owner, string text, string caption)
		{
			return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		}

		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons)
		{
			return Show(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
		}

		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
			MessageBoxIcon icon)
		{
			return Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
		}

		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
			MessageBoxIcon icon, MessageBoxDefaultButton def)
		{
			Palette p = FluentThemeExt.CurrentPalette;
			if(p != null)
			{
				try
				{
					if(owner == null)
					{
						Form act = Form.ActiveForm; // nur Fenster im selben Thread
						if((act != null) && !act.InvokeRequired && act.Visible) owner = act;
					}
					using(MsgDialog d = new MsgDialog(p, text, caption, buttons, icon, def))
					{
						return ((owner != null) ? d.ShowDialog(owner) : d.ShowDialog());
					}
				}
				catch(Exception ex) { FluentThemeExt.Report(ex); }
			}
			// Rückfall: Design noch nicht geladen oder Fehler im eigenen Fenster
			return System.Windows.Forms.MessageBox.Show(owner, text, caption, buttons, icon, def);
		}
	}

	/// <summary>Das Meldungsfenster selbst: Symbol im farbigen Kreis, Text, Knöpfe rechts unten.</summary>
	internal sealed class MsgDialog : Form
	{
		private readonly Palette m_p;
		private readonly float k;
		private readonly MessageBoxIcon m_icon;
		private readonly DialogResult m_escResult;
		private Rectangle m_glyphRect;

		public MsgDialog(Palette p, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
			MessageBoxDefaultButton def)
		{
			m_p = p;
			m_icon = icon;
			this.Text = (string.IsNullOrEmpty(caption) ? "Fluent Theme" : caption);
			this.Name = "FluentThemeMessage";
			try
			{
				Icon ico = AppIcon.GetIcon(AppIcon.SmallSize);
				if(ico != null) this.Icon = ico;
			}
			catch(Exception) { }
			// Fensterrahmen wie bei allen Dialogen: normaler Rahmen, feste Größe (siehe WndProc)
			this.FormBorderStyle = FormBorderStyle.Sizable;
			this.SizeGripStyle = SizeGripStyle.Hide;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowInTaskbar = false;
			this.StartPosition = FormStartPosition.CenterParent;
			this.AutoScaleMode = AutoScaleMode.None;
			this.BackColor = p.Bg;
			this.ForeColor = p.Text;
			this.Font = new Font("Segoe UI", 9.5f);
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
				ControlStyles.ResizeRedraw, true);
			using(Graphics g = CreateGraphics()) { k = g.DpiX / 96f; }

			int pad = S(24);
			bool hasGlyph = (GlyphColor() != Color.Empty);
			int glyph = (hasGlyph ? S(32) : 0);
			int textX = pad + (hasGlyph ? glyph + S(16) : 0);

			// Breite nach Textlänge: mindestens 420, höchstens 600 (× Skalierung)
			string msg = (text ?? string.Empty);
			const TextFormatFlags tf = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
				TextFormatFlags.TextBoxControl;
			int oneLine = TextRenderer.MeasureText(msg, this.Font, new Size(int.MaxValue, int.MaxValue),
				TextFormatFlags.NoPrefix).Width;
			int W = Math.Max(S(420), Math.Min(S(600), textX + oneLine + pad + S(4)));
			int textW = W - textX - pad;
			int textH = TextRenderer.MeasureText(msg, this.Font, new Size(textW, int.MaxValue), tf).Height;

			Label lbl = new Label();
			lbl.Text = msg;
			lbl.UseMnemonic = false;
			lbl.AutoSize = false;
			lbl.ForeColor = p.Text;
			lbl.BackColor = p.Bg;
			lbl.Font = this.Font;
			lbl.Padding = Padding.Empty;
			int textY = pad;
			if(hasGlyph && (textH < glyph)) textY = pad + (glyph - textH) / 2; // eine Zeile: mittig zum Symbol
			lbl.Bounds = new Rectangle(textX, textY, textW + S(2), textH + S(2));
			this.Controls.Add(lbl);
			if(hasGlyph) m_glyphRect = new Rectangle(pad, pad, glyph, glyph);

			// Knöpfe in Windows-Reihenfolge, rechtsbündig
			List<KeyValuePair<string, DialogResult>> list = ButtonList(buttons);
			int defIndex = ((def == MessageBoxDefaultButton.Button3) ? 2 :
				((def == MessageBoxDefaultButton.Button2) ? 1 : 0));
			if(defIndex >= list.Count) defIndex = 0;
			m_escResult = EscResult(buttons);

			int y = Math.Max(textY + textH, pad + glyph) + S(24);
			int gap = S(8);
			List<AboutDialog.InfoButton> btns = new List<AboutDialog.InfoButton>();
			int total = 0;
			for(int i = 0; i < list.Count; ++i)
			{
				AboutDialog.InfoButton b = new AboutDialog.InfoButton(list[i].Key, null, (i == defIndex), p, k);
				b.Width = Math.Max(b.Width, S(110));
				b.DialogResult = list[i].Value;
				btns.Add(b);
				total += b.Width + ((i > 0) ? gap : 0);
			}
			int x = W - pad - total;
			foreach(AboutDialog.InfoButton b in btns)
			{
				b.Location = new Point(x, y);
				this.Controls.Add(b);
				x += b.Width + gap;
			}
			AboutDialog.InfoButton defBtn = btns[defIndex];
			this.AcceptButton = defBtn;
			foreach(AboutDialog.InfoButton b in btns)
			{
				if(b.DialogResult == m_escResult) { this.CancelButton = b; break; }
			}
			this.ClientSize = new Size(W, y + defBtn.Height + pad);
			this.Shown += delegate { defBtn.Focus(); };
		}

		private int S(float v)
		{
			return (int)Math.Round(v * k);
		}

		private static List<KeyValuePair<string, DialogResult>> ButtonList(MessageBoxButtons buttons)
		{
			List<KeyValuePair<string, DialogResult>> l = new List<KeyValuePair<string, DialogResult>>();
			switch(buttons)
			{
				case MessageBoxButtons.OKCancel:
					l.Add(Btn("OK", DialogResult.OK));
					l.Add(Btn("Abbrechen", DialogResult.Cancel));
					break;
				case MessageBoxButtons.AbortRetryIgnore:
					l.Add(Btn("Abbrechen", DialogResult.Abort));
					l.Add(Btn("Wiederholen", DialogResult.Retry));
					l.Add(Btn("Ignorieren", DialogResult.Ignore));
					break;
				case MessageBoxButtons.YesNoCancel:
					l.Add(Btn("Ja", DialogResult.Yes));
					l.Add(Btn("Nein", DialogResult.No));
					l.Add(Btn("Abbrechen", DialogResult.Cancel));
					break;
				case MessageBoxButtons.YesNo:
					l.Add(Btn("Ja", DialogResult.Yes));
					l.Add(Btn("Nein", DialogResult.No));
					break;
				case MessageBoxButtons.RetryCancel:
					l.Add(Btn("Wiederholen", DialogResult.Retry));
					l.Add(Btn("Abbrechen", DialogResult.Cancel));
					break;
				default:
					l.Add(Btn("OK", DialogResult.OK));
					break;
			}
			return l;
		}

		private static KeyValuePair<string, DialogResult> Btn(string de, DialogResult r)
		{
			return new KeyValuePair<string, DialogResult>(L.T(de), r);
		}

		/// <summary>Esc bzw. X: bei Ja/Nein wie „Nein“, bei OK wie „OK“, sonst wie „Abbrechen“.</summary>
		private static DialogResult EscResult(MessageBoxButtons buttons)
		{
			switch(buttons)
			{
				case MessageBoxButtons.OK: return DialogResult.OK;
				case MessageBoxButtons.YesNo: return DialogResult.No;
				case MessageBoxButtons.AbortRetryIgnore: return DialogResult.Abort;
				default: return DialogResult.Cancel;
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			// Schließen über X: Ergebnis wie Esc
			if(this.DialogResult == DialogResult.None) this.DialogResult = m_escResult;
			base.OnFormClosing(e);
		}

		/// <summary>Farbe des Kreises; Color.Empty = kein Symbol.</summary>
		private Color GlyphColor()
		{
			switch(m_icon)
			{
				case MessageBoxIcon.Question: // = Frage
				case MessageBoxIcon.Information: // = Asterisk
					return m_p.Accent;
				case MessageBoxIcon.Warning: // = Exclamation
					return Color.FromArgb(0xE8, 0x6A, 0x10);
				case MessageBoxIcon.Error: // = Hand, Stop
					return Color.FromArgb(0xC4, 0x2B, 0x1C);
				default:
					return Color.Empty;
			}
		}

		private string GlyphChar()
		{
			switch(m_icon)
			{
				case MessageBoxIcon.Question: return "?";
				case MessageBoxIcon.Information: return "i";
				case MessageBoxIcon.Warning: return "!";
				default: return "×";
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Color c = GlyphColor();
			if((c == Color.Empty) || m_glyphRect.IsEmpty) return;
			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			using(SolidBrush b = new SolidBrush(c)) { g.FillEllipse(b, m_glyphRect); }
			// Zeichen nach seinen tatsächlich gezeichneten Pixeln mittig in den Kreis setzen
			string ch = GlyphChar();
			float px = m_glyphRect.Height * ((ch == "×") ? 0.72f : 0.56f);
			using(Font f = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel))
			using(GraphicsPath path = new GraphicsPath())
			{
				path.AddString(ch, f.FontFamily, (int)f.Style, f.Size, new PointF(0, 0), StringFormat.GenericTypographic);
				RectangleF bb = path.GetBounds();
				using(Matrix m = new Matrix())
				{
					m.Translate(m_glyphRect.X + (m_glyphRect.Width - bb.Width) / 2f - bb.X,
						m_glyphRect.Y + (m_glyphRect.Height - bb.Height) / 2f - bb.Y);
					path.Transform(m);
				}
				g.FillPath(Brushes.White, path);
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			NativeMethods.SetTitleBar(this.Handle, m_p.Dark, m_p.Bg, m_p.Text, m_p.Border);
		}

		// Globale Vorgabe „Fensterrahmen“: feste Größe, keine Größenänderungs-Pfeile am Rand
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
	}
}
