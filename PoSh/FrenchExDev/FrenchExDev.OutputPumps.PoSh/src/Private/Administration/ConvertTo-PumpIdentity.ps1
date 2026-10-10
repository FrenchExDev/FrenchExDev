function ConvertTo-PumpIdentity {
    param($Descriptor)
    $lines=[Collections.Generic.List[string]]::new()
    $lines.Add("# $($Descriptor.Name)");$lines.Add('');$lines.Add($Descriptor.IdentityMarkdown.Trim());$lines.Add('')
    $lines.Add("Version vérifiée : $($Descriptor.VerifiedVersion).");$lines.Add('')
    $lines.Add('Documentation officielle :');$lines.Add('')
    foreach($url in $Descriptor.Documentation){$lines.Add("- <$url>")}
    $lines.Add('');$lines.Add("Module : ``$($Descriptor.Module)``.");$lines.Add('')
    $lines.Add('Appels disponibles :');$lines.Add('')
    foreach($entry in $Descriptor.Commands){
        if($entry.Contains('Route')){$lines.Add("- Route : ``$($entry.Route.Executable) $($entry.Route.Tokens -join ' ')`` ; identifiant ``$($entry.Id)``.")}
        $lines.Add("- ``$($entry.Function)`` : $($entry.When)")
        $lines.Add("  Paramètres du besoin : ``$($entry.Parameters -join ', ')``.")
        $lines.Add("  Exemple : ``$($entry.Example)``.")
    }
    $lines.Add('');$lines.Add('Paramètres communs : `MaxItems` (20), `MaxOutputBytes` (8192, minimum 1024), `TimeoutSeconds` (600). Objets uniquement ; aucun affichage terminal.');$lines.Add('')
    return ($lines -join "`n")
}
