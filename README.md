  # About

This proof of concept demonstrates using Kubernetes as a deployment engine to handle a multi-tenant environment with multiple versions of the same software.

- **App.Management**: A frontend application built with Blazor. It allows users to add, list, and update the versions of the environments.
- **App.Management.Backend**: Connects to a MongoDB database to persist the environmental applications. It also adds deployment messages to a queue when changes are made.
- **App.Queue.AppDeployments**: Reads from the RabbitMQ queue and communicates with the Kubernetes API to make changes to deployments.

# Getting Started

## Start Minikube

Start Minikube with the Docker driver:

```shell
minikube start --driver=docker
```

## Install Ingress Controller

Enable the Ingress controller in Minikube:

```shell
minikube addons enable ingress
```

## Apply Configuration and Secrets

Apply the necessary configuration and secret files:

```shell
kubectl apply -f k8s/config.yaml
kubectl apply -f k8s/secrets.yaml
```

## Deploy RabbitMQ Cluster Operator

Deploy the RabbitMQ Cluster Operator:

```shell
kubectl apply -f "https://github.com/rabbitmq/cluster-operator/releases/latest/download/cluster-operator.yml"
```

## Apply Deployments

Deploy the application components:

```shell
kubectl apply -f k8s/registry.yaml
kubectl apply -f k8s/app-management-db.yaml
kubectl apply -f k8s/app-management-backend.yaml
kubectl apply -f k8s/app-management.yaml
kubectl apply -f k8s/app-queue-appdeployment.yaml
```

## Install Seq

Use helm charts to install seq with config file:

```shell
helm install -f seq-config.yaml seq datalust/seq
```

## Set Docker Context to Default

Fix the error "Unable to resolve the current Docker CLI context 'default': context 'default'" by setting the Docker context to default:

```shell
docker context use default
```

## Set Environment Variables to Push to Registry Inside Kubernetes

Set the environment variables needed to push images to the registry inside Kubernetes:

```shell
& minikube -p minikube docker-env --shell powershell | Invoke-Expression
```

## Build and Push Images to Registry

### App Management

Build and push the App Management image:

```shell
docker build -t localhost:5000/app-management -f ./App.Management/Dockerfile .
docker push localhost:5000/app-management
```

### App Management Backend

Build and push the App Management Backend image:

```shell
docker build -t localhost:5000/app-management-backend -f ./App.Management.Backend/Dockerfile .
docker push localhost:5000/app-management-backend
```

### App Deployment Queue

Build and push the App Deployment Queue image:

```shell
docker build -t localhost:5000/app-queue-appdeployment -f ./App.Queue.AppDeployment/Dockerfile .
docker push localhost:5000/app-queue-appdeployment
```

### App

Use the APP-VERSION argument to push different versions of the app.

```shell
docker build --build-arg APP_VERSION=3.0 -t localhost:5000/app -f ./App/Dockerfile .
docker push localhost:5000/app:3.0
```

or use powershell

```shell
.\Powershell\deploy-app.ps1 -version "3.0"
```



## Create Tunnel to Kubernetes Services

Create a tunnel to access Kubernetes services:

```shell
minikube tunnel -c
```

## Add dns to hosts file

```shell
127.0.0.1 k8s-app-management.local
127.0.0.1 k8s-registry.local
127.0.0.1 k8s-app.local
127.0.0.1 k8s-seq.local
```

## View management page

[View management page at http://k8s-app-management.local](http://k8s-app-management.local)


# Troubleshooting

## Restart Deployments with Latest Images

Restart the deployments to use the latest images:

```shell
kubectl rollout restart deployment app-management
kubectl rollout restart deployment app-management-backend
kubectl rollout restart deployment app-queue-appdeployment
```

## Lookup DNS

Verify the DNS resolution for the services:

### App Management Service

```shell
kubectl run -i --tty --rm debug --image=busybox --restart=Never -- nslookup app-management-service.default.svc.cluster.local
```

### App Management Backend Service

```shell
kubectl run -i --tty --rm debug --image=busybox --restart=Never -- nslookup app-management-backend-service.default.svc.cluster.local
```

### App Queue AppDeployment Service

```shell
kubectl run -i --tty --rm debug --image=busybox --restart=Never -- nslookup app-queue-appdeployment-service.default.svc.cluster.local
```

## Check ingress logs

```shell
kubectl logs -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx
```