$gitVersionVersion = '5.1.2';    

function InstallGitVersion {
    Write-Host "Installing GitVersion";
    & dotnet tool update --global GitVersion.Tool --version $gitVersionVersion
}

function GetVersion {        
    Write-Host "Running GitVersion";
    [string]$versionInfoString = & dotnet gitversion
    $versionInfo = ConvertFrom-Json $versionInfoString;
    return $versionInfo.LegacySemVer;    
}

InstallGitVersion
$version = GetVersion;
Write-Host "##vso[build.updatebuildnumber]$version";

& dotnet pack -c Release -o ./artifacts -p:Version=$version