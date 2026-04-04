[CmdletBinding()]
param(
    [string]$Source = ".",

    [Parameter(Mandatory = $true)]
    [string]$Target
)

class ErrorDTO {
    [int]    $Id
    [string] $Path
    
    ErrorDTO([int]$id, [string]$path) {
        $this.Id   = $id
        $this.Path = $path
    }
}

function Merge-Pdf {
    [CmdletBinding()]
    param(
        [string]$Source = ".",

        [Parameter(Mandatory = $true)]
        [string]$Target
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        Write-Error "Unable to find the source directory: '$Source'"
        return
    }
    $absSource = (Resolve-Path -LiteralPath $Source).ProviderPath

    if (-not (Test-Path -LiteralPath $Target)) {
        try {
            New-Item -Path $Target -ItemType Directory -ErrorAction Stop | Out-Null
            Write-Verbose "Created target directory: '$Target'"
        }
        catch {
            Write-Error "Failed to create target directory: '$Target'. Error: $_"
            return
        }
    }
    $absTarget = (Resolve-Path -LiteralPath $Target).ProviderPath

    if (-not (Get-Command "RanaPdfTool.exe" -ErrorAction SilentlyContinue)) {
        Write-Error "Unable to find 'RanaPdfTool.exe'. Please ensure it is in the PATH."
        return
    }

    $subFolders = Get-ChildItem -LiteralPath $absSource -Directory
    if ($subFolders.Count -eq 0) {
        Write-Warning "No subdirectories found in '$absSource'. Please run RanaPdfTool.exe manually for the desired folder."
        return
    }

    $processing = 0
    $total = $subFolders.Count
    $errorList = [System.Collections.Generic.List[ErrorDTO]]::new()

    foreach ($folder in $subFolders) {
        $processing++
        Write-Host ""
        Write-Host "`e[30;47m  $processing / $total - $($folder.Name) `e[0m"
        Write-Host ""

        $arguments = @("merge", "--source", "`"$($folder.FullName)`"", "--destination", "`"$absTarget`"", "--resize")
        $processResult = Start-Process -FilePath "RanaPdfTool.exe" -ArgumentList $arguments -NoNewWindow -Wait -PassThru
        $exitCode = $processResult.ExitCode

        if ($exitCode -ne 0) {
            $errorList.Add([ErrorDTO]::new($processing, $folder.Name))
        }
    }

    $errorCount = $errorList.Count
    if ( $errorCount -gt 0) {
        Write-Host ""
        Write-Warning "$errorCount folder(s) out of $total failed to process"
        return $errorList
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Merge-Pdf @PSBoundParameters | Set-Variable -Name failedItems

    if ($failedItems.Count -gt 0) {
        $failedItems | Format-Table -AutoSize
    }
}
