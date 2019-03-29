using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArmaOP.Arma3
{
    /// <summary>
    /// Provides functionality to read, write and create pbo files.
    /// </summary>
    public class PboFile
    {
        #region Fields
        private string _path;
        private Stream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;

        private List<string> _productEntries;
        private List<PboEntry> _entries;
        private bool _storeTimestamps;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets if timestamps should be stored when writing.
        /// </summary>
        public bool StoreTimstamps {
            get {
                return _storeTimestamps;
            } set {
                _storeTimestamps = value;
            }
        }

        /// <summary>
        /// Gets the file path if committed.
        /// </summary>
        public string Path {
            get {
                return _path;
            }
        }

        /// <summary>
        /// Gets if the file is committed to disk.
        /// </summary>
        public bool IsCommitted {
            get {
                return _path != null;
            }
        }

        /// <summary>
        /// Gets the product entries.
        /// </summary>
        public string[] ProductEntries {
            get {
                return _productEntries.ToArray();
            }
        }
        
        /// <summary>
        /// Gets the product entries as key value pairs.
        /// </summary>
        public KeyValuePair<string, string>[] ProductEntriesPairs {
            get {
                // not enough product entries
                if (_productEntries.Count < 2)
                    return new KeyValuePair<string, string>[0];

                // build key/value pairs
                KeyValuePair<string, string>[] kvs = new KeyValuePair<string, string>[_productEntries.Count / 2];

                for (int i = 0; i < _productEntries.Count; i=i+2) {
                    kvs[i / 2] = new KeyValuePair<string, string>(_productEntries[i], _productEntries[i + 1]);
                }

                return kvs;
            }
        }

        /// <summary>
        /// Gets the entries.
        /// </summary>
        public PboEntry[] Entries {
            get {
                return _entries.ToArray();
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Reads a null-terminated string from the reader.
        /// </summary>
        /// <param name="reader">The reader.</param>
        private static string ReadStringNT(BinaryReader reader) {
            using (MemoryStream ms = new MemoryStream()) {
                byte c = reader.ReadByte();

                while (c != 0) {
                    ms.WriteByte(c);
                    c = reader.ReadByte();
                }

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// Writes a null-terminated string to the writer.
        /// </summary>
        /// <param name="val">The value.</param>
        /// <param name="writer">The writer.</param>
        private void WriteStringNT(string val, BinaryWriter writer) {
            writer.Write(Encoding.UTF8.GetBytes(val));
            writer.Write((byte)0);
        }

        /// <summary>
        /// Loads entries into memory.
        /// </summary>
        private void Load() {
            // seek to begin
            _stream.Seek(0, SeekOrigin.Begin);

            // file structures
            List<FileEntry> fileEntries = new List<FileEntry>();

            // begin reading structs
            string name;
            uint originalSize, dataSize, timestamp;
            PboPacking packing;

            while (true) {
                name = ReadStringNT(_reader);
                packing = (PboPacking)_reader.ReadUInt32();
                originalSize = _reader.ReadUInt32();
                _reader.ReadUInt32();
                timestamp = _reader.ReadUInt32();
                dataSize = _reader.ReadUInt32();

                if (packing == PboPacking.Uncompressed)
                    originalSize = dataSize;

                // handle eoh
                if (name == "" && packing != PboPacking.ProductEntry)
                    break;

                // handle product entries
                if (packing == PboPacking.ProductEntry) {
                    string str = ReadStringNT(_reader);

                    while (str != "") {
                        _productEntries.Add(str);
                        str = ReadStringNT(_reader);
                    }
                } else {
                    fileEntries.Add(new FileEntry() {
                        Name = name,
                        DataSize = dataSize,
                        OriginalSize = originalSize,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp),
                        Packing = packing
                    });
                }
            }

            // compute final offsets
            long dataOffset = _stream.Position;

            foreach (FileEntry f in fileEntries) {
                // create entry
                PboEntry entry = new PboEntry(f.Name, f.OriginalSize, f.Timestamp, this, new PboStreamResolver(_stream, f.Packing, dataOffset, f.DataSize));

                _entries.Add(entry);
                dataOffset += f.OriginalSize;
            }
        }

        private void ResolveAllEntries() {
            // get all file entries and clear array
            PboEntry[] entries = _entries.ToArray();
            _entries.Clear();

            // resolve any binded files
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].Resolver is PboStreamResolver) {
                    // get file resolver
                    PboStreamResolver resolver = entries[i].Resolver as PboStreamResolver;

                    // read data from file
                    Stream pboStream = resolver.Resolve();
                    byte[] data = new byte[entries[i].Size];
                    
                    if (resolver.Packing == PboPacking.Packed)
                        Console.WriteLine(entries[i].Name);
                    try {
                        pboStream.Read(data, 0, (int)entries[i].Size);
                        if (resolver.Packing == PboPacking.Packed)
                            Console.WriteLine(Encoding.ASCII.GetString(data));
                    } catch (Exception ex) {
                        if (resolver.Packing == PboPacking.Packed)
                            Console.WriteLine("Error: " + ex.Message);
                    }

                    // load into and convert to memory backed resolver
                    entries[i] = new PboEntry(entries[i].Name, entries[i].Size, entries[i].Timestamp, this, new PboDataResolver(data));
                }
            }

            // restore entries
            _entries = new List<PboEntry>(entries);
        }

        /// <summary>
        /// Saves all entries to the provided path.
        /// </summary>
        /// <param name="path">The path.</param>
        public void Save(string path) {
            // save to same path
            if (path == _path) {
                Save();
                return;
            }

            // save to new path
            _path = path;
            Save(new FileStream(path, FileMode.Create));
        }

        /// <summary>
        /// Saves all the entries to the stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public void Save(Stream stream) {
            // save to new path
            ResolveAllEntries();
            _stream = stream;
            _writer = new BinaryWriter(_stream);
            _reader = new BinaryReader(_stream);
            Save();
        }

        /// <summary>
        /// Saves entries to file, all entries data must be fully loaded into data before the file can be updated.
        /// </summary>
        private void Save() {
            ResolveAllEntries();

            // reopen to overwrite
            if (_path != null) {
                _stream.Dispose();
                _stream = new FileStream(_path, FileMode.Create);
            }

            _writer = new BinaryWriter(_stream);
            _reader = new BinaryReader(_stream);

            // write product entries
            if (_productEntries.Count > 0) {
                // write entry header
                WriteStringNT("", _writer);
                _writer.Write((int)PboPacking.ProductEntry);
                _writer.Write(0);
                _writer.Write(0);
                _writer.Write(_storeTimestamps ? (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0);
                _writer.Write(0);

                // write entries
                foreach(string entry in _productEntries) {
                    WriteStringNT(entry, _writer);
                }

                WriteStringNT("", _writer);
            }

            // write file entry headers
            foreach(PboEntry entry in _entries) {
                WriteStringNT(entry.Name, _writer);
                _writer.Write((int)PboPacking.Uncompressed);
                _writer.Write(0);
                _writer.Write(0);
                _writer.Write(_storeTimestamps ? (int)entry.Timestamp.ToUnixTimeSeconds() : 0);
                _writer.Write((int)entry.Size);
            }

            // last entry
            WriteStringNT("", _writer);
            _writer.Write((int)PboPacking.Uncompressed);
            _writer.Write(new byte[16]);

            // write datas
            foreach (PboEntry entry in _entries) {
                byte[] data = new byte[entry.Size];

                try {
                    // copy over bytes
                    Stream stream = entry.Resolver.Resolve();
                    stream.Read(data, 0, (int)entry.Size);
                } catch(Exception) {
                }

                _stream.Write(data, 0, data.Length);
            }

            // flush buffers
            _stream.Flush();
        }

        /// <summary>
        /// Add a new entry from a data buffer.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="data">The data.</param>
        public void AddEntry(string path, byte[] data) {
            _entries.Add(new PboEntry(path, data.Length, DateTimeOffset.UtcNow, this, new PboDataResolver(data)));
        }

        /// <summary>
        /// Adds a raw product entry.
        /// </summary>
        /// <param name="val">The product entry value.</param>
        public void AddProductEntry(string val) {
            _productEntries.Add(val);
        }

        /// <summary>
        /// Adds a key/value product entry.
        /// </summary>
        /// <param name="key">The product entry key.</param>
        /// <param name="val">The product entry value.</param>
        public void AddProductEntry(string key, string val) {
            _productEntries.Add(key);
            _productEntries.Add(val);
        }

        /// <summary>
        /// Finds the product entry by key.
        /// </summary>
        /// <param name="key">The product entry key.</param>
        /// <returns>The value.</returns>
        public string FindProductEntry(string key) {
            KeyValuePair<string, string>[] kvs = ProductEntriesPairs;
            string keyLower = key.ToLower();

            foreach (KeyValuePair<string, string> kv in kvs) {
                if (kv.Key.ToLower() == keyLower)
                    return kv.Value;
            }

            return null;
        }
        #endregion

        #region Structures
        /// <summary>
        /// Represents a file entry, must be stored before creating PboEntry objects
        /// as the offset base cannot be known until the entire header is read.
        /// </summary>
        struct FileEntry
        {
            public long DataSize { get; set; }
            public long OriginalSize { get; set; }
            public string Name { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public PboPacking Packing { get; set; }
        }
        #endregion

        #region Constructors/Destructors
        /// <summary>
        /// Creates a new pbo file.
        /// </summary>
        public PboFile() {
            _productEntries = new List<string>();
            _entries = new List<PboEntry>();
        }

        /// <summary>
        /// Creates a pbo file object and either opens an existing PBO or creates a new one at the provided path.
        /// </summary>
        /// <param name="path">The path.</param>
        public PboFile(string path)
            : this(path, FileAccess.ReadWrite) {
        }

        /// <summary>
        /// Creates a pbo file object and either opens an existing PBO or creates a new one at the provided path.
        /// The file will be opened in binding mode.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="access">The access permissions.</param>
        public PboFile(string path, FileAccess access)
            : this() {
            // set path
            _path = path;

            // open stream
            _stream = new FileStream(path, FileMode.OpenOrCreate);

            // reader/writer
            _reader = new BinaryReader(_stream);

            if ((access & FileAccess.Write) == FileAccess.Write)
                _writer = new BinaryWriter(_stream);

            // determine if the file needs to be created/loaded
            if (_stream.Length == 0)
                if (access == FileAccess.Read)
                    throw new InvalidOperationException("The access permissions must allow write if the PBO is to be created");
            else
                Load();
        }

        /// <summary>
        /// Disposes of the underlying stream.
        /// </summary>
        ~PboFile() {
            _stream.Dispose();
        }
        #endregion
    }
}
