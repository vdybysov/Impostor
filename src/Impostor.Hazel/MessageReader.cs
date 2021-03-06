﻿using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using Impostor.Api.Net.Messages;
using Microsoft.Extensions.ObjectPool;

namespace Impostor.Hazel
{
    public class MessageReader : IMessageReader
    {
        private readonly ObjectPool<MessageReader> _pool;
        private bool _inUse;

        public byte Tag { get; private set; }
        public ReadOnlyMemory<byte> Buffer { get; private set; }
        public int Position { get; set; }
        public int Length => Buffer.Length;

        internal MessageReader(ObjectPool<MessageReader> pool)
        {
            _pool = pool;
        }

        public void Update(ReadOnlyMemory<byte> buffer)
        {
            Update(byte.MaxValue, buffer);
        }

        public void Update(byte tag, ReadOnlyMemory<byte> buffer)
        {
            _inUse = true;

            Tag = tag;
            Buffer = buffer;
            Position = 0;
        }

        internal void Reset()
        {
            _inUse = false;

            Tag = byte.MaxValue;
            Buffer = null;
            Position = 0;
        }

        public IMessageReader ReadMessage()
        {
            var length = ReadUInt16();
            var tag = FastByte();
            var pos = Position;

            Position += length;

            var reader = _pool.Get();
            reader.Update(tag, Buffer.Slice(pos, length));
            return reader;
        }

        public bool ReadBoolean()
        {
            byte val = FastByte();
            return val != 0;
        }

        public sbyte ReadSByte()
        {
            return (sbyte)FastByte();
        }

        public byte ReadByte()
        {
            return FastByte();
        }

        public ushort ReadUInt16()
        {
            var output = BinaryPrimitives.ReadUInt16LittleEndian(Buffer.Span.Slice(Position));
            Position += sizeof(ushort);
            return output;
        }

        public short ReadInt16()
        {
            var output = BinaryPrimitives.ReadInt16LittleEndian(Buffer.Span.Slice(Position));
            Position += sizeof(short);
            return output;
        }

        public uint ReadUInt32()
        {
            var output = BinaryPrimitives.ReadUInt32LittleEndian(Buffer.Span.Slice(Position));
            Position += sizeof(uint);
            return output;
        }

        public int ReadInt32()
        {
            var output = BinaryPrimitives.ReadInt32LittleEndian(Buffer.Span.Slice(Position));
            Position += sizeof(int);
            return output;
        }

        public unsafe float ReadSingle()
        {
            var output = BinaryPrimitives.ReadSingleLittleEndian(Buffer.Span.Slice(Position));
            Position += sizeof(float);
            return output;
        }

        public string ReadString()
        {
            var len = ReadPackedInt32();
            var output = Encoding.UTF8.GetString(Buffer.Span.Slice(Position, len));
            Position += len;
            return output;
        }

        public ReadOnlyMemory<byte> ReadBytesAndSize()
        {
            var len = ReadPackedInt32();
            return ReadBytes(len);
        }

        public ReadOnlyMemory<byte> ReadBytes(int length)
        {
            var output = Buffer.Slice(Position, length);
            Position += length;
            return output;
        }

        public int ReadPackedInt32()
        {
            return (int)ReadPackedUInt32();
        }

        public uint ReadPackedUInt32()
        {
            bool readMore = true;
            int shift = 0;
            uint output = 0;

            while (readMore)
            {
                byte b = FastByte();
                if (b >= 0x80)
                {
                    readMore = true;
                    b ^= 0x80;
                }
                else
                {
                    readMore = false;
                }

                output |= (uint)(b << shift);
                shift += 7;
            }

            return output;
        }

        public void CopyTo(IMessageWriter writer)
        {
            writer.Write((ushort) Length);
            writer.Write((byte) Tag);
            writer.Write(Buffer);
        }

        public IMessageReader Slice(int start)
        {
            var reader = _pool.Get();
            reader.Update(Tag, Buffer.Slice(start));
            return reader;
        }

        public IMessageReader Slice(int start, int length)
        {
            var reader = _pool.Get();
            reader.Update(Tag, Buffer.Slice(start, length));
            return reader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte FastByte()
        {
            return Buffer.Span[Position++];
        }

        public void Dispose()
        {
            if (_inUse)
            {
                _pool.Return(this);
            }
        }
    }
}
