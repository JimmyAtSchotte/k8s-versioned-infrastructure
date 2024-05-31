using k8s.Models;

namespace Admin.Events.AppVersionListner;

public static class V1DeploymentExtensions
{
    public static bool BelongsToApp(this V1Deployment deployment, ApplicationData app)
        => deployment.Metadata.Name == app.GetDeploymentName();
    
    
    public static V1Deployment ApplyApp(this V1Deployment deployment, ApplicationData app)
    {
        deployment.Metadata = new V1ObjectMeta
        {
            Name = app.GetDeploymentName(),
            NamespaceProperty = app.GetNamespace()
        };

        deployment.Spec = new V1DeploymentSpec
        {
            Selector = new V1LabelSelector
            {
                MatchLabels = new System.Collections.Generic.Dictionary<string, string> { { "app", app.GetLabel() } }
            },
            Replicas = 1,
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new System.Collections.Generic.Dictionary<string, string> { { "app", app.GetLabel() } }
                },
                Spec = new V1PodSpec
                {
                    Containers = new[]
                    {
                        new V1Container
                        {
                            Name = app.GetLabel(),
                            Image = $"localhost:5000/webapplication:{app.Image}"
                        }
                    }
                }
            }
        };

        return deployment;
    }
}