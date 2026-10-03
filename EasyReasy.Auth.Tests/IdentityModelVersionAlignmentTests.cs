using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Guards the IdentityModel package family against resolving at mixed versions. JwtBearer brings some of the
    /// family transitively while EasyReasy.Auth references others directly, and a mixed set rejects every token the
    /// library issues (IDX14102). The check reads the running test assembly's own <c>.deps.json</c>, which is the set
    /// a consumer of the packages under test resolves, so this file is also linked into
    /// <c>EasyReasy.Auth.Google.Tests</c>.
    /// </summary>
    [TestClass]
    public class IdentityModelVersionAlignmentTests
    {
        [TestMethod]
        public void ResolvedDependencies_IdentityModelPackages_ShareOneVersion()
        {
            Assembly testAssembly = typeof(IdentityModelVersionAlignmentTests).Assembly;
            string depsPath = Path.Combine(AppContext.BaseDirectory, $"{testAssembly.GetName().Name}.deps.json");

            using JsonDocument deps = JsonDocument.Parse(File.ReadAllText(depsPath));
            List<string> identityModelPackages = deps.RootElement.GetProperty("libraries")
                .EnumerateObject()
                .Where(library => library.Value.GetProperty("type").GetString() == "package")
                .Select(library => library.Name)
                .Where(nameAndVersion =>
                    nameAndVersion.StartsWith("Microsoft.IdentityModel.", StringComparison.Ordinal) ||
                    nameAndVersion.StartsWith("System.IdentityModel.", StringComparison.Ordinal))
                .ToList();

            // JwtBearer alone brings several of the family, so finding fewer means the check has stopped looking.
            Assert.IsTrue(identityModelPackages.Count >= 4, $"Found only: {string.Join(", ", identityModelPackages)}");

            List<string> versions = identityModelPackages
                .Select(nameAndVersion => nameAndVersion.Split('/')[1])
                .Distinct()
                .ToList();

            Assert.AreEqual(1, versions.Count, $"IdentityModel packages resolve at mixed versions: {string.Join(", ", identityModelPackages)}");
        }
    }
}
