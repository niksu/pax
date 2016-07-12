/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using PacketDotNet;
using System.Reflection;
using System.Linq;
using System.Diagnostics;

namespace Pax {

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

    public static IPacketProcessor InstantiatePacketProcessor(Type type, IDictionary<string, string> argsDict)
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
          IPacketProcessor pp = (IPacketProcessor)constructor.Invoke(arguments);
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
