export interface InstallerInfo {
  version: string;
  exeAvailable: boolean;
  zipAvailable: boolean;
  exeFileSizeBytes: number | null;
  zipFileSizeBytes: number | null;
  sha256Hash: string;
  releaseNotes: string | null;
}
