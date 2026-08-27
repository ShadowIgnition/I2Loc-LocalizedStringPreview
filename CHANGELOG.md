# Changelog

All notable changes to I2LocalizationPreview Attribute are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and releases use [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-08-27

### Added

- Added a Unity Package Manager manifest, stable asset metadata, and separate runtime and Editor assemblies.
- Added package-facing documentation and a manual release checklist for validation with licensed I2 installations.
- Added a searchable term picker beside supported `LocalizedString` fields.

### Changed

- Removed the compile-time dependency on I2 assemblies and delegated drawing and translation through a compatibility bridge.
- Declared Unity 2021.1 as the minimum supported version and documented validation against Unity 2021.3.20f1 and Unity 6000.3.14f1, including collection fields.
- Matched preview translations to every serialized `LocalizedString` RTL and parameter option.
- Made automatic sizing responsive to the actual Inspector width and the active skin's scrollbar.
- Limited explicit preview heights to 20 rows to prevent unusably large Inspectors.
- Loaded a fresh list of registered I2 terms whenever the picker opens, removing blank and duplicate identifiers and sorting the result deterministically.

### Fixed

- Fixed short automatic previews entering a scroll view when their content did not overflow.
- Fixed long and fixed-height previews failing to scroll or measuring text at a different width than they rendered.
- Fixed height changes drawing outside the rectangle Unity allocated during layout transitions.
- Fixed state leaking between properties, objects, and Inspector windows.
- Fixed mixed-object selections previewing an arbitrary target value.
- Fixed stale translations after I2 refresh events and transient translation failures being cached indefinitely.
- Fixed unsupported fields drawing an error outside their reserved Inspector height.
- Fixed collection elements sharing native drawer state or receiving a term selected from another visible element.
- Fixed stale picker results changing a value after its collection slot, I2 options, or target set changed.
- Removed an obsolete drawer-caching warning in Unity 6 while retaining the Unity 2021 cache behavior.

## [1.0.0] - 2023-06-07

### Added

- Added the `I2Preview` attribute with automatic and fixed preview heights.

[1.1.0]: https://github.com/ShadowIgnition/I2Loc-LocalizedStringPreview/compare/v1.0...v1.1.0
[1.0.0]: https://github.com/ShadowIgnition/I2Loc-LocalizedStringPreview/releases/tag/v1.0
