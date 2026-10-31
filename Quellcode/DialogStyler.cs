using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using KeePass.Forms;

namespace FluentTheme
{
	/// <summary>Gestaltet alle KeePass-Dialoge (Eintrag bearbeiten, Optionen, Datenbank öffnen …)
	/// im Windows-11-Stil: Hell/Dunkel wie Windows, Akzentfarbe, flache Eingabefelder,
	/// abgerundete Buttons, moderne Tabs.</summary>
	internal sealed class DialogStyler
	{
		private readonly Form m_main;
		private readonly HashSet<Form> m_styled = new HashSet<Form>();
		private readonly Dictionary<Button, ButtonSkin> m_buttons = new Dictionary<Button, ButtonSkin>();
		private readonly Dictionary<TabControl, TabStripPainter> m_tabs = new Dictionary<TabControl, TabStripPainter>();
		private readonly Dictionary<Control, QualityBarPainter> m_qbars = new Dictionary<Control, QualityBarPainter>();
		private readonly HashSet<Control> m_hookedContainers = new HashSet<Control>();
		private Palette m_p;
		private float m_s = 1f;
		private EventInfo m_gwmEvent;
		private Delegate m_gwmHandler;

		public DialogStyler(Form mainForm)
		{
			m_main = mainForm;
		}

		internal Palette Palette
		{
			get { return m_p; }
		}

		internal float Scale
		{
			get { return m_s; }
		}

		public void Attach()
		{
			try
			{
				Type gwm = typeof(MainForm).Assembly.GetType("KeePass.UI.GlobalWindowManager");
				if(gwm == null) return;
				m_gwmEvent = gwm.GetEvent("WindowAdded", BindingFlags.Public | BindingFlags.Static);
				MethodInfo mi = typeof(DialogStyler).GetMethod("OnWindowAdded",
					BindingFlags.Instance | BindingFlags.NonPublic);
				if((m_gwmEvent == null) || (mi == null)) return;
				m_gwmHandler = Delegate.CreateDelegate(m_gwmEvent.EventHandlerType, this, mi);
				m_gwmEvent.AddEventHandler(null, m_gwmHandler);
			}
			catch(Exception ex)
			{
				FluentThemeExt.Report(ex);
				m_gwmEvent = null;
				m_gwmHandler = null;
			}
		}

		public void Detach()
		{
			try
			{
				if((m_gwmEvent != null) && (m_gwmHandler != null)) m_gwmEvent.RemoveEventHandler(null, m_gwmHandler);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			foreach(TabStripPainter tp in m_tabs.Values) tp.Detach();
			m_tabs.Clear();
			foreach(QualityBarPainter qp in m_qbars.Values) qp.Detach();
			m_qbars.Clear();
		}

		public void Update(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
			foreach(Form f in new List<Form>(m_styled))
			{
				if(!f.IsDisposed) ApplyForm(f);
			}
		}

		/// <summary>Sicherheitsnetz: Fenster, die nicht über KeePass gemeldet wurden.</summary>
		public void Poll()
		{
			if(m_p == null) return;
			try
			{
				foreach(Form f in Application.OpenForms)
				{
					if((f != m_main) && !m_styled.Contains(f) && !f.IsDisposed) Style(f);
				}
			}
			catch(InvalidOperationException) { } // Auflistung während Änderung
		}

		private void OnWindowAdded(object sender, EventArgs e)
		{
			try
			{
				PropertyInfo pi = e.GetType().GetProperty("Form", BindingFlags.Instance | BindingFlags.Public);
				Form f = ((pi != null) ? (pi.GetValue(e, null) as Form) : null);
				if(f != null) Style(f);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		public void Style(Form f)
		{
			if((f == null) || (f == m_main) || f.IsDisposed || (m_p == null)) return;
			if(f is AboutDialog) return; // gestaltet sich selbst
			if(!m_styled.Add(f)) return;
			f.Disposed += OnFormDisposed;
			f.HandleCreated += OnFormHandleCreated;
			ApplyForm(f);
		}

		private void OnFormDisposed(object sender, EventArgs e)
		{
			Form f = (sender as Form);
			if(f == null) return;
			m_styled.Remove(f);
		}

		private void OnFormHandleCreated(object sender, EventArgs e)
		{
			Form f = (sender as Form);
			if((f != null) && (m_p != null))
				NativeMethods.SetTitleBar(f.Handle, m_p.Dark, m_p.Bg, m_p.Text, m_p.Border);
		}

		private void ApplyForm(Form f)
		{
			try
			{
				f.SuspendLayout();
				f.BackColor = m_p.Bg;
				f.ForeColor = m_p.Text;
				if(f.IsHandleCreated) NativeMethods.SetTitleBar(f.Handle, m_p.Dark, m_p.Bg, m_p.Text, m_p.Border);
				foreach(Control c in f.Controls) ApplyControl(c, f);
				HookContainer(f, f);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			finally { f.ResumeLayout(); }
		}

		private void HookContainer(Control c, Form f)
		{
			if(!m_hookedContainers.Add(c)) return;
			c.ControlAdded += delegate(object sender, ControlEventArgs e)
			{
				if((m_p != null) && (e.Control != null)) ApplyControl(e.Control, f);
			};
			c.Disposed += delegate { m_hookedContainers.Remove(c); };
		}

		private void ThemeNative(Control c, string darkTheme, string lightTheme)
		{
			if(c.IsHandleCreated) NativeMethods.ThemeControl(c, m_p.Dark, darkTheme, lightTheme);
			else
			{
				c.HandleCreated += delegate
				{
					if(m_p != null) NativeMethods.ThemeControl(c, m_p.Dark, darkTheme, lightTheme);
				};
			}
		}

		private void ApplyControl(Control c, Form f)
		{
			if((c == null) || c.IsDisposed) return;
			Palette p = m_p;

			try
			{
				if(c is PictureBox) { /* Banner und Bilder unverändert */ }
				else if(QualityBarPainter.IsQualityBar(c))
				{
					QualityBarPainter qp;
					if(!m_qbars.TryGetValue(c, out qp))
					{
						qp = new QualityBarPainter(c);
						if(qp.IsValid)
						{
							m_qbars[c] = qp;
							Control qc = c;
							c.Disposed += delegate
							{
								QualityBarPainter q;
								if(m_qbars.TryGetValue(qc, out q)) { q.Detach(); m_qbars.Remove(qc); }
							};
						}
						else { qp.Detach(); qp = null; }
					}
					if(qp != null) qp.Update(p, m_s);
				}
				else if(c is Button) StyleButton((Button)c);
				else if((c is TextBoxBase) || (c is NumericUpDown))
				{
					c.BackColor = p.Card;
					c.ForeColor = p.Text;
					TextBox tb = (c as TextBox);
					if((tb != null) && p.Dark && (tb.BorderStyle == BorderStyle.Fixed3D))
						tb.BorderStyle = BorderStyle.FixedSingle;
					ThemeNative(c, "DarkMode_Explorer", "Explorer");
				}
				else if(c is ComboBox)
				{
					ComboBox cb = (ComboBox)c;
					cb.BackColor = p.Card;
					cb.ForeColor = p.Text;
					if(p.Dark) cb.FlatStyle = FlatStyle.Flat;
					ThemeNative(c, "DarkMode_CFD", "Explorer");
				}
				else if(c is ListView)
				{
					ListView lv = (ListView)c;
					lv.BackColor = p.Card;
					lv.ForeColor = p.Text;
					ThemeNative(c, "DarkMode_Explorer", "Explorer");
					if(lv.IsHandleCreated)
						NativeMethods.ThemeHandle(NativeMethods.GetListHeader(lv), p.Dark, "DarkMode_ItemsView", "ItemsView");
				}
				else if(c is TreeView)
				{
					c.BackColor = p.Card;
					c.ForeColor = p.Text;
					ThemeNative(c, "DarkMode_Explorer", "Explorer");
				}
				else if(c is ListBox)
				{
					c.BackColor = p.Card;
					c.ForeColor = p.Text;
					ThemeNative(c, "DarkMode_Explorer", "Explorer");
				}
				else if(c is TabControl)
				{
					TabControl tc = (TabControl)c;
					foreach(TabPage tp in tc.TabPages)
					{
						tp.UseVisualStyleBackColor = false;
						tp.BackColor = p.Bg;
						tp.ForeColor = p.Text;
					}
					TabStripPainter painter;
					if(!m_tabs.TryGetValue(tc, out painter))
					{
						painter = new TabStripPainter(tc);
						m_tabs[tc] = painter;
						tc.Disposed += delegate
						{
							TabStripPainter tsp;
							if(m_tabs.TryGetValue(tc, out tsp)) { tsp.Detach(); m_tabs.Remove(tc); }
						};
					}
					// Mehr Innenabstand, damit die Beschriftungen in den abgerundeten Tabs nicht
					// abgeschnitten werden und die Akzentlinie unter dem Text bleibt
					Point pad = new Point((int)Math.Round(12 * m_s), (int)Math.Round(7 * m_s));
					if((tc.Padding.X < pad.X) || (tc.Padding.Y < pad.Y)) tc.Padding = pad;
					Font bold;
					try { bold = new Font("Segoe UI Semibold", tc.Font.SizeInPoints); }
					catch(ArgumentException) { bold = new Font(tc.Font, FontStyle.Bold); }
					painter.Underline = true;
					painter.Update(p, m_s, tc.Font, bold);
				}
				else if(c is LinkLabel)
				{
					LinkLabel ll = (LinkLabel)c;
					ll.ForeColor = p.Text;
					ll.LinkColor = p.Link;
					ll.ActiveLinkColor = p.Link;
					ll.VisitedLinkColor = p.Link;
				}
				else if((c is Label) || (c is CheckBox) || (c is RadioButton) || (c is GroupBox))
				{
					c.ForeColor = p.Text;
					if(c.BackColor != Color.Transparent) c.BackColor = c.Parent.BackColor;
					if((c is CheckBox) || (c is RadioButton)) ThemeNative(c, "DarkMode_Explorer", "Explorer");
				}
				else if((c is Panel) || (c is TableLayoutPanel) || (c is FlowLayoutPanel) ||
					(c is SplitContainer) || (c is UserControl) || (c is TabPage))
				{
					if(!(c is TabPage)) c.BackColor = c.Parent.BackColor;
					c.ForeColor = p.Text;
				}
				else if(c is ToolStrip) { /* Renderer ist global gesetzt */ }
				else
				{
					c.ForeColor = p.Text;
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }

			if((c.Controls.Count > 0) && !(c is NumericUpDown) && !(c is UserControl && c.GetType().Namespace == "System.Windows.Forms"))
			{
				foreach(Control child in c.Controls) ApplyControl(child, f);
			}
			if((c is Panel) || (c is TabPage) || (c is GroupBox) || (c is TableLayoutPanel) ||
				(c is FlowLayoutPanel) || (c is UserControl)) HookContainer(c, f);
		}

		private void StyleButton(Button b)
		{
			ButtonSkin skin;
			if(!m_buttons.TryGetValue(b, out skin))
			{
				skin = new ButtonSkin(b, this);
				m_buttons[b] = skin;
				b.Disposed += delegate { m_buttons.Remove(b); };
			}
			skin.Apply();
		}
	}

	/// <summary>Zeichnet einen Button abgerundet im Windows-11-Stil. Der Standard-Button (OK)
	/// bekommt die Akzentfarbe, alle anderen sind weiß bzw. dunkel mit feinem Rahmen.</summary>
	internal sealed class ButtonSkin
	{
		private readonly Button m_b;
		private readonly DialogStyler m_owner;
		private bool m_hot;
		private bool m_down;

		public ButtonSkin(Button b, DialogStyler owner)
		{
			m_b = b;
			m_owner = owner;
			b.Paint += OnPaint;
			b.MouseEnter += delegate { m_hot = true; m_b.Invalidate(); };
			b.MouseLeave += delegate { m_hot = false; m_down = false; m_b.Invalidate(); };
			b.MouseDown += delegate(object s, MouseEventArgs e) { if(e.Button == MouseButtons.Left) { m_down = true; m_b.Invalidate(); } };
			b.MouseUp += delegate { m_down = false; m_b.Invalidate(); };
			b.GotFocus += delegate { m_b.Invalidate(); };
			b.LostFocus += delegate { m_b.Invalidate(); };
			b.EnabledChanged += delegate { m_b.Invalidate(); };
		}

		public void Apply()
		{
			Palette p = m_owner.Palette;
			if(p == null) return;
			m_b.FlatStyle = FlatStyle.Flat;
			m_b.FlatAppearance.BorderSize = 0;
			m_b.UseVisualStyleBackColor = false;
			m_b.BackColor = ((m_b.Parent != null) ? m_b.Parent.BackColor : p.Bg);
			m_b.ForeColor = p.Text;
			m_b.Invalidate();
		}

		private bool IsPrimary()
		{
			Form f = m_b.FindForm();
			return ((f != null) && (f.AcceptButton == m_b));
		}

		private void OnPaint(object sender, PaintEventArgs e)
		{
			Palette p = m_owner.Palette;
			if(p == null) return;
			try
			{
				float s = m_owner.Scale;
				int r6 = (int)Math.Round(5 * s);
				Graphics g = e.Graphics;
				Color parentBg = ((m_b.Parent != null) ? m_b.Parent.BackColor : p.Bg);
				g.Clear(parentBg);

				Rectangle rc = new Rectangle(1, 1, m_b.Width - 2, m_b.Height - 2);
				bool primary = IsPrimary();
				bool enabled = m_b.Enabled;
				Color fill, border, text;

				if(primary && enabled)
				{
					Color acc = p.Pill;
					fill = (m_down ? Palette.Blend(acc, p.Dark ? Color.Black : Color.Black, 0.18) :
						(m_hot ? Palette.Blend(acc, p.Dark ? Color.White : Color.Black, 0.10) : acc));
					border = fill;
					text = ((Palette.Contrast(fill, Color.White) >= 3.0) ? Color.White : Color.Black);
				}
				else
				{
					Color baseFill = (p.Dark ? p.BtnBg : p.Card);
					fill = (!enabled ? Palette.Blend(baseFill, parentBg, 0.5) : (m_down ? p.Sel : (m_hot ? p.CardHover : baseFill)));
					border = p.Border;
					text = (enabled ? p.Text : Palette.Blend(p.Sub, fill, 0.35));
				}

				Gfx.Fill(g, rc, r6, fill);
				Gfx.Stroke(g, rc, r6, border);
				if(m_b.Focused && enabled)
				{
					Rectangle fr = new Rectangle(0, 0, m_b.Width, m_b.Height);
					Gfx.Stroke(g, fr, r6 + 1, p.Pill);
				}

				// Inhalt: optionales Bild links vom Text bzw. mittig
				Rectangle content = Rectangle.Inflate(rc, -(int)Math.Round(6 * s), -2);
				Image img = m_b.Image;
				string txt = m_b.Text;
				if(img != null)
				{
					int iw = img.Width, ih = img.Height;
					int tw = (string.IsNullOrEmpty(txt) ? 0 : TextRenderer.MeasureText(g, txt, m_b.Font).Width);
					int total = iw + ((tw > 0) ? ((int)Math.Round(6 * s) + tw) : 0);
					int ix = content.Left + Math.Max(0, (content.Width - total) / 2);
					int iy = content.Top + (content.Height - ih) / 2;
					if(enabled) g.DrawImage(img, new Rectangle(ix, iy, iw, ih));
					else ControlPaint.DrawImageDisabled(g, img, ix, iy, fill);
					if(tw > 0)
					{
						Rectangle tr = new Rectangle(ix + iw + (int)Math.Round(6 * s), content.Top, tw, content.Height);
						TextRenderer.DrawText(g, txt, m_b.Font, tr, text, TextFormatFlags.Left |
							TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
							TextFormatFlags.HidePrefix);
					}
				}
				else if(!string.IsNullOrEmpty(txt))
				{
					TextRenderer.DrawText(g, txt, m_b.Font, content, text, TextFormatFlags.HorizontalCenter |
						TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
						TextFormatFlags.HidePrefix);
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}
	}
}