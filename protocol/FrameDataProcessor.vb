''' <summary>
''' Class to process incoming data and make websocket frame
''' </summary>
Public Class FrameDataProcessor
    'ON X64, max allowed byte() length is &H7FFFFFC7
    'Const MAX_STREAM_LENGTH_TO_PROCESS = &H7FFFFFC7
    Const DEFAULT_PAYLOAD_LENGTH As Integer = 4096
    Private encoder As New System.Text.UTF8Encoding
    Public Enum FinMode
        Final
        Continuation
    End Enum

    Public Function ProcessIncomingData(ByVal Buffer() As Byte) As IncomingData
        Dim PlainPayload As New List(Of Byte)
        Dim opCode As WsOpcode
        Dim isFinal As Boolean
        Dim ms As New IO.MemoryStream(Buffer)
        Dim isMasked As Boolean
        While ms.Length > ms.Position
            Dim payloadLength As UInt64 = 0
            Dim byteOne = ms.ReadByte()
            Dim bitOne = Convert.ToString(byteOne, 2).PadLeft(8, "0"c)
            If ms.Position = 1 Then
                isFinal = bitOne.Substring(0, 1) = "1"
                opCode = CType(Convert.ToUInt16(bitOne.Substring(4, 4), 2), WsOpcode)
            End If
            Dim byteTwo = ms.ReadByte()
            Dim bitTwo = Convert.ToString(byteTwo, 2).PadLeft(8, "0"c)
            isMasked = bitTwo.Substring(0, 1) = "1"
            Dim checkPayload = Convert.ToUInt16(bitTwo.Substring(1, 7), 2)

            If checkPayload < 126 Then
                payloadLength = checkPayload
            ElseIf checkPayload = 126 Then
                Dim b16(1) As Byte
                ms.Read(b16, 0, 2)
                If BitConverter.IsLittleEndian Then
                    Array.Reverse(b16)
                End If
                payloadLength = BitConverter.ToUInt16(b16, 0)
            Else
                Dim b64(7) As Byte
                ms.Read(b64, 0, 8)
                If BitConverter.IsLittleEndian Then
                    Array.Reverse(b64)
                End If
                payloadLength = BitConverter.ToUInt64(b64, 0)
            End If
            Dim counterIndex As Integer = 0
            If isMasked Then
                Dim keys(3) As Byte
                ms.Read(keys, 0, 4)

                Dim bitMask As Integer
                While payloadLength > counterIndex
                    PlainPayload.Add(Convert.ToByte(ms.ReadByte() Xor keys(bitMask Mod 4)))
                    counterIndex += 1
                    bitMask += 1

                    If bitMask = 4 Then bitMask = 0
                End While
            Else
                While payloadLength > counterIndex
                    PlainPayload.Add(Convert.ToByte(ms.ReadByte()))
                    counterIndex += 1
                End While
            End If
        End While

        ms.Close()
        ms.Dispose()
        Dim rvalue As New IncomingData With {.PlainPayload = PlainPayload.ToArray(),
            .OpCode = opCode
            }

        Return rvalue
    End Function

    Public Function MakeFrame(ByVal Payload As Byte(), ByVal Opcode As WsOpcode, Optional ByVal usemask As Boolean = False) As Byte()
        Dim rvalue As New List(Of Byte)

        Dim payloadLength = Payload.LongCount()
        If payloadLength = 0 Then
            Dim binFirst = "1000" &
                Convert.ToString(Convert.ToUInt16(Opcode), 2).PadLeft(4, "0"c)

            rvalue.Add(Convert.ToByte(binFirst, 2))
            'byte#2
            Dim binSecond As String = "0"
            binSecond &= Convert.ToString(0, 2).PadLeft(7, "0"c)
            rvalue.Add(Convert.ToByte(binSecond, 2))
            Return rvalue.ToArray()

        End If




        Dim FRAMEMAXLENGTH = DEFAULT_PAYLOAD_LENGTH
        Dim payloadFrames As New List(Of Byte())
        Dim fragmentLength As Long
        Dim loopCounter As Integer
        While payloadLength - loopCounter * FRAMEMAXLENGTH > 0
            fragmentLength = Math.Min(payloadLength - loopCounter * FRAMEMAXLENGTH, FRAMEMAXLENGTH)
            Dim frame(Convert.ToInt32(fragmentLength - 1)) As Byte
            Array.Copy(Payload, loopCounter * FRAMEMAXLENGTH, frame, 0, frame.Length)
            payloadFrames.Add(frame)

            loopCounter += 1
        End While

        Dim isFinal As Boolean
        Dim frameOpcode As WsOpcode

        For frameIndex As Integer = 0 To payloadFrames.Count - 1
            'byte#1
            isFinal = (frameIndex = 0 AndAlso payloadFrames.Count = 1) OrElse
                (frameIndex = payloadFrames.Count - 1)
            frameOpcode = If(frameIndex = 0, Opcode, WsOpcode.Continuation)

            Dim binFirst = If(isFinal, "1000", "0000") &
                Convert.ToString(Convert.ToUInt16(frameOpcode), 2).PadLeft(4, "0"c)

            rvalue.Add(Convert.ToByte(binFirst, 2))
            'byte#2
            Dim frame = payloadFrames(frameIndex)
            Dim framelength = frame.LongLength

            Dim binSecond As String = If(usemask, "1", "0")
            If framelength < 126 Then
                binSecond &= Convert.ToString(framelength, 2).PadLeft(7, "0"c)
                rvalue.Add(Convert.ToByte(binSecond, 2))
            ElseIf framelength < &HFFFF Then
                binSecond &= Convert.ToString(126, 2).PadLeft(7, "0"c)
                rvalue.Add(Convert.ToByte(binSecond, 2))
                Dim u16 As Byte() = BitConverter.GetBytes(Convert.ToUInt16(framelength))
                If BitConverter.IsLittleEndian Then
                    Array.Reverse(u16)
                End If
                rvalue.AddRange(u16)


            Else
                binSecond &= Convert.ToString(127, 2).PadLeft(7, "0"c)
                rvalue.Add(Convert.ToByte(binSecond, 2))
                Dim u64 As Byte() = BitConverter.GetBytes(Convert.ToUInt64(framelength))
                If BitConverter.IsLittleEndian Then
                    Array.Reverse(u64)
                End If
                rvalue.AddRange(u64)
            End If

            If usemask Then
                Dim keys(3) As Byte
                Dim rnd As New Random
                rnd.NextBytes(keys)
                Dim imod As Integer
                rvalue.AddRange(keys)
                For Each block In frame
                    rvalue.Add(Convert.ToByte(block Xor keys(imod Mod 4)))
                    imod += 1
                    If imod = 4 Then imod = 0
                Next
            Else
                rvalue.AddRange(frame)
            End If

        Next
        Return rvalue.ToArray()
    End Function

    Public Function DecodeCloseMessage(ByVal Message As Byte()) As String
        Dim frame = ProcessIncomingData(Message)
        Dim bytes() As Byte = frame.PlainPayload
        Dim reasoncode = BitConverter.ToUInt16({bytes(1), bytes(0)}, 0)
        Dim reasonText = encoder.GetString(bytes, 2, bytes.Length - 2)
        Return String.Format("{0}-{1}", reasoncode, reasonText)
    End Function
    Public Function BuildPing() As Byte()
        Dim pngmessage = encoder.GetBytes("abcdefghijklmnopqrstuvwxyz")
        Dim enc = MakeFrame(pngmessage, WsOpcode.Ping)
        Return enc
    End Function
    Public Function BuildPong(ByVal message As Byte()) As Byte()
        If message.Length = 1 AndAlso message.Equals(vbNullChar) Then
            Return {&H8A, &H0}
        End If
        Dim enc = MakeFrame(message, WsOpcode.Pong)
        Return enc
    End Function




End Class
