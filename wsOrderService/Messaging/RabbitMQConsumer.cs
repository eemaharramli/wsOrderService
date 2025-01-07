using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace wsOrderService.Messaging
{
    public class RabbitMQConsumer
    {
        private readonly string _queueName;
        private readonly string _hostname;

        public RabbitMQConsumer(string queueName, string hostname = "rabbitmq")
        {
            _queueName = queueName;
            _hostname = hostname;
        }

        public async Task StartListeningAsync(Func<string, Task> messageHandler)
        {
            var factory = new ConnectionFactory
            {
                HostName = _hostname,
                UserName = "user",
                Password = "strongpassword"
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                await messageHandler(message);
            };

            await channel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: true,
                consumer: consumer
            );
        }
    }
}
