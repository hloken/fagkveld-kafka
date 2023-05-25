using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Contracts;


var bootstrapServers = "localhost:9092";
const string topicName = "demo-topic";
var schemaRegistryUrl = "http://localhost:8085";
var consumerGroupId = "pinger-group";

var producerConfig = new ProducerConfig
{
    BootstrapServers = bootstrapServers,
};

var schemaRegistryConfig = new SchemaRegistryConfig
{
    // Note: you can specify more than one schema registry url using the
    // schema.registry.url property for redundancy (comma separated list). 
    // The property name is not plural to follow the convention set by
    // the Java implementation.
    Url = schemaRegistryUrl
};

var consumerConfig = new ConsumerConfig
{
    BootstrapServers = bootstrapServers,
    GroupId = consumerGroupId
};

// Note: Specifying json serializer configuration is optional.
var jsonSerializerConfig = new JsonSerializerConfig
{
    BufferBytes = 100
};

var cts = new CancellationTokenSource();
var consumeTask = Task.Run(() =>
{
    using var consumer = new ConsumerBuilder<string, Pong>(consumerConfig)
        .SetKeyDeserializer(Deserializers.Utf8)
        .SetValueDeserializer(new JsonDeserializer<Pong>().AsSyncOverAsync())
        .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
        .Build();
    
    consumer.Subscribe(topicName);

    try
    {
        while (true)
        {
            try
            {
                var cr = consumer.Consume(cts.Token);
                var ping = cr.Message.Value;
                Console.WriteLine($"Pong text: {ping.Text} SequenceNumber: {ping.SequenceNumber}");
            }
            catch (ConsumeException e)
            {
                Console.WriteLine($"Consume error: {e.Error.Reason}");
            }
        }
    }
    catch (OperationCanceledException)
    {
        consumer.Close();
    }
});

using (var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig))
using (var producer =
    new ProducerBuilder<string, Ping>(producerConfig)
        .SetValueSerializer(new JsonSerializer<Ping>(schemaRegistry, jsonSerializerConfig))
        .Build())
{
    Console.WriteLine($"{producer.Name} producing on {topicName}. Press a key so send a Ping event, q to quit");

    long i = 1;
    while (Console.ReadKey().Key != ConsoleKey.Q)
    {
        var @event = new Ping ( Text: "Ping!", SequenceNumber: i++);
        try 
        {
            await producer.ProduceAsync(topicName, new Message<string, Ping> { Value = @event });
        }
        catch (Exception e) 
        {
            Console.WriteLine($"error producing message: {e.Message}");
        }
    }
}

cts.Cancel();

using (var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig))
{
    // Note: a subject name strategy was not configured, so the default "Topic" was used.
    var schema = await schemaRegistry.GetLatestSchemaAsync(SubjectNameStrategy.Topic.ConstructValueSubjectName(topicName));
    Console.WriteLine("\nThe JSON schema corresponding to the written data:");
    Console.WriteLine(schema.SchemaString);
}
