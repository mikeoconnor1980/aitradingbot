; TradingApp Execution Agent — Inno Setup Script
; Produces a standard Windows installer with private key prompt,
; Windows Service registration, and silent install support.
;
; Build with: iscc.exe /DAppVersion=0.1.0 installer.iss
; Requires Inno Setup 6+ (https://jrsoftware.org/isinfo.php)

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define AppName "TradingApp Execution Agent"
#define AppPublisher "TradingApp"
#define AppExeName "TradingApp.ExecutionAgent.exe"
#define ServiceName "TradingApp.ExecutionAgent"

[Setup]
AppId={{B7E3F8A1-4D2C-4F5E-A9B1-3C6D8E0F2A4B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\TradingApp\ExecutionAgent
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\..\artifacts\installer
OutputBaseFilename=TradingApp-ExecutionAgent-v{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
MinVersion=10.0
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
ArchitecturesInstallIn64BitMode=x64compatible
SetupLogging=yes
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Published binary and config — sourced from the build output
Source: "..\..\artifacts\publish\worker\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Dirs]
Name: "{app}\data"; Permissions: users-modify
Name: "{app}\logs"; Permissions: users-modify

[UninstallDelete]
; Remove logs on uninstall (data is preserved — see [Code])
Type: filesandordirs; Name: "{app}\logs"

[Code]
var
  PrivateKeyPage: TInputQueryWizardPage;
  PrivateKeyAlreadySet: Boolean;

const
  ENV_VAR_NAME = 'Hyperliquid__PrivateKey';

// ---- Helper: run a command and return exit code ----
function RunCmd(const Cmd, Params: String; var ResultCode: Integer): Boolean;
begin
  Result := Exec(Cmd, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// ---- Check if env var is already set (upgrade scenario) ----
function IsPrivateKeyConfigured: Boolean;
var
  Value: String;
begin
  Result := RegQueryStringValue(HKLM,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    ENV_VAR_NAME, Value) and (Value <> '');
end;

// ---- Validate hex key format: 0x + 64 hex chars ----
function IsValidPrivateKey(const Key: String): Boolean;
var
  I: Integer;
  C: Char;
begin
  Result := False;
  if Length(Key) <> 66 then Exit;
  if Copy(Key, 1, 2) <> '0x' then Exit;
  for I := 3 to 66 do
  begin
    C := Key[I];
    if not ((C >= '0') and (C <= '9')) and
       not ((C >= 'a') and (C <= 'f')) and
       not ((C >= 'A') and (C <= 'F')) then
      Exit;
  end;
  Result := True;
end;

// ---- Stop existing service if running ----
procedure StopExistingService;
var
  ResultCode: Integer;
begin
  // Try to stop — ignore errors if service doesn't exist
  RunCmd(ExpandConstant('{sys}\sc.exe'), ExpandConstant('stop {#ServiceName}'), ResultCode);
  if ResultCode = 0 then
    Sleep(3000); // Wait for service to stop
  // Delete existing service registration
  RunCmd(ExpandConstant('{sys}\sc.exe'), ExpandConstant('delete {#ServiceName}'), ResultCode);
  if ResultCode = 0 then
    Sleep(1000);
end;

// ---- Register and start the Windows Service ----
procedure RegisterService;
var
  ResultCode: Integer;
  ExePath: String;
begin
  ExePath := ExpandConstant('{app}\{#AppExeName}');

  // Create service with delayed-auto start
  RunCmd(ExpandConstant('{sys}\sc.exe'),
    Format('create %s binPath= "\"%s\"" start= delayed-auto DisplayName= "%s"', ['{#ServiceName}', ExePath, '{#AppName}']),
    ResultCode);

  if ResultCode <> 0 then
  begin
    MsgBox('Failed to register Windows Service. You may need to register it manually.', mbError, MB_OK);
    Exit;
  end;

  // Set description
  RunCmd(ExpandConstant('{sys}\sc.exe'),
    Format('description %s "Executes trading strategies on Hyperliquid. Private key never leaves this machine."', ['{#ServiceName}']),
    ResultCode);

  // Set recovery policy: restart on failure (30s, 60s, 120s)
  RunCmd(ExpandConstant('{sys}\sc.exe'),
    Format('failure %s reset= 86400 actions= restart/30000/restart/60000/restart/120000', ['{#ServiceName}']),
    ResultCode);

  // Start the service
  RunCmd(ExpandConstant('{sys}\sc.exe'),
    Format('start %s', ['{#ServiceName}']), ResultCode);

  if ResultCode <> 0 then
    Log('Service registered but failed to start. Check Event Viewer for details.');
end;

// ---- Set machine-level environment variable for private key ----
procedure SetPrivateKeyEnvVar(const Key: String);
begin
  RegWriteStringValue(HKLM,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    ENV_VAR_NAME, Key);
end;

// ---- Remove private key env var (silent uninstall support) ----
procedure RemovePrivateKeyEnvVar;
begin
  RegDeleteValue(HKLM,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    ENV_VAR_NAME);
end;

// ---- Wizard init: add private key input page ----
procedure InitializeWizard;
begin
  PrivateKeyAlreadySet := IsPrivateKeyConfigured;

  PrivateKeyPage := CreateInputQueryPage(wpSelectDir,
    'Hyperliquid Private Key',
    'Your private key is used to sign orders. It NEVER leaves this machine.',
    'Enter your Hyperliquid private key (0x followed by 64 hex characters).' + #13#10 +
    'The key will be stored as a machine-level environment variable.');

  PrivateKeyPage.Add('Private Key:', False);

  if PrivateKeyAlreadySet then
    PrivateKeyPage.SubCaptionLabel.Caption :=
      'A private key is already configured. Leave blank to keep the existing key.';
end;

// ---- Skip key page in very-silent mode ----
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = PrivateKeyPage.ID then
  begin
    // Skip in silent/very-silent mode
    if WizardSilent then
      Result := True;
  end;
end;

// ---- Validate private key input ----
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Key: String;
begin
  Result := True;

  if CurPageID = PrivateKeyPage.ID then
  begin
    Key := Trim(PrivateKeyPage.Values[0]);

    // Allow blank if key already exists (upgrade scenario)
    if (Key = '') and PrivateKeyAlreadySet then
      Exit;

    // Require key on fresh install
    if (Key = '') and (not PrivateKeyAlreadySet) then
    begin
      MsgBox('A private key is required for fresh installations.' + #13#10 +
             'You can set it later via the Hyperliquid__PrivateKey environment variable.',
             mbInformation, MB_OK);
      // Allow proceeding without key — user can set env var manually
      Exit;
    end;

    // Validate format if provided
    if (Key <> '') and (not IsValidPrivateKey(Key)) then
    begin
      MsgBox('Invalid key format. Expected: 0x followed by 64 hexadecimal characters (66 characters total).',
             mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// ---- Pre-install: stop existing service ----
procedure CurStepChanged(CurStep: TSetupStep);
var
  Key: String;
begin
  if CurStep = ssInstall then
  begin
    StopExistingService;
  end;

  if CurStep = ssPostInstall then
  begin
    // Store private key if provided
    if not WizardSilent then
    begin
      Key := Trim(PrivateKeyPage.Values[0]);
      if Key <> '' then
        SetPrivateKeyEnvVar(Key);
    end;

    // Register and start the service
    RegisterService;
  end;
end;

// ---- Uninstall: stop service, preserve data directory ----
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  DataDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop and remove the service
    RunCmd(ExpandConstant('{sys}\sc.exe'), ExpandConstant('stop {#ServiceName}'), ResultCode);
    if ResultCode = 0 then
      Sleep(3000);
    RunCmd(ExpandConstant('{sys}\sc.exe'), ExpandConstant('delete {#ServiceName}'), ResultCode);

    // Preserve data directory (SQLite trade history)
    DataDir := ExpandConstant('{app}\data');
    if DirExists(DataDir) then
      Log('Preserving data directory: ' + DataDir);
      // Inno Setup won't delete it because it's not in [UninstallDelete]
      // and files inside weren't installed by us
  end;
end;
