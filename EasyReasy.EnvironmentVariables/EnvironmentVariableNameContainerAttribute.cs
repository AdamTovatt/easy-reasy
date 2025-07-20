namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Attribute to mark classes that contain environment variable name definitions.
    /// Classes marked with this attribute will be scanned during environment variable validation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EnvironmentVariableNameContainerAttribute : Attribute
    {
    }
}
