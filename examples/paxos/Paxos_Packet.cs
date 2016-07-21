/*
Porting the P4 implementation of Paxos as described in the "Paxos made Switch-y"
paper by Huynh Tu Dang, Marco Canini, Fernando Pedone, and Robert Soule.

Nik Sultana, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using PacketDotNet;
using MiscUtil.Conversion;


public struct Paxos_Packet_Fields {
  // Length is expressed in bytes.
  public readonly static int MsgType_Length = 2;
  public readonly static int Instance_Length = 4;
  public readonly static int Round_Length = 2;
  public readonly static int Datapath_Length = 2;
  public readonly static int Value_Length = 32;

  public readonly static int MsgType_Position = 0;
  public readonly static int Instance_Position;
  public readonly static int Round_Position;
  public readonly static int VotedRound_Position;
  public readonly static int AcceptID_Position;
  public readonly static int Value_Position;

  static Paxos_Packet_Fields()
  {
    Instance_Position = MsgType_Length;
    Round_Position = Instance_Position + Instance_Length;
    VotedRound_Position = Round_Position + Round_Length;
    AcceptID_Position = VotedRound_Position + Round_Length;
    Value_Position = AcceptID_Position + Datapath_Length;
  }
}

public struct Value_Type {
  public ulong f0;
  public ulong f1;
  public ulong f2;
  public ulong f3;
}

public class Paxos_Packet : Packet {
  public ushort MsgType
  {
   get {
     return EndianBitConverter.Big.ToUInt16 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.MsgType_Position);
   }

   set {
     EndianBitConverter.Big.CopyBytes (value, header.Bytes,
         header.Offset + Paxos_Packet_Fields.MsgType_Position);
   }
  }

  public uint Instance
  {
   get {
     return EndianBitConverter.Big.ToUInt32 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.Instance_Position);
   }

   set {
     EndianBitConverter.Big.CopyBytes (value, header.Bytes,
         header.Offset + Paxos_Packet_Fields.Instance_Position);
   }
  }

  public ushort Round
  {
   get {
     return EndianBitConverter.Big.ToUInt16 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.Round_Position);
   }

   set {
     EndianBitConverter.Big.CopyBytes (value, header.Bytes,
         header.Offset + Paxos_Packet_Fields.Round_Position);
   }
  }

  public ushort Voted_Round
  {
   get {
     return EndianBitConverter.Big.ToUInt16 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.VotedRound_Position);
   }

   set {
     EndianBitConverter.Big.CopyBytes (value, header.Bytes,
         header.Offset + Paxos_Packet_Fields.VotedRound_Position);
   }
  }

  public ushort Accept_ID
  {
   get {
     return EndianBitConverter.Big.ToUInt16 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.AcceptID_Position);
   }

   set {
     EndianBitConverter.Big.CopyBytes (value, header.Bytes,
         header.Offset + Paxos_Packet_Fields.AcceptID_Position);
   }
  }

  public Value_Type Value
  {
   get {
     Value_Type v = new Value_Type();
     v.f0 = EndianBitConverter.Big.ToUInt64 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.AcceptID_Position +
        0 * sizeof(UInt64));
     v.f1 = EndianBitConverter.Big.ToUInt64 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.AcceptID_Position +
        1 * sizeof(UInt64));
     v.f2 = EndianBitConverter.Big.ToUInt64 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.AcceptID_Position +
        2 * sizeof(UInt64));
     v.f3 = EndianBitConverter.Big.ToUInt64 (header.Bytes,
        header.Offset + Paxos_Packet_Fields.AcceptID_Position +
        3 * sizeof(UInt64));
     return v;
   }

   set {
     Value_Type v = value;
     EndianBitConverter.Big.CopyBytes (v.f0, header.Bytes,
         header.Offset + Paxos_Packet_Fields.AcceptID_Position +
         0 * sizeof(UInt64));
     EndianBitConverter.Big.CopyBytes (v.f0, header.Bytes,
         header.Offset + Paxos_Packet_Fields.AcceptID_Position +
         1 * sizeof(UInt64));
     EndianBitConverter.Big.CopyBytes (v.f0, header.Bytes,
         header.Offset + Paxos_Packet_Fields.AcceptID_Position +
         2 * sizeof(UInt64));
     EndianBitConverter.Big.CopyBytes (v.f0, header.Bytes,
         header.Offset + Paxos_Packet_Fields.AcceptID_Position +
         3 * sizeof(UInt64));
   }
  }
}
