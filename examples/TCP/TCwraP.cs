/*
IBerkeleySocket wrapper for .NET's TCP interface, for testing against TCPuny.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Pax_TCP;

public class TCwraP : IBerkeleySocket {
    uint max_conn; // FIXME unused
    uint max_backlog;

    public TCwraP (uint max_conn, uint max_backlog) {
      this.max_conn = max_conn;
      this.max_backlog = max_backlog;
    }

    public class SockID_dotNET : SockID {
      public readonly Socket base_socket;

      public SockID_dotNET () : base(0/*FIXME const*/) {
        this.base_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
      }

      public SockID_dotNET (Socket s) : base(0/*FIXME const*/) {
        this.base_socket = s;
      }
    }

    public Result<SockID> socket (Internet_Domain domain, Internet_Type type, Internet_Protocol prot) {
      Debug.Assert (domain == Internet_Domain.AF_Inet);
      Debug.Assert (type == Internet_Type.Sock_Stream);
      Debug.Assert (prot == Internet_Protocol.TCP);
      SockID sid = (SockID)(new SockID_dotNET());
      return new Result<SockID> (sid, null);
    }

    public Result<bool> bind (SockID sid, SockAddr_In address) {
      SockID_dotNET s = upcast_sock(sid);

      IPEndPoint ep = new IPEndPoint (address.address, (Int32)address.port/*FIXME cast*/);
      s.base_socket.Bind(ep);
      return new Result<bool> (true, null);
      // FIXME return "false" if we have problem
    }

    public Result<bool> listen (SockID sid) {
      SockID_dotNET s = upcast_sock(sid);
      s.base_socket.Listen((int)this.max_backlog); // FIXME casting uint into int
      return new Result<bool> (true, null);
    }

    public Result<SockID> accept (SockID sid, out SockAddr_In address) {
      SockID_dotNET s = upcast_sock(sid);
      Socket client_s = s.base_socket.Accept();

      IPEndPoint ep;
      if (client_s.RemoteEndPoint is IPEndPoint) {
        ep = (IPEndPoint)client_s.RemoteEndPoint;
      } else {
        throw new Exception("Can only handle IPEndPoint");
      }

      address = new SockAddr_In ((uint)ep.Port/*FIXME cast*/, ep.Address);

      sid = (SockID)(new SockID_dotNET(client_s));
      return new Result<SockID> (sid, null);
    }

    public Result<int> write (SockID sid, byte[] buf, uint count) {
      SockID_dotNET s = upcast_sock(sid);
      int result = s.base_socket.Send(buf, (int)count/*FIXME cast*/, SocketFlags.None);
      return new Result<int> (result, null);
    }

    public Result<int> read (SockID sid, byte[] buf, uint count) {
      SockID_dotNET s = upcast_sock(sid);
      int result;
      try {
        result = s.base_socket.Receive(buf);
      } catch (SocketException e) {
        // FIXME inspect e.ErrorCode
        return new Result<int> (-1, Error.EBADF/*FIXME not sure if this is the right error code*/);
      }

      if (s.base_socket.Connected) {
        return new Result<int> (result, null);
      } else {
        return new Result<int> (-1, Error.EBADF/*FIXME not sure if this is the right error code*/);
      }
    }

    public Result<bool> close (SockID sid) {
      SockID_dotNET s = upcast_sock(sid);
      s.base_socket.Close();
      s.base_socket.Dispose();
      return new Result<bool> (true, null);
    }

    public SockID_dotNET upcast_sock (SockID sid) {
      if (sid is SockID_dotNET) {
        return (SockID_dotNET)sid;
      } else {
        throw new Exception("sid is not a SockID_dotNET");
      }
    }
}
