using System;
using System.Net;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  internal class ConnectionKey<T> : IEquatable<ConnectionKey<T>> where T : Packet
  {
    public Node<T> Source { get; }
    public Node<T> Destination { get; }
    private readonly int hashCode;
    #if DEBUG
    private string asString;
    #endif

    public ConnectionKey(Node<T> source, Node<T> destination)
    {
      if (ReferenceEquals(null, source)) throw new ArgumentNullException(nameof(source));
      if (ReferenceEquals(null, destination)) throw new ArgumentNullException(nameof(destination));

      Source = source;
      Destination = destination;
      // FIXME computing hash at construction relies on address being immutable (addr.Scope can change)
      hashCode = new { Source, Destination }.GetHashCode();
      #if DEBUG
      asString = this.ToString();
      #endif
    }

    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as ConnectionKey<T>);
    }

    public bool Equals(ConnectionKey<T> other)
    {
      return !ReferenceEquals(null, other)
        && Source.Equals(other.Source)
        && Destination.Equals(other.Destination);
    }

    public override int GetHashCode() { return hashCode; }

    // TODO: should all ToString overrides be conditional on target? (i.e. not on Kiwi)
    public override string ToString()
    {
#if DEBUG
      if (asString != null)
        return asString;
      else
#endif
      return $"{Source} to {Destination}";
    }
  }
}
