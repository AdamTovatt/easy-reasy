using System.Reflection;

namespace EasyReasy.Tests
{
    [TestClass]
    public class EmbeddedResourceProviderTests
    {
        [TestMethod]
        public void Constructor_WithAssembly_CreatesInstance()
        {
            // Arrange & Act
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());

            // Assert
            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public async Task ResourceExistsAsync_WithExistingResource_ReturnsTrue()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("TestResources/test.txt");

            // Act
            bool result = await provider.ResourceExistsAsync(testResource);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ResourceExistsAsync_WithNonExistingResource_ReturnsFalse()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("nonexistent.txt");

            // Act
            bool result = await provider.ResourceExistsAsync(testResource);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetResourceStreamAsync_WithExistingResource_ReturnsStream()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("TestResources/test.txt");

            // Act
            Stream result = await provider.GetResourceStreamAsync(testResource);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.CanRead);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public async Task GetResourceStreamAsync_WithNonExistingResource_ThrowsException()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("nonexistent.txt");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() =>
                provider.GetResourceStreamAsync(testResource));
        }

        [TestMethod]
        public async Task ReadAsBytesAsync_WithExistingResource_ReturnsBytes()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("TestResources/test.txt");

            // Act
            byte[] result = await ((IResourceProvider)provider).ReadAsBytesAsync(testResource);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public async Task ReadAsStringAsync_WithExistingResource_ReturnsString()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("TestResources/test.txt");

            // Act
            string result = await ((IResourceProvider)provider).ReadAsStringAsync(testResource);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
            Assert.IsTrue(result.Contains("test text file"));
        }

        [TestMethod]
        public async Task ReadAsStringAsync_WithNonExistingResource_ThrowsException()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("nonexistent.txt");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() =>
                ((IResourceProvider)provider).ReadAsStringAsync(testResource));
        }

        [TestMethod]
        public async Task ReadAsBytesAsync_WithNonExistingResource_ThrowsException()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("nonexistent.txt");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() =>
                ((IResourceProvider)provider).ReadAsBytesAsync(testResource));
        }

        [TestMethod]
        public async Task GetResourceStreamAsync_WithResourceInSubdirectory_ReturnsStream()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("TestResources/test.json");

            // Act
            Stream result = await provider.GetResourceStreamAsync(testResource);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.CanRead);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public async Task ResourceExistsAsync_WithResourceInSubdirectory_ReturnsTrue()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("TestResources/test.json");

            // Act
            bool result = await provider.ResourceExistsAsync(testResource);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ReadAsStringAsync_WithJsonResource_ReturnsCorrectContent()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("TestResources/test.json");

            // Act
            string result = await ((IResourceProvider)provider).ReadAsStringAsync(testResource);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("test"));
            Assert.IsTrue(result.Contains("json"));
            Assert.IsTrue(result.Contains("embedded resource"));
        }

        [TestMethod]
        public async Task ReadAsBytesAsync_WithBinaryResource_ReturnsCorrectBytes()
        {
            // Arrange
            EmbeddedResourceProvider provider = new EmbeddedResourceProvider(Assembly.GetExecutingAssembly());
            Resource testResource = new Resource("TestResources/test.bin");

            // Act
            byte[] result = await ((IResourceProvider)provider).ReadAsBytesAsync(testResource);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
            string content = System.Text.Encoding.UTF8.GetString(result);
            Assert.IsTrue(content.Contains("test binary file"));
        }
    }
}