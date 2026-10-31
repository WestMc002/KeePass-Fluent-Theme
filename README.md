# KeePass Fluent Theme

Modernes Windows-11-Design für [KeePass 2.x](https://keepass.info/) – Hell/Dunkel, Windows-Akzentfarbe, Detailansicht mit Karten, Anzeige von TOTP-Einmalcodes und Ablauf-Hinweisen. Das gewohnte KeePass-Layout bleibt erhalten: Gruppenbaum links, Eintragsliste rechts, Detailansicht darunter.

Autor: Günther Schoder · [www.westmc002.de](https://www.westmc002.de)

<table>
  <tr>
    <td><img src="Screenshots/hell.png" alt="Hauptfenster im hellen Modus"></td>
    <td><img src="Screenshots/dunkel.png" alt="Hauptfenster im dunklen Modus"></td>
  </tr>
  <tr>
    <td align="center">Hell</td>
    <td align="center">Dunkel</td>
  </tr>
</table>

## Funktionen

- **Windows-11-Farben** im hellen und dunklen Modus – folgt Windows oder fest eingestellt
- **Windows-Akzentfarbe** oder eine eigene Akzentfarbe
- **Moderne Oberfläche:** Menüs mit abgerundeten Ecken, farbige Fluent-Symbole in der Symbolleiste, Registerkarten, schmale Scrollleisten, modernes Suchfeld
- **Gruppenbaum und Eintragsliste** mit abgerundeter Auswahl, Akzentstreifen und Hover-Effekt; Zeilenhöhe einstellbar
- **Ablauf-Hinweise:** rotes Badge „Abgelaufen“, oranges Badge „in x Tagen“ (Zeitspanne einstellbar)

<table>
  <tr>
    <td><img src="Screenshots/ablauf-hell.png" alt="Ablauf-Hinweise in der Eintragsliste, hell"></td>
    <td><img src="Screenshots/ablauf-dunkel.png" alt="Ablauf-Hinweise in der Eintragsliste, dunkel"></td>
  </tr>
</table>

- **Detailansicht als Karten**
  - Anmeldedaten mit Kopieren-, Anzeigen- und Öffnen-Knöpfen und Passwortstärke
  - **Anzeige des Einmalcodes (TOTP)** mit Countdown und Kopieren-Knopf – liest die `TimeOtp-*`-Felder von KeePass sowie ein Feld `otp` im Format von KeePassXC/KeeOtp. Eingerichtet wird die Zwei-Faktor-Anmeldung weiterhin in KeePass bzw. einem TOTP-Plugin; das Fluent Theme zeigt den Code nur an.
  - weitere Felder (z. B. „URL 2“), Links anklickbar
  - Notizen mit Kopieren-Knopf, lange Notizen aufklappbar
  - Anhänge per Klick öffnen oder speichern
  - Tags anklickbar (zeigt alle Einträge mit diesem Tag)
- **„Kopiert“-Hinweis** mit der Zeit, nach der KeePass die Zwischenablage leert
- Angezeigtes Passwort wird nach einstellbarer Zeit **automatisch wieder verborgen**
- Höhe der Detailansicht passt sich **automatisch** an den Eintrag an

<table>
  <tr>
    <td><img src="Screenshots/details-hell.png" alt="Detailansicht mit Einmalcode und Kopiert-Hinweis, hell"></td>
    <td><img src="Screenshots/details-dunkel.png" alt="Detailansicht mit Einmalcode und Kopiert-Hinweis, dunkel"></td>
  </tr>
</table>

- **Gestaltete KeePass-Dialoge:** Kopfbereich, Registerkarten, Inhalt auf einer Karte, einheitliche Eingabefelder mit Akzentlinie, Qualitätsbalken in Ampelfarben, Knöpfe – gilt für alle KeePass-Dialoge (Eintrag, Gruppe, Optionen, Passwort-Generator …)

<table>
  <tr>
    <td><img src="Screenshots/dialog-hell.png" alt="Dialog Eintrag bearbeiten, hell"></td>
    <td><img src="Screenshots/dialog-dunkel.png" alt="Dialog Eintrag bearbeiten, dunkel"></td>
  </tr>
</table>

Alle Einstellungen: **Extras › Fluent-Design**.

<table>
  <tr>
    <td><img src="Screenshots/menue-hell.png" alt="Menü Fluent-Design, hell"></td>
    <td><img src="Screenshots/menue-dunkel.png" alt="Menü Fluent-Design, dunkel"></td>
  </tr>
</table>

## Installation

1. KeePass beenden.
2. `FluentTheme.dll` aus den [Releases](https://github.com/WestMc002/KeePass-Fluent-Theme/releases) in den Ordner `Plugins` von KeePass kopieren, z. B. `C:\Program Files (x86)\KeePass\Plugins` (Administratorrechte nötig).
3. KeePass starten.

Voraussetzungen: KeePass 2.x unter Windows 10 oder 11 (runde Ecken und einige Details nur unter Windows 11).

Updates: KeePass meldet neue Versionen unter **Hilfe › Nach Updates suchen** (Datei `keepass.version`).

## Selbst kompilieren

`Erstellen.bat` im Projektordner starten. Sie

- kompiliert mit dem C#-Compiler des .NET Framework 4 (keine weitere Software nötig),
- vergibt die Versionsnummer automatisch (Haupt.Neben.Jahr.MonatTag),
- sichert das Projekt unter `Backup`,
- kopiert die DLL in den Plugins-Ordner und startet KeePass neu.

Der KeePass-Pfad ist in der `Erstellen.bat` im Block „Einstellungen“ hinterlegt (`KPDIR`).

## Projektaufbau

| Ordner / Datei | Inhalt |
|---|---|
| `Quellcode` | C#-Quellcode des Plugins |
| `Resources` | eingebettete Ressourcen (Logo) |
| `Screenshots` | Bilder für diese Seite (aus einer Demo-Datenbank) |
| `Erstellen.bat` | Build-Skript |
| `Liesmich.txt` | Anleitung |
| `keepass.version` | Versionsdatei für die Update-Prüfung von KeePass |

## Lizenz

GNU General Public License v3.0 oder später – siehe [LICENSE](LICENSE).

© 2026 Günther Schoder

**Ausnahme:** Das GS-Logo (`Resources/GS-Logo.png`) ist von der GPL ausgenommen. Alle Rechte vorbehalten; es darf nur zusammen mit diesem Plugin in unveränderter Form weitergegeben werden.

KeePass ist ein Projekt von Dominik Reichl. Dieses Plugin ist ein unabhängiges Projekt und nicht mit KeePass verbunden.

„Fluent“ bezieht sich auf den Gestaltungsstil von Windows 11. Dieses Projekt ist nicht mit Microsoft verbunden und wird nicht von Microsoft unterstützt; Windows ist eine Marke der Microsoft Corporation.
