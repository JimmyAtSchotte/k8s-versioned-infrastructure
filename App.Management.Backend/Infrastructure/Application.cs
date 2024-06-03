using MongoDB.Bson;

namespace App.Management.Backend.Infrastructure;

public class Application
{
    public ObjectId _id { get; set; }
    public string Name { get; set; }
    public string? Image { get; set; }
}