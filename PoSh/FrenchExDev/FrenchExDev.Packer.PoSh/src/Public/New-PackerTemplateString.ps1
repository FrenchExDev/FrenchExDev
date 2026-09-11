function New-PackerTemplateString {
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true, Position = 0)] [string] $Name
    )
    "{{ user ``$Name`` }}"
}