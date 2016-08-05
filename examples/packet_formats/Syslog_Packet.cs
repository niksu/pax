/*
Syslog packet format
Nik Sultana, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Text;
using System.Diagnostics;
using PacketDotNet;
using PacketDotNet.Utils;
using MiscUtil.Conversion;
using System.Text.RegularExpressions;


// FIXME currently i don't implement the full spec at https://tools.ietf.org/html/rfc5424#section-6
// NOTE there are various problems with syslog (see e.g., http://campin.net/syslog-ng/syslog.html)
//      so it might make more sense to use a different logging protocol?
public class Syslog_Packet : Packet {

  // The following fields keep track of the parsed representation is consistent with the header buffer.
  // This helps us know whether the contents of the buffer (holding the packet)
  // have been projected into fields (so we don't re-project the fields),
  // and whether the fields contents have (subsequently) been changed (so we
  // don't update the buffer unless necessary).
  private bool parsed = false;  // The buffer's contents have been parsed into fields. Initially this is false.
  private bool modified = false; // The parsed representation has been changed since parsing. Initially this is false.
  // From the RFC:
  // "The PRI part MUST have three, four, or five characters and will be
  //  bound with angle brackets as the first and last characters.  The PRI
  //  part starts with a leading "<" ('less-than' character), followed by a
  //  number, which is followed by a ">" ('greater-than' character).
  protected byte[] pri;
  // From the RFC:
  // - "The TIMESTAMP will immediately follow the trailing ">" from the PRI
  //    part and single space characters MUST follow each of the TIMESTAMP
  //    and HOSTNAME fields."
  // - "The TIMESTAMP field [...] is in the format of "Mmm dd hh:mm:ss""
  protected byte[] timestamp;
  // From the RFC:
  // "The HOSTNAME field will contain only the hostname, the IPv4 address,
  //  or the IPv6 address of the originator of the message.  The preferred
  //  value is the hostname."
  protected byte[] hostname;
  // From the RFC: "The MSG part will fill the remainder of the syslog packet."
  protected byte[] message;

  // Syslog uses ASCII-encoded text strings.
  private System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();

  // FIXME where should dissect() be called? from the ctor?
  // Extract bytes from the header and organise them into separate fields.
  // NOTE we don't do detailed dissecting or checking -- for instance, syslog
  //      messages are supposed to be given in a single line, but we don't check
  //      for \n in the message, or check that fields (e.g., date format) are
  //      wellformed.
  // Our main "parsing" method consists of looking for tokens such as brackets
  // and spaces.
  protected void dissect()
  {
    string pri_s;
    string timestamp_s;
    string hostname_s;
    string message_s;

    // FIXME this code is far from efficient
    var header_s = encoding.GetString(Header).TrimEnd('\0');
    // Regex syntax is described at https://msdn.microsoft.com/en-us/library/az24scfc(v=vs.110).aspx
    Regex regex = new Regex(@"^(<\d+>)(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec  ?\d\d? \d\d:\d\d:\d\d) (\w+) (\w+)$", RegexOptions.Compiled);

    Match m = regex.Match(header_s);
    if (m.Success) {
      GroupCollection gs = m.Groups;
      pri_s = gs[0].Value;
      timestamp_s = gs[1].Value;
      hostname_s = gs[2].Value;
      message_s = gs[3].Value;
    } else {
      throw (new Exception("Could not parse: " + header_s));
    }

    // We only expect a single match.
    Debug.Assert(!m.NextMatch().Success);

    pri = encoding.GetBytes(pri_s);
    timestamp = encoding.GetBytes(timestamp_s);
    hostname = encoding.GetBytes(hostname_s);
    message = encoding.GetBytes(message_s);
  }

  // This is the inverse of dissect.
  // We postpone updating the header's contents until it's actually needed.
  override public byte[] Bytes {
    get
    {
      // Append all the fields together, call base class' method (since that of Packet contains some useful routines) and return.
      if (modified) {
        byte[] pre_header = new byte[pri.Length + timestamp.Length + hostname.Length +
          message.Length];
        pri.CopyTo(pre_header, 0/*Offset we write to is initially 0*/);
        timestamp.CopyTo(pre_header, pri.Length);
        hostname.CopyTo(pre_header, pri.Length + timestamp.Length);
        message.CopyTo(pre_header, pri.Length + timestamp.Length + hostname.Length);

        header = new ByteArraySegment(pre_header);
        modified = false;
      }

      return base.Bytes;
    }
  }

  public byte[] Pri
  {
   get {
     if (!parsed) {
       dissect();
       parsed = true;
     }

     return pri;
   }

   set {
     if (!parsed) {
       dissect();
       parsed = true;
     }

     pri = value;
     modified = true;
   }
  }

  // FIXME return DateTime rather than string
  public string Timestamp
  {
   get {
     // FIXME this portion of code is repeated in other accessors
     if (!parsed) {
       dissect();
       parsed = true;
     }

     return encoding.GetString(timestamp);
   }

   set {
     // FIXME this portion of code is repeated in other accessors
     if (!parsed) {
       dissect();
       parsed = true;
     }

     timestamp = encoding.GetBytes(value);
     modified = true;
   }
  }

  public string Hostname
  {
   get {
     // FIXME this portion of code is repeated in other accessors
     if (!parsed) {
       dissect();
       parsed = true;
     }

     return encoding.GetString(hostname);
   }

   set {
     // FIXME this portion of code is repeated in other accessors
     if (!parsed) {
       dissect();
       parsed = true;
     }

     hostname = encoding.GetBytes(value);
     modified = true;
   }
  }

  public string Message
  {
   get {
     // FIXME this portion of code is repeated in other accessors
     if (!parsed) {
       dissect();
       parsed = true;
     }

     return encoding.GetString(message);
   }

   set {
     // FIXME this portion of code is repeated in other accessors
     if (!parsed) {
       dissect();
       parsed = true;
     }

     message = encoding.GetBytes(value);
     modified = true;
   }
  }
}
