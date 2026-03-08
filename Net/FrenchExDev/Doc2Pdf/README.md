# Doc2Pdf

Doc2Pdf is a .NET library and command-line tool designed to convert various document formats (such as DOCX, XLSX, PPTX, and more) to PDF. It leverages the power of Microsoft Office Interop or third-party libraries for accurate conversions, making it ideal for automation, batch processing, and integration into larger applications.

## Features

- **Multi-format Support**: Converts DOCX, XLSX, PPTX, RTF, TXT, and other common document formats to PDF.
- **High Fidelity**: Maintains formatting, images, and layouts during conversion.
- **Batch Processing**: Supports converting multiple files in a single operation.
- **Command-Line Interface**: Easy-to-use CLI for scripting and automation.
- **Library Integration**: Can be used as a NuGet package in .NET applications.
- **Error Handling**: Robust error reporting and logging for failed conversions.
- **Cross-Platform**: Compatible with Windows, with potential for Linux/Mac via Mono or .NET Core.

## Prerequisites

- .NET Framework 4.7.2 or later (for Windows).
- Microsoft Office installed (for Interop-based conversions) or alternative libraries like LibreOffice.
- Windows 10 or later.

## Installation

### As a NuGet Package

Add the package to your .NET project:

```bash
dotnet add package FrenchExDev.Doc2Pdf
```

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/FrenchExDev/FrenchExDev_i2.git
   cd FrenchExDev_i2/Net/FrenchExDev/Doc2Pdf
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

## Usage

### Command-Line Tool

Convert a single file:
```bash
Doc2Pdf.exe input.docx output.pdf
```

Convert multiple files:
```bash
Doc2Pdf.exe --batch input1.docx input2.xlsx --output-dir ./pdfs
```

Options:
- `--input <file>`: Input file path.
- `--output <file>`: Output PDF path.
- `--batch`: Enable batch mode.
- `--output-dir <dir>`: Directory for batch outputs.
- `--verbose`: Enable detailed logging.

### As a Library

```csharp
using FrenchExDev.Doc2Pdf;

var converter = new DocumentConverter();
converter.Convert("input.docx", "output.pdf");
```

For batch conversions:
```csharp
var files = new[] { "doc1.docx", "doc2.xlsx" };
converter.ConvertBatch(files, "./output");
```

## Configuration

Doc2Pdf can be configured via an `appsettings.json` file or environment variables:

- `Doc2Pdf:TempDir`: Temporary directory for processing.
- `Doc2Pdf:OfficePath`: Path to Microsoft Office installation.
- `Doc2Pdf:LogLevel`: Logging level (e.g., Information, Warning).

Example `appsettings.json`:
```json
{
  "Doc2Pdf": {
    "TempDir": "C:\\Temp",
    "OfficePath": "C:\\Program Files\\Microsoft Office",
    "LogLevel": "Information"
  }
}
```

## API Reference

### DocumentConverter Class

- `Convert(string inputPath, string outputPath)`: Converts a single document.
- `ConvertBatch(IEnumerable<string> inputPaths, string outputDir)`: Converts multiple documents.
- `SetOptions(ConversionOptions options)`: Configures conversion settings.

### ConversionOptions

- `PreserveFormatting`: Boolean to maintain original formatting.
- `IncludeImages`: Boolean to include images in PDF.
- `PageOrientation`: Landscape or Portrait.

## Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/your-feature`.
3. Commit changes: `git commit -m 'Add your feature'`.
4. Push to the branch: `git push origin feature/your-feature`.
5. Open a Pull Request.

Ensure all tests pass by running:
```bash
dotnet test
```

## Testing

Run unit tests:
```bash
dotnet test
```

Integration tests require sample documents in the `test-data` directory.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Support

For issues or questions, please open an issue on GitHub or contact support@frenchexdev.com.

## Changelog

### Version 1.0.0 (March 5, 2026)
- Initial release with basic conversion support.
- Added CLI and library interfaces.
- Support for DOCX, XLSX, PPTX formats.