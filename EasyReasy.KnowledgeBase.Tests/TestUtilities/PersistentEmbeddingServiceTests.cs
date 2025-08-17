using System.Text.Json;

namespace EasyReasy.KnowledgeBase.Tests.TestUtilities
{
    [TestClass]
    public class PersistentEmbeddingServiceTests
    {
        [TestMethod]
        public void Constructor_WithModelName_ShouldSetModelName()
        {
            // Arrange & Act
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model-v1", 2);

            // Assert
            Assert.AreEqual("test-model-v1", service.ModelName);
        }

        [TestMethod]
        public void Constructor_WithModelNameAndEmbeddings_ShouldSetModelNameAndEmbeddings()
        {
            // Arrange
            Dictionary<string, float[]> embeddings = new Dictionary<string, float[]>
            {
                { "test text", new float[] { 0.1f, 0.2f, 0.3f } },
                { "another text", new float[] { 0.4f, 0.5f, 0.6f } },
            };

            // Act
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model-v2", 3, embeddings);

            // Assert
            Assert.AreEqual("test-model-v2", service.ModelName);
            Assert.IsTrue(service.HasEmbedding("test text"));
            Assert.IsTrue(service.HasEmbedding("another text"));
        }

        [TestMethod]
        public void AddEmbedding_ShouldStoreEmbedding()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model", 3);
            string text = "test query";
            float[] embedding = new float[] { 0.1f, 0.2f, 0.3f };

            // Act
            service.AddEmbedding(text, embedding);

            // Assert
            Assert.IsTrue(service.HasEmbedding(text));
            float[] retrievedEmbedding = service.EmbedAsync(text).Result;
            CollectionAssert.AreEqual(embedding, retrievedEmbedding);
        }

        [TestMethod]
        public void EmbedAsync_WithExistingEmbedding_ShouldReturnEmbedding()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model", 3);
            string text = "test query";
            float[] embedding = new float[] { 0.1f, 0.2f, 0.3f };
            service.AddEmbedding(text, embedding);

            // Act
            float[] result = service.EmbedAsync(text).Result;

            // Assert
            CollectionAssert.AreEqual(embedding, result);
        }

        [TestMethod]
        public void EmbedAsync_WithNonExistentEmbedding_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model", 3);

            // Act & Assert
            KeyNotFoundException exception = Assert.ThrowsExceptionAsync<KeyNotFoundException>(
                () => service.EmbedAsync("non-existent text")).Result;
            Assert.IsTrue(exception.Message.Contains("non-existent text"));
        }

        [TestMethod]
        public void Serialize_WithEmbeddings_ShouldSerializeCorrectly()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model-v3", 2);
            service.AddEmbedding("text1", new float[] { 0.1f, 0.2f });
            service.AddEmbedding("text2", new float[] { 0.3f, 0.4f });

            // Act
            Stream serializedStream = service.Serialize();

            // Assert
            Assert.IsNotNull(serializedStream);
            Assert.IsTrue(serializedStream.Length > 0);
            Assert.AreEqual(0, serializedStream.Position); // Should be positioned at start
        }

        [TestMethod]
        public void Deserialize_WithValidStream_ShouldDeserializeCorrectly()
        {
            // Arrange
            PersistentEmbeddingService originalService = new PersistentEmbeddingService("test-model-v4", 2);
            originalService.AddEmbedding("text1", new float[] { 0.1f, 0.2f });
            originalService.AddEmbedding("text2", new float[] { 0.3f, 0.4f });
            Stream serializedStream = originalService.Serialize();

            // Act
            PersistentEmbeddingService deserializedService = PersistentEmbeddingService.Deserialize(serializedStream);

            // Assert
            Assert.AreEqual(originalService.ModelName, deserializedService.ModelName);
            Assert.IsTrue(deserializedService.HasEmbedding("text1"));
            Assert.IsTrue(deserializedService.HasEmbedding("text2"));

            float[] embedding1 = deserializedService.EmbedAsync("text1").Result;
            float[] embedding2 = deserializedService.EmbedAsync("text2").Result;

            CollectionAssert.AreEqual(new float[] { 0.1f, 0.2f }, embedding1);
            CollectionAssert.AreEqual(new float[] { 0.3f, 0.4f }, embedding2);
        }

        [TestMethod]
        public void SerializeDeserialize_WithEmptyService_ShouldPreserveModelName()
        {
            // Arrange
            PersistentEmbeddingService originalService = new PersistentEmbeddingService("empty-test-model", 3);

            // Act
            Stream serializedStream = originalService.Serialize();
            PersistentEmbeddingService deserializedService = PersistentEmbeddingService.Deserialize(serializedStream);

            // Assert
            Assert.AreEqual(originalService.ModelName, deserializedService.ModelName);
            Assert.AreEqual(0, deserializedService.GetAllEmbeddings().Count);
        }

        [TestMethod]
        public void SerializeDeserialize_WithComplexEmbeddings_ShouldPreserveAllData()
        {
            // Arrange
            PersistentEmbeddingService originalService = new PersistentEmbeddingService("complex-test-model", 3); // This test uses multiple different sizes, that should not be done for real
            originalService.AddEmbedding("short", new float[] { 0.1f });
            originalService.AddEmbedding("long", new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f });
            originalService.AddEmbedding("negative", new float[] { -0.1f, -0.2f, -0.3f });
            originalService.AddEmbedding("mixed", new float[] { 0.0f, -1.0f, 1.0f, 0.5f, -0.5f });

            // Act
            Stream serializedStream = originalService.Serialize();
            PersistentEmbeddingService deserializedService = PersistentEmbeddingService.Deserialize(serializedStream);

            // Assert
            Assert.AreEqual(originalService.ModelName, deserializedService.ModelName);

            Dictionary<string, float[]> originalEmbeddings = originalService.GetAllEmbeddings();
            Dictionary<string, float[]> deserializedEmbeddings = deserializedService.GetAllEmbeddings();

            Assert.AreEqual(originalEmbeddings.Count, deserializedEmbeddings.Count);

            foreach (KeyValuePair<string, float[]> kvp in originalEmbeddings)
            {
                Assert.IsTrue(deserializedEmbeddings.ContainsKey(kvp.Key));
                CollectionAssert.AreEqual(kvp.Value, deserializedEmbeddings[kvp.Key]);
            }
        }

        [TestMethod]
        public void Deserialize_WithInvalidStream_ShouldThrowJsonException()
        {
            // Arrange
            MemoryStream invalidStream = new MemoryStream();
            byte[] invalidData = System.Text.Encoding.UTF8.GetBytes("invalid json data");
            invalidStream.Write(invalidData, 0, invalidData.Length);
            invalidStream.Position = 0;

            // Act & Assert
            JsonException exception = Assert.ThrowsException<JsonException>(
                () => PersistentEmbeddingService.Deserialize(invalidStream));
        }

        [TestMethod]
        public void Deserialize_WithInvalidStructure_ShouldThrowInvalidOperationException()
        {
            // Arrange
            MemoryStream invalidStream = new MemoryStream();
            string invalidJson = "{\"SomeOtherProperty\": \"value\"}";
            byte[] invalidData = System.Text.Encoding.UTF8.GetBytes(invalidJson);
            invalidStream.Write(invalidData, 0, invalidData.Length);
            invalidStream.Position = 0;

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => PersistentEmbeddingService.Deserialize(invalidStream));
            Assert.IsTrue(exception.Message.Contains("Invalid serialized data format"));
        }

        [TestMethod]
        public void RemoveEmbedding_ShouldRemoveEmbedding()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model", 2);
            service.AddEmbedding("test text", new float[] { 0.1f, 0.2f });

            // Act
            bool removed = service.RemoveEmbedding("test text");

            // Assert
            Assert.IsTrue(removed);
            Assert.IsFalse(service.HasEmbedding("test text"));
        }

        [TestMethod]
        public void RemoveEmbedding_WithNonExistentText_ShouldReturnFalse()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model", 3);

            // Act
            bool removed = service.RemoveEmbedding("non-existent text");

            // Assert
            Assert.IsFalse(removed);
        }

        [TestMethod]
        public void Clear_ShouldRemoveAllEmbeddings()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model", 1);
            service.AddEmbedding("text1", new float[] { 0.1f });
            service.AddEmbedding("text2", new float[] { 0.2f });

            // Act
            service.Clear();

            // Assert
            Assert.AreEqual(0, service.GetAllEmbeddings().Count);
            Assert.IsFalse(service.HasEmbedding("text1"));
            Assert.IsFalse(service.HasEmbedding("text2"));
        }

        [TestMethod]
        public void GetAllEmbeddings_ShouldReturnCopyOfEmbeddings()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model", 1);
            service.AddEmbedding("text1", new float[] { 0.1f });

            // Act
            Dictionary<string, float[]> embeddings = service.GetAllEmbeddings();
            embeddings["text1"] = new float[] { 0.9f }; // Modify the copy

            // Assert
            float[] originalEmbedding = service.EmbedAsync("text1").Result;
            CollectionAssert.AreEqual(new float[] { 0.1f }, originalEmbedding); // Original should be unchanged
        }

        [TestMethod]
        public void Serialize_WithFilePath_ShouldCreateFile()
        {
            // Arrange
            string filePath = Path.GetTempFileName();
            PersistentEmbeddingService service = new PersistentEmbeddingService("file-test-model", 2);
            service.AddEmbedding("text1", new float[] { 0.1f, 0.2f });
            service.AddEmbedding("text2", new float[] { 0.3f, 0.4f });

            try
            {
                // Act
                service.Serialize(filePath);

                // Assert
                Assert.IsTrue(File.Exists(filePath));
                Assert.IsTrue(new FileInfo(filePath).Length > 0);
            }
            finally
            {
                // Cleanup
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [TestMethod]
        public void Deserialize_WithFilePath_ShouldLoadService()
        {
            // Arrange
            string filePath = Path.GetTempFileName();
            PersistentEmbeddingService originalService = new PersistentEmbeddingService("file-test-model-v2", 2);
            originalService.AddEmbedding("text1", new float[] { 0.1f, 0.2f });
            originalService.AddEmbedding("text2", new float[] { 0.3f, 0.4f });

            try
            {
                originalService.Serialize(filePath);

                // Act
                PersistentEmbeddingService deserializedService = PersistentEmbeddingService.Deserialize(filePath);

                // Assert
                Assert.AreEqual(originalService.ModelName, deserializedService.ModelName);
                Assert.IsTrue(deserializedService.HasEmbedding("text1"));
                Assert.IsTrue(deserializedService.HasEmbedding("text2"));

                float[] embedding1 = deserializedService.EmbedAsync("text1").Result;
                float[] embedding2 = deserializedService.EmbedAsync("text2").Result;

                CollectionAssert.AreEqual(new float[] { 0.1f, 0.2f }, embedding1);
                CollectionAssert.AreEqual(new float[] { 0.3f, 0.4f }, embedding2);
            }
            finally
            {
                // Cleanup
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [TestMethod]
        public void SerializeDeserialize_WithFilePath_ShouldPreserveAllData()
        {
            // Arrange
            string filePath = Path.GetTempFileName();
            PersistentEmbeddingService originalService = new PersistentEmbeddingService("file-complex-test-model", 3); // This test uses multiple different sizes, that should not be done for real
            originalService.AddEmbedding("short", new float[] { 0.1f });
            originalService.AddEmbedding("long", new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f });
            originalService.AddEmbedding("negative", new float[] { -0.1f, -0.2f, -0.3f });
            originalService.AddEmbedding("mixed", new float[] { 0.0f, -1.0f, 1.0f, 0.5f, -0.5f });

            try
            {
                // Act
                originalService.Serialize(filePath);
                PersistentEmbeddingService deserializedService = PersistentEmbeddingService.Deserialize(filePath);

                // Assert
                Assert.AreEqual(originalService.ModelName, deserializedService.ModelName);

                Dictionary<string, float[]> originalEmbeddings = originalService.GetAllEmbeddings();
                Dictionary<string, float[]> deserializedEmbeddings = deserializedService.GetAllEmbeddings();

                Assert.AreEqual(originalEmbeddings.Count, deserializedEmbeddings.Count);

                foreach (KeyValuePair<string, float[]> kvp in originalEmbeddings)
                {
                    Assert.IsTrue(deserializedEmbeddings.ContainsKey(kvp.Key));
                    CollectionAssert.AreEqual(kvp.Value, deserializedEmbeddings[kvp.Key]);
                }
            }
            finally
            {
                // Cleanup
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [TestMethod]
        public void Deserialize_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), "non-existent-file.json");

            // Act & Assert
            FileNotFoundException exception = Assert.ThrowsException<FileNotFoundException>(
                () => PersistentEmbeddingService.Deserialize(nonExistentPath));
        }

        [TestMethod]
        public void Serialize_WithInvalidPath_ShouldThrowException()
        {
            // Arrange
            PersistentEmbeddingService service = new PersistentEmbeddingService("test-model", 3);
            string invalidPath = Path.Combine("non-existent-directory", "test.json");

            // Act & Assert
            Assert.ThrowsException<DirectoryNotFoundException>(
                () => service.Serialize(invalidPath));
        }

        [TestMethod]
        public void SerializeDeserializeToFile_WithEmptyService_ShouldPreserveModelName()
        {
            // Arrange
            string filePath = Path.GetTempFileName();
            PersistentEmbeddingService originalService = new PersistentEmbeddingService("file-empty-test-model", 3);

            try
            {
                // Act
                originalService.Serialize(filePath);
                PersistentEmbeddingService deserializedService = PersistentEmbeddingService.Deserialize(filePath);

                // Assert
                Assert.AreEqual(originalService.ModelName, deserializedService.ModelName);
                Assert.AreEqual(0, deserializedService.GetAllEmbeddings().Count);
            }
            finally
            {
                // Cleanup
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
}
