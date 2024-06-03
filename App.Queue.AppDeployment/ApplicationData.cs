namespace Admin.Events.AppVersionListner;

public class ApplicationData
{
    public string Name { get; set; }
    public string Image { get; set; }

    public string GetNamespace() => $"ns-{Name}";
    public string GetLabel() => $"app-{Name}";
    public string GetServiceName() => $"svc-{Name}";
    public string GetDeploymentName() => $"app-{Name}";
    public string GetIngressName() => $"ingr-{Name}";
    public string GetPath() => $"/{Name}";
}