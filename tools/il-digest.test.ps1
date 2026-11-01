# Checks that il-digest's image digest catches a changed string, which the IL digest alone cannot. Writes only to a temporary copy.
# Usage: pwsh -File tools/il-digest.test.ps1 -Path build/PoVPlus.Compat.dll
param([Parameter(Mandatory)][string]$Path)
$digest = Join-Path $PSScriptRoot 'il-digest.ps1'
function Lines($dll) { & pwsh -NoProfile -File $digest -Path $dll }
$copy = Join-Path ([IO.Path]::GetTempPath()) "il-digest-test-$PID.dll"
try {
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path $Path))
    # Latin-1 maps each byte to one char, so offsets carry over. Replaces both the #Strings and the UTF-16 #US forms.
    $latin = [Text.Encoding]::Latin1
    $text = $latin.GetString($bytes)
    $changed = $text.Replace('ValheimCuisine', 'ValheimCuisinX').Replace("C`0u`0i`0s`0i`0n`0e`0", "C`0u`0i`0s`0i`0n`0X`0")
    if ($changed -ceq $text) { throw 'The sample has no ValheimCuisine string to change.' }
    [IO.File]::WriteAllBytes($copy, $latin.GetBytes($changed))
    $a = Lines $Path; $b = Lines $copy
    if (($a | Where-Object { $_ -like 'Image*' }) -ceq ($b | Where-Object { $_ -like 'Image*' })) { throw 'Image digest missed a changed string.' }
    if (($a | Where-Object { $_ -like 'IL*' }) -cne ($b | Where-Object { $_ -like 'IL*' })) { throw 'Expected the IL digest to stay the same, as its limit is documented.' }
    'ok: image digest changed, IL digest did not'
} finally { Remove-Item $copy -EA 0 }
