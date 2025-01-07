using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace wsOrderService.Messaging
{
    public class RabbitMQProducer
    {
        private readonly ConnectionFactory _factory;

        public RabbitMQProducer(ConnectionFactory factory)
        {
            _factory = new ConnectionFactory
            {
                HostName = "rabbitmq",
                UserName = "user",
                Password = "strongpassword"
            };
        }

        public async Task PublishOrderCreatedAsync(int orderId, string status)
        {
            try
            {
                await using var connection = await _factory.CreateConnectionAsync();

                await using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(
                    queue: "order_created",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                var message = $"OrderId:{orderId},Status:{status}";
                var body = Encoding.UTF8.GetBytes(message);

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "order_created",
                    body: body
                );


                Console.WriteLine($"[x] Sent: {message}");
            }
            catch (BrokerUnreachableException ex)
            {
                Console.WriteLine($"[!] RabbitMQ broker unreachable: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error publishing message: {ex.Message}");
                throw;
            }
        }
    }
}
