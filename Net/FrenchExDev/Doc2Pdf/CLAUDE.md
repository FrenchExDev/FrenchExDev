# Doc2Pdf — Claude Context

A .NET library and CLI for converting common document formats (DOCX, XLSX, PPTX, RTF, TXT) to PDF, intended for batch processing and automation. Uses Office Interop on Windows or third-party libraries as alternatives.

## Package docs
- [README](README.md)
- `doc/` — empty (no ARCHITECTURE / HOW-TO / PHILOSOPHY yet)

## Relevant skills
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Doc2Pdf.slnx`

## Notes for Claude
- Windows-only in practice — Office Interop requires Microsoft Office installed on the host. LibreOffice is the documented Linux/macOS fallback.
- The README mentions `.NET Framework 4.7.2`, but the project lives in a .NET 10 monorepo; verify the actual TFM in the .csproj before assuming.
- `doc/` is empty — do not invent architectural claims; ask the user before scaffolding extensive design docs.
- Conversion fidelity depends on the underlying engine (Interop vs LibreOffice); they are not byte-for-byte equivalent.
