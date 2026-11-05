@ECHO OFF
SETLOCAL EnableExtensions
ECHO #######################################
ECHO #  Fluent Theme erstellen             #
ECHO #                                     #
ECHO #  Autor: Guenther Schoder            #
ECHO #         www.westmc002.de            #
ECHO #                                     #
ECHO #  Datum: 29.10.2026 / 05.11.2026     #
ECHO #######################################
ECHO.

CD /D "%~dp0"
SET "RESULT=0"

REM Einstellungen (projektspezifisch)
REM MAJOR     = Hauptversion
REM NAME      = Programmname = Name der DLL ohne .dll (auch Backup-Ordner)
REM NAMESPACE = C#-Namespace, in dem die Klasse AppInfo liegt
REM SRCDIR    = Ordner mit allen .cs-Dateien (VersionInfo.cs wird dort erzeugt)
REM RESDIR    = Ordner mit GS-Logo.png und FluentTheme.ico (Icon des Plugins)
REM KPDIR     = KeePass-Programmordner
REM EXTRAREFS = zusaetzliche Verweise (hier KeePass.exe)
REM Abweichung von der Vorlage: Plugin-DLL in Bin statt EXE, kein Icon,
REM danach Kopie in den KeePass-Plugins-Ordner und Start von KeePass
SET "MAJOR=1"
SET "NAME=FluentTheme"
SET "NAMESPACE=FluentTheme"
SET "SRCDIR=Quellcode"
SET "RESDIR=Resources"
SET "KPDIR=C:\Program Files (x86)\KeePass"
SET "EXTRAREFS="/r:%KPDIR%\KeePass.exe""
SET "DLL=Bin\%NAME%.dll"
SET "NEWDLL=Bin\%NAME%.neu.dll"

REM Administratorrechte fuer den KeePass-Plugins-Ordner anfordern
NET SESSION >NUL 2>&1
IF ERRORLEVEL 1 GOTO :ELEVATE

REM C#-Compiler aus dem .NET Framework 4 suchen
SET "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
IF NOT EXIST "%CSC%" SET "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
IF NOT EXIST "%CSC%" GOTO :ERR_CSC
IF NOT EXIST "%KPDIR%\KeePass.exe" GOTO :ERR_KEEPASS

REM KeePass muss beendet sein (sonst ist die Plugin-DLL gesperrt)
:WAITEXE
powershell -NoProfile -Command "if(Get-Process -Name 'KeePass' -ErrorAction SilentlyContinue){exit 1}"
IF NOT ERRORLEVEL 1 GOTO :VERSION
ECHO KeePass laeuft noch. Bitte beenden und dann eine Taste druecken.
PAUSE >NUL
GOTO :WAITEXE

:VERSION
REM Versionsnummer: Haupt.Neben.Jahr.MonatTag
REM Nebenversion = hoechste vorhandene (aktuelle DLL und Backup) + 1
SET "MINOR="
FOR /F "usebackq delims=" %%M IN (`powershell -NoProfile -Command "$m=0; if(Test-Path -LiteralPath '%DLL%'){ $v=(Get-Item -LiteralPath '%DLL%').VersionInfo.FileVersion; if($v -match '^(\d+)\.(\d+)' -and [int]$Matches[1] -eq %MAJOR%){ $m=[int]$Matches[2]+1 } }; if(Test-Path -LiteralPath 'Backup'){ foreach($f in Get-ChildItem -LiteralPath 'Backup'){ if($f.Name -match ' v(\d+)\.(\d+)\.' -and [int]$Matches[1] -eq %MAJOR% -and [int]$Matches[2] -ge $m){ $m=[int]$Matches[2]+1 } } }; $m"`) DO SET "MINOR=%%M"
IF "%MINOR%"=="" GOTO :ERR_VERSION
FOR /F "usebackq delims=" %%V IN (`powershell -NoProfile -Command "Get-Date -Format 'yyyy.MMdd'"`) DO SET "DATEVER=%%V"
SET "VER=%MAJOR%.%MINOR%.%DATEVER%"
ECHO KeePass-Ordner: %KPDIR%
ECHO Neue Version:   %VER%
ECHO.

REM Versionsdatei fuer den Compiler erzeugen
> "%SRCDIR%\VersionInfo.cs" ECHO [assembly: System.Reflection.AssemblyVersion("%VER%")]
>> "%SRCDIR%\VersionInfo.cs" ECHO [assembly: System.Reflection.AssemblyFileVersion("%VER%")]
>> "%SRCDIR%\VersionInfo.cs" ECHO [assembly: System.Reflection.AssemblyInformationalVersion("%VER%")]
>> "%SRCDIR%\VersionInfo.cs" ECHO namespace %NAMESPACE% { public static partial class AppInfo { public const string Version = "%VER%"; } }

REM Kompilieren (zuerst in eine neue Datei, die alte DLL bleibt bei Fehlern erhalten)
IF NOT EXIST "Bin" MKDIR "Bin"
IF EXIST "%NEWDLL%" DEL /F /Q "%NEWDLL%"
ECHO Kompiliere %DLL% ...
"%CSC%" /nologo /target:library /optimize+ /codepage:65001 "/out:%NEWDLL%" "/resource:%RESDIR%\GS-Logo.png,GS-Logo.png" "/resource:%RESDIR%\FluentTheme.ico,FluentTheme.ico" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll %EXTRAREFS% "%SRCDIR%\*.cs" > Erstellen.log 2>&1
SET "RESULT=%ERRORLEVEL%"
TYPE Erstellen.log
IF NOT "%RESULT%"=="0" GOTO :ERR_BUILD

REM Bisherige DLL sichern, falls ihre Version noch keinen Ordner in Backup hat
IF NOT EXIST "%DLL%" GOTO :INSTALL
SET "OLDVER="
FOR /F "usebackq delims=" %%O IN (`powershell -NoProfile -Command "(Get-Item -LiteralPath '%DLL%').VersionInfo.FileVersion"`) DO SET "OLDVER=%%O"
IF "%OLDVER%"=="" SET "OLDVER=unbekannt"
SET "OLDDIR=Backup\%NAME% v%OLDVER%"
IF EXIST "%OLDDIR%\" GOTO :INSTALL
MKDIR "%OLDDIR%\Bin"
MOVE /Y "%DLL%" "%OLDDIR%\%DLL%" >NUL
IF ERRORLEVEL 1 GOTO :ERR_BACKUP
ECHO Backup der bisherigen DLL: %OLDDIR%

:INSTALL
MOVE /Y "%NEWDLL%" "%DLL%" >NUL
IF ERRORLEVEL 1 GOTO :ERR_INSTALL
COLOR A0
ECHO Erfolgreich erstellt: %DLL% (Version %VER%)

REM Projektspezifisch: Versionsdatei fuer die Update-Pruefung von KeePass (liegt auf GitHub)
REM Name vor dem Doppelpunkt = AssemblyTitle des Plugins
> "keepass.version" ECHO :
>> "keepass.version" ECHO Fluent Theme:%VER%
>> "keepass.version" ECHO :

REM Projektspezifisch: DLL in den KeePass-Plugins-Ordner kopieren
IF NOT EXIST "%KPDIR%\Plugins" MKDIR "%KPDIR%\Plugins"
COPY /Y "%DLL%" "%KPDIR%\Plugins\%NAME%.dll" >NUL
IF ERRORLEVEL 1 GOTO :ERR_COPY
ECHO Kopiert nach: %KPDIR%\Plugins\%NAME%.dll

REM Projektspezifisch: Screenshots fuer GitHub aus Demo-Daten (hell und dunkel) nach Screenshots,
REM in einer eigenen portablen KeePass-Kopie (Design\KeePass-Portable, kommt nicht auf GitHub);
REM nur auf Nachfrage: ohne Antwort nach 10 Sekunden Nein, bei /nopause nie
SET "PORTKP=Design\KeePass-Portable"
IF /I "%~1"=="/nopause" GOTO :NOSHOTS
IF NOT EXIST "%PORTKP%\KeePass.exe" GOTO :NOSHOTS
ECHO.
CHOICE /C JN /T 10 /D N /M "Screenshots fuer GitHub erstellen (nach 10 s automatisch Nein)"
IF ERRORLEVEL 2 GOTO :NOSHOTS
IF NOT EXIST "%PORTKP%\Plugins" MKDIR "%PORTKP%\Plugins"
COPY /Y "%DLL%" "%PORTKP%\Plugins\%NAME%.dll" >NUL
ECHO Erstelle Screenshots (bitte Maus und Tastatur kurz nicht benutzen) ...
START "" /WAIT "%CD%\%PORTKP%\KeePass.exe" "-fluent-screenshots:%CD%\Screenshots"
IF EXIST "Screenshots\Fehler.txt" (ECHO Hinweis: Screenshots fehlgeschlagen - siehe Screenshots\Fehler.txt) ELSE (ECHO Screenshots erstellt.)
:NOSHOTS

REM Sicherung: komplettes Projekt nach Backup\<Projekt> v<Version>
REM (ohne die portable KeePass-Kopie fuer die Screenshots)
SET "BAKDIR=Backup\%NAME% v%VER%"
ECHO.
ECHO Sichere nach %BAKDIR% ...
ROBOCOPY "%CD%" "%CD%\%BAKDIR%" /E /XD "%CD%\Backup" "%CD%\Design\KeePass-Portable" /XF "%NAME%.neu.dll" "Erstellen.log" /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >NUL
IF ERRORLEVEL 8 GOTO :ERR_SNAPSHOT
ECHO Gesichert.

REM Projektspezifisch: KeePass ueber den Explorer starten (ohne Administratorrechte)
ECHO KeePass wird gestartet ...
START "" explorer.exe "%KPDIR%\KeePass.exe"
GOTO :Ende

:ELEVATE
ECHO Administratorrechte werden angefordert ...
IF "%~1"=="" (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
) ELSE (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%~1' -Verb RunAs"
)
ENDLOCAL & EXIT /B 0

:ERR_CSC
COLOR C0
SET "RESULT=1"
ECHO Fehler: csc.exe aus .NET Framework 4 wurde nicht gefunden.
GOTO :Ende

:ERR_KEEPASS
COLOR C0
SET "RESULT=1"
ECHO Fehler: KeePass.exe wurde nicht gefunden in %KPDIR%
GOTO :Ende

:ERR_VERSION
COLOR C0
SET "RESULT=1"
ECHO Fehler: Die neue Versionsnummer konnte nicht ermittelt werden.
GOTO :Ende

:ERR_BUILD
COLOR C0
ECHO.
ECHO Fehler beim Kompilieren - siehe Erstellen.log
GOTO :Ende

:ERR_BACKUP
COLOR C0
SET "RESULT=1"
ECHO Fehler: Die alte DLL konnte nicht nach Backup verschoben werden.
IF EXIST "%NEWDLL%" DEL /F /Q "%NEWDLL%"
GOTO :Ende

:ERR_INSTALL
COLOR C0
SET "RESULT=1"
ECHO Fehler: Die neue DLL konnte nicht an ihren Platz verschoben werden.
ECHO Sie liegt als "%NEWDLL%" im Projektordner.
GOTO :Ende

:ERR_COPY
COLOR C0
SET "RESULT=1"
ECHO Fehler: Kopieren in den KeePass-Plugins-Ordner fehlgeschlagen.
GOTO :Ende

:ERR_SNAPSHOT
COLOR C0
SET "RESULT=1"
ECHO Sicherung fehlgeschlagen.

:Ende
ECHO.
IF /I NOT "%~1"=="/nopause" PAUSE
ENDLOCAL & EXIT /B %RESULT%