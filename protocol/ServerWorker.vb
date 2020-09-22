Imports System.Net.Sockets
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.IO
Public Class ServerWorker
    Private Property webroot As String
    Dim _TcpClient As System.Net.Sockets.TcpClient
    Dim _ClientID As String
    Dim _ClientIP As String
    Private processor As FrameDataProcessor
    Private dct As Dictionary(Of String, String)
    Public ReadOnly Property ClientId As String
        Get
            Return _ClientID
        End Get
    End Property

    ReadOnly Property ClientIP As String
        Get
            Return _ClientIP
        End Get
    End Property

    Public Delegate Sub OnClientDisconnectDelegateHandler(ByVal ClientId As String)
    Public Event onClientDisconnect As OnClientDisconnectDelegateHandler
    Public Delegate Sub OnClientDataAvailableDelegateHandler(ByVal Sender As ServerWorker, ByVal e As IncomingData)
    Public Event onClientDataAvailable As OnClientDataAvailableDelegateHandler

    Sub New(ByVal tcpClient As System.Net.Sockets.TcpClient)
        Dim l As New LingerOption(True, 10)
        processor = New FrameDataProcessor
        tcpClient.Client.LingerState = l
        Me._TcpClient = tcpClient
        _ClientIP = DirectCast(Me._TcpClient.Client.RemoteEndPoint, IPEndPoint).Address.ToString()
        webroot = "./webroot"
        populateMimeType()
    End Sub


    Function isConnected() As Boolean
        Return Me._TcpClient.Client.Connected
    End Function
    Sub CloseConnection(Optional ByVal code As UInt16 = 1000, Optional ByVal reason As String = "")
        Dim encoder = New Text.UTF8Encoding
        Dim ls As New List(Of Byte)
        Dim bcode = BitConverter.GetBytes(code)
        Array.Reverse(bcode)
        ls.AddRange(bcode)
        Dim msg = encoder.GetBytes(reason)
        ls.AddRange(msg)
        Dim frame = processor.MakeFrame(ls.ToArray, WsOpcode.ConnectionClose)
        WriteStream(frame)
        If Me.isConnected Then
            Me._TcpClient.Client.Disconnect(False)
            Me._TcpClient.Client.Close()
            Me._TcpClient.Close()
            DirectCast(Me._TcpClient, IDisposable).Dispose()
        End If
    End Sub

    Public Sub HandShake()
        Dim stream As NetworkStream = Me._TcpClient.GetStream()
        Dim bytes As Byte() = Nothing
        Dim HandshakeHeader As String = String.Empty


        If Me._TcpClient.Connected Then
            While (stream.CanRead AndAlso stream.DataAvailable)
                ReDim bytes(Me._TcpClient.Client.Available)
                Dim readREsult = stream.Read(bytes, 0, bytes.Length)
            End While
            If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                HandshakeHeader = System.Text.Encoding.UTF8.GetString(bytes)
            End If


            Dim isGetMethod = New Text.RegularExpressions.Regex("^GET").IsMatch(HandshakeHeader)
            Dim isWebSocketRequest = New Text.RegularExpressions.Regex("Upgrade: (.*)").Match(HandshakeHeader).
                Groups(1).Value().Trim().Equals("websocket", StringComparison.InvariantCultureIgnoreCase)

            If isGetMethod AndAlso isWebSocketRequest Then
                _ClientID = New Regex("Sec-WebSocket-Key: (.*)").Match(HandshakeHeader).Groups(1).Value.Trim()

                Dim keystring = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                Dim bytekeystring = Encoding.UTF8.GetBytes(_ClientID & keystring)

                Dim secwebsocketaccept = System.Security.Cryptography.SHA1.Create().
                    ComputeHash(bytekeystring)

                Dim sb As New StringBuilder

                sb.AppendLine("HTTP/1.1 101 Switching Protocols")
                sb.AppendLine("Upgrade: websocket")
                sb.AppendLine("Connection: Upgrade")

                sb.Append("Sec-WebSocket-Accept: ")
                sb.AppendLine(Convert.ToBase64String(secwebsocketaccept))
                sb.AppendLine(String.Format("Date: {0:r}", Date.Now.ToUniversalTime()))
                sb.AppendLine()


                Dim response As Byte() = Encoding.UTF8.GetBytes(sb.ToString())

                WriteStream(response)
            Else
                'We're going to disconnect the client here, because the client's not handshaking properly 
                'simple html server to serve http GET request
                Dim rgx = New Text.RegularExpressions.Regex("GET\s/(?<qs>.*)\sHTTP")
                Dim m = rgx.Matches(HandshakeHeader)
                If m.Count > 0 Then

                    Dim filepath = m(0).Groups("qs").Value
                    If String.IsNullOrEmpty(filepath) Then
                        filepath = "index.html"
                    End If
                    Dim fullpath = Path.GetFullPath(Path.Join(webroot, filepath))
                    Dim htmlContent As New StringBuilder
                    Dim htmlHeader As New StringBuilder
                    Dim content As Byte()

                    If File.Exists(fullpath) Then
                        htmlHeader.AppendLine("HTTP/1.1 200 OK")
                        content = File.ReadAllBytes(fullpath)

                        Dim fileextension = Path.GetExtension(fullpath)
                        Dim mimetype As String = String.Empty

                        dct.TryGetValue(fileextension, mimetype)
                        If mimetype Is Nothing Then
                            mimetype = "text/plain"
                        End If

                        htmlHeader.AppendLine("Content-type: " & mimetype)
                        htmlHeader.AppendLine("Content-length: " & content.Count.ToString())
                        htmlHeader.AppendLine(String.Format("Date: {0:r}", Date.Now.ToUniversalTime()))
                        htmlHeader.AppendLine(String.Format("expires: {0:r}", Date.Now.AddDays(-1).ToUniversalTime()))

                        htmlHeader.AppendLine()
                        htmlContent.Append(content)
                    Else
                        htmlHeader.AppendLine("HTTP/1.1 404 NOT FOUND")
                    End If





                    WriteStream(Encoding.UTF8.GetBytes(htmlHeader.ToString()))
                    If content IsNot Nothing Then
                        WriteStream(content)
                    End If



                End If

                stream.Close()
                Me._TcpClient.Close()
            End If

        End If

        If Me._TcpClient.Connected = False Then
            RaiseEvent onClientDisconnect(Me.ClientId)
        End If

    End Sub
    Sub WriteStream(ByVal b As Byte())
        If Me.isConnected Then
            Try
                Dim stream As NetworkStream = Me._TcpClient.GetStream()

                If stream.CanWrite Then
                    stream.Write(b, 0, b.Length)
                End If
                stream.Flush()

            Catch ex As Exception

                Console.WriteLine("Error when writestream: " & ex.Message)
            End Try


        End If
    End Sub
    Sub CheckForDataAvailability()

        Dim stream As NetworkStream = Me._TcpClient.GetStream()
        If (stream.DataAvailable AndAlso stream.CanRead) Then

            Dim streamData(Me._TcpClient.Client.Available) As Byte
            stream.Read(streamData, 0, streamData.Length - 1) 'Read the stream, don't close it..

            Dim frame = processor.ProcessIncomingData(streamData)


            Select Case frame.OpCode
                Case Is = WsOpcode.Text
                    RaiseEvent onClientDataAvailable(Me, frame)
                Case Is = WsOpcode.Binary
                    RaiseEvent onClientDataAvailable(Me, frame)
                Case Is = WsOpcode.Ping
                    Dim pong = processor.BuildPong(frame.PlainPayload)
                    WriteStream(pong)
                Case Is = WsOpcode.Pong
                Case Is = WsOpcode.ConnectionClose
                    Dim reason = processor.DecodeCloseMessage(streamData)
                    'for debug only
                    Console.WriteLine("Closed:{0}", reason)

                    WriteStream({136})
                    CloseConnection(1000)
                Case Else
                    CloseConnection(1002, "Invalid opcode")
            End Select


        End If


    End Sub
    ''' <summary>
    ''' populate dictionary of mimetype
    ''' ?? perhaps it's better to put it on setting file ??
    ''' </summary>
    Private Sub populateMimeType()
        dct = New Dictionary(Of String, String)
        dct.Add(".html", "text/html; charset=utf-8")
        dct.Add(".js", "application/javascript; charset=utf-8")
        dct.Add(".css", "text/css; charset=utf-8")
        dct.Add(".png", "image/png")
        dct.Add(".jpg", "image/jpg")
        dct.Add(".ico", "image/ vnd.microsoft.icon")

        '...more
    End Sub
End Class

Public Class ServerWorkerEventDataAvailable
    Property OpCode As WsOpcode
    Property MessageData As Byte()
End Class