// See https://aka.ms/new-console-template for more information
using Confluent.Kafka;
using System.Net;
using System.Text.Json.Nodes;
using Contracts;
using Newtonsoft.Json;


var config = new ProducerConfig
{
    BootstrapServers = "host1:9092",
};

using (var producer = new ProducerBuilder<Null, string>(config).Build())
{
    var message = new Message<string, string> { Value=JsonSerializer. new Pong() };
    var result = await producer.ProduceAsync("demo-topic", message);
}
