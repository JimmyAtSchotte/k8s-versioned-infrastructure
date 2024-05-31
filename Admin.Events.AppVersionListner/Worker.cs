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
            
            Console.WriteLine($"Action: {queueMessage.Action} (version: {queueMessage.Version})");

            var app = queueMessage.Data;

            if (app?.Image is null)
                return;

            if (queueMessage.Action == "Delete")
            {
                await _kubernetesClient.CoreV1.DeleteNamespaceAsync(queueMessage.Data.GetNamespace());
                return;
            }
            
            await NamespaceResource(cancellationToken, app);
            await DeploymentResource(cancellationToken, app);
            await ServiceResource(cancellationToken, app);
            await IngressResource(cancellationToken, app);
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task IngressResource(CancellationToken cancellationToken, ApplicationData app)
    {
        var ingresses = await _kubernetesClient.NetworkingV1.ListNamespacedIngressAsync(app.GetNamespace(), cancellationToken: cancellationToken);
        var ingress = ingresses.Items.FirstOrDefault(x => x.BelongsToApp(app)) ?? new V1Ingress();
        ingress = ingress.ApplyApp(app);
         
        if (ingresses.Items.Any(x => x.BelongsToApp(app)))
            await _kubernetesClient.NetworkingV1.ReplaceNamespacedIngressAsync(ingress, app.GetIngressName(), app.GetNamespace(), cancellationToken: cancellationToken);
        else
            await _kubernetesClient.NetworkingV1.CreateNamespacedIngressAsync(ingress,app.GetNamespace(), cancellationToken: cancellationToken);
    }

    private async Task ServiceResource(CancellationToken cancellationToken, ApplicationData app)
    {
        var services = await _kubernetesClient.CoreV1.ListNamespacedServiceAsync(app.GetNamespace(), cancellationToken: cancellationToken);
        var service = services.Items.FirstOrDefault(x => x.BelongsToApp(app)) ?? new V1Service();
        service = service.ApplyApp(app);
            
        if (services.Items.Any(x => x.BelongsToApp(app)))
            await _kubernetesClient.CoreV1.ReplaceNamespacedServiceAsync(service, app.GetServiceName(), app.GetNamespace(), cancellationToken: cancellationToken);
        else
            await _kubernetesClient.CoreV1.CreateNamespacedServiceAsync(service, app.GetNamespace(), cancellationToken: cancellationToken);
    }

    private async Task DeploymentResource(CancellationToken cancellationToken, ApplicationData app)
    {
        var deployments = await _kubernetesClient.AppsV1.ListNamespacedDeploymentAsync(app.GetNamespace(), cancellationToken: cancellationToken);
        var deployment = deployments.Items.FirstOrDefault(x => x.BelongsToApp(app)) ?? new V1Deployment();
        deployment = deployment.ApplyApp(app);
            
        if (deployments.Items.Any(x => x.BelongsToApp(app)))
            await _kubernetesClient.AppsV1.ReplaceNamespacedDeploymentAsync(deployment, app.GetDeploymentName(), app.GetNamespace(), cancellationToken: cancellationToken);
        else
            await _kubernetesClient.AppsV1.CreateNamespacedDeploymentAsync(deployment, app.GetNamespace(), cancellationToken: cancellationToken);
    }

    private async Task NamespaceResource(CancellationToken cancellationToken, ApplicationData app)
    {
        var namespaces = await _kubernetesClient.CoreV1.ListNamespaceAsync(cancellationToken: cancellationToken);
        if(namespaces.Items.All(x => x.Metadata.Name != app.GetNamespace()))
            await CreateNamespaceAsync(app, cancellationToken);
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

   

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _channel?.Close();
        _connection?.Close();
        Console.WriteLine("Stopping worker");
        await base.StopAsync(stoppingToken);
    }

    
}