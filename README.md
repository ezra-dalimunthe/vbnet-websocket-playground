# vbnet-websocket-playground
fun project with vbnet as implementation of IETF RFC#6455 

## about
This repository contains several vbnet projects about implementation of websocket protocol as mentioned in [RFC 6455](https://tools.ietf.org/html/rfc6455)

### Folder /protocol

Application type: class library,  

* FrameDataProcessor class, contains function to parse websocket message, make data frame, build pong and ping message
* ServerWorker class, class to serve client request and response.

### Folder /echo-server
Application type: console application.

This is Echo server, simple server to broadcast message sent by a websocket client to all client connected to this server. server port is 1001. the port is hard-coded on purpose.

### Folder /simple-client
Application type:console application

Simple websocket client.

## How To Play
in folder /server, run the echo server with command

```
cd server
dotnet run
```
after the server running, try to send messages to this server. you can use following clients

* [websocket.org Echo Test - Powered by Kaazing](https://www.websocket.org/echo.html)
  * set location to ws://localhost:1001 . 
  * make a connection to server, 
  * send message and see the echoed message on log section
  * you can use some browser / tab to see the message

* run the client app ( folder /client )
  * wait until the connection established. 
  * type some message and press enter
  * server will replay with the message 
  
## To do next
There are some ideas to make use of this fun project, e.g: make chat server; 
