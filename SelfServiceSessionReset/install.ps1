Param(
    [Parameter(Mandatory=$true, HelpMessage = 'Application Pool username')][string]$username,
    [Parameter(Mandatory=$true, HelpMessage = 'Application Pool password')][System.Security.SecureString]$password,
    [Parameter(Mandatory=$true, HelpMessage = 'Path to SSSR files')][string]$path
)

Write-Progress "Installing prerequisites"
$ServicesToInstall = @(
"Web-Windows-Auth",
"Web-ISAPI-Ext",
"Web-Metabase",
"Web-WMI",
"NET-Framework-Features",
"Web-Asp-Net",
"Web-Asp-Net45",
"NET-HTTP-Activation",
"NET-Non-HTTP-Activ",
"Web-Static-Content",
"Web-Default-Doc",
"Web-Dir-Browsing",
"Web-Http-Errors",
"Web-Http-Redirect",
"Web-App-Dev",
"Web-Net-Ext",
"Web-Net-Ext45",
"Web-ISAPI-Filter",
"Web-Health",
"Web-Http-Logging",
"Web-Log-Libraries",
"Web-Request-Monitor",
"Web-HTTP-Tracing",
"Web-Security",
"Web-Filtering",
"Web-Url-Auth",
"Web-Performance",
"Web-Stat-Compression",
"Web-Mgmt-Console",
"Web-Scripting-Tools",
"Web-Mgmt-Compat"
)
 
Install-WindowsFeature -Name $ServicesToInstall -IncludeManagementTools

Write-Progress "Adding SSSR Application Pool"
New-WebAppPool -Name "SSSR"
$AppPool = Get-Item IIS:\AppPools\SSSR
$AppPool.processModel.userName = $username
$AppPool.processModel.password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
$AppPool.processModel.identityType = 3
Write-Progress "Setting SSSR Application Pool credentials"
$AppPool | Set-Item
Write-Progress "Creating SSSR Web Application under Default Web Site"
New-WebApplication -Name "SSSR" -Site "Default Web Site" -PhysicalPath $path -ApplicationPool "SSSR"
Set-WebConfigurationProperty -Filter '/system.webServer/security/authentication/anonymousAuthentication' -Name 'enabled' -Value 'false' -PSPath 'IIS:\' -Location "Default Web Site/SSSR"
Set-WebConfigurationProperty -Filter '/system.webServer/security/authentication/windowsAuthentication' -Name 'enabled' -Value 'true' -PSPath 'IIS:\' -Location "Default Web Site/SSSR"