& minikube -p minikube docker-env --shell powershell | Invoke-Expression
docker build -t localhost:5000/app-queue-appdeployment -f ./App.Queue.AppDeployment/Dockerfile . 
docker push localhost:5000/app-queue-appdeployment
kubectl rollout restart deployment app-queue-appdeployment