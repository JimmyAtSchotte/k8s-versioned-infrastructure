& minikube -p minikube docker-env --shell powershell | Invoke-Expression
docker build -t localhost:5000/app-management-backend -f ./App.Management.Backend/Dockerfile . 
docker push localhost:5000/app-management-backend
kubectl rollout restart deployment app-management-backend