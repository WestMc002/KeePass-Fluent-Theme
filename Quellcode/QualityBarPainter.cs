using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Zeichnet den Passwort-Qualitätsbalken von KeePass (QualityProgressBar, z. B. in
	/// „Eintrag bearbeiten“) im Windows-11-Stil: abgerundet, Ampelfarbe wie in der Detailansicht
	/// statt des Verlaufs von Orange nach Grün.</summary>
	internal sealed class QualityBarPainter : NativeWindow
	{
		private const int WM_PAINT = 0x000F;
		private const int WM_ERASEBKGND = 0x0014;

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct PAINTSTRUCT
		{
			public IntPtr hdc;
			public int fErase;
			public RECT rcPaint;
			public int fRestore;
			public int fIncUpdate;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
			public byte[] rgbReserved;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

		[DllImport("user32.dll")]
		private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

		private readonly Control m_c;
		private readonly PropertyInfo m_value, m_min, m_max, m_text;
		private Palette m_p;
		private float m_s = 1f;

		/// <summary>Nur für KeePass' Qualitätsbalken (und nur, wenn seine Eigenschaften gefunden werden).</summary>
		public static bool IsQualityBar(Control c)
		{
			return ((c != null) && (c.GetType().Name == "QualityProgressBar"));
		}

		public QualityBarPainter(Control c)
		{
			m_c = c;
			Type t = c.GetType();
			m_value = Prop(t, "ProgressValue", "Value");
			m_min = Prop(t, "ProgressMinimum", "Minimum");
			m_max = Prop(t, "ProgressMaximum", "Maximum");
			m_text = t.GetProperty("ProgressText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if((m_text != null) && (m_text.PropertyType != typeof(string))) m_text = null;
			c.HandleCreated += OnHandleCreated;
			c.HandleDestroyed += OnHandleDestroyed;
			if(c.IsHandleCreated) AssignHandle(c.Handle);
		}

		public bool IsValid
		{
			get { return ((m_value != null) && (m_max != null)); }
		}

		private static PropertyInfo Prop(Type t, string a, string b)
		{
			const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			PropertyInfo pi = t.GetProperty(a, bf);
			if((pi == null) || (pi.PropertyType != typeof(int))) pi = t.GetProperty(b, bf);
			return (((pi != null) && (pi.PropertyType == typeof(int))) ? pi : null);
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		public void Update(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
			m_c.Invalidate();
		}

		public void Detach()
		{
			m_c.HandleCreated -= OnHandleCreated;
			m_c.HandleDestroyed -= OnHandleDestroyed;
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
			m_c.Invalidate();
		}

		private void OnHandleCreated(object sender, EventArgs e)
		{
			if(this.Handle == IntPtr.Zero) AssignHandle(m_c.Handle);
		}

		private void OnHandleDestroyed(object sender, EventArgs e)
		{
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
		}

		protected override void WndProc(ref Message m)
		{
			if((m_p != null) && this.IsValid)
			{
				if(m.Msg == WM_ERASEBKGND)
				{
					m.Result = new IntPtr(1);
					return;
				}
				if(m.Msg == WM_PAINT)
				{
					PAINTSTRUCT ps;
					IntPtr hdc = BeginPaint(m.HWnd, out ps);
					try { Paint(hdc); }
					catch(Exception ex) { FluentThemeExt.Report(ex); }
					finally { EndPaint(m.HWnd, ref ps); }
					m.Result = IntPtr.Zero;
					return;
				}
			}
			base.WndProc(ref m);
		}

		/// <summary>Erste Zahl im Text (z. B. 95 aus „95 Bits“), sonst -1.</summary>
		private static int LeadingNumber(string s)
		{
			if(string.IsNullOrEmpty(s)) return -1;
			int i = 0;
			while((i < s.Length) && !char.IsDigit(s[i])) ++i;
			int n = 0, digits = 0;
			while((i < s.Length) && char.IsDigit(s[i]) && (digits < 6))
			{
				n = n * 10 + (s[i] - '0');
				++i;
				++digits;
			}
			return ((digits > 0) ? n : -1);
		}

		private void Paint(IntPtr hdc)
		{
			Rectangle cr = m_c.ClientRectangle;
			if((cr.Width <= 0) || (cr.Height <= 0)) return;

			int val = (int)m_value.GetValue(m_c, null);
			int min = ((m_min != null) ? (int)m_min.GetValue(m_c, null) : 0);
			int max = (int)m_max.GetValue(m_c, null);
			double frac = ((max > min) ? Math.Max(0.0, Math.Min(1.0, (val - min) / (double)(max - min))) : 0.0);

			string text = ((m_text != null) ? (m_text.GetValue(m_c, null) as string) : null);
			if(string.IsNullOrEmpty(text)) text = m_c.Text;

			// Gleiche Schwellen wie die Passwortstärke in der Detailansicht. Der Balkenwert ist bei
			// KeePass ein Prozentwert – die Bits stehen in der Beschriftung („95 Bits“).
			int bits = LeadingNumber(text);
			if(bits < 0) bits = (int)Math.Round(frac * 128.0);
			Color c = ((bits < 50) ? m_p.Weak : ((bits < 80) ? m_p.Mid : m_p.Good));

			using(BufferedGraphics bg = BufferedGraphicsManager.Current.Allocate(hdc, cr))
			{
				Graphics g = bg.Graphics;
				Color back = ((m_c.Parent != null) ? m_c.Parent.BackColor : m_p.Bg);
				g.Clear(back);

				Rectangle track = Rectangle.FromLTRB(cr.Left, cr.Top + 1, cr.Right - 1, cr.Bottom - 1);
				int rad = Math.Min(D(6), track.Height / 2);
				Gfx.Fill(g, track, rad, m_p.Card);
				Gfx.Stroke(g, new Rectangle(track.X, track.Y, track.Width + 1, track.Height + 1), rad, m_p.Border);

				if(frac > 0.0)
				{
					Rectangle fill = track;
					fill.Inflate(-D(2), -D(2));
					fill.Width = Math.Max(fill.Height, (int)Math.Round(fill.Width * frac));
					Gfx.Fill(g, fill, Math.Max(1, rad - D(2)), Palette.Blend(m_p.Card, c, m_p.Dark ? 0.55 : 0.45));
				}

				if(!string.IsNullOrEmpty(text))
					TextRenderer.DrawText(g, text, m_c.Font, track, m_p.Text, TextFormatFlags.HorizontalCenter |
						TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

				bg.Render();
			}
		}
	}
}
