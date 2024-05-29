& minikube -p minikube docker-env --shell powershell | Invoke-Expression
docker build -t localhost:5000/admin-api -f ./Admin.Api/Dockerfile . 
docker push localhost:5000/admin-api
kubectl rollout restart deployment admin-api-deployment