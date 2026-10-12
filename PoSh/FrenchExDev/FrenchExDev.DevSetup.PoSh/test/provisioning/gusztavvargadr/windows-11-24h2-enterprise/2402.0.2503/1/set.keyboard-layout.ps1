$actualKB = Get-WinUserLanguageList
$actualKB[0].InputMethodTips.Add('040C:0000040C') #change [0] to your desired lang position
Set-WinUserLanguageList $actualKB -Force
Set-WinDefaultInputMethodOverride -InputTip "040C:0000040C" #set default keyboard en-US(International)
