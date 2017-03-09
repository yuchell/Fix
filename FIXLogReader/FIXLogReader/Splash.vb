Public Class Splash

    Private Sub Splash_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        Timer1.Start()
    End Sub


    Private Sub Timer1_Tick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Timer1.Tick

        Timer1.Stop()

        '==========----------AUTOUPDATER----------==========
        AutoUpdater.VersionCheckAddress = "http://fnygm7216/AutoUpdaterFiles/FIXLogReader/"
        AutoUpdater.VersionCheckFile = "version.txt"
        AutoUpdater.CurrentVersion = 41
        Me.Label1.Text = "v." & AutoUpdater.CurrentVersion
        Application.DoEvents()
        If AutoUpdater.UpdateFiles() = "SUCCESS" Then
            Dispose()
            Application.Exit()
        End If
        '==========----------AUTOUPDATER----------==========
        Dim iCount As Integer
        For iCount = 90 To 10 Step -5
            Me.Opacity = iCount / 100
            Me.Refresh()
            Threading.Thread.Sleep(5)
            Application.DoEvents()
        Next
        Me.Hide()
        Dim zForm1 As New Form1
        zForm1.VersionInfo = AutoUpdater.CurrentVersion

        zForm1.ShowDialog()
        Me.Close()
        Application.Exit()

        GC.Collect()

    End Sub

End Class