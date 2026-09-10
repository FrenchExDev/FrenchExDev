# Help fixtures

The `*.fr.help.txt` files were captured on Windows on 2026-09-09 with SDK
`11.0.100-rc.1.26425.128`, using `dotnet --help`, `dotnet build --help`,
`dotnet tool --help` and `dotnet solution add --help`. The SDK emits a mixture
of French headings and English descriptions on this host, including when
`DOTNET_CLI_UI_LANGUAGE` is set to English.

Tests also cover English help syntax and wrapped descriptions with small fixtures.

The `*.en.help.txt` files are SDK 8.0.100 outputs collected by the Design pipeline
in an Alpine 3.19 container on the same date. The original cache is retained under
`src/FrenchExDev.Net.Dotnet/scrape/help/8.0.100/`.

