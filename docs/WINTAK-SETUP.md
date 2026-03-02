# WinTAK Environment Setup

## Required: Fix System.Runtime.CompilerServices.Unsafe Assembly

WinTAK 5.4 ships with a misconfigured assembly binding. The `WinTAK.exe.config` has a binding redirect for `System.Runtime.CompilerServices.Unsafe` pointing to version `6.0.0.0`, but the actual DLL on disk is version `4.0.4.1`. This breaks Google.Protobuf operations.

### Symptoms
If this is not fixed, you'll see errors like:
```
Could not load file or assembly 'System.Runtime.CompilerServices.Unsafe, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
```

### Fix (One-Time Setup)

1. Download `System.Runtime.CompilerServices.Unsafe` version 6.0.0 from NuGet:
   - https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.0.0

2. Extract the `.nupkg` file (it's a ZIP archive)

3. Copy `lib/net461/System.Runtime.CompilerServices.Unsafe.dll` to:
   ```
   C:\Program Files\WinTAK\System.Runtime.CompilerServices.Unsafe.dll
   ```
   (This replaces the existing 4.0.4.1 version)

4. Verify the fix:
   ```powershell
   [System.Reflection.AssemblyName]::GetAssemblyName('C:\Program Files\WinTAK\System.Runtime.CompilerServices.Unsafe.dll').Version
   ```
   Should show: `6.0.0.0`

### Automated Script

Run this PowerShell script as Administrator:

```powershell
# Download and install correct Unsafe.dll for WinTAK
$tempDir = "$env:TEMP\wintak-fix"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

# Download NuGet package
$url = "https://www.nuget.org/api/v2/package/System.Runtime.CompilerServices.Unsafe/6.0.0"
$nupkg = "$tempDir\unsafe.nupkg"
Invoke-WebRequest -Uri $url -OutFile $nupkg

# Extract
Expand-Archive -Path $nupkg -DestinationPath "$tempDir\pkg" -Force

# Copy to WinTAK
$source = "$tempDir\pkg\lib\net461\System.Runtime.CompilerServices.Unsafe.dll"
$dest = "C:\Program Files\WinTAK\System.Runtime.CompilerServices.Unsafe.dll"
Copy-Item -Path $source -Destination $dest -Force

# Verify
$version = [System.Reflection.AssemblyName]::GetAssemblyName($dest).Version
Write-Host "Installed version: $version"

# Cleanup
Remove-Item -Recurse -Force $tempDir
```

### Why This Happens

WinTAK's `WinTAK.exe.config` contains:
```xml
<dependentAssembly>
    <assemblyIdentity name="System.Runtime.CompilerServices.Unsafe" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
    <bindingRedirect oldVersion="4.0.0.0-6.0.0.0" newVersion="6.0.0.0" />
</dependentAssembly>
```

This tells .NET to redirect all requests for versions 4.0.0.0 through 6.0.0.0 to version 6.0.0.0. However, WinTAK ships with version 4.0.4.1, which doesn't match. When Google.Protobuf (which WinTAK also ships) tries to use this assembly, it fails.

This is a WinTAK packaging bug that should be reported to TAK Product Center.
