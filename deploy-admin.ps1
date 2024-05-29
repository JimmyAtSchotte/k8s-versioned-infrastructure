& minikube -p minikube docker-env --shell powershell | Invoke-Expression
docker build -t localhost:5000/admin -f ./Admin/Dockerfile . 
docker push localhost:5000/admin
kubectl rollout restart deployment admin-deployment