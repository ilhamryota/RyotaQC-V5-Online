; ============================================================================ 
; RyotaQC V5 Online - Project EsteraOne
; Inno Setup Script - repack-style installer
; Author: IlhamRyota
;
; Design notes (v5.0.3):
;  * Native Inno Setup light theme is preserved - every label, checkbox,
;    memo and footer uses native black text on light background for maximum
;    readability.
;  * bgg.jpg artwork is used on the side panel (WizardImageFile).
;  * No dark footer band, no BGA.jpg finish background - all fonts are
;    black on the wizard's native light surface.
;  * Installing page shows live elapsed / remaining / total time estimates
;    plus a status caption that follows the install progress.
; ============================================================================

#ifndef AppVersion
  #define AppVersion "5.0.3"
#endif

#define AppName "RyotaQC V5"
#define AppPublisher "RyotaQC"
#define AppExeName "RyotaQC.App.exe"
#define AppSourceDir "..\publish"
#define OutputDir "..\release"

[Setup]
AppId={{F4FCF1F5-916B-4D6D-9A99-0A94C4A8A6B7}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion} - Project EsteraOne
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/ilhamryota/RyotaQC-V5-Online
AppSupportURL=https://github.com/ilhamryota/RyotaQC-V5-Online/issues
AppUpdatesURL=https://github.com/ilhamryota/RyotaQC-V5-Online/releases
AppCopyright=Copyright (c) RyotaQC V5

DefaultDirName={autopf}\RyotaQC\V5
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

OutputDir={#OutputDir}
OutputBaseFilename=RyotaQC-V5-Setup-x64-v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

PrivilegesRequired=admin

WizardStyle=modern

; === Artwork ===
; Side panel artwork (bgg.jpg) for the left wizard panel.
WizardImageFile=installer-side.bmp
WizardSmallImageFile=installer-small.bmp

SetupIconFile=..\src\RyotaQC.App\Assets\Images\Tiles\logo.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

DiskSpanning=no

ShowLanguageDialog=no
CloseApplications=yes
RestartApplications=yes
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup - Project EsteraOne

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
Name: "quicklaunchicon"; Description: "Create a Quick Launch shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Comment: "Open RyotaQC V5 - Project EsteraOne"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"; Comment: "Remove RyotaQC V5 from this computer"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; Comment: "Open RyotaQC V5 - Project EsteraOne"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[CustomMessages]
english.WelcomeLabel1=Welcome to RyotaQC V5 Setup
english.WelcomeLabel2=This will install RyotaQC V5 - Project EsteraOne on your computer.%n%nOnline QC Edition with ERP sync, 15 QC tests, and bundled technician tools.%n%nClick Next to continue.
english.FinishedLabel=RyotaQC V5 has been installed successfully. All QC tools and ERP connector are ready to use.
english.FinishedHeadingLabel=Installation Complete
english.SelectDirLabel3=Setup will install RyotaQC V5 into the following folder.
english.SelectTasksLabel2=Select the additional tasks you would like Setup to perform while installing RyotaQC V5, then click Next.
english.ReadyLabel1=Setup is now ready to begin installing RyotaQC V5 on your computer.
english.ReadyLabel2=Click Install to continue with the installation, or click Back if you want to review or change any settings.

[Code]
var
  InstallStartTick: LongWord;
  LastCurProgress: Integer;
  LastMaxProgress: Integer;
  Pct2: Integer;

  ProgressInfoLabel: TNewStaticText;
  RemainingInfoLabel: TNewStaticText;
  TotalEstimateLabel: TNewStaticText;
  InstallMessageLabel: TNewStaticText;

function GetTickCount: LongWord;
external 'GetTickCount@kernel32.dll stdcall';

function FormatDuration(SecondsValue: Integer): string;
var
  HoursValue, MinutesValue, SecsValue: Integer;
begin
  if SecondsValue < 0 then
    SecondsValue := 0;
  HoursValue := SecondsValue div 3600;
  MinutesValue := (SecondsValue mod 3600) div 60;
  SecsValue := SecondsValue mod 60;
  Result := Format('%.2d:%.2d:%.2d', [HoursValue, MinutesValue, SecsValue]);
end;

procedure InitializeWizard;
begin
  { Timing + status labels on the Installing page. Native Inno Setup colors
    (black text on light background) are kept so every label stays readable. }
  ProgressInfoLabel := TNewStaticText.Create(WizardForm);
  ProgressInfoLabel.Parent := WizardForm.InstallingPage;
  ProgressInfoLabel.Left := WizardForm.ProgressGauge.Left;
  ProgressInfoLabel.Top := WizardForm.ProgressGauge.Top + WizardForm.ProgressGauge.Height + ScaleY(10);
  ProgressInfoLabel.Width := WizardForm.ProgressGauge.Width;
  ProgressInfoLabel.Font.Size := 8;
  ProgressInfoLabel.Font.Color := $00000000;
  ProgressInfoLabel.Caption := 'Elapsed: 00:00:00   |   Progress: 0%';

  RemainingInfoLabel := TNewStaticText.Create(WizardForm);
  RemainingInfoLabel.Parent := WizardForm.InstallingPage;
  RemainingInfoLabel.Left := ProgressInfoLabel.Left;
  RemainingInfoLabel.Top := ProgressInfoLabel.Top + ProgressInfoLabel.Height + ScaleY(2);
  RemainingInfoLabel.Width := WizardForm.ProgressGauge.Width;
  RemainingInfoLabel.Font.Size := 8;
  RemainingInfoLabel.Font.Color := $00000000;
  RemainingInfoLabel.Caption := 'Estimated remaining: calculating...';

  TotalEstimateLabel := TNewStaticText.Create(WizardForm);
  TotalEstimateLabel.Parent := WizardForm.InstallingPage;
  TotalEstimateLabel.Left := RemainingInfoLabel.Left;
  TotalEstimateLabel.Top := RemainingInfoLabel.Top + RemainingInfoLabel.Height + ScaleY(2);
  TotalEstimateLabel.Width := WizardForm.ProgressGauge.Width;
  TotalEstimateLabel.Font.Size := 8;
  TotalEstimateLabel.Font.Color := $00000000;
  TotalEstimateLabel.Caption := 'Estimated total duration: calculating...';

  InstallMessageLabel := TNewStaticText.Create(WizardForm);
  InstallMessageLabel.Parent := WizardForm.InstallingPage;
  InstallMessageLabel.Left := WizardForm.ProgressGauge.Left;
  InstallMessageLabel.Top := TotalEstimateLabel.Top + TotalEstimateLabel.Height + ScaleY(10);
  InstallMessageLabel.Width := WizardForm.ProgressGauge.Width;
  InstallMessageLabel.Alignment := taCenter;
  InstallMessageLabel.Font.Style := [fsBold];
  InstallMessageLabel.Font.Size := 10;
  InstallMessageLabel.Font.Color := $00000000;
  InstallMessageLabel.Caption := 'Preparing installation...';

  InstallStartTick := GetTickCount;
end;

function GetElapsedSeconds: Integer;
begin
  Result := Integer((GetTickCount - InstallStartTick) div 1000);
  if Result < 0 then
    Result := 0;
end;

procedure UpdateInstallTimingLabels;
var
  ElapsedSeconds, RemainingSeconds, TotalSeconds: Integer;
  ProgressRatio: Extended;
begin
  ElapsedSeconds := GetElapsedSeconds;

  if LastMaxProgress <= 0 then
  begin
    ProgressInfoLabel.Caption := Format('Elapsed: %s   |   Progress: 0%%', [FormatDuration(ElapsedSeconds)]);
    RemainingInfoLabel.Caption := 'Estimated remaining: calculating...';
    TotalEstimateLabel.Caption := 'Estimated total duration: calculating...';
    exit;
  end;

  ProgressRatio := LastCurProgress / LastMaxProgress;

  if ProgressRatio > 0 then
  begin
    TotalSeconds := Round(ElapsedSeconds / ProgressRatio);
    RemainingSeconds := TotalSeconds - ElapsedSeconds;
  end
  else
  begin
    TotalSeconds := 0;
    RemainingSeconds := 0;
  end;

  if RemainingSeconds < 0 then
    RemainingSeconds := 0;
  if TotalSeconds < 0 then
    TotalSeconds := 0;

  ProgressInfoLabel.Caption := Format('Elapsed: %s   |   Progress: %d%%', [FormatDuration(ElapsedSeconds), (LastCurProgress * 100) div LastMaxProgress]);
  RemainingInfoLabel.Caption := 'Estimated remaining: ' + FormatDuration(RemainingSeconds);
  TotalEstimateLabel.Caption := 'Estimated total duration: ' + FormatDuration(TotalSeconds);

  if LastMaxProgress > 0 then
  begin
    Pct2 := (LastCurProgress * 100) div LastMaxProgress;
    if Pct2 < 20 then
      InstallMessageLabel.Caption := 'Extracting core application files...'
    else if Pct2 < 45 then
      InstallMessageLabel.Caption := 'Installing Quality Control tools...'
    else if Pct2 < 70 then
      InstallMessageLabel.Caption := 'Deploying hardware utilities...'
    else if Pct2 < 90 then
      InstallMessageLabel.Caption := 'Configuring ERP connector...'
    else if Pct2 < 99 then
      InstallMessageLabel.Caption := 'Creating desktop shortcuts...'
    else
      InstallMessageLabel.Caption := 'Finalizing installation...';
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { Native Inno Setup light theme - no chrome overrides, no dark backgrounds.
    All wizard labels use their default black text on light surface. }
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    InstallStartTick := GetTickCount;
    LastCurProgress := 0;
    LastMaxProgress := 0;
    UpdateInstallTimingLabels;
  end;

  if (CurStep = ssPostInstall) or (CurStep = ssDone) then
    UpdateInstallTimingLabels;
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
begin
  LastCurProgress := CurProgress;
  LastMaxProgress := MaxProgress;
  UpdateInstallTimingLabels;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
