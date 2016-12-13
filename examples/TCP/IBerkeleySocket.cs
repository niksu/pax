/*
Berkeley sockets interface.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using Pax;

namespace Pax_TCP {

  public enum Unit { Value };

  // FIXME many constant values are excluded, e.g., various error codes.
  //       i only included the ones that i think i'll use in the prototype.
  public enum Internet_Domain {AF_Inet};
  public enum Internet_Type {Sock_Stream};
  public enum Internet_Protocol {TCP};
  public enum Error {EACCES, EADDRINUSE, EBADF, EINVAL, ENOTSOCK, EOPNOTSUPP, EINTR, EIO,
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
    public readonly bool erroneous = false;
    public readonly Error? error;

    public Result (T result, Error? error) {
      this.result = result;
      this.error = error;
      if (error != null) {
        erroneous = true;
      }
    }

    public T value_exc () {
      if (erroneous) {
        throw new Exception(this.ToString());
      }

      return result;
    }

    override public string ToString() {
      string result;

      if (error == null) {
        return this.result.ToString();
      }

      switch (error) {
        case Error.EACCES:
          result = "EACCESS";
          break;
        case Error.EADDRINUSE:
          result = "EADDRINUSE";
          break;
        case Error.EBADF:
          result = "EBADF";
          break;
        case Error.EINVAL:
          result = "EINVAL";
          break;
        case Error.ENOTSOCK:
          result = "ENOTSOCK";
          break;
        case Error.EOPNOTSUPP:
          result = "EOPNOTSUPP";
          break;
        case Error.EINTR:
          result = "EINTR";
          break;
        case Error.EIO:
          result = "EIO";
          break;
        case Error.EAGAIN:
          result = "EAGAIN";
          break;
        case Error.EWOULDBLOCK:
          result = "EWOULDBLOCK";
          break;
        case Error.EDESTADDRREQ:
          result = "EDESTADDRREQ";
          break;
        case Error.EDQUOT:
          result = "EDQUOT";
          break;
        case Error.EFAULT:
          result = "EFAULT";
          break;
        case Error.EFBIG:
          result = "EFBIG";
          break;
        case Error.ENOSP:
          result = "ENOSP";
          break;
        case Error.EPERM:
          result = "EPERM";
          break;
        case Error.EPIPE:
          result = "EPIPE";
          break;
        default:
          result = "(Unknown error)";
          break;
      }

      return result;
    }
  }

  // FIXME specialised this to work on IPv4
  public class SockAddr_In {
    public readonly ushort port;
    public readonly IPAddress address;
    public SockAddr_In (ushort port, IPAddress address) {
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
    Result<SockID> socket (Internet_Domain domain, Internet_Type type, Internet_Protocol prot);
    Result<Unit> bind (SockID sid, SockAddr_In address);
    Result<Unit> listen (SockID sid); // NOTE we let the "backlog" parameter be implicit for prototyping reasons; it's a parameter of TCP, not the interface.
    Result<SockID> accept (SockID sid, out SockAddr_In address);
    Result<int> write (SockID sid, byte[] buf, uint count);
    Result<int> read (SockID sid, byte[] buf, uint count);
    Result<Unit> close (SockID sid);
  }

  public interface IActiveBerkeleySocket: IBerkeleySocket, IActive {}
}
