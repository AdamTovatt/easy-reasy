namespace EasyReasy.Auth
{
    /// <summary>
    /// A service a provider package's sign-in endpoints need, for
    /// <see cref="ExternalSignInEndpoint.ThrowIfServicesMissing"/> to check at startup and, when it is missing, to say how
    /// to register it.
    /// </summary>
    public sealed class ExternalSignInDependency
    {
        private ExternalSignInDependency(Type serviceType, Func<string, string> howToRegister)
        {
            ServiceType = serviceType;
            HowToRegister = howToRegister;
        }

        /// <summary>
        /// The service that must be registered.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// The advice for when it is missing, given the endpoint-mapping method's name.
        /// </summary>
        internal Func<string, string> HowToRegister { get; }

        /// <summary>
        /// A service the package's own registration method adds.
        /// </summary>
        /// <param name="serviceType">The service.</param>
        /// <param name="registrationMethodName">The method that registers it, such as <c>AddEasyReasySamlAuth</c>.</param>
        /// <returns>The dependency.</returns>
        public static ExternalSignInDependency RegisteredBy(Type serviceType, string registrationMethodName)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            ArgumentException.ThrowIfNullOrWhiteSpace(registrationMethodName);

            return new ExternalSignInDependency(
                serviceType,
                mapMethodName => $"Call builder.Services.{registrationMethodName}(...) before {mapMethodName}.");
        }

        /// <summary>
        /// A service the application implements and registers itself.
        /// </summary>
        /// <param name="serviceType">The service, an interface.</param>
        /// <param name="role">What the application's implementation is, as the advice names it, such as
        /// <c>handler</c>.</param>
        /// <param name="exampleImplementationName">The implementation the advice's example registers, such as
        /// <c>MySamlAuthHandler</c>.</param>
        /// <returns>The dependency.</returns>
        public static ExternalSignInDependency ImplementedByApplication(Type serviceType, string role, string exampleImplementationName)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            ArgumentException.ThrowIfNullOrWhiteSpace(role);
            ArgumentException.ThrowIfNullOrWhiteSpace(exampleImplementationName);

            return new ExternalSignInDependency(
                serviceType,
                mapMethodName => $"Register your {role} before {mapMethodName}, e.g.: " +
                    $"builder.Services.AddScoped<{serviceType.Name}, {exampleImplementationName}>();");
        }
    }
}
