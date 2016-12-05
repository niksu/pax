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
    private const string pax_fqn_prefix = "Pax.";

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
      // Get the underlying type if nullable (e.g. int?)
      if (ty.IsGenericType && ty.GetGenericTypeDefinition() == typeof(Nullable<>))
        ty = Nullable.GetUnderlyingType(ty);

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
        if (intf.FullName.StartsWith(pax_fqn_prefix))
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
        if (type.FullName.StartsWith(pax_fqn_prefix))
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
        ctor.GetParameters().All(p => parameterIsAvailable(p) || p.IsOptional);

      // Get the constructors for this type that we could call with the given arguments:
      var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                             .Where(constructorCanBeCalled);

      // Try to instantiate the type, using the best constructor we can:
      // Prefer constructors where we can provide all the parameters to those where some will be their default values
      var sortedConstructors = constructors.OrderByDescending(ctor => ctor.GetParameters().Length)
                                           .ThenByDescending(ctor => ctor.GetParameters().Count(p => !p.IsOptional));
      foreach (var constructor in sortedConstructors)
      {
        try
        {
          // Get the arguments for the constructor, converted to the proper types
          var arguments = GetArgumentsForConstructor(constructor, argsDict).ToArray();
          // Invoke the constructor, instantiating the type
#if MOREDEBUG
          Console.WriteLine("Invoking new {0}", ConstructorString(constructor, arguments));
#endif
          IPacketProcessor pp = (IPacketProcessor)constructor.Invoke(arguments);
          return pp;
        }
        catch (Exception ex) when (ex is InvalidCastException
                                || ex is FormatException
                                || ex is KeyNotFoundException)
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

    private static IEnumerable<object> GetArgumentsForConstructor(ConstructorInfo ctor, IDictionary<string,string> argsDict)
    {
      foreach (var param in ctor.GetParameters())
      {
        if (argsDict.ContainsKey(param.Name))
        {
          object converted = null;
          string argString = argsDict[param.Name];
          bool successful = false;
          try
          {
            // Convert to the desired parameter type from the config string
            converted = ConvertConstructorParameter(param.ParameterType, argString);
            successful = true;
          }
          catch (Exception ex) when (ex is FormatException || ex is InvalidCastException)
          {
            // We couldn't convert this value. Log it, and then carry on, in case it is optional anyway
            Debug.WriteLine("Couldn't convert string \"{0}\" to type {1}", argString, param.ParameterType.FullName);
          }
          if (successful)
          {
            yield return converted;
            continue;
          }
        }

        if (param.IsOptional)
          // Allow optional parameters to be missing
          yield return Type.Missing;
        else
          // We cannot provide the argument value - throw an error
          throw new KeyNotFoundException();
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
