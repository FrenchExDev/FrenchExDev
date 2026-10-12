function New-PackerVagrantPostProcessor {
    [CmdletBinding()]
    param(
        [int] $CompressionLevel = 9,
        [switch] $KeepInputArtefact,
        [string] $Output = '{{user `output_vagrant` }}/{{user `vm_name` }}-v{{user `box_version` }}{{user `flavor`}}.box',
        [string] $VagrantfileTemplate = "./vagrant/Vagrantfile"
    )

    @{
        type                 = "vagrant"
        compression_level    = $CompressionLevel
        keep_input_artifact  = [bool] $KeepInputArtefact.IsPresent
        output               = "$Output"
        vagrantfile_template = "$VagrantfileTemplate"
    }
}