/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

namespace Pax {

  // A packet processor makes some sort of forwarding decision, in addition
  // to possibly analysing/modifying the packet (and generating new packets).
  // The forwarding decision makes clear whether the processor recommends to
  // * drop the packet (i.e., nothing more is to be done with it)
  // * forward to a single port
  // * forward to multiple ports
  //
  // NOTE this idiom simulates using algebraic types in C#. It's values are
  //      analysed using the Visitor pattern.
  public abstract class ForwardingDecision {
    private ForwardingDecision() {}

    // FIXME can use singleton to avoid multiple allocations of this?
    public sealed class Drop : ForwardingDecision {
      public Drop () {}
    }

    public sealed class SinglePortForward : ForwardingDecision {
      public readonly int target_port;

      public SinglePortForward (int target_port) {
        this.target_port = target_port;
      }
    }

    public sealed class MultiPortForward : ForwardingDecision {
      public readonly int[] target_ports;

      public MultiPortForward (int[] target_ports) {
        this.target_ports = target_ports;
      }
    }
  }
}
