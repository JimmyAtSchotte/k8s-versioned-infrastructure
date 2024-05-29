using MongoDB.Bson;

namespace Admin.DbContext;

public class Application
{
    public ObjectId _id { get; set; }
    public string Name { get; set; }
    public string? Image { get; set; }
}