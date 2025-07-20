namespace EasyReasy
{
    /// <summary>
    /// Represents a resource with a file path and provides utility methods for working with resources.
    /// </summary>
    public readonly struct Resource
    {
        /// <summary>
        /// Gets the file path of the resource.
        /// </summary>
        public string Path { get; }

        public Resource(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            Path = path;
        }

        /// <summary>
        /// Warning, this is dangerous and should not be used unless the string it's used with is from a serialized resource instance so that it's certain it exists!
        /// Should not be used unless it can be certain that the resource path is valid and exists since this can't be checked before hand!
        /// </summary>
        /// <param name="resourcePath">The verifyed resource path that is known to be of an existing resource that has then been serialized.</param>
        /// <returns>A resource instance.</returns>
        public static Resource Create(string resourcePath)
        {
            return new Resource(resourcePath);
        }

        /// <summary>
        /// Gets the file name (without path) of the resource.
        /// </summary>
        /// <returns>The file name of the resource.</returns>
        public string GetFileName()
        {
            return System.IO.Path.GetFileName(Path);
        }

        /// <summary>
        /// Gets the MIME content type for this resource based on its file extension.
        /// </summary>
        /// <returns>The content type string.</returns>
        public string GetContentType()
        {
            // Handle URLs with query parameters and fragments by extracting just the path part
            // File names are not allowed to contain "?" on windows or linux so this should be okay
            string pathWithoutQuery = Path.Split('?')[0].Split('#')[0];
            string extension = System.IO.Path.GetExtension(pathWithoutQuery).ToLowerInvariant();

            return extension switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".zip" => "application/zip",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".webm" => "video/webm",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".eot" => "application/vnd.ms-fontobject",
                ".otf" => "font/otf",
                ".ttf" => "font/ttf",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".pdf" => "application/pdf",
                ".cer" => "application/x-x509-ca-cert",
                ".onnx" => "application/octet-stream",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Returns the resource path as a string.
        /// </summary>
        /// <returns>The resource path.</returns>
        public override string ToString() => Path;

        /// <summary>
        /// Implicitly converts a Resource to its string path.
        /// </summary>
        /// <param name="resourcePath">The resource to convert.</param>
        /// <returns>The resource path as a string.</returns>
        public static implicit operator string(Resource resourcePath) => resourcePath.Path;
    }
}