// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Tooltip im Fluent-Design (globale Vorgabe „Weitere Fenster“): Kartenfarbe, 1 px Rahmen,
	/// Segoe UI 9 pt, Innenabstand 10 × 6, Umbruch bei 400 (× Skalierung). Folgt hell/dunkel.</summary>
	internal sealed class ModernToolTip : ToolTip
	{
		private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
			TextFormatFlags.TextBoxControl;

		private readonly Font m_font = new Font("Segoe UI", 9f);

		public ModernToolTip()
		{
			this.OwnerDraw = true;
			this.Popup += OnPopup;
			this.Draw += OnDraw;
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing) m_font.Dispose();
			base.Dispose(disposing);
		}

		private static float Scale()
		{
			try
			{
				using(Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { return g.DpiX / 96f; }
			}
			catch(Exception) { return 1f; }
		}

		private static Palette Pal()
		{
			Palette p = FluentThemeExt.CurrentPalette;
			return (p ?? Palette.Create(false, Palette.DefaultAccent));
		}

		private void OnPopup(object sender, PopupEventArgs e)
		{
			try
			{
				string text = ((e.AssociatedControl != null) ? GetToolTip(e.AssociatedControl) : null);
				if(string.IsNullOrEmpty(text)) return;
				float k = Scale();
				int padX = (int)Math.Round(10 * k), padY = (int)Math.Round(6 * k);
				Size sz = TextRenderer.MeasureText(text, m_font, new Size((int)Math.Round(400 * k), int.MaxValue), Flags);
				e.ToolTipSize = new Size(sz.Width + 2 * padX + 2, sz.Height + 2 * padY + 2);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void OnDraw(object sender, DrawToolTipEventArgs e)
		{
			try
			{
				Palette p = Pal();
				Graphics g = e.Graphics;
				Rectangle r = e.Bounds;
				using(SolidBrush b = new SolidBrush(p.Card)) { g.FillRectangle(b, r); }
				using(Pen pen = new Pen(p.Border)) { g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1); }
				float k = Scale();
				int padX = (int)Math.Round(10 * k), padY = (int)Math.Round(6 * k);
				Rectangle tr = new Rectangle(r.X + padX + 1, r.Y + padY + 1, r.Width - 2 * padX - 2, r.Height - 2 * padY - 2);
				TextRenderer.DrawText(g, e.ToolTipText, m_font, tr, p.Text, Flags);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}
	}
}
