﻿Imports System.IO
Imports System.Reflection
Imports System.Threading.Tasks
Imports System.Data.SqlClient

Public Class Form1

    Dim File As String = ""
    Dim ErrorsInLog As String = ""
    Dim RawDataTable As Generic.List(Of RawDataRow)
    Dim DataTableMain As Generic.List(Of RawDataRow)
    Dim OrdersToProcessDataTable As Generic.List(Of ProcessOrderDataRow)
    Dim OrderDataTable As Generic.List(Of OrderDataRow)
    Dim OrderDataTableMain As Generic.List(Of OrderDataRow)
    Dim GroupedOrderDataTableMain As Generic.List(Of GroupedOrderDataRow)
    Dim TheLogMode As LogMode = Form1.LogMode.Appia

    Public VersionInfo As String = ""

    Public Shared SOH As String = Chr(1)
    Dim GTPConnStr As String = "data source=FNYCORE;initial catalog=gtpbrdb;uid=IntranetFO;pwd=mintraFO1;packet size=4096;Pooling=False;Connect Timeout=5;"
    Dim ISINs As DataTable = getDataTable("SELECT CODE,NAME FROM GTPFSI_FI_EXT_DOMAIN_CODES(nolock) ,GTPFSI_FINANCIAL_INSTRUMENTS (nolock) WHERE GTPFSI_FI_EXT_DOMAIN_CODES.FIN_INST_ID = GTPFSI_FINANCIAL_INSTRUMENTS.FIN_INST_ID", Me.GTPConnStr)

    Private Enum LogMode
        Appia
        QuickFix
        BISTECH
    End Enum


    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Me.TheLogMode = LogMode.Appia
        RawDataTable = New Generic.List(Of RawDataRow)
        If Me.CheckBox1.Checked Then
            Dim ProcessLogs As Boolean = GetLogFilesToDataTable("", "FNYMAGE")
            If ProcessLogs Then ProcessLogsInDataTable()
        Else
            Dim ProcessLogs As Boolean = GetLogFilesToDataTable("\\FNYMAGE\D$\Javelin\Appia7.1\logs\buy\C" & Format(Me.DateTimePicker1.Value, "yyyyMMdd") & ".log", "FNYMAGE")
            If ProcessLogs Then ProcessLogsInDataTable()

        End If
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Me.TheLogMode = LogMode.QuickFix
        RawDataTable = New Generic.List(Of RawDataRow)

        If Me.CheckBox1.Checked Then
            Dim ProcessLogs As Boolean = GetLogFilesToDataTable("", "FIX")
            If ProcessLogs Then ProcessLogsInDataTable()
        Else
            Dim ProcessLogs As Boolean = GetLogFilesToDataTable("\\FNYBISTECH\BistechLogs\BistechConnector1\" & Format(Me.DateTimePicker1.Value, "yyyyMMdd") & "\FIXT.1.1-BIFNY_FX311-BI.messages.current.log", "FNYBISTECH1")
            If ProcessLogs Then ProcessLogs = GetLogFilesToDataTable("\\FNYBISTECH\BistechLogs\BistechConnector2\" & Format(Me.DateTimePicker1.Value, "yyyyMMdd") & "\FIXT.1.1-BIFNY_FX312-BI.messages.current.log", "FNYBISTECH2")
            If ProcessLogs Then ProcessLogsInDataTable()
        End If

    End Sub


    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        Me.TheLogMode = LogMode.BISTECH
        RawDataTable = New Generic.List(Of RawDataRow)

        If Me.CheckBox1.Checked Then
            Dim ProcessLogs As Boolean = GetLogFilesToDataTable("", "BISTECH")
            If ProcessLogs Then ProcessLogsInDataTable()
        Else
            Dim ProcessLogs As Boolean = True
            For FileCounter As Integer = 1 To 100
                Dim FileName As String = "BistechConnector1" & Format(Me.DateTimePicker1.Value, "yyyyMMdd") & "-XXX.log"
                FileName = FileName.Replace("XXX", Microsoft.VisualBasic.Right("000" & FileCounter, 3))
                Try
                    GetLogFilesToDataTable("\\FNYBISTECH\BistechLogs\BistechConnector1\" & FileName & "", "FNYBISTECH1")
                    Me.Text = "\\FNYBISTECH\BistechLogs\BistechConnector1\" & FileName & " alındı..."
                Catch ex As Exception
                    Exit For
                End Try
            Next

            For FileCounter As Integer = 1 To 100
                Dim FileName As String = "BistechConnector2" & Format(Me.DateTimePicker1.Value, "yyyyMMdd") & "-XXX.log"
                FileName = FileName.Replace("XXX", Microsoft.VisualBasic.Right("000" & FileCounter, 3))
                Try
                    GetLogFilesToDataTable("\\FNYBISTECH\BistechLogs\BistechConnector2\" & FileName & "", "FNYBISTECH2")
                    Me.Text = "\\FNYBISTECH\BistechLogs\BistechConnector2\" & FileName & " alındı..."
                Catch ex As Exception
                    Exit For
                End Try
            Next

            If ProcessLogs Then ProcessLogsInDataTable()

        End If

    End Sub

    Private Function GetLogFilesToDataTable(ByVal LogFile As String, ByVal LogFileChannel As String) As Boolean
        ErrorsInLog = ""

        If LogFile = "" Then

            OpenFileDialog1.FileName = ""
            OpenFileDialog1.Filter = "Log Dosyaları (*.log)|*.log|All Files (*.*)|*.*"

            If OpenFileDialog1.ShowDialog = DialogResult.OK Then
                LogFile = OpenFileDialog1.FileName
            Else
                MsgBox("Dosya seçilmedi / bulunamadı..")
                Return False
            End If

        End If


        Me.Cursor = Cursors.WaitCursor

        Dim FileName As String = LogFile.Substring(LogFile.LastIndexOf("\")).Replace(".log", Format(Now, "yyyy-MM-dd-HH-mm-ss-ffffff") & ".log")
        System.IO.File.Copy(LogFile, Application.StartupPath & "\LogFiles" & FileName, True)
        File = Application.StartupPath & "\LogFiles" & FileName

        Using StreamsReader As New StreamReader(File)
            Do While StreamsReader.Peek() >= 0
                RawDataTable.Add(New RawDataRow(StreamsReader.ReadLine, LogFileChannel))
            Loop
        End Using

        Me.Cursor = Cursors.Default

        Return True

    End Function

    Private Sub ProcessLogsInDataTable()


        Me.Cursor = Cursors.WaitCursor

        SetDoubleBuffered(Me.DataGridView1)
        SetDoubleBuffered(Me.DataGridView2)
        SetDoubleBuffered(Me.DataGridView3)
        SetDoubleBuffered(Me.dtgFields)


        OrdersToProcessDataTable = New Generic.List(Of ProcessOrderDataRow)
        OrderDataTable = New Generic.List(Of OrderDataRow)
        OrderDataTableMain = New Generic.List(Of OrderDataRow)
        GroupedOrderDataTableMain = New Generic.List(Of GroupedOrderDataRow)


        Dim xx As String = ""

        xx &= "1" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf

        'Parse The DataTable
        Dim DistinctMessages = From t In RawDataTable.Distinct() Select t
        RawDataTable = DistinctMessages.ToList

        'Set LogMode / use datetime format to choose
        'If DataTable.Rows.Count > 0 Then If Mid(DataTable.Rows(0)("Raw"), 1, 5).IndexOf("/") < 0 Then TheLogMode = Form1.LogMode.QuickFix

        Dim VERSION_ID As String = ""
        If TheLogMode = Form1.LogMode.Appia Then

            For Each RawDataRow In RawDataTable

                Try

                    Dim RawMessageString As String = RawDataRow.Raw
                    RawDataRow.LogDate = CDate(Replace(Mid(RawMessageString, 1, 19), "/", "-") & "." & Mid(RawMessageString, 21, 3))
                    'Get I/O
                    If Mid(RawMessageString, 25, 35) = " FIXConnectionData: Sending data on" Then
                        RawDataRow.IO = "O"
                    ElseIf Mid(RawMessageString, 25, 35) = " FIXPump: Received data on connecti" Then
                        RawDataRow.IO = "I"
                    End If

                    'Get Message
                    Dim AddUpdateOrderShouldWork As Boolean = False
                    Dim ExecType As String = ""
                    Dim ClOrderId As String = ""
                    Dim OrigClOrderId As String = ""
                    Dim OrderId As String = ""
                    Dim GTPOrderId As String = ""
                    Dim Account As String = ""
                    Dim BS As String = ""
                    Dim SecCode As String = ""
                    Dim Units As Integer = 0
                    Dim RealizedUnits As Integer = 0
                    Dim RemainingUnits As Integer = 0
                    Dim AvgPrice As Double = 0
                    Dim LastPx As Double = 0

                    Dim SqBr As Integer = RawMessageString.IndexOf("[")
                    If SqBr > 0 Then
                        RawDataRow.Message = Replace(RawMessageString.Substring(SqBr + 1), "]", "")

                        Dim ArrayOfFields As String() = RawDataRow.Message.Split(SOH)
                        For Each Field As String In ArrayOfFields

                            If VERSION_ID = "" Then
                                If Mid(Field, 1, 2) = "8=" Then VERSION_ID = Mid(Field, 3)
                            End If

                            If Mid(Field, 1, 3) = "35=" Then

                                RawDataRow.MessageType = GetMessageName(VERSION_ID, Mid(Field, 4))
                                If Mid(Field, 4) = "8" Then AddUpdateOrderShouldWork = True

                            End If

                            If Mid(Field, 1, 4) = "150=" Then
                                RawDataRow.MessageType = RawDataRow.MessageType & "-" & GetExecTypeDef(VERSION_ID, Mid(Field, 5))
                                ExecType = Mid(Field, 5)
                            End If

                            If Mid(Field, 1, 2) = "1=" Then Account = Mid(Field, 3)
                            If Mid(Field, 1, 3) = "54=" Then BS = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "55=" Then SecCode = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "38=" Then Units = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "14=" Then RealizedUnits = Mid(Field, 4)
                            If Mid(Field, 1, 4) = "151=" Then RemainingUnits = Mid(Field, 5)
                            If Mid(Field, 1, 2) = "6=" Then AvgPrice = Mid(Field, 3)
                            If Mid(Field, 1, 3) = "31=" Then LastPx = Mid(Field, 4)

                            If Mid(Field, 1, 3) = "11=" Then ClOrderId = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "41=" Then OrigClOrderId = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "37=" Then OrderId = Mid(Field, 4)
                            If Mid(Field, 1, 4) = "70=" Then GTPOrderId = Mid(Field, 5)

                            If Mid(Field, 1, 3) = "11=" Then RawDataRow.ClOrderId = ClOrderId
                            If Mid(Field, 1, 3) = "41=" Then RawDataRow.OrigClOrderId = OrigClOrderId
                            If Mid(Field, 1, 3) = "37=" Then RawDataRow.OrderId = OrderId
                            If Mid(Field, 1, 4) = "70=" Then
                                RawDataRow.GTPOrderId = GTPOrderId
                            End If

                            If Mid(Field, 1, 3) = "34=" Then RawDataRow.MessageSeqNum = Mid(Field, 4)

                        Next

                        'If AddUpdateOrderShouldWork Then Me.AddUpdateOrder(ExecType, ClOrderId, OrigClOrderId, OrderId, GTPOrderId, Account, BS, SecCode, Units, RealizedUnits, RemainingUnits, AvgPrice, LastPx)
                        If AddUpdateOrderShouldWork Then OrdersToProcessDataTable.Add(New ProcessOrderDataRow(ExecType, ClOrderId, OrigClOrderId, OrderId, GTPOrderId, Account, BS, SecCode, Units, RealizedUnits, RemainingUnits, AvgPrice, LastPx, RawDataRow.MessageSeqNum))

                    End If

                Catch ex As Exception
                    ErrorsInLog = ErrorsInLog & RawDataRow.Raw & vbCrLf
                End Try

            Next
            
        ElseIf TheLogMode = Form1.LogMode.QuickFix Then


            For Each RawDataRow In RawDataTable

                Try
                    Dim RawMessageString As String = RawDataRow.Raw
                    RawDataRow.LogDate = CDate(Mid(RawMessageString, 1, 4) & "-" & Mid(RawMessageString, 5, 2) & "-" & Mid(RawMessageString, 7, 2) & " " & Mid(RawMessageString, 10, 12))

                    'Get I/O

                    If RawMessageString.IndexOf("49=BIFNY") > 0 Then
                        RawDataRow.IO = "I"
                    Else
                        RawDataRow.IO = "O"
                    End If


                    'Get Message
                    Dim AddUpdateOrderShouldWork As Boolean = False
                    Dim ExecType As String = ""
                    Dim ClOrderId As String = ""
                    Dim OrigClOrderId As String = ""
                    Dim OrderId As String = ""
                    Dim GTPOrderId As String = ""
                    Dim Account As String = ""
                    Dim BS As String = ""
                    Dim SecCode As String = ""
                    Dim Units As Integer = 0
                    Dim RealizedUnits As Integer = 0
                    Dim RemainingUnits As Integer = 0
                    Dim AvgPrice As Double = 0
                    Dim LastPx As Double = 0


                    RawDataRow.Message = Mid(RawMessageString, 25)
                    Dim ArrayOfFields As String() = RawDataRow.Message.Split(SOH)
                    For Each Field As String In ArrayOfFields

                        If VERSION_ID = "" Then
                            If Mid(Field, 1, 2) = "8=" Then VERSION_ID = Mid(Field, 3)
                        End If

                        If Mid(Field, 1, 3) = "35=" Then

                            RawDataRow.MessageType = GetMessageName(VERSION_ID, Mid(Field, 4))
                            If Mid(Field, 4) = "8" Then AddUpdateOrderShouldWork = True

                        End If
                        'If Mid(Field, 1, 3) = "70=" Then
                        '    RawDataRow.GTPOrderId = Mid(Field, 7, 6)

                        'End If

                        If Mid(Field, 1, 4) = "150=" Then
                            RawDataRow.MessageType = RawDataRow.MessageType & "-" & GetExecTypeDef(VERSION_ID, Mid(Field, 5))
                            ExecType = Mid(Field, 5)
                        End If

                        If Mid(Field, 1, 2) = "1=" Then Account = Mid(Field, 3)
                        If Mid(Field, 1, 3) = "54=" Then BS = Mid(Field, 4)
                        If Mid(Field, 1, 3) = "55=" Then SecCode = Mid(Field, 4)
                        If Mid(Field, 1, 3) = "38=" Then Units = Mid(Field, 4)
                        If Mid(Field, 1, 3) = "14=" Then RealizedUnits = Mid(Field, 4)
                        If Mid(Field, 1, 4) = "151=" Then RemainingUnits = Mid(Field, 5)
                        If Mid(Field, 1, 2) = "6=" Then AvgPrice = Mid(Field, 3)
                        If Mid(Field, 1, 3) = "31=" Then LastPx = Mid(Field, 4)

                        If Mid(Field, 1, 3) = "11=" Then ClOrderId = Mid(Field, 4)
                        If Mid(Field, 1, 3) = "41=" Then OrigClOrderId = Mid(Field, 4)
                        If Mid(Field, 1, 3) = "37=" Then OrderId = Mid(Field, 4)
                        If Mid(Field, 1, 3) = "70=" Then
                            GTPOrderId = Mid(Field, 7)
                        End If


                        If Mid(Field, 1, 3) = "11=" Then RawDataRow.ClOrderId = ClOrderId
                        If Mid(Field, 1, 3) = "41=" Then RawDataRow.OrigClOrderId = OrigClOrderId
                        If Mid(Field, 1, 3) = "37=" Then RawDataRow.OrderId = OrderId
                        If Mid(Field, 1, 4) = "70=" Then RawDataRow.GTPOrderId = GTPOrderId
                        If Mid(Field, 1, 3) = "34=" Then RawDataRow.MessageSeqNum = Mid(Field, 4)

                    Next

                    If ExecType = "F" Then
                        If RemainingUnits = 0 Then RawDataRow.MessageType = Replace(RawDataRow.MessageType, "PartialFillORFill", "Fill")
                        If RemainingUnits > 0 Then RawDataRow.MessageType = Replace(RawDataRow.MessageType, "PartialFillORFill", "PartialFill")
                    End If

                    'If AddUpdateOrderShouldWork Then Me.AddUpdateOrder(ExecType, ClOrderId, OrigClOrderId, OrderId, GTPOrderId, Account, BS, SecCode, Units, RealizedUnits, RemainingUnits, AvgPrice, LastPx)
                    If AddUpdateOrderShouldWork Then OrdersToProcessDataTable.Add(New ProcessOrderDataRow(ExecType, ClOrderId, OrigClOrderId, OrderId, GTPOrderId, Account, BS, SecCode, Units, RealizedUnits, RemainingUnits, AvgPrice, LastPx, RawDataRow.MessageSeqNum))



                Catch ex As Exception
                    ErrorsInLog = ErrorsInLog & RawDataRow.Raw & vbCrLf
                End Try

            Next



        ElseIf TheLogMode = Form1.LogMode.BISTECH Then

            For RowDataCounter As Integer = RawDataTable.Count - 1 To 0 Step -1

                Try

                    Dim RawMessageString As String = RawDataTable(RowDataCounter).Raw
                    If Mid(RawMessageString, 42, 14) = "To App Message" Or Mid(RawMessageString, 42, 14) = "From App Messa" Then
                    Else
                        RawDataTable.Remove(RawDataTable(RowDataCounter))
                    End If
                Catch ex As Exception
                End Try

            Next



            For Each RawDataRow In RawDataTable

                Try
                    Dim RawMessageString As String = RawDataRow.Raw

                    RawDataRow.LogDate = CDate("1900-01-01 " & Mid(RawMessageString, 1, 16))


                    'Get I/O
                    If RawMessageString.IndexOf("49=BIFNY") > 0 Then
                        RawDataRow.IO = "O"
                    Else
                        RawDataRow.IO = "I"
                    End If

                    'Get Message
                    Dim AddUpdateOrderShouldWork As Boolean = False
                    Dim ExecType As String = ""
                    Dim ClOrderId As String = ""
                    Dim OrigClOrderId As String = ""
                    Dim OrderId As String = ""
                    Dim GTPOrderId As String = ""
                    Dim Account As String = ""
                    Dim BS As String = ""
                    Dim SecCode As String = ""
                    Dim Units As Integer = 0
                    Dim RealizedUnits As Integer = 0
                    Dim RemainingUnits As Integer = 0
                    Dim AvgPrice As Double = 0
                    Dim LastPx As Double = 0



                    Dim GTPLOGProcessString As Boolean = True
                    If Mid(RawMessageString, 42, 14) = "To App Message" Or Mid(RawMessageString, 42, 14) = "From App Messa" Then
                        GTPLOGProcessString = True
                        RawDataRow.Message = Mid(RawMessageString, 42)
                        RawDataRow.Message = RawDataRow.Message.Replace("To App Message ", "")
                        RawDataRow.Message = RawDataRow.Message.Replace("From App Message ", "")
                    Else
                        GTPLOGProcessString = False
                    End If



                    If GTPLOGProcessString Then


                        Dim ArrayOfFields As String() = RawDataRow.Message.Split(SOH)
                        For Each Field As String In ArrayOfFields

                            If VERSION_ID = "" Then
                                If Mid(Field, 1, 2) = "8=" Then VERSION_ID = Mid(Field, 3)
                            End If

                            If Mid(Field, 1, 3) = "35=" Then

                                RawDataRow.MessageType = GetMessageName(VERSION_ID, Mid(Field, 4))
                                If Mid(Field, 4) = "8" Then AddUpdateOrderShouldWork = True

                            End If
                            If Mid(Field, 1, 3) = "70=" Then
                                RawDataRow.GTPOrderId = Mid(Field, 7, 6)

                            End If

                            If Mid(Field, 1, 4) = "150=" Then
                                RawDataRow.MessageType = RawDataRow.MessageType & "-" & GetExecTypeDef(VERSION_ID, Mid(Field, 5))
                                ExecType = Mid(Field, 5)
                            End If

                            If Mid(Field, 1, 2) = "1=" Then Account = Mid(Field, 3)
                            If Mid(Field, 1, 3) = "54=" Then BS = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "55=" Then SecCode = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "38=" Then Units = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "14=" Then RealizedUnits = Mid(Field, 4)
                            If Mid(Field, 1, 4) = "151=" Then RemainingUnits = Mid(Field, 5)
                            If Mid(Field, 1, 2) = "6=" Then AvgPrice = Mid(Field, 3)
                            If Mid(Field, 1, 3) = "31=" Then LastPx = Mid(Field, 4)

                            If Mid(Field, 1, 3) = "11=" Then ClOrderId = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "41=" Then OrigClOrderId = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "37=" Then OrderId = Mid(Field, 4)
                            If Mid(Field, 1, 3) = "70=" Then
                                GTPOrderId = Mid(Field, 7)
                            End If


                            If Mid(Field, 1, 3) = "11=" Then RawDataRow.ClOrderId = ClOrderId
                            If Mid(Field, 1, 3) = "41=" Then RawDataRow.OrigClOrderId = OrigClOrderId
                            If Mid(Field, 1, 3) = "37=" Then RawDataRow.OrderId = OrderId
                            If Mid(Field, 1, 4) = "70=" Then RawDataRow.GTPOrderId = GTPOrderId
                            If Mid(Field, 1, 3) = "34=" Then RawDataRow.MessageSeqNum = Mid(Field, 4)

                        Next

                        If ExecType = "F" Then
                            If RemainingUnits = 0 Then RawDataRow.MessageType = Replace(RawDataRow.MessageType, "PartialFillORFill", "Fill")
                            If RemainingUnits > 0 Then RawDataRow.MessageType = Replace(RawDataRow.MessageType, "PartialFillORFill", "PartialFill")
                        End If

                        'If AddUpdateOrderShouldWork Then Me.AddUpdateOrder(ExecType, ClOrderId, OrigClOrderId, OrderId, GTPOrderId, Account, BS, SecCode, Units, RealizedUnits, RemainingUnits, AvgPrice, LastPx)
                        If AddUpdateOrderShouldWork Then OrdersToProcessDataTable.Add(New ProcessOrderDataRow(ExecType, ClOrderId, OrigClOrderId, OrderId, GTPOrderId, Account, BS, SecCode, Units, RealizedUnits, RemainingUnits, AvgPrice, LastPx, RawDataRow.MessageSeqNum))


                    End If

                Catch ex As Exception
                    ErrorsInLog = ErrorsInLog & RawDataRow.Raw & vbCrLf
                End Try

            Next


        End If


        xx &= "2" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf

        'Clear messages with empty IO and try to set seq numbers
        Dim result = From t In RawDataTable Where t.IO <> "" And t.MessageType <> "Heartbeat" And t.MessageType <> "TestRequest" Select t
        DataTableMain = result.ToList


        If Me.CheckBox2.Checked Then

            xx &= "3" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf
            'once new olanları
            'Sonra digerleri 
            OrdersToProcessDataTable = OrdersToProcessDataTable.OrderBy(Function(x) x.SeqNumber).ToList()
            For Each OrderToProcess In OrdersToProcessDataTable
                If OrderToProcess.ExecType = "0" Then
                    AddUpdateOrder(OrderToProcess.ExecType, OrderToProcess.ClOrderId, OrderToProcess.OrigClOrderId, OrderToProcess.OrderId, OrderToProcess.GTPOrderId, OrderToProcess.Account, OrderToProcess.BS, OrderToProcess.SecCode, OrderToProcess.Units, OrderToProcess.RealizedUnits, OrderToProcess.RemainingUnits, OrderToProcess.AvgPrice, OrderToProcess.LastPx)
                End If
            Next
            For Each OrderToProcess In OrdersToProcessDataTable
                Dim ExecTypeOfOrder As String = OrderToProcess.ExecType
                If ExecTypeOfOrder = "1" Or ExecTypeOfOrder = "2" Or ExecTypeOfOrder = "4" Or ExecTypeOfOrder = "5" Or ExecTypeOfOrder = "F" Then
                    AddUpdateOrder(OrderToProcess.ExecType, OrderToProcess.ClOrderId, OrderToProcess.OrigClOrderId, OrderToProcess.OrderId, OrderToProcess.GTPOrderId, OrderToProcess.Account, OrderToProcess.BS, OrderToProcess.SecCode, OrderToProcess.Units, OrderToProcess.RealizedUnits, OrderToProcess.RemainingUnits, OrderToProcess.AvgPrice, OrderToProcess.LastPx)
                End If
            Next

            Dim result2 = From t In OrderDataTable Select t
            OrderDataTableMain = result2.ToList

            xx &= "4" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf

            If ErrorsInLog <> "" Then
                Dim zErrors As New Errors
                zErrors.ErrorString = ErrorsInLog
                zErrors.ShowDialog()
            End If

            xx &= "5" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf

            'BIST kodlarını yaz
            Dim xcnt As Integer = 1
            If TheLogMode = LogMode.QuickFix Or TheLogMode = LogMode.BISTECH Then

                'For Each RawDataRow In DataTableMain

                '    Dim OrderId As String = RawDataRow.OrderId
                '    Dim GTPOrderId As String = RawDataRow.GTPOrderId

                '    If GTPOrderId <> "" And OrderId Is Nothing Then
                '        For Each OrderDataRow In OrderDataTableMain
                '            If OrderDataRow.GTPOrderId = RawDataRow.GTPOrderId And OrderDataRow.OrderId <> "" Then
                '                RawDataRow.OrderId = OrderDataRow.OrderId
                '                Exit For
                '            End If
                '        Next
                '    End If

                '    If OrderId <> "" And GTPOrderId Is Nothing Then
                '        For Each OrderDataRow In OrderDataTableMain
                '            If OrderDataRow.OrderId = RawDataRow.OrderId And OrderDataRow.GTPOrderId <> "" Then
                '                RawDataRow.GTPOrderId = OrderDataRow.GTPOrderId
                '                Exit For
                '            End If
                '        Next
                '    End If

                'Next

                Parallel.ForEach(DataTableMain.Cast(Of RawDataRow), Sub(RawDataRow)
                                                                        'main loop

                                                                        Dim OrderId As String = RawDataRow.OrderId
                                                                        Dim GTPOrderId As String = RawDataRow.GTPOrderId

                                                                        If GTPOrderId <> "" And OrderId Is Nothing Then
                                                                            For Each OrderDataRow In OrderDataTableMain
                                                                                If OrderDataRow.GTPOrderId = RawDataRow.GTPOrderId And OrderDataRow.OrderId <> "" Then
                                                                                    RawDataRow.OrderId = OrderDataRow.OrderId
                                                                                    Exit For
                                                                                End If
                                                                            Next
                                                                        End If

                                                                        If OrderId <> "" And GTPOrderId Is Nothing Then
                                                                            For Each OrderDataRow In OrderDataTableMain
                                                                                If OrderDataRow.OrderId = RawDataRow.OrderId And OrderDataRow.GTPOrderId <> "" Then
                                                                                    RawDataRow.GTPOrderId = OrderDataRow.GTPOrderId
                                                                                    Exit For
                                                                                End If
                                                                            Next
                                                                        End If

                                                                    End Sub)


            End If

            xx &= "6" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf


            If TheLogMode = LogMode.Appia Then

                For Each OrderDataRow In OrderDataTableMain

                    Dim OrderFound As Boolean = False
                    For Each GroupedOrderDataRow In GroupedOrderDataTableMain
                        If GroupedOrderDataRow.Account = OrderDataRow.Account And GroupedOrderDataRow.BS = OrderDataRow.BS And GroupedOrderDataRow.SecCode = OrderDataRow.SecCode Then
                            GroupedOrderDataRow.RealizedUnits = CDbl(GroupedOrderDataRow.RealizedUnits) + CDbl(OrderDataRow.RealizedUnits)
                            If CDbl(GroupedOrderDataRow.RealizedUnits) > 0 Then
                                If TheLogMode = LogMode.Appia Then
                                    GroupedOrderDataRow.RealizedTotal = CDbl(GroupedOrderDataRow.RealizedTotal) + (CDbl(OrderDataRow.RealizedUnits) * CDbl(OrderDataRow.AvgPrice))
                                Else
                                    GroupedOrderDataRow.RealizedTotal = CDbl(GroupedOrderDataRow.RealizedTotal) + (CDbl(OrderDataRow.RealizedUnits) * CDbl(OrderDataRow.LastPx))
                                End If

                            End If
                            OrderFound = True
                        End If
                    Next

                    If Not OrderFound Then

                        Dim GODR As New GroupedOrderDataRow(OrderDataRow.Account, OrderDataRow.BS, OrderDataRow.SecCode, "", 0, 0, 0, 0)

                        Dim foundrows() As DataRow = ISINs.Select("CODE = '" & OrderDataRow.SecCode & "'")
                        If foundrows.Length > 0 Then
                            GODR.Name = foundrows(0)("NAME")
                        End If

                        GODR.RealizedUnits = OrderDataRow.RealizedUnits

                        If TheLogMode = LogMode.Appia Then
                            GODR.RealizedTotal = (CDbl(OrderDataRow.RealizedUnits) * CDbl(OrderDataRow.AvgPrice))
                        Else
                            GODR.RealizedTotal = (CDbl(OrderDataRow.RealizedUnits) * CDbl(OrderDataRow.LastPx))
                        End If

                        GroupedOrderDataTableMain.Add(GODR)

                    End If

                Next

                For Each GroupedOrderDataRow In GroupedOrderDataTableMain
                    GroupedOrderDataRow.AvgPrice = FormatNumber(CDbl(GroupedOrderDataRow.RealizedTotal) / CDbl(GroupedOrderDataRow.RealizedUnits), 6, -1, 0, 0)
                Next

                Me.DataGridView3.DataSource = GroupedOrderDataTableMain

            End If


        End If

        xx &= "7" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf

        DataGridView1.RowHeadersVisible = False
        DataGridView1.DataSource = DataTableMain
        If DataTableMain.Count > 0 Then
            DataGridView1.Columns(0).Visible = False
            DataGridView1.Columns(9).Visible = False
            DataGridView1.Columns(1).AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
            DataGridView1.Columns(2).AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
            DataGridView1.Columns(2).DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
            DataGridView1.Columns(3).AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            DataGridView1.Columns(4).AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
            DataGridView1.Columns(5).AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
            DataGridView1.Columns(6).AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
            DataGridView1.Columns(7).AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
            DataGridView1.Columns(1).DefaultCellStyle.Format = "dd.MM.yyyy HH:mm:ss fff"
        End If

        xx &= "8" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf

        Me.DataGridView2.DataSource = OrderDataTable

        xx &= "9" & Format(Now, "yyyy-MM-dd HH:mm:ss.ffffff") & vbCrLf

        Me.Cursor = Cursors.Default


        ' MsgBox(xx)
    End Sub

    Public Shared Sub SetDoubleBuffered(ByVal control As Control)
        GetType(Control).InvokeMember("DoubleBuffered", BindingFlags.SetProperty Or BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, control, New Object() {True})
    End Sub

    Private Sub DataGridView1_CellMouseDoubleClick(sender As Object, e As DataGridViewCellMouseEventArgs) Handles DataGridView1.CellMouseDoubleClick
        If e.RowIndex >= 0 Then
            If TheLogMode = LogMode.Appia Then
                If CheckDBNull(Me.DataGridView1.Rows(e.RowIndex).Cells(4).Value, "") <> "" Then
                    Me.TextBox1.Text = Me.DataGridView1.Rows(e.RowIndex).Cells(4).Value
                    Me.DataGridView1.Refresh()
                    Ara_Click(Nothing, Nothing)
                End If
            Else
                If CheckDBNull(Me.DataGridView1.Rows(e.RowIndex).Cells(7).Value, "") <> "" Then
                    Me.TextBox1.Text = Me.DataGridView1.Rows(e.RowIndex).Cells(7).Value
                    Me.DataGridView1.Refresh()
                    Ara_Click(Nothing, Nothing)
                End If
            End If
        End If
    End Sub


    Private Sub DataGridView1_RowEnter(sender As Object, e As DataGridViewCellEventArgs) Handles DataGridView1.RowEnter
        If e.RowIndex >= 0 Then
            Me.txtMessage.Text = Me.DataGridView1.Rows(e.RowIndex).Cells(8).Value
            Me.DataGridView1.Refresh()
            getFields_Click(Nothing, Nothing)
        End If
    End Sub




    Private Sub getFields_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)

        Dim FieldDT As New DataTable
        FieldDT.Columns.Add(New System.Data.DataColumn("FIELD_TAG", GetType(System.Int32)))
        FieldDT.Columns.Add(New System.Data.DataColumn("FIELD_NAME", GetType(System.String)))
        FieldDT.Columns.Add(New System.Data.DataColumn("FIELD_VALUE", GetType(System.String)))
        FieldDT.Columns.Add(New System.Data.DataColumn("FIELD_DESCRIPTION", GetType(System.String)))

        Dim FieldDR As DataRow
        Dim ArrayOfFields As String() = Me.txtMessage.Text.Split(SOH)
        Dim VERSION_ID As String = ""

        For FieldCounter As Integer = 0 To ArrayOfFields.Length - 2

            Dim TempFieldString As String = ArrayOfFields(FieldCounter)

            Dim FIELD_TAG As Integer = 0
            Dim FIELD_NAME As String = ""
            Dim FIELD_VALUE As String = ""
            Dim FIELD_DESCRIPTION As String = ""

            If VERSION_ID = "" Then

                'Ilk field versionId alinir
                FIELD_TAG = Mid(TempFieldString, 1, TempFieldString.IndexOf("="))
                FIELD_VALUE = Mid(TempFieldString, TempFieldString.IndexOf("=") + 2)
                VERSION_ID = FIELD_VALUE
                FIELD_NAME = GetFieldName(VERSION_ID, FIELD_TAG)

            Else
                FIELD_TAG = Mid(TempFieldString, 1, TempFieldString.IndexOf("="))
                FIELD_NAME = GetFieldName(VERSION_ID, FIELD_TAG)
                FIELD_VALUE = Mid(TempFieldString, TempFieldString.IndexOf("=") + 2)

                If FIELD_TAG = 35 Then
                    FIELD_DESCRIPTION = GetMessageName(VERSION_ID, FIELD_VALUE)
                End If

            End If

            FieldDR = FieldDT.NewRow
            FieldDR("FIELD_TAG") = FIELD_TAG
            FieldDR("FIELD_NAME") = FIELD_NAME
            FieldDR("FIELD_VALUE") = FIELD_VALUE
            FieldDR("FIELD_DESCRIPTION") = FIELD_DESCRIPTION
            FieldDT.Rows.Add(FieldDR)
        Next

        Me.dtgFields.DataSource = FieldDT
        Me.dtgFields.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells


        For Each xItem As DataGridViewRow In Me.dtgFields.Rows


            Dim FIELD_TAG As Integer = xItem.Cells(0).Value

            Dim DoneWithRow As Boolean = False

            If Not DoneWithRow Then
                If GetCheckIsHeaderField(VERSION_ID, FIELD_TAG) Then
                    xItem.DefaultCellStyle.BackColor = Color.LemonChiffon
                    DoneWithRow = True
                End If
            End If

            If Not DoneWithRow Then
                If GetCheckIsTrailerField(VERSION_ID, FIELD_TAG) Then
                    xItem.DefaultCellStyle.BackColor = Color.LightCoral
                    DoneWithRow = True
                End If
            End If

            If Not DoneWithRow Then
                xItem.DefaultCellStyle.BackColor = Color.White
                DoneWithRow = True
            End If

        Next

    End Sub

    Public Function AddOrderId(OrderId As String, OrderList As List(Of String)) As List(Of String)

        Threading.Thread.CurrentThread.CurrentCulture = New Globalization.CultureInfo("en-US")
        Threading.Thread.CurrentThread.CurrentUICulture = New Globalization.CultureInfo("en-US")

        If Not OrderList.Exists(Function(x) String.Equals(x, OrderId, StringComparison.CurrentCulture)) Then
            OrderList.Add(OrderId)
        End If

    End Function

    Public Function AddProcessedOrderId(ProcessedOrderId As String, ProcessedOrderList As List(Of String)) As List(Of String)

        Dim exists As Boolean = False
        For Each Item As String In ProcessedOrderList
            If ProcessedOrderId = Item Then
                exists = True
                Exit For
            End If
        Next
        If Not exists Then
            ProcessedOrderList.Add(ProcessedOrderId)
        End If
        Return ProcessedOrderList

    End Function


    Private Sub Ara_Click(sender As Object, e As EventArgs) Handles Ara.Click


        Threading.Thread.CurrentThread.CurrentCulture = New Globalization.CultureInfo("en-US")
        Threading.Thread.CurrentThread.CurrentUICulture = New Globalization.CultureInfo("en-US")

        Me.Text = "Lütfen bekleyiniz..."

        Me.Cursor = Cursors.WaitCursor
        Application.DoEvents()

        Dim SearchOrderId As String = Me.TextBox1.Text

        If TheLogMode = LogMode.Appia Then


            Dim OrderIdList As New List(Of String)
            Dim ProcessedOrderList As New List(Of String)
            AddOrderId(SearchOrderId, OrderIdList)

            Dim Goon As Boolean = True
            Do While Goon

                Dim OrderIdListLength As Integer = OrderIdList.Count

                Dim ListOfOrderIdsToBeAdded As String = ""

                For Each item As String In OrderIdList

                    Dim itemwasprocessed As Boolean = False
                    For Each processeditem As String In ProcessedOrderList
                        If processeditem = item Then
                            itemwasprocessed = True
                            Exit For
                        End If
                    Next

                    If Not itemwasprocessed Then

                        For Each RawDataRow In DataTableMain
                            If RawDataRow.ClOrderId = item Or RawDataRow.OrigClOrderId = item Or RawDataRow.OrderId = item Then
                                If RawDataRow.ClOrderId <> "" Then
                                    ListOfOrderIdsToBeAdded = ListOfOrderIdsToBeAdded & "+" & RawDataRow.ClOrderId
                                End If
                                If CheckDBNull(RawDataRow.OrigClOrderId, "") <> "" Then
                                    ListOfOrderIdsToBeAdded = ListOfOrderIdsToBeAdded & "+" & RawDataRow.OrigClOrderId
                                End If
                            End If
                        Next

                        AddProcessedOrderId(item, ProcessedOrderList)

                    End If


                Next

                If ListOfOrderIdsToBeAdded.Length > 0 Then
                    Dim ListOfOrderIds() As String = Mid(ListOfOrderIdsToBeAdded, 2).Split("+")
                    For Each ListOfOrderId As String In ListOfOrderIds
                        AddOrderId(ListOfOrderId, OrderIdList)
                    Next
                End If


                If OrderIdListLength = OrderIdList.Count Then
                    Goon = False
                End If


            Loop

            Dim TempDataTable As New Generic.List(Of RawDataRow)
            For Each item As String In OrderIdList
                Dim SelectList As String = ""
                SelectList = SelectList & " ClOrderId = '" & item & "' "
                SelectList = SelectList & " OR OrigClOrderId = '" & item & "' "
                SelectList = SelectList & " OR OrderId = '" & item & "' "

                For Each RawDataRow In DataTableMain
                    If RawDataRow.ClOrderId = item Or RawDataRow.OrigClOrderId = item Or RawDataRow.OrderId = item Then
                        TempDataTable.Add(RawDataRow)
                    End If
                Next

            Next

            '        
            Dim resul3 = From t In TempDataTable.Distinct() Select t Order By t.LogDate, t.MessageSeqNum
            RawDataTable = resul3.ToList

            Me.DataGridView1.DataSource = RawDataTable
            If Me.DataGridView1.RowCount > 0 Then
                DataGridView1.Columns(0).Visible = False
                DataGridView1.RowHeadersVisible = False
            End If

            For OrderCounter As Integer = RawDataTable.Count - 1 To 0 Step -1
                OrderDataTable = Nothing
                Try
                    Dim results = From t In OrderDataTableMain Where t.ClOrderId = RawDataTable(OrderCounter).ClOrderId Select t
                    OrderDataTable = results.ToList()
                Catch ex As Exception
                End Try
                If Not OrderDataTable Is Nothing Then
                    Me.DataGridView2.DataSource = OrderDataTable
                    Exit For
                End If
            Next


            Me.Text = "FIX Log Reader - v." & Me.VersionInfo
            Application.DoEvents()

        ElseIf TheLogMode = LogMode.QuickFix Then

            Dim TempDataTable As New Generic.List(Of RawDataRow)
            Dim SelectList As String = ""
            SelectList = SelectList & " OrderId = '" & SearchOrderId & "' "
            SelectList = SelectList & " OR GTPOrderId = '" & SearchOrderId & "' "
            For Each RawDataRow In DataTableMain
                If RawDataRow.OrderId = SearchOrderId Or RawDataRow.GTPOrderId = SearchOrderId Then
                    TempDataTable.Add(RawDataRow)
                End If
            Next

            Dim resul3 = From t In TempDataTable.Distinct() Select t Order By t.LogDate, t.MessageSeqNum
            RawDataTable = resul3.ToList

            Me.DataGridView1.DataSource = RawDataTable
            If Me.DataGridView1.RowCount > 0 Then
                DataGridView1.Columns(0).Visible = False
                DataGridView1.RowHeadersVisible = False
            End If

            For OrderCounter As Integer = RawDataTable.Count - 1 To 0 Step -1
                OrderDataTable = Nothing
                Try
                    Dim results2 = From t In OrderDataTableMain Where t.OrderId = RawDataTable(OrderCounter).OrderId Select t
                    OrderDataTable = results2.ToList()
                Catch ex As Exception
                End Try
                If Not OrderDataTable Is Nothing Then
                    Me.DataGridView2.DataSource = OrderDataTable
                    Exit For
                End If
            Next


            Me.Text = "FIX Log Reader - v." & Me.VersionInfo
            Application.DoEvents()

        ElseIf TheLogMode = LogMode.BISTECH Then

            Dim TempDataTable As New Generic.List(Of RawDataRow)
            Dim SelectList As String = ""
            SelectList = SelectList & " OrderId = '" & SearchOrderId & "' "
            SelectList = SelectList & " OR GTPOrderId = '" & SearchOrderId & "' "
            For Each RawDataRow In DataTableMain
                If RawDataRow.OrderId = SearchOrderId Or RawDataRow.GTPOrderId = SearchOrderId Then
                    TempDataTable.Add(RawDataRow)
                End If
            Next

            Dim resul3 = From t In TempDataTable.Distinct() Select t Order By t.LogDate, t.MessageSeqNum
            RawDataTable = resul3.ToList

            Me.DataGridView1.DataSource = RawDataTable
            If Me.DataGridView1.RowCount > 0 Then
                DataGridView1.Columns(0).Visible = False
                DataGridView1.RowHeadersVisible = False
            End If

            For OrderCounter As Integer = RawDataTable.Count - 1 To 0 Step -1
                OrderDataTable = Nothing
                Try
                    Dim results2 = From t In OrderDataTableMain Where t.OrderId = RawDataTable(OrderCounter).OrderId Select t
                    OrderDataTable = results2.ToList()
                Catch ex As Exception
                End Try
                If Not OrderDataTable Is Nothing Then
                    Me.DataGridView2.DataSource = OrderDataTable
                    Exit For
                End If
            Next

            Me.Text = "FIX Log Reader - v." & Me.VersionInfo
            Application.DoEvents()

        End If



        Me.Cursor = Cursors.Default

        Me.Focus()


    End Sub




    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click


        Me.Cursor = Cursors.WaitCursor

        Me.TextBox1.Text = ""

        Dim result1 = From t In DataTableMain Select t
        RawDataTable = result1.ToList

        Me.DataGridView1.DataSource = RawDataTable
        If Me.DataGridView1.RowCount > 0 Then
            DataGridView1.Columns(0).Visible = False
            DataGridView1.RowHeadersVisible = False
        End If

        Dim result2 = From t In OrderDataTableMain Select t
        OrderDataTable = result2.ToList
        Me.DataGridView2.DataSource = OrderDataTable


        Me.Cursor = Cursors.Default

    End Sub

    Private Function GetExecTypeDef(VERSION_ID As String, TAG_VALUE As String) As String

        If VERSION_ID = "FIX.4.2" Then
            Select Case TAG_VALUE
                Case "0" : Return "New"
                Case "1" : Return "PartialFill"
                Case "2" : Return "Fill"
                Case "3" : Return "DoneForDay"
                Case "4" : Return "Canceled"
                Case "5" : Return "Replaced"
                Case "6" : Return "PendingCancel"
                Case "7" : Return "Stopped"
                Case "8" : Return "Rejected"
                Case "9" : Return "Suspended"
                Case "A" : Return "PendingNew"
                Case "B" : Return "Calculated"
                Case "C" : Return "Expired"
                Case "D" : Return "Restated"
                Case "E" : Return "PendingReplace"
                Case Else : Return "!!!UNKNOWN TYPE!!!"
            End Select
        End If

        If VERSION_ID = "FIXT.1.1" Then
            Select Case TAG_VALUE
                Case "0" : Return "New"
                Case "3" : Return "DoneForDay"
                Case "4" : Return "Canceled"
                Case "5" : Return "Replaced"
                Case "6" : Return "PendingCancel"
                Case "7" : Return "Stopped"
                Case "8" : Return "Rejected"
                Case "9" : Return "Suspended"
                Case "A" : Return "PendingNew"
                Case "B" : Return "Calculated"
                Case "C" : Return "Expired"
                Case "D" : Return "Restated"
                Case "E" : Return "PendingReplace"
                Case "F" : Return "Trade|PartialFillORFill|"
                Case "G" : Return "TradeCorrect"
                Case "H" : Return "TradeCancel"
                Case "I" : Return "OrderStatus"
                Case "J" : Return "TradeInAClearingHold"
                Case "K" : Return "TradeHasBeenReleasedToClearing"
                Case "L" : Return "TriggeredORActivatedBySystem"
                Case Else : Return "!!!UNKNOWN TYPE!!!"
            End Select
        End If


    End Function

    Private Function AddUpdateOrder(ByVal ExecType As String, ByVal ClOrderId As String, ByVal OrigClOrderId As String, ByVal OrderId As String, ByVal GTPOrderId As String, ByVal Account As String, ByVal BS As String, ByVal SecCode As String, ByVal Units As Integer, ByVal RealizedUnits As Integer, ByVal RemainingUnits As Integer, ByVal AvgPrice As Double, ByVal LastPx As Double) As Boolean

        Try

            If TheLogMode = LogMode.Appia Then
                If ExecType = "0" Then

                    OrderDataTable.Add(New OrderDataRow(Account, ClOrderId, OrigClOrderId, OrderId, GTPOrderId, BS, SecCode, Units, RealizedUnits, RemainingUnits, AvgPrice, LastPx))

                ElseIf ExecType = "4" Or ExecType = "5" Then

                    Dim Order = OrderDataTable.Where(Function(d) d.ClOrderId = OrigClOrderId).FirstOrDefault()
                    If Order Is Nothing Then Order = OrderDataTable.Where(Function(d) d.ClOrderId = ClOrderId).FirstOrDefault()
                    Order.ClOrderId = ClOrderId
                    Order.OrigClOrderId = OrigClOrderId
                    Order.OrderId = OrderId
                    Order.GTPOrderId = GTPOrderId

                    Order.Units = Units
                    Order.RealizedUnits = RealizedUnits
                    Order.RemainingUnits = RemainingUnits
                    Order.AvgPrice = AvgPrice
                    Order.LastPx = LastPx

                ElseIf ExecType = "1" Or ExecType = "2" Then

                    Dim Order = OrderDataTable.Where(Function(d) d.ClOrderId = ClOrderId).FirstOrDefault()
                    Order.ClOrderId = ClOrderId
                    Order.OrigClOrderId = OrigClOrderId
                    Order.OrderId = OrderId
                    Order.GTPOrderId = GTPOrderId

                    Order.Units = Units
                    Order.RealizedUnits = RealizedUnits
                    Order.RemainingUnits = RemainingUnits
                    Order.AvgPrice = AvgPrice
                    Order.LastPx = LastPx


                End If

            Else

                If ExecType = "0" Then

                    Dim FoundOrder As Boolean = False
                    For Each Order As OrderDataRow In OrderDataTable
                        If Order.OrderId = OrderId Or Order.GTPOrderId = GTPOrderId Then
                            If Order.ClOrderId = "" Or ClOrderId = Nothing Then Order.ClOrderId = ClOrderId
                            If Order.OrigClOrderId = "" Or OrigClOrderId = Nothing Then Order.OrigClOrderId = OrigClOrderId
                            If Order.OrderId = "" Or OrderId = Nothing Then Order.OrderId = OrderId
                            If Order.GTPOrderId = "" Or GTPOrderId = Nothing Then Order.GTPOrderId = GTPOrderId
                            If Order.BS = "" Or BS = Nothing Then Order.BS = BS
                            If Order.SecCode = "" Or SecCode = Nothing Then Order.SecCode = SecCode
                            If Order.RealizedUnits = 0 Or RealizedUnits = Nothing Then Order.RealizedUnits = RealizedUnits
                            If Order.RemainingUnits = 0 Or RemainingUnits = Nothing Then Order.RemainingUnits = RemainingUnits
                            If Order.AvgPrice = 0 Or AvgPrice = Nothing Then Order.AvgPrice = AvgPrice
                            If Order.LastPx = 0 Or LastPx = Nothing Then Order.LastPx = LastPx
                            FoundOrder = True
                            Exit For
                        End If
                    Next
                    If Not FoundOrder Then
                        OrderDataTable.Add(New OrderDataRow(Account, ClOrderId, OrigClOrderId, OrderId, GTPOrderId, BS, SecCode, Units, RealizedUnits, RemainingUnits, AvgPrice, LastPx))
                    End If

                ElseIf ExecType = "4" Or ExecType = "5" Then

                    For Each Order As OrderDataRow In OrderDataTable
                        If Order.OrderId = OrderId Or Order.GTPOrderId = GTPOrderId Or Order.ClOrderId = ClOrderId Then
                            Order.ClOrderId = ClOrderId
                            Order.OrigClOrderId = OrigClOrderId

                            Order.Units = Units
                            Order.RealizedUnits = RealizedUnits
                            Order.RemainingUnits = RemainingUnits
                            Order.AvgPrice = AvgPrice
                            Order.LastPx = LastPx
                            Exit For
                        End If
                    Next

                ElseIf ExecType = "F" Then

                    For Each Order As OrderDataRow In OrderDataTable
                        If Order.OrderId = OrderId Or Order.GTPOrderId = GTPOrderId Then
                            Order.Units = Units
                            Order.RealizedUnits = Units - RemainingUnits
                            Order.RemainingUnits = RemainingUnits
                            Order.AvgPrice = AvgPrice
                            Order.LastPx = LastPx
                            Exit For
                        End If
                    Next

                End If


            End If

        Catch ex As Exception
            ErrorsInLog = ErrorsInLog & "ClOrderId:" & ClOrderId & "|OrigClOrderId:" & OrigClOrderId & "|OrderId:" & OrderId & "|GTPOrderId:" & GTPOrderId & vbCrLf
        End Try

        Return True

    End Function

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Me.Cursor = Cursors.WaitCursor
        Me.Panel1.Visible = Not Me.Panel1.Visible
        Me.Cursor = Cursors.Default
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Text = "FIX Log Reader - v." & Me.VersionInfo

        Threading.Thread.CurrentThread.CurrentCulture = New Globalization.CultureInfo("en-US")
        Threading.Thread.CurrentThread.CurrentUICulture = New Globalization.CultureInfo("en-US")

    End Sub


    Private Sub Export2ExcelToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles Export2ExcelToolStripMenuItem.Click

        Dim ExportDataset As New DataSet
        ExportDataset.Tables.Clear()

        If Me.DataGridView1.SelectedCells.Count > 0 Then
            ExportDataset.Tables.Add(ConvertToDataTable(RawDataTable))
        Else
            ExportDataset.Tables.Add(ConvertToDataTable(DataTableMain))
        End If

        ExportDataset.Tables.Add(ConvertToDataTable(OrderDataTableMain))
        If GroupedOrderDataTableMain.Count > 0 Then ExportDataset.Tables.Add(ConvertToDataTable(GroupedOrderDataTableMain))
        ExportDataset.Tables(0).TableName = "Log"
        ExportDataset.Tables(1).TableName = "Orders"
        If GroupedOrderDataTableMain.Count > 0 Then ExportDataset.Tables(2).TableName = "GroupedOrders"
        ExportToExcel(ExportDataset, Application.StartupPath & "\FileExports\FileExport-" & Format(Now, "yyyy-MM-dd-HH-mm-ss") & ".xls")

        MsgBox("Export successful!", MsgBoxStyle.OkOnly, "FixLogReader Export2Excel")

    End Sub



    Private Sub Export2ExcelAndOpenExcelToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles Export2ExcelAndOpenExcelToolStripMenuItem.Click

        Dim ExportDataset As New DataSet
        ExportDataset.Tables.Clear()

        If Me.DataGridView1.SelectedCells.Count > 0 Then
            ExportDataset.Tables.Add(ConvertToDataTable(RawDataTable))
        Else
            ExportDataset.Tables.Add(ConvertToDataTable(DataTableMain))
        End If

        ExportDataset.Tables.Add(ConvertToDataTable(OrderDataTableMain))
        If GroupedOrderDataTableMain.Count > 0 Then ExportDataset.Tables.Add(ConvertToDataTable(GroupedOrderDataTableMain))
        ExportDataset.Tables(0).TableName = "Log"
        ExportDataset.Tables(1).TableName = "Orders"
        If GroupedOrderDataTableMain.Count > 0 Then ExportDataset.Tables(2).TableName = "GroupedOrders"
        Dim ExportFileName As String = Application.StartupPath & "\FileExports\FileExport-" & Format(Now, "yyyy-MM-dd-HH-mm-ss") & ".xls"
        ExportToExcel(ExportDataset, ExportFileName)

        Try
            Process.Start("EXCEL.EXE", """" & ExportFileName & """")
        Catch ex As Exception
        End Try

    End Sub


    Public Shared Function ConvertToDataTable(Of T)(ByVal list As IList(Of T)) As DataTable
        Dim table As New DataTable()
        Dim fields() As FieldInfo = GetType(T).GetFields(BindingFlags.Instance Or BindingFlags.NonPublic)
        For Each field As FieldInfo In fields
            table.Columns.Add(field.Name, field.FieldType)
        Next
        For Each item As T In list
            Dim row As DataRow = table.NewRow()
            For Each field As FieldInfo In fields
                row(field.Name) = field.GetValue(item)
            Next
            table.Rows.Add(row)
        Next
        Return table
    End Function



    Public Sub ExportToExcel(ByVal dataSet As DataSet, ByVal outputPath As String)
        ' Create the Excel Application object
        Dim excelApp As New Microsoft.Office.Interop.Excel.Application()

        ' Create a new Excel Workbook
        Dim excelWorkbook As Microsoft.Office.Interop.Excel.Workbook = excelApp.Workbooks.Add(Type.Missing)

        Dim sheetIndex As Integer = 0
        Dim col, row As Integer
        Dim excelSheet As Microsoft.Office.Interop.Excel.Worksheet = Nothing

        ' Copy each DataTable as a new Sheet
        For Each dt As System.Data.DataTable In dataSet.Tables

            sheetIndex += 1

            ' Copy the DataTable to an object array
            Dim rawData(dt.Rows.Count, dt.Columns.Count - 1) As Object

            ' Copy the column names to the first row of the object array
            For col = 0 To dt.Columns.Count - 1
                rawData(0, col) = dt.Columns(col).ColumnName
            Next

            ' Copy the values to the object array
            For col = 0 To dt.Columns.Count - 1
                For row = 0 To dt.Rows.Count - 1
                    rawData(row + 1, col) = dt.Rows(row).ItemArray(col)
                Next
            Next

            ' Calculate the final column letter
            Dim finalColLetter As String = String.Empty
            Dim colCharset As String = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            Dim colCharsetLen As Integer = colCharset.Length

            If dt.Columns.Count > colCharsetLen Then
                finalColLetter = colCharset.Substring( _
                 (dt.Columns.Count - 1) \ colCharsetLen - 1, 1)
            End If

            finalColLetter += colCharset.Substring( _
              (dt.Columns.Count - 1) Mod colCharsetLen, 1)

            ' Create a new Sheet
            excelSheet = CType( _
                excelWorkbook.Sheets.Add(excelWorkbook.Sheets(sheetIndex), _
                Type.Missing, 1, Microsoft.Office.Interop.Excel.XlSheetType.xlWorksheet), Microsoft.Office.Interop.Excel.Worksheet)

            excelSheet.Name = dt.TableName

            ' Fast data export to Excel
            Dim excelRange As String = String.Format("A1:{0}{1}", finalColLetter, dt.Rows.Count + 1)
            excelSheet.Range(excelRange, Type.Missing).Value2 = rawData

            ' Mark the first row as BOLD
            CType(excelSheet.Rows(1, Type.Missing), Microsoft.Office.Interop.Excel.Range).Font.Bold = True

            excelSheet = Nothing
        Next

        ' Save and Close the Workbook
        excelWorkbook.SaveAs(outputPath, Microsoft.Office.Interop.Excel.XlFileFormat.xlWorkbookNormal, Type.Missing, _
         Type.Missing, Type.Missing, Type.Missing, Microsoft.Office.Interop.Excel.XlSaveAsAccessMode.xlExclusive, _
         Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing)

        excelWorkbook.Close(True, Type.Missing, Type.Missing)

        excelWorkbook = Nothing

        ' Release the Application object
        excelApp.Quit()
        excelApp = Nothing

        releaseObject(excelSheet)
        releaseObject(excelWorkbook)
        releaseObject(excelApp)

        ' Collect the unreferenced objects
        GC.Collect()
        GC.WaitForPendingFinalizers()

    End Sub

    Private Sub releaseObject(ByVal obj As Object)
        Try
            System.Runtime.InteropServices.Marshal.ReleaseComObject(obj)
            obj = Nothing
        Catch ex As Exception
            obj = Nothing
        Finally
            GC.Collect()
        End Try
    End Sub

    Private Function GetMessageName(ByVal VersionId As String, ByVal MessageTag As String) As String

        If VersionId = "FIXT.1.1" Then
            If MessageTag.Equals("0", StringComparison.CurrentCulture) Then Return "Heartbeat"
            If MessageTag.Equals("1", StringComparison.CurrentCulture) Then Return "TestRequest"
            If MessageTag.Equals("2", StringComparison.CurrentCulture) Then Return "ResendRequest"
            If MessageTag.Equals("3", StringComparison.CurrentCulture) Then Return "Reject"
            If MessageTag.Equals("4", StringComparison.CurrentCulture) Then Return "SequenceReset"
            If MessageTag.Equals("5", StringComparison.CurrentCulture) Then Return "Logout"
            If MessageTag.Equals("6", StringComparison.CurrentCulture) Then Return "IOI"
            If MessageTag.Equals("7", StringComparison.CurrentCulture) Then Return "Advertisement"
            If MessageTag.Equals("8", StringComparison.CurrentCulture) Then Return "ExecutionReport"
            If MessageTag.Equals("9", StringComparison.CurrentCulture) Then Return "OrderCancelReject"
            If MessageTag.Equals("A", StringComparison.CurrentCulture) Then Return "Logon"
            If MessageTag.Equals("AA", StringComparison.CurrentCulture) Then Return "DerivativeSecurityList"
            If MessageTag.Equals("AB", StringComparison.CurrentCulture) Then Return "NewOrderMultileg"
            If MessageTag.Equals("AC", StringComparison.CurrentCulture) Then Return "MultilegOrderCancelReplace"
            If MessageTag.Equals("AD", StringComparison.CurrentCulture) Then Return "TradeCaptureReportRequest"
            If MessageTag.Equals("AE", StringComparison.CurrentCulture) Then Return "TradeCaptureReport"
            If MessageTag.Equals("AF", StringComparison.CurrentCulture) Then Return "OrderMassStatusRequest"
            If MessageTag.Equals("AG", StringComparison.CurrentCulture) Then Return "QuoteRequestReject"
            If MessageTag.Equals("AH", StringComparison.CurrentCulture) Then Return "RFQRequest"
            If MessageTag.Equals("AI", StringComparison.CurrentCulture) Then Return "QuoteStatusReport"
            If MessageTag.Equals("AJ", StringComparison.CurrentCulture) Then Return "QuoteResponse"
            If MessageTag.Equals("AK", StringComparison.CurrentCulture) Then Return "Confirmation"
            If MessageTag.Equals("AL", StringComparison.CurrentCulture) Then Return "PositionMaintenanceRequest"
            If MessageTag.Equals("AM", StringComparison.CurrentCulture) Then Return "PositionMaintenanceReport"
            If MessageTag.Equals("AN", StringComparison.CurrentCulture) Then Return "RequestForPositions"
            If MessageTag.Equals("AO", StringComparison.CurrentCulture) Then Return "RequestForPositionsAck"
            If MessageTag.Equals("AP", StringComparison.CurrentCulture) Then Return "PositionReport"
            If MessageTag.Equals("AQ", StringComparison.CurrentCulture) Then Return "TradeCaptureReportRequestAck"
            If MessageTag.Equals("AR", StringComparison.CurrentCulture) Then Return "TradeCaptureReportAck"
            If MessageTag.Equals("AS", StringComparison.CurrentCulture) Then Return "AllocationReport"
            If MessageTag.Equals("AT", StringComparison.CurrentCulture) Then Return "AllocationReportAck"
            If MessageTag.Equals("AU", StringComparison.CurrentCulture) Then Return "Confirmation_Ack"
            If MessageTag.Equals("AV", StringComparison.CurrentCulture) Then Return "SettlementInstructionRequest"
            If MessageTag.Equals("AW", StringComparison.CurrentCulture) Then Return "AssignmentReport"
            If MessageTag.Equals("AX", StringComparison.CurrentCulture) Then Return "CollateralRequest"
            If MessageTag.Equals("AY", StringComparison.CurrentCulture) Then Return "CollateralAssignment"
            If MessageTag.Equals("AZ", StringComparison.CurrentCulture) Then Return "CollateralResponse"
            If MessageTag.Equals("B", StringComparison.CurrentCulture) Then Return "News"
            If MessageTag.Equals("BA", StringComparison.CurrentCulture) Then Return "CollateralReport"
            If MessageTag.Equals("BB", StringComparison.CurrentCulture) Then Return "CollateralInquiry"
            If MessageTag.Equals("BC", StringComparison.CurrentCulture) Then Return "NetworkCounterpartySystemStatusRequest"
            If MessageTag.Equals("BD", StringComparison.CurrentCulture) Then Return "NetworkCounterpartySystemStatusResponse"
            If MessageTag.Equals("BE", StringComparison.CurrentCulture) Then Return "UserRequest"
            If MessageTag.Equals("BF", StringComparison.CurrentCulture) Then Return "UserResponse"
            If MessageTag.Equals("BG", StringComparison.CurrentCulture) Then Return "CollateralInquiryAck"
            If MessageTag.Equals("BH", StringComparison.CurrentCulture) Then Return "ConfirmationRequest"
            If MessageTag.Equals("BI", StringComparison.CurrentCulture) Then Return "TradingSessionListRequest"
            If MessageTag.Equals("BJ", StringComparison.CurrentCulture) Then Return "TradingSessionList"
            If MessageTag.Equals("BK", StringComparison.CurrentCulture) Then Return "SecurityListUpdateReport"
            If MessageTag.Equals("BL", StringComparison.CurrentCulture) Then Return "AdjustedPositionReport"
            If MessageTag.Equals("BM", StringComparison.CurrentCulture) Then Return "AllocationInstructionAlert"
            If MessageTag.Equals("BN", StringComparison.CurrentCulture) Then Return "ExecutionAcknowledgement"
            If MessageTag.Equals("BO", StringComparison.CurrentCulture) Then Return "ContraryIntentionReport"
            If MessageTag.Equals("BP", StringComparison.CurrentCulture) Then Return "SecurityDefinitionUpdateReport"
            If MessageTag.Equals("BQ", StringComparison.CurrentCulture) Then Return "SettlementObligationReport"
            If MessageTag.Equals("BR", StringComparison.CurrentCulture) Then Return "DerivativeSecurityListUpdateReport"
            If MessageTag.Equals("BS", StringComparison.CurrentCulture) Then Return "TradingSessionListUpdateReport"
            If MessageTag.Equals("BT", StringComparison.CurrentCulture) Then Return "MarketDefinitionRequest"
            If MessageTag.Equals("BU", StringComparison.CurrentCulture) Then Return "MarketDefinition"
            If MessageTag.Equals("BV", StringComparison.CurrentCulture) Then Return "MarketDefinitionUpdateReport"
            If MessageTag.Equals("BW", StringComparison.CurrentCulture) Then Return "ApplicationMessageRequest"
            If MessageTag.Equals("BX", StringComparison.CurrentCulture) Then Return "ApplicationMessageRequestAck"
            If MessageTag.Equals("BY", StringComparison.CurrentCulture) Then Return "ApplicationMessageReport"
            If MessageTag.Equals("BZ", StringComparison.CurrentCulture) Then Return "OrderMassActionReport"
            If MessageTag.Equals("C", StringComparison.CurrentCulture) Then Return "Email"
            If MessageTag.Equals("CA", StringComparison.CurrentCulture) Then Return "OrderMassActionRequest"
            If MessageTag.Equals("CB", StringComparison.CurrentCulture) Then Return "UserNotification"
            If MessageTag.Equals("D", StringComparison.CurrentCulture) Then Return "NewOrderSingle"
            If MessageTag.Equals("E", StringComparison.CurrentCulture) Then Return "NewOrderList"
            If MessageTag.Equals("F", StringComparison.CurrentCulture) Then Return "OrderCancelRequest"
            If MessageTag.Equals("G", StringComparison.CurrentCulture) Then Return "OrderCancelReplaceRequest"
            If MessageTag.Equals("H", StringComparison.CurrentCulture) Then Return "OrderStatusRequest"
            If MessageTag.Equals("J", StringComparison.CurrentCulture) Then Return "AllocationInstruction"
            If MessageTag.Equals("K", StringComparison.CurrentCulture) Then Return "ListCancelRequest"
            If MessageTag.Equals("L", StringComparison.CurrentCulture) Then Return "ListExecute"
            If MessageTag.Equals("M", StringComparison.CurrentCulture) Then Return "ListStatusRequest"
            If MessageTag.Equals("N", StringComparison.CurrentCulture) Then Return "ListStatus"
            If MessageTag.Equals("P", StringComparison.CurrentCulture) Then Return "AllocationInstructionAck"
            If MessageTag.Equals("Q", StringComparison.CurrentCulture) Then Return "DontKnowTradeDK"
            If MessageTag.Equals("R", StringComparison.CurrentCulture) Then Return "QuoteRequest"
            If MessageTag.Equals("S", StringComparison.CurrentCulture) Then Return "Quote"
            If MessageTag.Equals("T", StringComparison.CurrentCulture) Then Return "SettlementInstructions"
            If MessageTag.Equals("V", StringComparison.CurrentCulture) Then Return "MarketDataRequest"
            If MessageTag.Equals("W", StringComparison.CurrentCulture) Then Return "MarketDataSnapshotFullRefresh"
            If MessageTag.Equals("X", StringComparison.CurrentCulture) Then Return "MarketDataIncrementalRefresh"
            If MessageTag.Equals("Y", StringComparison.CurrentCulture) Then Return "MarketDataRequestReject"
            If MessageTag.Equals("Z", StringComparison.CurrentCulture) Then Return "QuoteCancel"
            If MessageTag.Equals("a", StringComparison.CurrentCulture) Then Return "QuoteStatusRequest"
            If MessageTag.Equals("b", StringComparison.CurrentCulture) Then Return "MassQuoteAcknowledgement"
            If MessageTag.Equals("c", StringComparison.CurrentCulture) Then Return "SecurityDefinitionRequest"
            If MessageTag.Equals("d", StringComparison.CurrentCulture) Then Return "SecurityDefinition"
            If MessageTag.Equals("e", StringComparison.CurrentCulture) Then Return "SecurityStatusRequest"
            If MessageTag.Equals("f", StringComparison.CurrentCulture) Then Return "SecurityStatus"
            If MessageTag.Equals("g", StringComparison.CurrentCulture) Then Return "TradingSessionStatusRequest"
            If MessageTag.Equals("h", StringComparison.CurrentCulture) Then Return "TradingSessionStatus"
            If MessageTag.Equals("i", StringComparison.CurrentCulture) Then Return "MassQuote"
            If MessageTag.Equals("j", StringComparison.CurrentCulture) Then Return "BusinessMessageReject"
            If MessageTag.Equals("k", StringComparison.CurrentCulture) Then Return "BidRequest"
            If MessageTag.Equals("l", StringComparison.CurrentCulture) Then Return "BidResponse"
            If MessageTag.Equals("m", StringComparison.CurrentCulture) Then Return "ListStrikePrice"
            If MessageTag.Equals("n", StringComparison.CurrentCulture) Then Return "XML_non_FIX"
            If MessageTag.Equals("o", StringComparison.CurrentCulture) Then Return "RegistrationInstructions"
            If MessageTag.Equals("p", StringComparison.CurrentCulture) Then Return "RegistrationInstructionsResponse"
            If MessageTag.Equals("q", StringComparison.CurrentCulture) Then Return "OrderMassCancelRequest"
            If MessageTag.Equals("r", StringComparison.CurrentCulture) Then Return "OrderMassCancelReport"
            If MessageTag.Equals("s", StringComparison.CurrentCulture) Then Return "NewOrderCross"
            If MessageTag.Equals("t", StringComparison.CurrentCulture) Then Return "CrossOrderCancelReplaceRequest"
            If MessageTag.Equals("u", StringComparison.CurrentCulture) Then Return "CrossOrderCancelRequest"
            If MessageTag.Equals("v", StringComparison.CurrentCulture) Then Return "SecurityTypeRequest"
            If MessageTag.Equals("w", StringComparison.CurrentCulture) Then Return "SecurityTypes"
            If MessageTag.Equals("x", StringComparison.CurrentCulture) Then Return "SecurityListRequest"
            If MessageTag.Equals("y", StringComparison.CurrentCulture) Then Return "SecurityList"
            If MessageTag.Equals("z", StringComparison.CurrentCulture) Then Return "DerivativeSecurityListRequest"
            If MessageTag.Equals("0", StringComparison.CurrentCulture) Then Return "Heartbeat"
            If MessageTag.Equals("1", StringComparison.CurrentCulture) Then Return "Test Request"
            If MessageTag.Equals("2", StringComparison.CurrentCulture) Then Return "Resend Request"
            If MessageTag.Equals("3", StringComparison.CurrentCulture) Then Return "Reject"
            If MessageTag.Equals("4", StringComparison.CurrentCulture) Then Return "Sequence Reset"
            If MessageTag.Equals("5", StringComparison.CurrentCulture) Then Return "Logout"
            If MessageTag.Equals("6", StringComparison.CurrentCulture) Then Return "Indication of Interest"
            If MessageTag.Equals("7", StringComparison.CurrentCulture) Then Return "Advertisement"
            If MessageTag.Equals("8", StringComparison.CurrentCulture) Then Return "Execution Report"
            If MessageTag.Equals("9", StringComparison.CurrentCulture) Then Return "Order Cancel Reject"
            If MessageTag.Equals("a", StringComparison.CurrentCulture) Then Return "Quote Status Request"
            If MessageTag.Equals("A", StringComparison.CurrentCulture) Then Return "Logon"
            If MessageTag.Equals("b", StringComparison.CurrentCulture) Then Return "Quote Acknowledgement"
            If MessageTag.Equals("B", StringComparison.CurrentCulture) Then Return "News"
            If MessageTag.Equals("c", StringComparison.CurrentCulture) Then Return "Security Definition Request"
            If MessageTag.Equals("C", StringComparison.CurrentCulture) Then Return "Email"
            If MessageTag.Equals("d", StringComparison.CurrentCulture) Then Return "Security Definition"
            If MessageTag.Equals("D", StringComparison.CurrentCulture) Then Return "Order - Single"
            If MessageTag.Equals("e", StringComparison.CurrentCulture) Then Return "Security Status Request"
            If MessageTag.Equals("E", StringComparison.CurrentCulture) Then Return "Order - List"
            If MessageTag.Equals("f", StringComparison.CurrentCulture) Then Return "Security Status"
            If MessageTag.Equals("F", StringComparison.CurrentCulture) Then Return "Order Cancel Request"
            If MessageTag.Equals("g", StringComparison.CurrentCulture) Then Return "Trading Session Status Request"
            If MessageTag.Equals("G", StringComparison.CurrentCulture) Then Return "Order Cancel/Replace Request"
            If MessageTag.Equals("h", StringComparison.CurrentCulture) Then Return "Trading Session Status"
            If MessageTag.Equals("H", StringComparison.CurrentCulture) Then Return "Order Status Request"
            If MessageTag.Equals("i", StringComparison.CurrentCulture) Then Return "Mass Quote"
            If MessageTag.Equals("j", StringComparison.CurrentCulture) Then Return "Business Message Reject"
            If MessageTag.Equals("J", StringComparison.CurrentCulture) Then Return "Allocation"
            If MessageTag.Equals("k", StringComparison.CurrentCulture) Then Return "Bid Request"
            If MessageTag.Equals("K", StringComparison.CurrentCulture) Then Return "List Cancel Request"
            If MessageTag.Equals("l", StringComparison.CurrentCulture) Then Return "Bid Response"
            If MessageTag.Equals("L", StringComparison.CurrentCulture) Then Return "List Execute"
            If MessageTag.Equals("m", StringComparison.CurrentCulture) Then Return "List Strike Price"
            If MessageTag.Equals("M", StringComparison.CurrentCulture) Then Return "List Status Request"
            If MessageTag.Equals("N", StringComparison.CurrentCulture) Then Return "List Status"
            If MessageTag.Equals("P", StringComparison.CurrentCulture) Then Return "Allocation ACK"
            If MessageTag.Equals("Q", StringComparison.CurrentCulture) Then Return "Don't Know Trade"
            If MessageTag.Equals("R", StringComparison.CurrentCulture) Then Return "Quote Request"
            If MessageTag.Equals("S", StringComparison.CurrentCulture) Then Return "Quote"
            If MessageTag.Equals("T", StringComparison.CurrentCulture) Then Return "Settlement Instructions"
            If MessageTag.Equals("V", StringComparison.CurrentCulture) Then Return "Market Data Request"
            If MessageTag.Equals("W", StringComparison.CurrentCulture) Then Return "Market Data - Snapshot/Full Refresh"
            If MessageTag.Equals("X", StringComparison.CurrentCulture) Then Return "Market Data - Incremental Refresh"
            If MessageTag.Equals("Y", StringComparison.CurrentCulture) Then Return "Market Data Request Reject"
            If MessageTag.Equals("Z", StringComparison.CurrentCulture) Then Return "Quote Cancel"
        ElseIf VersionId = "FIX.4.2" Then
            If MessageTag.Equals("0", StringComparison.CurrentCulture) Then Return "Heartbeat"
            If MessageTag.Equals("1", StringComparison.CurrentCulture) Then Return "Test Request"
            If MessageTag.Equals("2", StringComparison.CurrentCulture) Then Return "Resend Request"
            If MessageTag.Equals("3", StringComparison.CurrentCulture) Then Return "Reject"
            If MessageTag.Equals("4", StringComparison.CurrentCulture) Then Return "Sequence Reset"
            If MessageTag.Equals("5", StringComparison.CurrentCulture) Then Return "Logout"
            If MessageTag.Equals("6", StringComparison.CurrentCulture) Then Return "Indication of Interest"
            If MessageTag.Equals("7", StringComparison.CurrentCulture) Then Return "Advertisement"
            If MessageTag.Equals("8", StringComparison.CurrentCulture) Then Return "Execution Report"
            If MessageTag.Equals("9", StringComparison.CurrentCulture) Then Return "Order Cancel Reject"
            If MessageTag.Equals("a", StringComparison.CurrentCulture) Then Return "Quote Status Request"
            If MessageTag.Equals("A", StringComparison.CurrentCulture) Then Return "Logon"
            If MessageTag.Equals("b", StringComparison.CurrentCulture) Then Return "Quote Acknowledgement"
            If MessageTag.Equals("B", StringComparison.CurrentCulture) Then Return "News"
            If MessageTag.Equals("c", StringComparison.CurrentCulture) Then Return "Security Definition Request"
            If MessageTag.Equals("C", StringComparison.CurrentCulture) Then Return "Email"
            If MessageTag.Equals("d", StringComparison.CurrentCulture) Then Return "Security Definition"
            If MessageTag.Equals("D", StringComparison.CurrentCulture) Then Return "Order - Single"
            If MessageTag.Equals("e", StringComparison.CurrentCulture) Then Return "Security Status Request"
            If MessageTag.Equals("E", StringComparison.CurrentCulture) Then Return "Order - List"
            If MessageTag.Equals("f", StringComparison.CurrentCulture) Then Return "Security Status"
            If MessageTag.Equals("F", StringComparison.CurrentCulture) Then Return "Order Cancel Request"
            If MessageTag.Equals("g", StringComparison.CurrentCulture) Then Return "Trading Session Status Request"
            If MessageTag.Equals("G", StringComparison.CurrentCulture) Then Return "Order Cancel/Replace Request"
            If MessageTag.Equals("h", StringComparison.CurrentCulture) Then Return "Trading Session Status"
            If MessageTag.Equals("H", StringComparison.CurrentCulture) Then Return "Order Status Request"
            If MessageTag.Equals("i", StringComparison.CurrentCulture) Then Return "Mass Quote"
            If MessageTag.Equals("j", StringComparison.CurrentCulture) Then Return "Business Message Reject"
            If MessageTag.Equals("J", StringComparison.CurrentCulture) Then Return "Allocation"
            If MessageTag.Equals("k", StringComparison.CurrentCulture) Then Return "Bid Request"
            If MessageTag.Equals("K", StringComparison.CurrentCulture) Then Return "List Cancel Request"
            If MessageTag.Equals("l", StringComparison.CurrentCulture) Then Return "Bid Response"
            If MessageTag.Equals("L", StringComparison.CurrentCulture) Then Return "List Execute"
            If MessageTag.Equals("m", StringComparison.CurrentCulture) Then Return "List Strike Price"
            If MessageTag.Equals("M", StringComparison.CurrentCulture) Then Return "List Status Request"
            If MessageTag.Equals("N", StringComparison.CurrentCulture) Then Return "List Status"
            If MessageTag.Equals("P", StringComparison.CurrentCulture) Then Return "Allocation ACK"
            If MessageTag.Equals("Q", StringComparison.CurrentCulture) Then Return "Don't Know Trade"
            If MessageTag.Equals("R", StringComparison.CurrentCulture) Then Return "Quote Request"
            If MessageTag.Equals("S", StringComparison.CurrentCulture) Then Return "Quote"
            If MessageTag.Equals("T", StringComparison.CurrentCulture) Then Return "Settlement Instructions"
            If MessageTag.Equals("V", StringComparison.CurrentCulture) Then Return "Market Data Request"
            If MessageTag.Equals("W", StringComparison.CurrentCulture) Then Return "Market Data - Snapshot/Full Refresh"
            If MessageTag.Equals("X", StringComparison.CurrentCulture) Then Return "Market Data - Incremental Refresh"
            If MessageTag.Equals("Y", StringComparison.CurrentCulture) Then Return "Market Data Request Reject"
            If MessageTag.Equals("Z", StringComparison.CurrentCulture) Then Return "Quote Cancel"

        End If

    End Function

    Private Function GetFieldName(ByVal VersionId As String, ByVal FieldTag As String) As String


        If VersionId = "FIXT.1.1" Then

            If FieldTag = "8" Then Return "BeginString"
            If FieldTag = "9" Then Return "BodyLength"
            If FieldTag = "34" Then Return "MsgSeqNum"
            If FieldTag = "35" Then Return "MsgType"
            If FieldTag = "43" Then Return "PossDupFlag"
            If FieldTag = "49" Then Return "SenderCompID"
            If FieldTag = "50" Then Return "SenderSubID"
            If FieldTag = "52" Then Return "SendingTime"
            If FieldTag = "56" Then Return "TargetCompID"
            If FieldTag = "57" Then Return "TargetSubID"
            If FieldTag = "90" Then Return "SecureDataLen"
            If FieldTag = "91" Then Return "SecureData"
            If FieldTag = "97" Then Return "PossResend"
            If FieldTag = "115" Then Return "OnBehalfOfCompID"
            If FieldTag = "116" Then Return "OnBehalfOfSubID"
            If FieldTag = "122" Then Return "OrigSendingTime"
            If FieldTag = "128" Then Return "DeliverToCompID"
            If FieldTag = "129" Then Return "DeliverToSubID"
            If FieldTag = "142" Then Return "SenderLocationID"
            If FieldTag = "143" Then Return "TargetLocationID"
            If FieldTag = "144" Then Return "OnBehalfOfLocationID"
            If FieldTag = "145" Then Return "DeliverToLocationID"
            If FieldTag = "212" Then Return "XmlDataLen"
            If FieldTag = "213" Then Return "XmlData"
            If FieldTag = "347" Then Return "MessageEncoding"
            If FieldTag = "369" Then Return "LastMsgSeqNumProcessed"
            If FieldTag = "627" Then Return "NoHops"
            If FieldTag = "628" Then Return "HopCompID"
            If FieldTag = "629" Then Return "HopSendingTime"
            If FieldTag = "630" Then Return "HopRefID"
            If FieldTag = "1128" Then Return "ApplVerID"


            If FieldTag = "1" Then Return "Account"
            If FieldTag = "2" Then Return "AdvId"
            If FieldTag = "3" Then Return "AdvRefID"
            If FieldTag = "4" Then Return "AdvSide"
            If FieldTag = "5" Then Return "AdvTransType"
            If FieldTag = "6" Then Return "AvgPx"
            If FieldTag = "7" Then Return "BeginSeqNo"
            If FieldTag = "11" Then Return "ClOrdID"
            If FieldTag = "12" Then Return "Commission"
            If FieldTag = "13" Then Return "CommType"
            If FieldTag = "14" Then Return "CumQty"
            If FieldTag = "15" Then Return "Currency"
            If FieldTag = "16" Then Return "EndSeqNo"
            If FieldTag = "17" Then Return "ExecID"
            If FieldTag = "18" Then Return "ExecInst"
            If FieldTag = "19" Then Return "ExecRefID"
            If FieldTag = "20" Then Return "ExecTransType"
            If FieldTag = "21" Then Return "HandlInst"
            If FieldTag = "22" Then Return "SecurityIDSource"
            If FieldTag = "23" Then Return "IOIID"
            If FieldTag = "24" Then Return "IOIOthSvc (no longer used)"
            If FieldTag = "25" Then Return "IOIQltyInd"
            If FieldTag = "26" Then Return "IOIRefID"
            If FieldTag = "27" Then Return "IOIQty"
            If FieldTag = "28" Then Return "IOITransType"
            If FieldTag = "29" Then Return "LastCapacity"
            If FieldTag = "30" Then Return "LastMkt"
            If FieldTag = "31" Then Return "LastPx"
            If FieldTag = "32" Then Return "LastQty"
            If FieldTag = "33" Then Return "NoLinesOfText"
            If FieldTag = "36" Then Return "NewSeqNo"
            If FieldTag = "37" Then Return "OrderID"
            If FieldTag = "38" Then Return "OrderQty"
            If FieldTag = "39" Then Return "OrdStatus"
            If FieldTag = "40" Then Return "OrdType"
            If FieldTag = "41" Then Return "OrigClOrdID"
            If FieldTag = "42" Then Return "OrigTime"
            If FieldTag = "44" Then Return "Price"
            If FieldTag = "45" Then Return "RefSeqNum"
            If FieldTag = "46" Then Return "RelatdSym (no longer used)"
            If FieldTag = "47" Then Return "Rule80A(No Longer Used)"
            If FieldTag = "48" Then Return "SecurityID"
            If FieldTag = "51" Then Return "SendingDate (no longer used)"
            If FieldTag = "53" Then Return "Quantity"
            If FieldTag = "54" Then Return "Side"
            If FieldTag = "55" Then Return "Symbol"
            If FieldTag = "58" Then Return "Text"
            If FieldTag = "59" Then Return "TimeInForce"
            If FieldTag = "60" Then Return "TransactTime"
            If FieldTag = "61" Then Return "Urgency"
            If FieldTag = "62" Then Return "ValidUntilTime"
            If FieldTag = "63" Then Return "SettlType"
            If FieldTag = "64" Then Return "SettlDate"
            If FieldTag = "65" Then Return "SymbolSfx"
            If FieldTag = "66" Then Return "ListID"
            If FieldTag = "67" Then Return "ListSeqNo"
            If FieldTag = "68" Then Return "TotNoOrders"
            If FieldTag = "69" Then Return "ListExecInst"
            If FieldTag = "70" Then Return "AllocID"
            If FieldTag = "71" Then Return "AllocTransType"
            If FieldTag = "72" Then Return "RefAllocID"
            If FieldTag = "73" Then Return "NoOrders"
            If FieldTag = "74" Then Return "AvgPxPrecision"
            If FieldTag = "75" Then Return "TradeDate"
            If FieldTag = "76" Then Return "ExecBroker"
            If FieldTag = "77" Then Return "PositionEffect"
            If FieldTag = "78" Then Return "NoAllocs"
            If FieldTag = "79" Then Return "AllocAccount"
            If FieldTag = "80" Then Return "AllocQty"
            If FieldTag = "81" Then Return "ProcessCode"
            If FieldTag = "82" Then Return "NoRpts"
            If FieldTag = "83" Then Return "RptSeq"
            If FieldTag = "84" Then Return "CxlQty"
            If FieldTag = "85" Then Return "NoDlvyInst"
            If FieldTag = "86" Then Return "DlvyInst"
            If FieldTag = "87" Then Return "AllocStatus"
            If FieldTag = "88" Then Return "AllocRejCode"
            If FieldTag = "92" Then Return "BrokerOfCredit"
            If FieldTag = "94" Then Return "EmailType"
            If FieldTag = "95" Then Return "RawDataLength"
            If FieldTag = "96" Then Return "RawData"
            If FieldTag = "98" Then Return "EncryptMethod"
            If FieldTag = "99" Then Return "StopPx"
            If FieldTag = "100" Then Return "ExDestination"
            If FieldTag = "101" Then Return "(Not Defined)"
            If FieldTag = "102" Then Return "CxlRejReason"
            If FieldTag = "103" Then Return "OrdRejReason"
            If FieldTag = "104" Then Return "IOIQualifier"
            If FieldTag = "105" Then Return "WaveNo"
            If FieldTag = "106" Then Return "Issuer"
            If FieldTag = "107" Then Return "SecurityDesc"
            If FieldTag = "108" Then Return "HeartBtInt"
            If FieldTag = "109" Then Return "ClientID"
            If FieldTag = "110" Then Return "MinQty"
            If FieldTag = "111" Then Return "MaxFloor"
            If FieldTag = "112" Then Return "TestReqID"
            If FieldTag = "113" Then Return "ReportToExch"
            If FieldTag = "114" Then Return "LocateReqd"
            If FieldTag = "117" Then Return "QuoteID"
            If FieldTag = "118" Then Return "NetMoney"
            If FieldTag = "119" Then Return "SettlCurrAmt"
            If FieldTag = "120" Then Return "SettlCurrency"
            If FieldTag = "121" Then Return "ForexReq"
            If FieldTag = "123" Then Return "GapFillFlag"
            If FieldTag = "124" Then Return "NoExecs"
            If FieldTag = "125" Then Return "CxlType"
            If FieldTag = "126" Then Return "ExpireTime"
            If FieldTag = "127" Then Return "DKReason"
            If FieldTag = "130" Then Return "IOINaturalFlag"
            If FieldTag = "131" Then Return "QuoteReqID"
            If FieldTag = "132" Then Return "BidPx"
            If FieldTag = "133" Then Return "OfferPx"
            If FieldTag = "134" Then Return "BidSize"
            If FieldTag = "135" Then Return "OfferSize"
            If FieldTag = "136" Then Return "NoMiscFees"
            If FieldTag = "137" Then Return "MiscFeeAmt"
            If FieldTag = "138" Then Return "MiscFeeCurr"
            If FieldTag = "139" Then Return "MiscFeeType"
            If FieldTag = "140" Then Return "PrevClosePx"
            If FieldTag = "141" Then Return "ResetSeqNumFlag"
            If FieldTag = "146" Then Return "NoRelatedSym"
            If FieldTag = "147" Then Return "Subject"
            If FieldTag = "148" Then Return "Headline"
            If FieldTag = "149" Then Return "URLLink"
            If FieldTag = "150" Then Return "ExecType"
            If FieldTag = "151" Then Return "LeavesQty"
            If FieldTag = "152" Then Return "CashOrderQty"
            If FieldTag = "153" Then Return "AllocAvgPx"
            If FieldTag = "154" Then Return "AllocNetMoney"
            If FieldTag = "155" Then Return "SettlCurrFxRate"
            If FieldTag = "156" Then Return "SettlCurrFxRateCalc"
            If FieldTag = "157" Then Return "NumDaysInterest"
            If FieldTag = "158" Then Return "AccruedInterestRate"
            If FieldTag = "159" Then Return "AccruedInterestAmt"
            If FieldTag = "160" Then Return "SettlInstMode"
            If FieldTag = "161" Then Return "AllocText"
            If FieldTag = "162" Then Return "SettlInstID"
            If FieldTag = "163" Then Return "SettlInstTransType"
            If FieldTag = "164" Then Return "EmailThreadID"
            If FieldTag = "165" Then Return "SettlInstSource"
            If FieldTag = "166" Then Return "SettlLocation"
            If FieldTag = "167" Then Return "SecurityType"
            If FieldTag = "168" Then Return "EffectiveTime"
            If FieldTag = "169" Then Return "StandInstDbType"
            If FieldTag = "170" Then Return "StandInstDbName"
            If FieldTag = "171" Then Return "StandInstDbID"
            If FieldTag = "172" Then Return "SettlDeliveryType"
            If FieldTag = "173" Then Return "SettlDepositoryCode"
            If FieldTag = "174" Then Return "SettlBrkrCode"
            If FieldTag = "175" Then Return "SettlInstCode"
            If FieldTag = "176" Then Return "SecuritySettlAgentName"
            If FieldTag = "177" Then Return "SecuritySettlAgentCode"
            If FieldTag = "178" Then Return "SecuritySettlAgentAcctNum"
            If FieldTag = "179" Then Return "SecuritySettlAgentAcctName"
            If FieldTag = "180" Then Return "SecuritySettlAgentContactName"
            If FieldTag = "181" Then Return "SecuritySettlAgentContactPhone"
            If FieldTag = "182" Then Return "CashSettlAgentName"
            If FieldTag = "183" Then Return "CashSettlAgentCode"
            If FieldTag = "184" Then Return "CashSettlAgentAcctNum"
            If FieldTag = "185" Then Return "CashSettlAgentAcctName"
            If FieldTag = "186" Then Return "CashSettlAgentContactName"
            If FieldTag = "187" Then Return "CashSettlAgentContactPhone"
            If FieldTag = "188" Then Return "BidSpotRate"
            If FieldTag = "189" Then Return "BidForwardPoints"
            If FieldTag = "190" Then Return "OfferSpotRate"
            If FieldTag = "191" Then Return "OfferForwardPoints"
            If FieldTag = "192" Then Return "OrderQty2"
            If FieldTag = "193" Then Return "SettlDate2"
            If FieldTag = "194" Then Return "LastSpotRate"
            If FieldTag = "195" Then Return "LastForwardPoints"
            If FieldTag = "196" Then Return "AllocLinkID"
            If FieldTag = "197" Then Return "AllocLinkType"
            If FieldTag = "198" Then Return "SecondaryOrderID"
            If FieldTag = "199" Then Return "NoIOIQualifiers"
            If FieldTag = "200" Then Return "MaturityMonthYear"
            If FieldTag = "201" Then Return "PutOrCall"
            If FieldTag = "202" Then Return "StrikePrice"
            If FieldTag = "203" Then Return "CoveredOrUncovered"
            If FieldTag = "204" Then Return "CustomerOrFirm"
            If FieldTag = "205" Then Return "MaturityDay"
            If FieldTag = "206" Then Return "OptAttribute"
            If FieldTag = "207" Then Return "SecurityExchange"
            If FieldTag = "208" Then Return "NotifyBrokerOfCredit"
            If FieldTag = "209" Then Return "AllocHandlInst"
            If FieldTag = "210" Then Return "MaxShow"
            If FieldTag = "211" Then Return "PegOffsetValue"
            If FieldTag = "214" Then Return "SettlInstRefID"
            If FieldTag = "215" Then Return "NoRoutingIDs"
            If FieldTag = "216" Then Return "RoutingType"
            If FieldTag = "217" Then Return "RoutingID"
            If FieldTag = "218" Then Return "Spread"
            If FieldTag = "219" Then Return "Benchmark"
            If FieldTag = "220" Then Return "BenchmarkCurveCurrency"
            If FieldTag = "221" Then Return "BenchmarkCurveName"
            If FieldTag = "222" Then Return "BenchmarkCurvePoint"
            If FieldTag = "223" Then Return "CouponRate"
            If FieldTag = "224" Then Return "CouponPaymentDate"
            If FieldTag = "225" Then Return "IssueDate"
            If FieldTag = "226" Then Return "RepurchaseTerm"
            If FieldTag = "227" Then Return "RepurchaseRate"
            If FieldTag = "228" Then Return "Factor"
            If FieldTag = "229" Then Return "TradeOriginationDate"
            If FieldTag = "230" Then Return "ExDate"
            If FieldTag = "231" Then Return "ContractMultiplier"
            If FieldTag = "232" Then Return "NoStipulations"
            If FieldTag = "233" Then Return "StipulationType"
            If FieldTag = "234" Then Return "StipulationValue"
            If FieldTag = "235" Then Return "YieldType"
            If FieldTag = "236" Then Return "Yield"
            If FieldTag = "237" Then Return "TotalTakedown"
            If FieldTag = "238" Then Return "Concession"
            If FieldTag = "239" Then Return "RepoCollateralSecurityType"
            If FieldTag = "240" Then Return "RedemptionDate"
            If FieldTag = "241" Then Return "UnderlyingCouponPaymentDate"
            If FieldTag = "242" Then Return "UnderlyingIssueDate"
            If FieldTag = "243" Then Return "UnderlyingRepoCollateralSecurityType"
            If FieldTag = "244" Then Return "UnderlyingRepurchaseTerm"
            If FieldTag = "245" Then Return "UnderlyingRepurchaseRate"
            If FieldTag = "246" Then Return "UnderlyingFactor"
            If FieldTag = "247" Then Return "UnderlyingRedemptionDate"
            If FieldTag = "248" Then Return "LegCouponPaymentDate"
            If FieldTag = "249" Then Return "LegIssueDate"
            If FieldTag = "250" Then Return "LegRepoCollateralSecurityType"
            If FieldTag = "251" Then Return "LegRepurchaseTerm"
            If FieldTag = "252" Then Return "LegRepurchaseRate"
            If FieldTag = "253" Then Return "LegFactor"
            If FieldTag = "254" Then Return "LegRedemptionDate"
            If FieldTag = "255" Then Return "CreditRating"
            If FieldTag = "256" Then Return "UnderlyingCreditRating"
            If FieldTag = "257" Then Return "LegCreditRating"
            If FieldTag = "258" Then Return "TradedFlatSwitch"
            If FieldTag = "259" Then Return "BasisFeatureDate"
            If FieldTag = "260" Then Return "BasisFeaturePrice"
            If FieldTag = "261" Then Return "Reserved/Allocated to the Fixed Income proposal"
            If FieldTag = "262" Then Return "MDReqID"
            If FieldTag = "263" Then Return "SubscriptionRequestType"
            If FieldTag = "264" Then Return "MarketDepth"
            If FieldTag = "265" Then Return "MDUpdateType"
            If FieldTag = "266" Then Return "AggregatedBook"
            If FieldTag = "267" Then Return "NoMDEntryTypes"
            If FieldTag = "268" Then Return "NoMDEntries"
            If FieldTag = "269" Then Return "MDEntryType"
            If FieldTag = "270" Then Return "MDEntryPx"
            If FieldTag = "271" Then Return "MDEntrySize"
            If FieldTag = "272" Then Return "MDEntryDate"
            If FieldTag = "273" Then Return "MDEntryTime"
            If FieldTag = "274" Then Return "TickDirection"
            If FieldTag = "275" Then Return "MDMkt"
            If FieldTag = "276" Then Return "QuoteCondition"
            If FieldTag = "277" Then Return "TradeCondition"
            If FieldTag = "278" Then Return "MDEntryID"
            If FieldTag = "279" Then Return "MDUpdateAction"
            If FieldTag = "280" Then Return "MDEntryRefID"
            If FieldTag = "281" Then Return "MDReqRejReason"
            If FieldTag = "282" Then Return "MDEntryOriginator"
            If FieldTag = "283" Then Return "LocationID"
            If FieldTag = "284" Then Return "DeskID"
            If FieldTag = "285" Then Return "DeleteReason"
            If FieldTag = "286" Then Return "OpenCloseSettlFlag"
            If FieldTag = "287" Then Return "SellerDays"
            If FieldTag = "288" Then Return "MDEntryBuyer"
            If FieldTag = "289" Then Return "MDEntrySeller"
            If FieldTag = "290" Then Return "MDEntryPositionNo"
            If FieldTag = "291" Then Return "FinancialStatus"
            If FieldTag = "292" Then Return "CorporateAction"
            If FieldTag = "293" Then Return "DefBidSize"
            If FieldTag = "294" Then Return "DefOfferSize"
            If FieldTag = "295" Then Return "NoQuoteEntries"
            If FieldTag = "296" Then Return "NoQuoteSets"
            If FieldTag = "297" Then Return "QuoteStatus"
            If FieldTag = "298" Then Return "QuoteCancelType"
            If FieldTag = "299" Then Return "QuoteEntryID"
            If FieldTag = "300" Then Return "QuoteRejectReason"
            If FieldTag = "301" Then Return "QuoteResponseLevel"
            If FieldTag = "302" Then Return "QuoteSetID"
            If FieldTag = "303" Then Return "QuoteRequestType"
            If FieldTag = "304" Then Return "TotNoQuoteEntries"
            If FieldTag = "305" Then Return "UnderlyingSecurityIDSource"
            If FieldTag = "306" Then Return "UnderlyingIssuer"
            If FieldTag = "307" Then Return "UnderlyingSecurityDesc"
            If FieldTag = "308" Then Return "UnderlyingSecurityExchange"
            If FieldTag = "309" Then Return "UnderlyingSecurityID"
            If FieldTag = "310" Then Return "UnderlyingSecurityType"
            If FieldTag = "311" Then Return "UnderlyingSymbol"
            If FieldTag = "312" Then Return "UnderlyingSymbolSfx"
            If FieldTag = "313" Then Return "UnderlyingMaturityMonthYear"
            If FieldTag = "314" Then Return "UnderlyingMaturityDay"
            If FieldTag = "315" Then Return "UnderlyingPutOrCall"
            If FieldTag = "316" Then Return "UnderlyingStrikePrice"
            If FieldTag = "317" Then Return "UnderlyingOptAttribute"
            If FieldTag = "318" Then Return "UnderlyingCurrency"
            If FieldTag = "319" Then Return "RatioQty"
            If FieldTag = "320" Then Return "SecurityReqID"
            If FieldTag = "321" Then Return "SecurityRequestType"
            If FieldTag = "322" Then Return "SecurityResponseID"
            If FieldTag = "323" Then Return "SecurityResponseType"
            If FieldTag = "324" Then Return "SecurityStatusReqID"
            If FieldTag = "325" Then Return "UnsolicitedIndicator"
            If FieldTag = "326" Then Return "SecurityTradingStatus"
            If FieldTag = "327" Then Return "HaltReason"
            If FieldTag = "328" Then Return "InViewOfCommon"
            If FieldTag = "329" Then Return "DueToRelated"
            If FieldTag = "330" Then Return "BuyVolume"
            If FieldTag = "331" Then Return "SellVolume"
            If FieldTag = "332" Then Return "HighPx"
            If FieldTag = "333" Then Return "LowPx"
            If FieldTag = "334" Then Return "Adjustment"
            If FieldTag = "335" Then Return "TradSesReqID"
            If FieldTag = "336" Then Return "TradingSessionID"
            If FieldTag = "337" Then Return "ContraTrader"
            If FieldTag = "338" Then Return "TradSesMethod"
            If FieldTag = "339" Then Return "TradSesMode"
            If FieldTag = "340" Then Return "TradSesStatus"
            If FieldTag = "341" Then Return "TradSesStartTime"
            If FieldTag = "342" Then Return "TradSesOpenTime"
            If FieldTag = "343" Then Return "TradSesPreCloseTime"
            If FieldTag = "344" Then Return "TradSesCloseTime"
            If FieldTag = "345" Then Return "TradSesEndTime"
            If FieldTag = "346" Then Return "NumberOfOrders"
            If FieldTag = "348" Then Return "EncodedIssuerLen"
            If FieldTag = "349" Then Return "EncodedIssuer"
            If FieldTag = "350" Then Return "EncodedSecurityDescLen"
            If FieldTag = "351" Then Return "EncodedSecurityDesc"
            If FieldTag = "352" Then Return "EncodedListExecInstLen"
            If FieldTag = "353" Then Return "EncodedListExecInst"
            If FieldTag = "354" Then Return "EncodedTextLen"
            If FieldTag = "355" Then Return "EncodedText"
            If FieldTag = "356" Then Return "EncodedSubjectLen"
            If FieldTag = "357" Then Return "EncodedSubject"
            If FieldTag = "358" Then Return "EncodedHeadlineLen"
            If FieldTag = "359" Then Return "EncodedHeadline"
            If FieldTag = "360" Then Return "EncodedAllocTextLen"
            If FieldTag = "361" Then Return "EncodedAllocText"
            If FieldTag = "362" Then Return "EncodedUnderlyingIssuerLen"
            If FieldTag = "363" Then Return "EncodedUnderlyingIssuer"
            If FieldTag = "364" Then Return "EncodedUnderlyingSecurityDescLen"
            If FieldTag = "365" Then Return "EncodedUnderlyingSecurityDesc"
            If FieldTag = "366" Then Return "AllocPrice"
            If FieldTag = "367" Then Return "QuoteSetValidUntilTime"
            If FieldTag = "368" Then Return "QuoteEntryRejectReason"
            If FieldTag = "370" Then Return "OnBehalfOfSendingTime"
            If FieldTag = "371" Then Return "RefTagID"
            If FieldTag = "372" Then Return "RefMsgType"
            If FieldTag = "373" Then Return "SessionRejectReason"
            If FieldTag = "374" Then Return "BidRequestTransType"
            If FieldTag = "375" Then Return "ContraBroker"
            If FieldTag = "376" Then Return "ComplianceID"
            If FieldTag = "377" Then Return "SolicitedFlag"
            If FieldTag = "378" Then Return "ExecRestatementReason"
            If FieldTag = "379" Then Return "BusinessRejectRefID"
            If FieldTag = "380" Then Return "BusinessRejectReason"
            If FieldTag = "381" Then Return "GrossTradeAmt"
            If FieldTag = "382" Then Return "NoContraBrokers"
            If FieldTag = "383" Then Return "MaxMessageSize"
            If FieldTag = "384" Then Return "NoMsgTypes"
            If FieldTag = "385" Then Return "MsgDirection"
            If FieldTag = "386" Then Return "NoTradingSessions"
            If FieldTag = "387" Then Return "TotalVolumeTraded"
            If FieldTag = "388" Then Return "DiscretionInst"
            If FieldTag = "389" Then Return "DiscretionOffsetValue"
            If FieldTag = "390" Then Return "BidID"
            If FieldTag = "391" Then Return "ClientBidID"
            If FieldTag = "392" Then Return "ListName"
            If FieldTag = "393" Then Return "TotNoRelatedSym"
            If FieldTag = "394" Then Return "BidType"
            If FieldTag = "395" Then Return "NumTickets"
            If FieldTag = "396" Then Return "SideValue1"
            If FieldTag = "397" Then Return "SideValue2"
            If FieldTag = "398" Then Return "NoBidDescriptors"
            If FieldTag = "399" Then Return "BidDescriptorType"
            If FieldTag = "400" Then Return "BidDescriptor"
            If FieldTag = "401" Then Return "SideValueInd"
            If FieldTag = "402" Then Return "LiquidityPctLow"
            If FieldTag = "403" Then Return "LiquidityPctHigh"
            If FieldTag = "404" Then Return "LiquidityValue"
            If FieldTag = "405" Then Return "EFPTrackingError"
            If FieldTag = "406" Then Return "FairValue"
            If FieldTag = "407" Then Return "OutsideIndexPct"
            If FieldTag = "408" Then Return "ValueOfFutures"
            If FieldTag = "409" Then Return "LiquidityIndType"
            If FieldTag = "410" Then Return "WtAverageLiquidity"
            If FieldTag = "411" Then Return "ExchangeForPhysical"
            If FieldTag = "412" Then Return "OutMainCntryUIndex"
            If FieldTag = "413" Then Return "CrossPercent"
            If FieldTag = "414" Then Return "ProgRptReqs"
            If FieldTag = "415" Then Return "ProgPeriodInterval"
            If FieldTag = "416" Then Return "IncTaxInd"
            If FieldTag = "417" Then Return "NumBidders"
            If FieldTag = "418" Then Return "BidTradeType"
            If FieldTag = "419" Then Return "BasisPxType"
            If FieldTag = "420" Then Return "NoBidComponents"
            If FieldTag = "421" Then Return "Country"
            If FieldTag = "422" Then Return "TotNoStrikes"
            If FieldTag = "423" Then Return "PriceType"
            If FieldTag = "424" Then Return "DayOrderQty"
            If FieldTag = "425" Then Return "DayCumQty"
            If FieldTag = "426" Then Return "DayAvgPx"
            If FieldTag = "427" Then Return "GTBookingInst"
            If FieldTag = "428" Then Return "NoStrikes"
            If FieldTag = "429" Then Return "ListStatusType"
            If FieldTag = "430" Then Return "NetGrossInd"
            If FieldTag = "431" Then Return "ListOrderStatus"
            If FieldTag = "432" Then Return "ExpireDate"
            If FieldTag = "433" Then Return "ListExecInstType"
            If FieldTag = "434" Then Return "CxlRejResponseTo"
            If FieldTag = "435" Then Return "UnderlyingCouponRate"
            If FieldTag = "436" Then Return "UnderlyingContractMultiplier"
            If FieldTag = "437" Then Return "ContraTradeQty"
            If FieldTag = "438" Then Return "ContraTradeTime"
            If FieldTag = "439" Then Return "ClearingFirm"
            If FieldTag = "440" Then Return "ClearingAccount"
            If FieldTag = "441" Then Return "LiquidityNumSecurities"
            If FieldTag = "442" Then Return "MultiLegReportingType"
            If FieldTag = "443" Then Return "StrikeTime"
            If FieldTag = "444" Then Return "ListStatusText"
            If FieldTag = "445" Then Return "EncodedListStatusTextLen"
            If FieldTag = "446" Then Return "EncodedListStatusText"
            If FieldTag = "447" Then Return "PartyIDSource"
            If FieldTag = "448" Then Return "PartyID"
            If FieldTag = "449" Then Return "TotalVolumeTradedDate"
            If FieldTag = "450" Then Return "TotalVolumeTraded Time"
            If FieldTag = "451" Then Return "NetChgPrevDay"
            If FieldTag = "452" Then Return "PartyRole"
            If FieldTag = "453" Then Return "NoPartyIDs"
            If FieldTag = "454" Then Return "NoSecurityAltID"
            If FieldTag = "455" Then Return "SecurityAltID"
            If FieldTag = "456" Then Return "SecurityAltIDSource"
            If FieldTag = "457" Then Return "NoUnderlyingSecurityAltID"
            If FieldTag = "458" Then Return "UnderlyingSecurityAltID"
            If FieldTag = "459" Then Return "UnderlyingSecurityAltIDSource"
            If FieldTag = "460" Then Return "Product"
            If FieldTag = "461" Then Return "CFICode"
            If FieldTag = "462" Then Return "UnderlyingProduct"
            If FieldTag = "463" Then Return "UnderlyingCFICode"
            If FieldTag = "464" Then Return "TestMessageIndicator"
            If FieldTag = "465" Then Return "QuantityType"
            If FieldTag = "466" Then Return "BookingRefID"
            If FieldTag = "467" Then Return "IndividualAllocID"
            If FieldTag = "468" Then Return "RoundingDirection"
            If FieldTag = "469" Then Return "RoundingModulus"
            If FieldTag = "470" Then Return "CountryOfIssue"
            If FieldTag = "471" Then Return "StateOrProvinceOfIssue"
            If FieldTag = "472" Then Return "LocaleOfIssue"
            If FieldTag = "473" Then Return "NoRegistDtls"
            If FieldTag = "474" Then Return "MailingDtls"
            If FieldTag = "475" Then Return "InvestorCountryOfResidence"
            If FieldTag = "476" Then Return "PaymentRef"
            If FieldTag = "477" Then Return "DistribPaymentMethod"
            If FieldTag = "478" Then Return "CashDistribCurr"
            If FieldTag = "479" Then Return "CommCurrency"
            If FieldTag = "480" Then Return "CancellationRights"
            If FieldTag = "481" Then Return "MoneyLaunderingStatus"
            If FieldTag = "482" Then Return "MailingInst"
            If FieldTag = "483" Then Return "TransBkdTime"
            If FieldTag = "484" Then Return "ExecPriceType"
            If FieldTag = "485" Then Return "ExecPriceAdjustment"
            If FieldTag = "486" Then Return "DateOfBirth"
            If FieldTag = "487" Then Return "TradeReportTransType"
            If FieldTag = "488" Then Return "CardHolderName"
            If FieldTag = "489" Then Return "CardNumber"
            If FieldTag = "490" Then Return "CardExpDate"
            If FieldTag = "491" Then Return "CardIssNum"
            If FieldTag = "492" Then Return "PaymentMethod"
            If FieldTag = "493" Then Return "RegistAcctType"
            If FieldTag = "494" Then Return "Designation"
            If FieldTag = "495" Then Return "TaxAdvantageType"
            If FieldTag = "496" Then Return "RegistRejReasonText"
            If FieldTag = "497" Then Return "FundRenewWaiv"
            If FieldTag = "498" Then Return "CashDistribAgentName"
            If FieldTag = "499" Then Return "CashDistribAgentCode"
            If FieldTag = "500" Then Return "CashDistribAgentAcctNumber"
            If FieldTag = "501" Then Return "CashDistribPayRef"
            If FieldTag = "502" Then Return "CashDistribAgentAcctName"
            If FieldTag = "503" Then Return "CardStartDate"
            If FieldTag = "504" Then Return "PaymentDate"
            If FieldTag = "505" Then Return "PaymentRemitterID"
            If FieldTag = "506" Then Return "RegistStatus"
            If FieldTag = "507" Then Return "RegistRejReasonCode"
            If FieldTag = "508" Then Return "RegistRefID"
            If FieldTag = "509" Then Return "RegistDtls"
            If FieldTag = "510" Then Return "NoDistribInsts"
            If FieldTag = "511" Then Return "RegistEmail"
            If FieldTag = "512" Then Return "DistribPercentage"
            If FieldTag = "513" Then Return "RegistID"
            If FieldTag = "514" Then Return "RegistTransType"
            If FieldTag = "515" Then Return "ExecValuationPoint"
            If FieldTag = "516" Then Return "OrderPercent"
            If FieldTag = "517" Then Return "OwnershipType"
            If FieldTag = "518" Then Return "NoContAmts"
            If FieldTag = "519" Then Return "ContAmtType"
            If FieldTag = "520" Then Return "ContAmtValue"
            If FieldTag = "521" Then Return "ContAmtCurr"
            If FieldTag = "522" Then Return "OwnerType"
            If FieldTag = "523" Then Return "PartySubID"
            If FieldTag = "524" Then Return "NestedPartyID"
            If FieldTag = "525" Then Return "NestedPartyIDSource"
            If FieldTag = "526" Then Return "SecondaryClOrdID"
            If FieldTag = "527" Then Return "SecondaryExecID"
            If FieldTag = "528" Then Return "OrderCapacity"
            If FieldTag = "529" Then Return "OrderRestrictions"
            If FieldTag = "530" Then Return "MassCancelRequestType"
            If FieldTag = "531" Then Return "MassCancelResponse"
            If FieldTag = "532" Then Return "MassCancelRejectReason"
            If FieldTag = "533" Then Return "TotalAffectedOrders"
            If FieldTag = "534" Then Return "NoAffectedOrders"
            If FieldTag = "535" Then Return "AffectedOrderID"
            If FieldTag = "536" Then Return "AffectedSecondaryOrderID"
            If FieldTag = "537" Then Return "QuoteType"
            If FieldTag = "538" Then Return "NestedPartyRole"
            If FieldTag = "539" Then Return "NoNestedPartyIDs"
            If FieldTag = "540" Then Return "TotalAccruedInterestAmt"
            If FieldTag = "541" Then Return "MaturityDate"
            If FieldTag = "542" Then Return "UnderlyingMaturityDate"
            If FieldTag = "543" Then Return "InstrRegistry"
            If FieldTag = "544" Then Return "CashMargin"
            If FieldTag = "545" Then Return "NestedPartySubID"
            If FieldTag = "546" Then Return "Scope"
            If FieldTag = "547" Then Return "MDImplicitDelete"
            If FieldTag = "548" Then Return "CrossID"
            If FieldTag = "549" Then Return "CrossType"
            If FieldTag = "550" Then Return "CrossPrioritization"
            If FieldTag = "551" Then Return "OrigCrossID"
            If FieldTag = "552" Then Return "NoSides"
            If FieldTag = "553" Then Return "Username"
            If FieldTag = "554" Then Return "Password"
            If FieldTag = "555" Then Return "NoLegs"
            If FieldTag = "556" Then Return "LegCurrency"
            If FieldTag = "557" Then Return "TotNoSecurityTypes"
            If FieldTag = "558" Then Return "NoSecurityTypes"
            If FieldTag = "559" Then Return "SecurityListRequestType"
            If FieldTag = "560" Then Return "SecurityRequestResult"
            If FieldTag = "561" Then Return "RoundLot"
            If FieldTag = "562" Then Return "MinTradeVol"
            If FieldTag = "563" Then Return "MultiLegRptTypeReq"
            If FieldTag = "564" Then Return "LegPositionEffect"
            If FieldTag = "565" Then Return "LegCoveredOrUncovered"
            If FieldTag = "566" Then Return "LegPrice"
            If FieldTag = "567" Then Return "TradSesStatusRejReason"
            If FieldTag = "568" Then Return "TradeRequestID"
            If FieldTag = "569" Then Return "TradeRequestType"
            If FieldTag = "570" Then Return "PreviouslyReported"
            If FieldTag = "571" Then Return "TradeReportID"
            If FieldTag = "572" Then Return "TradeReportRefID"
            If FieldTag = "573" Then Return "MatchStatus"
            If FieldTag = "574" Then Return "MatchType"
            If FieldTag = "575" Then Return "OddLot"
            If FieldTag = "576" Then Return "NoClearingInstructions"
            If FieldTag = "577" Then Return "ClearingInstruction"
            If FieldTag = "578" Then Return "TradeInputSource"
            If FieldTag = "579" Then Return "TradeInputDevice"
            If FieldTag = "580" Then Return "NoDates"
            If FieldTag = "581" Then Return "AccountType"
            If FieldTag = "582" Then Return "CustOrderCapacity"
            If FieldTag = "583" Then Return "ClOrdLinkID"
            If FieldTag = "584" Then Return "MassStatusReqID"
            If FieldTag = "585" Then Return "MassStatusReqType"
            If FieldTag = "586" Then Return "OrigOrdModTime"
            If FieldTag = "587" Then Return "LegSettlType"
            If FieldTag = "588" Then Return "LegSettlDate"
            If FieldTag = "589" Then Return "DayBookingInst"
            If FieldTag = "590" Then Return "BookingUnit"
            If FieldTag = "591" Then Return "PreallocMethod"
            If FieldTag = "592" Then Return "UnderlyingCountryOfIssue"
            If FieldTag = "593" Then Return "UnderlyingStateOrProvinceOfIssue"
            If FieldTag = "594" Then Return "UnderlyingLocaleOfIssue"
            If FieldTag = "595" Then Return "UnderlyingInstrRegistry"
            If FieldTag = "596" Then Return "LegCountryOfIssue"
            If FieldTag = "597" Then Return "LegStateOrProvinceOfIssue"
            If FieldTag = "598" Then Return "LegLocaleOfIssue"
            If FieldTag = "599" Then Return "LegInstrRegistry"
            If FieldTag = "600" Then Return "LegSymbol"
            If FieldTag = "601" Then Return "LegSymbolSfx"
            If FieldTag = "602" Then Return "LegSecurityID"
            If FieldTag = "603" Then Return "LegSecurityIDSource"
            If FieldTag = "604" Then Return "NoLegSecurityAltID"
            If FieldTag = "605" Then Return "LegSecurityAltID"
            If FieldTag = "606" Then Return "LegSecurityAltIDSource"
            If FieldTag = "607" Then Return "LegProduct"
            If FieldTag = "608" Then Return "LegCFICode"
            If FieldTag = "609" Then Return "LegSecurityType"
            If FieldTag = "610" Then Return "LegMaturityMonthYear"
            If FieldTag = "611" Then Return "LegMaturityDate"
            If FieldTag = "612" Then Return "LegStrikePrice"
            If FieldTag = "613" Then Return "LegOptAttribute"
            If FieldTag = "614" Then Return "LegContractMultiplier"
            If FieldTag = "615" Then Return "LegCouponRate"
            If FieldTag = "616" Then Return "LegSecurityExchange"
            If FieldTag = "617" Then Return "LegIssuer"
            If FieldTag = "618" Then Return "EncodedLegIssuerLen"
            If FieldTag = "619" Then Return "EncodedLegIssuer"
            If FieldTag = "620" Then Return "LegSecurityDesc"
            If FieldTag = "621" Then Return "EncodedLegSecurityDescLen"
            If FieldTag = "622" Then Return "EncodedLegSecurityDesc"
            If FieldTag = "623" Then Return "LegRatioQty"
            If FieldTag = "624" Then Return "LegSide"
            If FieldTag = "625" Then Return "TradingSessionSubID"
            If FieldTag = "626" Then Return "AllocType"
            If FieldTag = "631" Then Return "MidPx"
            If FieldTag = "632" Then Return "BidYield"
            If FieldTag = "633" Then Return "MidYield"
            If FieldTag = "634" Then Return "OfferYield"
            If FieldTag = "635" Then Return "ClearingFeeIndicator"
            If FieldTag = "636" Then Return "WorkingIndicator"
            If FieldTag = "637" Then Return "LegLastPx"
            If FieldTag = "638" Then Return "PriorityIndicator"
            If FieldTag = "639" Then Return "PriceImprovement"
            If FieldTag = "640" Then Return "Price2"
            If FieldTag = "641" Then Return "LastForwardPoints2"
            If FieldTag = "642" Then Return "BidForwardPoints2"
            If FieldTag = "643" Then Return "OfferForwardPoints2"
            If FieldTag = "644" Then Return "RFQReqID"
            If FieldTag = "645" Then Return "MktBidPx"
            If FieldTag = "646" Then Return "MktOfferPx"
            If FieldTag = "647" Then Return "MinBidSize"
            If FieldTag = "648" Then Return "MinOfferSize"
            If FieldTag = "649" Then Return "QuoteStatusReqID"
            If FieldTag = "650" Then Return "LegalConfirm"
            If FieldTag = "651" Then Return "UnderlyingLastPx"
            If FieldTag = "652" Then Return "UnderlyingLastQty"
            If FieldTag = "653" Then Return "SecDefStatus"
            If FieldTag = "654" Then Return "LegRefID"
            If FieldTag = "655" Then Return "ContraLegRefID"
            If FieldTag = "656" Then Return "SettlCurrBidFxRate"
            If FieldTag = "657" Then Return "SettlCurrOfferFxRate"
            If FieldTag = "658" Then Return "QuoteRequestRejectReason"
            If FieldTag = "659" Then Return "SideComplianceID"
            If FieldTag = "660" Then Return "AcctIDSource"
            If FieldTag = "661" Then Return "AllocAcctIDSource"
            If FieldTag = "662" Then Return "BenchmarkPrice"
            If FieldTag = "663" Then Return "BenchmarkPriceType"
            If FieldTag = "664" Then Return "ConfirmID"
            If FieldTag = "665" Then Return "ConfirmStatus"
            If FieldTag = "666" Then Return "ConfirmTransType"
            If FieldTag = "667" Then Return "ContractSettlMonth"
            If FieldTag = "668" Then Return "DeliveryForm"
            If FieldTag = "669" Then Return "LastParPx"
            If FieldTag = "670" Then Return "NoLegAllocs"
            If FieldTag = "671" Then Return "LegAllocAccount"
            If FieldTag = "672" Then Return "LegIndividualAllocID"
            If FieldTag = "673" Then Return "LegAllocQty"
            If FieldTag = "674" Then Return "LegAllocAcctIDSource"
            If FieldTag = "675" Then Return "LegSettlCurrency"
            If FieldTag = "676" Then Return "LegBenchmarkCurveCurrency"
            If FieldTag = "677" Then Return "LegBenchmarkCurveName"
            If FieldTag = "678" Then Return "LegBenchmarkCurvePoint"
            If FieldTag = "679" Then Return "LegBenchmarkPrice"
            If FieldTag = "680" Then Return "LegBenchmarkPriceType"
            If FieldTag = "681" Then Return "LegBidPx"
            If FieldTag = "682" Then Return "LegIOIQty"
            If FieldTag = "683" Then Return "NoLegStipulations"
            If FieldTag = "684" Then Return "LegOfferPx"
            If FieldTag = "685" Then Return "LegOrderQty"
            If FieldTag = "686" Then Return "LegPriceType"
            If FieldTag = "687" Then Return "LegQty"
            If FieldTag = "688" Then Return "LegStipulationType"
            If FieldTag = "689" Then Return "LegStipulationValue"
            If FieldTag = "690" Then Return "LegSwapType"
            If FieldTag = "691" Then Return "Pool"
            If FieldTag = "692" Then Return "QuotePriceType"
            If FieldTag = "693" Then Return "QuoteRespID"
            If FieldTag = "694" Then Return "QuoteRespType"
            If FieldTag = "695" Then Return "QuoteQualifier"
            If FieldTag = "696" Then Return "YieldRedemptionDate"
            If FieldTag = "697" Then Return "YieldRedemptionPrice"
            If FieldTag = "698" Then Return "YieldRedemptionPriceType"
            If FieldTag = "699" Then Return "BenchmarkSecurityID"
            If FieldTag = "700" Then Return "ReversalIndicator"
            If FieldTag = "701" Then Return "YieldCalcDate"
            If FieldTag = "702" Then Return "NoPositions"
            If FieldTag = "703" Then Return "PosType"
            If FieldTag = "704" Then Return "LongQty"
            If FieldTag = "705" Then Return "ShortQty"
            If FieldTag = "706" Then Return "PosQtyStatus"
            If FieldTag = "707" Then Return "PosAmtType"
            If FieldTag = "708" Then Return "PosAmt"
            If FieldTag = "709" Then Return "PosTransType"
            If FieldTag = "710" Then Return "PosReqID"
            If FieldTag = "711" Then Return "NoUnderlyings"
            If FieldTag = "712" Then Return "PosMaintAction"
            If FieldTag = "713" Then Return "OrigPosReqRefID"
            If FieldTag = "714" Then Return "PosMaintRptRefID"
            If FieldTag = "715" Then Return "ClearingBusinessDate"
            If FieldTag = "716" Then Return "SettlSessID"
            If FieldTag = "717" Then Return "SettlSessSubID"
            If FieldTag = "718" Then Return "AdjustmentType"
            If FieldTag = "719" Then Return "ContraryInstructionIndicator"
            If FieldTag = "720" Then Return "PriorSpreadIndicator"
            If FieldTag = "721" Then Return "PosMaintRptID"
            If FieldTag = "722" Then Return "PosMaintStatus"
            If FieldTag = "723" Then Return "PosMaintResult"
            If FieldTag = "724" Then Return "PosReqType"
            If FieldTag = "725" Then Return "ResponseTransportType"
            If FieldTag = "726" Then Return "ResponseDestination"
            If FieldTag = "727" Then Return "TotalNumPosReports"
            If FieldTag = "728" Then Return "PosReqResult"
            If FieldTag = "729" Then Return "PosReqStatus"
            If FieldTag = "730" Then Return "SettlPrice"
            If FieldTag = "731" Then Return "SettlPriceType"
            If FieldTag = "732" Then Return "UnderlyingSettlPrice"
            If FieldTag = "733" Then Return "UnderlyingSettlPriceType"
            If FieldTag = "734" Then Return "PriorSettlPrice"
            If FieldTag = "735" Then Return "NoQuoteQualifiers"
            If FieldTag = "736" Then Return "AllocSettlCurrency"
            If FieldTag = "737" Then Return "AllocSettlCurrAmt"
            If FieldTag = "738" Then Return "InterestAtMaturity"
            If FieldTag = "739" Then Return "LegDatedDate"
            If FieldTag = "740" Then Return "LegPool"
            If FieldTag = "741" Then Return "AllocInterestAtMaturity"
            If FieldTag = "742" Then Return "AllocAccruedInterestAmt"
            If FieldTag = "743" Then Return "DeliveryDate"
            If FieldTag = "744" Then Return "AssignmentMethod"
            If FieldTag = "745" Then Return "AssignmentUnit"
            If FieldTag = "746" Then Return "OpenInterest"
            If FieldTag = "747" Then Return "ExerciseMethod"
            If FieldTag = "748" Then Return "TotNumTradeReports"
            If FieldTag = "749" Then Return "TradeRequestResult"
            If FieldTag = "750" Then Return "TradeRequestStatus"
            If FieldTag = "751" Then Return "TradeReportRejectReason"
            If FieldTag = "752" Then Return "SideMultiLegReportingType"
            If FieldTag = "753" Then Return "NoPosAmt"
            If FieldTag = "754" Then Return "AutoAcceptIndicator"
            If FieldTag = "755" Then Return "AllocReportID"
            If FieldTag = "756" Then Return "NoNested2PartyIDs"
            If FieldTag = "757" Then Return "Nested2PartyID"
            If FieldTag = "758" Then Return "Nested2PartyIDSource"
            If FieldTag = "759" Then Return "Nested2PartyRole"
            If FieldTag = "760" Then Return "Nested2PartySubID"
            If FieldTag = "761" Then Return "BenchmarkSecurityIDSource"
            If FieldTag = "762" Then Return "SecuritySubType"
            If FieldTag = "763" Then Return "UnderlyingSecuritySubType"
            If FieldTag = "764" Then Return "LegSecuritySubType"
            If FieldTag = "765" Then Return "AllowableOneSidednessPct"
            If FieldTag = "766" Then Return "AllowableOneSidednessValue"
            If FieldTag = "767" Then Return "AllowableOneSidednessCurr"
            If FieldTag = "768" Then Return "NoTrdRegTimestamps"
            If FieldTag = "769" Then Return "TrdRegTimestamp"
            If FieldTag = "770" Then Return "TrdRegTimestampType"
            If FieldTag = "771" Then Return "TrdRegTimestampOrigin"
            If FieldTag = "772" Then Return "ConfirmRefID"
            If FieldTag = "773" Then Return "ConfirmType"
            If FieldTag = "774" Then Return "ConfirmRejReason"
            If FieldTag = "775" Then Return "BookingType"
            If FieldTag = "776" Then Return "IndividualAllocRejCode"
            If FieldTag = "777" Then Return "SettlInstMsgID"
            If FieldTag = "778" Then Return "NoSettlInst"
            If FieldTag = "779" Then Return "LastUpdateTime"
            If FieldTag = "780" Then Return "AllocSettlInstType"
            If FieldTag = "781" Then Return "NoSettlPartyIDs"
            If FieldTag = "782" Then Return "SettlPartyID"
            If FieldTag = "783" Then Return "SettlPartyIDSource"
            If FieldTag = "784" Then Return "SettlPartyRole"
            If FieldTag = "785" Then Return "SettlPartySubID"
            If FieldTag = "786" Then Return "SettlPartySubIDType"
            If FieldTag = "787" Then Return "DlvyInstType"
            If FieldTag = "788" Then Return "TerminationType"
            If FieldTag = "789" Then Return "NextExpectedMsgSeqNum"
            If FieldTag = "790" Then Return "OrdStatusReqID"
            If FieldTag = "791" Then Return "SettlInstReqID"
            If FieldTag = "792" Then Return "SettlInstReqRejCode"
            If FieldTag = "793" Then Return "SecondaryAllocID"
            If FieldTag = "794" Then Return "AllocReportType"
            If FieldTag = "795" Then Return "AllocReportRefID"
            If FieldTag = "796" Then Return "AllocCancReplaceReason"
            If FieldTag = "797" Then Return "CopyMsgIndicator"
            If FieldTag = "798" Then Return "AllocAccountType"
            If FieldTag = "799" Then Return "OrderAvgPx"
            If FieldTag = "800" Then Return "OrderBookingQty"
            If FieldTag = "801" Then Return "NoSettlPartySubIDs"
            If FieldTag = "802" Then Return "NoPartySubIDs"
            If FieldTag = "803" Then Return "PartySubIDType"
            If FieldTag = "804" Then Return "NoNestedPartySubIDs"
            If FieldTag = "805" Then Return "NestedPartySubIDType"
            If FieldTag = "806" Then Return "NoNested2PartySubIDs"
            If FieldTag = "807" Then Return "Nested2PartySubIDType"
            If FieldTag = "808" Then Return "AllocIntermedReqType"
            If FieldTag = "809" Then Return "NoUsernames"
            If FieldTag = "810" Then Return "UnderlyingPx"
            If FieldTag = "811" Then Return "PriceDelta"
            If FieldTag = "812" Then Return "ApplQueueMax"
            If FieldTag = "813" Then Return "ApplQueueDepth"
            If FieldTag = "814" Then Return "ApplQueueResolution"
            If FieldTag = "815" Then Return "ApplQueueAction"
            If FieldTag = "816" Then Return "NoAltMDSource"
            If FieldTag = "817" Then Return "AltMDSourceID"
            If FieldTag = "818" Then Return "SecondaryTradeReportID"
            If FieldTag = "819" Then Return "AvgPxIndicator"
            If FieldTag = "820" Then Return "TradeLinkID"
            If FieldTag = "821" Then Return "OrderInputDevice"
            If FieldTag = "822" Then Return "UnderlyingTradingSessionID"
            If FieldTag = "823" Then Return "UnderlyingTradingSessionSubID"
            If FieldTag = "824" Then Return "TradeLegRefID"
            If FieldTag = "825" Then Return "ExchangeRule"
            If FieldTag = "826" Then Return "TradeAllocIndicator"
            If FieldTag = "827" Then Return "ExpirationCycle"
            If FieldTag = "828" Then Return "TrdType"
            If FieldTag = "829" Then Return "TrdSubType"
            If FieldTag = "830" Then Return "TransferReason"
            If FieldTag = "831" Then Return "AsgnReqID"
            If FieldTag = "832" Then Return "TotNumAssignmentReports"
            If FieldTag = "833" Then Return "AsgnRptID"
            If FieldTag = "834" Then Return "ThresholdAmount"
            If FieldTag = "835" Then Return "PegMoveType"
            If FieldTag = "836" Then Return "PegOffsetType"
            If FieldTag = "837" Then Return "PegLimitType"
            If FieldTag = "838" Then Return "PegRoundDirection"
            If FieldTag = "839" Then Return "PeggedPrice"
            If FieldTag = "840" Then Return "PegScope"
            If FieldTag = "841" Then Return "DiscretionMoveType"
            If FieldTag = "842" Then Return "DiscretionOffsetType"
            If FieldTag = "843" Then Return "DiscretionLimitType"
            If FieldTag = "844" Then Return "DiscretionRoundDirection"
            If FieldTag = "845" Then Return "DiscretionPrice"
            If FieldTag = "846" Then Return "DiscretionScope"
            If FieldTag = "847" Then Return "TargetStrategy"
            If FieldTag = "848" Then Return "TargetStrategyParameters"
            If FieldTag = "849" Then Return "ParticipationRate"
            If FieldTag = "850" Then Return "TargetStrategyPerformance"
            If FieldTag = "851" Then Return "LastLiquidityInd"
            If FieldTag = "852" Then Return "PublishTrdIndicator"
            If FieldTag = "853" Then Return "ShortSaleReason"
            If FieldTag = "854" Then Return "QtyType"
            If FieldTag = "855" Then Return "SecondaryTrdType"
            If FieldTag = "856" Then Return "TradeReportType"
            If FieldTag = "857" Then Return "AllocNoOrdersType"
            If FieldTag = "858" Then Return "SharedCommission"
            If FieldTag = "859" Then Return "ConfirmReqID"
            If FieldTag = "860" Then Return "AvgParPx"
            If FieldTag = "861" Then Return "ReportedPx"
            If FieldTag = "862" Then Return "NoCapacities"
            If FieldTag = "863" Then Return "OrderCapacityQty"
            If FieldTag = "864" Then Return "NoEvents"
            If FieldTag = "865" Then Return "EventType"
            If FieldTag = "866" Then Return "EventDate"
            If FieldTag = "867" Then Return "EventPx"
            If FieldTag = "868" Then Return "EventText"
            If FieldTag = "869" Then Return "PctAtRisk"
            If FieldTag = "870" Then Return "NoInstrAttrib"
            If FieldTag = "871" Then Return "InstrAttribType"
            If FieldTag = "872" Then Return "InstrAttribValue"
            If FieldTag = "873" Then Return "DatedDate"
            If FieldTag = "874" Then Return "InterestAccrualDate"
            If FieldTag = "875" Then Return "CPProgram"
            If FieldTag = "876" Then Return "CPRegType"
            If FieldTag = "877" Then Return "UnderlyingCPProgram"
            If FieldTag = "878" Then Return "UnderlyingCPRegType"
            If FieldTag = "879" Then Return "UnderlyingQty"
            If FieldTag = "880" Then Return "TrdMatchID"
            If FieldTag = "881" Then Return "SecondaryTradeReportRefID"
            If FieldTag = "882" Then Return "UnderlyingDirtyPrice"
            If FieldTag = "883" Then Return "UnderlyingEndPrice"
            If FieldTag = "884" Then Return "UnderlyingStartValue"
            If FieldTag = "885" Then Return "UnderlyingCurrentValue"
            If FieldTag = "886" Then Return "UnderlyingEndValue"
            If FieldTag = "887" Then Return "NoUnderlyingStips"
            If FieldTag = "888" Then Return "UnderlyingStipType"
            If FieldTag = "889" Then Return "UnderlyingStipValue"
            If FieldTag = "890" Then Return "MaturityNetMoney"
            If FieldTag = "891" Then Return "MiscFeeBasis"
            If FieldTag = "892" Then Return "TotNoAllocs"
            If FieldTag = "893" Then Return "LastFragment"
            If FieldTag = "894" Then Return "CollReqID"
            If FieldTag = "895" Then Return "CollAsgnReason"
            If FieldTag = "896" Then Return "CollInquiryQualifier"
            If FieldTag = "897" Then Return "NoTrades"
            If FieldTag = "898" Then Return "MarginRatio"
            If FieldTag = "899" Then Return "MarginExcess"
            If FieldTag = "900" Then Return "TotalNetValue"
            If FieldTag = "901" Then Return "CashOutstanding"
            If FieldTag = "902" Then Return "CollAsgnID"
            If FieldTag = "903" Then Return "CollAsgnTransType"
            If FieldTag = "904" Then Return "CollRespID"
            If FieldTag = "905" Then Return "CollAsgnRespType"
            If FieldTag = "906" Then Return "CollAsgnRejectReason"
            If FieldTag = "907" Then Return "CollAsgnRefID"
            If FieldTag = "908" Then Return "CollRptID"
            If FieldTag = "909" Then Return "CollInquiryID"
            If FieldTag = "910" Then Return "CollStatus"
            If FieldTag = "911" Then Return "TotNumReports"
            If FieldTag = "912" Then Return "LastRptRequested"
            If FieldTag = "913" Then Return "AgreementDesc"
            If FieldTag = "914" Then Return "AgreementID"
            If FieldTag = "915" Then Return "AgreementDate"
            If FieldTag = "916" Then Return "StartDate"
            If FieldTag = "917" Then Return "EndDate"
            If FieldTag = "918" Then Return "AgreementCurrency"
            If FieldTag = "919" Then Return "DeliveryType"
            If FieldTag = "920" Then Return "EndAccruedInterestAmt"
            If FieldTag = "921" Then Return "StartCash"
            If FieldTag = "922" Then Return "EndCash"
            If FieldTag = "923" Then Return "UserRequestID"
            If FieldTag = "924" Then Return "UserRequestType"
            If FieldTag = "925" Then Return "NewPassword"
            If FieldTag = "926" Then Return "UserStatus"
            If FieldTag = "927" Then Return "UserStatusText"
            If FieldTag = "928" Then Return "StatusValue"
            If FieldTag = "929" Then Return "StatusText"
            If FieldTag = "930" Then Return "RefCompID"
            If FieldTag = "931" Then Return "RefSubID"
            If FieldTag = "932" Then Return "NetworkResponseID"
            If FieldTag = "933" Then Return "NetworkRequestID"
            If FieldTag = "934" Then Return "LastNetworkResponseID"
            If FieldTag = "935" Then Return "NetworkRequestType"
            If FieldTag = "936" Then Return "NoCompIDs"
            If FieldTag = "937" Then Return "NetworkStatusResponseType"
            If FieldTag = "938" Then Return "NoCollInquiryQualifier"
            If FieldTag = "939" Then Return "TrdRptStatus"
            If FieldTag = "940" Then Return "AffirmStatus"
            If FieldTag = "941" Then Return "UnderlyingStrikeCurrency"
            If FieldTag = "942" Then Return "LegStrikeCurrency"
            If FieldTag = "943" Then Return "TimeBracket"
            If FieldTag = "944" Then Return "CollAction"
            If FieldTag = "945" Then Return "CollInquiryStatus"
            If FieldTag = "946" Then Return "CollInquiryResult"
            If FieldTag = "947" Then Return "StrikeCurrency"
            If FieldTag = "948" Then Return "NoNested3PartyIDs"
            If FieldTag = "949" Then Return "Nested3PartyID"
            If FieldTag = "950" Then Return "Nested3PartyIDSource"
            If FieldTag = "951" Then Return "Nested3PartyRole"
            If FieldTag = "952" Then Return "NoNested3PartySubIDs"
            If FieldTag = "953" Then Return "Nested3PartySubID"
            If FieldTag = "954" Then Return "Nested3PartySubIDType"
            If FieldTag = "955" Then Return "LegContractSettlMonth"
            If FieldTag = "956" Then Return "LegInterestAccrualDate"
            If FieldTag = "957" Then Return "NoStrategyParameters"
            If FieldTag = "958" Then Return "StrategyParameterName"
            If FieldTag = "959" Then Return "StrategyParameterType"
            If FieldTag = "960" Then Return "StrategyParameterValue"
            If FieldTag = "961" Then Return "HostCrossID"
            If FieldTag = "962" Then Return "SideTimeInForce"
            If FieldTag = "963" Then Return "MDReportID"
            If FieldTag = "964" Then Return "SecurityReportID"
            If FieldTag = "965" Then Return "SecurityStatus"
            If FieldTag = "966" Then Return "SettleOnOpenFlag"
            If FieldTag = "967" Then Return "StrikeMultiplier"
            If FieldTag = "968" Then Return "StrikeValue"
            If FieldTag = "969" Then Return "MinPriceIncrement"
            If FieldTag = "970" Then Return "PositionLimit"
            If FieldTag = "971" Then Return "NTPositionLimit"
            If FieldTag = "972" Then Return "UnderlyingAllocationPercent"
            If FieldTag = "973" Then Return "UnderlyingCashAmount"
            If FieldTag = "974" Then Return "UnderlyingCashType"
            If FieldTag = "975" Then Return "UnderlyingSettlementType"
            If FieldTag = "976" Then Return "QuantityDate"
            If FieldTag = "977" Then Return "ContIntRptID"
            If FieldTag = "978" Then Return "LateIndicator"
            If FieldTag = "979" Then Return "InputSource"
            If FieldTag = "980" Then Return "SecurityUpdateAction"
            If FieldTag = "981" Then Return "NoExpiration"
            If FieldTag = "982" Then Return "ExpirationQtyType"
            If FieldTag = "983" Then Return "ExpQty"
            If FieldTag = "984" Then Return "NoUnderlyingAmounts"
            If FieldTag = "985" Then Return "UnderlyingPayAmount"
            If FieldTag = "986" Then Return "UnderlyingCollectAmount"
            If FieldTag = "987" Then Return "UnderlyingSettlementDate"
            If FieldTag = "988" Then Return "UnderlyingSettlementStatus"
            If FieldTag = "989" Then Return "SecondaryIndividualAllocID"
            If FieldTag = "990" Then Return "LegReportID"
            If FieldTag = "991" Then Return "RndPx"
            If FieldTag = "992" Then Return "IndividualAllocType"
            If FieldTag = "993" Then Return "AllocCustomerCapacity"
            If FieldTag = "994" Then Return "TierCode"
            If FieldTag = "996" Then Return "UnitOfMeasure"
            If FieldTag = "997" Then Return "TimeUnit"
            If FieldTag = "998" Then Return "UnderlyingUnitOfMeasure"
            If FieldTag = "999" Then Return "LegUnitOfMeasure"
            If FieldTag = "1000" Then Return "UnderlyingTimeUnit"
            If FieldTag = "1001" Then Return "LegTimeUnit"
            If FieldTag = "1002" Then Return "AllocMethod"
            If FieldTag = "1003" Then Return "TradeID"
            If FieldTag = "1005" Then Return "SideTradeReportID"
            If FieldTag = "1006" Then Return "SideFillStationCd"
            If FieldTag = "1007" Then Return "SideReasonCd"
            If FieldTag = "1008" Then Return "SideTrdSubTyp"
            If FieldTag = "1009" Then Return "SideQty"
            If FieldTag = "1011" Then Return "MessageEventSource"
            If FieldTag = "1012" Then Return "SideTrdRegTimestamp"
            If FieldTag = "1013" Then Return "SideTrdRegTimestampType"
            If FieldTag = "1014" Then Return "SideTrdRegTimestampSrc"
            If FieldTag = "1015" Then Return "AsOfIndicator"
            If FieldTag = "1016" Then Return "NoSideTrdRegTS"
            If FieldTag = "1017" Then Return "LegOptionRatio"
            If FieldTag = "1018" Then Return "NoInstrumentParties"
            If FieldTag = "1019" Then Return "InstrumentPartyID"
            If FieldTag = "1020" Then Return "TradeVolume"
            If FieldTag = "1021" Then Return "MDBookType"
            If FieldTag = "1022" Then Return "MDFeedType"
            If FieldTag = "1023" Then Return "MDPriceLevel"
            If FieldTag = "1024" Then Return "MDOriginType"
            If FieldTag = "1025" Then Return "FirstPx"
            If FieldTag = "1026" Then Return "MDEntrySpotRate"
            If FieldTag = "1027" Then Return "MDEntryForwardPoints"
            If FieldTag = "1028" Then Return "ManualOrderIndicator"
            If FieldTag = "1029" Then Return "CustDirectedOrder"
            If FieldTag = "1030" Then Return "ReceivedDeptID"
            If FieldTag = "1031" Then Return "CustOrderHandlingInst"
            If FieldTag = "1032" Then Return "OrderHandlingInstSource"
            If FieldTag = "1033" Then Return "DeskType"
            If FieldTag = "1034" Then Return "DeskTypeSource"
            If FieldTag = "1035" Then Return "DeskOrderHandlingInst"
            If FieldTag = "1036" Then Return "ExecAckStatus"
            If FieldTag = "1037" Then Return "UnderlyingDeliveryAmount"
            If FieldTag = "1038" Then Return "UnderlyingCapValue"
            If FieldTag = "1039" Then Return "UnderlyingSettlMethod"
            If FieldTag = "1040" Then Return "SecondaryTradeID"
            If FieldTag = "1041" Then Return "FirmTradeID"
            If FieldTag = "1042" Then Return "SecondaryFirmTradeID"
            If FieldTag = "1043" Then Return "CollApplType"
            If FieldTag = "1044" Then Return "UnderlyingAdjustedQuantity"
            If FieldTag = "1045" Then Return "UnderlyingFXRate"
            If FieldTag = "1046" Then Return "UnderlyingFXRateCalc"
            If FieldTag = "1047" Then Return "AllocPositionEffect"
            If FieldTag = "1048" Then Return "DealingCapacity"
            If FieldTag = "1049" Then Return "InstrmtAssignmentMethod"
            If FieldTag = "1050" Then Return "InstrumentPartyIDSource"
            If FieldTag = "1051" Then Return "InstrumentPartyRole"
            If FieldTag = "1052" Then Return "NoInstrumentPartySubIDs"
            If FieldTag = "1053" Then Return "InstrumentPartySubID"
            If FieldTag = "1054" Then Return "InstrumentPartySubIDType"
            If FieldTag = "1055" Then Return "PositionCurrency"
            If FieldTag = "1056" Then Return "CalculatedCcyLastQty"
            If FieldTag = "1057" Then Return "AggressorIndicator"
            If FieldTag = "1058" Then Return "NoUndlyInstrumentParties"
            If FieldTag = "1059" Then Return "UndlyInstrumentPartyID"
            If FieldTag = "1060" Then Return "UndlyInstrumentPartyIDSource"
            If FieldTag = "1061" Then Return "UndlyInstrumentPartyRole"
            If FieldTag = "1062" Then Return "NoUndlyInstrumentPartySubIDs"
            If FieldTag = "1063" Then Return "UndlyInstrumentPartySubID"
            If FieldTag = "1064" Then Return "UndlyInstrumentPartySubIDType"
            If FieldTag = "1065" Then Return "BidSwapPoints"
            If FieldTag = "1066" Then Return "OfferSwapPoints"
            If FieldTag = "1067" Then Return "LegBidForwardPoints"
            If FieldTag = "1068" Then Return "LegOfferForwardPoints"
            If FieldTag = "1069" Then Return "SwapPoints"
            If FieldTag = "1070" Then Return "MDQuoteType"
            If FieldTag = "1071" Then Return "LastSwapPoints"
            If FieldTag = "1072" Then Return "SideGrossTradeAmt"
            If FieldTag = "1073" Then Return "LegLastForwardPoints"
            If FieldTag = "1074" Then Return "LegCalculatedCcyLastQty"
            If FieldTag = "1075" Then Return "LegGrossTradeAmt"
            If FieldTag = "1079" Then Return "MaturityTime"
            If FieldTag = "1080" Then Return "RefOrderID"
            If FieldTag = "1081" Then Return "RefOrderIDSource"
            If FieldTag = "1082" Then Return "SecondaryDisplayQty"
            If FieldTag = "1083" Then Return "DisplayWhen"
            If FieldTag = "1084" Then Return "DisplayMethod"
            If FieldTag = "1085" Then Return "DisplayLowQty"
            If FieldTag = "1086" Then Return "DisplayHighQty"
            If FieldTag = "1087" Then Return "DisplayMinIncr"
            If FieldTag = "1088" Then Return "RefreshQty"
            If FieldTag = "1089" Then Return "MatchIncrement"
            If FieldTag = "1090" Then Return "MaxPriceLevels"
            If FieldTag = "1091" Then Return "PreTradeAnonymity"
            If FieldTag = "1092" Then Return "PriceProtectionScope"
            If FieldTag = "1093" Then Return "LotType"
            If FieldTag = "1094" Then Return "PegPriceType"
            If FieldTag = "1095" Then Return "PeggedRefPrice"
            If FieldTag = "1096" Then Return "PegSecurityIDSource"
            If FieldTag = "1097" Then Return "PegSecurityID"
            If FieldTag = "1098" Then Return "PegSymbol"
            If FieldTag = "1099" Then Return "PegSecurityDesc"
            If FieldTag = "1100" Then Return "TriggerType"
            If FieldTag = "1101" Then Return "TriggerAction"
            If FieldTag = "1102" Then Return "TriggerPrice"
            If FieldTag = "1103" Then Return "TriggerSymbol"
            If FieldTag = "1104" Then Return "TriggerSecurityID"
            If FieldTag = "1105" Then Return "TriggerSecurityIDSource"
            If FieldTag = "1106" Then Return "TriggerSecurityDesc"
            If FieldTag = "1107" Then Return "TriggerPriceType"
            If FieldTag = "1108" Then Return "TriggerPriceTypeScope"
            If FieldTag = "1109" Then Return "TriggerPriceDirection"
            If FieldTag = "1110" Then Return "TriggerNewPrice"
            If FieldTag = "1111" Then Return "TriggerOrderType"
            If FieldTag = "1112" Then Return "TriggerNewQty"
            If FieldTag = "1113" Then Return "TriggerTradingSessionID"
            If FieldTag = "1114" Then Return "TriggerTradingSessionSubID"
            If FieldTag = "1115" Then Return "OrderCategory"
            If FieldTag = "1116" Then Return "NoRootPartyIDs"
            If FieldTag = "1117" Then Return "RootPartyID"
            If FieldTag = "1118" Then Return "RootPartyIDSource"
            If FieldTag = "1119" Then Return "RootPartyRole"
            If FieldTag = "1120" Then Return "NoRootPartySubIDs"
            If FieldTag = "1121" Then Return "RootPartySubID"
            If FieldTag = "1122" Then Return "RootPartySubIDType"
            If FieldTag = "1123" Then Return "TradeHandlingInstr"
            If FieldTag = "1124" Then Return "OrigTradeHandlingInstr"
            If FieldTag = "1125" Then Return "OrigTradeDate"
            If FieldTag = "1126" Then Return "OrigTradeID"
            If FieldTag = "1127" Then Return "OrigSecondaryTradeID"
            If FieldTag = "1130" Then Return "RefApplVerID"
            If FieldTag = "1131" Then Return "RefCstmApplVerID"
            If FieldTag = "1132" Then Return "TZTransactTime"
            If FieldTag = "1133" Then Return "ExDestinationIDSource"
            If FieldTag = "1134" Then Return "ReportedPxDiff"
            If FieldTag = "1135" Then Return "RptSys"
            If FieldTag = "1136" Then Return "AllocClearingFeeIndicator"
            If FieldTag = "1137" Then Return "DefaultApplVerID"
            If FieldTag = "1138" Then Return "DisplayQty"
            If FieldTag = "1139" Then Return "ExchangeSpecialInstructions"
            If FieldTag = "1140" Then Return "MaxTradeVol"
            If FieldTag = "1141" Then Return "NoMDFeedTypes"
            If FieldTag = "1142" Then Return "MatchAlgorithm"
            If FieldTag = "1143" Then Return "MaxPriceVariation"
            If FieldTag = "1144" Then Return "ImpliedMarketIndicator"
            If FieldTag = "1145" Then Return "EventTime"
            If FieldTag = "1146" Then Return "MinPriceIncrementAmount"
            If FieldTag = "1147" Then Return "UnitOfMeasureQty"
            If FieldTag = "1148" Then Return "LowLimitPrice"
            If FieldTag = "1149" Then Return "HighLimitPrice"
            If FieldTag = "1150" Then Return "TradingReferencePrice"
            If FieldTag = "1151" Then Return "SecurityGroup"
            If FieldTag = "1152" Then Return "LegNumber"
            If FieldTag = "1153" Then Return "SettlementCycleNo"
            If FieldTag = "1154" Then Return "SideCurrency"
            If FieldTag = "1155" Then Return "SideSettlCurrency"
            If FieldTag = "1156" Then Return "ApplExtID"
            If FieldTag = "1157" Then Return "CcyAmt"
            If FieldTag = "1158" Then Return "NoSettlDetails"
            If FieldTag = "1159" Then Return "SettlObligMode"
            If FieldTag = "1160" Then Return "SettlObligMsgID"
            If FieldTag = "1161" Then Return "SettlObligID"
            If FieldTag = "1162" Then Return "SettlObligTransType"
            If FieldTag = "1163" Then Return "SettlObligRefID"
            If FieldTag = "1164" Then Return "SettlObligSource"
            If FieldTag = "1165" Then Return "NoSettlOblig"
            If FieldTag = "1166" Then Return "QuoteMsgID"
            If FieldTag = "1167" Then Return "QuoteEntryStatus"
            If FieldTag = "1168" Then Return "TotNoCxldQuotes"
            If FieldTag = "1169" Then Return "TotNoAccQuotes"
            If FieldTag = "1170" Then Return "TotNoRejQuotes"
            If FieldTag = "1171" Then Return "PrivateQuote"
            If FieldTag = "1172" Then Return "RespondentType"
            If FieldTag = "1173" Then Return "MDSubBookType"
            If FieldTag = "1174" Then Return "SecurityTradingEvent"
            If FieldTag = "1175" Then Return "NoStatsIndicators"
            If FieldTag = "1176" Then Return "StatsType"
            If FieldTag = "1177" Then Return "NoOfSecSizes"
            If FieldTag = "1178" Then Return "MDSecSizeType"
            If FieldTag = "1179" Then Return "MDSecSize"
            If FieldTag = "1180" Then Return "ApplID"
            If FieldTag = "1181" Then Return "ApplSeqNum"
            If FieldTag = "1182" Then Return "ApplBegSeqNum"
            If FieldTag = "1183" Then Return "ApplEndSeqNum"
            If FieldTag = "1184" Then Return "SecurityXMLLen"
            If FieldTag = "1185" Then Return "SecurityXML"
            If FieldTag = "1186" Then Return "SecurityXMLSchema"
            If FieldTag = "1187" Then Return "RefreshIndicator"
            If FieldTag = "1188" Then Return "Volatility"
            If FieldTag = "1189" Then Return "TimeToExpiration"
            If FieldTag = "1190" Then Return "RiskFreeRate"
            If FieldTag = "1191" Then Return "PriceUnitOfMeasure"
            If FieldTag = "1192" Then Return "PriceUnitOfMeasureQty"
            If FieldTag = "1193" Then Return "SettlMethod"
            If FieldTag = "1194" Then Return "ExerciseStyle"
            If FieldTag = "1195" Then Return "OptPayAmount"
            If FieldTag = "1196" Then Return "PriceQuoteMethod"
            If FieldTag = "1197" Then Return "FuturesValuationMethod"
            If FieldTag = "1198" Then Return "ListMethod"
            If FieldTag = "1199" Then Return "CapPrice"
            If FieldTag = "1200" Then Return "FloorPrice"
            If FieldTag = "1201" Then Return "NoStrikeRules"
            If FieldTag = "1202" Then Return "StartStrikePxRange"
            If FieldTag = "1203" Then Return "EndStrikePxRange"
            If FieldTag = "1204" Then Return "StrikeIncrement"
            If FieldTag = "1205" Then Return "NoTickRules"
            If FieldTag = "1206" Then Return "StartTickPriceRange"
            If FieldTag = "1207" Then Return "EndTickPriceRange"
            If FieldTag = "1208" Then Return "TickIncrement"
            If FieldTag = "1209" Then Return "TickRuleType"
            If FieldTag = "1210" Then Return "NestedInstrAttribType"
            If FieldTag = "1211" Then Return "NestedInstrAttribValue"
            If FieldTag = "1212" Then Return "LegMaturityTime"
            If FieldTag = "1213" Then Return "UnderlyingMaturityTime"
            If FieldTag = "1214" Then Return "DerivativeSymbol"
            If FieldTag = "1215" Then Return "DerivativeSymbolSfx"
            If FieldTag = "1216" Then Return "DerivativeSecurityID"
            If FieldTag = "1217" Then Return "DerivativeSecurityIDSource"
            If FieldTag = "1218" Then Return "NoDerivativeSecurityAltID"
            If FieldTag = "1219" Then Return "DerivativeSecurityAltID"
            If FieldTag = "1220" Then Return "DerivativeSecurityAltIDSource"
            If FieldTag = "1221" Then Return "SecondaryLowLimitPrice"
            If FieldTag = "1222" Then Return "MaturityRuleID"
            If FieldTag = "1223" Then Return "StrikeRuleID"
            If FieldTag = "1224" Then Return "LegUnitOfMeasureQty"
            If FieldTag = "1225" Then Return "DerivativeOptPayAmount"
            If FieldTag = "1226" Then Return "EndMaturityMonthYear"
            If FieldTag = "1227" Then Return "ProductComplex"
            If FieldTag = "1228" Then Return "DerivativeProductComplex"
            If FieldTag = "1229" Then Return "MaturityMonthYearIncrement"
            If FieldTag = "1230" Then Return "SecondaryHighLimitPrice"
            If FieldTag = "1231" Then Return "MinLotSize"
            If FieldTag = "1232" Then Return "NoExecInstRules"
            If FieldTag = "1234" Then Return "NoLotTypeRules"
            If FieldTag = "1235" Then Return "NoMatchRules"
            If FieldTag = "1236" Then Return "NoMaturityRules"
            If FieldTag = "1237" Then Return "NoOrdTypeRules"
            If FieldTag = "1239" Then Return "NoTimeInForceRules"
            If FieldTag = "1240" Then Return "SecondaryTradingReferencePrice"
            If FieldTag = "1241" Then Return "StartMaturityMonthYear"
            If FieldTag = "1242" Then Return "FlexProductEligibilityIndicator"
            If FieldTag = "1243" Then Return "DerivFlexProductEligibilityIndicator"
            If FieldTag = "1244" Then Return "FlexibleIndicator"
            If FieldTag = "1245" Then Return "TradingCurrency"
            If FieldTag = "1246" Then Return "DerivativeProduct"
            If FieldTag = "1247" Then Return "DerivativeSecurityGroup"
            If FieldTag = "1248" Then Return "DerivativeCFICode"
            If FieldTag = "1249" Then Return "DerivativeSecurityType"
            If FieldTag = "1250" Then Return "DerivativeSecuritySubType"
            If FieldTag = "1251" Then Return "DerivativeMaturityMonthYear"
            If FieldTag = "1252" Then Return "DerivativeMaturityDate"
            If FieldTag = "1253" Then Return "DerivativeMaturityTime"
            If FieldTag = "1254" Then Return "DerivativeSettleOnOpenFlag"
            If FieldTag = "1255" Then Return "DerivativeInstrmtAssignmentMethod"
            If FieldTag = "1256" Then Return "DerivativeSecurityStatus"
            If FieldTag = "1257" Then Return "DerivativeInstrRegistry"
            If FieldTag = "1258" Then Return "DerivativeCountryOfIssue"
            If FieldTag = "1259" Then Return "DerivativeStateOrProvinceOfIssue"
            If FieldTag = "1260" Then Return "DerivativeLocaleOfIssue"
            If FieldTag = "1261" Then Return "DerivativeStrikePrice"
            If FieldTag = "1262" Then Return "DerivativeStrikeCurrency"
            If FieldTag = "1263" Then Return "DerivativeStrikeMultiplier"
            If FieldTag = "1264" Then Return "DerivativeStrikeValue"
            If FieldTag = "1265" Then Return "DerivativeOptAttribute"
            If FieldTag = "1266" Then Return "DerivativeContractMultiplier"
            If FieldTag = "1267" Then Return "DerivativeMinPriceIncrement"
            If FieldTag = "1268" Then Return "DerivativeMinPriceIncrementAmount"
            If FieldTag = "1269" Then Return "DerivativeUnitOfMeasure"
            If FieldTag = "1270" Then Return "DerivativeUnitOfMeasureQty"
            If FieldTag = "1271" Then Return "DerivativeTimeUnit"
            If FieldTag = "1272" Then Return "DerivativeSecurityExchange"
            If FieldTag = "1273" Then Return "DerivativePositionLimit"
            If FieldTag = "1274" Then Return "DerivativeNTPositionLimit"
            If FieldTag = "1275" Then Return "DerivativeIssuer"
            If FieldTag = "1276" Then Return "DerivativeIssueDate"
            If FieldTag = "1277" Then Return "DerivativeEncodedIssuerLen"
            If FieldTag = "1278" Then Return "DerivativeEncodedIssuer"
            If FieldTag = "1279" Then Return "DerivativeSecurityDesc"
            If FieldTag = "1280" Then Return "DerivativeEncodedSecurityDescLen"
            If FieldTag = "1281" Then Return "DerivativeEncodedSecurityDesc"
            If FieldTag = "1282" Then Return "DerivativeSecurityXMLLen"
            If FieldTag = "1283" Then Return "DerivativeSecurityXML"
            If FieldTag = "1284" Then Return "DerivativeSecurityXMLSchema"
            If FieldTag = "1285" Then Return "DerivativeContractSettlMonth"
            If FieldTag = "1286" Then Return "NoDerivativeEvents"
            If FieldTag = "1287" Then Return "DerivativeEventType"
            If FieldTag = "1288" Then Return "DerivativeEventDate"
            If FieldTag = "1289" Then Return "DerivativeEventTime"
            If FieldTag = "1290" Then Return "DerivativeEventPx"
            If FieldTag = "1291" Then Return "DerivativeEventText"
            If FieldTag = "1292" Then Return "NoDerivativeInstrumentParties"
            If FieldTag = "1293" Then Return "DerivativeInstrumentPartyID"
            If FieldTag = "1294" Then Return "DerivativeInstrumentPartyIDSource"
            If FieldTag = "1295" Then Return "DerivativeInstrumentPartyRole"
            If FieldTag = "1296" Then Return "NoDerivativeInstrumentPartySubIDs"
            If FieldTag = "1297" Then Return "DerivativeInstrumentPartySubID"
            If FieldTag = "1298" Then Return "DerivativeInstrumentPartySubIDType"
            If FieldTag = "1299" Then Return "DerivativeExerciseStyle"
            If FieldTag = "1300" Then Return "MarketSegmentID"
            If FieldTag = "1301" Then Return "MarketID"
            If FieldTag = "1302" Then Return "MaturityMonthYearIncrementUnits"
            If FieldTag = "1303" Then Return "MaturityMonthYearFormat"
            If FieldTag = "1304" Then Return "StrikeExerciseStyle"
            If FieldTag = "1305" Then Return "SecondaryPriceLimitType"
            If FieldTag = "1306" Then Return "PriceLimitType"
            If FieldTag = "1307" Then Return "DerivativeSecurityListRequestType"
            If FieldTag = "1308" Then Return "ExecInstValue"
            If FieldTag = "1309" Then Return "NoTradingSessionRules"
            If FieldTag = "1310" Then Return "NoMarketSegments"
            If FieldTag = "1311" Then Return "NoDerivativeInstrAttrib"
            If FieldTag = "1312" Then Return "NoNestedInstrAttrib"
            If FieldTag = "1313" Then Return "DerivativeInstrAttribType"
            If FieldTag = "1314" Then Return "DerivativeInstrAttribValue"
            If FieldTag = "1315" Then Return "DerivativePriceUnitOfMeasure"
            If FieldTag = "1316" Then Return "DerivativePriceUnitOfMeasureQty"
            If FieldTag = "1317" Then Return "DerivativeSettlMethod"
            If FieldTag = "1318" Then Return "DerivativePriceQuoteMethod"
            If FieldTag = "1319" Then Return "DerivativeFuturesValuationMethod"
            If FieldTag = "1320" Then Return "DerivativeListMethod"
            If FieldTag = "1321" Then Return "DerivativeCapPrice"
            If FieldTag = "1322" Then Return "DerivativeFloorPrice"
            If FieldTag = "1323" Then Return "DerivativePutOrCall"
            If FieldTag = "1324" Then Return "ListUpdateAction"
            If FieldTag = "1325" Then Return "ParentMktSegmID"
            If FieldTag = "1326" Then Return "TradingSessionDesc"
            If FieldTag = "1327" Then Return "TradSesUpdateAction"
            If FieldTag = "1328" Then Return "RejectText"
            If FieldTag = "1329" Then Return "FeeMultiplier"
            If FieldTag = "1330" Then Return "UnderlyingLegSymbol"
            If FieldTag = "1331" Then Return "UnderlyingLegSymbolSfx"
            If FieldTag = "1332" Then Return "UnderlyingLegSecurityID"
            If FieldTag = "1333" Then Return "UnderlyingLegSecurityIDSource"
            If FieldTag = "1334" Then Return "NoUnderlyingLegSecurityAltID"
            If FieldTag = "1335" Then Return "UnderlyingLegSecurityAltID"
            If FieldTag = "1336" Then Return "UnderlyingLegSecurityAltIDSource"
            If FieldTag = "1337" Then Return "UnderlyingLegSecurityType"
            If FieldTag = "1338" Then Return "UnderlyingLegSecuritySubType"
            If FieldTag = "1339" Then Return "UnderlyingLegMaturityMonthYear"
            If FieldTag = "1340" Then Return "UnderlyingLegStrikePrice"
            If FieldTag = "1341" Then Return "UnderlyingLegSecurityExchange"
            If FieldTag = "1342" Then Return "NoOfLegUnderlyings"
            If FieldTag = "1343" Then Return "UnderlyingLegPutOrCall"
            If FieldTag = "1344" Then Return "UnderlyingLegCFICode"
            If FieldTag = "1345" Then Return "UnderlyingLegMaturityDate"
            If FieldTag = "1346" Then Return "ApplReqID"
            If FieldTag = "1347" Then Return "ApplReqType"
            If FieldTag = "1348" Then Return "ApplResponseType"
            If FieldTag = "1349" Then Return "ApplTotalMessageCount"
            If FieldTag = "1350" Then Return "ApplLastSeqNum"
            If FieldTag = "1351" Then Return "NoApplIDs"
            If FieldTag = "1352" Then Return "ApplResendFlag"
            If FieldTag = "1353" Then Return "ApplResponseID"
            If FieldTag = "1354" Then Return "ApplResponseError"
            If FieldTag = "1355" Then Return "RefApplID"
            If FieldTag = "1356" Then Return "ApplReportID"
            If FieldTag = "1357" Then Return "RefApplLastSeqNum"
            If FieldTag = "1358" Then Return "LegPutOrCall"
            If FieldTag = "1359" Then Return "EncodedSymbolLen"
            If FieldTag = "1360" Then Return "EncodedSymbol"
            If FieldTag = "1361" Then Return "TotNoFills"
            If FieldTag = "1362" Then Return "NoFills"
            If FieldTag = "1363" Then Return "FillExecID"
            If FieldTag = "1364" Then Return "FillPx"
            If FieldTag = "1365" Then Return "FillQty"
            If FieldTag = "1366" Then Return "LegAllocID"
            If FieldTag = "1367" Then Return "LegAllocSettlCurrency"
            If FieldTag = "1368" Then Return "TradSesEvent"
            If FieldTag = "1369" Then Return "MassActionReportID"
            If FieldTag = "1370" Then Return "NoNotAffectedOrders"
            If FieldTag = "1371" Then Return "NotAffectedOrderID"
            If FieldTag = "1372" Then Return "NotAffOrigClOrdID"
            If FieldTag = "1373" Then Return "MassActionType"
            If FieldTag = "1374" Then Return "MassActionScope"
            If FieldTag = "1375" Then Return "MassActionResponse"
            If FieldTag = "1376" Then Return "MassActionRejectReason"
            If FieldTag = "1377" Then Return "MultilegModel"
            If FieldTag = "1378" Then Return "MultilegPriceMethod"
            If FieldTag = "1379" Then Return "LegVolatility"
            If FieldTag = "1380" Then Return "DividendYield"
            If FieldTag = "1381" Then Return "LegDividendYield"
            If FieldTag = "1382" Then Return "CurrencyRatio"
            If FieldTag = "1383" Then Return "LegCurrencyRatio"
            If FieldTag = "1384" Then Return "LegExecInst"
            If FieldTag = "1385" Then Return "ContingencyType"
            If FieldTag = "1386" Then Return "ListRejectReason"
            If FieldTag = "1387" Then Return "NoTrdRepIndicators"
            If FieldTag = "1388" Then Return "TrdRepPartyRole"
            If FieldTag = "1389" Then Return "TrdRepIndicator"
            If FieldTag = "1390" Then Return "TradePublishIndicator"
            If FieldTag = "1391" Then Return "UnderlyingLegOptAttribute"
            If FieldTag = "1392" Then Return "UnderlyingLegSecurityDesc"
            If FieldTag = "1393" Then Return "MarketReqID"
            If FieldTag = "1394" Then Return "MarketReportID"
            If FieldTag = "1395" Then Return "MarketUpdateAction"
            If FieldTag = "1396" Then Return "MarketSegmentDesc"
            If FieldTag = "1397" Then Return "EncodedMktSegmDescLen"
            If FieldTag = "1398" Then Return "EncodedMktSegmDesc"
            If FieldTag = "1399" Then Return "ApplNewSeqNum"
            If FieldTag = "1400" Then Return "EncryptedPasswordMethod"
            If FieldTag = "1401" Then Return "EncryptedPasswordLen"
            If FieldTag = "1402" Then Return "EncryptedPassword"
            If FieldTag = "1403" Then Return "EncryptedNewPasswordLen"
            If FieldTag = "1404" Then Return "EncryptedNewPassword"
            If FieldTag = "1405" Then Return "UnderlyingLegMaturityTime"
            If FieldTag = "1406" Then Return "RefApplExtID"
            If FieldTag = "1407" Then Return "DefaultApplExtID"
            If FieldTag = "1408" Then Return "DefaultCstmApplVerID"
            If FieldTag = "1409" Then Return "SessionStatus"
            If FieldTag = "1410" Then Return "DefaultVerIndicator"
            If FieldTag = "1411" Then Return "Nested4PartySubIDType"
            If FieldTag = "1412" Then Return "Nested4PartySubID"
            If FieldTag = "1413" Then Return "NoNested4PartySubIDs"
            If FieldTag = "1414" Then Return "NoNested4PartyIDs"
            If FieldTag = "1415" Then Return "Nested4PartyID"
            If FieldTag = "1416" Then Return "Nested4PartyIDSource"
            If FieldTag = "1417" Then Return "Nested4PartyRole"
            If FieldTag = "1418" Then Return "LegLastQty"
            If FieldTag = "1419" Then Return "UnderlyingExerciseStyle"
            If FieldTag = "1420" Then Return "LegExerciseStyle"
            If FieldTag = "1421" Then Return "LegPriceUnitOfMeasure"
            If FieldTag = "1422" Then Return "LegPriceUnitOfMeasureQty"
            If FieldTag = "1423" Then Return "UnderlyingUnitOfMeasureQty"
            If FieldTag = "1424" Then Return "UnderlyingPriceUnitOfMeasure"
            If FieldTag = "1425" Then Return "UnderlyingPriceUnitOfMeasureQty"
            If FieldTag = "1426" Then Return "ApplReportType"

            If FieldTag = "10" Then Return "CheckSum"
            If FieldTag = "89" Then Return "Signature"
            If FieldTag = "93" Then Return "SignatureLength"


        ElseIf VersionId = "FIX.4.2" Then


            If FieldTag = "8" Then Return "BeginString"
            If FieldTag = "9" Then Return "BodyLength"
            If FieldTag = "34" Then Return "MsgSeqNum"
            If FieldTag = "35" Then Return "MsgType"
            If FieldTag = "43" Then Return "PossDupFlag"
            If FieldTag = "49" Then Return "SenderCompID"
            If FieldTag = "50" Then Return "SenderSubID"
            If FieldTag = "52" Then Return "SendingTime"
            If FieldTag = "56" Then Return "TargetCompID"
            If FieldTag = "57" Then Return "TargetSubID"
            If FieldTag = "90" Then Return "SecureDataLen"
            If FieldTag = "91" Then Return "SecureData"
            If FieldTag = "97" Then Return "PossResend"
            If FieldTag = "115" Then Return "OnBehalfOfCompID"
            If FieldTag = "116" Then Return "OnBehalfOfSubID"
            If FieldTag = "122" Then Return "OrigSendingTime"
            If FieldTag = "128" Then Return "DeliverToCompID"
            If FieldTag = "129" Then Return "DeliverToSubID"
            If FieldTag = "142" Then Return "SenderLocationID"
            If FieldTag = "143" Then Return "TargetLocationID"
            If FieldTag = "144" Then Return "OnBehalfOfLocationID"
            If FieldTag = "145" Then Return "DeliverToLocationID"
            If FieldTag = "212" Then Return "XmlDataLen"
            If FieldTag = "213" Then Return "XmlData"
            If FieldTag = "347" Then Return "MessageEncoding"
            If FieldTag = "369" Then Return "LastMsgSeqNumProcessed"
            If FieldTag = "370" Then Return "OnBehalfOfSendingTime"





            If FieldTag = "1" Then Return "Account"
            If FieldTag = "2" Then Return "AdvId"
            If FieldTag = "3" Then Return "AdvRefID"
            If FieldTag = "4" Then Return "AdvSide"
            If FieldTag = "5" Then Return "AdvTransType"
            If FieldTag = "6" Then Return "AvgPx"
            If FieldTag = "7" Then Return "BeginSeqNo"
            If FieldTag = "11" Then Return "ClOrdID"
            If FieldTag = "12" Then Return "Commission"
            If FieldTag = "13" Then Return "CommType"
            If FieldTag = "14" Then Return "CumQty"
            If FieldTag = "15" Then Return "Currency"
            If FieldTag = "16" Then Return "EndSeqNo"
            If FieldTag = "17" Then Return "ExecID"
            If FieldTag = "18" Then Return "ExecInst"
            If FieldTag = "19" Then Return "ExecRefID"
            If FieldTag = "20" Then Return "ExecTransType"
            If FieldTag = "21" Then Return "HandlInst"
            If FieldTag = "22" Then Return "IDSource"
            If FieldTag = "23" Then Return "IOIid"
            If FieldTag = "24" Then Return "IOIOthSvc (no longer used)"
            If FieldTag = "25" Then Return "IOIQltyInd"
            If FieldTag = "26" Then Return "IOIRefID"
            If FieldTag = "27" Then Return "IOIShares"
            If FieldTag = "28" Then Return "IOITransType"
            If FieldTag = "29" Then Return "LastCapacity"
            If FieldTag = "30" Then Return "LastMkt"
            If FieldTag = "31" Then Return "LastPx"
            If FieldTag = "32" Then Return "LastShares"
            If FieldTag = "33" Then Return "LinesOfText"
            If FieldTag = "36" Then Return "NewSeqNo"
            If FieldTag = "37" Then Return "OrderID"
            If FieldTag = "38" Then Return "OrderQty"
            If FieldTag = "39" Then Return "OrdStatus"
            If FieldTag = "40" Then Return "OrdType"
            If FieldTag = "41" Then Return "OrigClOrdID"
            If FieldTag = "42" Then Return "OrigTime"
            If FieldTag = "44" Then Return "Price"
            If FieldTag = "45" Then Return "RefSeqNum"
            If FieldTag = "46" Then Return "RelatdSym"
            If FieldTag = "47" Then Return "Rule80A(aka OrderCapacity)"
            If FieldTag = "48" Then Return "SecurityID"
            If FieldTag = "51" Then Return "SendingDate (no longer used)"
            If FieldTag = "53" Then Return "Shares"
            If FieldTag = "54" Then Return "Side"
            If FieldTag = "55" Then Return "Symbol"
            If FieldTag = "58" Then Return "Text"
            If FieldTag = "59" Then Return "TimeInForce"
            If FieldTag = "60" Then Return "TransactTime"
            If FieldTag = "61" Then Return "Urgency"
            If FieldTag = "62" Then Return "ValidUntilTime"
            If FieldTag = "63" Then Return "SettlmntTyp"
            If FieldTag = "64" Then Return "FutSettDate"
            If FieldTag = "65" Then Return "SymbolSfx"
            If FieldTag = "66" Then Return "ListID"
            If FieldTag = "67" Then Return "ListSeqNo"
            If FieldTag = "68" Then Return "TotNoOrders(formerly named: ListNoOrds)"
            If FieldTag = "69" Then Return "ListExecInst"
            If FieldTag = "70" Then Return "AllocID"
            If FieldTag = "71" Then Return "AllocTransType"
            If FieldTag = "72" Then Return "RefAllocID"
            If FieldTag = "73" Then Return "NoOrders"
            If FieldTag = "74" Then Return "AvgPrxPrecision"
            If FieldTag = "75" Then Return "TradeDate"
            If FieldTag = "76" Then Return "ExecBroker"
            If FieldTag = "77" Then Return "OpenClose"
            If FieldTag = "78" Then Return "NoAllocs"
            If FieldTag = "79" Then Return "AllocAccount"
            If FieldTag = "80" Then Return "AllocShares"
            If FieldTag = "81" Then Return "ProcessCode"
            If FieldTag = "82" Then Return "NoRpts"
            If FieldTag = "83" Then Return "RptSeq"
            If FieldTag = "84" Then Return "CxlQty"
            If FieldTag = "85" Then Return "NoDlvyInst(no longer used)"
            If FieldTag = "86" Then Return "DlvyInst(no longer used)"
            If FieldTag = "87" Then Return "AllocStatus"
            If FieldTag = "88" Then Return "AllocRejCode"
            If FieldTag = "92" Then Return "BrokerOfCredit"
            If FieldTag = "94" Then Return "EmailType"
            If FieldTag = "95" Then Return "RawDataLength"
            If FieldTag = "96" Then Return "RawData"
            If FieldTag = "98" Then Return "EncryptMethod"
            If FieldTag = "99" Then Return "StopPx"
            If FieldTag = "100" Then Return "ExDestination"
            If FieldTag = "102" Then Return "CxlRejReason"
            If FieldTag = "103" Then Return "OrdRejReason"
            If FieldTag = "104" Then Return "IOIQualifier"
            If FieldTag = "105" Then Return "WaveNo"
            If FieldTag = "106" Then Return "Issuer"
            If FieldTag = "107" Then Return "SecurityDesc"
            If FieldTag = "108" Then Return "HeartBtInt"
            If FieldTag = "109" Then Return "ClientID"
            If FieldTag = "110" Then Return "MinQty"
            If FieldTag = "111" Then Return "MaxFloor"
            If FieldTag = "112" Then Return "TestReqID"
            If FieldTag = "113" Then Return "ReportToExch"
            If FieldTag = "114" Then Return "LocateReqd"
            If FieldTag = "117" Then Return "QuoteID"
            If FieldTag = "118" Then Return "NetMoney"
            If FieldTag = "119" Then Return "SettlCurrAmt"
            If FieldTag = "120" Then Return "SettlCurrency"
            If FieldTag = "121" Then Return "ForexReq"
            If FieldTag = "123" Then Return "GapFillFlag"
            If FieldTag = "124" Then Return "NoExecs"
            If FieldTag = "125" Then Return "CxlType(no longer used)"
            If FieldTag = "126" Then Return "ExpireTime"
            If FieldTag = "127" Then Return "DKReason"
            If FieldTag = "130" Then Return "IOINaturalFlag"
            If FieldTag = "131" Then Return "QuoteReqID"
            If FieldTag = "132" Then Return "BidPx"
            If FieldTag = "133" Then Return "OfferPx"
            If FieldTag = "134" Then Return "BidSize"
            If FieldTag = "135" Then Return "OfferSize"
            If FieldTag = "136" Then Return "NoMiscFees"
            If FieldTag = "137" Then Return "MiscFeeAmt"
            If FieldTag = "138" Then Return "MiscFeeCurr"
            If FieldTag = "139" Then Return "MiscFeeType"
            If FieldTag = "140" Then Return "PrevClosePx"
            If FieldTag = "141" Then Return "ResetSeqNumFlag"
            If FieldTag = "146" Then Return "NoRelatedSym"
            If FieldTag = "147" Then Return "Subject"
            If FieldTag = "148" Then Return "Headline"
            If FieldTag = "149" Then Return "URLLink"
            If FieldTag = "150" Then Return "ExecType"
            If FieldTag = "151" Then Return "LeavesQty"
            If FieldTag = "152" Then Return "CashOrderQty"
            If FieldTag = "153" Then Return "AllocAvgPx"
            If FieldTag = "154" Then Return "AllocNetMoney"
            If FieldTag = "155" Then Return "SettlCurrFxRate"
            If FieldTag = "156" Then Return "SettlCurrFxRateCalc"
            If FieldTag = "157" Then Return "NumDaysInterest"
            If FieldTag = "158" Then Return "AccruedInterestRate"
            If FieldTag = "159" Then Return "AccruedInterestAmt"
            If FieldTag = "160" Then Return "SettlInstMode"
            If FieldTag = "161" Then Return "AllocText"
            If FieldTag = "162" Then Return "SettlInstID"
            If FieldTag = "163" Then Return "SettlInstTransType"
            If FieldTag = "164" Then Return "EmailThreadID"
            If FieldTag = "165" Then Return "SettlInstSource"
            If FieldTag = "166" Then Return "SettlLocation"
            If FieldTag = "167" Then Return "SecurityType"
            If FieldTag = "168" Then Return "EffectiveTime"
            If FieldTag = "169" Then Return "StandInstDbType"
            If FieldTag = "170" Then Return "StandInstDbName"
            If FieldTag = "171" Then Return "StandInstDbID"
            If FieldTag = "172" Then Return "SettlDeliveryType"
            If FieldTag = "173" Then Return "SettlDepositoryCode"
            If FieldTag = "174" Then Return "SettlBrkrCode"
            If FieldTag = "175" Then Return "SettlInstCode"
            If FieldTag = "176" Then Return "SecuritySettlAgentName"
            If FieldTag = "177" Then Return "SecuritySettlAgentCode"
            If FieldTag = "178" Then Return "SecuritySettlAgentAcctNum"
            If FieldTag = "179" Then Return "SecuritySettlAgentAcctName"
            If FieldTag = "180" Then Return "SecuritySettlAgentContactName"
            If FieldTag = "181" Then Return "SecuritySettlAgentContactPhone"
            If FieldTag = "182" Then Return "CashSettlAgentName"
            If FieldTag = "183" Then Return "CashSettlAgentCode"
            If FieldTag = "184" Then Return "CashSettlAgentAcctNum"
            If FieldTag = "185" Then Return "CashSettlAgentAcctName"
            If FieldTag = "186" Then Return "CashSettlAgentContactName"
            If FieldTag = "187" Then Return "CashSettlAgentContactPhone"
            If FieldTag = "188" Then Return "BidSpotRate"
            If FieldTag = "189" Then Return "BidForwardPoints"
            If FieldTag = "190" Then Return "OfferSpotRate"
            If FieldTag = "191" Then Return "OfferForwardPoints"
            If FieldTag = "192" Then Return "OrderQty2"
            If FieldTag = "193" Then Return "FutSettDate2"
            If FieldTag = "194" Then Return "LastSpotRate"
            If FieldTag = "195" Then Return "LastForwardPoints"
            If FieldTag = "196" Then Return "AllocLinkID"
            If FieldTag = "197" Then Return "AllocLinkType"
            If FieldTag = "198" Then Return "SecondaryOrderID"
            If FieldTag = "199" Then Return "NoIOIQualifiers"
            If FieldTag = "200" Then Return "MaturityMonthYear"
            If FieldTag = "201" Then Return "PutOrCall"
            If FieldTag = "202" Then Return "StrikePrice"
            If FieldTag = "203" Then Return "CoveredOrUncovered"
            If FieldTag = "204" Then Return "CustomerOrFirm"
            If FieldTag = "205" Then Return "MaturityDay"
            If FieldTag = "206" Then Return "OptAttribute"
            If FieldTag = "207" Then Return "SecurityExchange"
            If FieldTag = "208" Then Return "NotifyBrokerOfCredit"
            If FieldTag = "209" Then Return "AllocHandlInst"
            If FieldTag = "210" Then Return "MaxShow"
            If FieldTag = "211" Then Return "PegDifference"
            If FieldTag = "214" Then Return "SettlInstRefID"
            If FieldTag = "215" Then Return "NoRoutingIDs"
            If FieldTag = "216" Then Return "RoutingType"
            If FieldTag = "217" Then Return "RoutingID"
            If FieldTag = "218" Then Return "SpreadToBenchmark"
            If FieldTag = "219" Then Return "Benchmark"
            If FieldTag = "223" Then Return "CouponRate"
            If FieldTag = "231" Then Return "ContractMultiplier"
            If FieldTag = "262" Then Return "MDReqID"
            If FieldTag = "263" Then Return "SubscriptionRequestType"
            If FieldTag = "264" Then Return "MarketDepth"
            If FieldTag = "265" Then Return "MDUpdateType"
            If FieldTag = "266" Then Return "AggregatedBook"
            If FieldTag = "267" Then Return "NoMDEntryTypes"
            If FieldTag = "268" Then Return "NoMDEntries"
            If FieldTag = "269" Then Return "MDEntryType"
            If FieldTag = "270" Then Return "MDEntryPx"
            If FieldTag = "271" Then Return "MDEntrySize"
            If FieldTag = "272" Then Return "MDEntryDate"
            If FieldTag = "273" Then Return "MDEntryTime"
            If FieldTag = "274" Then Return "TickDirection"
            If FieldTag = "275" Then Return "MDMkt"
            If FieldTag = "276" Then Return "QuoteCondition"
            If FieldTag = "277" Then Return "TradeCondition"
            If FieldTag = "278" Then Return "MDEntryID"
            If FieldTag = "279" Then Return "MDUpdateAction"
            If FieldTag = "280" Then Return "MDEntryRefID"
            If FieldTag = "281" Then Return "MDReqRejReason"
            If FieldTag = "282" Then Return "MDEntryOriginator"
            If FieldTag = "283" Then Return "LocationID"
            If FieldTag = "284" Then Return "DeskID"
            If FieldTag = "285" Then Return "DeleteReason"
            If FieldTag = "286" Then Return "OpenCloseSettleFlag"
            If FieldTag = "287" Then Return "SellerDays"
            If FieldTag = "288" Then Return "MDEntryBuyer"
            If FieldTag = "289" Then Return "MDEntrySeller"
            If FieldTag = "290" Then Return "MDEntryPositionNo"
            If FieldTag = "291" Then Return "FinancialStatus"
            If FieldTag = "292" Then Return "CorporateAction"
            If FieldTag = "293" Then Return "DefBidSize"
            If FieldTag = "294" Then Return "DefOfferSize"
            If FieldTag = "295" Then Return "NoQuoteEntries"
            If FieldTag = "296" Then Return "NoQuoteSets"
            If FieldTag = "297" Then Return "QuoteAckStatus"
            If FieldTag = "298" Then Return "QuoteCancelType"
            If FieldTag = "299" Then Return "QuoteEntryID"
            If FieldTag = "300" Then Return "QuoteRejectReason"
            If FieldTag = "301" Then Return "QuoteResponseLevel"
            If FieldTag = "302" Then Return "QuoteSetID"
            If FieldTag = "303" Then Return "QuoteRequestType"
            If FieldTag = "304" Then Return "TotQuoteEntries"
            If FieldTag = "305" Then Return "UnderlyingIDSource"
            If FieldTag = "306" Then Return "UnderlyingIssuer"
            If FieldTag = "307" Then Return "UnderlyingSecurityDesc"
            If FieldTag = "308" Then Return "UnderlyingSecurityExchange"
            If FieldTag = "309" Then Return "UnderlyingSecurityID"
            If FieldTag = "310" Then Return "UnderlyingSecurityType"
            If FieldTag = "311" Then Return "UnderlyingSymbol"
            If FieldTag = "312" Then Return "UnderlyingSymbolSfx"
            If FieldTag = "313" Then Return "UnderlyingMaturityMonthYear"
            If FieldTag = "314" Then Return "UnderlyingMaturityDay"
            If FieldTag = "315" Then Return "UnderlyingPutOrCall"
            If FieldTag = "316" Then Return "UnderlyingStrikePrice"
            If FieldTag = "317" Then Return "UnderlyingOptAttribute"
            If FieldTag = "318" Then Return "Underlying Currency"
            If FieldTag = "319" Then Return "RatioQty"
            If FieldTag = "320" Then Return "SecurityReqID"
            If FieldTag = "321" Then Return "SecurityRequestType"
            If FieldTag = "322" Then Return "SecurityResponseID"
            If FieldTag = "323" Then Return "SecurityResponseType"
            If FieldTag = "324" Then Return "SecurityStatusReqID"
            If FieldTag = "325" Then Return "UnsolicitedIndicator"
            If FieldTag = "326" Then Return "SecurityTradingStatus"
            If FieldTag = "327" Then Return "HaltReason"
            If FieldTag = "328" Then Return "InViewOfCommon"
            If FieldTag = "329" Then Return "DueToRelated"
            If FieldTag = "330" Then Return "BuyVolume"
            If FieldTag = "331" Then Return "SellVolume"
            If FieldTag = "332" Then Return "HighPx"
            If FieldTag = "333" Then Return "LowPx"
            If FieldTag = "334" Then Return "Adjustment"
            If FieldTag = "335" Then Return "TradSesReqID"
            If FieldTag = "336" Then Return "TradingSessionID"
            If FieldTag = "337" Then Return "ContraTrader"
            If FieldTag = "338" Then Return "TradSesMethod"
            If FieldTag = "339" Then Return "TradSesMode"
            If FieldTag = "340" Then Return "TradSesStatus"
            If FieldTag = "341" Then Return "TradSesStartTime"
            If FieldTag = "342" Then Return "TradSesOpenTime"
            If FieldTag = "343" Then Return "TradSesPreCloseTime"
            If FieldTag = "344" Then Return "TradSesCloseTime"
            If FieldTag = "345" Then Return "TradSesEndTime"
            If FieldTag = "346" Then Return "NumberOfOrders"
            If FieldTag = "348" Then Return "EncodedIssuerLen"
            If FieldTag = "349" Then Return "EncodedIssuer"
            If FieldTag = "350" Then Return "EncodedSecurityDescLen"
            If FieldTag = "351" Then Return "EncodedSecurityDesc"
            If FieldTag = "352" Then Return "EncodedListExecInstLen"
            If FieldTag = "353" Then Return "EncodedListExecInst"
            If FieldTag = "354" Then Return "EncodedTextLen"
            If FieldTag = "355" Then Return "EncodedText"
            If FieldTag = "356" Then Return "EncodedSubjectLen"
            If FieldTag = "357" Then Return "EncodedSubject"
            If FieldTag = "358" Then Return "EncodedHeadlineLen"
            If FieldTag = "359" Then Return "EncodedHeadline"
            If FieldTag = "360" Then Return "EncodedAllocTextLen"
            If FieldTag = "361" Then Return "EncodedAllocText"
            If FieldTag = "362" Then Return "EncodedUnderlyingIssuerLen"
            If FieldTag = "363" Then Return "EncodedUnderlyingIssuer"
            If FieldTag = "364" Then Return "EncodedUnderlyingSecurityDescLen"
            If FieldTag = "365" Then Return "EncodedUnderlyingSecurityDesc"
            If FieldTag = "366" Then Return "AllocPrice"
            If FieldTag = "367" Then Return "QuoteSetValidUntilTime"
            If FieldTag = "368" Then Return "QuoteEntryRejectReason"
            If FieldTag = "371" Then Return "RefTagID"
            If FieldTag = "372" Then Return "RefMsgType"
            If FieldTag = "373" Then Return "SessionRejectReason"
            If FieldTag = "374" Then Return "BidRequestTransType"
            If FieldTag = "375" Then Return "ContraBroker"
            If FieldTag = "376" Then Return "ComplianceID"
            If FieldTag = "377" Then Return "SolicitedFlag"
            If FieldTag = "378" Then Return "ExecRestatementReason"
            If FieldTag = "379" Then Return "BusinessRejectRefID"
            If FieldTag = "380" Then Return "BusinessRejectReason"
            If FieldTag = "381" Then Return "GrossTradeAmt"
            If FieldTag = "382" Then Return "NoContraBrokers"
            If FieldTag = "383" Then Return "MaxMessageSize"
            If FieldTag = "384" Then Return "NoMsgTypes"
            If FieldTag = "385" Then Return "MsgDirection"
            If FieldTag = "386" Then Return "NoTradingSessions"
            If FieldTag = "387" Then Return "TotalVolumeTraded"
            If FieldTag = "388" Then Return "DiscretionInst"
            If FieldTag = "389" Then Return "DiscretionOffset"
            If FieldTag = "390" Then Return "BidID"
            If FieldTag = "391" Then Return "ClientBidID"
            If FieldTag = "392" Then Return "ListName"
            If FieldTag = "393" Then Return "TotalNumSecurities"
            If FieldTag = "394" Then Return "BidType"
            If FieldTag = "395" Then Return "NumTickets"
            If FieldTag = "396" Then Return "SideValue1"
            If FieldTag = "397" Then Return "SideValue2"
            If FieldTag = "398" Then Return "NoBidDescriptors"
            If FieldTag = "399" Then Return "BidDescriptorType"
            If FieldTag = "400" Then Return "BidDescriptor"
            If FieldTag = "401" Then Return "SideValueInd"
            If FieldTag = "402" Then Return "LiquidityPctLow"
            If FieldTag = "403" Then Return "LiquidityPctHigh"
            If FieldTag = "404" Then Return "LiquidityValue"
            If FieldTag = "405" Then Return "EFPTrackingError"
            If FieldTag = "406" Then Return "FairValue"
            If FieldTag = "407" Then Return "OutsideIndexPct"
            If FieldTag = "408" Then Return "ValueOfFutures"
            If FieldTag = "409" Then Return "LiquidityIndType"
            If FieldTag = "410" Then Return "WtAverageLiquidity"
            If FieldTag = "411" Then Return "ExchangeForPhysical"
            If FieldTag = "412" Then Return "OutMainCntryUIndex"
            If FieldTag = "413" Then Return "CrossPercent"
            If FieldTag = "414" Then Return "ProgRptReqs"
            If FieldTag = "415" Then Return "ProgPeriodInterval"
            If FieldTag = "416" Then Return "IncTaxInd"
            If FieldTag = "417" Then Return "NumBidders"
            If FieldTag = "418" Then Return "TradeType"
            If FieldTag = "419" Then Return "BasisPxType"
            If FieldTag = "420" Then Return "NoBidComponents"
            If FieldTag = "421" Then Return "Country"
            If FieldTag = "422" Then Return "TotNoStrikes"
            If FieldTag = "423" Then Return "PriceType"
            If FieldTag = "424" Then Return "DayOrderQty"
            If FieldTag = "425" Then Return "DayCumQty"
            If FieldTag = "426" Then Return "DayAvgPx"
            If FieldTag = "427" Then Return "GTBookingInst"
            If FieldTag = "428" Then Return "NoStrikes"
            If FieldTag = "429" Then Return "ListStatusType"
            If FieldTag = "430" Then Return "NetGrossInd"
            If FieldTag = "431" Then Return "ListOrderStatus"
            If FieldTag = "432" Then Return "ExpireDate"
            If FieldTag = "433" Then Return "ListExecInstType"
            If FieldTag = "434" Then Return "CxlRejResponseTo"
            If FieldTag = "435" Then Return "UnderlyingCouponRate"
            If FieldTag = "436" Then Return "UnderlyingContractMultiplier"
            If FieldTag = "437" Then Return "ContraTradeQty"
            If FieldTag = "438" Then Return "ContraTradeTime"
            If FieldTag = "439" Then Return "ClearingFirm"
            If FieldTag = "440" Then Return "ClearingAccount"
            If FieldTag = "441" Then Return "LiquidityNumSecurities"
            If FieldTag = "442" Then Return "MultiLegReportingType"
            If FieldTag = "443" Then Return "StrikeTime"
            If FieldTag = "444" Then Return "ListStatusText"
            If FieldTag = "445" Then Return "EncodedListStatusTextLen"
            If FieldTag = "446" Then Return "EncodedListStatusText"


            If FieldTag = "10" Then Return "CheckSum"
            If FieldTag = "89" Then Return "Signature"
            If FieldTag = "93" Then Return "SignatureLength"


        End If


    End Function

    Private Function GetCheckIsHeaderField(ByVal VersionId As String, ByVal FieldTag As String) As Boolean

        If VersionId = "FIXT.1.1" Then
            If FieldTag = "8" Then Return True
            If FieldTag = "9" Then Return True
            If FieldTag = "34" Then Return True
            If FieldTag = "35" Then Return True
            If FieldTag = "43" Then Return True
            If FieldTag = "49" Then Return True
            If FieldTag = "50" Then Return True
            If FieldTag = "52" Then Return True
            If FieldTag = "56" Then Return True
            If FieldTag = "57" Then Return True
            If FieldTag = "90" Then Return True
            If FieldTag = "91" Then Return True
            If FieldTag = "97" Then Return True
            If FieldTag = "115" Then Return True
            If FieldTag = "116" Then Return True
            If FieldTag = "122" Then Return True
            If FieldTag = "128" Then Return True
            If FieldTag = "129" Then Return True
            If FieldTag = "142" Then Return True
            If FieldTag = "143" Then Return True
            If FieldTag = "144" Then Return True
            If FieldTag = "145" Then Return True
            If FieldTag = "212" Then Return True
            If FieldTag = "213" Then Return True
            If FieldTag = "347" Then Return True
            If FieldTag = "369" Then Return True
            If FieldTag = "627" Then Return True
            If FieldTag = "628" Then Return True
            If FieldTag = "629" Then Return True
            If FieldTag = "630" Then Return True
            If FieldTag = "1128" Then Return True
        ElseIf VersionId = "FIX.4.2" Then
            If FieldTag = "8" Then Return True
            If FieldTag = "9" Then Return True
            If FieldTag = "34" Then Return True
            If FieldTag = "35" Then Return True
            If FieldTag = "43" Then Return True
            If FieldTag = "49" Then Return True
            If FieldTag = "50" Then Return True
            If FieldTag = "52" Then Return True
            If FieldTag = "56" Then Return True
            If FieldTag = "57" Then Return True
            If FieldTag = "90" Then Return True
            If FieldTag = "91" Then Return True
            If FieldTag = "97" Then Return True
            If FieldTag = "115" Then Return True
            If FieldTag = "116" Then Return True
            If FieldTag = "122" Then Return True
            If FieldTag = "128" Then Return True
            If FieldTag = "129" Then Return True
            If FieldTag = "142" Then Return True
            If FieldTag = "143" Then Return True
            If FieldTag = "144" Then Return True
            If FieldTag = "145" Then Return True
            If FieldTag = "212" Then Return True
            If FieldTag = "213" Then Return True
            If FieldTag = "347" Then Return True
            If FieldTag = "369" Then Return True
            If FieldTag = "370" Then Return True
        End If

        Return False
    End Function

    Private Function GetCheckIsTrailerField(ByVal VersionId As String, ByVal FieldTag As String) As Boolean

        If VersionId = "FIXT.1.1" Then
            If FieldTag = "10" Then Return True
            If FieldTag = "89" Then Return True
            If FieldTag = "93" Then Return True
        ElseIf VersionId = "FIX.4.2" Then
            If FieldTag = "10" Then Return True
            If FieldTag = "89" Then Return True
            If FieldTag = "93" Then Return True
        End If

        Return False
    End Function

    Public Function CheckDBNull(ByVal ItemValue As Object, ByVal IfNullValue As Object) As Object
        If IsDBNull(ItemValue) Or ItemValue Is Nothing Then Return IfNullValue Else Return ItemValue
    End Function

    Public Function getDataTable(ByVal strSQL As String, ByVal ConnectionString As String, Optional ByVal CommandTimeout As Integer = 30) As System.Data.DataTable
        Dim cnn As SqlConnection = Nothing
        Dim dt As System.Data.DataTable
        Dim cmd As SqlCommand
        Dim adp As SqlDataAdapter

        Try
            If Not cnn Is Nothing Then
                If cnn.State = System.Data.ConnectionState.Open Then cnn.Close()
            End If
            cnn = New SqlConnection(ConnectionString)
            cnn.Open()
            cmd = New SqlCommand(strSQL, cnn)
            cmd.CommandTimeout = CommandTimeout
            adp = New SqlDataAdapter
            adp.SelectCommand = cmd
            dt = New System.Data.DataTable
            adp.Fill(dt)
            adp.Dispose()

            cnn.Close()
            cnn.Dispose()
            Return dt
        Catch
            If Not cnn Is Nothing Then
                cnn.Close()
            End If
            Throw
        End Try
    End Function

End Class


Public Class RawDataRow
    Public Property Raw As String
    Public Property LogDate As Date
    Public Property IO As String
    Public Property MessageType As String
    Public Property ClOrderId As String
    Public Property OrigClOrderId As String
    Public Property OrderId As String
    Public Property GTPOrderId As String
    Public Property Message As String
    Public Property MessageSeqNum As String
    Public Property LogFileChannel As String

    Public Sub New(ByVal m_Raw As String)
        Raw = m_Raw
    End Sub
    Public Sub New(ByVal m_Raw As String, ByVal m_LogFileChannel As String)
        Raw = m_Raw
        LogFileChannel = m_LogFileChannel
    End Sub
End Class

Public Class ProcessOrderDataRow
    Public Property ExecType As String
    Public Property ClOrderId As String
    Public Property OrigClOrderId As String
    Public Property OrderId As String
    Public Property GTPOrderId As String
    Public Property Account As String
    Public Property BS As String
    Public Property SecCode As String
    Public Property Units As Integer
    Public Property RealizedUnits As Integer
    Public Property RemainingUnits As Integer
    Public Property AvgPrice As Double
    Public Property LastPx As Double
    Public Property SeqNumber As Double

    Public Sub New(ByVal m_ExecType As String, ByVal m_ClOrderId As String, ByVal m_OrigClOrderId As String, ByVal m_OrderId As String, ByVal m_GTPOrderId As String, ByVal m_Account As String, ByVal m_BS As String, ByVal m_SecCode As String, ByVal m_Units As Integer, ByVal m_RealizedUnits As Integer, ByVal m_RemainingUnits As Integer, ByVal m_AvgPrice As Double, ByVal m_LastPx As Double, ByVal m_SeqNumber As Double)
        ExecType = m_ExecType
        ClOrderId = m_ClOrderId
        OrigClOrderId = m_OrigClOrderId
        OrderId = m_OrderId
        GTPOrderId = m_GTPOrderId
        Account = m_Account
        BS = m_BS
        SecCode = m_SecCode
        Units = m_Units
        RealizedUnits = m_RealizedUnits
        RemainingUnits = m_RemainingUnits
        AvgPrice = m_AvgPrice
        LastPx = m_LastPx
        SeqNumber = m_SeqNumber
    End Sub
End Class

Public Class OrderDataRow
    Public Property Account As String
    Public Property ClOrderId As String
    Public Property OrigClOrderId As String
    Public Property OrderId As String
    Public Property GTPOrderId As String
    Public Property BS As String
    Public Property SecCode As String
    Public Property Units As Integer
    Public Property RealizedUnits As Integer
    Public Property RemainingUnits As Integer
    Public Property AvgPrice As Double
    Public Property LastPx As Double

    Public Sub New(ByVal m_Account As String, ByVal m_ClOrderId As String, ByVal m_OrigClOrderId As String, ByVal m_OrderId As String, ByVal m_GTPOrderId As String, ByVal m_BS As String, ByVal m_SecCode As String, ByVal m_Units As Integer, ByVal m_RealizedUnits As Integer, ByVal m_RemainingUnits As Integer, ByVal m_AvgPrice As Double, ByVal m_LastPx As Double)
        Account = m_Account
        ClOrderId = m_ClOrderId
        OrigClOrderId = m_OrigClOrderId
        OrderId = m_OrderId
        GTPOrderId = m_GTPOrderId
        BS = m_BS
        SecCode = m_SecCode
        Units = m_Units
        RealizedUnits = m_RealizedUnits
        RemainingUnits = m_RemainingUnits
        AvgPrice = m_AvgPrice
        LastPx = m_LastPx
    End Sub
End Class

Public Class GroupedOrderDataRow
    Public Property Account As String
    Public Property BS As String
    Public Property SecCode As String
    Public Property Name As String
    Public Property RealizedUnits As Integer
    Public Property RealizedTotal As Double
    Public Property AvgPrice As Double
    Public Property LastPx As Double

    Public Sub New(ByVal m_Account As String, ByVal m_BS As String, ByVal m_SecCode As String, ByVal m_Name As String, ByVal m_RealizedUnits As Integer, ByVal m_RealizedTotal As Integer, ByVal m_AvgPrice As Double, ByVal m_LastPx As Double)
        Account = m_Account
        BS = m_BS
        SecCode = m_SecCode
        Name = m_Name
        RealizedUnits = m_RealizedUnits
        RealizedTotal = m_RealizedTotal
        AvgPrice = m_AvgPrice
        LastPx = m_LastPx
    End Sub
End Class


