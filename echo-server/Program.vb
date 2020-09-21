Imports System
Imports WebSocketEchoServer
Imports HaloWebSocket
Class Program
    Private Shared WithEvents WsServer As WebSocketServer

    Shared Sub Main(args As String())

        StartWebSocketServer()

    End Sub

   
    Public Shared Sub StartWebSocketServer()
        Try
            Dim serverport As Integer = 1001
            WsServer = New WebSocketServer("0.0.0.0", serverport)

            Console.WriteLine("Server started at port {0}", serverport)
            Console.WriteLine("Press any key to close the server")
            WsServer.Start()
            Console.ReadLine()
           
        Catch ex As Exception
            Console.WriteLine("Error occured: " & ex.Message)
            Console.WriteLine(ex.StackTrace)
        finally
            WsServer.Stop()
        End Try
    
    End Sub


    Private Shared Sub WsServer_OnErrorOccured(sender As Object, message As String) Handles WsServer.OnErrorOccured
        Console.WriteLine("Error occured:" & message)
    End Sub
End Class
