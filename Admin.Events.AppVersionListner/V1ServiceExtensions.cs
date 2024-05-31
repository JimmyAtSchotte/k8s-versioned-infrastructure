using k8s.Models;

namespace Admin.Events.AppVersionListner;

public static class V1ServiceExtensions
{
    public static bool BelongsToApp(this V1Service service, ApplicationData app)
        => service.Metadata.Name == app.GetDeploymentName();
    
    
    public static V1Service ApplyApp(this V1Service service, ApplicationData app)
    {
        service.Metadata = new V1ObjectMeta()
        {
            Name = app.GetServiceName(),
            NamespaceProperty = app.GetNamespace()
        };
        service.Spec = new V1ServiceSpec()
        {
            Selector = new Dictionary<string, string>()
            {
                ["app"] = app.GetLabel()
            },
            Ports = new List<V1ServicePort>()
            {
                new V1ServicePort()
                {
                    Port = 80,
                    TargetPort = 8080,
                    Protocol = "TCP"
                }
            }
        };

        return service;
    }
}