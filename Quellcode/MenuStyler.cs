using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FluentTheme
{
	/// <summary>Windows-11-Menüs: mehr Innenabstand, höhere Einträge und abgerundete Ecken.
	/// Hängt sich an alle Dropdowns (Menüleiste, Symbolleiste, Kontextmenüs).</summary>
	internal sealed class MenuStyler
	{
		private readonly HashSet<ToolStripDropDown> m_hooked = new HashSet<ToolStripDropDown>();
		private Palette m_p;
		private Font m_dropFont;
		private readonly HashSet<ToolStripSeparator> m_hiddenSeps = new HashSet<ToolStripSeparator>();
		private readonly HashSet<ToolStripItem> m_edgeItems = new HashSet<ToolStripItem>();
		private float m_s = 1f;

		// „Weitere Befehle“ im Kontextmenü öffnet das Menü „Eintrag“ der Menüleiste – KeePass zeigt es
		// dann unter der Menüleiste an. Wir merken uns, wo im Kontextmenü geklickt wurde.
		private Point m_ctxClickPt = Point.Empty;
		private DateTime m_ctxClickTime = DateTime.MinValue;

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		/// <summary>Schrift für die aufklappenden Menüs (bleibt in normaler Größe,
		/// auch wenn die Menüleiste selbst größer ist).</summary>
		public void SetDropDownFont(Font f)
		{
			m_dropFont = f;
		}

		public void Update(Palette p, float scale)
		{
			m_p = p;
			m_s = scale;
		}

		/// <summary>Alle Dropdowns einer Leiste (Menüleiste, Symbolleiste) einhängen.</summary>
		public void Attach(ToolStrip ts)
		{
			if(ts == null) return;
			ToolStripDropDown dd = (ts as ToolStripDropDown);
			if(dd != null) HookDropDown(dd);
			foreach(ToolStripItem it in ts.Items) HookItem(it);
		}

		private void HookItem(ToolStripItem it)
		{
			ToolStripDropDownItem ddi = (it as ToolStripDropDownItem);
			if((ddi == null) || !ddi.HasDropDownItems) return;
			HookDropDown(ddi.DropDown);
		}

		public void HookDropDown(ToolStripDropDown dd)
		{
			if((dd == null) || dd.IsDisposed) return;
			if(!m_hooked.Add(dd)) return;
			DisableLegacyShadow(dd);
			dd.Opening += OnOpening;
			dd.ItemClicked += OnItemClicked;
			dd.Opened += OnOpened;
			dd.ItemAdded += OnItemAdded;
			dd.Disposed += OnDisposed;
		}

		/// <summary>Unter Windows 11 zeichnet DWM für abgerundete Popups einen eigenen, weichen Schatten.
		/// Der alte eckige Schlagschatten von WinForms würde an den runden Ecken unten rechts
		/// als dunkles Eck herausragen – daher abschalten.
		/// Wichtig: das Menüfenster dabei NIE neu erzeugen (DropShadowEnabled bei vorhandenem
		/// Fenster) – sonst landet es als Kindfenster mitten in KeePass. Ist das Fenster schon da,
		/// wird nur der Schatten-Stil der Fensterklasse entfernt.</summary>
		private static void DisableLegacyShadow(ToolStripDropDown dd)
		{
			if(!NativeMethods.IsWin11 || dd.IsDisposed) return;
			try
			{
				if(!dd.IsHandleCreated)
				{
					if(dd.DropShadowEnabled) dd.DropShadowEnabled = false;
				}
				else NativeMethods.RemoveClassDropShadow(dd.Handle);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		/// <summary>Später hinzugefügte Einträge (z. B. von anderen Plugins) gleich mitgestalten.</summary>
		private void OnItemAdded(object sender, ToolStripItemEventArgs e)
		{
			if((m_p == null) || (e.Item == null)) return;
			try
			{
				StyleItem(e.Item);
				ToolStripDropDown dd = (sender as ToolStripDropDown);
				if(dd != null) UpdateImageMargin(dd);
			}
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		public void Detach()
		{
			foreach(ToolStripDropDown dd in m_hooked)
			{
				dd.Opening -= OnOpening;
				dd.ItemClicked -= OnItemClicked;
				dd.Opened -= OnOpened;
				dd.ItemAdded -= OnItemAdded;
				dd.Disposed -= OnDisposed;
			}
			m_hooked.Clear();
		}

		private void OnDisposed(object sender, EventArgs e)
		{
			ToolStripDropDown dd = (sender as ToolStripDropDown);
			if(dd != null) m_hooked.Remove(dd);
		}

		private void OnOpening(object sender, EventArgs e)
		{
			ToolStripDropDown dd = (sender as ToolStripDropDown);
			if((dd == null) || (m_p == null)) return;
			try { StyleItems(dd); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void OnOpened(object sender, EventArgs e)
		{
			ToolStripDropDown dd = (sender as ToolStripDropDown);
			if((dd == null) || (m_p == null) || !dd.IsHandleCreated) return;
			try { CollapseSeparators(dd); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
			DisableLegacyShadow(dd);
			NativeMethods.SetPopupStyle(dd.Handle, m_p.Border);
			try { MoveToContextClick(dd); }
			catch(Exception ex) { FluentThemeExt.Report(ex); }
		}

		private void StyleItems(ToolStripDropDown dd)
		{
			dd.SuspendLayout();
			try
			{
				Padding ddPad = new Padding(D(6), D(4), D(6), D(4));
				if(dd.Padding != ddPad) dd.Padding = ddPad;

				// Alle Symbole auf 16 px (skaliert) – sonst machen große Plugin-Symbole
				// einzelne Zeilen höher und der Abstand wirkt ungleichmäßig
				Size isz = new Size(D(16), D(16));
				if(dd.ImageScalingSize != isz) dd.ImageScalingSize = isz;

				if((m_dropFont != null) && !dd.Font.Equals(m_dropFont)) dd.Font = m_dropFont;

				foreach(ToolStripItem it in dd.Items) StyleItem(it);
				UpdateImageMargin(dd);
				CollapseSeparators(dd);
			}
			finally { dd.ResumeLayout(); }
		}

		/// <summary>Doppelte Trennstriche (z. B. wenn ein Plugin eigene Trennstriche einfügt oder
		/// Einträge dazwischen ausgeblendet sind) sowie Striche am Anfang/Ende ausblenden.</summary>
		private void CollapseSeparators(ToolStripDropDown dd)
		{
			bool anyBefore = false;
			bool prevSep = false;
			ToolStripSeparator lastSep = null;
			foreach(ToolStripItem it in dd.Items)
			{
				ToolStripSeparator sep = (it as ToolStripSeparator);
				if(sep == null)
				{
					if(it.Available) { anyBefore = true; prevSep = false; lastSep = null; }
					continue;
				}

				bool hide = (!anyBefore || prevSep);
				if(hide)
				{
					if(sep.Available) { sep.Available = false; m_hiddenSeps.Add(sep); }
				}
				else
				{
					if(m_hiddenSeps.Remove(sep)) sep.Available = true;
					if(sep.Available) { prevSep = true; lastSep = sep; }
				}
			}
			// Trennstrich ganz am Ende
			if((lastSep != null) && prevSep && lastSep.Available)
			{
				lastSep.Available = false;
				m_hiddenSeps.Add(lastSep);
			}

			ApplyEdgeSpacing(dd);
		}

		/// <summary>Zusätzlicher Abstand über dem ersten und unter dem letzten sichtbaren Eintrag
		/// (zuverlässiger als das Padding des Menüs).</summary>
		private void ApplyEdgeSpacing(ToolStripDropDown dd)
		{
			ToolStripItem first = null, last = null;
			foreach(ToolStripItem it in dd.Items)
			{
				if(!it.Available) continue;
				if(first == null) first = it;
				last = it;
			}

			foreach(ToolStripItem it in dd.Items)
			{
				if((it == first) || (it == last) || !m_edgeItems.Contains(it)) continue;
				it.Margin = Padding.Empty;
				m_edgeItems.Remove(it);
			}
			if(first == null) return;

			int top = D(6), bottom = D(6); // oben und unten gleich (globale Vorgabe: 6 × Skalierung)
			Padding pf = new Padding(0, top, 0, ((first == last) ? bottom : 0));
			if(first.Margin != pf) first.Margin = pf;
			m_edgeItems.Add(first);
			if(last != first)
			{
				Padding pl = new Padding(0, 0, 0, bottom);
				if(last.Margin != pl) last.Margin = pl;
				m_edgeItems.Add(last);
			}
		}

		private void OnItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			ToolStripDropDown dd = (sender as ToolStripDropDown);
			if((dd == null) || (e.ClickedItem == null)) return;
			// Nur Kontextmenüs (kein Besitzer in einer Menüleiste)
			ToolStripDropDown top = dd;
			while((top.OwnerItem != null) && (top.OwnerItem.Owner is ToolStripDropDown))
				top = (ToolStripDropDown)top.OwnerItem.Owner;
			if(top.OwnerItem != null) return;
			m_ctxClickPt = Cursor.Position;
			m_ctxClickTime = DateTime.Now;
		}

		/// <summary>Öffnet sich ein Menü der Menüleiste direkt nach einem Klick im Kontextmenü
		/// (z. B. „Weitere Befehle“), erscheint es an der Klickstelle statt unter der Menüleiste.</summary>
		private void MoveToContextClick(ToolStripDropDown dd)
		{
			if(m_ctxClickPt.IsEmpty || ((DateTime.Now - m_ctxClickTime).TotalMilliseconds > 800)) return;
			ToolStripItem owner = dd.OwnerItem;
			if((owner == null) || !(owner.Owner is MenuStrip)) return;
			// Hat der Nutzer die Menüleiste selbst angeklickt, nichts verschieben
			ToolStrip bar = owner.Owner;
			if(bar.IsHandleCreated && bar.RectangleToScreen(bar.ClientRectangle).Contains(Cursor.Position)) return;

			Point pt = m_ctxClickPt;
			m_ctxClickPt = Point.Empty;
			Rectangle wa = Screen.FromPoint(pt).WorkingArea;
			int x = Math.Max(wa.Left, Math.Min(pt.X, wa.Right - dd.Width));
			int y = Math.Max(wa.Top, Math.Min(pt.Y, wa.Bottom - dd.Height));
			dd.Location = new Point(x, y);
		}

		/// <summary>Globale Vorgabe: Menüs ohne Symbole bekommen keine leere Symbolspalte
		/// (der Text beginnt dann mit 16 × Skalierung Rand, siehe FluentRenderer).</summary>
		private static void UpdateImageMargin(ToolStripDropDown dd)
		{
			ToolStripDropDownMenu ddm = (dd as ToolStripDropDownMenu);
			if(ddm == null) return;
			bool need = false;
			foreach(ToolStripItem it in ddm.Items)
			{
				if(!it.Available) continue;
				ToolStripMenuItem mi = (it as ToolStripMenuItem);
				if((it.Image != null) || ((mi != null) && (mi.Checked || mi.CheckOnClick)))
				{
					need = true;
					break;
				}
			}
			if(ddm.ShowImageMargin != need) ddm.ShowImageMargin = need;
		}

		private void StyleItem(ToolStripItem it)
		{
			if(it is ToolStripSeparator)
			{
				Padding sepMargin = new Padding(0, D(2), 0, D(2));
				if(it.Margin != sepMargin) it.Margin = sepMargin;
				return;
			}

			ToolStripMenuItem mi = (it as ToolStripMenuItem);
			if(mi != null)
			{
				// rechts etwas mehr, weil der Text für den Abstand Symbol -> Text nach rechts rückt
				Padding itemPad = new Padding(D(4), D(2), D(11), D(2));
				if(mi.Padding != itemPad) mi.Padding = itemPad;
				if(mi.ImageScaling != ToolStripItemImageScaling.SizeToFit)
					mi.ImageScaling = ToolStripItemImageScaling.SizeToFit;
			}
			HookItem(it);
		}
	}
}