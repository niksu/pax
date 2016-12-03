/*
Berkeley sockets interface.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace Pax {

  // FIXME many constant values are excluded, e.g., various error codes.
  //       i only included the ones that i think i'll use in the prototype.
  enum Internet_Domain {AF_Inet};
  enum Internet_Type {Sock_Stream};
  enum Internet_Protocol {TCP};
  enum Error {EACCES, EADDRINUSE, EBADF, EINVAL, ENOTSOCK, EOPNOTSUPP, EINTR, EIO,
    EAGAIN, EWOULDBLOCK, EDESTADDRREQ, EDQUOT, EFAULT, EFBIG, ENOSP, EPERM, EPIPE};

  public class SockID {
    public SockID (uint sockid) {
      this.sockid = sockid;
    }

    public uint sockid {
      get;
      set;
    }
  }

  public class Result<T> {
    public readonly T result;
    public readonly string error;
    public Result (T result, string error) {
      this.result = result;
      this.error = error;
    }
  }

  // FIXME specialised this to work on IPv4
  public class SockAddr_In {
    public readonly int port;
    public readonly IPAddress address;
    public SockAddr_In (int port, IPAddress address) {
      this.port = port;
      this.address = address;
    }
    // FIXME specify equality?
  }

  /* FIXME right now I only care about servers, therefore don't include
     functions like "connect", "gethostbyname", and "gethostbyaddr" in the API.
     For simplicity, I don't include "getsockopt", "setsockopt", "select", and
     "poll" either.
     Also i exclude "sendto", "recvfrom", "send", and "recv", again for prototyping.
  */
  public interface IBerkeleySocket {
    SockID socket (Internet_Domain domain, Internet_Type type, Internet_Protocol prot);
    Result<bool> bind (SockID sid, SockAddr_In address);
    Result<bool> listen (SockID sid, uint backlog);
    Result<bool> accept (SockID sid, SockAddr_In address);
    Result<int> write (SockID sid, byte[] buf);
    Result<int> read (SockID sid, out byte[] buf, uint count);
    Result<bool> close (SockID sid);
  }
}
