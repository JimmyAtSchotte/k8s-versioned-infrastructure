& minikube -p minikube docker-env --shell powershell | Invoke-Expression
docker build -t localhost:5000/app-management -f ./App.Management/Dockerfile . 
docker push localhost:5000/app-management
kubectl rollout restart deployment app-management