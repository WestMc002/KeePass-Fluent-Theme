// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Listen in Dialogen im dunklen Design: Windows zeichnet Spaltenköpfe und
	/// Gruppenüberschriften zwar auf dunklem Grund, die Schrift aber weiterhin schwarz bzw. dunkelblau.
	/// Bei den Spaltenköpfen wird nur die Schriftfarbe vorgegeben, die Gruppenüberschriften werden
	/// selbst gezeichnet (grau, halbfett – wie in der Eintragsliste). Im hellen Design und bei Listen,
	/// die sich selbst zeichnen (OwnerDraw), bleibt alles bei Windows.</summary>
	internal sealed class HeaderTextFix : NativeWindow
	{
		private const int WM_NOTIFY = 0x004E;
		private const int WM_REFLECT_NOTIFY = 0x204E; // von WinForms an die Liste zurückgereichtes WM_NOTIFY
		private const int NM_CUSTOMDRAW = -12;
		private const int CDDS_PREPAINT = 0x00000001;
		private const int CDDS_ITEMPREPAINT = 0x00010001;
		private const int CDRF_DODEFAULT = 0x00000000;
		private const int CDRF_SKIPDEFAULT = 0x00000004;
		private const int CDRF_NOTIFYITEMDRAW = 0x00000020;
		private const int LVCDI_GROUP = 0x0001;
		private const int LVM_GETGROUPRECT = 0x1062;
		private const int LVGGR_HEADER = 1;

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct NMHDR
		{
			public IntPtr hwndFrom;
			public UIntPtr idFrom;
			public int code;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct NMCUSTOMDRAW
		{
			public NMHDR hdr;
			public int dwDrawStage;
			public IntPtr hdc;
			public RECT rc;
			public UIntPtr dwItemSpec;
			public uint uItemState;
			public IntPtr lItemlParam;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct NMLVCUSTOMDRAW
		{
			public NMCUSTOMDRAW nmcd;
			public int clrText;
			public int clrTextBk;
			public int iSubItem;
			public int dwItemType;
		}

		[DllImport("gdi32.dll")]
		private static extern int SetTextColor(IntPtr hdc, int crColor);

		[DllImport("user32.dll")]
		private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		private static extern IntPtr SendRect(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

		private static PropertyInfo s_groupId;

		private readonly ListView m_lv;
		private bool m_on;
		private Color m_text = Color.White;
		private Color m_sub = Color.Silver;
		private Color m_back = Color.Black;

		public HeaderTextFix(ListView lv)
		{
			m_lv = lv;
			m_lv.HandleCreated += OnHandleCreated;
			if(m_lv.IsHandleCreated) AssignHandle(m_lv.Handle);
		}

		/// <summary>Farben setzen; aktiv nur im dunklen Design.</summary>
		public void Update(Color text, Color sub, Color back, bool dark)
		{
			m_text = text;
			m_sub = sub;
			m_back = back;
			m_on = dark;
			try
			{
				IntPtr hdr = NativeMethods.GetListHeader(m_lv);
				if(hdr != IntPtr.Zero) InvalidateRect(hdr, IntPtr.Zero, true);
				if(m_lv.IsHandleCreated) m_lv.Invalidate();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		public void Detach()
		{
			m_on = false;
			m_lv.HandleCreated -= OnHandleCreated;
			try { if(this.Handle != IntPtr.Zero) ReleaseHandle(); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void OnHandleCreated(object sender, EventArgs e)
		{
			try
			{
				if(this.Handle == m_lv.Handle) return;
				if(this.Handle != IntPtr.Zero) ReleaseHandle();
				AssignHandle(m_lv.Handle);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		protected override void WndProc(ref Message m)
		{
			if(m_on && (m.LParam != IntPtr.Zero) && ((m.Msg == WM_NOTIFY) || (m.Msg == WM_REFLECT_NOTIFY)))
			{
				try
				{
					if(!m_lv.OwnerDraw)
					{
						if((m.Msg == WM_NOTIFY) && HeaderText(ref m)) return;
						if((m.Msg == WM_REFLECT_NOTIFY) && GroupHeader(ref m)) return;
					}
				}
				catch(Exception ex) { FluentThemeExt.Report(ex); }
			}
			base.WndProc(ref m);
		}

		/// <summary>Spaltenkopf: nur die Schriftfarbe vorgeben, den Rest zeichnet Windows.</summary>
		private bool HeaderText(ref Message m)
		{
			NMHDR nh = (NMHDR)Marshal.PtrToStructure(m.LParam, typeof(NMHDR));
			if((nh.code != NM_CUSTOMDRAW) || (nh.hwndFrom == IntPtr.Zero)) return false;
			if(nh.hwndFrom != NativeMethods.GetListHeader(m_lv)) return false;

			NMCUSTOMDRAW cd = (NMCUSTOMDRAW)Marshal.PtrToStructure(m.LParam, typeof(NMCUSTOMDRAW));
			if(cd.dwDrawStage == CDDS_PREPAINT)
			{
				m.Result = new IntPtr(CDRF_NOTIFYITEMDRAW);
				return true;
			}
			if(cd.dwDrawStage == CDDS_ITEMPREPAINT)
			{
				SetTextColor(cd.hdc, (m_text.B << 16) | (m_text.G << 8) | m_text.R);
				m.Result = new IntPtr(CDRF_DODEFAULT);
				return true;
			}
			return false;
		}

		/// <summary>Gruppenüberschrift (z. B. in den Optionen) selbst zeichnen statt im Windows-Blau.</summary>
		private bool GroupHeader(ref Message m)
		{
			NMHDR nh = (NMHDR)Marshal.PtrToStructure(m.LParam, typeof(NMHDR));
			if(nh.code != NM_CUSTOMDRAW) return false;
			NMLVCUSTOMDRAW cd = (NMLVCUSTOMDRAW)Marshal.PtrToStructure(m.LParam, typeof(NMLVCUSTOMDRAW));
			if((cd.nmcd.dwDrawStage != CDDS_PREPAINT) || (cd.dwItemType != LVCDI_GROUP)) return false;

			int id = (int)cd.nmcd.dwItemSpec.ToUInt32();
			RECT r = new RECT();
			r.Top = LVGGR_HEADER;
			if(SendRect(m_lv.Handle, LVM_GETGROUPRECT, new IntPtr(id), ref r) == IntPtr.Zero) return false;
			Rectangle rc = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
			if((rc.Width <= 0) || (rc.Height <= 0) || (cd.nmcd.hdc == IntPtr.Zero)) return false;

			string text = GroupText(id);
			using(Graphics g = Graphics.FromHdc(cd.nmcd.hdc))
			{
				using(SolidBrush b = new SolidBrush(m_back)) { g.FillRectangle(b, rc); }
				Font f;
				try { f = new Font("Segoe UI Semibold", m_lv.Font.SizeInPoints); }
				catch(ArgumentException) { f = new Font(m_lv.Font, FontStyle.Bold); }
				using(f)
				{
					int pad = (int)Math.Round(6 * g.DpiX / 96f);
					Rectangle tr = Rectangle.FromLTRB(rc.Left + pad, rc.Top, rc.Right - pad, rc.Bottom);
					TextRenderer.DrawText(g, text, f, tr, m_sub, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
						TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
				}
			}
			m.Result = new IntPtr(CDRF_SKIPDEFAULT);
			return true;
		}

		/// <summary>Text der Gruppe mit der Windows-ID (WinForms führt die ID intern).</summary>
		private string GroupText(int id)
		{
			if(s_groupId == null)
				s_groupId = typeof(ListViewGroup).GetProperty("ID", BindingFlags.Instance | BindingFlags.NonPublic);
			foreach(ListViewGroup grp in m_lv.Groups)
			{
				if((s_groupId != null) && (Convert.ToInt32(s_groupId.GetValue(grp, null)) == id)) return grp.Header;
			}
			return string.Empty;
		}
	}
}
