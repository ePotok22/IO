using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFF.IO
{
    public static class StreamExtensions
    {
        private const int DefaultBufferSize = 0x1000;

        public static long CopyTo(this Stream source, Stream destination, byte[] buffer)
        {
            long total = 0;
            int bytesRead;
            while (true)
            {
                bytesRead = source.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    return total;
                total += bytesRead;
                destination.Write(buffer, 0, bytesRead);
            }
        }

        public static long CopyTo(this Stream source, Stream destination, int bufferLen) =>
            source.CopyTo(destination, new byte[bufferLen]);

        public static Stream Tail(this Stream @this, int n)
        {
            if (@this.Length == 0)
                return @this;
            @this.Seek(0, SeekOrigin.End);
            int count = 0;
            while (count <= n)
            {
                @this.Position--;
                int c = @this.ReadByte();
                @this.Position--;
                if (c == '\n')
                {
                    ++count;
                }
                if (@this.Position == 0)
                    break;

            }
            return @this;
        }

        public static void WriteTo(this Stream sourceStream, Stream stream) =>
            WriteTo(sourceStream, stream, DefaultBufferSize);

        public static void WriteTo(this Stream sourceStream, Stream stream, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int n;
            while ((n = sourceStream.Read(buffer, 0, buffer.Length)) != 0)
                stream.Write(buffer, 0, n);

        }

        public static void WriteBytesToFile(byte[] bytes, string filePath)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be only whitespace characters.", nameof(filePath));

            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                fs.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Asynchronously read the contents of a Stream from its current location
        /// into a String
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static async Task<string> ReadAllTextAsync(this Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
                 return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously read all the bytes in a Stream from its current
        /// location to a byte[] array
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static async Task<byte[]> ReadAllBytesAsync(this Stream stream)
        {
            using (MemoryStream content = new MemoryStream())
            {
                byte[] buffer = new byte[4096];

                int read = await stream.ReadAsync(buffer, 0, 4096).ConfigureAwait(false);
                while (read > 0)
                {
                    content.Write(buffer, 0, read);
                    read = await stream.ReadAsync(buffer, 0, 4096).ConfigureAwait(false);
                }

                return content.ToArray();
            }
        }

        public static async Task<List<string>> ReadLinesAsync(this Stream stream)
        {
            List<string> lines = new List<string>();

            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    lines.Add(line);
            }

            return lines;
        }

        public static Func<Stream> ToStreamFactory(this Stream stream)
        {
            byte[] buffer;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                try
                {
                    stream.CopyTo(memoryStream);
                    buffer = memoryStream.ToArray();
                }
                finally
                {
                    stream.Close();
                }
            }
            return () => new MemoryStream(buffer);
        }

        public static Stream AsStream(this string value) =>
            value.AsStream(Encoding.UTF8);

        public static Stream AsStream(this string value, Encoding encoding) =>
            new MemoryStream(encoding.GetBytes(value));

        public static bool ContentEquals(this Stream stream, Stream otherStream)
        {
            bool flag = IsBinary(otherStream);

            otherStream.Seek(0L, SeekOrigin.Begin);

            if (!flag)
                return CompareText(stream, otherStream);

            return CompareBinary(stream, otherStream);
        }

        public static bool IsBinary(Stream stream)
        {
            byte[] numArray = new byte[30];
            int count = stream.Read(numArray, 0, 30);

            return Array.FindIndex(numArray, 0, count, d => d == 0) >= 0;
        }

        private static bool CompareText(Stream stream, Stream otherStream) =>
            ReadStreamLines(stream).SequenceEqual(ReadStreamLines(otherStream), StringComparer.Ordinal);

        private static IEnumerable<string> ReadStreamLines(Stream stream)
        {
            using (StreamReader streamReader = new StreamReader(stream))
            {
                while (streamReader.Peek() != -1)
                    yield return streamReader.ReadLine();
            }
        }

        private static bool CompareBinary(Stream stream, Stream otherStream)
        {
            if (stream.CanSeek && otherStream.CanSeek
                && stream.Length != otherStream.Length)
            {
                return false;
            }

            byte[] buffer1 = new byte[4096];
            byte[] buffer2 = new byte[4096];

            int count;
            do
            {
                count = stream.Read(buffer1, 0, buffer1.Length);
                if (count > 0)
                {
                    int num = otherStream.Read(buffer2, 0, count);
                    if (count != num)
                    {
                        return false;
                    }
                    for (int index = 0; index < count; ++index)
                    {
                        if (buffer1[index] != buffer2[index])
                        {
                            return false;
                        }
                    }
                }
            }

            while (count > 0);

            return true;
        }

        /// <summary>
        /// Turns a stream into a string
        /// </summary>
        /// <param name="streamToConvert">Input stream</param>
        /// <returns>Output String</returns>
        public static string ToString(Stream streamToConvert)
        {
            string retVal = string.Empty;
            Stream stream = streamToConvert;

            stream.Position = 0;
            if (stream.CanRead && stream.CanSeek)
            {
                int length = (int)stream.Length;
                byte[] buffer = new byte[length];
                stream.Read(buffer, 0, length);
                retVal = Encoding.UTF8.GetString(buffer);
            }
            return retVal;
        }

        /// <summary>
        /// Turns a stream into a byte array
        /// </summary>
        /// <param name="streamToConvert">Input stream</param>
        /// <returns>Output array</returns>
        public static byte[] ToArray(Stream streamToConvert)
        {
            byte[] retVal = null;
            Stream stream = streamToConvert;

            stream.Position = 0;
            if (stream.CanRead && stream.CanSeek)
            {
                int length = (int)stream.Length;
                retVal = new byte[length];
                stream.Read(retVal, 0, length);
            }
            return retVal;
        }

        private const int BufferSize = 0x2000;

        /// <summary>Read from a stream asynchronously.</summary>
        /// <param name="stream">The stream.</param>
        /// <param name="buffer">An array of bytes to be filled by the read operation.</param>
        /// <param name="offset">The offset at which data should be stored.</param>
        /// <param name="count">The number of bytes to be read.</param>
        /// <returns>A Task containing the number of bytes read.</returns>
        public static Task<int> ReadAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return Task<int>.Factory.FromAsync(
                stream.BeginRead, stream.EndRead, buffer, offset, count, stream /* object state */);
        }

        /// <summary>Write to a stream asynchronously.</summary>
        /// <param name="stream">The stream.</param>
        /// <param name="buffer">An array of bytes to be written.</param>
        /// <param name="offset">The offset from which data should be read to be written.</param>
        /// <param name="count">The number of bytes to be written.</param>
        /// <returns>A Task representing the completion of the asynchronous operation.</returns>
        public static Task WriteAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return Task.Factory.FromAsync(
                stream.BeginWrite, stream.EndWrite,
                buffer, offset, count, stream);
        }

    }
}
