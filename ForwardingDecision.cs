/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

namespace Pax {

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

    // FIXME can use singleton to avoid multiple allocations of this?
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
  }
}
