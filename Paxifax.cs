/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using System.Collections;
using System.Collections.Generic;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;

namespace Pax {

  // FIXME use javadoc-style comments to describe the API
  public interface PacketProcessor {
    void packetHandler (object sender, CaptureEventArgs e);
  }

  internal static class PacketProcessorHelper
  {
    public readonly static Type[] AllowedConstructorParameterTypes = new Type[]
      {
        typeof(Boolean),  typeof(Byte),   typeof(SByte),  typeof(UInt16),   typeof(Int16),
        typeof(UInt32),   typeof(Int32),  typeof(UInt64), typeof(Int64),    typeof(Decimal),
        typeof(Single),   typeof(Double), typeof(String), typeof(DateTime),
        typeof(TimeSpan), typeof(System.Net.IPAddress), typeof(PhysicalAddress)
      };

    public static bool IsAllowedConstructorParameterType(Type ty)
    {
      // Allow nullable types
      if (ty.IsGenericType && ty.GetGenericTypeDefinition() == typeof(Nullable<>))
        ty = Nullable.GetUnderlyingType(ty);
      return AllowedConstructorParameterTypes.Contains(ty);
    }

    public static object ConvertConstructorParameter(Type ty, string s)
    {
      if (ty == typeof(string))
        return s;
      else if (ty == typeof(System.Net.IPAddress))
        return System.Net.IPAddress.Parse(s);
      else if (ty == typeof(PhysicalAddress))
        return PhysicalAddress.Parse(s.ToUpper().Replace(':', '-'));
      else if (ty == typeof(TimeSpan))
        return TimeSpan.Parse(s);
      else
        // Convert to primitives + DateTime
        return ((IConvertible)s).ToType(ty, System.Globalization.CultureInfo.CurrentCulture); // NOTE throws InvalidCastException
    }

    public static string ConstructorString(ConstructorInfo constructor, object[] arguments = null)
    {
      IEnumerable<string> parameters;
      // Get the string representations of the parameters (or arguments if present)
      if (arguments == null)
        parameters = constructor.GetParameters()
                                .Select(p => String.Format("{0}: {1}", p.Name, p.ParameterType.FullName));
      else
        parameters = arguments.Select(obj => obj.ToString());
      // Format nicely so it looks like a constructor
      return String.Format("{0}({1})", constructor.DeclaringType.Name, String.Join(", ", parameters));
    }

    public static IEnumerable<Type> GetUsedPaxTypes(Type type)
    {
      // Yield implemented Pax interfaces
      foreach (Type intf in type.GetInterfaces())
      {
        if (intf.FullName.StartsWith("Pax."))
          yield return intf;
        else
        {
          // Check for interfaces lower down
          foreach (Type subintf in GetUsedPaxTypes(intf))
            yield return subintf;
        }
      }
      
      // Yield the highest-up extended Pax type
      type = type.BaseType;
      while (type != null)
      {
        if (type.FullName.StartsWith("Pax."))
        {
          yield return type;
          yield break;
        }
        type = type.BaseType;
      }
    }

    public static PacketProcessor InstantiatePacketProcessor(Type type, IDictionary<string, string> argsDict)
    {
      // Predicate determining if a parameter could be provided
      Func<ParameterInfo,bool> parameterIsAvailable = param =>
        argsDict.ContainsKey(param.Name) && IsAllowedConstructorParameterType(param.ParameterType);
      // Predicate determining if a constructor can be called
      Func<ConstructorInfo,bool> constructorCanBeCalled = ctor =>
        ctor.GetParameters().All(parameterIsAvailable);

      // Get the constructors for this type that we could call with the given arguments:
      var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                             .Where(constructorCanBeCalled);

      // Try to instantiate the type, using the best constructor we can:
      foreach (var constructor in constructors.OrderByDescending(ctor => ctor.GetParameters().Length))
      {
        try
        {
          // Get the arguments for the constructor, converted to the proper types
          var arguments =
            constructor.GetParameters()
                       .Select(p => ConvertConstructorParameter(p.ParameterType, argsDict[p.Name]))
                       .ToArray();
          // Invoke the constructor, instantiating the type
#if MOREDEBUG
          Console.WriteLine("Invoking new {0}", ConstructorString(constructor, arguments));
#endif
          PacketProcessor pp = (PacketProcessor)constructor.Invoke(arguments);
          return pp;
        }
        catch (Exception ex) when (ex is InvalidCastException
                                || ex is FormatException)
        {
          // If an exception is thrown, ignore it and try the next best constructor
          // But log it first:
          Debug.WriteLine("Constructor failed - {0}:", constructor.ToString());
          Debug.WriteLine(ex);
        }
      }

      // If we reach this point, there were no constructors that we could use
      Console.WriteLine("No suitable constructor could be found.");
      return null;
    }
  }

  // A packet monitor does not output anything onto the network, it simply
  // accumulates state based on what it observes happening on the network.
  // It might produce output on side-channels, through side-effects.
  // This could be used for diagnosis, to observe network activity and print
  // digests to the console or log.
  public abstract class PacketMonitor : PacketProcessor {
    abstract public void handler (int in_port, Packet packet);

    public void packetHandler (object sender, CaptureEventArgs e)
    {
      var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
      int in_port = PaxConfig.rdeviceMap[e.Device.Name];

      handler (in_port, packet);
#if DEBUG
      // FIXME could append name of the class in the debug message, so we know which
      //       packet processor is being used.
      Debug.Write(PaxConfig.deviceMap[in_port].Name + " -|");
#endif
    }
  }

  // Simple packet processor: it can only transform the given packet and forward it to at most one interface.
  public abstract class SimplePacketProcessor : PacketProcessor {
    // Return the offset of network interface that "packet" is to be forwarded to.
    abstract public int handler (int in_port, ref Packet packet);

    public void packetHandler (object sender, CaptureEventArgs e)
    {
      var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
      int in_port = PaxConfig.rdeviceMap[e.Device.Name];

      int out_port = handler (in_port, ref packet);
#if DEBUG
      Debug.Write(PaxConfig.deviceMap[in_port].Name + " -1> ");
#endif
      if (out_port > -1)
      {
        var device = PaxConfig.deviceMap[out_port];
        if (packet is EthernetPacket)
          ((EthernetPacket)packet).SourceHwAddress = device.MacAddress;
        device.SendPacket(packet);
#if DEBUG
        Debug.WriteLine(PaxConfig.deviceMap[out_port].Name);
      } else {
        Debug.WriteLine("<dropped>");
#endif
      }
    }
  }

  // Simple packet processor that can forward to multiple interfaces. It is "simple" because
  // it can only transform the given packet, and cannot generate new ones.
  public abstract class MultiInterface_SimplePacketProcessor : PacketProcessor {
    // Return the offsets of network interfaces that "packet" is to be forwarded to.
    abstract public int[] handler (int in_port, ref Packet packet);

    public static int[] broadcast (int in_port)
    {
      int[] out_ports = new int[PaxConfig.no_interfaces - 1];
      // We retrieve number of interfaces in use from PaxConfig.
      // Naturally, we exclude in_port from the interfaces we're forwarding to since this is a broadcast.
      int idx = 0;
      for (int ofs = 0; ofs < PaxConfig.no_interfaces; ofs++)
      {
        if (ofs != in_port)
        {
          out_ports[idx] = ofs;
          idx++;
        }
      }
      return out_ports;
    }

    public void packetHandler (object sender, CaptureEventArgs e)
    {
      var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
      int in_port = PaxConfig.rdeviceMap[e.Device.Name];

      int[] out_ports = handler (in_port, ref packet);
#if DEBUG
      Debug.Write(PaxConfig.deviceMap[in_port].Name + " -> ");
#endif
      for (int idx = 0; idx < out_ports.Length; idx++)
      {
        PaxConfig.deviceMap[out_ports[idx]].SendPacket(packet);
#if DEBUG
        if (idx < out_ports.Length - 1)
        {
          Debug.Write(PaxConfig.deviceMap[out_ports[idx]].Name + ", ");
        } else {
          Debug.Write(PaxConfig.deviceMap[out_ports[idx]].Name);
        }
#endif
      }

#if DEBUG
      Debug.WriteLine("");
#endif
    }
  }

  public class PacketProcessor_Chain : PacketProcessor {
    List<PacketProcessor> chain;

    public PacketProcessor_Chain (List<PacketProcessor> chain) {
      this.chain = chain;
    }

    public void packetHandler (object sender, CaptureEventArgs e) {
      foreach (PacketProcessor pp in chain) {
        pp.packetHandler (sender, e);
      }
    }
  }

  public static class PacketEncap {
    public static bool Encapsulates (this Packet p, params Type[] encs) {
      if (encs.Length > 0)
      {
        if (p.PayloadPacket == null)
        {
          // "p" doesn't encapsulate whatever it is that "encs" asks it to,
          // since "p" doesn't encapsulate anything.
          return false;
        } else {
          if (encs[0].IsAssignableFrom(p.PayloadPacket.GetType())) {
            return p.PayloadPacket.Encapsulates(encs.Skip(1).ToArray());
          } else {
            return false;
          }
        }
      } else {
        return true;
      }
    }
  }
}
