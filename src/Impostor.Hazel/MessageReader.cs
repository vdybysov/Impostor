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

        private byte _tag;

        internal MessageReader(ObjectPool<MessageReader> pool)
        {
            _pool = pool;
        }

        public byte[] Buffer { get; private set; }

        public int Offset { get; internal set; }

        public int Position { get; internal set; }

        public int Length { get; internal set; }

        public byte Tag { get; private set; }

        private int ReadPosition => Offset + Position;

        public void Update(byte[] buffer, int offset = 0, int position = 0, int? length = null, byte tag = byte.MaxValue)
        {
            Buffer = buffer;
            Offset = offset;
            Position = position;
            Length = length ?? buffer.Length;
            Tag = tag;
        }

        internal void Reset()
        {
            Tag = byte.MaxValue;
            Buffer = null;
            Offset = 0;
            Position = 0;
            Length = 0;
        }

        public IMessageReader ReadMessage()
        {
            var length = ReadUInt16();
            var tag = FastByte();
            var pos = ReadPosition;

            Position += length;

            var reader = _pool.Get();
            reader.Update(Buffer, pos, 0, length, tag);
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
            var output = BinaryPrimitives.ReadUInt16LittleEndian(Buffer.AsSpan(ReadPosition));
            Position += sizeof(ushort);
            return output;
        }

        public short ReadInt16()
        {
            var output = BinaryPrimitives.ReadInt16LittleEndian(Buffer.AsSpan(ReadPosition));
            Position += sizeof(short);
            return output;
        }

        public uint ReadUInt32()
        {
            var output = BinaryPrimitives.ReadUInt32LittleEndian(Buffer.AsSpan(ReadPosition));
            Position += sizeof(uint);
            return output;
        }

        public int ReadInt32()
        {
            var output = BinaryPrimitives.ReadInt32LittleEndian(Buffer.AsSpan(ReadPosition));
            Position += sizeof(int);
            return output;
        }

        public unsafe float ReadSingle()
        {
            var output = BinaryPrimitives.ReadSingleLittleEndian(Buffer.AsSpan(ReadPosition));
            Position += sizeof(float);
            return output;
        }

        public string ReadString()
        {
            var len = ReadPackedInt32();
            var output = Encoding.UTF8.GetString(Buffer.AsSpan(ReadPosition, len));
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
            var output = Buffer.AsMemory(ReadPosition, length);
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
            writer.Write(Buffer.AsMemory(Offset, Length));
        }

        public void Seek(int position)
        {
            Position = position;
        }

        public IMessageReader Copy(int offset = 0)
        {
            var reader = _pool.Get();
            reader.Update(Buffer, Offset + offset, Position, Length - offset, Tag);
            return reader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte FastByte()
        {
            return Buffer[Offset + Position++];
        }

        public void Dispose()
        {
            _pool.Return(this);
        }
    }
}
