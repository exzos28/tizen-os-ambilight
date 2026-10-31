using System;
using Google.FlatBuffers;

namespace Service
{
    // Minimal encoder/decoder for HyperHDR's FlatBuffers input (TCP, default port 19400).
    //
    // Written by hand against HyperHDR's schema instead of committing flatc output:
    // include/flatbuffers/parser/hyperhdr_request.fbs / hyperhdr_reply.fbs (namespace
    // hyperhdrnet, unchanged for RawImage since HyperHDR v20). CI decodes the bytes produced
    // here with flatc against that schema, independently of this code.
    //
    //   union Command   { Color = 1, Image = 2, Clear = 3, Register = 4 }
    //   union ImageType { RawImage = 1, NV12Image = 2 }
    //   table Request   { command_type: ubyte (slot 0); command (slot 1) }
    //   table Register  { origin: string (0); priority: int (1) }
    //   table Image     { data_type: ubyte (0); data (1); duration: int = -1 (2) }
    //   table RawImage  { data: [ubyte] (0); width: int = -1 (1); height: int = -1 (2) }
    //   table Reply     { error: string (0); video: int = -1 (1); registered: int = -1 (2) }
    //
    // On the wire every message is prefixed with its length as a 4-byte BIG-endian integer
    // (FlatBuffers' own size prefix is little-endian, so it is added by hand).
    static class HyperHdrProtocol
    {
        private const byte CommandImage = 2;
        private const byte CommandRegister = 4;
        private const byte ImageTypeRawImage = 1;

        public const int MinPriority = 50;    // HyperHDR ignores Register outside 50..250
        public const int MaxPriority = 250;

        public static byte[] EncodeRegister(string origin, int priority)
        {
            var b = new FlatBufferBuilder(64);
            var originOffset = b.CreateString(origin);

            b.StartTable(2);
            b.AddOffset(0, originOffset.Value, 0);
            b.AddInt(1, priority, 0);
            int register = b.EndTable();

            return FinishRequest(b, CommandRegister, register);
        }

        public static byte[] EncodeRawImage(byte[] rgb, int width, int height)
        {
            if (rgb.Length != width * height * 3) throw new ArgumentException("rgb size must be width*height*3");

            var b = new FlatBufferBuilder(rgb.Length + 128);
            b.StartVector(1, rgb.Length, 1);
            b.Add(rgb);
            var data = b.EndVector();

            b.StartTable(3);
            b.AddOffset(0, data.Value, 0);
            b.AddInt(1, width, -1);
            b.AddInt(2, height, -1);
            int raw = b.EndTable();

            b.StartTable(3);
            b.AddByte(0, ImageTypeRawImage, 0);
            b.AddOffset(1, raw, 0);
            // duration (slot 2) left at its default -1 = until the next image / disconnect
            int image = b.EndTable();

            return FinishRequest(b, CommandImage, image);
        }

        private static byte[] FinishRequest(FlatBufferBuilder b, byte commandType, int command)
        {
            b.StartTable(2);
            b.AddByte(0, commandType, 0);
            b.AddOffset(1, command, 0);
            int request = b.EndTable();
            b.Finish(request);
            return b.SizedByteArray();
        }

        // Prepends the 4-byte big-endian length HyperHDR expects. One buffer, so the
        // message goes out in a single send.
        public static byte[] Frame(byte[] message)
        {
            var framed = new byte[4 + message.Length];
            framed[0] = (byte)(message.Length >> 24);
            framed[1] = (byte)(message.Length >> 16);
            framed[2] = (byte)(message.Length >> 8);
            framed[3] = (byte)message.Length;
            Buffer.BlockCopy(message, 0, framed, 4, message.Length);
            return framed;
        }

        public static int ReadLength(byte[] prefix) =>
            (prefix[0] << 24) | (prefix[1] << 16) | (prefix[2] << 8) | prefix[3];

        public readonly struct Reply
        {
            public readonly string Error;      // null or empty = OK
            public readonly int Video;         // -1 if absent
            public readonly int Registered;    // priority in use, -1 if absent
            public Reply(string error, int video, int registered) { Error = error; Video = video; Registered = registered; }
        }

        public static Reply DecodeReply(byte[] body)
        {
            var bb = new ByteBuffer(body);
            var t = new Table(bb.GetInt(bb.Position) + bb.Position, bb);
            int o;
            string error = (o = t.__offset(4)) != 0 ? t.__string(o + t.bb_pos) : null;
            int video = (o = t.__offset(6)) != 0 ? bb.GetInt(o + t.bb_pos) : -1;
            int registered = (o = t.__offset(8)) != 0 ? bb.GetInt(o + t.bb_pos) : -1;
            return new Reply(error, video, registered);
        }
    }
}
