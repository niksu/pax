This implements a network-based form of a [TCP
wrapper](https://en.wikipedia.org/wiki/TCP_Wrapper) as described in this
article at ftp://ftp.porcupine.org/pub/security/tcp_wrapper.pdf.
In the method described in the article, the wrapper consists of a process B that
pretends to be some other process A in order to intercept network messages
intended to be processed by A. For example, A could be fingerd, and B would log
network invocations of A, then itself invoke A to guile callers into thinking
that they were being serviced by A directly.

The network-based form implemented here involves watching on the network for
invocations of A, and then carrying out the logging behaviour.


## Improvements
* Currently we log by printing to the console. We could log over the network by sending [syslog](ftp://ftp.isi.edu/in-notes/rfc3164.txt) [messages](http://www.rfc-base.org/txt/rfc-5426.txt).

* Another feature involves checking if one of the communicating hosts is using a blacklisted address, and sending a RST packet to both hosts if so.

* Possible improvement: if we find that we're sending the same message frequently, then send summaries rather than discrete messages. For example, instead of logging message M four times, we could send a single packet that says "4M".
