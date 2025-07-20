namespace EasyReasy.Tests
{
    [TestClass]
    public class ResourceTests
    {
        [TestMethod]
        public void Constructor_WithValidPath_CreatesResource()
        {
            // Arrange & Act
            Resource resource = new Resource("test.txt");

            // Assert
            Assert.IsNotNull(resource);
            Assert.AreEqual("test.txt", resource.Path);
        }

        [TestMethod]
        public void Constructor_WithNullPath_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new Resource(null!));
        }

        [TestMethod]
        public void Constructor_WithEmptyPath_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => new Resource(""));
        }

        [TestMethod]
        public void Constructor_WithWhitespacePath_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => new Resource("   "));
        }

        [TestMethod]
        public void Path_ReturnsCorrectPath()
        {
            // Arrange
            string expectedPath = "test.txt";
            Resource resource = new Resource(expectedPath);

            // Act
            string actualPath = resource.Path;

            // Assert
            Assert.AreEqual(expectedPath, actualPath);
        }

        [DataTestMethod]
        [DataRow("test.txt", "text/plain")]
        [DataRow("test.html", "text/html")]
        [DataRow("test.css", "text/css")]
        [DataRow("test.js", "application/javascript")]
        [DataRow("test.json", "application/json")]
        [DataRow("test.xml", "application/xml")]
        [DataRow("test.pdf", "application/pdf")]
        [DataRow("test.zip", "application/zip")]
        [DataRow("test.jpg", "image/jpeg")]
        [DataRow("test.jpeg", "image/jpeg")]
        [DataRow("test.png", "image/png")]
        [DataRow("test.gif", "image/gif")]
        [DataRow("test.bmp", "image/bmp")]
        [DataRow("test.svg", "image/svg+xml")]
        [DataRow("test.mp4", "video/mp4")]
        [DataRow("test.mp3", "audio/mpeg")]
        [DataRow("test.wav", "audio/wav")]
        [DataRow("test.ogg", "audio/ogg")]
        [DataRow("test.webm", "video/webm")]
        [DataRow("test.webp", "image/webp")]
        [DataRow("test.ico", "image/x-icon")]
        [DataRow("test.ttf", "font/ttf")]
        [DataRow("test.woff", "font/woff")]
        [DataRow("test.woff2", "font/woff2")]
        [DataRow("test.eot", "application/vnd.ms-fontobject")]
        [DataRow("test.otf", "font/otf")]
        [DataRow("test.unknown", "application/octet-stream")]
        [DataRow("test", "application/octet-stream")]
        [DataRow("test.TXT", "text/plain")]
        [DataRow("test.JpG", "image/jpeg")]
        [DataRow("test.backup.txt", "text/plain")]
        [DataRow("folder/subfolder/test.txt", "text/plain")]
        [DataRow("folder\\subfolder\\test.txt", "text/plain")]
        [DataRow("test.txt?version=1.0", "text/plain")]
        [DataRow("test.txt#section", "text/plain")]
        [DataRow("folder/subfolder/test.backup.txt?version=1.0#section", "text/plain")]
        public void GetContentType_WithVariousFileTypes_ReturnsCorrectContentType(string filePath, string expectedContentType)
        {
            // Arrange
            Resource resource = new Resource(filePath);

            // Act
            string actualContentType = resource.GetContentType();

            // Assert
            Assert.AreEqual(expectedContentType, actualContentType);
        }
    }
}