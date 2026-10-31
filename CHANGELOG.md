# Changelog

All notable changes to this package will be documented in this file.

## [1.0.1] - 2026-10-31
### Changed
- Replaced DOTween with LitMotion. `DOTweenWindowTransition` was renamed to
  `LitMotionWindowTransition` and rewritten against the LitMotion API; the
  `RMVVM.asmdef` reference to DOTween was swapped for a `LitMotion` reference.
  Fixes `CS0246: The type or namespace name 'DG' could not be found`.

## [1.0.0] - 2026-10-30
### Added
- Initial UPM package release.
