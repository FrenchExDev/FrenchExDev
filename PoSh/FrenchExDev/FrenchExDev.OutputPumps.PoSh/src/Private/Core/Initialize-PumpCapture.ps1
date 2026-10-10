function Initialize-PumpCapture {
    if (-not ('FrenchExDev.OutputPumps.BoundedProcess' -as [type])) {
        if ($script:PumpCaptureSource) { Add-Type -TypeDefinition $script:PumpCaptureSource -ErrorAction Stop }
        else { Add-Type -Path (Join-Path $script:PumpLibraryRoot 'src/Private/CSharp/BoundedProcess.cs') -ErrorAction Stop }
    }
}
