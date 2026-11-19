#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$SecretsDir = ".\secrets",

    [string]$Account = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name,

    [Parameter(Position = 1)]
    [string]$SwaggerAuthFile = ".\nginx\.htpasswd"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Path $SecretsDir -Force | Out-Null
$SecretsRoot = (Resolve-Path -LiteralPath $SecretsDir).Path
$JwtKeyCountDefault = if ($env:JWT_KEY_COUNT) { $env:JWT_KEY_COUNT } else { "3" }

Write-Host "=== SimFramework Secrets Setup ==="
Write-Host "Target directory: $SecretsRoot"
Write-Host ""

function ConvertTo-PlainText {
    param(
        [Parameter(Mandatory = $true)][System.Security.SecureString]$SecureString
    )

    $pointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Get-Md5Bytes {
    param(
        [Parameter(Mandatory = $true)][System.Collections.Generic.List[byte]]$Bytes
    )

    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        return $md5.ComputeHash($Bytes.ToArray())
    }
    finally {
        $md5.Dispose()
    }
}

function Add-ByteList {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[byte]]$Target,
        [Parameter(Mandatory = $true)][byte[]]$Bytes
    )

    foreach ($byte in $Bytes) {
        $Target.Add($byte)
    }
}

function ConvertTo-ApacheBase64 {
    param(
        [Parameter(Mandatory = $true)][byte[]]$Hash
    )

    $alphabet = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray()
    $builder = [System.Text.StringBuilder]::new()

    function ConvertTo-Group {
        param(
            [Parameter(Mandatory = $true)][int]$Value,
            [Parameter(Mandatory = $true)][int]$Length
        )

        for ($i = 0; $i -lt $Length; $i++) {
            [void]$builder.Append($alphabet[$value -band 0x3f])
            $value = $value -shr 6
        }
    }

    ConvertTo-Group (([int]$Hash[0] -shl 16) -bor ([int]$Hash[6] -shl 8) -bor [int]$Hash[12]) 4
    ConvertTo-Group (([int]$Hash[1] -shl 16) -bor ([int]$Hash[7] -shl 8) -bor [int]$Hash[13]) 4
    ConvertTo-Group (([int]$Hash[2] -shl 16) -bor ([int]$Hash[8] -shl 8) -bor [int]$Hash[14]) 4
    ConvertTo-Group (([int]$Hash[3] -shl 16) -bor ([int]$Hash[9] -shl 8) -bor [int]$Hash[15]) 4
    ConvertTo-Group (([int]$Hash[4] -shl 16) -bor ([int]$Hash[10] -shl 8) -bor [int]$Hash[5]) 4
    ConvertTo-Group $Hash[11] 2

    return $builder.ToString()
}

function New-ApacheMd5Password {
    param(
        [Parameter(Mandatory = $true)][string]$Password
    )

    $alphabet = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray()
    $saltBytes = New-Object byte[] 8
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($saltBytes)
    }
    finally {
        $rng.Dispose()
    }

    $salt = -join ($saltBytes | ForEach-Object { $alphabet[($_ % 64)] })
    $passwordBytes = [System.Text.Encoding]::UTF8.GetBytes($Password)
    $saltTextBytes = [System.Text.Encoding]::UTF8.GetBytes($salt)
    $magicBytes = [System.Text.Encoding]::UTF8.GetBytes('$apr1$')

    $context = [System.Collections.Generic.List[byte]]::new()
    Add-ByteList $context $passwordBytes
    Add-ByteList $context $magicBytes
    Add-ByteList $context $saltTextBytes

    $seedContext = [System.Collections.Generic.List[byte]]::new()
    Add-ByteList $seedContext $passwordBytes
    Add-ByteList $seedContext $saltTextBytes
    Add-ByteList $seedContext $passwordBytes
    $hash = Get-Md5Bytes $seedContext

    $remaining = $passwordBytes.Length
    while ($remaining -gt 0) {
        $take = [Math]::Min($remaining, 16)
        Add-ByteList $context $hash[0..($take - 1)]
        $remaining -= 16
    }

    # Apache clears this digest before the password-length bit loop.
    $hash = [byte[]]::new(16)

    $lengthBits = $passwordBytes.Length
    while ($lengthBits -ne 0) {
        if (($lengthBits -band 1) -ne 0) {
            $context.Add($hash[0])
        }
        else {
            $context.Add($passwordBytes[0])
        }
        $lengthBits = $lengthBits -shr 1
    }

    $hash = Get-Md5Bytes $context

    for ($round = 0; $round -lt 1000; $round++) {
        $roundContext = [System.Collections.Generic.List[byte]]::new()
        if (($round -band 1) -ne 0) {
            Add-ByteList $roundContext $passwordBytes
        }
        else {
            Add-ByteList $roundContext $hash
        }

        if (($round % 3) -ne 0) {
            Add-ByteList $roundContext $saltTextBytes
        }
        if (($round % 7) -ne 0) {
            Add-ByteList $roundContext $passwordBytes
        }

        if (($round -band 1) -ne 0) {
            Add-ByteList $roundContext $hash
        }
        else {
            Add-ByteList $roundContext $passwordBytes
        }

        $hash = Get-Md5Bytes $roundContext
    }

    return ('$apr1$' + $salt + '$' + (ConvertTo-ApacheBase64 $hash))
}

function Write-SecretFile {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Value
    )

    $path = Join-Path $SecretsRoot $Name
    [System.IO.File]::WriteAllText($path, $Value, [System.Text.UTF8Encoding]::new($false))
}

function Set-JwtKeys {
    if (-not ($JwtKeyCountDefault -match '^[0-9]+$') -or [int]$JwtKeyCountDefault -lt 1) {
        Write-Error "JWT_KEY_COUNT must be a positive integer (default: 3)."
    }

    $countInput = Read-Host "Enter JWT key count [$JwtKeyCountDefault]"
    $countText = if ([string]::IsNullOrWhiteSpace($countInput)) { $JwtKeyCountDefault } else { $countInput }
    if (-not ($countText -match '^[0-9]+$') -or [int]$countText -lt 1) {
        Write-Error "JWT key count must be a positive integer."
    }

    $count = [int]$countText
    Write-Host "Generating JWT signing keys (count=$count)..."
    for ($i = 0; $i -lt $count; $i++) {
        $kid = $i + 1
        $bytes = New-Object byte[] 64
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $rng.GetBytes($bytes)
        }
        finally {
            $rng.Dispose()
        }
        $secret = [System.BitConverter]::ToString($bytes).Replace("-", "")
        $isActive = if ($i -eq 0) { "true" } else { "false" }

        Write-SecretFile "JwtSettings__SigningKeys__Keys__${i}__Kid" "$kid"
        Write-SecretFile "JwtSettings__SigningKeys__Keys__${i}__Secret" $secret
        Write-SecretFile "JwtSettings__SigningKeys__Keys__${i}__IsActive" $isActive
        Write-Host "  Kid=$kid (Keys__${i}__) [active=$isActive]"
    }
}

function Set-DatabaseConnection {
    $value = Read-Host "Enter database connection string (empty to skip)"
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Write-SecretFile "DataBase__ConnectionConfigs__0__ConnectionString" $value
        Write-Host "  Database connection string set."
    }
    else {
        Write-Host "  Database connection string skipped."
    }
}

function Set-RedisConnection {
    $value = Read-Host "Enter Redis connection string (empty to skip)"
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Write-SecretFile "Framework__Caching__RedisConnectionString" $value
        Write-Host "  Redis connection string set."
    }
    else {
        Write-Host "  Redis connection string skipped."
    }
}

function Set-SwaggerBasicAuth {
    $userName = Read-Host "Enter Swagger Basic Auth username (empty to skip)"
    if ([string]::IsNullOrWhiteSpace($userName)) {
        Write-Host "  Swagger Basic Auth skipped."
        return
    }
    if ($userName -notmatch '^[A-Za-z0-9._-]+$') {
        Write-Warning "Swagger username can only contain letters, digits, dot, underscore, and hyphen."
        return
    }

    $passwordSecure = Read-Host "Enter Swagger Basic Auth password" -AsSecureString
    $passwordConfirmSecure = Read-Host "Confirm Swagger Basic Auth password" -AsSecureString
    $password = ConvertTo-PlainText $passwordSecure
    $passwordConfirm = ConvertTo-PlainText $passwordConfirmSecure

    if ([string]::IsNullOrEmpty($password)) {
        Write-Warning "Swagger password cannot be empty."
        return
    }
    if ($password -cne $passwordConfirm) {
        Write-Warning "Swagger passwords do not match."
        return
    }

    $authDirectory = Split-Path -Parent $SwaggerAuthFile
    if (-not [string]::IsNullOrWhiteSpace($authDirectory)) {
        New-Item -ItemType Directory -Path $authDirectory -Force | Out-Null
    }

    $authPath = (Resolve-Path -LiteralPath $SwaggerAuthFile -ErrorAction SilentlyContinue).Path
    if (-not $authPath) {
        $authPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $SwaggerAuthFile))
    }

    $passwordHash = New-ApacheMd5Password $password
    [System.IO.File]::WriteAllText($authPath, "$($userName):$($passwordHash)`n", [System.Text.UTF8Encoding]::new($false))

    & icacls.exe $authPath /inheritance:r /grant:r "${Account}:F" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set ACL on Swagger Basic Auth file: $authPath"
    }

    Write-Host "  Swagger Basic Auth file set: $authPath"
}

function Set-RabbitMqConnection {
    $value = Read-Host "Enter RabbitMQ connection string (empty to skip)"
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Write-SecretFile "Framework__Messaging__ConnectionString" $value
        Write-Host "  RabbitMQ connection string set."
    }
    else {
        Write-Host "  RabbitMQ connection string skipped."
    }
}

while ($true) {
    Write-Host "Select a secret to set:"
    Write-Host "  1) JWT signing keys"
    Write-Host "  2) Database connection string"
    Write-Host "  3) Redis connection string"
    Write-Host "  4) RabbitMQ connection string"
    Write-Host "  5) Swagger Basic Auth (nginx .htpasswd)"
    Write-Host "  6) All secrets"
    Write-Host "  0) Finish"
    $choice = Read-Host "Choice [0-6]"

    switch ($choice) {
        "1" { Set-JwtKeys }
        "2" { Set-DatabaseConnection }
        "3" { Set-RedisConnection }
        "4" { Set-RabbitMqConnection }
        "0" { }
        "5" { Set-SwaggerBasicAuth }
        "6" {
            Set-JwtKeys
            Set-DatabaseConnection
            Set-RedisConnection
            Set-RabbitMqConnection
            Set-SwaggerBasicAuth
        }
        default { Write-Warning "Invalid choice." }
    }

    Write-Host ""
    if ($choice -eq "0") {
        break
    }
}

& icacls.exe $SecretsRoot /inheritance:r /grant:r "${Account}:(OI)(CI)F" | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Failed to set ACL on secret directory: $SecretsRoot"
}

Get-ChildItem -LiteralPath $SecretsRoot -File | ForEach-Object {
    & icacls.exe $_.FullName /inheritance:r /grant:r "${Account}:F" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set ACL on secret file: $($_.FullName)"
    }
}

Write-Host ""
Write-Host "=== Secrets created successfully ==="
Write-Host "Set environment variable: SIM_SECRETS_DIRECTORY=$SecretsRoot"
Write-Host ""
Write-Host "Files created:"
Get-ChildItem -LiteralPath $SecretsRoot -Force | Format-Table Name,Length,Mode -AutoSize
if (Test-Path -LiteralPath $SwaggerAuthFile) {
    Write-Host "Swagger Basic Auth file: $([System.IO.Path]::GetFullPath($SwaggerAuthFile))"
}
