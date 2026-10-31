using System;
using System.Drawing;
using System.Globalization;

using Microsoft.Win32;

namespace FluentTheme
{
	internal enum ThemeMode
	{
		System = 0,
		Light = 1,
		Dark = 2
	}

	/// <summary>Windows-11-Standardfarben (wie Monitor Dimmer und Fensterpositionen):
	/// Fenster #F3F3F3 / #202020, Karten #FFFFFF / #2B2B2B, dazu die Windows-Akzentfarbe.</summary>
	internal sealed class Palette
	{
		public static readonly Color DefaultAccent = Color.FromArgb(0x1F, 0x6F, 0xD6);

		public bool Dark;

		public Color Bg;          // Fensterhintergrund
		public Color Card;        // Karten (Gruppen, Liste, Details), Menüs
		public Color Pane;        // = Card (Liste)
		public Color Text;
		public Color Sub;         // Sekundärtext
		public Color Border;      // Kartenrahmen
		public Color RowLine;     // Trennlinien zwischen Zeilen
		public Color Hover;       // Hover in Listen
		public Color Sel;         // Auswahl in Listen
		public Color CardHover;   // Hover von Buttons/Menüeinträgen auf Karten
		public Color BtnBg;       // Sekundär-Button
		public Color BadgeBg;
		public Color BadgeBorder;
		public Color BadgeText;

		public Color Accent;
		public Color Pill;        // Auswahlmarkierung links
		public Color Link;
		public Color Good;
		public Color Mid;
		public Color Weak;

		public static Palette Create(bool dark, Color accent)
		{
			Palette p = new Palette();
			p.Dark = dark;
			p.Accent = accent;

			if(dark)
			{
				// Windows-11-Standardwerte (dunkel)
				p.Bg = Color.FromArgb(0x20, 0x20, 0x20);
				p.Card = Color.FromArgb(0x2B, 0x2B, 0x2B);
				p.Text = Color.FromArgb(0xFA, 0xFA, 0xFA);
				p.Sub = Color.FromArgb(0xCE, 0xCE, 0xCE);
				p.Border = Color.FromArgb(0x3D, 0x3D, 0x3D);
				p.RowLine = Color.FromArgb(0x3A, 0x3A, 0x3A);
				p.BtnBg = Color.FromArgb(0x37, 0x37, 0x37);
				p.Link = EnsureContrast(Blend(accent, Color.White, 0.35), p.Card, Color.White);
				p.Pill = p.Link;
				p.Hover = Color.FromArgb(0x34, 0x34, 0x34);
				p.Sel = Blend(p.Card, accent, 0.25);
				p.CardHover = Color.FromArgb(0x3E, 0x3E, 0x3E);
				p.BadgeBg = Blend(p.Card, accent, 0.28);
				p.BadgeBorder = Blend(p.Card, accent, 0.50);
				p.BadgeText = EnsureContrast(Blend(accent, Color.White, 0.70), p.BadgeBg, Color.White);
				p.Good = Color.FromArgb(0x6C, 0xCB, 0x5F);
				p.Mid = Color.FromArgb(0xFC, 0xE1, 0x00);
				p.Weak = Color.FromArgb(0xFF, 0x99, 0xA4);
			}
			else
			{
				// Windows-11-Standardwerte (hell)
				p.Bg = Color.FromArgb(0xF3, 0xF3, 0xF3);
				p.Card = Color.FromArgb(0xFF, 0xFF, 0xFF);
				p.Text = Color.FromArgb(0x1B, 0x1B, 0x1B);
				p.Sub = Color.FromArgb(0x61, 0x61, 0x61);
				p.Border = Color.FromArgb(0xE5, 0xE5, 0xE5);
				p.RowLine = Color.FromArgb(0xEA, 0xEA, 0xEA);
				p.BtnBg = Color.FromArgb(0xFB, 0xFB, 0xFB);
				p.Link = EnsureContrast(accent, Color.White, Color.Black);
				p.Pill = EnsureContrast(accent, Color.White, Color.Black, 3.0);
				p.Hover = Color.FromArgb(0xF6, 0xF6, 0xF6);
				p.Sel = Blend(p.Card, accent, 0.12);
				p.CardHover = Color.FromArgb(0xEC, 0xEC, 0xEC);
				p.BadgeBg = Blend(p.Card, accent, 0.20);
				p.BadgeBorder = Blend(p.Card, accent, 0.38);
				p.BadgeText = EnsureContrast(Blend(accent, Color.Black, 0.45), p.BadgeBg, Color.Black);
				p.Good = Color.FromArgb(0x0F, 0x7B, 0x0F);
				p.Mid = Color.FromArgb(0x9D, 0x5D, 0x00);
				p.Weak = Color.FromArgb(0xC4, 0x2B, 0x1C);
			}

			p.Pane = p.Card;
			return p;
		}

		private static double Lum(Color c)
		{
			double r = Lin(c.R), g = Lin(c.G), b = Lin(c.B);
			return 0.2126 * r + 0.7152 * g + 0.0722 * b;
		}

		private static double Lin(int v)
		{
			double s = v / 255.0;
			return ((s <= 0.03928) ? (s / 12.92) : Math.Pow((s + 0.055) / 1.055, 2.4));
		}

		public static double Contrast(Color a, Color b)
		{
			double la = Lum(a), lb = Lum(b);
			if(la < lb) { double t = la; la = lb; lb = t; }
			return (la + 0.05) / (lb + 0.05);
		}

		/// <summary>Schiebt eine (Windows-)Akzentfarbe so lange Richtung toward, bis sie auf bg
		/// gut lesbar ist – damit auch helle Akzentfarben (Gelb, Hellgrün) als Link taugen.</summary>
		public static Color EnsureContrast(Color c, Color bg, Color toward)
		{
			return EnsureContrast(c, bg, toward, 4.5);
		}

		public static Color EnsureContrast(Color c, Color bg, Color toward, double min)
		{
			Color r = c;
			for(int i = 1; (i <= 20) && (Contrast(r, bg) < min); ++i) r = Blend(c, toward, i * 0.05);
			return r;
		}

		public static Color Blend(Color a, Color b, double t)
		{
			if(t < 0.0) t = 0.0;
			if(t > 1.0) t = 1.0;
			int r = (int)Math.Round(a.R + (b.R - a.R) * t);
			int g = (int)Math.Round(a.G + (b.G - a.G) * t);
			int bl = (int)Math.Round(a.B + (b.B - a.B) * t);
			return Color.FromArgb(r, g, bl);
		}

		public static bool SystemUsesDark()
		{
			try
			{
				using(RegistryKey k = Registry.CurrentUser.OpenSubKey(
					@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
				{
					if(k != null)
					{
						object o = k.GetValue("AppsUseLightTheme");
						if(o is int) return ((int)o == 0);
					}
				}
			}
			catch(Exception) { }
			return false;
		}

		public static Color SystemAccent()
		{
			try
			{
				using(RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
				{
					if(k != null)
					{
						object o = k.GetValue("AccentColor");
						if(o is int)
						{
							uint u = unchecked((uint)(int)o); // 0xAABBGGRR
							return Color.FromArgb((int)(u & 0xFF), (int)((u >> 8) & 0xFF),
								(int)((u >> 16) & 0xFF));
						}
					}
				}
			}
			catch(Exception) { }
			return DefaultAccent;
		}

		public static Color ParseHex(string s, Color fallback)
		{
			if(string.IsNullOrEmpty(s)) return fallback;
			s = s.Trim().TrimStart('#');
			int v;
			if((s.Length == 6) && int.TryParse(s, NumberStyles.HexNumber,
				CultureInfo.InvariantCulture, out v))
				return Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
			return fallback;
		}

		public static string ToHex(Color c)
		{
			return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
		}
	}
}