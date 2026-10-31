using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

using KeePass.Forms;

namespace FluentTheme
{
	/// <summary>Ersetzt die farbigen Verlaufs-Banner oben in den KeePass-Dialogen (und in Dialogen
	/// anderer Plugins wie KeeOtp2) durch einen schlichten Windows-11-Kopf: Fensterfarbe, Symbol
	/// in einer Kachel, Titel und Untertitel. Nutzt dafür BannerFactory.CustomGenerator von KeePass.</summary>
	internal static class BannerStyler
	{
		private static Palette s_p;
		private static bool s_installed;
		private static object s_prev;
		private static PropertyInfo s_prop;

		public static void Install(Palette p)
		{
			bool changed = ((s_p == null) || (p == null) || (s_p.Dark != p.Dark) || (s_p.Accent != p.Accent));
			s_p = p;
			try
			{
				if(!s_installed)
				{
					Type t = typeof(MainForm).Assembly.GetType("KeePass.UI.BannerFactory");
					if(t == null) return;
					s_prop = t.GetProperty("CustomGenerator", BindingFlags.Public | BindingFlags.Static);
					if((s_prop == null) || !s_prop.CanWrite) return;
					MethodInfo gen = typeof(BannerStyler).GetMethod("Generate",
						BindingFlags.NonPublic | BindingFlags.Static);
					Delegate d = Delegate.CreateDelegate(s_prop.PropertyType, gen, false);
					if(d == null) return;
					s_prev = s_prop.GetValue(null, null);
					s_prop.SetValue(null, d, null);
					s_installed = true;
					changed = true;
				}
				if(changed) ClearCache();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		public static void Uninstall()
		{
			if(!s_installed) return;
			try
			{
				s_prop.SetValue(null, s_prev, null);
				ClearCache();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			s_installed = false;
		}

		/// <summary>KeePass merkt sich erzeugte Banner – nach einem Farbwechsel neu erzeugen lassen.</summary>
		private static void ClearCache()
		{
			try
			{
				Type t = typeof(MainForm).Assembly.GetType("KeePass.UI.BannerFactory");
				if(t == null) return;
				foreach(FieldInfo fi in t.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
				{
					IDictionary dict = (fi.GetValue(null) as IDictionary);
					if(dict != null) dict.Clear();
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private static object Get(object o, string name)
		{
			Type t = o.GetType();
			PropertyInfo pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
			if(pi != null) return pi.GetValue(o, null);
			FieldInfo fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
			return ((fi != null) ? fi.GetValue(o) : null);
		}

		// Wird von KeePass über den Delegaten aufgerufen (Parameter: BfBannerInfo)
		private static Image Generate(object info)
		{
			Palette p = s_p;
			if((p == null) || (info == null)) return null;
			try
			{
				int w = Convert.ToInt32(Get(info, "Width"));
				int h = Convert.ToInt32(Get(info, "Height"));
				if((w <= 0) || (h <= 0)) return null;
				Image icon = (Get(info, "Icon") as Image);
				string title = (Get(info, "TitleText") as string) ?? string.Empty;
				string text = (Get(info, "InfoText") as string) ?? string.Empty;
				return Draw(p, w, h, icon, title, text);
			}
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				return null;
			}
		}

		private static Image Draw(Palette p, int w, int h, Image icon, string title, string text)
		{
			Bitmap bmp = new Bitmap(w, h);
			using(Graphics g = Graphics.FromImage(bmp))
			{
				float k = h / 60f; // KeePass-Banner sind 60 px hoch (bei 100 %)
				g.Clear(p.Card);
				using(Pen pen = new Pen(p.Border)) { g.DrawLine(pen, 0, h - 1, w, h - 1); }

				int pad = (int)Math.Round(20 * k);
				int tile = (int)Math.Round(40 * k);
				int x = pad;
				if(icon != null)
				{
					Rectangle rt = new Rectangle(pad, (h - tile) / 2, tile, tile);
					int rad = (int)Math.Round(8 * k);
					Gfx.Fill(g, rt, rad, p.Bg);
					Gfx.Stroke(g, rt, rad, p.Border);
					int isz = (int)Math.Round(26 * k);
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					g.PixelOffsetMode = PixelOffsetMode.HighQuality;
					g.DrawImage(icon, new Rectangle(rt.Left + (tile - isz) / 2, rt.Top + (tile - isz) / 2, isz, isz));
					x = rt.Right + (int)Math.Round(14 * k);
				}

				int tw = Math.Max(10, w - x - pad);
				using(Font ft = MakeFont("Segoe UI Semibold", 15f * k, FontStyle.Bold))
				using(Font fi = MakeFont("Segoe UI", 11f * k, FontStyle.Regular))
				{
					const TextFormatFlags tf = TextFormatFlags.Left | TextFormatFlags.SingleLine |
						TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
					int th = ft.Height;
					int ih = (string.IsNullOrEmpty(text) ? 0 : fi.Height);
					int gap = ((ih > 0) ? (int)Math.Round(2 * k) : 0);
					int y = (h - th - gap - ih) / 2;
					TextRenderer.DrawText(g, title, ft, new Rectangle(x, y, tw, th), p.Text, p.Card, tf);
					if(ih > 0)
						TextRenderer.DrawText(g, text, fi, new Rectangle(x, y + th + gap, tw, ih), p.Sub, p.Card, tf);
				}
			}
			return bmp;
		}

		private static Font MakeFont(string family, float px, FontStyle fallback)
		{
			try { return new Font(family, px, FontStyle.Regular, GraphicsUnit.Pixel); }
			catch(ArgumentException) { return new Font(SystemFonts.MessageBoxFont.FontFamily, px, fallback, GraphicsUnit.Pixel); }
		}
	}
}
