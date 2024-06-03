using RabbitMQ.Client;

namespace Admin.Api.Services;

public class RabbitMqService : IDisposable
{
    private readonly string _queueName;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqService(string hostname, string queueName, string username, string password)
    {
        _queueName = queueName;
        var factory = new ConnectionFactory()
        {
            HostName = hostname,
            UserName = username,
            Password = password
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
    }

    public void SendMessage(string message)
    {
        var body = System.Text.Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(exchange: "", routingKey: _queueName, basicProperties: null, body: body);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}