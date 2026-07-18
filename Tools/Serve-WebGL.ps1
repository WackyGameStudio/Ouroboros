param(
    [Parameter(Mandatory = $false)]
    [string]$BuildDirectory = "Builds/Step05/WebGL",

    [Parameter(Mandatory = $false)]
    [ValidateRange(1024, 65535)]
    [int]$Port = 8055,

    [Parameter(Mandatory = $false)]
    [switch]$OpenBrowser
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$requestedRoot = if ([System.IO.Path]::IsPathRooted($BuildDirectory)) {
    $BuildDirectory
} else {
    Join-Path $projectRoot $BuildDirectory
}

$webRoot = [System.IO.Path]::GetFullPath($requestedRoot)
$indexPath = Join-Path $webRoot "index.html"
if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    throw "WebGL index was not found: $indexPath"
}

$mimeTypes = @{
    ".html" = "text/html; charset=utf-8"
    ".htm" = "text/html; charset=utf-8"
    ".js" = "application/javascript"
    ".mjs" = "application/javascript"
    ".wasm" = "application/wasm"
    ".json" = "application/json"
    ".css" = "text/css"
    ".png" = "image/png"
    ".jpg" = "image/jpeg"
    ".jpeg" = "image/jpeg"
    ".svg" = "image/svg+xml"
    ".ico" = "image/x-icon"
    ".data" = "application/octet-stream"
    ".unityweb" = "application/octet-stream"
}

$prefix = "http://127.0.0.1:$Port/"
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($prefix)
$listener.Start()

Write-Host "Serving OUROBOROS WebGL from $webRoot"
Write-Host "Open $prefix"
Write-Host "Press Ctrl+C to stop."

if ($OpenBrowser) {
    Start-Process $prefix
}

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        try {
            $relativePath = [System.Uri]::UnescapeDataString($context.Request.Url.AbsolutePath.TrimStart("/"))
            if ([string]::IsNullOrWhiteSpace($relativePath)) {
                $relativePath = "index.html"
            }

            $candidatePath = [System.IO.Path]::GetFullPath((Join-Path $webRoot $relativePath))
            $rootPrefix = $webRoot.TrimEnd(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            if (-not $candidatePath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
                -not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
                $context.Response.StatusCode = 404
                $context.Response.Close()
                continue
            }

            $contentPath = $candidatePath
            $extension = [System.IO.Path]::GetExtension($contentPath).ToLowerInvariant()
            if ($extension -eq ".br") {
                $context.Response.AddHeader("Content-Encoding", "br")
                $contentPath = [System.IO.Path]::GetFileNameWithoutExtension($contentPath)
                $extension = [System.IO.Path]::GetExtension($contentPath).ToLowerInvariant()
            } elseif ($extension -eq ".gz") {
                $context.Response.AddHeader("Content-Encoding", "gzip")
                $contentPath = [System.IO.Path]::GetFileNameWithoutExtension($contentPath)
                $extension = [System.IO.Path]::GetExtension($contentPath).ToLowerInvariant()
            }

            $context.Response.ContentType = if ($mimeTypes.ContainsKey($extension)) {
                $mimeTypes[$extension]
            } else {
                "application/octet-stream"
            }
            $context.Response.AddHeader("Cache-Control", "no-cache")

            $bytes = [System.IO.File]::ReadAllBytes($candidatePath)
            $context.Response.ContentLength64 = $bytes.LongLength
            if ($context.Request.HttpMethod -eq "HEAD") {
                $context.Response.Close()
                continue
            }

            $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $context.Response.OutputStream.Close()
        } catch {
            if ($context.Response.OutputStream.CanWrite) {
                $context.Response.StatusCode = 500
                $context.Response.Close()
            }
        }
    }
} finally {
    $listener.Stop()
    $listener.Close()
}
