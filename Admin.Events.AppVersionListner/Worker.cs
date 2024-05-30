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
            
            var app = JsonSerializer.Deserialize<ApplicationVersionMessage>(message);
        
            if(app?.Image is null)
                return;
        
            var namespaces = await _kubernetesClient.CoreV1.ListNamespaceAsync(cancellationToken: cancellationToken);
            var ns = namespaces.Items.FirstOrDefault(x => x.Metadata.Name == app.Name) ??
                     await CreateNamespaceAsync(app, cancellationToken);

            var deployment = CreateDeployment(app, ns);
            var service = CreateService(deployment);
            var ingress = CreateIngress(service, app);
            
            await _kubernetesClient.AppsV1.CreateNamespacedDeploymentAsync(deployment, ns.Metadata.Name, cancellationToken: cancellationToken);
            await _kubernetesClient.CoreV1.CreateNamespacedServiceAsync(service, ns.Metadata.Name, cancellationToken: cancellationToken);
            await _kubernetesClient.NetworkingV1.CreateNamespacedIngressAsync(ingress, ns.Metadata.Name, cancellationToken: cancellationToken);

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private V1Ingress CreateIngress(V1Service service, ApplicationVersionMessage app)
    {
        return new V1Ingress()
        {
            Metadata = new V1ObjectMeta()
            {
                Name = $"{service.Metadata.Name}-ingress",
                Annotations = new Dictionary<string, string>()
                {
                    { "nginx.ingress.kubernetes.io/rewrite-target", "/" }
                }
            },
            Spec = new V1IngressSpec()
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
                                    Path = $"/{app.Name}",
                                    Backend = new V1IngressBackend()
                                    {
                                        Service = new V1IngressServiceBackend()
                                        {
                                            Name = service.Metadata.Name,
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
            }
        };
    }


    private V1Service CreateService(V1Deployment deployment)
    {
        return new V1Service()
        {
            Metadata = new V1ObjectMeta()
            {
                Name = $"{deployment.Metadata.Name}-service",
                NamespaceProperty = deployment.Metadata.NamespaceProperty
            },
            Spec = new V1ServiceSpec()
            {
                Selector = deployment.Spec.Selector.MatchLabels,
                Ports = new List<V1ServicePort>()
                {
                    new V1ServicePort()
                    {
                        Port = 80,
                        TargetPort = 8080,
                        Protocol = "TCP"
                    }
                }
            }
        };
    }

    private async Task<V1Namespace> CreateNamespaceAsync(ApplicationVersionMessage app, CancellationToken cancellationToken)
    {
        try {
            var ns = new V1Namespace()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = $"app-{app.Name}-ns"
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

    private V1Deployment CreateDeployment(ApplicationVersionMessage app, V1Namespace ns)
    {
        var appLabel = $"app-{app.Name}";
        
       return new V1Deployment
        {
            Metadata = new V1ObjectMeta
            {
                Name = appLabel,
                NamespaceProperty = ns.Metadata.Name
            },
            Spec = new V1DeploymentSpec
            {
                Selector = new V1LabelSelector
                {
                    MatchLabels = new System.Collections.Generic.Dictionary<string, string> { { "app", appLabel } }
                },
                Replicas = 1,
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = new System.Collections.Generic.Dictionary<string, string> { { "app", appLabel } }
                    },
                    Spec = new V1PodSpec
                    {
                        Containers = new[]
                        {
                            new V1Container
                            {
                                Name = appLabel,
                                Image = $"localhost:5000/webapplication:{app.Image}"
                            }
                        }
                    }
                }
            }
        };
    }
    
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _channel?.Close();
        _connection?.Close();
        Console.WriteLine("Stopping worker");
        await base.StopAsync(stoppingToken);
    }
}

public class ApplicationVersionMessage
{
    public string Name { get; set; }
    public string Image { get; set; }
}

public class CreateNamespaceException : Exception
{
    public CreateNamespaceException(Exception inner) : base("Failed to create namespace", inner) { }
}