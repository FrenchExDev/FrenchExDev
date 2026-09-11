function Clean-DotnetSolution {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, position = 0, ValueFromPipelineByPropertyName = $true)] [string] $Solution
    )

    process {
        if (-not (Test-Path -Path $Solution)) {
            Throw "The solution file '$Solution' does not exist."
        }

        Write-Verbose "Cleaning solution: $Solution"
        dotnet clean $Solution
    }
}