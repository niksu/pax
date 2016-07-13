/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

namespace Pax
{
  public static class PaxConfig_Lite {
    // This is the number of interfaces in the configuration file.
    // This must be greater than 1.
    // Note that no_interfaces may be larger than the number of interfaces to which a packet processor has
    // been attached (i.e., interfaces that have a "lead_handler" defined in the configuration).
    // But this is fine, because there might be interface for which we don't want to process
    // their incoming packets, but we want to be able to forward packets to them nonetheless.
    public static int no_interfaces;
  }
}
