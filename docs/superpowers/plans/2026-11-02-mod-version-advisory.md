# Mod version advisory repair

Goal: Stop reporting the installed mod as current when the author's VERSION policy is newer than the GitHub release mirror.

Evidence: On November 2, installed and GitHub release files are v2.0.1; Nexus lists v2.0.2, and the author's VERSION file marks 2.01 disabled and >2.02 enabled.

Scope: Keep the existing layout and manual ZIP workflow. Never download or redistribute the mod, alter saves, bypass the mod's version checks, or treat a Nexus ZIP as GitHub-verified. Preserve offline launch.

- [x] Add failing policy regression tests: mirror lag, current mirror, malformed policy, unsupported tag, unavailable policy.
- [x] Add a small strict parser/client in Core and integrate it into CheckAsync. When the mirror lags, direct Get latest release to Nexus and describe both versions. When policy cannot be checked, state the limit instead of claiming current.
- [x] Run the full existing suite and build WPF. Verify against the live author policy. Record UI/launch verification limits.
- [ ] Finish editable Figma desktop, update-required, and controller studies; obtain design approval before product UI redesign.

Execution: Inline. Existing checkout was clean; a separate fix branch preserves its starting branch. No release or push is authorized by this plan.

Verification: .NET 10 full test executable completed with 25 executed checks passing, including live author-policy/GitHub metadata comparison and extraction of the existing player-downloaded ZIP. Two optional tests that download public release artifacts were skipped. Release WPF build succeeded with zero warnings/errors. Fresh code review found no actionable issue. Installed mod files match the prior v2.0.1 record. The raw policy confirms author minimum v2.0.2 and GitHub v2.0.1. Game launch and rendered patch behavior remain unverified: an unrelated Node.js Windows Security dialog blocks computer input; Nexus v2.0.2 requires an authenticated manual download and is not in Downloads.

Design: The initial utility-layout study remains on Page 1. Max requested a more substantial Elden Ring companion direction; editable Play, update-required and settings studies are on the Companion redesign page of https://www.figma.com/design/uwB6ruY6RRFTEW6zuoSF6G . Product UI redesign awaits approval of actual Figma frames. Official artwork is reference-only until distribution rights are resolved.
