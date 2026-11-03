// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Suchfeld der Symbolleiste im Windows-11-Stil: abgerundeter Rahmen, schlichter Pfeil
	/// statt des Windows-Pfeilkastens, Akzentlinie unten bei Fokus. Das Textfeld selbst zeichnet
	/// Windows weiterhin (eigenes Fenster), hier wird nur alles drumherum gezeichnet.</summary>
	internal sealed class SearchBoxPainter : NativeWindow
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

		[StructLayout(LayoutKind.Sequential)]
		private struct COMBOBOXINFO
		{
			public int cbSize;
			public RECT rcItem;
			public RECT rcButton;
			public int stateButton;
			public IntPtr hwndCombo;
			public IntPtr hwndItem;
			public IntPtr hwndList;
		}

		private const uint RDW_INVALIDATE = 0x0001, RDW_ALLCHILDREN = 0x0080, RDW_FRAME = 0x0400;

		[DllImport("user32.dll")]
		private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

		[DllImport("user32.dll")]
		private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

		[DllImport("user32.dll")]
		private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

		[DllImport("user32.dll")]
		private static extern bool GetComboBoxInfo(IntPtr hwndCombo, ref COMBOBOXINFO pcbi);

		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		private const int CB_SETITEMHEIGHT = 0x0153, CB_GETITEMHEIGHT = 0x0154;

		[DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
			uint uFlags);

		private readonly ComboBox m_cb;
		private Palette m_p;
		private float m_s = 1f;
		private Color m_back = Color.Empty;
		private bool m_hotButton;
		private bool m_hotClear;

		public SearchBoxPainter(ComboBox cb)
		{
			m_cb = cb;
			cb.HandleCreated += OnHandleCreated;
			cb.HandleDestroyed += OnHandleDestroyed;
			cb.Enter += OnFocusChanged;
			cb.Enter += OnEnterSelectAll;
			cb.Leave += OnFocusChanged;
			cb.DropDown += OnFocusChanged;
			cb.DropDown += OnDropDown;
			cb.DropDownClosed += OnFocusChanged;
			cb.MouseMove += OnMouseMove;
			cb.MouseLeave += OnMouseLeave;
			cb.DrawItem += OnDrawItem;
			cb.EnabledChanged += OnEnabledChanged;
			cb.TextChanged += OnTextChanged;
			// Das Umstellen legt das Feld neu an – dabei hängt sich OnHandleCreated schon ein.
			// Deshalb danach nur einhängen, wenn noch nicht geschehen (sonst Ausnahme).
			cb.DrawMode = DrawMode.OwnerDrawFixed;
			if(cb.IsHandleCreated && (this.Handle == IntPtr.Zero)) AssignHandle(cb.Handle);
			NoTheme();
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		/// <param name="back">Hintergrund rund um das Feld (Farbe der Symbolleiste).</param>
		public void Update(Palette p, float scale, Color back)
		{
			bool changed = ((m_p != p) || (m_s != scale) || (m_back != back));
			m_p = p;
			m_s = scale;
			m_back = back;
			// Höhere Zeilen nur in der Liste – das Suchfeld selbst behält seine Höhe
			int ih = Math.Max(m_cb.Font.Height + D(10), D(28));
			if((m_cb.ItemHeight != ih) && m_cb.IsHandleCreated)
			{
				IntPtr selH = SendMessage(m_cb.Handle, CB_GETITEMHEIGHT, new IntPtr(-1), IntPtr.Zero);
				m_cb.ItemHeight = ih;
				if(selH.ToInt32() > 0) SendMessage(m_cb.Handle, CB_SETITEMHEIGHT, new IntPtr(-1), selH);
			}
			CenterEdit();
			WatchEdit();
			if(changed) m_cb.Invalidate();
		}

		public void Detach()
		{
			m_cb.HandleCreated -= OnHandleCreated;
			m_cb.HandleDestroyed -= OnHandleDestroyed;
			m_cb.Enter -= OnFocusChanged;
			m_cb.Enter -= OnEnterSelectAll;
			m_cb.Leave -= OnFocusChanged;
			m_cb.DropDown -= OnFocusChanged;
			m_cb.DropDown -= OnDropDown;
			m_cb.DropDownClosed -= OnFocusChanged;
			m_cb.MouseMove -= OnMouseMove;
			m_cb.MouseLeave -= OnMouseLeave;
			m_cb.DrawItem -= OnDrawItem;
			m_cb.EnabledChanged -= OnEnabledChanged;
			m_cb.TextChanged -= OnTextChanged;
			m_cb.DrawMode = DrawMode.Normal;
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
			if(m_disBrush != IntPtr.Zero) { DeleteObject(m_disBrush); m_disBrush = IntPtr.Zero; }
			if(m_selTimer != null) { m_selTimer.Dispose(); m_selTimer = null; }
			if(m_editWatch != null) { if(m_editWatch.Handle != IntPtr.Zero) m_editWatch.ReleaseHandle(); m_editWatch = null; }
			if(m_afterSearchTimer != null) { m_afterSearchTimer.Dispose(); m_afterSearchTimer = null; }
			m_cb.Invalidate();
		}

		private void OnHandleCreated(object sender, EventArgs e)
		{
			if(this.Handle == IntPtr.Zero) AssignHandle(m_cb.Handle);
			NoTheme();
			CenterEdit();
			WatchEdit();
		}

		/// <summary>Windows-Design für das Feld abschalten: Mit Design zeichnet Windows bei Fokus-
		/// und Maus-Wechseln eine kurze Übergangs-Animation von Rahmen und Pfeil direkt aufs Feld
		/// (an unserem Zeichnen vorbei) – das Suchfeld flackert dann. Gezeichnet wird alles hier.</summary>
		private void NoTheme()
		{
			if(!m_cb.IsHandleCreated) return;
			try { NativeMethods.ThemeHandle(m_cb.Handle, false, " ", " "); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void OnHandleDestroyed(object sender, EventArgs e)
		{
			if(this.Handle != IntPtr.Zero) ReleaseHandle();
		}

		private void OnFocusChanged(object sender, EventArgs e)
		{
			m_cb.Invalidate();
		}

		/// <summary>Beim Hineinklicken die bisherige Suche komplett markieren (wie in der Windows-
		/// Suche oder einer Adressleiste): Tippen ersetzt sie dann, statt sie zu verlängern.
		/// Nach dem Klick ins Feld setzt Windows den Cursor, deshalb erst danach markieren.</summary>
		private void OnEnterSelectAll(object sender, EventArgs e)
		{
			try
			{
				if(!m_cb.IsHandleCreated || (m_cb.Text.Length == 0)) return;
				// Warten, bis die Maustaste losgelassen ist (höchstens 2 s)
				if(m_selTimer == null)
				{
					m_selTimer = new Timer();
					m_selTimer.Interval = 40;
					m_selTimer.Tick += OnSelTimerTick;
				}
				m_selTicks = 0;
				m_selTimer.Start();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private Timer m_selTimer;
		private int m_selTicks;

		private void OnSelTimerTick(object sender, EventArgs e)
		{
			try
			{
				if(((Control.MouseButtons & MouseButtons.Left) != 0) && (++m_selTicks < 50)) return;
				m_selTimer.Stop();
				if(!m_cb.ContainsFocus || ((Control.MouseButtons & MouseButtons.Left) != 0)) return;
				IntPtr edit = EditHandle();
				if(edit == IntPtr.Zero) return;
				int ss, se;
				GetSel(edit, out ss, out se);
				if(se > ss) return; // schon etwas markiert (von KeePass nach der Suche oder per Maus)
				SendMessage(edit, EM_SETSEL, IntPtr.Zero, new IntPtr(-1));
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Nach Sperren/Entsperren (KeePass schaltet das Feld ab und wieder an) neu zeichnen,
		/// samt Eingabefeld darin.</summary>
		private void OnEnabledChanged(object sender, EventArgs e)
		{
			if(this.Handle == IntPtr.Zero) return;
			// Während KeePass sucht, ist das Feld kurz gesperrt. Eine Markierung würde Windows dann als
			// blauen Block auf grauem Grund zeigen – solange ausblenden und danach wiederherstellen.
			try
			{
				IntPtr edit = EditHandle();
				if(edit != IntPtr.Zero)
				{
					if(!m_cb.Enabled)
					{
						int ss, se;
						GetSel(edit, out ss, out se);
						m_disSelStart = ss;
						m_disSelEnd = se;
						if(se > ss) SendMessage(edit, EM_SETSEL, new IntPtr(se), new IntPtr(se));
					}
					else
					{
						if(m_disSelEnd > m_disSelStart)
							SendMessage(edit, EM_SETSEL, new IntPtr(m_disSelStart), new IntPtr(m_disSelEnd));
						m_disSelStart = m_disSelEnd = 0;
						// Nach der Suche soll der Suchbegriff markiert bleiben, damit die nächste Eingabe
						// ihn ersetzt. KeePass bzw. Windows setzen den Cursor danach aber teils an den
						// Anfang (dann entstand z. B. „winwinsim“) – kurz nachprüfen und wieder markieren.
						m_afterSearchText = m_cb.Text;
						m_afterSearchChecks = 0;
						if(m_afterSearchTimer == null)
						{
							m_afterSearchTimer = new Timer();
							m_afterSearchTimer.Interval = 100;
							m_afterSearchTimer.Tick += OnAfterSearchTick;
						}
						m_afterSearchTimer.Start();
					}
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			RedrawWindow(this.Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_FRAME | RDW_ALLCHILDREN);
		}

		private int m_disSelStart, m_disSelEnd;
		private Timer m_afterSearchTimer;
		private string m_afterSearchText = string.Empty;
		private int m_afterSearchChecks;

		private void OnAfterSearchTick(object sender, EventArgs e)
		{
			try
			{
				// etwa 1 Sekunde lang prüfen, danach nicht mehr eingreifen
				if(++m_afterSearchChecks > 10) { m_afterSearchTimer.Stop(); return; }
				if(!m_cb.Enabled || (m_cb.Text != m_afterSearchText) || (m_cb.Text.Length == 0))
				{
					if(m_cb.Text != m_afterSearchText) m_afterSearchTimer.Stop(); // Nutzer tippt schon
					return;
				}
				if(!m_cb.ContainsFocus || ((Control.MouseButtons & MouseButtons.Left) != 0)) return;
				IntPtr edit = EditHandle();
				if(edit == IntPtr.Zero) return;
				int ss, se;
				GetSel(edit, out ss, out se);
				if(se > ss) return;
				SendMessage(edit, EM_SETSEL, IntPtr.Zero, new IntPtr(-1));
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}
		private IntPtr m_disBrush = IntPtr.Zero;
		private Color m_disBrushColor = Color.Empty;
		private const int WM_CTLCOLORSTATIC = 0x0138;

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateSolidBrush(int crColor);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr hObject);

		[DllImport("gdi32.dll")]
		private static extern int SetBkColor(IntPtr hdc, int crColor);

		[DllImport("gdi32.dll")]
		private static extern int SetTextColor(IntPtr hdc, int crColor);

		/// <summary>Gesperrtes Textfeld (während der Suche) in Feldfarbe statt Windows-Grau zeichnen,
		/// damit es zum übrigen Suchfeld passt; die Schrift wird nur grau.</summary>
		private bool PaintDisabledEdit(ref Message m)
		{
			if((m_p == null) || m_cb.Enabled || (m.WParam == IntPtr.Zero)) return false;
			if((m_disBrush == IntPtr.Zero) || (m_disBrushColor != m_p.Card))
			{
				if(m_disBrush != IntPtr.Zero) DeleteObject(m_disBrush);
				m_disBrush = CreateSolidBrush(ColorTranslator.ToWin32(m_p.Card));
				m_disBrushColor = m_p.Card;
			}
			SetBkColor(m.WParam, ColorTranslator.ToWin32(m_p.Card));
			SetTextColor(m.WParam, ColorTranslator.ToWin32(m_p.Sub));
			m.Result = m_disBrush;
			return true;
		}

		/// <summary>Liste der früheren Suchen: leer gar nicht erst öffnen, sonst abgerundet,
		/// mit Windows-11-Schatten und etwas Abstand unter dem Feld.</summary>
		private void OnDropDown(object sender, EventArgs e)
		{
			if(m_p == null) return;
			try
			{
				m_cb.BeginInvoke(new MethodInvoker(delegate
				{
					try
					{
						if(m_cb.Items.Count == 0)
						{
							m_cb.DroppedDown = false;
							return;
						}
						COMBOBOXINFO ci = new COMBOBOXINFO();
						ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
						if(!GetComboBoxInfo(m_cb.Handle, ref ci) || (ci.hwndList == IntPtr.Zero)) return;

						NativeMethods.RemoveClassDropShadow(ci.hwndList);
						NativeMethods.SetPopupStyle(ci.hwndList, m_p.Border);

						RECT rl, rc;
						if(GetWindowRect(ci.hwndList, out rl) && GetWindowRect(m_cb.Handle, out rc) &&
							(rl.Top >= rc.Bottom - 1)) // nur wenn die Liste unter dem Feld aufgeht
						{
							const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
							SetWindowPos(ci.hwndList, IntPtr.Zero, rc.Left, rc.Bottom + D(4), 0, 0,
								SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
						}
					}
					catch(Exception ex) { FluentThemeExt.Report(ex); }
				}));
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Einträge der Liste: Abstand links, Auswahl als helle abgerundete Fläche mit
		/// Akzentstreifen (wie Windows-11-Listen) statt des blauen Balkens.</summary>
		private void OnDrawItem(object sender, DrawItemEventArgs e)
		{
			if((m_p == null) || (e.Index < 0)) return;
			try
			{
				Graphics g = e.Graphics;
				Rectangle r = e.Bounds;
				using(SolidBrush b = new SolidBrush(m_p.Card)) { g.FillRectangle(b, r); }

				bool sel = ((e.State & DrawItemState.Selected) != 0);
				if(sel)
				{
					Rectangle hr = Rectangle.FromLTRB(r.Left + D(4), r.Top + 1, r.Right - D(4), r.Bottom - 1);
					Gfx.Fill(g, hr, D(4), m_p.CardHover);
					int ph = Math.Max(D(8), Math.Min(D(16), r.Height - D(10)));
					Gfx.Fill(g, new Rectangle(hr.Left, r.Top + (r.Height - ph) / 2, D(3), ph), D(2), m_p.Pill);
				}

				string text = m_cb.GetItemText(m_cb.Items[e.Index]);
				Rectangle tr = Rectangle.FromLTRB(r.Left + D(14), r.Top, r.Right - D(8), r.Bottom);
				TextRenderer.DrawText(g, text, m_cb.Font, tr, m_p.Text, TextFormatFlags.Left |
					TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
					TextFormatFlags.NoPrefix);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			bool hot = ButtonRect().Contains(e.Location);
			bool hotClear = (HasText() && ClearRect().Contains(e.Location));
			if((hot != m_hotButton) || (hotClear != m_hotClear))
			{
				m_hotButton = hot;
				m_hotClear = hotClear;
				m_cb.Invalidate();
			}
			Cursor want = (hotClear ? Cursors.Hand : Cursors.Default);
			if(m_cb.Cursor != want) m_cb.Cursor = want;
		}

		private void OnMouseLeave(object sender, EventArgs e)
		{
			if(!m_hotButton && !m_hotClear) return;
			m_hotButton = false;
			m_hotClear = false;
			m_cb.Cursor = Cursors.Default;
			m_cb.Invalidate();
		}

		/// <summary>Das „x“ zum Leeren nur zeigen, wenn etwas im Feld steht.</summary>
		private void OnTextChanged(object sender, EventArgs e)
		{
			if(!HasText() && m_hotClear) { m_hotClear = false; m_cb.Cursor = Cursors.Default; }
			m_cb.Invalidate();
		}

		private int m_editBaseLeft = -1;   // Lage des Textfelds, wie Windows es setzt
		private int m_editSetLeft = -1;    // Lage, die zuletzt hier gesetzt wurde

		/// <summary>Platz für die Lupe links im Suchfeld.</summary>
		private int SearchIconWidth { get { return D(22); } }

		private bool HasText()
		{
			return !string.IsNullOrEmpty(m_cb.Text);
		}

		/// <summary>Platz für das „x“ zwischen Textfeld und Pfeil.</summary>
		private int ClearWidth { get { return D(26); } }

		private Rectangle ClearRect()
		{
			Rectangle rb = ButtonRect();
			if(rb.IsEmpty) return Rectangle.Empty;
			Rectangle cr = m_cb.ClientRectangle;
			return Rectangle.FromLTRB(rb.Left - this.ClearWidth, cr.Top + D(3), rb.Left, cr.Bottom - D(3));
		}

		/// <summary>Suchfeld leeren und den Cursor wieder hineinsetzen.</summary>
		private void ClearText()
		{
			// erst die Auswahl aus der Liste lösen, sonst setzt das Suchfeld beim nächsten Tippen
			// wieder den alten Begriff ein
			try { m_cb.SelectedIndex = -1; }
			catch(Exception) { }
			m_cb.Text = string.Empty;
			m_hotClear = false;
			m_cb.Cursor = Cursors.Default;
			m_cb.Focus();
			m_cb.Invalidate();
		}

		private Rectangle ButtonRect()
		{
			if(!m_cb.IsHandleCreated) return Rectangle.Empty;
			COMBOBOXINFO ci = new COMBOBOXINFO();
			ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
			if(!GetComboBoxInfo(m_cb.Handle, ref ci)) return Rectangle.Empty;
			return Rectangle.FromLTRB(ci.rcButton.Left, ci.rcButton.Top, ci.rcButton.Right, ci.rcButton.Bottom);
		}

		protected override void WndProc(ref Message m)
		{
			if((m.Msg == WM_CTLCOLORSTATIC) && PaintDisabledEdit(ref m)) return;
			if(m_p != null)
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
			// Senkrechte Lage selbst festlegen: Bei jedem Neuanordnen der Symbolleiste (KeePass tauscht
			// z. B. beim Klick auf einen Eintrag das Speichern-Symbol aus) rechnet WinForms die Höhe
			// minimal anders – das Feld zuckte dann um 1 px hoch und runter
			if((m.Msg == WM_WINDOWPOSCHANGING) && (m.LParam != IntPtr.Zero)) PinVertical(m.LParam);
			// Klick auf das „x“: Feld leeren statt die Liste aufzuklappen
			if(((m.Msg == WM_LBUTTONDOWN) || (m.Msg == WM_LBUTTONDBLCLK)) && (m_p != null) && HasText())
			{
				long lp = m.LParam.ToInt64();
				Point pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
				if(ClearRect().Contains(pt))
				{
					try { ClearText(); }
					catch(Exception ex) { FluentThemeExt.Report(ex); }
					m_swallowUp = true;
					m.Result = IntPtr.Zero;
					return;
				}
			}
			// zum geschluckten Klick auf das „x“ gehört auch das Loslassen – nicht ans Feld weitergeben
			if((m.Msg == WM_LBUTTONUP) && m_swallowUp)
			{
				m_swallowUp = false;
				m.Result = IntPtr.Zero;
				return;
			}
			// Bei Größen-/Lageänderungen markiert Windows den ganzen Text im Feld – beim Tippen würde
			// der nächste Buchstabe dann alles ersetzen. Markierung deshalb sichern und wiederherstellen.
			bool resize = ((m.Msg == WM_SIZE) || (m.Msg == WM_WINDOWPOSCHANGED));
			IntPtr edit = (resize ? EditHandle() : IntPtr.Zero);
			int selStart = 0, selEnd = 0;
			bool keep = ((edit != IntPtr.Zero) && GetSel(edit, out selStart, out selEnd));
			string textBefore = (keep ? GetEditText(edit) : null);
			base.WndProc(ref m);
			// Windows setzt das Textfeld bei jeder Größenänderung oben an – danach wieder mittig setzen
			if((m_p != null) && resize) CenterEdit();
			if(!keep) return;
			// Bei jeder Größenänderung sucht das Windows-Suchfeld in der Liste der letzten Suchen einen
			// Eintrag, der mit dem Getippten beginnt, setzt ihn ein und markiert alles (aus „w“ wurde
			// „winsim“, der nächste Buchstabe ersetzte dann alles). Das hier wieder rückgängig machen.
			string textAfter = GetEditText(edit);
			if(textAfter != textBefore)
			{
				SetEditText(edit, textBefore);
				SendMessage(edit, EM_SETSEL, new IntPtr(selStart), new IntPtr(selEnd));
				return;
			}
			// Nur die Markierung „alles“ rückgängig machen, die Windows bei Größenänderungen setzt
			if(m_cb.ContainsFocus)
			{
				int s2, e2;
				GetSel(edit, out s2, out e2);
				bool allNow = ((s2 == 0) && (e2 >= textBefore.Length) && (textBefore.Length > 0));
				bool allBefore = ((selStart == 0) && (selEnd >= textBefore.Length));
				if(allNow && !allBefore) RestoreSel(edit, selStart, selEnd);
			}
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
		private static extern IntPtr SendMessageStr(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

		private static string GetEditText(IntPtr edit)
		{
			int n = GetWindowTextLength(edit);
			System.Text.StringBuilder sb = new System.Text.StringBuilder(n + 1);
			GetWindowText(edit, sb, n + 1);
			return sb.ToString();
		}

		private static void SetEditText(IntPtr edit, string text)
		{
			SendMessageStr(edit, 0x000C, IntPtr.Zero, text ?? string.Empty); // WM_SETTEXT
		}

		private const int EM_GETSEL = 0x00B0, EM_SETSEL = 0x00B1;

		private IntPtr EditHandle()
		{
			if(!m_cb.IsHandleCreated) return IntPtr.Zero;
			COMBOBOXINFO ci = new COMBOBOXINFO();
			ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
			return (GetComboBoxInfo(m_cb.Handle, ref ci) ? ci.hwndItem : IntPtr.Zero);
		}

		private static bool GetSel(IntPtr edit, out int start, out int end)
		{
			long r = SendMessage(edit, EM_GETSEL, IntPtr.Zero, IntPtr.Zero).ToInt64();
			start = (int)(r & 0xFFFF);
			end = (int)((r >> 16) & 0xFFFF);
			return true;
		}

		private static void RestoreSel(IntPtr edit, int start, int end)
		{
			int s2, e2;
			GetSel(edit, out s2, out e2);
			if((s2 != start) || (e2 != end))
				SendMessage(edit, EM_SETSEL, new IntPtr(start), new IntPtr(end));
		}

		private const int WM_SIZE = 0x0005, WM_WINDOWPOSCHANGED = 0x0047, WM_WINDOWPOSCHANGING = 0x0046;
		private const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONDBLCLK = 0x0203, WM_LBUTTONUP = 0x0202;
		private bool m_swallowUp;

		/// <summary>Beobachtet das Textfeld im Suchfeld, um den Textcursor zu ersetzen.</summary>
		private sealed class EditWatch : NativeWindow
		{
			private readonly SearchBoxPainter m_owner;
			public EditWatch(SearchBoxPainter owner) { m_owner = owner; }

			protected override void WndProc(ref Message m)
			{
				base.WndProc(ref m);
				// Windows legt den Textcursor beim Fokus neu an – danach durch den kürzeren ersetzen
				if((m.Msg == 0x0007) || ((m.Msg == 0x0030) && (GetFocus() == m.HWnd))) // WM_SETFOCUS, WM_SETFONT
					m_owner.ApplyCaret(m.HWnd);
			}
		}

		[DllImport("user32.dll")]
		private static extern IntPtr GetFocus();

		[DllImport("user32.dll")]
		private static extern bool CreateCaret(IntPtr hWnd, IntPtr hBitmap, int nWidth, int nHeight);

		[DllImport("user32.dll")]
		private static extern bool ShowCaret(IntPtr hWnd);

		[DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
		private static extern bool DeleteGdiObject(IntPtr hObject);

		private IntPtr m_caretBmp = IntPtr.Zero;
		private Size m_caretSize = Size.Empty;

		/// <summary>Kürzerer Textcursor: Windows zeichnet ihn so hoch wie die ganze Textzeile (fast so
		/// hoch wie das Feld). Ein Bild mit gleich hohem, aber oben und unten leerem Strich ergibt einen
		/// kürzeren Cursor, der trotzdem mittig zur Schrift sitzt (Windows setzt ihn immer oben an).</summary>
		internal void ApplyCaret(IntPtr edit)
		{
			try
			{
				int line = m_cb.Font.Height;
				int w = Math.Max(SystemInformation.CaretWidth, (int)Math.Round(m_s));
				if(line <= 4) return;
				Size sz = new Size(w, line);
				if((m_caretBmp == IntPtr.Zero) || (m_caretSize != sz))
				{
					if(m_caretBmp != IntPtr.Zero) { DeleteGdiObject(m_caretBmp); m_caretBmp = IntPtr.Zero; }
					using(Bitmap b = new Bitmap(w, line, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
					using(Graphics g = Graphics.FromImage(b))
					{
						// Schwarz = unverändert, Weiß = invertiert (sichtbarer Strich)
						g.Clear(Color.Black);
						int top = (int)Math.Round(line * 0.17);
						int bottom = (int)Math.Round(line * 0.87);
						using(SolidBrush br = new SolidBrush(Color.White))
							g.FillRectangle(br, 0, top, w, Math.Max(1, bottom - top));
						m_caretBmp = b.GetHbitmap();
					}
					m_caretSize = sz;
				}
				if(CreateCaret(edit, m_caretBmp, 0, 0)) ShowCaret(edit);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private EditWatch m_editWatch;

		private void WatchEdit()
		{
			try
			{
				IntPtr edit = EditHandle();
				if(edit == IntPtr.Zero) return;
				if((m_editWatch != null) && (m_editWatch.Handle == edit)) return;
				if((m_editWatch != null) && (m_editWatch.Handle != IntPtr.Zero)) m_editWatch.ReleaseHandle();
				m_editWatch = new EditWatch(this);
				m_editWatch.AssignHandle(edit);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}
		private const int SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002;

		[StructLayout(LayoutKind.Sequential)]
		private struct WINDOWPOS
		{
			public IntPtr hwnd;
			public IntPtr hwndInsertAfter;
			public int x, y, cx, cy;
			public int flags;
		}

		/// <summary>Feld immer senkrecht mittig in der Symbolleiste halten.</summary>
		private void PinVertical(IntPtr lParam)
		{
			try
			{
				ToolStrip ts = (m_cb.Parent as ToolStrip);
				if(ts == null) return;
				WINDOWPOS wp = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
				if((wp.flags & SWP_NOMOVE) != 0) return;
				int h = (((wp.flags & SWP_NOSIZE) != 0) ? m_cb.Height : wp.cy);
				int area = ts.DisplayRectangle.Height;
				if((h <= 0) || (area <= 0) || (h > area)) return;
				int y = ts.DisplayRectangle.Top + (area - h) / 2;
				if(wp.y == y) return;
				wp.y = y;
				Marshal.StructureToPtr(wp, lParam, false);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Textfeld (eigenes Fenster im Suchfeld) senkrecht mittig setzen, damit die Schrift
		/// nicht oben klebt.</summary>
		private void CenterEdit()
		{
			try
			{
				if(!m_cb.IsHandleCreated) return;
				COMBOBOXINFO ci = new COMBOBOXINFO();
				ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
				if(!GetComboBoxInfo(m_cb.Handle, ref ci) || (ci.hwndItem == IntPtr.Zero)) return;
				Rectangle er = EditRect();
				if(er.IsEmpty) return;
				// Höhe auf eine Textzeile begrenzen (das Textfeld schreibt immer oben in sein Fenster)
				int h = Math.Min(er.Height, m_cb.Font.Height + D(2));
				int top = (m_cb.ClientSize.Height - h) / 2;
				// Links Platz für die Lupe (immer, damit der Text beim Tippen nicht springt). Hat Windows
				// das Textfeld selbst neu gesetzt, gilt dessen Lage als Ausgangspunkt.
				if((m_editBaseLeft < 0) || (er.Left != m_editSetLeft)) m_editBaseLeft = er.Left;
				int left = m_editBaseLeft + this.SearchIconWidth;
				// rechts Platz für das „x“ lassen (Windows zieht das Textfeld bis an den Pfeil)
				int w = er.Right - left;
				Rectangle rb = ButtonRect();
				if(!rb.IsEmpty) w = Math.Max(D(40), rb.Left - this.ClearWidth - left);
				m_editSetLeft = left;
				if((Math.Abs(top - er.Top) < 1) && (h == er.Height) && (w == er.Width) && (left == er.Left))
				{
					m_cb.Invalidate();
					return;
				}
				const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
				int ss, se;
				GetSel(ci.hwndItem, out ss, out se);
				SetWindowPos(ci.hwndItem, IntPtr.Zero, left, top, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
				RestoreSel(ci.hwndItem, ss, se);
				m_cb.Invalidate();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void Paint(IntPtr hdc)
		{
			Rectangle cr = m_cb.ClientRectangle;
			if((cr.Width <= 0) || (cr.Height <= 0)) return;

			using(BufferedGraphics bg = BufferedGraphicsManager.Current.Allocate(hdc, cr))
			{
				Graphics g = bg.Graphics;
				g.Clear(m_back.IsEmpty ? m_p.Bg : m_back);

				bool focused = (m_cb.ContainsFocus || m_cb.DroppedDown);
				int rad = D(4);
				Rectangle box = new Rectangle(cr.Left, cr.Top, cr.Width, cr.Height);
				Gfx.Fill(g, box, rad, m_p.Card);
				Gfx.Stroke(g, box, rad, focused ? Palette.Blend(m_p.Border, m_p.Text, 0.15) : m_p.Border);

				// Pfeil-Bereich: schlichter Chevron, beim Überfahren leicht hinterlegt
				Rectangle rb = ButtonRect();
				if(!rb.IsEmpty)
				{
					Rectangle hb = Rectangle.FromLTRB(rb.Left + D(2), cr.Top + D(3), cr.Right - D(3), cr.Bottom - D(3));
					// wie das Auswahlfeld im Monitor Dimmer: nur ein feiner Pfeil, ohne Kasten
					Color cc = (m_hotButton ? m_p.Text : m_p.Sub);
					Gfx.Chevron(g, hb.Left + hb.Width / 2f, hb.Top + hb.Height / 2f, 7f * m_s, m_cb.DroppedDown,
						cc, Math.Max(1f, 1.1f * m_s));
				}

				// Lupe am Anfang des Feldes
				if(m_editBaseLeft >= 0)
				{
					float r = 4.2f * m_s;
					float cx = m_editBaseLeft + D(10), cy = cr.Top + cr.Height / 2f - 0.8f * m_s;
					System.Drawing.Drawing2D.SmoothingMode sm0 = g.SmoothingMode;
					g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
					using(Pen pen = new Pen(m_p.Sub, Math.Max(1f, 1.3f * m_s)))
					{
						pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
						pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
						g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
						float d = r * 0.7071f;
						g.DrawLine(pen, cx + d, cy + d, cx + d + 3.6f * m_s, cy + d + 3.6f * m_s);
					}
					g.SmoothingMode = sm0;
				}

				// „x“ zum Leeren, nur wenn etwas im Feld steht
				if(HasText())
				{
					Rectangle xr = ClearRect();
					if(!xr.IsEmpty)
					{
						if(m_hotClear)
						{
							int hs = Math.Min(xr.Width, xr.Height) - D(2);
							Rectangle hb = new Rectangle(xr.Left + (xr.Width - hs) / 2, xr.Top + (xr.Height - hs) / 2, hs, hs);
							Gfx.Fill(g, hb, D(4), m_p.CardHover);
						}
						float cx = xr.Left + xr.Width / 2f, cy = xr.Top + xr.Height / 2f, u = 4f * m_s;
						System.Drawing.Drawing2D.SmoothingMode sm = g.SmoothingMode;
						g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
						using(Pen pen = new Pen(m_hotClear ? m_p.Text : m_p.Sub, Math.Max(1f, 1.2f * m_s)))
						{
							pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
							pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
							g.DrawLine(pen, cx - u, cy - u, cx + u, cy + u);
							g.DrawLine(pen, cx - u, cy + u, cx + u, cy - u);
						}
						g.SmoothingMode = sm;
					}
				}

				// Fokus: Akzentlinie an der Unterkante (wie Windows-11-Eingabefelder)
				// 2 px hoch und ganz unten, damit das Textfeld (eigenes Fenster) sie nicht teilweise
				// überdeckt; auf die abgerundete Form zugeschnitten wie in Windows 11
				if(focused)
				{
					const int lh = 2;
					System.Drawing.Drawing2D.GraphicsState st = g.Save();
					using(System.Drawing.Drawing2D.GraphicsPath gp = Gfx.Round(
						new Rectangle(box.X, box.Y, box.Width - 1, box.Height - 1), rad))
					{
						g.SetClip(gp);
						g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
						using(SolidBrush b = new SolidBrush(m_p.Pill))
						{
							g.FillRectangle(b, box.Left, box.Bottom - lh - 1, box.Width, lh + 1);
						}
					}
					g.Restore(st);
				}

				// Den Bereich des Textfelds (eigenes Fenster) nicht übermalen – sonst zeichnet Windows
				// es gleich danach neu darüber und das Feld flackert
				Rectangle er = EditRect();
				using(Graphics tg = Graphics.FromHdc(hdc))
				{
					if(!er.IsEmpty) tg.ExcludeClip(er);
					bg.Render(tg);
				}
			}
		}

		private Rectangle EditRect()
		{
			if(!m_cb.IsHandleCreated) return Rectangle.Empty;
			COMBOBOXINFO ci = new COMBOBOXINFO();
			ci.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
			if(!GetComboBoxInfo(m_cb.Handle, ref ci) || (ci.hwndItem == IntPtr.Zero)) return Rectangle.Empty;
			RECT wr;
			if(!GetWindowRect(ci.hwndItem, out wr)) return Rectangle.Empty;
			return m_cb.RectangleToClient(Rectangle.FromLTRB(wr.Left, wr.Top, wr.Right, wr.Bottom));
		}
	}
}
