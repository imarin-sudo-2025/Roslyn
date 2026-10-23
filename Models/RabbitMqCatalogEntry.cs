namespace CatalogApp.Models;

public class RabbitMqCatalogEntry
{
    public string QueueName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
}
