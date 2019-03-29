using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArmaOP.Arma3
{
    /// <summary>
    /// Provides functionality to decode data from compression streams.
    /// </summary>
    public class PboCompressionStream : Stream
    {
        #region Fields
        private byte[] _overflow;
        private Stream _baseStream;

        private byte[] _queueBuffer;
        private int _queueOffset;
        #endregion

        #region Properties
        public override bool CanRead {
            get {
                return true;
            }
        }

        public override bool CanSeek {
            get {
                return true;
            }
        }

        public override bool CanWrite {
            get {
                return false;
            }
        }

        public override long Length {
            get {
                throw new InvalidOperationException();
            }
        }

        public override long Position {
            get {
                throw new InvalidOperationException();
            } set {
                throw new InvalidOperationException();
            }
        }
        #endregion

        #region Methods
        public override void Flush() {
            throw new InvalidOperationException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            int remainingBytes = count;
            int bytesI = 0;

            if (remainingBytes == 0)
                return 0;

            // drain from overflow
            if (_overflow.Length > count) {
                // copy out data from overflow buffer
                Buffer.BlockCopy(_overflow, 0, buffer, offset, count);

                // remove copied bytes
                byte[] newOverflow = new byte[_overflow.Length - count];
                Buffer.BlockCopy(_overflow, count, newOverflow, 0, _overflow.Length - count);
                _overflow = newOverflow;

                return count;
            } else if (_overflow.Length > 0) {
                // copy out data from overflow buffer
                Buffer.BlockCopy(_overflow, 0, buffer, offset, _overflow.Length);
                remainingBytes -= _overflow.Length;
                _overflow = new byte[0];
            }

            // keep reading new format bytes until we fill our desired buffer
            while (true) {
                using (MemoryStream ms = new MemoryStream()) {
                    // read format commands
                    int _format = _baseStream.ReadByte();

                    // we couldn't read byte, report back how much we did manage to read
                    if (_format == -1)
                        return count - remainingBytes;

                    for (int i = 0; i < 8; i++) {
                        byte v = (byte)(((byte)_format >> i) & 1);

                        // read byte
                        if (v == 1) {
                            // try and read byte
                            int byteVal = _baseStream.ReadByte();

                            if (byteVal == -1)
                                return count - remainingBytes;

                            // write to output
                            ms.WriteByte((byte)byteVal);
                        } else {
                            // read pointer
                            byte[] ptrBytes = new byte[2];
                            _baseStream.Read(ptrBytes, 0, 2);
                            ushort ptr = BitConverter.ToUInt16(ptrBytes, 0);

                            // calculate rpos/rlen
                            int relativePos = ((ptr & 0x00FF) + (ptr & 0xF000) >> 4);
                            int len = (ptr & 0x0F00 >> 8) + 3;

                            if (_queueOffset < relativePos + len) {
#if DEBUG
                                Console.WriteLine("Relative: " + relativePos + " Len: " + len + " Queue offset: " + _queueOffset);
#endif
                                throw new Exception("The compressed stream is corrupted");
                            } else {
                                byte[] buff = new byte[len];
                                Buffer.BlockCopy(_queueBuffer, _queueOffset - relativePos, buff, 0, buff.Length);
                                ms.Write(buff, 0, buff.Length);
                            }
                        }
                    }

                    // decrement based on bytes we read
                    remainingBytes -= (int)ms.Length;

                    // got as many bytes as want, stop and place overflow bytes into buffer if required
                    if (remainingBytes == 0) {
                        Buffer.BlockCopy(ms.ToArray(), 0, buffer, offset, count);
                        return count;
                    } else if (remainingBytes < 0) {
                        byte[] buff = ms.ToArray();
                        Buffer.BlockCopy(buff, 0, buffer, offset, count);

                        // add remainder to overflow
                        _overflow = new byte[(int)ms.Length - count];
                        Buffer.BlockCopy(buff, count, _overflow, 0, _overflow.Length);
                        return count;
                    } else {
                        byte[] buff = ms.ToArray();

                        // copy data into buffer
                        Buffer.BlockCopy(buff, 0, buffer, offset + bytesI, (int)ms.Length);
                        bytesI += (int)ms.Length;

                        // if we are going to overflow the queue take the last 4096 - buff.Length
                        // and shift it to the front, discarding previous data
                        // we do it this way to reduce the number of memory copies at the expense of holding
                        // twice the required buffer size
                        if (_queueOffset + (int)ms.Length > 8191) {
                            int keepOfOld = 4096 - (int)ms.Length;
                            Buffer.BlockCopy(_queueBuffer, _queueOffset - keepOfOld, _queueBuffer, 0, keepOfOld);
                            _queueOffset = 4096;
                        }

                        // store in queue (for pointer decompression)
                        Buffer.BlockCopy(buff, 0, _queueBuffer, _queueOffset, (int)ms.Length);
                        _queueOffset += (int)ms.Length;
                    }
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return _baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value) {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new InvalidOperationException();
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new compression stream for the underlying base stream.
        /// </summary>
        /// <param name="baseStream">The base stream.</param>
        public PboCompressionStream(Stream baseStream) {
            _baseStream = baseStream;
            _overflow = new byte[0];

            _queueBuffer = new byte[8192];
            _queueOffset = 0;
        }
        #endregion
    }
}
