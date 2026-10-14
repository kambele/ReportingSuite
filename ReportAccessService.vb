Imports System.Configuration
Imports System.Collections.Generic
Imports System.Data
Imports System.Data.Entity.Core.EntityClient
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Linq
Imports System.Security.Principal
Imports System.Text.RegularExpressions
Imports System.Web

Public NotInheritable Class ReportAccessService
    Private Const ActiveDirectoryUserRole As String = "Active Directory User"
    Private Const ActiveDirectoryGroupRole As String = "Active Directory Group"

    Private Sub New()
    End Sub

    Public Shared Function GetCurrentWindowsAccount(ByVal context As HttpContext) As String
        If context Is Nothing Then
            Return String.Empty
        End If

        If context.Request IsNot Nothing AndAlso
           context.Request.LogonUserIdentity IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(context.Request.LogonUserIdentity.Name) Then
            Return context.Request.LogonUserIdentity.Name.Trim()
        End If

        If context.User IsNot Nothing AndAlso context.User.Identity IsNot Nothing Then
            Return Convert.ToString(context.User.Identity.Name, CultureInfo.InvariantCulture).Trim()
        End If

        Return String.Empty
    End Function

    Public Shared Function IsCurrentUserAdministrator(ByVal context As HttpContext) As Boolean
        Dim accountName As String = GetCurrentWindowsAccount(context)
        If String.IsNullOrWhiteSpace(accountName) Then
            Return False
        End If

        Using connection As New SqlConnection(GetProviderConnectionString())
            connection.Open()
            Using command As New SqlCommand(
                "SELECT CASE WHEN EXISTS " &
                "(SELECT 1 FROM dbo.Users WHERE Username = @AccountName AND Is_Admin = 1) " &
                "THEN 1 ELSE 0 END", connection)
                command.Parameters.Add("@AccountName", SqlDbType.VarChar, 256).Value = accountName
                Return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) = 1
            End Using
        End Using
    End Function

    Public Shared Function GetRules(ByVal reportId As Long) As List(Of ReportAccessRuleInfo)
        If reportId <= 0 Then
            Throw New ArgumentOutOfRangeException("reportId")
        End If

        Dim rules As New List(Of ReportAccessRuleInfo)()
        Using connection As New SqlConnection(GetProviderConnectionString())
            connection.Open()
            Using command As New SqlCommand(
                "SELECT rr.ReportRoleID, rr.ReportID, rr.RoleTypeID, " &
                "COALESCE(rt.RoleTypeName, '') AS RoleTypeName, COALESCE(rr.Grantee, '') AS Grantee " &
                "FROM dbo.ReportRoles AS rr " &
                "LEFT JOIN dbo.RoleTypes AS rt ON rt.RoleTypeID = rr.RoleTypeID " &
                "WHERE rr.ReportID = @ReportID " &
                "ORDER BY rt.RoleTypeName, rr.Grantee", connection)
                command.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
                Using reader As SqlDataReader = command.ExecuteReader()
                    While reader.Read()
                        rules.Add(New ReportAccessRuleInfo With {
                            .ReportRoleId = Convert.ToInt64(reader("ReportRoleID"), CultureInfo.InvariantCulture),
                            .ReportId = Convert.ToInt64(reader("ReportID"), CultureInfo.InvariantCulture),
                            .RoleTypeId = If(reader("RoleTypeID") Is DBNull.Value,
                                             CType(Nothing, Long?),
                                             Convert.ToInt64(reader("RoleTypeID"), CultureInfo.InvariantCulture)),
                            .RoleTypeName = Convert.ToString(reader("RoleTypeName"), CultureInfo.InvariantCulture),
                            .Grantee = Convert.ToString(reader("Grantee"), CultureInfo.InvariantCulture)
                        })
                    End While
                End Using
            End Using
        End Using
        Return rules
    End Function

    Public Shared Function CanAccessReport(ByVal context As HttpContext,
                                           ByVal reportId As Long,
                                           ByRef denialReason As String) As Boolean
        denialReason = String.Empty
        If reportId <= 0 Then
            denialReason = "The requested report is invalid."
            Return False
        End If

        Dim accountName As String = GetCurrentWindowsAccount(context)
        If String.IsNullOrWhiteSpace(accountName) Then
            denialReason = "Your Windows account could not be identified."
            Return False
        End If

        If IsCurrentUserAdministrator(context) Then
            Return True
        End If

        Dim rules As List(Of ReportAccessRuleInfo) = GetRules(reportId)
        If rules.Count = 0 Then
            Return True
        End If

        For Each rule As ReportAccessRuleInfo In rules
            If RuleMatches(context, accountName, rule) Then
                Return True
            End If
        Next

        denialReason = "You do not have permission to access this report. Contact the report owner or Reporting Suite administrator."
        Return False
    End Function

    Public Shared Sub DemandReportAccess(ByVal context As HttpContext, ByVal reportId As Long)
        Dim denialReason As String = Nothing
        If Not CanAccessReport(context, reportId, denialReason) Then
            Throw New UnauthorizedAccessException(denialReason)
        End If
    End Sub

    Public Shared Function GetAccessibleReportIds(ByVal context As HttpContext,
                                                  ByVal reportIds As IEnumerable(Of Long)) As HashSet(Of Long)
        Dim requested As New HashSet(Of Long)(
            If(reportIds, Enumerable.Empty(Of Long)()).Where(Function(value) value > 0))
        If requested.Count = 0 Then
            Return requested
        End If

        Dim accountName As String = GetCurrentWindowsAccount(context)
        If String.IsNullOrWhiteSpace(accountName) Then
            Return New HashSet(Of Long)()
        End If

        If IsCurrentUserAdministrator(context) Then
            Return requested
        End If

        Dim rulesByReport As New Dictionary(Of Long, List(Of ReportAccessRuleInfo))()
        Using connection As New SqlConnection(GetProviderConnectionString())
            connection.Open()
            Dim parameterNames As New List(Of String)()
            Using command As New SqlCommand()
                command.Connection = connection
                Dim parameterIndex As Integer = 0
                For Each reportId As Long In requested
                    Dim parameterName As String = "@ReportID" & parameterIndex.ToString(CultureInfo.InvariantCulture)
                    parameterNames.Add(parameterName)
                    command.Parameters.Add(parameterName, SqlDbType.BigInt).Value = reportId
                    parameterIndex += 1
                Next
                command.CommandText =
                    "SELECT rr.ReportRoleID, rr.ReportID, rr.RoleTypeID, " &
                    "COALESCE(rt.RoleTypeName, '') AS RoleTypeName, COALESCE(rr.Grantee, '') AS Grantee " &
                    "FROM dbo.ReportRoles AS rr " &
                    "LEFT JOIN dbo.RoleTypes AS rt ON rt.RoleTypeID = rr.RoleTypeID " &
                    "WHERE rr.ReportID IN (" & String.Join(",", parameterNames) & ")"
                Using reader As SqlDataReader = command.ExecuteReader()
                    While reader.Read()
                        Dim rule As New ReportAccessRuleInfo With {
                            .ReportRoleId = Convert.ToInt64(reader("ReportRoleID"), CultureInfo.InvariantCulture),
                            .ReportId = Convert.ToInt64(reader("ReportID"), CultureInfo.InvariantCulture),
                            .RoleTypeId = If(reader("RoleTypeID") Is DBNull.Value,
                                             CType(Nothing, Long?),
                                             Convert.ToInt64(reader("RoleTypeID"), CultureInfo.InvariantCulture)),
                            .RoleTypeName = Convert.ToString(reader("RoleTypeName"), CultureInfo.InvariantCulture),
                            .Grantee = Convert.ToString(reader("Grantee"), CultureInfo.InvariantCulture)
                        }
                        If Not rulesByReport.ContainsKey(rule.ReportId) Then
                            rulesByReport(rule.ReportId) = New List(Of ReportAccessRuleInfo)()
                        End If
                        rulesByReport(rule.ReportId).Add(rule)
                    End While
                End Using
            End Using
        End Using

        Dim accessible As New HashSet(Of Long)(requested)
        For Each configured In rulesByReport
            If Not configured.Value.Any(Function(rule) RuleMatches(context, accountName, rule)) Then
                accessible.Remove(configured.Key)
            End If
        Next
        Return accessible
    End Function

    Public Shared Sub SaveReportAccess(ByVal reportId As Long,
                                       ByVal restricted As Boolean,
                                       ByVal usersText As String,
                                       ByVal groupsText As String)
        If reportId <= 0 Then
            Throw New ArgumentOutOfRangeException("reportId")
        End If

        Dim users As List(Of String) = ParseGrantees(usersText)
        Dim groups As List(Of String) = ParseGrantees(groupsText)
        If restricted AndAlso users.Count = 0 AndAlso groups.Count = 0 Then
            Throw New ArgumentException("Add at least one Active Directory user or group before restricting the report.")
        End If

        Using connection As New SqlConnection(GetProviderConnectionString())
            connection.Open()
            Using transaction As SqlTransaction = connection.BeginTransaction(IsolationLevel.ReadCommitted)
                Try
                    Using reportCommand As New SqlCommand(
                        "SELECT COUNT(1) FROM dbo.Reports WHERE ReportID = @ReportID AND Is_Deleted = 0",
                        connection, transaction)
                        reportCommand.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
                        If Convert.ToInt32(reportCommand.ExecuteScalar(), CultureInfo.InvariantCulture) = 0 Then
                            Throw New KeyNotFoundException("The selected report is unavailable.")
                        End If
                    End Using

                    Using deleteCommand As New SqlCommand(
                        "DELETE FROM dbo.ReportRoles WHERE ReportID = @ReportID", connection, transaction)
                        deleteCommand.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
                        deleteCommand.ExecuteNonQuery()
                    End Using

                    If restricted Then
                        Dim userRoleTypeId As Long = EnsureRoleType(connection, transaction, ActiveDirectoryUserRole)
                        Dim groupRoleTypeId As Long = EnsureRoleType(connection, transaction, ActiveDirectoryGroupRole)
                        For Each grantee As String In users
                            InsertRule(connection, transaction, reportId, userRoleTypeId, grantee)
                        Next
                        For Each grantee As String In groups
                            InsertRule(connection, transaction, reportId, groupRoleTypeId, grantee)
                        Next
                    End If

                    transaction.Commit()
                Catch
                    transaction.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Sub

    Private Shared Function ParseGrantees(ByVal value As String) As List(Of String)
        Dim result As New List(Of String)()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each candidate As String In Regex.Split(Convert.ToString(value), "[,;\r\n]+")
            Dim normalized As String = candidate.Trim()
            If normalized.Length = 0 Then
                Continue For
            End If
            If normalized.Length > 100 Then
                Throw New ArgumentException("Active Directory names cannot exceed 100 characters.")
            End If
            If normalized.Any(Function(character) Char.IsControl(character)) Then
                Throw New ArgumentException("Active Directory names cannot contain control characters.")
            End If
            If seen.Add(normalized) Then
                result.Add(normalized)
            End If
        Next
        Return result
    End Function

    Private Shared Function RuleMatches(ByVal context As HttpContext,
                                        ByVal accountName As String,
                                        ByVal rule As ReportAccessRuleInfo) As Boolean
        If rule Is Nothing OrElse String.IsNullOrWhiteSpace(rule.Grantee) Then
            Return False
        End If

        Dim roleTypeName As String = Convert.ToString(rule.RoleTypeName)
        If roleTypeName.IndexOf("user", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return AccountMatches(accountName, rule.Grantee)
        End If

        If roleTypeName.IndexOf("group", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return IsInRole(context, rule.Grantee)
        End If

        Return False
    End Function

    Private Shared Function AccountMatches(ByVal currentAccount As String, ByVal configuredAccount As String) As Boolean
        Dim currentNormalized As String = currentAccount.Trim()
        Dim configuredNormalized As String = configuredAccount.Trim()
        If currentNormalized.Equals(configuredNormalized, StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        If configuredNormalized.IndexOf("\"c) < 0 AndAlso configuredNormalized.IndexOf("@"c) < 0 Then
            Dim separatorIndex As Integer = currentNormalized.LastIndexOf("\"c)
            Dim currentShortName As String = If(separatorIndex >= 0,
                                                currentNormalized.Substring(separatorIndex + 1),
                                                currentNormalized)
            Return currentShortName.Equals(configuredNormalized, StringComparison.OrdinalIgnoreCase)
        End If

        Return False
    End Function

    Private Shared Function IsInRole(ByVal context As HttpContext, ByVal groupName As String) As Boolean
        Try
            If context IsNot Nothing AndAlso context.User IsNot Nothing AndAlso context.User.IsInRole(groupName) Then
                Return True
            End If

            If context IsNot Nothing AndAlso context.Request IsNot Nothing AndAlso
               context.Request.LogonUserIdentity IsNot Nothing AndAlso
               context.Request.LogonUserIdentity.IsAuthenticated Then
                Dim principal As New WindowsPrincipal(context.Request.LogonUserIdentity)
                Return principal.IsInRole(groupName)
            End If
        Catch ex As SystemException
            Return False
        End Try
        Return False
    End Function

    Private Shared Function EnsureRoleType(ByVal connection As SqlConnection,
                                           ByVal transaction As SqlTransaction,
                                           ByVal roleTypeName As String) As Long
        Using selectCommand As New SqlCommand(
            "SELECT TOP (1) RoleTypeID FROM dbo.RoleTypes " &
            "WHERE LOWER(LTRIM(RTRIM(RoleTypeName))) = LOWER(@RoleTypeName)",
            connection, transaction)
            selectCommand.Parameters.Add("@RoleTypeName", SqlDbType.VarChar, 50).Value = roleTypeName
            Dim existing As Object = selectCommand.ExecuteScalar()
            If existing IsNot Nothing AndAlso existing IsNot DBNull.Value Then
                Return Convert.ToInt64(existing, CultureInfo.InvariantCulture)
            End If
        End Using

        Using insertCommand As New SqlCommand(
            "INSERT INTO dbo.RoleTypes (RoleTypeName) VALUES (@RoleTypeName); " &
            "SELECT CAST(SCOPE_IDENTITY() AS bigint);", connection, transaction)
            insertCommand.Parameters.Add("@RoleTypeName", SqlDbType.VarChar, 50).Value = roleTypeName
            Return Convert.ToInt64(insertCommand.ExecuteScalar(), CultureInfo.InvariantCulture)
        End Using
    End Function

    Private Shared Sub InsertRule(ByVal connection As SqlConnection,
                                  ByVal transaction As SqlTransaction,
                                  ByVal reportId As Long,
                                  ByVal roleTypeId As Long,
                                  ByVal grantee As String)
        Using command As New SqlCommand(
            "INSERT INTO dbo.ReportRoles (ReportID, RoleTypeID, Grantee) " &
            "VALUES (@ReportID, @RoleTypeID, @Grantee)", connection, transaction)
            command.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
            command.Parameters.Add("@RoleTypeID", SqlDbType.BigInt).Value = roleTypeId
            command.Parameters.Add("@Grantee", SqlDbType.VarChar, 100).Value = grantee
            command.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Function GetProviderConnectionString() As String
        Dim setting As ConnectionStringSettings = ConfigurationManager.ConnectionStrings("ReportingSuiteEntities3")
        If setting Is Nothing OrElse String.IsNullOrWhiteSpace(setting.ConnectionString) Then
            Throw New ConfigurationErrorsException("ReportingSuiteEntities3 is not configured.")
        End If
        Dim builder As New EntityConnectionStringBuilder(setting.ConnectionString)
        Return builder.ProviderConnectionString
    End Function
End Class

Public Class ReportAccessRuleInfo
    Public Property ReportRoleId As Long
    Public Property ReportId As Long
    Public Property RoleTypeId As Long?
    Public Property RoleTypeName As String
    Public Property Grantee As String

    Public ReadOnly Property IsGroup As Boolean
        Get
            Return Convert.ToString(RoleTypeName).IndexOf("group", StringComparison.OrdinalIgnoreCase) >= 0
        End Get
    End Property
End Class
