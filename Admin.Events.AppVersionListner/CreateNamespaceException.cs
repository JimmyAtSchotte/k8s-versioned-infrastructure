namespace Admin.Events.AppVersionListner;

public class CreateNamespaceException : Exception
{
    public CreateNamespaceException(Exception inner) : base("Failed to create namespace", inner) { }
}