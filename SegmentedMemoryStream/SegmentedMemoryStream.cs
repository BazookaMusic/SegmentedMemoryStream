
namespace Bazooka.SegmentedMemoryStream
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Buffers;
    using System.Threading.Tasks;
    using System.Threading;

    public class SegmentedMemoryStream : Stream, IDisposable
    {
        // 2^16 = 64K is below LOH threshold
        private const int defaultSegmentSizeExponent = 16;

        /// <summary>
        /// The default size of the segments.
        /// </summary>
        public const int DefaultSegmentSize = 1 << defaultSegmentSizeExponent;

        private int bitShiftForModulo = -1;

        private long finalWrittenByteIndex = 0;

        private long _position;

        private long _capacity;

        private readonly List<byte[]> segments;

        private bool disposed;

        private int CurrentSegmentIndex => (int)((ulong)this.Position >> this.bitShiftForModulo);

        private int CurrentSegmentPosition => (int)(this.Position & (this.SegmentSize - 1));

        private byte[] CurrentSegment => this.segments[this.CurrentSegmentIndex];

        /// <summary>
        /// Gets a value indicating if the stream can be read from.
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// Gets a value indicating if the stream supports seeking.
        /// </summary>
        public override bool CanSeek => true;

        /// <summary>
        /// Gets a value indicating if the stream can be written to.
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// Gets the length of the stream in bytes.
        /// </summary>
        public override long Length => this.finalWrittenByteIndex;

        /// <summary>
        /// Returns the current capacity of the stream.
        /// </summary>
        public long Capacity
        {
            get => this._capacity;
        }

        /// <summary>
        /// Gets or sets the position in the current stream.
        /// </summary>
        public override long Position
        {
            get => this._position;

            set
            {
                

                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(string.Format("The position {0} is outside the bounds of the stream.", value));
                }

                if (value > this._capacity)
                {
                    this.EnsureSizeForWrite(value - this._position);
                }

                this._position = value;
            }
        }

        /// <summary>
        /// The current segment size used.
        /// </summary>
        public int SegmentSize { get; }


        /// <summary>
        /// Creates a segmented memory stream.
        /// </summary>
        public SegmentedMemoryStream() : this(defaultSegmentSizeExponent)
        {
        }

        /// <summary>
        /// Create a segmented memory stream where the segment size is 2^segmentSizeExponent.
        /// </summary>
        /// <param name="segmentSizeExponent">The exponent of the size.</param>
        public SegmentedMemoryStream(int segmentSizeExponent) : this(1 << segmentSizeExponent, segmentSizeExponent)
        {
        }

        /// <summary>
        /// Create a segmented memory stream.
        /// </summary>
        /// <param name="segmentSizeExponent">The exponent of the size. Size is 2 ^ segmentSizeExponent</param>
        /// <param name="startingCapacity">The starting capacity of the stream.</param>
        public SegmentedMemoryStream(int startingCapacity, int segmentSizeExponent)
        {
            

            if (startingCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(string.Format("'{0}' has to be a positive integer", nameof(startingCapacity)));
            }

            if (segmentSizeExponent <= 0)
            {
                throw new ArgumentOutOfRangeException(string.Format("'{0}' has to be a positive integer", nameof(segmentSizeExponent)));
            }

            this.SegmentSize = 1 << segmentSizeExponent;
            this.bitShiftForModulo = segmentSizeExponent;

            int noSegments = startingCapacity >> bitShiftForModulo;
            this._capacity = noSegments << bitShiftForModulo;
            this.segments = new List<byte[]>(noSegments);

            for (int i = 0; i < noSegments; i++)
            {
                this.segments.Add(new byte[this.SegmentSize]);
            }
        }

        /// <summary>
        /// No-op, because the stream is memory based.
        /// </summary>
        public override void Flush()
        {
            // does not do anything due to no underlying device      
        }

        /// <summary>
        /// Returns the total amount of bytes read from the stream and advances the position by the same amount.
        /// </summary>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <param name="offset">The offset of the buffer to start writing the data to.</param>
        /// <param name="count">The count of bytes to read.</param>
        /// <returns>The amount of bytes read or zero if the end of the stream was reached.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!ValidateBufferBounds(buffer.Length, offset, count))
            {
                // offset is larger than buffer size
                return 0;
            }

            return this.Read(buffer.AsSpan().Slice(offset, count));
        }


        /// <summary>
        /// Returns the total amount of bytes read from the stream and advances the position by the same amount.
        /// </summary>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <returns>The amount of bytes read or zero if the end of the stream was reached.</returns>
        public override int Read(Span<byte> buffer)
        {
            

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            int count = buffer.Length;

            while (count > 0 && this.Position != this.finalWrittenByteIndex)
            {
                int currentSegmentIndex = this.CurrentSegmentIndex;

                byte[] currentSegment = this.segments[currentSegmentIndex];
                int startingPosition = this.CurrentSegmentPosition;

                int amountToRead = Math.Min(currentSegment.Length - startingPosition, count);
                Span<byte> segmentSpanToCopyFrom = currentSegment.AsSpan().Slice(startingPosition, amountToRead);

                segmentSpanToCopyFrom.CopyTo(buffer.Slice(buffer.Length - count, count));

                count -= amountToRead;
                this.Position += amountToRead;
            }

            return buffer.Length - count;
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">The offset</param>
        /// <param name="origin">The origin of the seek operation</param>
        /// <returns>The final position</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            

            switch (origin)
            {
                case SeekOrigin.Begin:
                    this.Position = offset;
                    break;
                case SeekOrigin.Current:
                    this.Position += offset;
                    break;
                case SeekOrigin.End:
                    this.Position += offset;
                    break;
            }

            return this.Position;
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The value to set the length to.</param>
        public override void SetLength(long value)
        {
            

            if (this.Length < 0)
            {
                throw new ArgumentOutOfRangeException("Length cannot be negative.");
            }

            this.finalWrittenByteIndex = value;

            if (this.Length < value)
            {
                this.Shrink();
            }
            else if (this.Length > value)
            {
                this.EnsureSizeForWrite(value - this.Length);
            }
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the position accordingly.
        /// </summary>
        /// <param name="buffer">The buffer of data.</param>
        /// <param name="offset">The offset to start at.</param>
        /// <param name="count">How many bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!ValidateBufferBounds(buffer.Length, offset, count))
            {
                return;
            }

            this.Write(buffer.AsSpan().Slice(offset, count));
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the position accordingly.
        /// </summary>
        /// <param name="buffer">The buffer of data.</param>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            int count = buffer.Length;

            if (count + this.Position > this.Capacity)
            {
                this.EnsureSizeForWrite(count);
            }

            while (count > 0)
            {
                int currentSegmentIndex = this.CurrentSegmentIndex;

                byte[] currentSegment = this.segments[currentSegmentIndex];
                int startingPosition = this.CurrentSegmentPosition;

                int amountToWrite = Math.Min(currentSegment.Length - startingPosition, count);
                Span<byte> segmentSpanToCopyTo = currentSegment.AsSpan().Slice(startingPosition, amountToWrite);

                ReadOnlySpan<byte> bufferSpan = buffer.Slice(buffer.Length - count, Math.Min(amountToWrite, count));

                bufferSpan.CopyTo(segmentSpanToCopyTo);

                count -= amountToWrite;

                this.finalWrittenByteIndex += amountToWrite;
                this.Position += amountToWrite;
            }
        }

        /// <summary>
        /// No-op
        /// </summary>
        public override void Close()
        {
        }

        /// <inheritdoc/>
        public override void CopyTo(Stream destination, int bufferSize)
        {
            

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("Buffer size must be positive.");
            }

            Span<byte> buffer = stackalloc byte[bufferSize];

            while (this.Position < this.finalWrittenByteIndex)
            {
                this.Read(buffer);
                destination.Write(buffer);
            }
        }

        public override int ReadByte()
        {
            

            if (this.Position == this.finalWrittenByteIndex)
            {
                return -1;
            }

            var segment = this.CurrentSegment;
            int value = segment[this.CurrentSegmentPosition];

            this.Position++;

            return value;
        }

        public override void WriteByte(byte value)
        {
            

            if (this.Position + 1 > this.Capacity)
            {
                this.EnsureSizeForWrite(this.Position + 1);
            }

            var segment = this.CurrentSegment;
            segment[this.CurrentSegmentPosition] = value;

            this.finalWrittenByteIndex++;
            this.Position++;
        }

        /// <summary>
        /// Pre-allocate extra segments to write data.
        /// </summary>
        /// <param name="sizeToWrite">The size of the data to write</param>
        public void EnsureSizeForWrite(long sizeToWrite)
        {
            

            long finalPosition = this.Position + sizeToWrite;
            int nSegmentsAfterWrite = (int)((ulong)finalPosition >> this.bitShiftForModulo) + ((finalPosition & (this.SegmentSize - 1)) == 0 ? 0 : 1);

            int segmentsToAdd = nSegmentsAfterWrite - this.segments.Count;

            if (segmentsToAdd > 0)
            {
                this.segments.Capacity = nSegmentsAfterWrite;
                
                for (int i = 0; i < segmentsToAdd; i++)
                {
                    this.segments.Add(new byte[this.SegmentSize]);
                }

                this._capacity = nSegmentsAfterWrite * this.SegmentSize;
            }
        }

        /// <summary>
        /// Shrinks the capacity of the stream to exactly fit the current length.
        /// </summary>
        public void Shrink()
        {
            

            int nSegmentsAfterShrink = (int)((ulong)this.Length >> this.bitShiftForModulo) + ((this.Length & (this.SegmentSize - 1)) == 0 ? 0 : 1);
            this.segments.RemoveRange(nSegmentsAfterShrink, this.segments.Count - nSegmentsAfterShrink);

            this._capacity = this.segments.Count * this.SegmentSize;
        }

        private static bool ValidateBufferBounds(int bufferLength, int offset, int count)
        {
            if (offset > bufferLength)
            {
                return false;
            }

            if (offset + count > bufferLength)
            {
                throw new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            }

            return true;
        }
    }
}
