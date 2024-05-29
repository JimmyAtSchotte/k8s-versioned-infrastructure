& minikube -p minikube docker-env --shell powershell | Invoke-Expression
docker build -t localhost:5000/admin-events-app-version-listner -f ./Admin.Events.AppVersionListner/Dockerfile . 
docker push localhost:5000/admin-events-app-version-listner
kubectl rollout restart deployment admin-events-app-version-listner-deployment