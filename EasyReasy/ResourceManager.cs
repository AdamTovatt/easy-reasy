using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace EasyReasy
{
    public class ResourceManager
    {
        private readonly Dictionary<Type, IResourceProvider> providers = new Dictionary<Type, IResourceProvider>();
        private readonly Dictionary<Type, List<Resource>> resourceCollections = new Dictionary<Type, List<Resource>>();
        private readonly Assembly assembly;

        /// <summary>
        /// Creates a new instance of <see cref="ResourceManager"/> for the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to discover resource collections from. If null, uses the executing assembly.</param>
        /// <param name="predefinedProviders">The predefined resource providers to register.</param>
        /// <returns>A new <see cref="ResourceManager"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when resource mapping integrity check fails during initialization, including when providers are missing for collections or when resources cannot be found by their providers.</exception>
        public static async Task<ResourceManager> CreateInstanceAsync(
            Assembly assembly,
            params PredefinedResourceProvider[] predefinedProviders
        )
        {
            ArgumentNullException.ThrowIfNull(assembly, nameof(assembly));

            ResourceManager instance = new ResourceManager(assembly);

            // Register predefined providers before discovery
            if (predefinedProviders != null)
            {
                foreach (PredefinedResourceProvider predefined in predefinedProviders)
                {
                    instance.RegisterProvider(predefined.ResourceCollectionType, predefined.Provider);
                }
            }

            instance.DiscoverResourceCollections();
            await instance.VerifyResourceMappingsAsync().ConfigureAwait(false);
            return instance;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ResourceManager"/> for the assembly that is calling this method.
        /// </summary>
        /// <param name="predefinedProviders">The predefined resource providers to register.</param>
        /// <returns>A new <see cref="ResourceManager"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when resource mapping integrity check fails during initialization, including when providers are missing for collections or when resources cannot be found by their providers.</exception>
        public static async Task<ResourceManager> CreateInstanceAsync(
            params PredefinedResourceProvider[] predefinedProviders
        )
        {
            return await CreateInstanceAsync(GetCallingAssembly() ?? throw new InvalidOperationException($"Could not find calling assembly"), predefinedProviders).ConfigureAwait(false);
        }

        // This method is honestly a bit questionable as it is quite error-prone. Maybe it would just be better to not allow creating instances of ResourceManager without providing an assembly
        private static Assembly? GetCallingAssembly() // Will find out the real calling assembly even in an async method that is normally missing that information since it's a continuation task
        {
            StackTrace stackTrace = new StackTrace(skipFrames: 1, fNeedFileInfo: false);
            foreach (StackFrame frame in stackTrace.GetFrames()!)
            {
                MethodBase? method = frame.GetMethod();
                if (method == null)
                {
                    continue;
                }

                Type? declaringType = method.DeclaringType;
                if (declaringType == null)
                {
                    continue;
                }

                Assembly assembly = declaringType.Assembly;

                if (assembly != Assembly.GetExecutingAssembly() && // If the assembly we're considering isn't the one that's declaring this code (meaning the call came from outside this assembly)
                    !assembly.FullName!.StartsWith("System") && //    and the name doesn't start with System
                    !assembly.FullName.StartsWith("Microsoft") && //  and the name doesn't start with Microsoft
                    !assembly.IsDynamic) //                           and the calling assembly isn't run time generated
                {
                    return assembly; // Then it's probably the real assembly that we want
                }
            }

            return null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceManager"/> class.
        /// </summary>
        /// <param name="assembly">The assembly to discover resource collections from.</param>
        private ResourceManager(Assembly assembly)
        {
            this.assembly = assembly;
        }

        /// <summary>
        /// Registers a provider for a specific resource collection type.
        /// </summary>
        /// <typeparam name="T">The resource collection type.</typeparam>
        /// <param name="provider">The provider to register.</param>
        public void RegisterProvider<T>(IResourceProvider provider) where T : class
        {
            providers[typeof(T)] = provider;
        }

        /// <summary>
        /// Registers a provider for a specific resource collection type.
        /// </summary>
        /// <param name="collectionType">The resource collection type.</param>
        /// <param name="provider">The provider to register.</param>
        public void RegisterProvider(Type collectionType, IResourceProvider provider)
        {
            providers[collectionType] = provider;
        }

        /// <summary>
        /// Reads the specified resource as a string.
        /// </summary>
        /// <param name="resource">The resource to read.</param>
        /// <returns>The resource content as a string.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no provider is registered for the resource's collection or when the resource cannot be read.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the resource does not exist in the provider.</exception>
        public async Task<string> ReadAsStringAsync(Resource resource)
        {
            IResourceProvider provider = GetProviderForResource(resource);
            return await provider.ReadAsStringAsync(resource).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads the specified resource as a byte array.
        /// </summary>
        /// <param name="resource">The resource to read.</param>
        /// <returns>The resource content as a byte array.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no provider is registered for the resource's collection or when the resource cannot be read.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the resource does not exist in the provider.</exception>
        public async Task<byte[]> ReadAsBytesAsync(Resource resource)
        {
            IResourceProvider provider = GetProviderForResource(resource);
            return await provider.ReadAsBytesAsync(resource).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a stream for reading the specified resource.
        /// </summary>
        /// <param name="resource">The resource to read.</param>
        /// <returns>A stream for reading the resource.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no provider is registered for the resource's collection or when the resource cannot be read.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the resource does not exist in the provider.</exception>
        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            IResourceProvider provider = GetProviderForResource(resource);
            return await provider.GetResourceStreamAsync(resource).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the content type for the specified resource.
        /// </summary>
        /// <param name="resource">The resource to get the content type for.</param>
        /// <returns>The content type of the resource.</returns>
        public string GetContentType(Resource resource)
        {
            return resource.GetContentType();
        }

        /// <summary>
        /// Checks if the specified resource exists.
        /// </summary>
        /// <param name="resource">The resource to check.</param>
        /// <returns>True if the resource exists; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no provider is registered for the resource's collection or when the resource was created manually instead of being defined in a resource collection.</exception>
        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            try
            {
                IResourceProvider provider = GetProviderForResource(resource);

                return await provider.ResourceExistsAsync(resource).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"Tried to check if the resource with path \"{resource.Path}\" exists " +
                    "but no provider for that resource could be found. This probably means you created an instance of the " +
                    "Resource type with the constructor instead of defining the Resource you wanted in a resource collection. " +
                    "The following exception message was from the invalid attempt to find a provider: {ex.Message}", innerException: ex);
            }
        }

        /// <summary>
        /// Gets all defined resources for the specified collection type.
        /// </summary>
        /// <param name="collectionType">The resource collection type to get resources for.</param>
        /// <returns>A list of all defined resources in the specified collection type.</returns>
        /// <exception cref="ArgumentException">Thrown when the specified type is not a resource collection type.</exception>
        public List<Resource> GetResourcesForCollection(Type collectionType)
        {
            if (resourceCollections.TryGetValue(collectionType, out List<Resource>? resources))
            {
                return new List<Resource>(resources);
            }

            throw new ArgumentException($"Type '{collectionType.Name}' is not a resource collection type or has not been discovered by this ResourceManager instance.");
        }

        /// <summary>
        /// Verifies that all mapped resources exist and are accessible.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when resource mapping integrity check fails, including when providers are missing for collections or when resources cannot be found by their providers.</exception>
        public async Task VerifyResourceMappingsAsync()
        {
            StringBuilder errors = new StringBuilder();

            foreach (KeyValuePair<Type, List<Resource>> collection in resourceCollections)
            {
                Type collectionType = collection.Key;
                List<Resource> resources = collection.Value;

                if (!providers.ContainsKey(collectionType))
                {
                    // Check if this collection has a ResourceCollectionAttribute to determine the provider type
                    ResourceCollectionAttribute? attribute = collectionType.GetCustomAttribute<ResourceCollectionAttribute>();
                    if (attribute != null)
                    {
                        // Find a public constructor where all parameters are either optional or injectable types
                        ConstructorInfo? suitableConstructor = attribute.ProviderType.GetConstructors()
                            .FirstOrDefault(ctor => ctor.GetParameters().All(p => p.IsOptional || IsInjectableType(p.ParameterType)));

                        if (suitableConstructor == null)
                        {
                            errors.AppendLine($"❌ No provider registered for resource collection: {collectionType.Name}");
                            errors.AppendLine($"   Provider type '{attribute.ProviderType.Name}' requires constructor parameters and must be registered manually.");
                            errors.AppendLine($"   Use CreateInstance with PredefinedResourceProvider for this collection.");
                        }
                        else
                        {
                            errors.AppendLine($"❌ No provider registered for resource collection: {collectionType.Name}");
                        }
                    }
                    else
                    {
                        errors.AppendLine($"❌ No provider registered for resource collection: {collectionType.Name}");
                    }
                    continue;
                }

                IResourceProvider provider = providers[collectionType];

                foreach (Resource resource in resources)
                {
                    try
                    {
                        bool exists = await provider.ResourceExistsAsync(resource).ConfigureAwait(false);
                        if (!exists)
                        {
                            errors.AppendLine($"❌ Resource not found: {resource.Path} (Collection: {collectionType.Name})");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.AppendLine($"❌ Error checking resource {resource.Path}: {ex.Message}");
                    }
                }
            }

            if (errors.Length > 0)
            {
                throw new InvalidOperationException($"Resource mapping integrity check failed:\n{errors}");
            }
        }

        private void DiscoverResourceCollections()
        {
            Dictionary<string, List<Type>> resourcePathToCollections = new Dictionary<string, List<Type>>();

            foreach (Type type in assembly.GetTypes())
            {
                ResourceCollectionAttribute? attribute = type.GetCustomAttribute<ResourceCollectionAttribute>();
                if (attribute != null)
                {
                    // Find all static Resource fields in this type
                    List<Resource> resources = new List<Resource>();

                    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (field.FieldType == typeof(Resource))
                        {
                            object? value = field.GetValue(null);
                            if (value is Resource resource)
                            {
                                resources.Add(resource);

                                // Track which collections contain each resource path
                                if (!resourcePathToCollections.ContainsKey(resource.Path))
                                {
                                    resourcePathToCollections[resource.Path] = new List<Type>();
                                }
                                resourcePathToCollections[resource.Path].Add(type);
                            }
                        }
                    }

                    resourceCollections[type] = resources;

                    // Auto-register the provider if it's a concrete type with a suitable constructor
                    if (attribute.ProviderType.IsClass && !attribute.ProviderType.IsAbstract)
                    {
                        // Find a public constructor where all parameters are either optional or injectable types
                        ConstructorInfo? suitableConstructor = attribute.ProviderType.GetConstructors()
                            .FirstOrDefault(ctor => ctor.GetParameters().All(p => p.IsOptional || IsInjectableType(p.ParameterType)));
                        if (suitableConstructor != null)
                        {
                            // Create parameter values array, injecting values for injectable types
                            ParameterInfo[] parameters = suitableConstructor.GetParameters();
                            object?[] parameterValues = new object?[parameters.Length];

                            for (int i = 0; i < parameters.Length; i++)
                            {
                                ParameterInfo parameter = parameters[i];
                                if (IsInjectableType(parameter.ParameterType))
                                {
                                    parameterValues[i] = GetInjectableValue(parameter.ParameterType);
                                }
                                else
                                {
                                    parameterValues[i] = parameter.DefaultValue;
                                }
                            }

                            IResourceProvider provider = (IResourceProvider)suitableConstructor.Invoke(parameterValues);
                            providers[type] = provider;
                        }
                        else
                        {
                            // Provider requires non-optional constructor parameters, so it needs to be registered as a predefined provider
                            // We'll let VerifyResourceMappings handle the error reporting with a specific message
                        }
                    }
                }
            }

            // Validate that no resource paths are duplicated across collections
            ValidateResourcePathUniqueness(resourcePathToCollections);
        }

        /// <summary>
        /// Validates that resource paths are unique across all resource collections.
        /// </summary>
        /// <param name="resourcePathToCollections">Dictionary mapping resource paths to the collections that contain them.</param>
        /// <exception cref="InvalidOperationException">Thrown when duplicate resource paths are found across different collections.</exception>
        private static void ValidateResourcePathUniqueness(Dictionary<string, List<Type>> resourcePathToCollections)
        {
            StringBuilder errors = new StringBuilder();
            bool hasErrors = false;

            foreach (KeyValuePair<string, List<Type>> entry in resourcePathToCollections)
            {
                string resourcePath = entry.Key;
                List<Type> collections = entry.Value;

                if (collections.Count > 1)
                {
                    hasErrors = true;
                    errors.AppendLine($"❌ Duplicate resource path '{resourcePath}' found in multiple collections:");
                    foreach (Type collection in collections)
                    {
                        errors.AppendLine($"   - {collection.Name}");
                    }
                    errors.AppendLine();
                }
            }

            if (hasErrors)
            {
                throw new InvalidOperationException($"Resource path conflicts detected:\n{errors}");
            }
        }

        /// <summary>
        /// Determines if a parameter type is injectable by the ResourceManager.
        /// </summary>
        /// <param name="parameterType">The parameter type to check.</param>
        /// <returns>True if the parameter type is injectable; otherwise, false.</returns>
        private static bool IsInjectableType(Type parameterType)
        {
            return parameterType == typeof(Assembly);
        }

        /// <summary>
        /// Gets the injectable value for the specified parameter type.
        /// </summary>
        /// <param name="parameterType">The parameter type to get the value for.</param>
        /// <returns>The injectable value for the parameter type.</returns>
        /// <exception cref="NotSupportedException">Thrown when the parameter type is not supported for injection.</exception>
        private object GetInjectableValue(Type parameterType)
        {
            if (parameterType == typeof(Assembly))
            {
                return assembly;
            }

            throw new NotSupportedException($"Parameter type '{parameterType.Name}' is not supported for automatic injection.");
        }

        /// <summary>
        /// Gets the provider for the specified resource.
        /// </summary>
        /// <param name="resource">The resource to get the provider for.</param>
        /// <returns>The provider that handles the specified resource.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no provider is registered for the resource's collection or when the resource was created manually instead of being defined in a resource collection.</exception>
        public IResourceProvider GetProviderForResource(Resource resource)
        {
            // Find which collection this resource belongs to
            foreach (KeyValuePair<Type, List<Resource>> collection in resourceCollections)
            {
                if (collection.Value.Contains(resource))
                {
                    if (providers.TryGetValue(collection.Key, out IResourceProvider? provider))
                    {
                        return provider;
                    }

                    throw new InvalidOperationException($"No provider registered for resource collection: {collection.Key.Name}");
                }
            }

            throw new InvalidOperationException(
                $"Resource not found in any collection: \"{resource.Path}\"\n" +
                "This could mean you've created a Resource object manually using the constructor instead of using a resource defined in a resource collection. " +
                "Resources must be defined as static fields in classes marked with [ResourceCollection] attribute." +
                $"{(resourceCollections.Count == 0 ? " It also seems like no resource collections was discovered, are you sure you've used CreateInstance with the right Assembly? Maybe try passing Assembly.GetExecutingAssembly() to it as a first parameter." : "")}");
        }
    }
}