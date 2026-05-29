#Requires -Version 5.1
<#
.SYNOPSIS
    Indexes .cs and .md source files into Azure AI Search for Phase 3 RAG retrieval.

.DESCRIPTION
    Walks RepoPath recursively, excludes bin/, obj/, and *.generated.cs files,
    splits each file into 300-500 line chunks at class/method/heading boundaries,
    then upserts every chunk to an Azure AI Search index using mergeOrUpload.
    Safe to re-run — existing documents are updated in place, no duplicates created.

.PARAMETER SearchUrl
    Azure AI Search service base URL, e.g. https://my-search.search.windows.net

.PARAMETER SearchKey
    Azure AI Search admin API key.

.PARAMETER RepoPath
    Root directory of the repository to index.

.PARAMETER IndexName
    Target search index name (default: codebase-chunks).
    The index is created automatically if it does not exist.

.EXAMPLE
    .\index-repo.ps1 `
        -SearchUrl  https://my-search.search.windows.net `
        -SearchKey  <admin-key> `
        -RepoPath   C:\Projects\MyRepo
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SearchUrl,
    [Parameter(Mandatory)][string] $SearchKey,
    [Parameter(Mandatory)][string] $RepoPath,
    [string]                        $IndexName = 'codebase-chunks'
)

Set-StrictMode -Version 1
$ErrorActionPreference = 'Stop'

# ── Constants ─────────────────────────────────────────────────────────────────

$ApiVersion    = '2023-11-01'
$BatchSize     = 100   # documents per upsert call (Azure Search max is 1000)
$MinChunkLines = 300
$MaxChunkLines = 500

# ── Helpers ───────────────────────────────────────────────────────────────────

function ConvertTo-DocId([string]$relPath, [int]$chunkIndex) {
    # Azure AI Search keys: letters, digits, hyphens only; max 1024 chars
    $safe = ($relPath -replace '[^a-zA-Z0-9]', '-') -replace '-{2,}', '-'
    $safe = $safe.TrimStart('-').TrimEnd('-')
    if ($safe.Length -gt 900) { $safe = $safe.Substring(0, 900) }
    return "$safe-c$chunkIndex"
}

function Get-Language([string]$ext) {
    switch ($ext) {
        '.cs' { return 'csharp' }
        '.md' { return 'markdown' }
        default { return 'text' }
    }
}

# ── Chunking ─────────────────────────────────────────────────────────────────

# C#: split before namespace/class/interface declarations and public/private/protected methods.
# Matches lines like:
#   namespace Foo.Bar
#   public class MyService
#   private async Task<string> RunAsync(
#   protected override void OnInit() {
$script:CsBoundary = [regex] (
    '^\s{0,8}(?:public|private|protected|internal)(?:\s+(?:static|async|abstract|virtual|override|sealed|readonly))?' +
    '\s+\S.*[\s({]' +
    '|^\s{0,4}(?:namespace|class|interface|record|enum|struct)\s'
)

# Markdown: split before H1/H2/H3 headings
$script:MdBoundary = [regex] '^#{1,3} '

function Get-Chunks {
    <#
    .SYNOPSIS
        Splits an array of lines into 300-500 line chunks, preferring natural boundaries.
    .OUTPUTS
        string[] — one array per chunk, emitted to the pipeline.
        Collect with @(Get-Chunks ...) to get an array-of-arrays.
    #>
    param(
        [string[]] $Lines,
        [string]   $Ext
    )

    $boundary = if ($Ext -eq '.cs') { $script:CsBoundary } else { $script:MdBoundary }
    $buf      = [System.Collections.Generic.List[string]]::new()

    foreach ($line in $Lines) {
        $atMax        = $buf.Count -ge $MaxChunkLines
        $atNatural    = $boundary.IsMatch($line) -and ($buf.Count -ge $MinChunkLines)

        if (($atMax -or $atNatural) -and $buf.Count -gt 0) {
            Write-Output (, $buf.ToArray())   # `,` wraps array so pipeline doesn't unroll it
            $buf = [System.Collections.Generic.List[string]]::new()
        }

        [void] $buf.Add($line)
    }

    if ($buf.Count -gt 0) {
        Write-Output (, $buf.ToArray())
    }
}

# ── Azure AI Search ───────────────────────────────────────────────────────────

$script:HttpHeaders = @{
    'api-key'      = $SearchKey
    'Content-Type' = 'application/json'
}

function Initialize-Index {
    $url = "$SearchUrl/indexes/$IndexName`?api-version=$ApiVersion"

    try {
        $null = Invoke-RestMethod -Uri $url -Method Get -Headers $script:HttpHeaders
        Write-Host "[index] '$IndexName' already exists."
        return
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -ne 404) { throw }
    }

    Write-Host "[index] Creating '$IndexName'..."

    $indexDef = @{
        name   = $IndexName
        fields = @(
            @{ name = 'id';         type = 'Edm.String'; key = $true;  searchable = $false; retrievable = $true  }
            @{ name = 'filePath';   type = 'Edm.String'; key = $false; searchable = $true;  filterable  = $true; retrievable = $true }
            @{ name = 'content';    type = 'Edm.String'; key = $false; searchable = $true;  retrievable = $true  }
            @{ name = 'chunkIndex'; type = 'Edm.Int32';  key = $false; searchable = $false; filterable  = $true; retrievable = $true }
            @{ name = 'language';   type = 'Edm.String'; key = $false; searchable = $false; filterable  = $true; retrievable = $true }
        )
    }

    $null = Invoke-RestMethod -Uri $url -Method Put -Headers $script:HttpHeaders `
                              -Body ($indexDef | ConvertTo-Json -Depth 6)
    Write-Host "[index] '$IndexName' created."
}

function Escape-JsonString([string]$s) {
    # Self-contained, dependency-free JSON string escaper.
    # Works on PS 5.1 (.NET Framework) and PS 7 (.NET Core).
    # ConvertTo-Json in PS 5.1 silently emits some control chars unescaped;
    # JavaScriptSerializer is unavailable in .NET Core — so we build it ourselves.
    $sb = [System.Text.StringBuilder]::new($s.Length + 32)
    foreach ($c in $s.ToCharArray()) {
        $n = [int][char]$c
        switch ($n) {
            34  { [void]$sb.Append('\"') }                              # "
            92  { [void]$sb.Append('\\') }                              # \
            8   { [void]$sb.Append('\b') }                              # backspace
            9   { [void]$sb.Append('\t') }                              # tab
            10  { [void]$sb.Append('\n') }                              # newline
            12  { [void]$sb.Append('\f') }                              # form feed
            13  { [void]$sb.Append('\r') }                              # carriage return
            default {
                if ($n -lt 32 -or $n -eq 127) {
                    [void]$sb.Append('\u{0:x4}' -f $n)                  # other control chars
                } else {
                    [void]$sb.Append($c)
                }
            }
        }
    }
    return $sb.ToString()
}

function ConvertTo-BatchJson([object[]]$Docs) {
    $items = @(foreach ($d in $Docs) {
        $parts = @(
            '"@search.action":"' + (Escape-JsonString ([string]$d['@search.action'])) + '"'
            '"id":"'             + (Escape-JsonString ([string]$d['id']))              + '"'
            '"filePath":"'       + (Escape-JsonString ([string]$d['filePath']))        + '"'
            '"content":"'        + (Escape-JsonString ([string]$d['content']))         + '"'
            '"chunkIndex":'      + ([int]$d['chunkIndex'])
            '"language":"'       + (Escape-JsonString ([string]$d['language']))        + '"'
        )
        '{' + ($parts -join ',') + '}'
    })
    return '{"value":[' + ($items -join ',') + ']}'
}

function Send-Batch([object[]]$Docs) {
    if (-not $Docs -or $Docs.Count -eq 0) { return }

    $url      = "$SearchUrl/indexes/$IndexName/docs/index?api-version=$ApiVersion"
    $bodyStr  = ConvertTo-BatchJson $Docs
    # Encode as UTF-8 bytes — Invoke-RestMethod on PS 5.1 uses the system code page
    # for string bodies, which corrupts non-ASCII content in multi-byte source files.
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($bodyStr)

    $resp = Invoke-RestMethod -Uri $url -Method Post -Headers $script:HttpHeaders `
                              -Body $bodyBytes -ContentType 'application/json; charset=utf-8'

    foreach ($r in $resp.value) {
        if (-not $r.status) {
            Write-Warning "  [!] Failed to upsert id='$($r.key)': $($r.errorMessage)"
        }
    }
}

# ── Main ──────────────────────────────────────────────────────────────────────

$RepoPath = (Resolve-Path $RepoPath).Path
Write-Host "Repository : $RepoPath"
Write-Host "Index      : $IndexName @ $SearchUrl"
Write-Host ''

Initialize-Index

$excludedSegments = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase
)
[void] $excludedSegments.Add('bin')
[void] $excludedSegments.Add('obj')

$files = Get-ChildItem -Path $RepoPath -Recurse -File |
    Where-Object {
        # Only .cs and .md
        if ($_.Extension -notin '.cs', '.md')   { return $false }
        # No generated files
        if ($_.Name -like '*.generated.cs')      { return $false }
        # Not inside bin/ or obj/
        $rel      = $_.FullName.Substring($RepoPath.Length)
        $segments = $rel -split '[\\/]'
        foreach ($seg in $segments) {
            if ($excludedSegments.Contains($seg)) { return $false }
        }
        return $true
    }

Write-Host "Files found : $($files.Count)"
Write-Host ''

$batch     = [System.Collections.Generic.List[object]]::new()
$fileCount = 0
$docCount  = 0
$errCount  = 0

foreach ($f in $files) {
    $rel  = ($f.FullName.Substring($RepoPath.Length).TrimStart('\', '/')) -replace '\\', '/'
    $ext  = $f.Extension.ToLower()
    $lang = Get-Language $ext

    try {
        $lines  = @(Get-Content -Path $f.FullName -Encoding UTF8)
        $chunks = @(Get-Chunks -Lines $lines -Ext $ext)
    }
    catch {
        Write-Warning "Skipping '$rel': $($_.Exception.Message)"
        $errCount++
        continue
    }

    $fileCount++

    for ($i = 0; $i -lt $chunks.Count; $i++) {
        # Strip control characters that are invalid unescaped in JSON
        # (keeps \t 0x09, \n 0x0a, \r 0x0d; removes 0x00-0x08, 0x0b, 0x0c, 0x0e-0x1f, 0x7f)
        $content = [regex]::Replace($chunks[$i] -join "`n", '[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]', '')

        $batch.Add([ordered]@{
            '@search.action' = 'mergeOrUpload'
            'id'             = ConvertTo-DocId $rel $i
            'filePath'       = $rel
            'content'        = $content
            'chunkIndex'     = $i
            'language'       = $lang
        })

        if ($batch.Count -ge $BatchSize) {
            Send-Batch $batch.ToArray()
            $docCount += $batch.Count
            Write-Host "  Upserted $docCount chunks..."
            $batch.Clear()
        }
    }
}

if ($batch.Count -gt 0) {
    Send-Batch $batch.ToArray()
    $docCount += $batch.Count
}

Write-Host ''
Write-Host "Done. Files indexed: $fileCount, skipped: $errCount. Total chunks upserted: $docCount."
