/*
Pax : tool support for prototyping packet processors
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// Lookup key for maps mapping Packets from the inside to the outside of a NAT or vice versa.
  /// </summary>
  internal sealed class ConnectionKey : IEquatable<ConnectionKey>
  {
    /// <summary>
    /// The source node.
    /// </summary>
    public Node Source { get; }

    /// <summary>
    /// The destination node.
    /// </summary>
    public Node Destination { get; }

    private readonly int hashCode;

    /// <param name="source">The source node.</param>
    /// <param name="destination">The destination node.</param>
    public ConnectionKey(Node source, Node destination)
    {
      if (ReferenceEquals(null, source)) throw new ArgumentNullException(nameof(source));
      if (ReferenceEquals(null, destination)) throw new ArgumentNullException(nameof(destination));

      Source = source;
      Destination = destination;

      hashCode = new { Source, Destination }.GetHashCode();
    }

    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as ConnectionKey);
    }

    public bool Equals(ConnectionKey other)
    {
      return !ReferenceEquals(null, other)
        && Source.Equals(other.Source)
        && Destination.Equals(other.Destination);
    }

    public override int GetHashCode() { return hashCode; }

    public override string ToString()
    {
      return String.Format("{0} to {1}", Source, Destination);
    }
  }
}
