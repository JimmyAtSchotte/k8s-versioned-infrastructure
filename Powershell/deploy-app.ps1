param (
    [string]$version = "1.0"
)

# Define image name and tag
$imageName = "localhost:5000/app"
$imageTag = "${imageName}:${version}"

# Build the Docker image with the specified version
Write-Output "Building Docker image with version $version..."
docker build --build-arg APP_VERSION=$version -t $imageTag -f ./App/Dockerfile .

# Push the Docker image to the registry
Write-Output "Pushing Docker image to registry..."
docker push $imageTag

Write-Output "Done. Image $imageTag has been built and pushed."
