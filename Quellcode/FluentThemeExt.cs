// KeePass Fluent Theme – Windows-11-Design für KeePass 2.x
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using Microsoft.Win32;

using KeePass.Forms;
using KeePass.Plugins;

namespace FluentTheme
{
	/// <summary>
	/// Fluent Theme für KeePass 2.x – modernes Windows-11-Design im klassischen Layout.
	/// Autor: Günther Schoder
	/// </summary>
	public sealed class FluentThemeExt : Plugin
	{
		private const string CfgMode = "FluentTheme.Mode";
		private const string CfgAccent = "FluentTheme.Accent";
		private const string CfgRowHeight = "FluentTheme.RowHeight";
		private const string CfgDetails = "FluentTheme.ModernDetails";
		private const string CfgHover = "FluentTheme.ListHover";
		private const string CfgIcons = "FluentTheme.ModernIcons";
		private const string CfgAutoHeight = "FluentTheme.DetailsAutoHeight";
		private const string CfgReveal = "FluentTheme.RevealSeconds";
		private const string CfgColumns = "FluentTheme.DetailsColumns";
		private const string CfgExpiryDays = "FluentTheme.ExpiryWarnDays";
		private const string CfgShowVersion = "FluentTheme.ShowVersion";

		private static string s_lastError;
		private static string s_toolbarInfo = string.Empty;

		private readonly Dictionary<ToolStripItem, Image> m_origImages = new Dictionary<ToolStripItem, Image>();
		private readonly Dictionary<ToolStripItem, Image> m_genImages = new Dictionary<ToolStripItem, Image>();
		private ToolStripItem m_saveItem;  // Speichern-Knopf der Symbolleiste
		private Image m_saveOff;           // graue Variante (keine Änderungen)
		private TabControl m_tabs;
		private TabStripPainter m_tabPainter;
		private DialogStyler m_dialogs;
		private OverlayScroll m_treeScroll;
		private OverlayScroll m_listScroll;
		private Font m_menuBaseFont;
		private Font m_menuBarFont;
		private Font m_menuDropFont;
		private Font m_tabFont;
		private Font m_tabFontSel;

		private IPluginHost m_host;
		private Timer m_timer;
		private Palette m_p;
		private static Palette s_current;
		private float m_s = 1f;
		private FluentRenderer m_renderer;
		private readonly List<ToolStrip> m_strips = new List<ToolStrip>();

		private TreeView m_tv;
		private TreePainter m_treePainter;
		private readonly Dictionary<ComboBox, SearchBoxPainter> m_searchPainters =
			new Dictionary<ComboBox, SearchBoxPainter>();
		private string m_tagFilter;      // aktuell per Tag-Chip gefilterter Tag
		private ListView m_lv;
		private RichTextBox m_rich;
		private EntryListPainter m_painter;
		private DetailsPanel m_details;
		private SplitContainer m_detailsSplit;
		private bool m_fitting;
		private CardFrame m_treeCard;
		private CardFrame m_listCard;
		private readonly MenuStyler m_menus = new MenuStyler();
		private FooterBadge m_badge;
		private ToolStripControlHost m_badgeHost;
		private ToolStripStatusLabel m_badgeSpring;
		private bool m_applying;

		/// <summary>Aktuelle Farben (für Meldungen und Tooltips); null, solange das Design noch nicht geladen ist.</summary>
		internal static Palette CurrentPalette
		{
			get { return s_current; }
		}

		internal static void Report(Exception ex)
		{
			if(ex == null) return;
			s_lastError = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex;
			System.Diagnostics.Debug.WriteLine(s_lastError);
		}

		/// <summary>Update-Prüfung von KeePass (Extras › Nach Updates suchen): Versionsdatei im
		/// GitHub-Repository, wird von der Erstellen.bat bei jedem Build neu geschrieben.</summary>
		public override string UpdateUrl
		{
			get { return "https://raw.githubusercontent.com/WestMc002/KeePass-Fluent-Theme/main/keepass.version"; }
		}

		/// <summary>Eigenes Icon in der Plugin-Liste von KeePass (Extras › Plugins).</summary>
		public override Image SmallIcon
		{
			get { return AppIcon.GetImage(AppIcon.SmallSize); }
		}

		public override bool Initialize(IPluginHost host)
		{
			if(host == null) return false;
			m_host = host;
			L.Init(); // Sprache von KeePass übernehmen (Deutsch oder Englisch)

			// Screenshot-Modus (nur mit -fluent-screenshots:"Ordner", für die portable Screenshot-Kopie):
			// Trigger, zuletzt geöffnete Datenbank und Update-Prüfung abschalten, bevor KeePass sie nutzt
			string shotDir = null;
			try { shotDir = KeePass.Program.CommandLineArgs["fluent-screenshots"]; }
			catch(Exception) { shotDir = null; }
			if(!string.IsNullOrEmpty(shotDir)) ScreenshotRunner.PrepareConfig();

			try
			{
				MainForm mf = host.MainWindow;
				m_tv = FindControl<TreeView>(mf, "m_tvGroups");
				m_tabs = FindControl<TabControl>(mf, "m_tabMain");
				m_lv = FindControl<ListView>(mf, "m_lvEntries");
				m_rich = FindControl<RichTextBox>(mf, "m_richEntryView");

				if((m_tv != null) && (m_tv.Parent != null))
				{
					m_treeCard = new CardFrame(m_tv.Parent);
					m_treeCard.TitleIndent = 18; // bündig mit dem Symbol der obersten Gruppe (siehe TreePainter)
				}
				if((m_lv != null) && (m_lv.Parent != null) && ((m_tv == null) || (m_lv.Parent != m_tv.Parent)))
					m_listCard = new CardFrame(m_lv.Parent);

				if(m_tv != null)
				{
					m_treePainter = new TreePainter(m_tv);
					m_treePainter.Attach();
					m_tv.AfterSelect += OnTreeAfterSelect;
					m_tv.EnabledChanged += OnTreeEnabledChanged;
				}

				if(m_lv != null)
				{
					m_painter = new EntryListPainter(m_lv);
					m_painter.Attach();
					m_lv.SelectedIndexChanged += OnEntrySelectionChanged;
				}

				if((m_lv != null) && (m_rich != null) && (m_rich.Parent != null))
				{
					m_details = new DetailsPanel(host, m_lv, m_painter);
					m_rich.Parent.Controls.Add(m_details);
					m_details.BringToFront();
					m_details.ContentHeightChanged += delegate { FitDetails(); };
					m_details.TagClicked += OnTagClicked;
					m_details.ColumnRatios = m_host.CustomConfig.GetString(CfgColumns, string.Empty);
					m_details.ColumnsChanged += delegate
					{
						m_host.CustomConfig.SetString(CfgColumns, m_details.ColumnRatios);
					};
					m_detailsSplit = FindDetailsSplit();
					if(m_detailsSplit != null) m_detailsSplit.SizeChanged += OnDetailsSplitSizeChanged;
				}

				// Dezente, schmale Scrollleisten statt der breiten grauen
				try
				{
					if((m_tv != null) && (m_tv.Parent != null)) m_treeScroll = new OverlayScroll(m_tv);
					if((m_lv != null) && (m_lv.Parent != null)) m_listScroll = new OverlayScroll(m_lv);
				}
				catch(Exception ex) { Report(ex); }

				m_dialogs = new DialogStyler(mf);
				m_dialogs.Attach();

				SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

				m_timer = new Timer();
				m_timer.Interval = 400;
				m_timer.Tick += OnTimerTick;
				m_timer.Start();

				if(mf.IsHandleCreated) ApplyAll();
				else mf.HandleCreated += OnMainHandleCreated;

				if(!string.IsNullOrEmpty(shotDir))
				{
					m_shots = new ScreenshotRunner(this, shotDir);
					mf.FormLoadPost += delegate { m_shots.Start(); };
				}
			}
			catch(Exception ex) { Report(ex); }

			return true;
		}

		public override void Terminate()
		{
			try
			{
				SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
				if(m_timer != null)
				{
					m_timer.Stop();
					m_timer.Dispose();
					m_timer = null;
				}
				if(m_lv != null) m_lv.SelectedIndexChanged -= OnEntrySelectionChanged;
				BannerStyler.Uninstall();
				foreach(SearchBoxPainter sp in m_searchPainters.Values) sp.Detach();
				m_searchPainters.Clear();
				if(m_painter != null) m_painter.Detach();
				if(m_treePainter != null) m_treePainter.Detach();
				if(m_tv != null)
				{
					m_tv.AfterSelect -= OnTreeAfterSelect;
					m_tv.EnabledChanged -= OnTreeEnabledChanged;
				}
				m_menus.Detach();
				RemoveBadge();
				if(m_dialogs != null) m_dialogs.Detach();
				if(m_treeScroll != null) { m_treeScroll.Dispose(); m_treeScroll = null; }
				if(m_listScroll != null) { m_listScroll.Dispose(); m_listScroll = null; }
				if(m_tabPainter != null) { m_tabPainter.Detach(); m_tabPainter = null; }
				if(m_treeCard != null) m_treeCard.Detach();
				if(m_listCard != null) m_listCard.Detach();
				if(m_detailsSplit != null)
				{
					m_detailsSplit.SizeChanged -= OnDetailsSplitSizeChanged;
					m_detailsSplit = null;
				}
				if(m_details != null)
				{
					if(m_details.Parent != null) m_details.Parent.Controls.Remove(m_details);
					m_details.Dispose();
					m_details = null;
				}
			}
			catch(Exception ex) { Report(ex); }
		}

		// ---------------------------------------------------------------- Einstellungen

		private ThemeMode Mode
		{
			get
			{
				long v = m_host.CustomConfig.GetLong(CfgMode, 0);
				if((v < 0) || (v > 2)) v = 0;
				return (ThemeMode)v;
			}
		}

		private bool IsDark()
		{
			if(m_shotDark >= 0) return (m_shotDark == 1); // Screenshot-Modus: fest hell/dunkel
			ThemeMode m = this.Mode;
			if(m == ThemeMode.Dark) return true;
			if(m == ThemeMode.Light) return false;
			return Palette.SystemUsesDark();
		}

		private string AccentSetting
		{
			get { return m_host.CustomConfig.GetString(CfgAccent, string.Empty) ?? string.Empty; }
		}

		/// <summary>Leer (Standard) oder "system" = Windows-Akzentfarbe, sonst #RRGGBB.</summary>
		private Color GetAccent()
		{
			string a = this.AccentSetting;
			if((a.Length == 0) || string.Equals(a, "system", StringComparison.OrdinalIgnoreCase))
				return Palette.SystemAccent();
			return Palette.ParseHex(a, Palette.SystemAccent());
		}

		private int RowHeight
		{
			get
			{
				long v = m_host.CustomConfig.GetLong(CfgRowHeight, 28);
				return (int)Math.Max(20, Math.Min(44, v));
			}
		}

		private bool ModernDetails
		{
			get { return m_host.CustomConfig.GetBool(CfgDetails, true); }
		}

		private bool ModernIcons
		{
			get { return m_host.CustomConfig.GetBool(CfgIcons, true); }
		}

		private int D(int v)
		{
			return (int)Math.Round(v * m_s);
		}

		private bool ListHover
		{
			get { return m_host.CustomConfig.GetBool(CfgHover, true); }
		}

		// ---------------------------------------------------------------- Anwenden

		private void ApplyAll()
		{
			if(m_applying) return;
			MainForm mf = m_host.MainWindow;
			if((mf == null) || mf.IsDisposed) return;

			m_applying = true;
			try
			{
				bool dark = IsDark();
				m_p = Palette.Create(dark, GetAccent());
				s_current = m_p;
				BannerStyler.Install(m_p);
				using(Graphics g = mf.CreateGraphics()) { m_s = g.DpiX / 96f; }

				NativeMethods.SetAppDark(dark);
				mf.BackColor = m_p.Bg;
				if(mf.IsHandleCreated)
					NativeMethods.SetTitleBar(mf.Handle, dark, m_p.Bg, m_p.Text, m_p.Border);

				// Menü, Symbolleiste, Statusleiste, Kontextmenüs
				m_renderer = new FluentRenderer(m_p, m_s);
				ToolStripManager.Renderer = m_renderer;
				m_strips.Clear();
				foreach(ToolStrip ts in FindAll<ToolStrip>(mf))
				{
					m_strips.Add(ts);
					StyleToolStrip(ts);
				}
				if(this.ShowVersion) EnsureBadge();
				else RemoveBadge();
				StyleMainToolbar();
				StyleTabs();
				if(m_dialogs != null) m_dialogs.Update(m_p, m_s);
				if(m_treeScroll != null) m_treeScroll.Update(m_p, m_s);
				if(m_listScroll != null) m_listScroll.Update(m_p, m_s);
				StyleMenuBar();
				m_menus.Update(m_p, m_s);
				foreach(ToolStrip ts in m_strips) m_menus.Attach(ts);
				List<Control> allControls = FindAll<Control>(mf);
				allControls.Add(mf);
				foreach(Control c in allControls)
				{
					if(c.ContextMenuStrip != null)
					{
						c.ContextMenuStrip.Renderer = m_renderer;
						m_menus.Attach(c.ContextMenuStrip);
					}
				}

				// Grauer Hintergrund, Bereiche als weiße Karten
				foreach(SplitContainer sc in FindAll<SplitContainer>(mf))
				{
					sc.BackColor = m_p.Bg;
					sc.Panel1.BackColor = m_p.Bg;
					sc.Panel2.BackColor = m_p.Bg;
				}
				if(m_treeCard != null) m_treeCard.Apply(m_p, m_s);
				if(m_listCard != null) m_listCard.Apply(m_p, m_s);
				UpdateCardHeaders();

				// Gruppenbaum
				if(m_tv != null)
				{
					m_tv.BorderStyle = BorderStyle.None;
					m_tv.BackColor = m_p.Card;
					m_tv.ForeColor = m_p.Text;
					m_tv.ShowLines = false;
					m_tv.FullRowSelect = true;
					m_tv.ItemHeight = Math.Max(16, (int)Math.Round((this.RowHeight - 2) * m_s));
					if(m_treePainter != null) m_treePainter.Update(m_p, m_s, this.ListHover);
					m_tv.Indent = (int)Math.Round(22 * m_s); // Platz zwischen Akzentstreifen, Pfeil und Symbol
					if(m_tv.IsHandleCreated)
					{
						NativeMethods.ThemeControl(m_tv, dark, "DarkMode_Explorer", "Explorer");
						NativeMethods.TreeDoubleBuffer(m_tv);
					}
				}

				// Eintragsliste
				if(m_painter != null)
				{
					m_painter.ExpiryDays = this.ExpiryDays;
					m_painter.Update(m_p, m_s, this.RowHeight, this.ListHover);
				}

				// Detailansicht
				if(m_rich != null)
				{
					m_rich.BorderStyle = BorderStyle.None;
					m_rich.BackColor = m_p.Card;
				}
				if(m_details != null)
				{
					m_details.SetPalette(m_p, m_s);
					m_details.RevealSeconds = this.RevealSeconds;
					m_details.Visible = this.ModernDetails;
					if(m_details.Visible) m_details.BringToFront();
					m_details.RefreshIfChanged(true);
				}

				mf.Invalidate(true);
				FitDetails();
			}
			catch(Exception ex) { Report(ex); }
			finally { m_applying = false; }
		}

		/// <summary>Menüleiste (Datei, Gruppe, Eintrag …) eine Stufe größer; die aufklappenden
		/// Menüs behalten die normale Schrift.</summary>
		private void StyleMenuBar()
		{
			foreach(ToolStrip ts in m_strips)
			{
				MenuStrip ms = (ts as MenuStrip);
				if(ms == null) continue;
				if(m_menuBaseFont == null) m_menuBaseFont = ms.Font;
				if(m_menuBarFont == null) m_menuBarFont = new Font(m_menuBaseFont.FontFamily, m_menuBaseFont.SizeInPoints + 1f);
				if(m_menuDropFont == null)
					m_menuDropFont = new Font(m_menuBaseFont.FontFamily, m_menuBaseFont.SizeInPoints + 0.75f);
				m_menus.SetDropDownFont(m_menuDropFont);
				foreach(ToolStripItem it in ms.Items)
				{
					if(!it.Font.Equals(m_menuBarFont)) it.Font = m_menuBarFont;
					it.Padding = new Padding(D(2), D(2), D(2), D(2));
				}
				break;
			}
		}

		/// <summary>Symbolleiste: moderne Linien-Symbole, mehr Luft, Abstand zum rechten Rand.</summary>
		private void StyleMainToolbar()
		{
			ToolStrip tb = null;
			foreach(ToolStrip ts in m_strips)
			{
				if((ts is MenuStrip) || (ts is StatusStrip)) continue;
				if(tb == null) tb = ts;
				if(ts.Name == "m_toolMain") { tb = ts; break; }
			}
			if(tb == null) return;

			int isz = D(16);
			bool modern = this.ModernIcons;
			tb.SuspendLayout();
			try
			{
				tb.GripStyle = ToolStripGripStyle.Hidden;
				// Höhe der Leiste nicht verändern (sonst überdeckt die Tab-Leiste die Symbole)
				tb.Padding = new Padding(D(6), 0, D(10), 0);
				tb.ImageScalingSize = new Size(isz, isz);

				List<string> unknown = new List<string>();
				foreach(ToolStripItem it in tb.Items)
				{
					if(it is ToolStripSeparator)
					{
						it.Margin = new Padding(D(2), 0, D(2), 0);
						continue;
					}
					if(it is ToolStripControlHost) continue;

					string kind = IconFactory.KindFor(it.Name);
					if(kind == null)
					{
						unknown.Add(string.IsNullOrEmpty(it.Name) ? ("(ohne Name: " + it.Text + ")") : it.Name);
						continue;
					}

					if(!m_origImages.ContainsKey(it)) m_origImages[it] = it.Image;
					if(modern)
					{
						Image gen = IconFactory.Create(kind, isz, m_p.Text, Math.Max(1.3f, 1.5f * m_s));
						m_genImages[it] = gen;
						if(kind == "save")
						{
							// KeePass lässt den Knopf aktiv und zeigt nur über das Bild an, ob es
							// ungespeicherte Änderungen gibt - das nachbilden (farbig / grau)
							m_saveItem = it;
							m_saveOff = ToolStripRenderer.CreateDisabledImage(gen);
							// KeePass setzt das Bild bei jedem Klick auf einen Eintrag neu (aus zwei
							// eigenen Feldern). Dort unsere Bilder hinterlegen, sonst blitzt kurz das
							// KeePass-Symbol auf, bis die Zeitschaltung es wieder ersetzt.
							SetKeePassSaveImages(gen, m_saveOff);
						}
						it.Image = DesiredImage(it, gen);
						it.ImageScaling = ToolStripItemImageScaling.None;
					}
					else
					{
						m_genImages.Remove(it);
						if(kind == "save") RestoreKeePassSaveImages();
						it.Image = m_origImages[it];
						it.ImageScaling = ToolStripItemImageScaling.SizeToFit;
					}
					it.Padding = new Padding(D(2), 0, D(2), 0);
					it.Margin = ((kind == "close") ? new Padding(0, D(1), D(8), D(1)) :
						new Padding(0, D(1), 0, D(1)));
				}
				s_toolbarInfo = ((unknown.Count > 0) ? ("Symbolleiste ohne Zuordnung: " +
					string.Join(", ", unknown.ToArray())) : string.Empty);
			}
			finally { tb.ResumeLayout(); }

			// Breite der Knöpfe festhalten (auch mit den KeePass-Symbolen): KeePass tauscht beim Klick auf
			// einen Eintrag das Speichern-Symbol aus, kurz wird der Knopf schmaler – die Leiste samt
			// Suchfeld sprang dann hin und her
			try
			{
				foreach(ToolStripItem it in tb.Items)
				{
					if((it is ToolStripSeparator) || (it is ToolStripControlHost)) continue;
					if(IconFactory.KindFor(it.Name) == null) continue;
					it.AutoSize = true;
					Size sz = it.GetPreferredSize(Size.Empty);
					it.AutoSize = false;
					it.Size = sz;
				}
			}
			catch(Exception ex) { Report(ex); }
		}

		/// <summary>Datenbank-Tabs: größere Schrift, aktiver Tab blau hinterlegt.</summary>
		private void StyleTabs()
		{
			if(m_tabs == null) return;

			if(m_tabFont != null) m_tabFont.Dispose();
			if(m_tabFontSel != null) m_tabFontSel.Dispose();
			m_tabFont = new Font("Segoe UI", 10f);
			try { m_tabFontSel = new Font("Segoe UI Semibold", 10f); }
			catch(ArgumentException) { m_tabFontSel = new Font("Segoe UI", 10f, FontStyle.Bold); }

			m_tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
			// Breite der Tabs nach der fetten Schrift bemessen, sonst ragt der Name der aktiven
			// Datenbank über die blaue Fläche hinaus
			m_tabs.Font = m_tabFontSel;
			m_tabs.Padding = new Point(D(14), D(5));
			if(m_tabs.Parent != null) m_tabs.Parent.BackColor = m_p.Bg;
			foreach(TabPage tp in m_tabs.TabPages) tp.BackColor = m_p.Bg;
			if(m_tabPainter == null) m_tabPainter = new TabStripPainter(m_tabs);
			m_tabPainter.Update(m_p, m_s, m_tabFont, m_tabFontSel);
			if(m_tabs.TabCount > 0)
			{
				int h = m_tabs.GetTabRect(0).Bottom + D(3);
				if(m_tabs.Height < h) m_tabs.Height = h;
			}
			m_tabs.Invalidate();
		}

		private void StyleToolStrip(ToolStrip ts)
		{
			ts.Renderer = m_renderer;
			ts.BackColor = m_p.Bg;
			ts.ForeColor = m_p.Text;
			foreach(ToolStripItem it in ts.Items)
			{
				ToolStripComboBox cb = (it as ToolStripComboBox);
				if(cb != null)
				{
					// Nicht „Flat“: Damit zeichnet WinForms Rahmen und Pfeil bei jedem Fokuswechsel selbst
					// über das Feld (Flackern, doppelter Pfeil). Gezeichnet wird alles vom SearchBoxPainter.
					if(cb.FlatStyle != FlatStyle.Standard) cb.FlatStyle = FlatStyle.Standard;
					cb.BackColor = m_p.Card;
					cb.ForeColor = m_p.Text;
					// Suchfeld breiter (Höhe bleibt, damit die Symbolleiste nicht wächst)
					int w = D(300);
					if(cb.Size.Width < w)
					{
						cb.AutoSize = false;
						cb.Size = new Size(w, cb.Size.Height);
						cb.DropDownWidth = w;
					}
					// Rahmen, Pfeil, Liste und Fokuslinie im Windows-11-Stil (zuerst, weil das
					// Umstellen auf eigene Listeneinträge das Feld neu anlegt)
					// Fehler hier dürfen nie das restliche Theme verhindern
					ComboBox box = cb.ComboBox;
					SearchBoxPainter sp;
					bool first = !m_searchPainters.TryGetValue(box, out sp);
					try
					{
						if(first)
						{
							sp = new SearchBoxPainter(box);
							m_searchPainters[box] = sp;
						}
						sp.Update(m_p, m_s, ts.BackColor);
					}
					catch(Exception ex) { Report(ex); }

					// etwas Abstand zwischen Rand und Text (auch nach jedem Neuanlegen des Felds)
					int margin = D(8);
					if(box.IsHandleCreated) NativeMethods.SetComboTextMargin(box, margin);
					if(first) box.HandleCreated += delegate { NativeMethods.SetComboTextMargin(box, D(8)); };
				}
				ToolStripTextBox tb = (it as ToolStripTextBox);
				if(tb != null)
				{
					tb.BorderStyle = BorderStyle.FixedSingle;
					tb.BackColor = m_p.Card;
					tb.ForeColor = m_p.Text;
				}
			}
		}

		/// <summary>Logo, Name und Webseite unten rechts in der Statusleiste.</summary>
		private void EnsureBadge()
		{
			StatusStrip ss = null;
			foreach(ToolStrip ts in m_strips)
			{
				ss = (ts as StatusStrip);
				if(ss != null) break;
			}
			if(ss == null) return;

			if(m_badge == null)
			{
				bool hasSpring = false;
				foreach(ToolStripItem it in ss.Items)
				{
					ToolStripStatusLabel sl = (it as ToolStripStatusLabel);
					if((sl != null) && sl.Spring) { hasSpring = true; break; }
				}
				if(!hasSpring)
				{
					m_badgeSpring = new ToolStripStatusLabel();
					m_badgeSpring.Spring = true;
					ss.Items.Add(m_badgeSpring);
				}

				m_badge = new FooterBadge();
				m_badge.InfoRequested += delegate { ShowAbout(); };
				m_badgeHost = new ToolStripControlHost(m_badge);
				m_badgeHost.AutoSize = false;
				ss.Items.Add(m_badgeHost);
			}

			m_badge.SetPalette(m_p, m_s);
			int d2 = (int)Math.Round(1 * m_s);
			m_badgeHost.Margin = new Padding((int)Math.Round(8 * m_s), d2, (int)Math.Round(14 * m_s), d2);
			m_badgeHost.Size = m_badge.Size;
		}

		/// <summary>Einheitliches Info-Fenster (Vorlage Monitor Dimmer).</summary>
		private void ShowAbout()
		{
			if(m_p == null) return;
			ThemeMode m = this.Mode;
			string scheme = L.T(m_p.Dark ? "dunkel" : "hell") +
				L.T((m == ThemeMode.System) ? " (folgt Windows)" : " (fest eingestellt)");
			AboutDialog.ShowFor(m_host.MainWindow, m_p, scheme);
		}

		private void RemoveBadge()
		{
			if(m_badgeHost != null)
			{
				ToolStrip owner = m_badgeHost.Owner;
				if(owner != null)
				{
					owner.Items.Remove(m_badgeHost);
					if(m_badgeSpring != null) owner.Items.Remove(m_badgeSpring);
				}
				m_badgeHost.Dispose();
				m_badgeHost = null;
				m_badge = null;
				m_badgeSpring = null;
			}
		}

		private void OnMainHandleCreated(object sender, EventArgs e)
		{
			m_host.MainWindow.HandleCreated -= OnMainHandleCreated;
			ApplyAll();
		}

		private void OnTimerTick(object sender, EventArgs e)
		{
			try
			{
				MainForm mf = m_host.MainWindow;
				if((mf == null) || mf.IsDisposed || !mf.IsHandleCreated) return;
				if(m_p == null) { ApplyAll(); return; }

				if((m_renderer != null) && (ToolStripManager.Renderer != m_renderer))
					ToolStripManager.Renderer = m_renderer;
				foreach(ToolStrip ts in m_strips)
				{
					if(!ts.IsDisposed && (ts.Renderer != m_renderer)) StyleToolStrip(ts);
				}

				if(m_painter != null) m_painter.EnsureState();
				if(m_dialogs != null) m_dialogs.Poll();
				if(m_treeScroll != null) m_treeScroll.Sync();
				if(m_listScroll != null) m_listScroll.Sync();
				if(this.ModernIcons)
				{
					// KeePass tauscht manche Symbole selbst aus (z. B. Speichern) - wieder ersetzen
					foreach(KeyValuePair<ToolStripItem, Image> kv in m_genImages)
					{
						Image want = DesiredImage(kv.Key, kv.Value);
						Image cur = kv.Key.Image;
						if(cur == want) continue;
						if((cur != kv.Value) && (cur != m_saveOff)) m_origImages[kv.Key] = cur;
						kv.Key.Image = want;
					}
				}
				if((m_details != null) && m_details.Visible) m_details.RefreshIfChanged(false);
				UpdateCardHeaders();
			}
			catch(Exception ex) { Report(ex); }
		}

		private Image m_kpSaveOn, m_kpSaveOff;   // ursprüngliche Speichern-Bilder von KeePass
		private bool m_kpSaveSaved;

		private System.Reflection.FieldInfo KpField(string name)
		{
			return typeof(MainForm).GetField(name, System.Reflection.BindingFlags.Instance |
				System.Reflection.BindingFlags.NonPublic);
		}

		/// <summary>Die Bilder, die KeePass für „geändert“ und „nicht geändert“ verwendet, durch
		/// unsere ersetzen (private Felder des Hauptfensters).</summary>
		private void SetKeePassSaveImages(Image on, Image off)
		{
			try
			{
				MainForm mf = m_host.MainWindow;
				System.Reflection.FieldInfo fOn = KpField("m_imgFileSaveEnabled");
				System.Reflection.FieldInfo fOff = KpField("m_imgFileSaveDisabled");
				if((mf == null) || (fOn == null) || (fOff == null)) return;
				if(!m_kpSaveSaved)
				{
					m_kpSaveOn = (fOn.GetValue(mf) as Image);
					m_kpSaveOff = (fOff.GetValue(mf) as Image);
					m_kpSaveSaved = true;
				}
				fOn.SetValue(mf, on);
				fOff.SetValue(mf, off);
			}
			catch(Exception ex) { Report(ex); }
		}

		private void RestoreKeePassSaveImages()
		{
			if(!m_kpSaveSaved) return;
			try
			{
				MainForm mf = m_host.MainWindow;
				System.Reflection.FieldInfo fOn = KpField("m_imgFileSaveEnabled");
				System.Reflection.FieldInfo fOff = KpField("m_imgFileSaveDisabled");
				if((mf == null) || (fOn == null) || (fOff == null)) return;
				fOn.SetValue(mf, m_kpSaveOn);
				fOff.SetValue(mf, m_kpSaveOff);
				m_kpSaveSaved = false;
			}
			catch(Exception ex) { Report(ex); }
		}

		/// <summary>Speichern-Symbol nur farbig, wenn die Datenbank ungespeicherte Änderungen hat.</summary>
		private Image DesiredImage(ToolStripItem it, Image gen)
		{
			if((it != m_saveItem) || (m_saveOff == null)) return gen;
			bool modified = false;
			try
			{
				KeePassLib.PwDatabase db = m_host.Database;
				modified = ((db != null) && db.IsOpen && db.Modified);
			}
			catch(Exception) { modified = true; }
			return (modified ? gen : m_saveOff);
		}

		private void UpdateCardHeaders()
		{
			if(m_treeCard != null)
			{
				string db = string.Empty;
				try
				{
					if((m_host.Database != null) && m_host.Database.IsOpen)
					{
						string path = m_host.Database.IOConnectionInfo.Path;
						db = System.IO.Path.GetFileName(path);
						if(string.IsNullOrEmpty(db)) db = path;
					}
				}
				catch(Exception) { db = string.Empty; }
				m_treeCard.SetHeader(L.T("Gruppen"), db);
			}

			if((m_listCard != null) && (m_lv != null))
			{
				string title = L.T("Einträge");
				if((m_tv != null) && (m_tv.SelectedNode != null)) title = m_tv.SelectedNode.Text;
				if((m_tagFilter != null) && !ListShowsTag(m_tagFilter)) m_tagFilter = null;
				if(m_tagFilter != null) title = L.F("Tag „{0}“", m_tagFilter);

				int n = m_lv.Items.Count;
				int k = m_lv.SelectedIndices.Count;
				string sub = L.F((n == 1) ? "{0} Eintrag" : "{0} Einträge", n);
				if(k > 0) sub += " · " + L.F("{0} ausgewählt", k);
				m_listCard.SetHeader(title, sub);
			}
		}

		/// <summary>Tage, ab denen ablaufende Einträge ein oranges Badge bekommen (0 = aus).</summary>
		private int ExpiryDays
		{
			get
			{
				long v = m_host.CustomConfig.GetLong(CfgExpiryDays, 14);
				return (int)Math.Max(0, Math.Min(365, v));
			}
		}

		private void OnTagClicked(object sender, TagEventArgs e)
		{
			try
			{
				if((e == null) || string.IsNullOrEmpty(e.Tag)) return;
				if(KpCompat.ShowEntriesByTag(m_host, e.Tag))
				{
					m_tagFilter = e.Tag;
					UpdateCardHeaders();
				}
			}
			catch(Exception ex) { Report(ex); }
		}

		/// <summary>Beim Sperren schaltet KeePass den Baum ab; abgeschaltet malt Windows die leere
		/// Fläche weiß. Nach dem Entsperren Farbe setzen und alles neu zeichnen lassen.</summary>
		private void OnTreeEnabledChanged(object sender, EventArgs e)
		{
			try
			{
				if((m_tv == null) || (m_p == null)) return;
				if(m_tv.BackColor != m_p.Card) m_tv.BackColor = m_p.Card;
				m_tv.Invalidate();
			}
			catch(Exception ex) { Report(ex); }
		}

		private void OnTreeAfterSelect(object sender, TreeViewEventArgs e)
		{
			if(m_tagFilter == null) return;
			m_tagFilter = null;
			UpdateCardHeaders();
		}

		/// <summary>Prüft, ob die Liste noch das Tag-Ergebnis zeigt (sonst Überschrift zurücksetzen).</summary>
		private bool ListShowsTag(string tag)
		{
			if((m_lv == null) || (m_lv.Items.Count == 0)) return false;
			int n = Math.Min(m_lv.Items.Count, 50);
			for(int i = 0; i < n; ++i)
			{
				KeePassLib.PwEntry pe = EntryListPainter.GetEntry(m_lv.Items[i]);
				if((pe == null) || !KpCompat.HasTag(pe, tag)) return false;
			}
			return true;
		}

		/// <summary>Sekunden, nach denen ein angezeigtes Passwort wieder verborgen wird (0 = nie).</summary>
		private int RevealSeconds
		{
			get
			{
				long v = m_host.CustomConfig.GetLong(CfgReveal, 15);
				return (int)Math.Max(0, Math.Min(3600, v));
			}
		}

		/// <summary>„Fluent Theme x.y“ unten rechts in der Statusleiste anzeigen.</summary>
		private bool ShowVersion
		{
			get { return m_host.CustomConfig.GetBool(CfgShowVersion, true); }
		}

		private bool DetailsAutoHeight
		{
			get { return m_host.CustomConfig.GetBool(CfgAutoHeight, true); }
		}

		/// <summary>Die waagrechte Teilung zwischen Eintragsliste (oben) und Detailansicht (unten).</summary>
		private SplitContainer FindDetailsSplit()
		{
			Control c = ((m_details != null) ? m_details.Parent : null);
			while((c != null) && !(c is SplitterPanel)) c = c.Parent;
			if(c == null) return null;
			SplitContainer sc = (c.Parent as SplitContainer);
			if((sc == null) || (sc.Orientation != Orientation.Horizontal) || (sc.Panel2 != c)) return null;
			return sc;
		}

		private void OnDetailsSplitSizeChanged(object sender, EventArgs e)
		{
			FitDetails();
		}

		/// <summary>Höhe der Detailansicht automatisch an den Inhalt des ausgewählten Eintrags
		/// anpassen (höchstens 60 % der Fensterhöhe, die Liste behält immer etwas Platz).</summary>
		private void FitDetails()
		{
			if(m_fitting || (m_details == null) || !m_details.Visible || !this.DetailsAutoHeight) return;
			SplitContainer sc = m_detailsSplit;
			if((sc == null) || sc.IsDisposed || (sc.Height <= 0)) return;
			int need = m_details.ContentHeight;
			if(need <= 0) return; // kein Eintrag ausgewählt: Höhe beibehalten

			m_fitting = true;
			try
			{
				int extra = Math.Max(0, sc.Panel2.ClientSize.Height - m_details.Height);
				int want = need + extra;
				int total = sc.Height - sc.SplitterWidth;
				int maxH = Math.Min((total * 3) / 5, total - Math.Max(sc.Panel1MinSize, D(140)));
				want = Math.Max(Math.Max(sc.Panel2MinSize, D(60)), Math.Min(want, maxH));
				int dist = total - want;
				if(dist < sc.Panel1MinSize) return;
				if(Math.Abs(sc.SplitterDistance - dist) > 1) sc.SplitterDistance = dist;
			}
			catch(Exception ex) { Report(ex); }
			finally { m_fitting = false; }
		}

		private void OnEntrySelectionChanged(object sender, EventArgs e)
		{
			try
			{
				if((m_details != null) && m_details.Visible) m_details.RefreshIfChanged(false);
			}
			catch(Exception ex) { Report(ex); }
		}

		private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			try
			{
				MainForm mf = m_host.MainWindow;
				if((mf == null) || mf.IsDisposed || !mf.IsHandleCreated) return;
				mf.BeginInvoke(new MethodInvoker(OnSystemThemeMaybeChanged));
			}
			catch(Exception ex) { Report(ex); }
		}

		private void OnSystemThemeMaybeChanged()
		{
			if(m_p == null) return;
			bool dark = IsDark();
			Color acc = GetAccent();
			if((dark != m_p.Dark) || (acc.ToArgb() != m_p.Accent.ToArgb())) ApplyAll();
		}

		// ---------------------------------------------------------------- Menü

		public override ToolStripMenuItem GetMenuItem(PluginMenuType t)
		{
			if(t != PluginMenuType.Main) return null;

			ToolStripMenuItem root = new ToolStripMenuItem(L.T("Fluent-Design"));
			root.Image = AppIcon.GetImage(AppIcon.SmallSize);
			m_menuRoot = root;

			ToolStripMenuItem miSys = new ToolStripMenuItem(L.T("Wie Windows (automatisch)"));
			miSys.Click += delegate { SetMode(ThemeMode.System); };
			ToolStripMenuItem miLight = new ToolStripMenuItem(L.T("Hell"));
			miLight.Click += delegate { SetMode(ThemeMode.Light); };
			ToolStripMenuItem miDark = new ToolStripMenuItem(L.T("Dunkel"));
			miDark.Click += delegate { SetMode(ThemeMode.Dark); };
			root.DropDownItems.Add(miSys);
			root.DropDownItems.Add(miLight);
			root.DropDownItems.Add(miDark);
			root.DropDownItems.Add(new ToolStripSeparator());

			// Akzentfarbe
			ToolStripMenuItem miAccent = new ToolStripMenuItem(L.T("Akzentfarbe"));
			List<ToolStripMenuItem> accentItems = new List<ToolStripMenuItem>();
			string[,] accents = new string[,] {
				{ "Windows-Akzentfarbe (Standard)", "" },
				{ "Blau (wie Fensterpositionen)", "#1F6FD6" },
				{ "Dunkelblau", "#005FB8" },
				{ "Türkis", "#038387" },
				{ "Grün", "#0F7B0F" },
				{ "Lila", "#8A2BB8" },
				{ "Rot", "#C42B1C" },
				{ "Orange", "#CA5010" }
			};
			for(int i = 0; i < accents.GetLength(0); ++i)
			{
				string hex = accents[i, 1];
				ToolStripMenuItem mi = new ToolStripMenuItem(L.T(accents[i, 0]));
				mi.Tag = hex;
				Color sw = Palette.DefaultAccent;
				if(hex.Length == 0) sw = Palette.SystemAccent();
				else if(hex.Length > 0) sw = Palette.ParseHex(hex, Color.Gray);
				mi.Image = MakeSwatch(sw);
				mi.Click += delegate { SetAccent(hex); };
				miAccent.DropDownItems.Add(mi);
				accentItems.Add(mi);
			}
			miAccent.DropDownItems.Add(new ToolStripSeparator());
			ToolStripMenuItem miCustom = new ToolStripMenuItem(L.T("Eigene Farbe …"));
			miCustom.Click += delegate { PickCustomAccent(); };
			miAccent.DropDownItems.Add(miCustom);
			root.DropDownItems.Add(miAccent);

			// Zeilenhöhe
			ToolStripMenuItem miRows = new ToolStripMenuItem(L.T("Zeilenhöhe"));
			ToolStripMenuItem miR24 = new ToolStripMenuItem(L.T("Kompakt"));
			miR24.Click += delegate { SetRowHeight(24); };
			ToolStripMenuItem miR28 = new ToolStripMenuItem(L.T("Normal"));
			miR28.Click += delegate { SetRowHeight(28); };
			ToolStripMenuItem miR32 = new ToolStripMenuItem(L.T("Groß"));
			miR32.Click += delegate { SetRowHeight(32); };
			miRows.DropDownItems.Add(miR24);
			miRows.DropDownItems.Add(miR28);
			miRows.DropDownItems.Add(miR32);
			root.DropDownItems.Add(miRows);

			root.DropDownItems.Add(new ToolStripSeparator());
			ToolStripMenuItem miDetails = new ToolStripMenuItem(L.T("Moderne Detailansicht"));
			miDetails.Click += delegate
			{
				m_host.CustomConfig.SetBool(CfgDetails, !this.ModernDetails);
				ApplyAll();
			};
			root.DropDownItems.Add(miDetails);

			ToolStripMenuItem miAutoH = new ToolStripMenuItem(L.T("Höhe der Detailansicht automatisch anpassen"));
			miAutoH.Click += delegate
			{
				m_host.CustomConfig.SetBool(CfgAutoHeight, !this.DetailsAutoHeight);
				FitDetails();
			};
			root.DropDownItems.Add(miAutoH);

			ToolStripMenuItem miReveal = new ToolStripMenuItem(L.T("Angezeigtes Passwort wieder verbergen"));
			List<ToolStripMenuItem> revealItems = new List<ToolStripMenuItem>();
			foreach(int sec in new int[] { 10, 15, 30, 60, 0 })
			{
				string txt = ((sec == 0) ? L.T("Nie") : (L.F("Nach {0} Sekunden", sec) +
					((sec == 15) ? L.T(" (Standard)") : string.Empty)));
				ToolStripMenuItem mi = new ToolStripMenuItem(txt);
				mi.Tag = sec;
				int v = sec;
				mi.Click += delegate
				{
					m_host.CustomConfig.SetLong(CfgReveal, v);
					if(m_details != null) m_details.RevealSeconds = v;
				};
				revealItems.Add(mi);
				miReveal.DropDownItems.Add(mi);
			}
			root.DropDownItems.Add(miReveal);
			ToolStripMenuItem miHover = new ToolStripMenuItem(L.T("Hover-Effekt in der Eintragsliste"));
			miHover.Click += delegate
			{
				m_host.CustomConfig.SetBool(CfgHover, !this.ListHover);
				ApplyAll();
			};
			root.DropDownItems.Add(miHover);

			ToolStripMenuItem miExpiry = new ToolStripMenuItem(L.T("Ablaufende Einträge hervorheben"));
			List<ToolStripMenuItem> expiryItems = new List<ToolStripMenuItem>();
			foreach(int days in new int[] { 7, 14, 30, 0 })
			{
				string txt = ((days == 0) ? L.T("Nur abgelaufene") : (L.F("Bis {0} Tage vorher", days) +
					((days == 14) ? L.T(" (Standard)") : string.Empty)));
				ToolStripMenuItem mi = new ToolStripMenuItem(txt);
				mi.Tag = days;
				int v = days;
				mi.Click += delegate
				{
					m_host.CustomConfig.SetLong(CfgExpiryDays, v);
					if(m_painter != null) m_painter.ExpiryDays = v;
				};
				expiryItems.Add(mi);
				miExpiry.DropDownItems.Add(mi);
			}
			root.DropDownItems.Add(miExpiry);
			ToolStripMenuItem miIcons = new ToolStripMenuItem(L.T("Moderne Symbole in der Symbolleiste"));
			miIcons.Click += delegate
			{
				m_host.CustomConfig.SetBool(CfgIcons, !this.ModernIcons);
				ApplyAll();
			};
			root.DropDownItems.Add(miIcons);

			ToolStripMenuItem miVersion = new ToolStripMenuItem(L.T("Version in der Statusleiste"));
			miVersion.Click += delegate
			{
				m_host.CustomConfig.SetBool(CfgShowVersion, !this.ShowVersion);
				ApplyAll();
			};
			root.DropDownItems.Add(miVersion);

			root.DropDownItems.Add(new ToolStripSeparator());
			ToolStripMenuItem miErr = new ToolStripMenuItem(L.T("Fehlerbericht kopieren"));
			miErr.Click += delegate
			{
				string rep = ((s_lastError ?? string.Empty) + Environment.NewLine + s_toolbarInfo +
					Environment.NewLine + MenuStyler.MoveInfo).Trim();
				if(rep.Length == 0) return;
				Clipboard.SetText(rep);
				MessageBox.Show(m_host.MainWindow, L.T("Der Fehlerbericht wurde in die Zwischenablage kopiert."),
					"Fluent Theme", MessageBoxButtons.OK, MessageBoxIcon.Information);
			};
			root.DropDownItems.Add(miErr);

			root.DropDownItems.Add(new ToolStripSeparator());
			ToolStripMenuItem miAbout = new ToolStripMenuItem(L.T("Info über Fluent Theme …"));
			miAbout.Click += delegate { ShowAbout(); };
			root.DropDownItems.Add(miAbout);

			root.DropDownOpening += delegate
			{
				ThemeMode m = this.Mode;
				miSys.Checked = (m == ThemeMode.System);
				miLight.Checked = (m == ThemeMode.Light);
				miDark.Checked = (m == ThemeMode.Dark);

				string acc = this.AccentSetting;
				bool known = false;
				foreach(ToolStripMenuItem mi in accentItems)
				{
					bool on = string.Equals((string)mi.Tag, acc, StringComparison.OrdinalIgnoreCase);
					mi.Checked = on;
					known |= on;
				}
				miCustom.Checked = !known;

				int rh = this.RowHeight;
				miR24.Checked = (rh == 24);
				miR28.Checked = (rh == 28);
				miR32.Checked = (rh == 32);

				miDetails.Checked = this.ModernDetails;
				miAutoH.Checked = this.DetailsAutoHeight;
				int rs = this.RevealSeconds;
				foreach(ToolStripMenuItem mi in revealItems) mi.Checked = ((int)mi.Tag == rs);
				miReveal.Enabled = this.ModernDetails;
				miAutoH.Enabled = this.ModernDetails;
				miHover.Checked = this.ListHover;
				int ed = this.ExpiryDays;
				foreach(ToolStripMenuItem mi in expiryItems) mi.Checked = ((int)mi.Tag == ed);
				miIcons.Checked = this.ModernIcons;
				miVersion.Checked = this.ShowVersion;
				miErr.Enabled = (!string.IsNullOrEmpty(s_lastError) || (s_toolbarInfo.Length > 0) ||
					(MenuStyler.MoveInfo.Length > 0));
			};

			return root;
		}

		private static Image MakeSwatch(Color c)
		{
			Bitmap bmp = new Bitmap(16, 16);
			using(Graphics g = Graphics.FromImage(bmp))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.Clear(Color.Transparent);
				Gfx.Fill(g, new Rectangle(2, 2, 12, 12), 3, c);
			}
			return bmp;
		}

		// ---------------------------------------------------------------- Screenshots

		private ScreenshotRunner m_shots;
		private ToolStripMenuItem m_menuRoot;
		private int m_shotDark = -1; // -1 = normal, 0 = fest hell, 1 = fest dunkel

		internal MainForm ShotMainForm { get { return m_host.MainWindow; } }
		internal ListView ShotList { get { return m_lv; } }
		internal DetailsPanel ShotDetails { get { return m_details; } }
		internal Control ShotListCard { get { return ((m_listCard != null) ? m_listCard.Host : null); } }
		internal ToolStripMenuItem ShotMenu { get { return m_menuRoot; } }

		/// <summary>Hell oder dunkel für die Screenshots, ohne die gespeicherte Einstellung zu ändern
		/// (im Menü bleibt „Wie Windows“ angehakt).</summary>
		internal void ShotTheme(bool dark)
		{
			m_shotDark = (dark ? 1 : 0);
			ApplyAll();
		}

		private void SetMode(ThemeMode m)
		{
			m_host.CustomConfig.SetLong(CfgMode, (long)m);
			ApplyAll();
		}

		private void SetAccent(string hex)
		{
			m_host.CustomConfig.SetString(CfgAccent, hex ?? string.Empty);
			ApplyAll();
		}

		private void SetRowHeight(int h)
		{
			m_host.CustomConfig.SetLong(CfgRowHeight, h);
			ApplyAll();
		}

		private void PickCustomAccent()
		{
			using(ColorDialog dlg = new ColorDialog())
			{
				dlg.FullOpen = true;
				dlg.Color = ((m_p != null) ? m_p.Accent : GetAccent());
				if(dlg.ShowDialog(m_host.MainWindow) == DialogResult.OK) SetAccent(Palette.ToHex(dlg.Color));
			}
		}

		// ---------------------------------------------------------------- Hilfen

		private static T FindControl<T>(Control root, string name) where T : Control
		{
			Control[] found = root.Controls.Find(name, true);
			foreach(Control c in found)
			{
				T t = (c as T);
				if(t != null) return t;
			}
			List<T> all = FindAll<T>(root);
			return ((all.Count > 0) ? all[0] : null);
		}

		private static List<T> FindAll<T>(Control root) where T : Control
		{
			List<T> l = new List<T>();
			Collect<T>(root, l);
			return l;
		}

		private static void Collect<T>(Control c, List<T> l) where T : Control
		{
			foreach(Control child in c.Controls)
			{
				T t = (child as T);
				if(t != null) l.Add(t);
				Collect<T>(child, l);
			}
		}
	}
}
