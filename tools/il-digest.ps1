# Prints a digest of a DLL's method bodies. Read-only. Needs PowerShell 7.
# The file hash embeds the build path, so two builds of the same source can differ. Compare method bodies instead.
# Usage: pwsh -File tools/il-digest.ps1 -Path build/PoVPlus.Compat.dll
param([Parameter(Mandatory)][string]$Path)
$pe = [Reflection.PortableExecutable.PEReader]::new([IO.File]::OpenRead((Resolve-Path $Path)))
$meta = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
$il = [IO.MemoryStream]::new(); $bodies = 0
foreach ($handle in $meta.MethodDefinitions) {
    $rva = $meta.GetMethodDefinition($handle).RelativeVirtualAddress
    if ($rva -eq 0) { continue }
    $bytes = [Reflection.Metadata.PEReaderExtensions]::GetMethodBody($pe, $rva).GetILBytes()
    $il.Write($bytes, 0, $bytes.Length); $bodies++
}
$digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($il.ToArray()))
"method bodies: $bodies"
"types: $($meta.TypeDefinitions.Count)"
"IL SHA-256: $digest"
$pe.Dispose()
