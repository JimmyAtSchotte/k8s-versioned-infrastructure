namespace Admin.Events.AppVersionListner;

public class QueueMessage<T> 
{
    public string Action { get; set; }
    public string Version { get; set; }
    public T Data { get; set; }
}