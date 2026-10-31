// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FluentTheme
{
	/// <summary>Farbige, flache Symbole im Windows-11-Stil für die Symbolleiste – angelehnt an die
	/// KeePass-Originale (gelber Ordner, blaue Diskette, goldener Schlüssel …).
	/// Gezeichnet auf einem 24er-Raster und auf die gewünschte Größe skaliert.</summary>
	internal static class IconFactory
	{
		private static readonly Color Gold = Color.FromArgb(0xF5, 0xB7, 0x1E);
		private static readonly Color GoldDark = Color.FromArgb(0xB9, 0x82, 0x0A);
		private static readonly Color Folder = Color.FromArgb(0xFF, 0xCA, 0x45);
		private static readonly Color FolderBack = Color.FromArgb(0xE9, 0xA8, 0x1F);
		private static readonly Color Blue = Color.FromArgb(0x2B, 0x7B, 0xE0);
		private static readonly Color BlueDark = Color.FromArgb(0x1A, 0x55, 0xA8);
		private static readonly Color BlueLight = Color.FromArgb(0x7F, 0xB6, 0xF5);
		private static readonly Color Green = Color.FromArgb(0x1E, 0xA4, 0x4F);
		private static readonly Color Gray = Color.FromArgb(0x8A, 0x93, 0xA3);
		private static readonly Color GrayDark = Color.FromArgb(0x55, 0x5E, 0x6E);
		private static readonly Color Paper = Color.FromArgb(0xFF, 0xFF, 0xFF);
		private static readonly Color PaperLine = Color.FromArgb(0x9A, 0xA4, 0xB4);

		/// <summary>Ordnet einem KeePass-Symbolleisten-Button anhand seines Namens ein Symbol zu.</summary>
		public static string KindFor(string name)
		{
			if(string.IsNullOrEmpty(name)) return null;
			string n = name.ToLowerInvariant();
			if(n.Contains("closetab") || n.Contains("close")) return "close";
			if(n.Contains("saveall")) return "saveall";
			if(n.Contains("save")) return "save";
			if(n.Contains("newdatabase") || n.EndsWith("new")) return "new";
			if(n.Contains("opendatabase")) return "open";
			if(n.Contains("addentry") || n.Contains("addentries")) return "addentry";
			if(n.Contains("copyusername") || n.Contains("username")) return "user";
			if(n.Contains("copypassword") || n.Contains("password")) return "key";
			if(n.Contains("copyurl")) return "link";
			if(n.Contains("openurl") || n.Contains("url")) return "globe";
			if(n.Contains("autotype")) return "keyboard";
			if(n.Contains("entryview") || n.Contains("views")) return "filter";
			if(n.Contains("find") || n.Contains("search")) return "search";
			if(n.Contains("lock")) return "lock";
			if(n.Contains("sync")) return "sync";
			if(n.Contains("open")) return "open";
			return null;
		}

		/// <param name="mono">Farbe für einfarbige Symbole (Schließen-X).</param>
		public static Bitmap Create(string kind, int size, Color mono, float stroke)
		{
			Bitmap bmp = new Bitmap(size, size);
			using(Graphics g = Graphics.FromImage(bmp))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.PixelOffsetMode = PixelOffsetMode.HighQuality;
				g.Clear(Color.Transparent);
				g.ScaleTransform(size / 24f, size / 24f);
				float w = Math.Max(1.1f, stroke * 24f / size);
				Draw(g, kind, w, mono);
			}
			return bmp;
		}

		// ------------------------------------------------------------ Hilfen

		private static GraphicsPath RoundPath(float x, float y, float w, float h, float r)
		{
			GraphicsPath gp = new GraphicsPath();
			float d = r * 2f;
			gp.AddArc(x, y, d, d, 180, 90);
			gp.AddArc(x + w - d, y, d, d, 270, 90);
			gp.AddArc(x + w - d, y + h - d, d, d, 0, 90);
			gp.AddArc(x, y + h - d, d, d, 90, 90);
			gp.CloseFigure();
			return gp;
		}

		private static Color Mix(Color a, Color b, float t)
		{
			return Color.FromArgb(a.A, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t),
				(int)(a.B + (b.B - a.B) * t));
		}

		/// <summary>Weicher Verlauf von oben (heller) nach unten (dunkler) – wie die
		/// farbigen Fluent-Symbole von Windows 11.</summary>
		private static Brush Grad(Color c, RectangleF r)
		{
			if((c == Paper) || (r.Height < 0.5f)) return new SolidBrush(c);
			return new LinearGradientBrush(new PointF(0f, r.Top - 0.01f), new PointF(0f, r.Bottom + 0.01f),
				Mix(c, Color.White, 0.25f), Mix(c, Color.Black, 0.10f));
		}

		private static void FillRound(Graphics g, Color c, float x, float y, float w, float h, float r)
		{
			using(GraphicsPath gp = RoundPath(x, y, w, h, r))
			{
				using(Brush b = Grad(c, gp.GetBounds())) { g.FillPath(b, gp); }
			}
		}

		private static void FillRoundOutline(Graphics g, Color fill, Color line, float lw, float x, float y,
			float w, float h, float r)
		{
			using(GraphicsPath gp = RoundPath(x, y, w, h, r))
			{
				using(Brush b = Grad(fill, gp.GetBounds())) { g.FillPath(b, gp); }
				using(Pen p = new Pen(line, lw)) { g.DrawPath(p, gp); }
			}
		}

		private static void FillPoly(Graphics g, Color c, params float[] xy)
		{
			PointF[] pts = new PointF[xy.Length / 2];
			for(int i = 0; i < pts.Length; ++i) pts[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
			float top = float.MaxValue, bottom = float.MinValue;
			foreach(PointF pt in pts) { top = Math.Min(top, pt.Y); bottom = Math.Max(bottom, pt.Y); }
			using(Brush b = Grad(c, new RectangleF(0f, top, 24f, bottom - top))) { g.FillPolygon(b, pts); }
		}

		private static Pen MakePen(Color c, float w)
		{
			Pen p = new Pen(c, w);
			p.StartCap = LineCap.Round;
			p.EndCap = LineCap.Round;
			p.LineJoin = LineJoin.Round;
			return p;
		}

		private static void Badge(Graphics g, Color c, float cx, float cy)
		{
			using(SolidBrush b = new SolidBrush(c)) { g.FillEllipse(b, cx - 5f, cy - 5f, 10f, 10f); }
			using(Pen p = MakePen(Color.White, 1.8f))
			{
				g.DrawLine(p, cx, cy - 2.6f, cx, cy + 2.6f);
				g.DrawLine(p, cx - 2.6f, cy, cx + 2.6f, cy);
			}
		}

		private static void Key(Graphics g, float w, float ox, float oy)
		{
			using(Pen p = MakePen(Gold, 3.2f))
			{
				g.DrawLine(p, ox + 9.5f, oy + 12.5f, ox + 19f, oy + 3f);
				g.DrawLine(p, ox + 16.5f, oy + 5.5f, ox + 19f, oy + 8f);
				g.DrawLine(p, ox + 14f, oy + 8f, ox + 15.8f, oy + 9.8f);
			}
			using(Brush b = Grad(Gold, new RectangleF(ox + 2.5f, oy + 10f, 10f, 10f))) { g.FillEllipse(b, ox + 2.5f, oy + 10f, 10f, 10f); }
			using(Pen p = new Pen(GoldDark, w * 0.7f)) { g.DrawEllipse(p, ox + 2.5f, oy + 10f, 10f, 10f); }
			using(SolidBrush b = new SolidBrush(Color.White)) { g.FillEllipse(b, ox + 5.2f, oy + 14.3f, 3f, 3f); }
		}

		private static void Disk(Graphics g, float w, float x, float y, float s)
		{
			// Körper
			using(GraphicsPath gp = new GraphicsPath())
			{
				gp.AddPolygon(new PointF[] { new PointF(x, y + 1.5f * s), new PointF(x + 1.5f * s, y),
					new PointF(x + 13f * s, y), new PointF(x + 16f * s, y + 3f * s), new PointF(x + 16f * s, y + 14.5f * s),
					new PointF(x + 14.5f * s, y + 16f * s), new PointF(x + 1.5f * s, y + 16f * s), new PointF(x, y + 14.5f * s) });
				using(Brush b = Grad(Blue, gp.GetBounds())) { g.FillPath(b, gp); }
				using(Pen p = new Pen(BlueDark, w * 0.7f)) { g.DrawPath(p, gp); }
			}
			// Etikett und Schieber
			FillRound(g, Paper, x + 3f * s, y + 8.5f * s, 10f * s, 6f * s, 1f * s);
			FillRound(g, BlueLight, x + 3.5f * s, y + 1.5f * s, 8f * s, 4.5f * s, 0.8f * s);
			using(SolidBrush b = new SolidBrush(BlueDark)) { g.FillRectangle(b, x + 9f * s, y + 2.2f * s, 1.6f * s, 3f * s); }
		}

		// ------------------------------------------------------------ Symbole

		private static void Draw(Graphics g, string kind, float w, Color mono)
		{
			switch(kind)
			{
				case "new":
				{
					using(GraphicsPath gp = new GraphicsPath())
					{
						gp.AddPolygon(new PointF[] { new PointF(5, 2.5f), new PointF(13.5f, 2.5f), new PointF(18.5f, 7.5f),
							new PointF(18.5f, 21.5f), new PointF(5, 21.5f) });
						using(SolidBrush b = new SolidBrush(Paper)) { g.FillPath(b, gp); }
						using(Pen p = new Pen(PaperLine, w * 0.8f)) { g.DrawPath(p, gp); }
					}
					FillPoly(g, Color.FromArgb(0xD5, 0xDC, 0xE6), 13.5f, 2.5f, 13.5f, 7.5f, 18.5f, 7.5f);
					Badge(g, Green, 17f, 17f);
					break;
				}
				case "open":
				{
					// Rückwand mit Reiter
					using(GraphicsPath gp = new GraphicsPath())
					{
						gp.AddLine(2.5f, 19.5f, 2.5f, 6f);
						gp.AddArc(2.5f, 4f, 3f, 3f, 180, 90);
						gp.AddLine(4f, 4f, 9f, 4f);
						gp.AddLine(9f, 4f, 11f, 6.5f);
						gp.AddLine(11f, 6.5f, 19.5f, 6.5f);
						gp.AddArc(18f, 6.5f, 3f, 3f, 270, 90);
						gp.AddLine(21f, 8f, 21f, 19.5f);
						gp.CloseFigure();
						using(Brush br = Grad(FolderBack, gp.GetBounds())) { g.FillPath(br, gp); }
					}
					// Blatt im Ordner
					FillRound(g, Paper, 5f, 8f, 13f, 9f, 1f);
					// Vordere, geöffnete Klappe
					using(GraphicsPath gp = new GraphicsPath())
					{
						gp.AddPolygon(new PointF[] { new PointF(2.5f, 20f), new PointF(5.5f, 11f),
							new PointF(22.5f, 11f), new PointF(19.5f, 20f) });
						using(Brush br = Grad(Folder, gp.GetBounds())) { g.FillPath(br, gp); }
						using(Pen pn = new Pen(Color.FromArgb(0xC9, 0x8A, 0x10), w * 0.6f)) { g.DrawPath(pn, gp); }
					}
					break;
				}
				case "save":
					Disk(g, w, 4f, 4f, 1f);
					break;
				case "saveall":
					Disk(g, w, 2f, 2f, 0.8f);
					Disk(g, w, 8.5f, 8.5f, 0.85f);
					break;
				case "addentry":
					Key(g, w, -1f, 0f);
					Badge(g, Green, 18f, 17.5f);
					break;
				case "user":
					using(Brush b = Grad(Blue, new RectangleF(7.5f, 2.5f, 9f, 9f))) { g.FillEllipse(b, 7.5f, 2.5f, 9f, 9f); }
					using(GraphicsPath gp = new GraphicsPath())
					{
						gp.AddArc(3.5f, 13f, 17f, 16f, 180, 180);
						gp.CloseFigure();
						using(Brush b = Grad(Blue, gp.GetBounds())) { g.FillPath(b, gp); }
					}
					break;
				case "key":
					Key(g, w, 0f, 1f);
					break;
				case "link":
					using(Pen p = MakePen(Gray, 2.6f))
					{
						using(GraphicsPath gp = RoundPath(2f, 8.5f, 11f, 7f, 3.5f)) { g.DrawPath(p, gp); }
					}
					using(Pen p = MakePen(Blue, 2.6f))
					{
						using(GraphicsPath gp = RoundPath(11f, 8.5f, 11f, 7f, 3.5f)) { g.DrawPath(p, gp); }
					}
					break;
				case "globe":
					using(Brush b = Grad(Blue, new RectangleF(2.5f, 2.5f, 19f, 19f))) { g.FillEllipse(b, 2.5f, 2.5f, 19f, 19f); }
					using(Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), w * 0.8f))
					{
						g.DrawEllipse(p, 8f, 2.8f, 8f, 18.4f);
						g.DrawLine(p, 3f, 12f, 21f, 12f);
						g.DrawLine(p, 4.5f, 7.5f, 19.5f, 7.5f);
						g.DrawLine(p, 4.5f, 16.5f, 19.5f, 16.5f);
					}
					break;
				case "keyboard":
					FillRoundOutline(g, Color.FromArgb(0xE9, 0xED, 0xF3), GrayDark, w * 0.8f, 1.5f, 5.5f, 21f, 13f, 2.5f);
					using(SolidBrush b = new SolidBrush(GrayDark))
					{
						foreach(float x in new float[] { 5f, 9f, 13f, 17f }) g.FillRectangle(b, x - 1f, 8.5f, 2.2f, 2.2f);
						g.FillRectangle(b, 6.5f, 13.5f, 11f, 2f);
					}
					break;
				case "search":
					using(Pen p = MakePen(GrayDark, 3.2f)) { g.DrawLine(p, 15f, 15f, 20.5f, 20.5f); }
					using(SolidBrush b = new SolidBrush(Color.FromArgb(0xD6, 0xE8, 0xFC))) { g.FillEllipse(b, 3f, 3f, 13.5f, 13.5f); }
					using(Pen p = new Pen(Blue, 2.2f)) { g.DrawEllipse(p, 3f, 3f, 13.5f, 13.5f); }
					break;
				case "filter":
					FillPoly(g, Blue, 3f, 4.5f, 21f, 4.5f, 14f, 12.5f, 14f, 19.5f, 10f, 21.5f, 10f, 12.5f);
					break;
				case "lock":
					using(Pen p = MakePen(Gray, 2.6f))
					{
						g.DrawArc(p, 7.5f, 2.5f, 9f, 10f, 180, 180);
						g.DrawLine(p, 7.5f, 7.5f, 7.5f, 11f);
						g.DrawLine(p, 16.5f, 7.5f, 16.5f, 11f);
					}
					FillRoundOutline(g, Gold, GoldDark, w * 0.7f, 4.5f, 10.5f, 15f, 11f, 2.2f);
					using(SolidBrush b = new SolidBrush(GoldDark))
					{
						g.FillEllipse(b, 10.6f, 13.5f, 2.8f, 2.8f);
						g.FillRectangle(b, 11.4f, 15.5f, 1.2f, 3f);
					}
					break;
				case "sync":
					using(Pen p = MakePen(Green, 2.6f))
					{
						g.DrawArc(p, 4f, 4f, 16f, 16f, 200, 130);
						g.DrawArc(p, 4f, 4f, 16f, 16f, 20, 130);
					}
					FillPoly(g, Green, 16.5f, 2.5f, 21f, 7.5f, 15f, 8.5f);
					FillPoly(g, Green, 7.5f, 21.5f, 3f, 16.5f, 9f, 15.5f);
					break;
				case "close":
					using(Pen p = MakePen(mono, w))
					{
						g.DrawLine(p, 6.5f, 6.5f, 17.5f, 17.5f);
						g.DrawLine(p, 17.5f, 6.5f, 6.5f, 17.5f);
					}
					break;
			}
		}
	}
}