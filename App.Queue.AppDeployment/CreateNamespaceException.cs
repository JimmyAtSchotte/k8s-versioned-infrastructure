namespace App.Queue.AppDeployment;

public class CreateNamespaceException : Exception
{
    public CreateNamespaceException(Exception inner) : base("Failed to create namespace", inner) { }
}