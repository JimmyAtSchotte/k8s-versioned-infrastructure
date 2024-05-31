using System.Text;
using System.Text.Json;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Admin.Events.AppVersionListner;

public class Worker : BackgroundService
{
    private readonly string _queue;
    private readonly ConnectionFactory _factory;
    private IConnection _connection;
    private IModel _channel;
    private readonly Kubernetes _kubernetesClient;

    public Worker(string queueHost, string queue, string username, string password)
    {
        _queue = queue;
        _factory = new ConnectionFactory()
        {
            HostName = queueHost,
            UserName = username,
            Password = password
        };

        var config = KubernetesClientConfiguration.InClusterConfig();
        _kubernetesClient = new Kubernetes(config);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(exchange: "logs", type: ExchangeType.Fanout);

            var queueName = _channel.QueueDeclare(queue: _queue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
            _channel.QueueBind(queue: queueName, exchange: "logs", routingKey: string.Empty);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (sender, args) => await ConsumerOnReceived(args, stoppingToken);
            _channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task ConsumerOnReceived(BasicDeliverEventArgs args, CancellationToken cancellationToken)
    {
        try
        {
            var body = args.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Received: {message}");
            
            var queueMessage = JsonSerializer.Deserialize<QueueMessage<ApplicationData>>(message);
            
            Console.WriteLine($"Action: {queueMessage.Action} (version: {queueMessage})");

            var app = queueMessage.Data;

            if (app?.Image is null)
                return;

            if (queueMessage.Action == "Delete")
            {
                await _kubernetesClient.CoreV1.DeleteNamespaceAsync(queueMessage.Data.GetNamespace());
                return;
            }
            
            var namespaces = await _kubernetesClient.CoreV1.ListNamespaceAsync(cancellationToken: cancellationToken);
            var ns = namespaces.Items.FirstOrDefault(x => x.Metadata.Name == app.GetNamespace()) ??
                     await CreateNamespaceAsync(app, cancellationToken);
            
            var deployments = await _kubernetesClient.AppsV1.ListNamespacedDeploymentAsync(ns.Metadata.Name, cancellationToken: cancellationToken);
            var deployment = deployments.Items.FirstOrDefault(x => x.Metadata.Name == app.GetDeploymentName());
            var deploymentExists = deployment is not null;
            
            var services = await _kubernetesClient.CoreV1.ListNamespacedServiceAsync(ns.Metadata.Name, cancellationToken: cancellationToken);
            var service = services.Items.FirstOrDefault(x => x.Metadata.Name == app.GetServiceName());
            var serviceExists = deployment is not null;
            
            var ingresses = await _kubernetesClient.NetworkingV1.ListNamespacedIngressAsync(ns.Metadata.Name, cancellationToken: cancellationToken);
            var ingress = ingresses.Items.FirstOrDefault(x => x.Metadata.Name == app.GetIngressName());
            var ingressExists = deployment is not null;

            deployment = ApplyDeployment(deploymentExists ? deployment : new V1Deployment(), app);
            service = ApplyService(serviceExists ? service : new V1Service(), app);
            ingress = ApplyIngress(ingressExists ? ingress : new V1Ingress(), app);
            
            Console.WriteLine($"deployments: {JsonSerializer.Serialize(deployments)}");
            Console.WriteLine($"deploymentExists: {deploymentExists} ({app.GetDeploymentName()})");
            
            Console.WriteLine($"services: {JsonSerializer.Serialize(services)}");
            Console.WriteLine($"serviceExists: {serviceExists} ({app.GetServiceName()})");
            
            Console.WriteLine($"ingresses: {JsonSerializer.Serialize(ingresses)}");
            Console.WriteLine($"ingressExists: {ingressExists} ({app.GetIngressName()})");

            if (deploymentExists)
                await _kubernetesClient.AppsV1.ReplaceNamespacedDeploymentAsync(deployment, app.GetDeploymentName(), app.GetNamespace(), cancellationToken: cancellationToken);
            else
                await _kubernetesClient.AppsV1.CreateNamespacedDeploymentAsync(deployment, app.GetNamespace(), cancellationToken: cancellationToken);

            if (serviceExists)
                await _kubernetesClient.CoreV1.ReplaceNamespacedServiceAsync(service, app.GetServiceName(), app.GetNamespace(), cancellationToken: cancellationToken);
            else
                await _kubernetesClient.CoreV1.CreateNamespacedServiceAsync(service, app.GetNamespace(), cancellationToken: cancellationToken);
            
            if(ingressExists)
                await _kubernetesClient.NetworkingV1.ReplaceNamespacedIngressAsync(ingress, app.GetIngressName(), app.GetNamespace(), cancellationToken: cancellationToken);
            else
                await _kubernetesClient.NetworkingV1.CreateNamespacedIngressAsync(ingress,app.GetNamespace(), cancellationToken: cancellationToken);
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private V1Patch ConvertToPatch<T>(T resource)
    {
        var json = JsonSerializer.Serialize(resource); 
        return new V1Patch(json, V1Patch.PatchType.MergePatch);
    }

    private static V1Ingress ApplyIngress(V1Ingress ingress, ApplicationData app)
    {
        ingress.Metadata = new V1ObjectMeta()
        {
            Name = app.GetIngressName(),
            Annotations = new Dictionary<string, string>()
            {
                { "nginx.ingress.kubernetes.io/rewrite-target", "/" }
            }
        };
        ingress.Spec = new V1IngressSpec()
        {
            IngressClassName = "nginx",
            Rules = new List<V1IngressRule>()
            {
                new V1IngressRule()
                {
                    Host = "k8s-app.local",
                    Http = new V1HTTPIngressRuleValue()
                    {
                        Paths = new List<V1HTTPIngressPath>()
                        {
                            new V1HTTPIngressPath()
                            {
                                PathType = "Prefix",
                                Path = app.GetPath(),
                                Backend = new V1IngressBackend()
                                {
                                    Service = new V1IngressServiceBackend()
                                    {
                                        Name = app.GetServiceName(),
                                        Port = new V1ServiceBackendPort()
                                        {
                                            Number = 80
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return ingress;
    }
    private V1Service ApplyService(V1Service service, ApplicationData app)
    {
        service.Metadata = new V1ObjectMeta()
        {
            Name = app.GetServiceName(),
            NamespaceProperty = app.GetNamespace()
        };
        service.Spec = new V1ServiceSpec()
        {
            Selector = new Dictionary<string, string>()
            {
                ["app"] = app.GetLabel()
            },
            Ports = new List<V1ServicePort>()
            {
                new V1ServicePort()
                {
                    Port = 80,
                    TargetPort = 8080,
                    Protocol = "TCP"
                }
            }
        };

        return service;
    }
    private async Task<V1Namespace> CreateNamespaceAsync(ApplicationData app,
        CancellationToken cancellationToken)
    {
        try
        {
            var ns = new V1Namespace()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = app.GetNamespace()
                }
            };

            ns.Validate();

            return await _kubernetesClient.CoreV1.CreateNamespaceAsync(ns, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            throw new CreateNamespaceException(e);
        }
    }

    private V1Deployment ApplyDeployment(V1Deployment deployment, ApplicationData app)
    {
        deployment.Metadata = new V1ObjectMeta
        {
            Name = app.GetDeploymentName(),
            NamespaceProperty = app.GetNamespace()
        };

        deployment.Spec = new V1DeploymentSpec
        {
            Selector = new V1LabelSelector
            {
                MatchLabels = new System.Collections.Generic.Dictionary<string, string> { { "app", app.GetLabel() } }
            },
            Replicas = 1,
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new System.Collections.Generic.Dictionary<string, string> { { "app", app.GetLabel() } }
                },
                Spec = new V1PodSpec
                {
                    Containers = new[]
                    {
                        new V1Container
                        {
                            Name = app.GetLabel(),
                            Image = $"localhost:5000/webapplication:{app.Image}"
                        }
                    }
                }
            }
        };

        return deployment;

    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _channel?.Close();
        _connection?.Close();
        Console.WriteLine("Stopping worker");
        await base.StopAsync(stoppingToken);
    }
}

public class ApplicationData
{
    public string Name { get; set; }
    public string Image { get; set; }

    public string GetNamespace() => $"ns-{Name}";
    public string GetLabel() => $"app-{Name}";
    public string GetServiceName() => $"svc-{Name}";
    public string GetDeploymentName() => $"app-{Name}";
    public string GetIngressName() => $"ingr-{Name}";
    public string GetPath() => $"/{Name}";
}

public class CreateNamespaceException : Exception
{
    public CreateNamespaceException(Exception inner) : base("Failed to create namespace", inner) { }
}


public class QueueMessage<T> 
{
    public string Action { get; set; }
    public string Version { get; set; }
    public T Data { get; set; }
}