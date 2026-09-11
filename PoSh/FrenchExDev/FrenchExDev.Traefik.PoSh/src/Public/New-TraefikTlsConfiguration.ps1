function New-TraefikTlsConfiguration {
  [CmdletBinding()]
  param(
    [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true, position = 0)][string] $Domain,
    [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true, position = 1)][string] $OutFile
  )

  $template = [pscustomobject] @{
    tls = [pscustomobject] @{
      stores       = [pscustomobject] @{
        default = [pscustomobject] @{
          defaultCertificate = [pscustomobject] @{
            certFile = "/etc/ssl/traefik/$Domain.crt"
            keyFile  = "/etc/ssl/traefik/$Domain.key"
          }
        }
      }
      certificates = @(,
        [pscustomobject] @{
          certFile = "/etc/ssl/traefik/$Domain.crt"
          keyFile  = "/etc/ssl/traefik/$Domain.key"
        }
      )
    }
  }

  if (Test-Path $OutFile) {
    Write-Debug "New-TraefikTlsConfiguration > Overwritting existing file"
  }

  if (!(Test-Path $(Split-Path $OutFile -Parent))) {
    New-Item -ItemType Directory $(Split-Path $OutFile -Parent) | Out-Null
  }

  $template | ConvertTo-Yaml | Out-File $OutFile -encoding ascii | Out-Null
}
