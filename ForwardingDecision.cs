/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

namespace Pax {

  // FIXME should there be a Lite version of this class? I think specifically
  //       it would statically dimension the MultiPortForward's array, and
  //       perhaps have it write directly to a field in Packet_Buffer.
  /// <summary>
  /// A packet processor makes some sort of forwarding decision, in addition
  /// to possibly analysing/modifying the packet (and generating new packets).
  /// </summary>
  /// <remark>
  /// The forwarding decision makes clear whether the processor recommends to
  /// * drop the packet (i.e., nothing more is to be done with it)
  /// * forward to a single port
  /// * forward to multiple ports
  ///
  /// NOTE this idiom simulates using algebraic types in C#. It's values are
  ///      analysed using the Visitor pattern.
  /// </remark>
  /// <remark>
  /// Note that there is a hierarchy between forwarding decisions: Drop is
  /// subsumed by SinglePortForward (which allows forwarding to at most one
  /// port), which is subsumed by MultiPortForward (which allows forwarding to
  /// any number of ports, including one port, or none).
  /// </remark>
  public abstract class ForwardingDecision {
    private ForwardingDecision() {}

    /// <summary>
    /// The decision to drop the packet.
    /// </summary>
    public sealed class Drop : ForwardingDecision {
      // We only need to keep a single instance of Drop, so we use a singleton pattern.
      private Drop () {}
      public static Drop Instance { get; } = new Drop();
    }

    /// <summary>
    /// The decision to forward the packet to *at most* a single port.
    /// Note that a processor that forwards to a single port might still
    /// decide to drop packets, even if it returns a <c>ForwardingDecision</c>
    /// of type <c>SinglePortForward</c>. A drop decision within
    /// <c>SinglePortForward</c> is communicated by having a negative
    /// "target_port" parameter to the constructor.
    /// </summary>
    public sealed class SinglePortForward : ForwardingDecision {
      public readonly int target_port;

      public SinglePortForward (int target_port) {
        this.target_port = target_port;
      }
    }

    /// <summary>
    /// The decision to forward the packet to any number of ports.
    /// Note that a processor typed <c>MultiPortForward</c> may still
    /// decide to drop packets. A drop decision within
    /// <c>MultiPortForward</c> is communicated by having an empty
    /// "target_ports" parameter to the constructor.
    /// </summary>
    public sealed class MultiPortForward : ForwardingDecision {
      public readonly int[] target_ports;

      public MultiPortForward (int[] target_ports) {
        this.target_ports = target_ports;
      }
    }

    public static int[] broadcast_raw (int in_port)
    {
      int[] out_ports = new int[PaxConfig_Lite.no_interfaces - 1];
      // We retrieve number of interfaces in use from PaxConfig_Lite.
      // Naturally, we exclude in_port from the interfaces we're forwarding to since this is a broadcast.
      int idx = 0;
      for (int ofs = 0; ofs < PaxConfig_Lite.no_interfaces; ofs++)
      {
        if (ofs != in_port)
        {
          out_ports[idx] = ofs;
          idx++;
        }
      }
      return out_ports;
    }

    public static MultiPortForward broadcast (int in_port)
    {
      return (new MultiPortForward (broadcast_raw (in_port)));
    }
  }
}
