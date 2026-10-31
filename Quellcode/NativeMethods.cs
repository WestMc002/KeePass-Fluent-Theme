using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using Microsoft.Win32;

namespace FluentTheme
{
	internal static class NativeMethods
	{
		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute,
			ref int pvAttribute, int cbAttribute);

		[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
		private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName,
			string pszSubIdList);

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam,
			IntPtr lParam);

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		private static extern IntPtr SendMessageHdItem(IntPtr hWnd, int msg, IntPtr wParam,
			ref HDITEM lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("kernel32.dll", EntryPoint = "GetProcAddress")]
		private static extern IntPtr GetProcAddressOrdinal(IntPtr hModule, IntPtr ordinal);

		[StructLayout(LayoutKind.Sequential)]
		private struct HDITEM
		{
			public uint mask;
			public int cxy;
			public IntPtr pszText;
			public IntPtr hbm;
			public int cchTextMax;
			public int fmt;
			public IntPtr lParam;
			public int iImage;
			public int iOrder;
			public uint type;
			public IntPtr pvFilter;
			public uint state;
		}

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate int SetPreferredAppModeFn(int mode);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		[return: MarshalAs(UnmanagedType.I1)]
		private delegate bool AllowDarkModeForWindowFn(IntPtr hWnd,
			[MarshalAs(UnmanagedType.I1)] bool allow);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate void FlushMenuThemesFn();

		private const int LVM_GETHEADER = 0x101F;
		private const int LVM_SETEXTENDEDLISTVIEWSTYLE = 0x1036;
		private const int LVS_EX_DOUBLEBUFFER = 0x10000;
		private const int TVM_SETEXTENDEDSTYLE = 0x112C;
		private const int TVS_EX_DOUBLEBUFFER = 0x0004;
		private const int HDM_GETITEMW = 0x120B;
		private const uint HDI_FORMAT = 0x0004;
		private const int HDF_SORTDOWN = 0x0200;
		private const int HDF_SORTUP = 0x0400;

		private static bool s_uxInit;
		private static SetPreferredAppModeFn s_setMode;
		private static AllowDarkModeForWindowFn s_allowWin;
		private static FlushMenuThemesFn s_flush;
		private static int s_build = -1;

		internal static int WindowsBuild()
		{
			if(s_build >= 0) return s_build;
			s_build = 0;
			try
			{
				using(RegistryKey k = Registry.LocalMachine.OpenSubKey(
					@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
				{
					if(k != null)
					{
						object o = k.GetValue("CurrentBuildNumber");
						int b;
						if((o != null) && int.TryParse(o.ToString(), out b)) s_build = b;
					}
				}
			}
			catch(Exception) { }
			return s_build;
		}

		private static void InitUx()
		{
			if(s_uxInit) return;
			s_uxInit = true;
			try
			{
				if(WindowsBuild() < 17763) return; // Windows 10 1809
				IntPtr h = LoadLibrary("uxtheme.dll");
				if(h == IntPtr.Zero) return;

				IntPtr p = GetProcAddressOrdinal(h, new IntPtr(135));
				if(p != IntPtr.Zero)
					s_setMode = (SetPreferredAppModeFn)Marshal.GetDelegateForFunctionPointer(
						p, typeof(SetPreferredAppModeFn));

				p = GetProcAddressOrdinal(h, new IntPtr(133));
				if(p != IntPtr.Zero)
					s_allowWin = (AllowDarkModeForWindowFn)Marshal.GetDelegateForFunctionPointer(
						p, typeof(AllowDarkModeForWindowFn));

				p = GetProcAddressOrdinal(h, new IntPtr(136));
				if(p != IntPtr.Zero)
					s_flush = (FlushMenuThemesFn)Marshal.GetDelegateForFunctionPointer(
						p, typeof(FlushMenuThemesFn));
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Dunkle Scrollleisten und Systemmenüs für den ganzen Prozess.</summary>
		internal static void SetAppDark(bool dark)
		{
			InitUx();
			try
			{
				if(s_setMode != null)
				{
					// Ab Build 18362: 2 = ForceDark, 3 = ForceLight.
					// Build 17763: Ordinal 135 = AllowDarkModeForApp(bool).
					if(WindowsBuild() >= 18362) s_setMode(dark ? 2 : 3);
					else s_setMode(dark ? 1 : 0);
				}
				if(s_flush != null) s_flush();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		internal static void ThemeControl(Control c, bool dark, string darkTheme, string lightTheme)
		{
			if((c == null) || !c.IsHandleCreated) return;
			ThemeHandle(c.Handle, dark, darkTheme, lightTheme);
		}

		internal static void ThemeHandle(IntPtr h, bool dark, string darkTheme, string lightTheme)
		{
			if(h == IntPtr.Zero) return;
			InitUx();
			try
			{
				if(s_allowWin != null) s_allowWin(h, dark);
				SetWindowTheme(h, dark ? darkTheme : lightTheme, null);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private static int ToColorRef(Color c)
		{
			return (c.B << 16) | (c.G << 8) | c.R;
		}

		internal static void SetTitleBar(IntPtr hwnd, bool dark, Color caption, Color text,
			Color border)
		{
			if(hwnd == IntPtr.Zero) return;
			try
			{
				int v = (dark ? 1 : 0);
				if(DwmSetWindowAttribute(hwnd, 20, ref v, 4) != 0)
					DwmSetWindowAttribute(hwnd, 19, ref v, 4);

				if(WindowsBuild() >= 22000) // Windows 11
				{
					int cc = ToColorRef(caption);
					DwmSetWindowAttribute(hwnd, 35, ref cc, 4); // DWMWA_CAPTION_COLOR
					int tc = ToColorRef(text);
					DwmSetWindowAttribute(hwnd, 36, ref tc, 4); // DWMWA_TEXT_COLOR
					int bc = ToColorRef(border);
					DwmSetWindowAttribute(hwnd, 34, ref bc, 4); // DWMWA_BORDER_COLOR
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		internal static bool IsWin11
		{
			get { return (WindowsBuild() >= 22000); }
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass,
			string lpszWindow);

		/// <summary>Innenabstand links im Textfeld einer ComboBox (z. B. Schnellsuche).</summary>
		internal static void SetComboTextMargin(ComboBox cb, int left)
		{
			if((cb == null) || !cb.IsHandleCreated) return;
			try
			{
				IntPtr edit = FindWindowEx(cb.Handle, IntPtr.Zero, "Edit", null);
				if(edit == IntPtr.Zero) return;
				const int EM_SETMARGINS = 0x00D3, EC_LEFTMARGIN = 0x0001;
				SendMessage(edit, EM_SETMARGINS, new IntPtr(EC_LEFTMARGIN), new IntPtr(left & 0xFFFF));
				cb.Invalidate(true);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		[DllImport("user32.dll", EntryPoint = "GetClassLong")]
		private static extern uint GetClassLong32(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", EntryPoint = "SetClassLong")]
		private static extern uint SetClassLong32(IntPtr hWnd, int nIndex, uint dwNewLong);

		[DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
		private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
		private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

		private const int GCL_STYLE = -26;
		private const long CS_DROPSHADOW = 0x00020000;

		/// <summary>Entfernt den alten (eckigen) Schlagschatten aus der Fensterklasse, ohne das
		/// Fenster neu zu erzeugen. Wirkt ab dem nächsten Anzeigen.</summary>
		internal static void RemoveClassDropShadow(IntPtr hwnd)
		{
			if(hwnd == IntPtr.Zero) return;
			try
			{
				if(IntPtr.Size == 8)
				{
					long st = GetClassLongPtr64(hwnd, GCL_STYLE).ToInt64();
					if((st & CS_DROPSHADOW) != 0) SetClassLongPtr64(hwnd, GCL_STYLE, new IntPtr(st & ~CS_DROPSHADOW));
				}
				else
				{
					uint st = GetClassLong32(hwnd, GCL_STYLE);
					if((st & (uint)CS_DROPSHADOW) != 0) SetClassLong32(hwnd, GCL_STYLE, st & ~(uint)CS_DROPSHADOW);
				}
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Abgerundete Ecken und Rahmenfarbe für Popup-Menüs (Windows 11).</summary>
		internal static void SetPopupStyle(IntPtr hwnd, Color border)
		{
			if((hwnd == IntPtr.Zero) || !IsWin11) return;
			try
			{
				int pref = 3; // DWMWCP_ROUNDSMALL
				DwmSetWindowAttribute(hwnd, 33, ref pref, 4); // DWMWA_WINDOW_CORNER_PREFERENCE
				int bc = ToColorRef(border);
				DwmSetWindowAttribute(hwnd, 34, ref bc, 4); // DWMWA_BORDER_COLOR
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		internal static IntPtr GetListHeader(ListView lv)
		{
			if((lv == null) || !lv.IsHandleCreated) return IntPtr.Zero;
			return SendMessage(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
		}

		/// <summary>1 = aufsteigend, -1 = absteigend, 0 = nicht sortiert.</summary>
		internal static int GetHeaderSort(IntPtr hdr, int index)
		{
			if(hdr == IntPtr.Zero) return 0;
			try
			{
				HDITEM hi = new HDITEM();
				hi.mask = HDI_FORMAT;
				SendMessageHdItem(hdr, HDM_GETITEMW, new IntPtr(index), ref hi);
				if((hi.fmt & HDF_SORTUP) != 0) return 1;
				if((hi.fmt & HDF_SORTDOWN) != 0) return -1;
			}
			catch(Exception) { }
			return 0;
		}

		internal static void ListDoubleBuffer(ListView lv)
		{
			if((lv == null) || !lv.IsHandleCreated) return;
			SendMessage(lv.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, new IntPtr(LVS_EX_DOUBLEBUFFER),
				new IntPtr(LVS_EX_DOUBLEBUFFER));
		}

		internal static void TreeDoubleBuffer(TreeView tv)
		{
			if((tv == null) || !tv.IsHandleCreated) return;
			SendMessage(tv.Handle, TVM_SETEXTENDEDSTYLE, new IntPtr(TVS_EX_DOUBLEBUFFER),
				new IntPtr(TVS_EX_DOUBLEBUFFER));
		}
	}
}