& minikube -p minikube docker-env --shell powershell | Invoke-Expression
docker build -t localhost:5000/admin-web-app-version-watcher -f ./Admin.WebAppVersionWatcher/Dockerfile . 
docker push localhost:5000/admin-web-app-version-watcher
kubectl rollout restart deployment admin-web-app-version-watcher