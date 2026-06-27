<!-- markdownlint-disable-file -->
# Release Changes: Execution Agent Installer Distribution

**Related Plan**: 20260626-execution-agent-installer-distribution-plan.instructions.md
**Implementation Date**: 2026-06-27

## Summary

Implements CI-built execution-agent installer distribution through private Blob Storage, manifest-backed API metadata/downloads, worker update integrity checks, and UI/operator improvements.

## Changes

### Added

<!-- Phase 2: Add Blob-Backed Release Manifest -->
- src/TradePilot.Application/Agent/Models/InstallerReleaseFile.cs: Added the manifest contract for individual installer artifacts with filename, blob path, size, content type, and SHA256.
- src/TradePilot.Application/Agent/Models/InstallerReleaseManifest.cs: Added the top-level installer release manifest contract for versioned metadata and file maps.
- deploy/worker/New-ReleaseManifest.ps1: Added manifest generation for installer release metadata from CI-built artifacts.
- deploy/worker/upload_release_manifest.py: Added versioned blob upload and post-upload verification logic driven by the generated manifest.

<!-- Phase 3: Use Manifest Metadata in the API -->
- tests/TradePilot.Infrastructure.Tests/Storage/LocalInstallerStoreTests.cs: Added coverage for local manifest loading and manifest-file stream fallback behavior.

### Modified

<!-- Phase 1: Make CI Build and Publish Installer Artifacts -->
- .github/workflows/deploy.yml: Added a Windows installer build job, published the installer artifact, and made blob upload consume only the current-run artifact with hard failure on missing files.
- deploy/worker/build-installer.ps1: Cleaned installer output per run and kept generated ZIP/installer artifacts under the workflow-owned installer directory.
- deploy/worker/installer.iss: Normalized product, executable, service, install path, and setup output naming around TradePilot.ExecutionAgent.

<!-- Phase 2: Add Blob-Backed Release Manifest -->
- .github/workflows/deploy.yml: Generated `latest.json`, uploaded manifest-driven versioned installer blobs, and verified uploaded blob metadata against the manifest.
- src/TradePilot.Infrastructure/Storage/BlobInstallerStore.cs: Added versioned blob-name normalization so manifest and version-prefixed installer blobs resolve consistently.
- tests/TradePilot.Infrastructure.Tests/Storage/BlobInstallerStoreTests.cs: Added coverage for versioned blob-name normalization and manifest path passthrough behavior.

<!-- Phase 3: Use Manifest Metadata in the API -->
- src/TradePilot.Application/Agent/IInstallerStore.cs: Extended the installer store contract with manifest loading and manifest-file streaming methods.
- src/TradePilot.Infrastructure/Storage/BlobInstallerStore.cs: Added manifest deserialization and manifest-file stream resolution for blob-backed installer artifacts.
- src/TradePilot.Infrastructure/Storage/LocalInstallerStore.cs: Added local manifest deserialization and safe fallback from manifest blob paths to local filenames.
- src/TradePilot.Application/Agent/Models/AgentUpdateOptions.cs: Clarified appsettings update metadata as fallback-only configuration.
- src/TradePilot.Api/Controllers/AgentController.cs: Switched installer info, download, update metadata, and heartbeat update responses to manifest-backed release resolution with explicit status values.
- tests/TradePilot.Api.Tests/Controllers/AgentControllerTests.cs: Added focused controller coverage for manifest-backed info, missing-blob status, manifest-backed download, and heartbeat update metadata.

<!-- Phase 4: Fix Download URLs and Update Integrity -->
- src/TradePilot.Api/Controllers/AgentController.cs: Returned absolute installer download URLs and added manifest/fallback release telemetry including missing-blob diagnostics.
- src/TradePilot.Worker/Services/UpdateCheckerService.cs: Resolved relative update URLs against the control-plane base address, rejected missing-hash updates, and expanded download/hash failure telemetry.
- src/TradePilot.Worker/Program.cs: Configured the update-download HttpClient with the control-plane base address for resolvable worker update URLs.
- tests/TradePilot.Api.Tests/Controllers/AgentControllerTests.cs: Added assertions that update metadata uses absolute URLs.
- tests/TradePilot.Worker.Tests/Services/UpdateCheckerServiceTests.cs: Added focused coverage for relative URL resolution and missing-hash rejection.

<!-- Phase 5: Secure Download Access -->
- src/TradePilot.Api/Controllers/AgentController.cs: Removed controller-wide anonymous access, required authenticated subscribed access for browser downloads, and added short-lived worker download tokens.
- tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs: Extended controller test authentication setup for user-specific authenticated requests.
- tests/TradePilot.Api.Tests/Controllers/AgentControllerTests.cs: Added coverage for authenticated browser access, anonymous worker token downloads, and subscription-gated installer downloads.
- frontend/trading-ui/src/app/core/services/agent.service.ts: Switched installer downloads to authenticated HTTP blob fetches instead of raw anchor navigation.
- frontend/trading-ui/src/app/features/profile/profile-page.component.ts: Added installer download action state and user-visible error handling.
- frontend/trading-ui/src/app/features/profile/profile-page.component.html: Replaced direct installer links with authenticated download buttons.
- frontend/trading-ui/src/app/core/models/installer-info.model.ts: Aligned the frontend installer metadata model with the API response contract.
- infrastructure/modules/storage-account.bicep: Added an explicit output documenting that installer artifacts remain private.

<!-- Phase 6: UI and Operator Experience -->
- frontend/trading-ui/src/app/core/services/agent.service.ts: Removed installer-info memoization so newly published release metadata is reflected without a full app reload.
- frontend/trading-ui/src/app/features/profile/profile-page.component.ts: Added installer view-model helpers for status-specific empty states, integrity metadata, and download errors.
- frontend/trading-ui/src/app/features/profile/profile-page.component.html: Reworked the Execution Agent card to show manifest status, published date, per-format availability, and checksum messaging.
- frontend/trading-ui/src/app/features/profile/profile-page.component.scss: Added styling for release-state callouts, badges, and per-package installer rows.
- deploy/worker/README.md: Added the operator runbook for build, publish, verify, rollback, and promotion of execution-agent releases.

### Removed

## Test Results

<!-- Phase 1: Make CI Build and Publish Installer Artifacts -->
- Installer packaging validation: PASSED (`./deploy/worker/build-installer.ps1`)
- Edited-file diagnostics: PASSED (workflow, PowerShell script, and Inno Setup script)
- Architecture tests: NOT RUN (not part of this phase)

<!-- Phase 2: Add Blob-Backed Release Manifest -->
- BlobInstallerStoreTests: 5/5 passed
- Release manifest generation: PASSED (`./deploy/worker/New-ReleaseManifest.ps1 -InstallerDirectory artifacts/installer -OutputPath artifacts/phase2-verify/latest.json`)
- Local upload helper dry run: PARTIAL (stopped at expected Azure CLI boundary; manifest parsing and artifact consistency checks passed)

<!-- Phase 3: Use Manifest Metadata in the API -->
- AgentControllerTests: 7/7 passed
- Installer store tests: 7/7 passed
- Architecture tests: NOT RUN

<!-- Phase 4: Fix Download URLs and Update Integrity -->
- AgentControllerTests: 8/8 passed
- UpdateCheckerServiceTests: 4/4 passed
- Architecture tests: NOT RUN

<!-- Phase 5: Secure Download Access -->
- AgentControllerTests: 20/20 passed
- UpdateCheckerServiceTests: 8/8 passed
- TradePilot.Api.Tests build: PASSED
- TradePilot.Worker.Tests build: PASSED
- Architecture tests: NOT RUN

<!-- Phase 6: UI and Operator Experience -->
- Frontend edited-file diagnostics: PASSED
- Angular production build: PASSED (`npm run build` in `frontend/trading-ui`)

<!-- Final validation -->
- API installer controller tests: PASSED (`dotnet test tests/TradePilot.Api.Tests/TradePilot.Api.Tests.csproj -nologo --filter FullyQualifiedName~AgentControllerTests`) - 12/12 passed
- Worker update tests: PASSED (`dotnet test tests/TradePilot.Worker.Tests/TradePilot.Worker.Tests.csproj -nologo --filter FullyQualifiedName~UpdateCheckerServiceTests`) - 4/4 passed
- Infrastructure installer-store tests: PASSED (`dotnet test tests/TradePilot.Infrastructure.Tests/TradePilot.Infrastructure.Tests.csproj -nologo --filter FullyQualifiedName~InstallerStoreTests`) - 7/7 passed
- API test project build: PASSED (`dotnet build tests/TradePilot.Api.Tests/TradePilot.Api.Tests.csproj -nologo`)
- `runTests` file-scoped validation: INCONCLUSIVE (tool reported generic project build failures while direct `dotnet build`/`dotnet test` runs passed for the same projects)

## Issues

<!-- Phase 1: Make CI Build and Publish Installer Artifacts -->
- Pre-existing `NU1902` and `NU1903` warnings for `MessagePack` 3.1.4 appeared during publish; build succeeded and this phase did not introduce them.

<!-- Phase 2: Add Blob-Backed Release Manifest -->
- PowerShell encoding options were not portable across shells for manifest writing; fixed by writing UTF-8 via `System.IO.File`.
- Inline YAML verification logic was brittle and caused workflow parsing issues; moved into `deploy/worker/upload_release_manifest.py`.
- Local verification of Azure upload stops without `az`; workflow-side verification remains the authoritative end-to-end check.

<!-- Phase 3: Use Manifest Metadata in the API -->
- Initial focused tests failed on record-constructor named argument mismatches in the new controller helper record; corrected and rerun.
- API tests then failed on a missing namespace import for `InstallerInfoResponse`; corrected and rerun.
- Pre-existing `NU1902` and `NU1903` warnings for `MessagePack` remain in touched projects and were not introduced here.

<!-- Phase 4: Fix Download URLs and Update Integrity -->
- The first focused worker test run failed due to a missing `TradePilot.Application.Agent.Models` import in the updated worker test file; corrected and reran.
- Pre-existing `NU1902` and `NU1903` warnings for `MessagePack` 3.1.4 remain and were not introduced by this phase.

<!-- Phase 5: Secure Download Access -->
- The initial worker-token implementation attempted to use the time-limited data protection API, which is not available in the current API project; replaced with a protected payload carrying an explicit expiry timestamp.
- Focused test execution intermittently surfaced generic build-failure wrappers; direct project builds confirmed compile health and the final focused runs passed.
- The profile page initially referenced installer filename fields missing from the frontend model; the model was updated to match the API contract.

<!-- Phase 6: UI and Operator Experience -->
- The first Angular build failed because `@else if (...; as alias)` is not valid Angular control-flow syntax; the template was restructured and the build rerun successfully.
- The frontend build still reports pre-existing warnings outside this PBI, including an optional-chain warning in `agents-page.component.html`, Sass deprecation warnings, CommonJS `qrcode` usage, and existing bundle/SCSS budget warnings.

<!-- Final validation -->
- The VS Code `runTests` tool reported generic file-scoped build failures for touched test files even though direct project builds and filtered `dotnet test` commands succeeded; treated as a tooling/reporting inconsistency rather than a code regression.
- Pre-existing `MessagePack` 3.1.4 vulnerability warnings (`NU1902`/`NU1903`) remain across API, worker, infrastructure, and test projects.

## Design Decisions

<!-- Phase 1: Make CI Build and Publish Installer Artifacts -->
- `upload-installers` no longer checks out the repo so it cannot accidentally upload stale local contents instead of the build artifact produced by the workflow.
- Installer outputs remain untracked and CI-generated because `artifacts/installer` is already ignored and not source-controlled.

<!-- Phase 2: Add Blob-Backed Release Manifest -->
- Kept `latest.json` at the container root and stored binaries under `v{version}/...` so the latest release can advance without flattening versioned artifacts.
- Added version-aware blob normalization in the installer store as the smallest compatibility step ahead of full API manifest consumption.
- Split upload and verification into a dedicated helper script to keep the workflow readable and reduce YAML parsing fragility.

<!-- Phase 3: Use Manifest Metadata in the API -->
- Centralized manifest consumption through a single release-resolution path in `AgentController` so info, downloads, `update/latest`, and heartbeat metadata stay consistent.
- Preserved `AgentUpdateOptions` as a local/dev fallback when `latest.json` is absent.
- Added explicit string status values including `NoManifest`, `ManifestFoundBlobMissing`, `Available`, and `FallbackConfigured` to distinguish manifest and blob state without a broader contract refactor.

<!-- Phase 4: Fix Download URLs and Update Integrity -->
- Hardened update URL handling on both sides: the API emits absolute URLs when possible and the worker still resolves relative paths against `Agent:ControlPlaneUrl` as a compatibility fallback.
- Treated a missing SHA256 hash as a hard update failure before staging or execution.
- Logged manifest status and missing blob names at controller resolution time because that is the most reliable point for rollout diagnostics across heartbeat and browser/API consumers.

<!-- Phase 5: Secure Download Access -->
- Browser-facing installer downloads now require authentication plus an active subscription because they distribute subscriber-only product artifacts.
- Worker update downloads use short-lived protected tokens so agent heartbeat compatibility is preserved without leaving browser download routes anonymous.
- Blob privacy remains unchanged; the API stays as the distribution boundary instead of exposing direct public blob URLs.

<!-- Phase 6: UI and Operator Experience -->
- Removed client-side installer metadata caching so the profile page reflects newly promoted manifests without stale empty states.
- Kept the release runbook in `deploy/worker/README.md` because worker packaging and upgrade operations already live there.

## Review Hints

- Verify a `main` GitHub workflow run can execute the new `windows-latest` packaging job and hand the `installer-dist` artifact to Azure upload.
- Confirm whether future packaging should exclude `.pdb` files; this phase intentionally preserved existing payload composition while fixing the CI path.
- Verify the workflow uploads `latest.json` and versioned EXE/ZIP/SHA256 artifacts under `v{version}/` in the private `installers` container.
- Check that the manifest schema consumed in Phase 3 matches the generated `files`/`artifacts` structure and `blobName` values exactly.
- Review whether `FallbackConfigured` is acceptable for current API/UI consumers alongside the planned `NoManifest`, `ManifestFoundBlobMissing`, and `Available` statuses.
- Check that follow-on Phase 4 work converts any remaining relative download URLs into absolute or reliably resolvable URLs for worker update consumption.
- Review whether the new absolute URL generation behaves correctly behind any production reverse proxy or path-base configuration.
- Check whether the new manifest-missing and update-availability logs create the right operational signal level during normal rollouts.
- Review whether keeping `update/latest` anonymous remains desirable once all workers rely primarily on heartbeat-provided metadata.
- Check the 10-minute worker token lifetime and binding assumptions against real-world client download latency and retry behavior.
- Verify the profile page messaging against real `NoManifest`, `ManifestFoundBlobMissing`, `FallbackConfigured`, and `Available` API states.
- Check whether the profile page SCSS budget exceedance should be addressed in a follow-up cleanup rather than this focused PBI.
- Review why the VS Code `runTests` tool reports generic build failures for file-scoped runs even though direct filtered `dotnet test` passes.

## Release Summary

Implemented the execution-agent installer distribution flow end to end: CI now builds and publishes versioned installer artifacts plus `latest.json` to private Blob Storage; the API serves manifest-derived metadata, authenticated browser downloads, and worker-compatible update tokens; the worker enforces resolvable URLs and non-empty SHA256 verification; and the UI/runbook now present clearer installer availability, integrity, and operator guidance.