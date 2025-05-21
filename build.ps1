# build.ps1
param (
    [string]$ProjectDir = ".",
    [string]$PublishDir = "bin\Release\net9.0\publish",
    [string]$DockerTag = "asteriskami",
    [string]$Dockerfile = "Dockerfile",
    [string]$DockerfileDev = "Dockerfile.dev",
    [string]$Platform = "linux/arm/v7",  # <-- Default to Raspberry Pi
    [string]$ServerAddress = "10.1.10.207",
    [string]$ServerLogin = "repeater"
)

function Clean {
    Write-Host "🧹 Cleaning..."
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $PublishDir
}

function Build {
    Write-Host "🛠 Restoring and publishing .NET app..."
    dotnet restore $ProjectDir
    dotnet publish $ProjectDir `
    -c Release `
    -r linux-arm `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:SuppressTrimAnalysisWarnings=true `
    -o $PublishDir
}

function DockerBuild {
    #Build
    Write-Host "🐳 Building Docker image for platform: $Platform"

    if ($Platform -eq "native") {
        docker build -t $DockerTag -f $Dockerfile .
    }
    else {
        # Ensure buildx is enabled
        docker buildx create --use | Out-Null
        docker buildx build --platform $Platform -t $DockerTag --load -f $Dockerfile .
    }
}

function DockerBuildDev {
    Write-Host "🔧 Building Dev Docker image..."
    docker build -t "$DockerTag-dev" -f $DockerfileDev .
}

function Push {
    Write-Host "📤 Uploading publish folder..."
    $remotePath = "/home/${ServerLogin}/${DockerTag}"
    ssh ${ServerLogin}@${ServerAddress} "mkdir -p ${remotePath}"
    scp -r $PublishDir ${ServerLogin}@${ServerAddress}:$remotePath
    #scp $Dockerfile ${ServerLogin}@${ServerAddress}:$remotePath
}

function RemoteRun {
    Write-Host "🚀 Building + Running container on Raspberry Pi..."
    $remoteScript = @"
cd /home/${ServerLogin}/${DockerTag}
sudo docker build -t ${DockerTag} -f Dockerfile .
sudo docker stop ${DockerTag} 2>/dev/null || true
sudo docker rm ${DockerTag} 2>/dev/null || true
sudo docker run -d --name ${DockerTag} --network host ${DockerTag}
"@
    $remoteScript -replace "`r`n", "`n" | Out-File -FilePath remote_script.sh -Encoding utf8
    ssh ${ServerLogin}@${ServerAddress} $remoteScript
}

# Run all steps
#Clean
#Build
Push
#RemoteRun
