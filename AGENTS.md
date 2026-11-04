# M6 Akkuwaechter agent instructions

## Project

Small Windows x64 tray application for Keychron M6 battery monitoring. Source is in `source/`; build with `build.ps1`. Keep the application lightweight and preserve the user's saved settings.

## Required commit and push workflow

- After every completed functionality change, commit it with a useful, descriptive message and push it to the configured upstream branch. Do not wait for a separate request to commit or push a verified change.
- **Only commit and push a functionality change once the reported problem is fixed or the requested functionality works, and relevant verification has passed.** An attempted fix, a plausible explanation, or unrelated passing tests are not sufficient.
- Reproduce the issue where practical, check the corrected behavior, and run focused regression checks. Use a hardware check when the change depends on device communication; simulated time is appropriate for timing boundaries and inactivity logic.
- If the problem remains unresolved or the required verification cannot be completed, leave the affected changes uncommitted and unpushed. Explain what remains broken or unverified. Do not label the work as fixed.
- Keep each verified functionality change in a focused commit. Include its relevant tests and documentation. Do not bundle unrelated work or commit another person's unfinished changes.
- Use messages describing the resulting behavior, for example `Fix tray freshness after polling interval changes`. Avoid vague names such as `update`, `changes`, or `fix stuff`.
- Review `git diff` and run `git diff --check` before committing. Check the configured remote and upstream before pushing. Never force-push or rewrite shared history without an explicit user request.
- After pushing, verify that the remote branch contains the commit. Report the change, verification results, and commit identifier. If pushing fails, report that accurately and preserve the local commit.
- Documentation-only requests may be committed and pushed after checking that the requested documentation is complete and accurate; they do not require a fabricated functionality test.

## Build and verification

- Build from the repository root with `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`.
- The executable may be locked while running. When necessary, stop this application with `M6Akku.exe --stop` before rebuilding, then restart the updated application with `--background`. Preserve the existing polling interval and warning state.
- Run `M6Akku.exe --self-test <absolute-output-path>` and inspect the result. Keep generated test output outside tracked source files.
- `M6Akku.exe --status <absolute-output-path>` reads the running application's status without triggering a USB query. Use it to check actual background operation when relevant.
- `M6Akku.exe --diagnose <absolute-output-path>` performs a real device query. Use it with monitoring and other software accessing the Keychron status channel stopped.
- Record material limits honestly: a simulated timing test is not a physical standby test, and a single successful query is not a full polling-cycle test.

## Boundaries

- Never commit credentials, local preferences, generated diagnostics, compiled executables, or ZIP archives. Follow `.gitignore`.
- Use only status-reading commands for device diagnostics. Do not flash firmware or change mouse settings unless the user explicitly requests it.
- Preserve fresh-response validation: do not accept cached battery data without the required receiver confirmation.
- Keep pause status, measurement age, and actual connection/query failures distinct in the tray display.
