// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Tooltip im Fluent-Design (globale Vorgabe „Weitere Fenster“): Kartenfarbe, 1 px Rahmen,
	/// Segoe UI 9 pt, Innenabstand 8 × 5 an allen Seiten, Umbruch bei 400 (× Skalierung), 15 s sichtbar.
	/// Folgt hell/dunkel. Über AttachTo bekommen auch die Tooltips der KeePass-Symbolleisten und
	/// -Menüs dieses Aussehen.</summary>
	internal sealed class ModernToolTip : ToolTip
	{
		// NoPadding: Text ohne den Zuschlag von Windows messen und zeichnen, sonst wird der Rand
		// rechts größer als links
		private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
			TextFormatFlags.NoPadding;

		private static readonly Font s_font = new Font("Segoe UI", 9f);
		private static readonly List<ToolTip> s_foreign = new List<ToolTip>();

		public ModernToolTip()
		{
			this.OwnerDraw = true;
			this.AutoPopDelay = 15000;
			this.Popup += OnPopup;
			this.Draw += OnDraw;
		}

		/// <summary>Stellt den (internen) Tooltip einer Symbolleiste oder eines Menüs von KeePass auf
		/// dieses Aussehen um. Mehrfache Aufrufe sind unschädlich.</summary>
		public static void AttachTo(ToolStrip ts)
		{
			try
			{
				if((ts == null) || ts.IsDisposed) return;
				PropertyInfo pi = typeof(ToolStrip).GetProperty("ToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
				if(pi == null) return;
				ToolTip tt = (pi.GetValue(ts, null) as ToolTip);
				if((tt == null) || (tt is ModernToolTip) || s_foreign.Contains(tt)) return;
				tt.OwnerDraw = true;
				tt.Popup += OnPopup;
				tt.Draw += OnDraw;
				tt.Disposed += OnForeignDisposed;
				s_foreign.Add(tt);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private static void OnForeignDisposed(object sender, EventArgs e)
		{
			s_foreign.Remove(sender as ToolTip);
		}

		/// <summary>Beim Beenden des Plugins: Tooltips von KeePass wieder im Windows-Stil.</summary>
		public static void DetachAll()
		{
			foreach(ToolTip tt in s_foreign.ToArray())
			{
				try
				{
					tt.Popup -= OnPopup;
					tt.Draw -= OnDraw;
					tt.Disposed -= OnForeignDisposed;
					tt.OwnerDraw = false;
				}
				catch(Exception) { }
			}
			s_foreign.Clear();
		}

		private static float Scale(Graphics g)
		{
			return g.DpiX / 96f;
		}

		private static Palette Pal()
		{
			Palette p = FluentThemeExt.CurrentPalette;
			return (p ?? Palette.Create(false, Palette.DefaultAccent));
		}

		private static void OnPopup(object sender, PopupEventArgs e)
		{
			try
			{
				ToolTip tt = (sender as ToolTip);
				string text = (((tt != null) && (e.AssociatedControl != null)) ? tt.GetToolTip(e.AssociatedControl) : null);
				if(string.IsNullOrEmpty(text)) return;
				// Mit Graphics messen – ohne rechnet Windows trotz NoPadding einen Zuschlag ein
				using(Graphics g = Graphics.FromHwnd(IntPtr.Zero))
				{
					float k = Scale(g);
					int padX = (int)Math.Round(8 * k), padY = (int)Math.Round(5 * k);
					Size sz = TextRenderer.MeasureText(g, text, s_font, new Size((int)Math.Round(400 * k), int.MaxValue), Flags);
					// + 2 für den Rahmen, + 1 px Breite als Reserve
					e.ToolTipSize = new Size(sz.Width + 2 * padX + 3, sz.Height + 2 * padY + 2);
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private static void OnDraw(object sender, DrawToolTipEventArgs e)
		{
			try
			{
				Palette p = Pal();
				Graphics g = e.Graphics;
				Rectangle r = e.Bounds;
				using(SolidBrush b = new SolidBrush(p.Card)) { g.FillRectangle(b, r); }
				using(Pen pen = new Pen(p.Border)) { g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1); }
				float k = Scale(g);
				int padX = (int)Math.Round(8 * k), padY = (int)Math.Round(5 * k);
				// Konnte die Größe nicht vorgegeben werden (Text beim Öffnen unbekannt), mit kleinem Rand zeichnen
				Size need = TextRenderer.MeasureText(g, e.ToolTipText, s_font, new Size((int)Math.Round(400 * k), int.MaxValue), Flags);
				if(r.Width < need.Width + 2 * padX + 2) padX = Math.Max(2, (r.Width - 2 - need.Width) / 2);
				if(r.Height < need.Height + 2 * padY + 2) padY = Math.Max(1, (r.Height - 2 - need.Height) / 2);
				Rectangle tr = new Rectangle(r.X + 1 + padX, r.Y + 1 + padY, r.Width - 2 - 2 * padX, r.Height - 2 - 2 * padY);
				TextRenderer.DrawText(g, e.ToolTipText, s_font, tr, p.Text, Flags);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}
	}
}
