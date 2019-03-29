using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArmaOP.Arma3
{
    /// <summary>
    /// Represents the base class for resolvers capable of generating a stream to entry data.
    /// </summary>
    internal abstract class PboResolver
    {
        /// <summary>
        /// Resolves a stream to the entry.
        /// </summary>
        /// <returns>The data stream.</returns>
        public abstract Stream Resolve();
    }

    /// <summary>
    /// Represents a resolver backed by data in memory.
    /// </summary>
    internal class PboDataResolver : PboResolver
    {
        #region Fields
        private byte[] _bytes;
        #endregion

        #region Methods
        /// <summary>
        /// Resolves a stream to the entry in memory.
        /// </summary>
        /// <returns>The data stream.</returns>
        public override Stream Resolve() {
            return new MemoryStream(_bytes);
        }
        #endregion

        #region Constructors
        public PboDataResolver(byte[] bytes) {
            _bytes = bytes;
        }
        #endregion
    }

    /// <summary>
    /// Represents a resolver with knowledge of the entries location in the pbo file.
    /// </summary>
    internal class PboStreamResolver : PboResolver
    {
        #region Fields
        private long _offset;
        private long _dataSize;
        private Stream _stream;
        private PboPacking _packing;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the offset.
        /// </summary>
        public long Offset {
            get {
                return _offset;
            }
        }

        /// <summary>
        /// Gets the data size.
        /// </summary>
        public long DataSize {
            get {
                return _dataSize;
            }
        }
        
        /// <summary>
        /// Gets the packing type.
        /// </summary>
        public PboPacking Packing {
            get {
                return _packing;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Resolves a stream to the location in the file where the entry is located.
        /// </summary>
        /// <returns>The data stream.</returns>
        public override Stream Resolve() {
            // seek to position in stream
            _stream.Seek(_offset, SeekOrigin.Begin);

            // wrap around compression stream if packed
            if (_packing == PboPacking.Packed)
                return new PboCompressionStream(_stream);

            return _stream;
        }
        #endregion

        #region Constructors
        public PboStreamResolver(Stream stream, PboPacking packing, long offset, long dataSize) {
            _stream = stream;
            _offset = offset;
            _dataSize = dataSize;
            _packing = packing;
        }
        #endregion
    }

    /// <summary>
    /// Represents a null resolver, will throw an invalid operation exception if resolving is attempted.
    /// </summary>
    internal class PboNullResolver : PboResolver
    {
        #region Methods
        /// <summary>
        /// Resolves the pbo entry.
        /// </summary>
        /// <returns></returns>
        public override Stream Resolve() {
            throw new InvalidOperationException("This pbo entry cannot be resolved");
        }
        #endregion

        #region Constructors
        public PboNullResolver() { }
        #endregion
    }
}
