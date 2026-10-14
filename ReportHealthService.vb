Imports System.Configuration
Imports System.Data
Imports System.Data.Entity.Core.EntityClient
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Linq
Imports System.Text.RegularExpressions

Public NotInheritable Class ReportHealthService
    Private Shared ReadOnly FormulaReferencePattern As New Regex(
        "(?<![A-Za-z0-9_])\d+(?![A-Za-z0-9_])",
        RegexOptions.Compiled Or RegexOptions.CultureInvariant)

    Private Sub New()
    End Sub

    Public Shared Function CheckConfiguredReport(ByVal reportId As Long) As ReportHealthResult
        Dim setting As ConnectionStringSettings = ConfigurationManager.ConnectionStrings("ReportingSuiteEntities3")
        If setting Is Nothing OrElse String.IsNullOrWhiteSpace(setting.ConnectionString) Then
            Throw New ConfigurationErrorsException("ReportingSuiteEntities3 is not configured.")
        End If

        Dim entityConnection As New EntityConnectionStringBuilder(setting.ConnectionString)
        Return CheckReport(entityConnection.ProviderConnectionString, reportId)
    End Function

    Public Shared Function CheckReport(ByVal connectionString As String,
                                       ByVal reportId As Long) As ReportHealthResult
        If reportId <= 0 Then
            Throw New ArgumentOutOfRangeException("reportId")
        End If
        If String.IsNullOrWhiteSpace(connectionString) Then
            Throw New ArgumentException("A source connection string is required.", "connectionString")
        End If

        Dim nodes As Dictionary(Of Long, ReportFormulaNode) = LoadFormulaNodes(connectionString, reportId)
        Dim rootIds As List(Of Long) = nodes.Values.
            Where(Function(node) node.ReportId.HasValue AndAlso
                                  node.ReportId.Value = reportId AndAlso
                                  IsDynamicProcess(node.ProcessTypeId)).
            Select(Function(node) node.LineItemColumnId).
            Distinct().
            OrderBy(Function(itemId) itemId).
            ToList()

        Dim graph As New Dictionary(Of Long, List(Of Long))()
        For Each node As ReportFormulaNode In nodes.Values
            graph(node.LineItemColumnId) = GetDependencies(node, nodes)
        Next

        Dim result As New ReportHealthResult With {
            .ReportId = reportId,
            .CheckedAtUtc = DateTime.UtcNow,
            .EvaluatedMappingCount = nodes.Values.
                Where(Function(node) node.ReportId.HasValue AndAlso node.ReportId.Value = reportId).
                Count(),
            .DynamicMappingCount = rootIds.Count
        }

        Dim states As New Dictionary(Of Long, Integer)()
        Dim stack As New List(Of Long)()
        Dim stackIndexes As New Dictionary(Of Long, Integer)()
        Dim cycleKeys As New HashSet(Of String)(StringComparer.Ordinal)
        Dim cycles As New List(Of List(Of Long))()
        Dim maximumDepth As Integer = 0

        For Each rootId As Long In rootIds
            Visit(rootId, graph, states, stack, stackIndexes, cycleKeys, cycles, maximumDepth)
        Next

        result.EvaluatedDependencyCount = states.Count
        result.MaximumDependencyDepth = maximumDepth

        For Each cycle As List(Of Long) In cycles
            Dim uniqueCycleIds As IEnumerable(Of Long) = cycle.Take(cycle.Count - 1).Distinct()
            For Each itemId As Long In uniqueCycleIds
                result.RecursiveLineItemColumnIds.Add(itemId)
            Next

            Dim primaryId As Long = cycle(0)
            Dim selfReference As Boolean = cycle.Count = 2 AndAlso cycle(0) = cycle(1)
            result.Issues.Add(New ReportHealthIssue With {
                .Code = If(selfReference, "SELF_REFERENCE", "CIRCULAR_REFERENCE"),
                .Title = If(selfReference,
                            "A line-item formula references itself",
                            "Line-item formulas contain a circular dependency"),
                .Detail = String.Join(" -> ", cycle.Select(Function(itemId) DescribeNode(itemId, nodes))),
                .PrimaryLineItemColumnId = primaryId
            })
        Next

        result.IsHealthy = result.Issues.Count = 0
        Return result
    End Function

    Private Shared Function LoadFormulaNodes(ByVal connectionString As String,
                                             ByVal reportId As Long) As Dictionary(Of Long, ReportFormulaNode)
        Const selectSql As String =
            "SELECT lc.LineItemColumnID, lc.ProcessTypeID, lc.Is_CalculatedValue, lc.Expression, " &
            "lc.StoredProc, lc.Is_Inherited, lc.Inherited_Lineitem_ColumnID, " &
            "rl.ReportID, rl.Line_itemName, rc.ColumnName " &
            "FROM dbo.LineItem_Columns AS lc " &
            "LEFT JOIN dbo.Report_LineItems AS rl ON rl.Report_LineItemID = lc.Report_LineItemID " &
            "LEFT JOIN dbo.Report_Columns AS rc ON rc.Report_ColumnID = lc.Report_ColumnID"

        Dim nodes As New Dictionary(Of Long, ReportFormulaNode)()
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Using command As New SqlCommand(selectSql & " WHERE rl.ReportID = @ReportID", connection)
                command.CommandTimeout = 60
                command.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
                Using reader As SqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess)
                    ReadFormulaNodes(reader, nodes)
                End Using
            End Using

            Dim attemptedIds As New HashSet(Of Long)(nodes.Keys)
            Dim pendingIds As New Queue(Of Long)()
            For Each node As ReportFormulaNode In nodes.Values.ToList()
                EnqueueUnresolvedDependencies(node, attemptedIds, pendingIds)
            Next

            While pendingIds.Count > 0
                Dim batchIds As New List(Of Long)()
                While pendingIds.Count > 0 AndAlso batchIds.Count < 500
                    Dim pendingId As Long = pendingIds.Dequeue()
                    If attemptedIds.Add(pendingId) Then
                        batchIds.Add(pendingId)
                    End If
                End While

                If batchIds.Count = 0 Then
                    Continue While
                End If

                Dim parameterNames As New List(Of String)()
                Using command As New SqlCommand(String.Empty, connection)
                    command.CommandTimeout = 60
                    For index As Integer = 0 To batchIds.Count - 1
                        Dim parameterName As String = "@Item" & index.ToString(CultureInfo.InvariantCulture)
                        parameterNames.Add(parameterName)
                        command.Parameters.Add(parameterName, SqlDbType.BigInt).Value = batchIds(index)
                    Next
                    command.CommandText = selectSql &
                        " WHERE lc.LineItemColumnID IN (" & String.Join(",", parameterNames) & ")"

                    Dim nodeCountBeforeQuery As Integer = nodes.Count
                    Using reader As SqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess)
                        ReadFormulaNodes(reader, nodes)
                    End Using

                    If nodes.Count > nodeCountBeforeQuery Then
                        For Each loadedId As Long In batchIds
                            Dim loadedNode As ReportFormulaNode = Nothing
                            If nodes.TryGetValue(loadedId, loadedNode) Then
                                EnqueueUnresolvedDependencies(loadedNode, attemptedIds, pendingIds)
                            End If
                        Next
                    End If
                End Using
            End While
        End Using
        Return nodes
    End Function

    Private Shared Sub ReadFormulaNodes(ByVal reader As SqlDataReader,
                                        ByVal nodes As IDictionary(Of Long, ReportFormulaNode))
        While reader.Read()
            Dim node As New ReportFormulaNode With {
                .LineItemColumnId = Convert.ToInt64(reader("LineItemColumnID"), CultureInfo.InvariantCulture),
                .ProcessTypeId = ReadNullableInt64(reader("ProcessTypeID")),
                .IsCalculated = Convert.ToBoolean(reader("Is_CalculatedValue"), CultureInfo.InvariantCulture),
                .Expression = ReadString(reader("Expression")),
                .StoredProcedure = ReadString(reader("StoredProc")),
                .IsInherited = Convert.ToBoolean(reader("Is_Inherited"), CultureInfo.InvariantCulture),
                .InheritedLineItemColumnId = ReadNullableInt64(reader("Inherited_Lineitem_ColumnID")),
                .ReportId = ReadNullableInt64(reader("ReportID")),
                .LineItemName = CleanLabel(ReadString(reader("Line_itemName"))),
                .ColumnName = CleanLabel(ReadString(reader("ColumnName")))
            }
            nodes(node.LineItemColumnId) = node
        End While
    End Sub

    Private Shared Sub EnqueueUnresolvedDependencies(ByVal node As ReportFormulaNode,
                                                     ByVal attemptedIds As ISet(Of Long),
                                                     ByVal pendingIds As Queue(Of Long))
        For Each dependencyId As Long In GetReferencedIds(node)
            If Not attemptedIds.Contains(dependencyId) Then
                pendingIds.Enqueue(dependencyId)
            End If
        Next
    End Sub

    Private Shared Function GetDependencies(ByVal node As ReportFormulaNode,
                                            ByVal nodes As IDictionary(Of Long, ReportFormulaNode)) As List(Of Long)
        Return GetReferencedIds(node).
            Where(Function(itemId) nodes.ContainsKey(itemId)).
            Distinct().
            OrderBy(Function(itemId) itemId).
            ToList()
    End Function

    Private Shared Function GetReferencedIds(ByVal node As ReportFormulaNode) As List(Of Long)
        Dim dependencies As New HashSet(Of Long)()

        If node.IsInherited AndAlso
            node.InheritedLineItemColumnId.HasValue AndAlso
            String.IsNullOrWhiteSpace(node.Expression) AndAlso
            String.IsNullOrWhiteSpace(node.StoredProcedure) Then
            dependencies.Add(node.InheritedLineItemColumnId.Value)
        End If

        Dim expressionToInspect As String = node.Expression
        If node.ProcessTypeId.HasValue AndAlso node.ProcessTypeId.Value = 4 Then
            Dim advancedParts As String() = If(expressionToInspect, String.Empty).
                Split(New Char() {","c}, 3, StringSplitOptions.None)
            expressionToInspect = If(advancedParts.Length >= 3, advancedParts(2), String.Empty)
        End If

        If Not String.IsNullOrWhiteSpace(expressionToInspect) Then
            For Each match As Match In FormulaReferencePattern.Matches(expressionToInspect)
                Dim referencedId As Long
                If Long.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, referencedId) Then
                    dependencies.Add(referencedId)
                End If
            Next
        End If

        Return dependencies.OrderBy(Function(itemId) itemId).ToList()
    End Function

    Private Shared Sub Visit(ByVal itemId As Long,
                             ByVal graph As IDictionary(Of Long, List(Of Long)),
                             ByVal states As IDictionary(Of Long, Integer),
                             ByVal stack As IList(Of Long),
                             ByVal stackIndexes As IDictionary(Of Long, Integer),
                             ByVal cycleKeys As ISet(Of String),
                             ByVal cycles As IList(Of List(Of Long)),
                             ByRef maximumDepth As Integer)
        Dim currentState As Integer = 0
        If states.ContainsKey(itemId) Then
            currentState = states(itemId)
        End If
        If currentState = 2 Then
            Return
        End If

        states(itemId) = 1
        stackIndexes(itemId) = stack.Count
        stack.Add(itemId)
        maximumDepth = Math.Max(maximumDepth, stack.Count)

        Dim dependencies As List(Of Long) = Nothing
        If graph.TryGetValue(itemId, dependencies) Then
            For Each dependencyId As Long In dependencies
                Dim dependencyState As Integer = 0
                If states.ContainsKey(dependencyId) Then
                    dependencyState = states(dependencyId)
                End If

                If dependencyState = 0 Then
                    Visit(dependencyId, graph, states, stack, stackIndexes, cycleKeys, cycles, maximumDepth)
                ElseIf dependencyState = 1 AndAlso stackIndexes.ContainsKey(dependencyId) Then
                    Dim cycle As New List(Of Long)()
                    For index As Integer = stackIndexes(dependencyId) To stack.Count - 1
                        cycle.Add(stack(index))
                    Next
                    cycle.Add(dependencyId)

                    Dim cycleKey As String = CreateCycleKey(cycle)
                    If cycleKeys.Add(cycleKey) Then
                        cycles.Add(cycle)
                    End If
                End If
            Next
        End If

        stack.RemoveAt(stack.Count - 1)
        stackIndexes.Remove(itemId)
        states(itemId) = 2
    End Sub

    Private Shared Function CreateCycleKey(ByVal cycle As IList(Of Long)) As String
        Dim body As List(Of Long) = cycle.Take(cycle.Count - 1).ToList()
        Dim minimumId As Long = body.Min()
        Dim startIndex As Integer = body.IndexOf(minimumId)
        Dim normalized As New List(Of Long)()
        For offset As Integer = 0 To body.Count - 1
            normalized.Add(body((startIndex + offset) Mod body.Count))
        Next
        Return String.Join(">", normalized.Select(Function(itemId) itemId.ToString(CultureInfo.InvariantCulture)))
    End Function

    Private Shared Function DescribeNode(ByVal itemId As Long,
                                         ByVal nodes As IDictionary(Of Long, ReportFormulaNode)) As String
        Dim node As ReportFormulaNode = Nothing
        If Not nodes.TryGetValue(itemId, node) Then
            Return "Line-item column " & itemId.ToString(CultureInfo.InvariantCulture)
        End If

        Dim labels As New List(Of String)()
        If Not String.IsNullOrWhiteSpace(node.LineItemName) Then
            labels.Add(node.LineItemName)
        End If
        If Not String.IsNullOrWhiteSpace(node.ColumnName) Then
            labels.Add(node.ColumnName)
        End If
        Dim label As String = If(labels.Count = 0, "Line-item column", String.Join(" / ", labels))
        Return label & " [" & itemId.ToString(CultureInfo.InvariantCulture) & "]"
    End Function

    Private Shared Function IsDynamicProcess(ByVal processTypeId As Long?) As Boolean
        Return processTypeId.HasValue AndAlso
               (processTypeId.Value = 2 OrElse processTypeId.Value = 3 OrElse processTypeId.Value = 4)
    End Function

    Private Shared Function ReadNullableInt64(ByVal value As Object) As Long?
        If value Is Nothing OrElse Convert.IsDBNull(value) Then
            Return Nothing
        End If
        Return Convert.ToInt64(value, CultureInfo.InvariantCulture)
    End Function

    Private Shared Function ReadString(ByVal value As Object) As String
        If value Is Nothing OrElse Convert.IsDBNull(value) Then
            Return String.Empty
        End If
        Return Convert.ToString(value, CultureInfo.InvariantCulture)
    End Function

    Private Shared Function CleanLabel(ByVal value As String) As String
        Return Regex.Replace(If(value, String.Empty), "\s+", " ").Trim()
    End Function

    Private NotInheritable Class ReportFormulaNode
        Public Property LineItemColumnId As Long
        Public Property ProcessTypeId As Long?
        Public Property IsCalculated As Boolean
        Public Property Expression As String
        Public Property StoredProcedure As String
        Public Property IsInherited As Boolean
        Public Property InheritedLineItemColumnId As Long?
        Public Property ReportId As Long?
        Public Property LineItemName As String
        Public Property ColumnName As String
    End Class
End Class

Public NotInheritable Class ReportHealthResult
    Public Sub New()
        Issues = New List(Of ReportHealthIssue)()
        RecursiveLineItemColumnIds = New HashSet(Of Long)()
    End Sub

    Public Property ReportId As Long
    Public Property IsHealthy As Boolean
    Public Property HasCheckError As Boolean
    Public Property CheckErrorMessage As String
    Public Property CheckedAtUtc As DateTime
    Public Property EvaluatedMappingCount As Integer
    Public Property DynamicMappingCount As Integer
    Public Property EvaluatedDependencyCount As Integer
    Public Property MaximumDependencyDepth As Integer
    Public Property Issues As List(Of ReportHealthIssue)
    Public Property RecursiveLineItemColumnIds As HashSet(Of Long)

    Public Function IsRecursiveMapping(ByVal lineItemColumnId As Long) As Boolean
        Return RecursiveLineItemColumnIds.Contains(lineItemColumnId)
    End Function

    Public Shared Function Failed(ByVal reportId As Long, ByVal message As String) As ReportHealthResult
        Return New ReportHealthResult With {
            .ReportId = reportId,
            .IsHealthy = False,
            .HasCheckError = True,
            .CheckErrorMessage = message,
            .CheckedAtUtc = DateTime.UtcNow
        }
    End Function
End Class

Public NotInheritable Class ReportHealthIssue
    Public Property Code As String
    Public Property Title As String
    Public Property Detail As String
    Public Property PrimaryLineItemColumnId As Long
End Class
