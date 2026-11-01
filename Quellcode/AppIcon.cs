// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Eigenes Icon des Plugins (eingebettete Ressource FluentTheme.ico) für die
	/// Plugin-Liste von KeePass, den Menüeintrag und die Titelleiste des Info-Fensters.</summary>
	internal static class AppIcon
	{
		private const string ResName = "FluentTheme.ico";

		/// <summary>Kleine Symbolgröße (16 px) passend zur Bildschirmskalierung.</summary>
		public static int SmallSize
		{
			get
			{
				try
				{
					using(Graphics g = Graphics.FromHwnd(IntPtr.Zero))
					{
						return Math.Max(16, (int)Math.Round(16f * g.DpiX / 96f));
					}
				}
				catch(Exception) { return 16; }
			}
		}

		/// <summary>Icon in der passenden Größe aus der ICO-Datei (Windows wählt die nächstliegende).</summary>
		public static Icon GetIcon(int size)
		{
			try
			{
				Assembly asm = Assembly.GetExecutingAssembly();
				using(Stream st = asm.GetManifestResourceStream(ResName))
				{
					if(st == null) return null;
					return new Icon(st, size, size);
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); return null; }
		}

		/// <summary>Bild genau in der gewünschten Größe; fehlt sie in der ICO-Datei, wird die
		/// nächstgrößere hochwertig verkleinert.</summary>
		public static Image GetImage(int size)
		{
			Icon ico = GetIcon(size);
			if(ico == null) return null;
			try
			{
				using(ico)
				{
					Bitmap src = ico.ToBitmap();
					if((src.Width == size) && (src.Height == size)) return src;
					using(src)
					{
						Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
						using(Graphics g = Graphics.FromImage(bmp))
						{
							g.InterpolationMode = InterpolationMode.HighQualityBicubic;
							g.PixelOffsetMode = PixelOffsetMode.HighQuality;
							g.DrawImage(src, new Rectangle(0, 0, size, size));
						}
						return bmp;
					}
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); return null; }
		}
	}
}
