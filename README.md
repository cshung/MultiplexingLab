# MultiplexingLab

Experimental code to build multiple streams in a single TCP connection.
The library allow both server and client to actively open a stream - so we can do server push.
Attached are a few examples that do a simple TCP echo server, and a TCP echo server on Server Push mode.

A detailed design for this is documented in my blog [here](http://andrew-technical.blogspot.com/2014/06/homemade-internet-service-relay-i.html)!