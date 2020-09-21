
Public Enum WsOpcode
    Continuation = &H0
    Text = &H1
    Binary = &H2
    ConnectionClose = &H8
    Ping = &H9
    Pong = &HA
End Enum

Public Class IncomingData
    Property OpCode As WsOpcode
    Property PlainPayload As Byte()
End Class