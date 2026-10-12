<#
.SYNOPSIS
Check catalogue metadata against available modules and exports.
.DESCRIPTION
Define Test-OutputPumpCatalogue for the OutputPumps runtime. Return a bounded validation report rather than importing unrelated software or exposing raw diagnostics.
#>


function Test-OutputPumpCatalogue {
    <#
    .SYNOPSIS
    Check catalogue metadata against available modules and exports.
    .DESCRIPTION
    Return a bounded validation report rather than importing unrelated software or exposing raw diagnostics.
    #>
    
    [CmdletBinding()]
    param([string]$LibraryRoot=$script:PumpLibraryRoot)
    try {
        $catalogue=@(Get-Content -LiteralPath (Join-Path $LibraryRoot 'catalogue.json') -Raw | ConvertFrom-Json -AsHashtable -ErrorAction Stop)
        $ids=[Collections.Generic.HashSet[string]]::new()
        foreach($descriptor in $catalogue){
            if(!$ids.Add($descriptor.Id)){throw 'Duplicate software id'}
            Assert-PumpDescriptor $descriptor $LibraryRoot
            if([IO.File]::ReadAllText((Join-Path $LibraryRoot "software/$($descriptor.Id).md")) -cne (ConvertTo-PumpIdentity $descriptor)){throw 'Software identity out of sync'}
        }
        if([IO.File]::ReadAllText((Join-Path $LibraryRoot 'index.md')) -cne (ConvertTo-PumpIndex $catalogue)){throw 'Index out of sync'}
        New-PumpResult -Summary 'Catalogue, identities, exports and parameters agree.' -Data @{Items=@(@{SoftwareCount=$catalogue.Count;Functions=@($catalogue.Commands.Function).Count})}
    } catch {New-PumpResult -Summary 'Catalogue validation failed.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='CatalogueInvalid';Message=$_.Exception.Message}}
}
