
namespace SegmentedMemoryStreamTest
{
    using Bazooka.SegmentedMemoryStream;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.IO;

    [TestClass]
    public class SegmentedMemoryStreamTest
    {
        [TestMethod]
        public void SegmentedMemoryStreamConstructors_Initialize_With_Correct_Values()
        {
            var stream = new SegmentedMemoryStream();

            Assert.AreEqual(0, stream.Position);
            Assert.AreEqual(0, stream.Length);
            Assert.AreEqual(true, stream.CanRead);
            Assert.AreEqual(true, stream.CanSeek);
            Assert.AreEqual(true, stream.CanWrite);

            stream = new SegmentedMemoryStream(3);

            Assert.AreEqual(0, stream.Position);
            Assert.AreEqual(0, stream.Length);
            Assert.AreEqual(true, stream.CanRead);
            Assert.AreEqual(true, stream.CanSeek);
            Assert.AreEqual(true, stream.CanWrite);

            stream = new SegmentedMemoryStream(1000, 3);

            Assert.AreEqual(0, stream.Position);
            Assert.AreEqual(0, stream.Length);
            Assert.AreEqual(true, stream.CanRead);
            Assert.AreEqual(true, stream.CanSeek);
            Assert.AreEqual(true, stream.CanWrite);
        }

        [TestMethod]
        public void SegmentedMemoryStream_Read()
        {
            var stream = new SegmentedMemoryStream();

            byte[] testData = CreateBuffer(stream.SegmentSize * 15);

            stream.Write(testData);

            byte[] buffer = new byte[stream.SegmentSize / 2];
            CompareStreamWithReferenceData(stream, testData, stream.SegmentSize / 2);
            CompareStreamWithReferenceData(stream, testData, stream.SegmentSize * 2);
            CompareStreamWithReferenceData(stream, testData, 4);
        }

        [TestMethod]
        public void SegmentedMemoryStream_SingleSegment_ReadWrite()
        {
            byte[] testData = CreateBuffer(1024);
            this.ReadWriteTest(testData);
        }

        [TestMethod]
        public void SegmentedMemoryStream_MultipleSegment_ReadWrite()
        {
            byte[] testData = CreateBuffer(SegmentedMemoryStream.DefaultSegmentSize * 150);
            this.ReadWriteTest(testData);
        }

        [TestMethod]
        public void SegmentedMemoryStream_Seek_Same_As_Memory_Stream()
        {
            var reference = new MemoryStream();
            var actual = new SegmentedMemoryStream();

            for (int i = 0; i < actual.SegmentSize * 100; i++)
            {
                actual.WriteByte((byte)(i % 256));
                reference.WriteByte((byte)(i % 256));
            }

            actual.Seek(276, SeekOrigin.Begin);
            reference.Seek(276, SeekOrigin.Begin);

            CompareStreamsToEnd(reference, actual);

            actual.Seek(-276, SeekOrigin.End);
            reference.Seek(-276, SeekOrigin.End);

            CompareStreamsToEnd(reference, actual);


            actual.Seek(-276, SeekOrigin.End);
            reference.Seek(-276, SeekOrigin.End);

            actual.Seek(20, SeekOrigin.Current);
            reference.Seek(20, SeekOrigin.Current);
            CompareStreamsToEnd(reference, actual);
        }

        [TestMethod]
        public void SegmentedMemoryStream_Throws_Same_Exception_As_MemoryStream_On_Wrong_Offsets()
        {
            var data = CreateBuffer(5);

            Action<Stream> insertWrong = (Stream stream) =>
            {
                stream.Write(data, 1, 5000);
            };

            ExceptionsMatchForStreams(insertWrong);
        }

        [TestMethod]
        public void SegmentedMemoryStream_Returns_0_For_Offset_Outside_Buffer()
        {
            var stream = new SegmentedMemoryStream();
            stream.Position = 5;
            Assert.AreEqual(0, stream.Read(new byte[10], 1000, 5000));
        }

        [TestMethod]
        public void SegmentedMemoryStream_Throws_Same_Exception_As_MemoryStream_On_Exceeding_Position()
        {
            var data = CreateBuffer(5);

            Action<Stream> insertWrong = (Stream stream) =>
            {
                stream.Position = -1;
            };

            ExceptionsMatchForStreams(insertWrong);
        }

        [TestMethod]
        public void SegmentedMemoryStream_Capacity_Test()
        {
            var actual = new SegmentedMemoryStream();

            var data = CreateBuffer(actual.SegmentSize * 10);

            actual.Write(data);

            Assert.AreEqual(actual.Length, data.Length);

            // One more segment
            Assert.AreEqual(10 * actual.SegmentSize, actual.Capacity);

            // More capacity
            actual.EnsureSizeForWrite(actual.SegmentSize * 4);

            // Should not need to grow further
            var data2 = CreateBuffer(actual.SegmentSize * 4);
            actual.Write(data2);

            Assert.AreEqual(14 * actual.SegmentSize, actual.Capacity);

            actual.EnsureSizeForWrite(actual.SegmentSize * 4);

            Assert.AreEqual(18 * actual.SegmentSize, actual.Capacity);

            actual.Shrink();

            Assert.AreEqual(14 * actual.SegmentSize, actual.Capacity);
        }

        [TestMethod]
        public void SegmentedMemoryStream_Capacity_Grows_With_Position()
        {
            var stream = new SegmentedMemoryStream();
            stream.Position = stream.SegmentSize * 2;
            Assert.AreEqual(stream.SegmentSize * 2, stream.Capacity);
        }

        [TestMethod]
        public void SegmentedMemoryStream_SetLength_Test()
        {
            var actual = new SegmentedMemoryStream();

            for (int i = 0; i < actual.SegmentSize * 15; i++)
            {
                actual.WriteByte((byte)(i % 255));
            }

            // Shrink
            actual.SetLength(actual.SegmentSize);

            Assert.AreEqual(actual.Length, actual.SegmentSize);
            actual.Seek(actual.SegmentSize, SeekOrigin.Begin);

            Assert.AreEqual(-1, actual.ReadByte());

            // Grow
            long prevLen = actual.Length;
            actual.SetLength(prevLen * 2);
            Assert.AreEqual(prevLen * 2, actual.Length);
        }

        [TestMethod]
        public void SegmentedMemoryStream_CopyTo_Test()
        {
            var segStream = new SegmentedMemoryStream();
            var segStream2 = new SegmentedMemoryStream();
            var memoryStream = new MemoryStream();

            byte[] reference = CreateBuffer(segStream.SegmentSize * 15);
            segStream.Write(reference);
            segStream.Position = 0;

            segStream.CopyTo(memoryStream);
            segStream.CopyTo(segStream2);

            memoryStream.Position = 0;
            segStream2.Position = 0;
            CompareStreamWithReferenceData(memoryStream, reference, 1024);
            CompareStreamWithReferenceData(segStream2, reference, 1024);
        }

        [TestMethod]
        public void SegmentedMemoryStream_Dispose_Test()
        {
            var segStream = new SegmentedMemoryStream();
            segStream.Dispose();


            Assert.ThrowsException<ObjectDisposedException>(() => segStream.ReadByte());
        }

        private void CompareStreamWithReferenceData(Stream stream, byte[] reference, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];

            stream.Position = 0;

            int index = 0;

            int readBytes;
            while ((readBytes = stream.Read(buffer)) != 0)
            {
                for (int i = 0; i < readBytes; i++)
                {
                    Assert.AreEqual(buffer[i], reference[index++]);
                }
            }
        }

        private void ExceptionsMatchForStreams(Action<Stream> action)
        {
            var stream = new SegmentedMemoryStream();
            var referenceStream = new MemoryStream();

            try
            {
                action(referenceStream);
            }
            catch (Exception e)
            {
                try
                {
                    action(stream);
                }
                catch (Exception e2)
                {
                    Assert.AreEqual(e2.GetType(), e.GetType());
                    return;
                }

                throw new InvalidOperationException("Failed to emit any exception");
            }

            throw new InvalidOperationException("Failed to emit any exception");
        }

        private void ReadWriteTest(byte[] testData)
        {
            var stream = new SegmentedMemoryStream();
            var referenceStream = new MemoryStream();

            Assert.ThrowsException<ArgumentNullException>(() => stream.Write(null));
            Assert.ThrowsException<ArgumentNullException>(() => stream.Read(null));

            Assert.AreEqual(0, stream.Position);
            Assert.AreEqual(0, stream.Length);

            this.SimpleWrite(testData, referenceStream, stream);
            Assert.AreEqual(stream.Length, testData.Length);

            stream.Write(testData, 5, testData.Length - 5);
            referenceStream.Write(testData, 5, testData.Length - 5);

            Assert.AreEqual(referenceStream.Position, stream.Position);

            stream.Position = 0;
            referenceStream.Position = 0;

            while (stream.Position != stream.Length)
            {
                int expected = referenceStream.ReadByte();
                int actual = stream.ReadByte();

                Assert.AreEqual(expected, actual);
            }

            Assert.AreEqual(stream.ReadByte(), -1);

            Assert.AreEqual(referenceStream.Position, stream.Position);
        }

        private void SimpleWrite(byte[] testData, Stream referenceStream, Stream actualStream)
        {
            // write twice
            actualStream.Write(testData, 0, testData.Length);
            referenceStream.Write(testData, 0, testData.Length);

            Assert.AreEqual(referenceStream.Position, actualStream.Position);
        }

        private void CompareStreamsToEnd(Stream reference, Stream actual)
        {
            Assert.AreEqual(reference.Position, actual.Position);
            Assert.AreEqual(reference.Length, actual.Length);

            int refByte = 0;
            do
            {
                refByte = reference.ReadByte();
                int actualByte = actual.ReadByte();

                Assert.AreEqual(refByte, actualByte);
            }
            while (refByte != -1);
        }

        private byte[] CreateBuffer(int size)
        {
            byte[] testData = new byte[size];

            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)(i % 256);
            }

            return testData;
        }
    }
}
