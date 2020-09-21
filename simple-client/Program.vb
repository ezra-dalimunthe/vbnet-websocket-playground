Imports System
Imports System.IO
Imports HaloWebSocket
Imports System.Net.WebSockets
Imports System.Threading
Imports System.Collections.Concurrent
Class MainProgram
    Private Shared Host As String
    Private Shared TargetServer As Uri
    Private Shared WsSocket As ClientWebSocket
    Private Shared encoder As System.Text.UTF8Encoding
    Private Shared cst As CancellationTokenSource
    Public Shared Sub Main()

        Run().GetAwaiter().GetResult()

        Console.WriteLine(" -- end session ---")

        Console.ReadLine()
    End Sub
    Public Shared Async Function Run() As Task
        Host = "ws://localhost:1001"
        'Host = "ws://echo.websocket.org/"
        TargetServer = New Uri(Host)
        WsSocket = New ClientWebSocket
        WsSocket.Options.KeepAliveInterval = New TimeSpan(0, 1, 0) 'ping to server every 1 minute.
        WsSocket.Options.SetBuffer(4 * 1024, 4 * 1024)
        WsSocket.Options.UseDefaultCredentials = True
        encoder = New System.Text.UTF8Encoding
        cst = New CancellationTokenSource
        Dim cs = cst.Token
        Dim fnHandleError = Function(e As Exception)
                                Console.WriteLine(e.Message)
                                If e.InnerException IsNot Nothing Then
                                    Console.WriteLine(e.InnerException.Message)
                                End If

                                Return True
                            End Function
        Await WsSocket.ConnectAsync(TargetServer,
                                    CancellationToken.None).
                     ContinueWith(Sub(t)
                                      If t.Exception IsNot Nothing Then
                                          t.Exception.Handle(fnHandleError)
                                      End If
                                  End Sub)
        If WsSocket.State <> WebSocketState.Open Then
            Exit Function
        End If
        Console.WriteLine("connected!")

        Dim receiving = Receive(WsSocket, cs)
        Dim sending = SendLoop(WsSocket, cs)
        Await Task.WhenAll(sending, receiving).
            ContinueWith(Sub(t)
                             t.Exception.Handle(Function(e) True)
                         End Sub)

    End Function

    Private Shared Async Function Receive(ByVal socket As ClientWebSocket,
                                          ByVal cs As CancellationToken) As Task
        Const BUFFER_LENGTH = 4 * 1024
        Dim buffer(BUFFER_LENGTH) As Byte
        While True

            Dim result = Await socket.ReceiveAsync(New ArraySegment(Of Byte)(buffer), cs)
            If result.MessageType = WebSocketMessageType.Text Then
                Dim message = encoder.GetString(buffer, 0, result.Count)
                Console.WriteLine("receive: " & message)
            ElseIf result.MessageType = WebSocketMessageType.Close Then
                If socket.State = WebSocketState.Open Then
                    Await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)
                Else
                    Console.WriteLine("Disconnected {0} {1}", socket.CloseStatus, socket.CloseStatusDescription)
                End If

                Exit While
            Else

            End If

        End While
    End Function


    Private Shared Async Function SendLoop(ByVal socket As ClientWebSocket,
                                           ByVal cs As CancellationToken) As Task
        While True
            Dim input = Console.ReadLine()
            If String.IsNullOrEmpty(input) Then
                Exit While
            End If
            Dim bytemessages = encoder.GetBytes(input)
            Dim b2 = Convert.ToBase64String(bytemessages)
            Dim b3 = encoder.GetBytes(b2)
            Dim chunk = New ArraySegment(Of Byte)(b3)
            Await socket.SendAsync(New ArraySegment(Of Byte)(bytemessages),
                                   WebSocketMessageType.Text, True, CancellationToken.None)
        End While
        Await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)
        cst.Cancel(False)
    End Function
End Class
