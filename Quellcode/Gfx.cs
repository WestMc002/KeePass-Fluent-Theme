// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FluentTheme
{
	internal static class Gfx
	{
		public static GraphicsPath Round(Rectangle r, int radius)
		{
			GraphicsPath p = new GraphicsPath();
			int d = radius * 2;
			if((radius <= 0) || (r.Width < d) || (r.Height < d))
			{
				p.AddRectangle(r);
				return p;
			}

			p.AddArc(r.X, r.Y, d, d, 180, 90);
			p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
			p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
			p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
			p.CloseFigure();
			return p;
		}

		public static void Fill(Graphics g, Rectangle r, int radius, Color c)
		{
			if((r.Width <= 0) || (r.Height <= 0)) return;
			SmoothingMode old = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using(GraphicsPath p = Round(r, radius))
			{
				using(SolidBrush b = new SolidBrush(c)) { g.FillPath(b, p); }
			}
			g.SmoothingMode = old;
		}

		public static void Stroke(Graphics g, Rectangle r, int radius, Color c)
		{
			r.Width -= 1;
			r.Height -= 1;
			if((r.Width <= 0) || (r.Height <= 0)) return;
			SmoothingMode old = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using(GraphicsPath p = Round(r, radius))
			{
				using(Pen pen = new Pen(c)) { g.DrawPath(pen, p); }
			}
			g.SmoothingMode = old;
		}

		public static void Chevron(Graphics g, float cx, float cy, float size, bool up, Color c,
			float width)
		{
			SmoothingMode old = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			float h = size / 2f;
			float q = size / 4f;
			using(Pen p = new Pen(c, width))
			{
				p.StartCap = LineCap.Round;
				p.EndCap = LineCap.Round;
				p.LineJoin = LineJoin.Round;
				if(up)
					g.DrawLines(p, new PointF[] { new PointF(cx - h, cy + q),
						new PointF(cx, cy - q), new PointF(cx + h, cy + q) });
				else
					g.DrawLines(p, new PointF[] { new PointF(cx - h, cy - q),
						new PointF(cx, cy + q), new PointF(cx + h, cy - q) });
			}
			g.SmoothingMode = old;
		}
	}
}