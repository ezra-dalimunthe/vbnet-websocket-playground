Imports System.Net
Imports HaloWebSocket
''' <summary>
''' 
''' </summary>
''' <remarks>credit: https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/WebSocket_Server_Vb.NET</remarks>
Public Class WebSocketServer
    Inherits System.Net.Sockets.TcpListener

    Delegate Sub OnClientConnectDelegate(ByVal sender As Object, ByRef Client As ServerWorker)
    Event OnClientConnect As OnClientConnectDelegate
    Delegate Sub OnErrorOccuredDelegate(ByVal sender As Object, ByVal message As String)
    Event OnErrorOccured As OnErrorOccuredDelegate

    Delegate Sub OnClientGetPingReplyMessageDelegate(ByVal sender As Object, ByVal client As ServerWorker)
    Event OnClientGetPingReplyMessage As OnClientGetPingReplyMessageDelegate


    Dim WithEvents PendingCheckTimer As Timers.Timer = New Timers.Timer(500)
    Dim WithEvents ClientDataAvailableTimer As Timers.Timer = New Timers.Timer(50)
    Property ClientCollection As List(Of ServerWorker) = New List(Of ServerWorker)

    Sub New(ByVal url As String, ByVal port As Integer)
        MyBase.New(IPAddress.Parse(url), port)
    End Sub

    Overloads Sub [Start]()
        MyBase.Start()
        If PendingCheckTimer Is Nothing Then
            PendingCheckTimer = New Timers.Timer(500)
        End If
        PendingCheckTimer.Start()
    End Sub

    Overloads Sub [Stop]()

        For Each c In Me.ClientCollection
            If c.isConnected Then
                c.CloseConnection(1000, "NORMAL CLOSURE")
            End If
        Next
        Me.ClientCollection.RemoveAll(AddressOf isClientDisconnected)
        MyBase.Stop()
        If PendingCheckTimer IsNot Nothing Then
            PendingCheckTimer.Stop()
            PendingCheckTimer.Dispose()
            PendingCheckTimer = Nothing

        End If

        If Not ClientDataAvailableTimer Is Nothing Then
            ClientDataAvailableTimer.Stop()
            ClientDataAvailableTimer.Dispose()
            ClientDataAvailableTimer = Nothing
        End If



        GC.Collect(3)
        GC.WaitForPendingFinalizers()
        GC.Collect(3)

    End Sub


    Private Sub Client_Connected(ByVal sender As Object, ByRef client As ServerWorker) Handles Me.OnClientConnect
        Me.ClientCollection.RemoveAll(AddressOf isClientDisconnected)
        Me.ClientCollection.Add(client)
        AddHandler client.onClientDisconnect, AddressOf Client_Disconnected
        AddHandler client.onClientDataAvailable, AddressOf BroadCastData
        client.HandShake()

        If ClientDataAvailableTimer Is Nothing Then
            ClientDataAvailableTimer = New Timers.Timer(50)
        End If
        If ClientDataAvailableTimer.Enabled = False Then
            ClientDataAvailableTimer.Start()
        End If


    End Sub


    Sub Client_Disconnected(ByVal ClientId As String)

    End Sub


    Function isClientDisconnected(ByVal client As ServerWorker) As Boolean
        If client.isConnected = False Then
            Client_Disconnected(client.ClientId)
        End If
        Return Not client.isConnected
    End Function

    Private Sub PendingCheckTimer_Elapsed(ByVal sender As Object, ByVal e As System.Timers.ElapsedEventArgs) Handles PendingCheckTimer.Elapsed

        If Pending() Then
            RaiseEvent OnClientConnect(Me, New ServerWorker(Me.AcceptTcpClient()))
        End If
    End Sub

    Dim collectionCopy As List(Of ServerWorker)
    Private Sub ClientDataAvailableTimer_Elapsed(ByVal sender As Object, ByVal e As System.Timers.ElapsedEventArgs) Handles ClientDataAvailableTimer.Elapsed
        Me.ClientCollection.RemoveAll(AddressOf isClientDisconnected)

        ClientDataAvailableTimer.Stop()
        Dim clientCount = Me.ClientCollection.Count
        For Each Client As ServerWorker In Me.ClientCollection
            Try
                Client.CheckForDataAvailability()
            Catch ex As Exception
                RaiseEvent OnErrorOccured(Me, ex.Message)
                'for debug only
                Console.WriteLine(ex.StackTrace)
            End Try
            If Me.ClientCollection.Count <> clientCount Then
                Exit For
            End If
        Next
        ClientDataAvailableTimer.Start()

    End Sub

    Private Sub BroadCastData(ByVal Sender As ServerWorker, ByVal e As IncomingData)

        Dim encoder As New System.Text.UTF8Encoding
        Dim processor As New FrameDataProcessor
        If e.OpCode = WsOpcode.Text Then

            Dim msgdata = encoder.GetString(e.PlainPayload)
            Dim frames = processor.MakeFrame(e.PlainPayload, WsOpcode.Text, False)
            For Each client As ServerWorker In Me.ClientCollection
                'If client.Equals(Sender) Then
                '    'Continue For
                'End If
                client.WriteStream(frames)
            Next
        End If

    End Sub



End Class