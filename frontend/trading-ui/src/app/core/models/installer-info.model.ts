export interface InstallerInfo {
  status: string;
  version: string;
  exeAvailable: boolean;
  zipAvailable: boolean;
  exeFileName: string | null;
  zipFileName: string | null;
  exeFileSizeBytes: number | null;
  zipFileSizeBytes: number | null;
  sha256Hash: string;
  exeSha256Hash?: string | null;
  zipSha256Hash?: string | null;
  publishedAtUtc?: string | null;
  minimumSupportedVersion?: string | null;
  releaseNotes: string | null;
}
