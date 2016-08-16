/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

namespace Pax
{
  public static class PaxConfig_Lite {
    // This is the number of interfaces that Pax processor instances can
    // forward to. When running in a full-fledged .NET VM, this indicates the
    // number of network interfaces in the configuration file. When running
    // in a constrained environment, this can be set directly by the
    // system after loading (from some static value, or using a configuration
    // source that it knows of).
    // This must be greater than 1.
    // Note that no_interfaces may be larger than the number of interfaces to which a packet processor has
    // been attached (i.e., interfaces that have a "lead_handler" defined in the configuration).
    // But this is fine, because there might be interface for which we don't want to process
    // their incoming packets, but we want to be able to forward packets to them nonetheless.
    public static int no_interfaces; //FIXME make uint?

    // By "phantom forwarding" I mean forwarding to a network port
    // that doesn't exist (because it's greater than no_interfaces).
    // Phantom forwarding might occur if we are running a processor that has
    // been hardcoded to expect a number of network interfaces, but we lack
    // that number of interfaces in the configuration we're running it in.
    public static bool ignore_phantom_forwarding = false;

//#if LITE
    // We need static bounds on arrays, and these are
    // provided by such constants.
    public const uint MAX_PACKET_SIZE = 1500; // Maximum size of a packet in bytes.
    public const uint MAX_INTERFACES = 10; // Maximum number of interfaces we can use.
//#endif
  }
}
