using System.Text;
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

    public Worker(string queueHost, string queue, string username, string password)
    { 
        _queue = queue;
        _factory = new ConnectionFactory()
        {
            HostName = queueHost,
            UserName = username,
            Password = password
        };
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
            
            Console.WriteLine(" [*] Waiting for logs.");
            
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] {message}");
            };
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

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _channel?.Close();
        _connection?.Close();
        Console.WriteLine("Stopping worker");
        await base.StopAsync(stoppingToken);
    }
}