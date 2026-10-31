using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Ersetzt die breite graue Windows-Scrollleiste von Gruppenbaum und Eintragsliste
	/// durch eine schmale, dezente Leiste im Windows-11-Stil. Die echte Scrollleiste wird
	/// rechts aus dem sichtbaren Bereich geschoben (abgeschnitten).</summary>
	internal sealed class OverlayScroll : IDisposable
	{
		[StructLayout(LayoutKind.Sequential)]
		internal struct SCROLLINFO
		{
			public uint cbSize;
			public uint fMask;
			public int nMin;
			public int nMax;
			public uint nPage;
			public int nPos;
			public int nTrackPos;
		}

		[DllImport("user32.dll")]
		private static extern bool GetScrollInfo(IntPtr hwnd, int fnBar, ref SCROLLINFO lpsi);

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		private const int SB_VERT = 1;
		private const uint SIF_ALL = 0x17;
		private const int WM_VSCROLL = 0x0115;
		private const int SB_PAGEUP = 2;
		private const int SB_PAGEDOWN = 3;
		private const int SB_THUMBPOSITION = 4;
		private const int SB_ENDSCROLL = 8;
		private const int LVM_SCROLL = 0x1014;

		private sealed class Hook : NativeWindow
		{
			private readonly OverlayScroll m_owner;

			public Hook(OverlayScroll owner)
			{
				m_owner = owner;
			}

			protected override void WndProc(ref Message m)
			{
				base.WndProc(ref m);
				switch(m.Msg)
				{
					case 0x0005: // WM_SIZE
					case 0x000F: // WM_PAINT
					case 0x0100: // WM_KEYDOWN
					case 0x0115: // WM_VSCROLL
					case 0x020A: // WM_MOUSEWHEEL
						m_owner.RequestSync();
						break;
				}
			}
		}

		private readonly Control m_target;
		private readonly Control m_origParent;
		private readonly DockStyle m_origDock;
		private readonly Panel m_wrap;
		private readonly ThinScrollBar m_bar;
		private readonly Hook m_hook;
		private bool m_sbVisible;
		private bool m_syncPending;
		private SCROLLINFO m_info;
		private float m_s = 1f;

		public OverlayScroll(Control target)
		{
			m_target = target;
			m_origParent = target.Parent;
			m_origDock = target.Dock;

			int idx = m_origParent.Controls.GetChildIndex(target);
			m_origParent.SuspendLayout();
			m_wrap = new Panel();
			m_wrap.Dock = DockStyle.Fill;
			m_wrap.Margin = Padding.Empty;
			m_wrap.Padding = Padding.Empty;
			m_wrap.Name = "FluentThemeScrollWrap";
			m_origParent.Controls.Add(m_wrap);
			m_origParent.Controls.SetChildIndex(m_wrap, idx);

			target.Dock = DockStyle.None;
			target.Parent = m_wrap;

			m_bar = new ThinScrollBar(this);
			m_wrap.Controls.Add(m_bar);
			m_bar.BringToFront();
			m_origParent.ResumeLayout(true);

			m_wrap.Resize += OnWrapResize;
			m_hook = new Hook(this);
			if(target.IsHandleCreated) m_hook.AssignHandle(target.Handle);
			target.HandleCreated += OnTargetHandleCreated;
			target.HandleDestroyed += OnTargetHandleDestroyed;

			DoLayout();
		}

		public void Dispose()
		{
			try
			{
				m_target.HandleCreated -= OnTargetHandleCreated;
				m_target.HandleDestroyed -= OnTargetHandleDestroyed;
				if(m_hook.Handle != IntPtr.Zero) m_hook.ReleaseHandle();
				m_wrap.Resize -= OnWrapResize;

				m_origParent.SuspendLayout();
				int idx = m_origParent.Controls.GetChildIndex(m_wrap);
				m_target.Parent = m_origParent;
				m_target.Dock = m_origDock;
				m_origParent.Controls.SetChildIndex(m_target, idx);
				m_origParent.Controls.Remove(m_wrap);
				m_origParent.ResumeLayout(true);
				m_wrap.Dispose();
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		internal float Scale
		{
			get { return m_s; }
		}

		internal SCROLLINFO Info
		{
			get { return m_info; }
		}

		public void Update(Palette p, float scale)
		{
			m_s = scale;
			m_wrap.BackColor = p.Card;
			m_bar.SetPalette(p, scale);
			DoLayout();
			Sync();
		}

		private void OnTargetHandleCreated(object sender, EventArgs e)
		{
			if(m_hook.Handle == IntPtr.Zero) m_hook.AssignHandle(m_target.Handle);
			RequestSync();
		}

		private void OnTargetHandleDestroyed(object sender, EventArgs e)
		{
			if(m_hook.Handle != IntPtr.Zero) m_hook.ReleaseHandle();
		}

		private void OnWrapResize(object sender, EventArgs e)
		{
			DoLayout();
		}

		private void DoLayout()
		{
			Size cs = m_wrap.ClientSize;
			int sbw = (m_sbVisible ? SystemInformation.VerticalScrollBarWidth : 0);
			m_target.SetBounds(0, 0, cs.Width + sbw, cs.Height);
			int bw = (int)Math.Round(10 * m_s);
			m_bar.SetBounds(Math.Max(0, cs.Width - bw), 0, bw, cs.Height);
			m_bar.Visible = m_sbVisible;
		}

		internal void RequestSync()
		{
			if(m_syncPending || !m_wrap.IsHandleCreated) return;
			m_syncPending = true;
			m_wrap.BeginInvoke(new MethodInvoker(delegate
			{
				m_syncPending = false;
				Sync();
			}));
		}

		public void Sync()
		{
			if(!m_target.IsHandleCreated) return;
			SCROLLINFO si = new SCROLLINFO();
			si.cbSize = (uint)Marshal.SizeOf(typeof(SCROLLINFO));
			si.fMask = SIF_ALL;
			if(!GetScrollInfo(m_target.Handle, SB_VERT, ref si))
			{
				si.nMin = 0;
				si.nMax = 0;
				si.nPage = 0;
				si.nPos = 0;
			}

			bool vis = ((si.nPage > 0) && ((si.nMax - si.nMin + 1) > (int)si.nPage));
			bool changed = ((si.nPos != m_info.nPos) || (si.nMax != m_info.nMax) || (si.nPage != m_info.nPage) ||
				(si.nMin != m_info.nMin));
			m_info = si;
			if(vis != m_sbVisible)
			{
				m_sbVisible = vis;
				DoLayout();
			}
			if(changed) m_bar.Invalidate();
		}

		internal void Page(bool down)
		{
			if(!m_target.IsHandleCreated) return;
			SendMessage(m_target.Handle, WM_VSCROLL, new IntPtr(down ? SB_PAGEDOWN : SB_PAGEUP), IntPtr.Zero);
			Sync();
		}

		internal void ScrollTo(int pos)
		{
			if(!m_target.IsHandleCreated) return;
			int maxPos = m_info.nMax - (int)m_info.nPage + 1;
			pos = Math.Max(m_info.nMin, Math.Min(maxPos, pos));
			if(pos == m_info.nPos) return;

			ListView lv = (m_target as ListView);
			if(lv != null)
			{
				int rowH = ((lv.Items.Count > 0) ? lv.GetItemRect(0).Height : 16);
				int dy = (pos - m_info.nPos) * rowH;
				SendMessage(lv.Handle, LVM_SCROLL, IntPtr.Zero, new IntPtr(dy));
			}
			else
			{
				SendMessage(m_target.Handle, WM_VSCROLL, new IntPtr((pos << 16) | SB_THUMBPOSITION), IntPtr.Zero);
				SendMessage(m_target.Handle, WM_VSCROLL, new IntPtr(SB_ENDSCROLL), IntPtr.Zero);
			}
			Sync();
		}
	}

	/// <summary>Schmaler Scroll-Daumen ohne Spur, wird beim Überfahren breiter.</summary>
	internal sealed class ThinScrollBar : Control
	{
		private readonly OverlayScroll m_owner;
		private Palette m_p;
		private float m_s = 1f;
		private bool m_hot;
		private bool m_drag;
		private int m_dragStartY;
		private int m_dragStartPos;

		public ThinScrollBar(OverlayScroll owner)
		{
			m_owner = owner;
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
				ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			this.Visible = false;
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		public void SetPalette(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
			this.BackColor = p.Card;
			Invalidate();
		}

		private bool GetGeometry(out Rectangle track, out Rectangle thumb, out int range)
		{
			OverlayScroll.SCROLLINFO si = m_owner.Info;
			int total = si.nMax - si.nMin + 1;
			range = total - (int)si.nPage;
			track = new Rectangle(0, D(4), this.Width, Math.Max(0, this.Height - D(8)));
			thumb = Rectangle.Empty;
			if((total <= 0) || (range <= 0) || (track.Height <= 0)) return false;

			int th = Math.Max(D(28), (int)((long)track.Height * si.nPage / total));
			th = Math.Min(th, track.Height);
			int ty = track.Top + (int)((long)(track.Height - th) * (si.nPos - si.nMin) / range);
			int w = ((m_hot || m_drag) ? D(6) : D(3));
			thumb = new Rectangle(this.Width - w - D(2), ty, w, th);
			return true;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if(m_p == null) return;
			Graphics g = e.Graphics;
			g.Clear(m_p.Card);
			Rectangle track, thumb;
			int range;
			if(!GetGeometry(out track, out thumb, out range)) return;
			Color c = Palette.Blend(m_p.Card, m_p.Text, (m_hot || m_drag) ? 0.42 : 0.22);
			Gfx.Fill(g, thumb, thumb.Width / 2, c);
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			base.OnMouseEnter(e);
			m_hot = true;
			Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			m_hot = false;
			Invalidate();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if(e.Button != MouseButtons.Left) return;
			Rectangle track, thumb;
			int range;
			if(!GetGeometry(out track, out thumb, out range)) return;

			Rectangle hit = new Rectangle(0, thumb.Top, this.Width, thumb.Height);
			if(hit.Contains(e.Location))
			{
				m_drag = true;
				m_dragStartY = e.Y;
				m_dragStartPos = m_owner.Info.nPos;
				this.Capture = true;
			}
			else m_owner.Page(e.Y > thumb.Bottom);
			Invalidate();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if(!m_drag) return;
			Rectangle track, thumb;
			int range;
			if(!GetGeometry(out track, out thumb, out range)) return;
			int free = Math.Max(1, track.Height - thumb.Height);
			int pos = m_dragStartPos + (int)Math.Round((double)(e.Y - m_dragStartY) * range / free);
			m_owner.ScrollTo(pos);
			Invalidate();
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			m_drag = false;
			this.Capture = false;
			Invalidate();
		}
	}
}