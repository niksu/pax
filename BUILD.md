# Building

## Dependencies
1. libpcap (on UNIXy systems) or winpcap.dll (on Windows).
  On Ubuntu: `sudo apt-get install libpcap0.8`
2. C# (version >=6.0) compiler and runtime -- we usually use Mono, but
  Microsoft's should work fine too.
  On Ubuntu Linux (tested with 14.04 and 16.04) it's easiest to follow Mono's
  [instructions](http://www.mono-project.com/docs/getting-started/install/linux/)
  to install the latest version, and run `apt-get install mono-complete`.
  You might need to tweak [lib/SharpPcap.dll.config](lib/SharpPcap.dll.config) in
  order to direct Mono's loader to the right location of libpcap on your system,
  for example by updating the path of libpcap to `<dllmap dll="wpcap" target="/usr/lib/x86_64-linux-gnu/libpcap.so.0.8" os="linux" />`).
  Alternatively you could add a symbolic link to your libpcap library, or modify
  the system-wide Mono config (usually at `/etc/mono/config`) to add a
  [dllmap](http://www.mono-project.com/docs/advanced/pinvoke/dllmap/) entry.
3. Put the DLLs for the following libraries in Pax's `lib/` directory:
  * [SharpPcap](https://github.com/chmorgan/sharppcap)
  * [PacketDotNet](https://github.com/chmorgan/packetnet)
  * Newtonsoft's JSON library (download one of the [releases](https://github.com/JamesNK/Newtonsoft.Json/releases))

Optionaly install Mininet for [testing](examples#testing-the-nat-with-mininet).
We used Mininet 2.2.1 on Ubuntu 14.04 LTS.


## Compiling
Run the `build.sh` script in Pax's directory and everything should go smoothly.
This will produce a single file (Pax.exe), called an *assembly* in .NET jargon.
This assembly serves two purposes:
* It is the tool that runs your packet processors using a configuration you provide.
* It is the library that your packet processors reference. This reference will be checked by the .NET compiler when compiling your code.
