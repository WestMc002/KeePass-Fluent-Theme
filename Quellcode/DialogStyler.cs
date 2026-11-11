// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
		private readonly Dictionary<ButtonBase, ButtonSkin> m_buttons = new Dictionary<ButtonBase, ButtonSkin>();
		private readonly HashSet<Control> m_bgGuard = new HashSet<Control>();
		private readonly Dictionary<TabControl, TabStripPainter> m_tabs = new Dictionary<TabControl, TabStripPainter>();
		private readonly Dictionary<Control, QualityBarPainter> m_qbars = new Dictionary<Control, QualityBarPainter>();
		private readonly Dictionary<Control, EditFramePainter> m_frames = new Dictionary<Control, EditFramePainter>();
		private readonly HashSet<Control> m_hookedContainers = new HashSet<Control>();
		private readonly HashSet<TabPage> m_cardPages = new HashSet<TabPage>();
		private readonly HashSet<Form> m_alignHooked = new HashSet<Form>();
		private Palette m_p;
		private float m_s = 1f;
		private readonly HashSet<TabControl> m_fitTabs = new HashSet<TabControl>();
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
			foreach(EditFramePainter fp in m_frames.Values) fp.Detach();
			m_frames.Clear();
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
				if(m_alignHooked.Add(f))
				{
					// Erst nach dem Laden ausrichten (KeePass setzt Positionen und DPI-Skalierung vorher)
					f.Shown += delegate { AlignLabels(f); };
					f.Disposed += delegate { m_alignHooked.Remove(f); };
				}
				if(f.Visible) AlignLabels(f);
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
				if(IsSeparator(c))
				{
					// Trennlinie (z. B. über den Knöpfen): statt der hellen Doppellinie eine feine Linie
					SetBorderStyle(c, BorderStyle.None);
					if(c is Label) c.Text = string.Empty;
					if(c.Height <= c.Width) c.Height = 1; else c.Width = 1;
					c.BackColor = p.Border;
				}
				else if(c is PictureBox) { /* Banner und Bilder unverändert */ }
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
				else if(((c is CheckBox) && (((CheckBox)c).Appearance == Appearance.Button)) ||
					((c is RadioButton) && (((RadioButton)c).Appearance == Appearance.Button)))
					StyleButton((ButtonBase)c); // Umschalt-Knöpfe wie „●●●“ (Passwort anzeigen)
				else if((c is TextBoxBase) || (c is NumericUpDown))
				{
					c.BackColor = FieldBg(p);
					c.ForeColor = p.Text;
					if(m_bgGuard.Add(c))
					{
						// KeePass setzt die Farbe teils zurück (z. B. „Passwort-Wdh.“ nach dem Vergleich auf
						// Weiß bzw. bei Abweichung auf Rot) – Weiß wird wieder Feldfarbe, Rot gedämpft
						Control gc = c;
						c.BackColorChanged += delegate { GuardFieldBg(gc); };
						c.Disposed += delegate { m_bgGuard.Remove(gc); };
					}
					// Einzeilige Felder ohne Windows-Design: Rahmen kommt von EditFramePainter. Das
					// Design würde beim Fokuswechsel seinen eigenen (im Dunkelmodus weißen) Rahmen animieren.
					if((c is TextBox) && EditFramePainter.Wants(c)) ThemeNative(c, " ", " ");
					else ThemeNative(c, "DarkMode_Explorer", "Explorer");
					if(EditFramePainter.Wants(c)) Frame(c, p);
				}
				else if(c is DateTimePicker) Frame(c, p);
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
						tp.BackColor = p.Card; // Inhalt auf einer Karte (siehe OnCardPaint)
						tp.ForeColor = p.Text;
						if(m_cardPages.Add(tp))
						{
							TabPage page = tp;
							tp.Paint += OnCardPaint;
							tp.Resize += delegate { page.Invalidate(); };
							tp.Disposed += delegate { m_cardPages.Remove(page); };
						}
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
					Font bold;
					try { bold = new Font("Segoe UI Semibold", tc.Font.SizeInPoints); }
					catch(ArgumentException) { bold = new Font(tc.Font, FontStyle.Bold); }
					// Bei vielen Tabs (z. B. Optionen) den seitlichen Abstand so weit verringern, dass alle
					// in die Breite passen – sonst blendet Windows Pfeile ein, die über den Tabs liegen.
					// Auch später noch, wenn Plugins Tabs hinzufügen oder der Dialog seine Größe ändert.
					bool firstFit = m_fitTabs.Add(tc);
					FitTabs(tc, firstFit);
					if(firstFit)
					{
						TabControl t2 = tc;
						// später hinzugefügte Tabs (Plugins): Abstand nur noch verkleinern, nie vergrößern –
						// das Ändern des Abstands legt das Steuerelement neu an, ein Hin und Her würde hängen
						tc.ControlAdded += delegate { BeginFitTabs(t2); };
						tc.Disposed += delegate { m_fitTabs.Remove(t2); };
					}
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
				else if(c is ToolStrip) ModernToolTip.AttachTo((ToolStrip)c); // Renderer ist global gesetzt
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

		/// <summary>Eigener Rahmen für Kommentar- und Datumsfeld (siehe EditFramePainter).</summary>
		private void Frame(Control c, Palette p)
		{
			EditFramePainter fp;
			if(!m_frames.TryGetValue(c, out fp))
			{
				fp = new EditFramePainter(c);
				m_frames[c] = fp;
				Control fc = c;
				c.Disposed += delegate
				{
					EditFramePainter e;
					if(m_frames.TryGetValue(fc, out e)) { e.Detach(); m_frames.Remove(fc); }
				};
			}
			fp.Update(p, m_s);
		}

		/// <summary>Hintergrund der Eingabefelder: auf der weißen Karte leicht abgesetzt (Windows 11).</summary>
		internal static Color FieldBg(Palette p)
		{
			return (p.Dark ? Palette.Blend(p.Card, Color.White, 0.04) : Palette.Blend(p.Card, p.Bg, 0.35));
		}

		/// <summary>Registerkarten-Inhalt als Karte: runde Ecken in Fensterfarbe, feiner Rahmen.</summary>
		private void BeginFitTabs(TabControl tc)
		{
			try
			{
				if(tc.IsHandleCreated && !tc.IsDisposed) tc.BeginInvoke(new MethodInvoker(delegate { FitTabs(tc, false); }));
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Seitlichen Innenabstand der Tabs so wählen, dass alle Tabs in eine Zeile passen
		/// (höchstens 12, mindestens 3 bei 100 %). Gemessen an den echten Tab-Breiten von Windows.</summary>
		private bool m_fitting;

		private void FitTabs(TabControl tc, bool allowGrow)
		{
			if(m_fitting) return;
			m_fitting = true;
			try
			{
				if(tc.IsDisposed) return;
				int maxPad = (int)Math.Round(12 * m_s);
				int minPad = (int)Math.Round(3 * m_s);
				int padY = Math.Max((int)Math.Round(8 * m_s), tc.Padding.Y);
				int n = tc.TabCount;
				int padX = maxPad;
				if((n > 0) && tc.IsHandleCreated && (tc.ClientSize.Width > 0))
				{
					// Breite der Tabs ohne Innenabstand aus den aktuellen Tab-Rechtecken
					int total = 0;
					for(int i = 0; i < n; ++i)
					{
						int w = tc.GetTabRect(i).Width;
						if(w <= 0) return; // noch nicht angeordnet – nichts ändern
						total += w;
					}
					int text = total - 2 * tc.Padding.X * n;
					// Platz für den Rand und die halbfette Schrift des aktiven Tabs
					int avail = tc.ClientSize.Width - (int)Math.Round(16 * m_s);
					if(text + 2 * maxPad * n > avail)
						padX = Math.Max(minPad, Math.Min(maxPad, (avail - text) / (2 * n)));
				}
				else if(n > 0) return; // ohne Maße nichts ändern
				if(!allowGrow) padX = Math.Min(padX, tc.Padding.X);
				if((tc.Padding.X != padX) || (tc.Padding.Y < padY)) tc.Padding = new Point(padX, Math.Max(padY, tc.Padding.Y));
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			finally { m_fitting = false; }
		}

		private void OnCardPaint(object sender, PaintEventArgs e)
		{
			TabPage tp = (sender as TabPage);
			Palette p = m_p;
			if((tp == null) || (p == null)) return;
			try
			{
				Rectangle r = tp.ClientRectangle;
				if((r.Width < 4) || (r.Height < 4)) return;
				int rad = (int)Math.Round(8 * m_s);
				using(GraphicsPath path = Gfx.Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), rad))
				using(Region outside = new Region(r))
				{
					outside.Exclude(path);
					using(SolidBrush b = new SolidBrush(p.Bg)) { e.Graphics.FillRegion(b, outside); }
				}
				Gfx.Stroke(e.Graphics, r, rad, p.Border);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Beschriftungen („Titel:“, „Benutzername:“ …) senkrecht mittig zum Feld daneben
		/// setzen. Nur kleine Korrekturen (höchstens 8 px), damit nichts verrutscht.</summary>
		private void AlignLabels(Control parent)
		{
			if((parent == null) || parent.IsDisposed || (m_p == null)) return;
			try
			{
				int maxShift = (int)Math.Round(8 * m_s);
				foreach(Control lb in parent.Controls)
				{
					if(!((lb is Label) || (lb is CheckBox)) || !lb.AutoSize) continue;
					if(lb.Height >= 2 * lb.Font.Height) continue; // mehrzeilig
					Control best = null;
					int bestDiff = int.MaxValue;
					int lbMid = lb.Top + lb.Height / 2;
					foreach(Control c in parent.Controls)
					{
						if((c == lb) || !c.Visible || !IsField(c) || (c.Left < lb.Right - 4)) continue;
						int diff = (c.Top + c.Height / 2) - lbMid;
						if(Math.Abs(diff) > Math.Max(c.Height, lb.Height) / 2) continue;
						if((best == null) || (Math.Abs(diff) < Math.Abs(bestDiff)) ||
							((Math.Abs(diff) == Math.Abs(bestDiff)) && (c.Left < best.Left)))
						{
							best = c;
							bestDiff = diff;
						}
					}
					if((best != null) && (bestDiff != 0) && (Math.Abs(bestDiff) <= maxShift))
						lb.Top += bestDiff;
				}
				foreach(Control child in parent.Controls)
				{
					if(child.Controls.Count > 0) AlignLabels(child);
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private static bool IsField(Control c)
		{
			TextBoxBase tb = (c as TextBoxBase);
			if(tb != null) return !tb.Multiline;
			return ((c is ComboBox) || (c is DateTimePicker) || (c is NumericUpDown) ||
				QualityBarPainter.IsQualityBar(c));
		}

		private void GuardFieldBg(Control c)
		{
			Palette p = m_p;
			if((p == null) || c.IsDisposed) return;
			Color want = FieldBg(p);
			Color cur = c.BackColor;
			if(cur.ToArgb() == want.ToArgb()) return;
			Color target;
			if((cur.ToArgb() == SystemColors.Window.ToArgb()) || (cur.ToArgb() == Color.White.ToArgb()) ||
				(cur.ToArgb() == p.Card.ToArgb()) || (cur.ToArgb() == SystemColors.Control.ToArgb()))
				target = want;
			else target = Palette.Blend(want, cur, p.Dark ? 0.30 : 0.60); // z. B. Rot bei Abweichung
			if(cur.ToArgb() != target.ToArgb()) c.BackColor = target;
		}

		/// <summary>Schmale Linie mit 3D-Rahmen (Label, Panel o. Ä.), die KeePass als Trenner nutzt.</summary>
		private bool IsSeparator(Control c)
		{
			if((c.Controls.Count > 0) || (c is TextBoxBase) || (c is ButtonBase)) return false;
			int lim = (int)Math.Round(3 * m_s) + 1;
			bool thin = (((c.Height <= lim) && (c.Width > 4 * lim)) || ((c.Width <= lim) && (c.Height > 4 * lim)));
			if(!thin) return false;
			PropertyInfo pi = c.GetType().GetProperty("BorderStyle", BindingFlags.Instance | BindingFlags.Public);
			if((pi == null) || (pi.PropertyType != typeof(BorderStyle))) return (c.BackColor == SeparatorColor());
			return ((BorderStyle)pi.GetValue(c, null) != BorderStyle.None) || (c.BackColor == SeparatorColor());
		}

		private Color SeparatorColor()
		{
			return ((m_p != null) ? m_p.Border : Color.Empty);
		}

		private static void SetBorderStyle(Control c, BorderStyle bs)
		{
			PropertyInfo pi = c.GetType().GetProperty("BorderStyle", BindingFlags.Instance | BindingFlags.Public);
			if((pi != null) && (pi.PropertyType == typeof(BorderStyle)) && pi.CanWrite) pi.SetValue(c, bs, null);
		}

		private void StyleButton(ButtonBase b)
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
		private readonly ButtonBase m_b;
		private readonly DialogStyler m_owner;
		private bool m_hot;
		private bool m_down;

		public ButtonSkin(ButtonBase b, DialogStyler owner)
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
			CheckBox cb = (b as CheckBox);
			if(cb != null) cb.CheckedChanged += delegate { m_b.Invalidate(); };
			RadioButton rb = (b as RadioButton);
			if(rb != null) rb.CheckedChanged += delegate { m_b.Invalidate(); };
		}

		private bool IsChecked()
		{
			CheckBox cb = (m_b as CheckBox);
			if(cb != null) return cb.Checked;
			RadioButton rb = (m_b as RadioButton);
			return ((rb != null) && rb.Checked);
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
			return ((f != null) && (m_b is IButtonControl) && (f.AcceptButton == (IButtonControl)m_b));
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
					if(IsChecked() && enabled) baseFill = Palette.Blend(baseFill, p.Pill, p.Dark ? 0.30 : 0.16);
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
