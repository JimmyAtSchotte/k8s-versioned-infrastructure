using k8s.Models;

namespace App.Queue.AppDeployment;

public static class VV1IngressExtensions
{
    public static bool BelongsToApp(this V1Ingress ingress, ApplicationData app)
        => ingress.Metadata.Name == app.GetIngressName();
    
    public static V1Ingress ApplyApp(this V1Ingress ingress, ApplicationData app)
    {
        ingress.Metadata = new V1ObjectMeta()
        {
            Name = app.GetIngressName(),
            Annotations = new Dictionary<string, string>()
            {
                { "nginx.ingress.kubernetes.io/rewrite-target", "/" }
            }
        };
        ingress.Spec = new V1IngressSpec()
        {
            IngressClassName = "nginx",
            Rules = new List<V1IngressRule>()
            {
                new V1IngressRule()
                {
                    Host = "k8s-app.local",
                    Http = new V1HTTPIngressRuleValue()
                    {
                        Paths = new List<V1HTTPIngressPath>()
                        {
                            new V1HTTPIngressPath()
                            {
                                PathType = "Prefix",
                                Path = app.GetPath(),
                                Backend = new V1IngressBackend()
                                {
                                    Service = new V1IngressServiceBackend()
                                    {
                                        Name = app.GetServiceName(),
                                        Port = new V1ServiceBackendPort()
                                        {
                                            Number = 80
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return ingress;
    }
}