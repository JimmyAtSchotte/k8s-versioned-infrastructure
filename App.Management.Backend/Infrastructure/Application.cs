using MongoDB.Bson;

namespace Admin.Api.Infrastructure;

public class Application
{
    public ObjectId _id { get; set; }
    public string Name { get; set; }
    public string? Image { get; set; }
}