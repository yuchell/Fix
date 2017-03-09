Public Class Errors

    Public ErrorString As String = ""

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Me.Close()
    End Sub

    Private Sub Errors_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.TextBox1.Text = ErrorString
    End Sub
End Class