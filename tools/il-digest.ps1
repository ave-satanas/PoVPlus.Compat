# Prints digests of a DLL's managed content. Read-only. Needs PowerShell 7.
# The file hash embeds the build path, so two builds of the same source can differ. Compare the managed image instead.
# Image SHA-256 covers the CLI header, IL, exception handlers, strings, signatures, attributes, references and resources. It excludes
# the #GUID heap (module ID) and everything outside the managed image (PE headers, debug directory, PDB path, imports).
# IL SHA-256 is method instructions only: strings appear as tokens, so a changed string does not change it. It is not proof of equivalence.
# Usage: pwsh -File tools/il-digest.ps1 -Path build/PoVPlus.Compat.dll
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$file = Resolve-Path $Path
$pe = [Reflection.PortableExecutable.PEReader]::new([IO.File]::OpenRead($file))
$meta = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
$il = [IO.MemoryStream]::new(); $bodies = 0
foreach ($handle in $meta.MethodDefinitions) {
    $rva = $meta.GetMethodDefinition($handle).RelativeVirtualAddress
    if ($rva -eq 0) { continue }
    $bytes = [Reflection.Metadata.PEReaderExtensions]::GetMethodBody($pe, $rva).GetILBytes()
    $il.Write($bytes, 0, $bytes.Length); $bodies++
}
$digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($il.ToArray()))
$raw = [IO.File]::ReadAllBytes($file); $headers = $pe.PEHeaders
$heaps = [Reflection.Metadata.Ecma335.MetadataReaderExtensions]; $heap = [Reflection.Metadata.Ecma335.HeapIndex]::Guid
[Array]::Clear($raw, $headers.MetadataStartOffset + $heaps::GetHeapMetadataOffset($meta, $heap), $heaps::GetHeapSize($meta, $heap))
$start = $headers.CorHeaderStartOffset; $end = $headers.MetadataStartOffset + $headers.MetadataSize
$resources = $headers.CorHeader.ResourcesDirectory
if ($resources.Size -gt 0 -or $headers.CorHeader.StrongNameSignatureDirectory.Size -gt 0) { throw 'Resources or a strong name lie outside the digested range.' }
$image = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([ArraySegment[byte]]::new($raw, $start, $end - $start).ToArray()))
"method bodies: $bodies"
"types: $($meta.TypeDefinitions.Count)"
"IL SHA-256: $digest"
"Image SHA-256: $image"
$pe.Dispose()
