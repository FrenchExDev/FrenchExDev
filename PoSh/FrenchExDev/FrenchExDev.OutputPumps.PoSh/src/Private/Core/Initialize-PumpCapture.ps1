<#
.SYNOPSIS
Load the internal native-process capture types on demand.
.DESCRIPTION
Define Initialize-PumpCapture for the OutputPumps runtime. Reuse types already present in the session and support embedded C# in compiled modules.
#>


function Initialize-PumpCapture {
    <#
    .SYNOPSIS
    Load the internal native-process capture types on demand.
    .DESCRIPTION
    Reuse types already present in the session and support embedded C# in compiled modules.
    #>
    
    if (-not ('FrenchExDev.OutputPumps.BoundedProcess' -as [type])) {
        if ($script:PumpCaptureSource) { Add-Type -TypeDefinition $script:PumpCaptureSource -ErrorAction Stop }
        else { Add-Type -Path (Join-Path $script:PumpLibraryRoot 'src/Private/CSharp/BoundedProcess.cs') -ErrorAction Stop }
    }
}
