Imports ReportingSuite.ReportingSuiteEntities3

Public Class Action
    Inherits System.Web.UI.Page

    Public action As String
    Public parameter As String
    Public parameter2 As String

    Public db As New ReportingSuiteEntities3
    Public ers As New VSABEntities
    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load

        Try
            action = Request.QueryString("operation")
            parameter = Request.QueryString("item")



            If Request.HttpMethod = "POST" Then


                If action = "submitReportColumnUpdate" Then

                    Dim id = CInt(parameter)
                    Dim rc = db.Report_Columns.Single(Function(f) f.Report_ColumnID = id)

                    For Each lc In rc.LineItem_Columns
                        lc.Updated = False
                    Next
                    db.SaveChanges()
                    Dim count = 0
                    For Each lc In rc.LineItem_Columns
                        count = count + 1
                        If Not Request.Form("AddMonthToReportingPeriod_" & rc.Report_ColumnID.ToString) Is Nothing Then
                            lc.AddMonthToReportingPeriod = CInt(Request.Form("AddMonthToReportingPeriod_" & rc.Report_ColumnID.ToString))
                            rc.AddMonthToReportingPeriod = CInt(Request.Form("AddMonthToReportingPeriod_" & rc.Report_ColumnID.ToString))

                        End If
                        If Not Request.Form("QueryWhere_" & rc.Report_ColumnID.ToString) Is Nothing Then
                            lc.QueryWhere = Request.Form("QueryWhere_" & rc.Report_ColumnID.ToString)
                            rc.QueryWhere = Request.Form("QueryWhere_" & rc.Report_ColumnID.ToString)

                        End If
                        If Request.Form("SchemaFieldName_" & rc.Report_ColumnID.ToString) = "" Then

                            If Not Request.Form("Column_Is_Calculated_Value_" & rc.Report_ColumnID.ToString) Is Nothing Then
                                If Request.Form("Column_Is_Calculated_Value_" & rc.Report_ColumnID.ToString) = "Yes" Then
                                    lc.Is_CalculatedValue = True
                                    rc.Is_Calculated = True
                                    lc.ProcessTypeID = 2

                                    lc.TableName = Nothing
                                    lc.ColumnName = Nothing
                                    lc.StoredProc = Nothing
                                    lc.SchemaFieldName = Nothing


                                Else
                                    lc.Is_CalculatedValue = False
                                    rc.Is_Calculated = False
                                    lc.ProcessTypeID = Nothing
                                    lc.Expression = Nothing
                                End If
                            Else
                                lc.Is_CalculatedValue = False
                                rc.Is_Calculated = False
                                lc.ProcessTypeID = Nothing
                                lc.Expression = Nothing
                            End If

                            If Not Request.Form("Column_Is_Static_Value_" & rc.Report_ColumnID.ToString) Is Nothing Then
                                If Request.Form("Column_Is_Static_Value_" & rc.Report_ColumnID.ToString) = "Yes" Then
                                    lc.Is_Static_Value = True
                                    rc.Is_Static_Value = True
                                Else
                                    lc.Is_Static_Value = False
                                    rc.Is_Static_Value = False
                                End If
                            Else
                                lc.Is_Static_Value = False
                                rc.Is_Static_Value = False
                            End If

                            If Not Request.Form("Is_Inherited_" & rc.Report_ColumnID.ToString) Is Nothing Then
                                If Request.Form("Is_Inherited_" & rc.Report_ColumnID.ToString) = "1" Then
                                    lc.Is_Inherited = True
                                    rc.Is_Inherited = True
                                    lc.ProcessTypeID = 3
                                    lc.EntityGroupBasedMapping = False
                                    If Not Request.Form("Inherited_Report_ColumnID_" & rc.Report_ColumnID.ToString) Is Nothing Then
                                        rc.Inherited_Report_ColumnID = CInt(Request.Form("Inherited_Report_ColumnID_" & rc.Report_ColumnID.ToString))

                                        Dim irc = db.Report_Columns.Single(Function(f) f.Report_ColumnID = rc.Inherited_Report_ColumnID)

                                        If irc.ReportID = rc.ReportID Then
                                            Dim calcItem = irc.LineItem_Columns.Single(Function(f) f.Report_ColumnID = irc.Report_ColumnID And f.Report_LineItemID = lc.Report_LineItemID)

                                            If Not Request.Form("Inherited_Type_" & rc.Report_ColumnID.ToString) Is Nothing Then
                                                If Request.Form("Inherited_Type_" & rc.Report_ColumnID.ToString) = "0" Then
                                                    lc.Inherited_Lineitem_ColumnID = calcItem.LineItemColumnID
                                                Else
                                                    lc.TableName = calcItem.TableName
                                                    lc.ColumnName = calcItem.ColumnName
                                                    lc.ProcessTypeID = 1
                                                    lc.Is_Inherited = False
                                                    rc.Is_Inherited = False
                                                End If
                                            End If

                                        Else
                                            Try
                                                Dim calcItem = irc.LineItem_Columns.Single(Function(f) f.Report_ColumnID = irc.Report_ColumnID And f.Report_LineItems.Sort_Order = count)

                                                If Not Request.Form("Inherited_Type_" & rc.Report_ColumnID.ToString) Is Nothing Then
                                                    If Request.Form("Inherited_Type_" & rc.Report_ColumnID.ToString) = "0" Then
                                                        lc.Inherited_Lineitem_ColumnID = calcItem.LineItemColumnID
                                                    Else
                                                        lc.TableName = calcItem.TableName
                                                        lc.ColumnName = calcItem.ColumnName
                                                        lc.ProcessTypeID = 1
                                                        lc.Is_Inherited = False
                                                        rc.Is_Inherited = False
                                                    End If
                                                End If
                                            Catch ex As Exception

                                            End Try

                                        End If



                                    End If

                                Else
                                    lc.Is_Inherited = False
                                    rc.Is_Inherited = False
                                    lc.Inherited_Lineitem_ColumnID = Nothing
                                End If
                            Else
                                lc.Is_Inherited = False
                                rc.Is_Inherited = False
                                lc.Inherited_Lineitem_ColumnID = Nothing
                            End If

                        End If
                        If Request.Form("SchemaFieldName_" & rc.Report_ColumnID.ToString) Is Nothing Then

                        End If

                        'If Not Request.Form("SchemaFieldName_" & rc.Report_ColumnID.ToString) Is Nothing Then
                        '    If Request.Form("SchemaFieldName_" & rc.Report_ColumnID.ToString) <> "" Then
                        '        Dim Text = Request.Form("SchemaFieldName_" & rc.Report_ColumnID.ToString)
                        '        Dim lines As String() = Text.Split(Environment.NewLine)
                        '        Dim lcount = 0
                        '        For Each line As String In lines
                        '            lcount = lcount + 1
                        '            If count = lcount Then
                        '                If lc.Is_CalculatedValue = False And lc.Is_Inherited = False Then
                        '                    lc.SchemaFieldName = line



                        '                    Try
                        '                        Dim schema = db.Schemata.First(Function(f) f.FieldName = lc.SchemaFieldName)
                        '                        If Not schema Is Nothing Then
                        '                            'lc.SchemaID = schema.SchemaID
                        '                            lc.TableName = schema.DatabaseTable
                        '                            lc.ColumnName = schema.DatabaseField




                        '                        End If
                        '                    Catch ex As Exception

                        '                    End Try
                        '                End If
                        '            End If
                        '        Next

                        '    End If
                        'End If

                    Next

                    db.SaveChanges()
                    For Each lc In rc.LineItem_Columns
                        lc.Updated = True
                    Next
                    db.SaveChanges()

                End If



                If action = "submitReportRowUpdate" Then

                    Dim id = CInt(parameter)
                    Dim rc = db.Report_LineItems.Single(Function(f) f.Report_LineItemID = id)

                    For Each lc In rc.LineItem_Columns
                        lc.Updated = False
                    Next
                    db.SaveChanges()
                    Dim count = 0
                    For Each lc In rc.LineItem_Columns
                        count = count + 1
                        If Not Request.Form("AddMonthToReportingPeriod_" & rc.Report_LineItemID.ToString) Is Nothing Then
                            lc.AddMonthToReportingPeriod = CInt(Request.Form("AddMonthToReportingPeriod_" & rc.Report_LineItemID.ToString))
                            rc.AddMonthToReportingPeriod = CInt(Request.Form("AddMonthToReportingPeriod_" & rc.Report_LineItemID.ToString))

                        End If

                        If Not Request.Form("QueryWhere_" & rc.Report_LineItemID.ToString) Is Nothing Then
                            lc.QueryWhere = Request.Form("QueryWhere_" & rc.Report_LineItemID.ToString)
                            rc.QueryWhere = Request.Form("QueryWhere_" & rc.Report_LineItemID.ToString)

                        End If

                        If Not Request.Form("Row_Is_Calculated_Value_" & rc.Report_LineItemID.ToString) Is Nothing Then
                            If Request.Form("Row_Is_Calculated_Value_" & rc.Report_LineItemID.ToString) = "Yes" Then
                                rc.Is_Calculated_Value = True
                                lc.Is_CalculatedValue = True
                                lc.ProcessTypeID = 2

                                lc.TableName = Nothing
                                lc.ColumnName = Nothing
                                lc.StoredProc = Nothing
                                lc.SchemaFieldName = Nothing


                            Else
                                rc.Is_Calculated_Value = False
                                lc.Is_CalculatedValue = False
                                lc.ProcessTypeID = Nothing
                                lc.Expression = Nothing
                            End If
                        Else
                            lc.Is_CalculatedValue = False
                            lc.ProcessTypeID = Nothing
                            lc.Expression = Nothing
                            rc.Is_Calculated_Value = False
                        End If

                        If Not Request.Form("Row_Is_Static_Value_" & rc.Report_LineItemID.ToString) Is Nothing Then
                            If Request.Form("Row_Is_Static_Value_" & rc.Report_LineItemID.ToString) = "Yes" Then
                                lc.Is_Static_Value = True
                                rc.Is_Static_Value = True
                            Else
                                lc.Is_Static_Value = False
                                rc.Is_Static_Value = False
                            End If
                        Else
                            lc.Is_Static_Value = False
                            rc.Is_Static_Value = False
                        End If

                        If Not Request.Form("Is_Inherited_" & rc.Report_LineItemID.ToString) Is Nothing Then
                            If Request.Form("Is_Inherited_" & rc.Report_LineItemID.ToString) = "1" Then
                                lc.Is_Inherited = True
                                rc.Is_Inherited = True
                                lc.ProcessTypeID = 3
                                lc.EntityGroupBasedMapping = False
                                If Not Request.Form("Inherited_Report_LineItemID_" & rc.Report_LineItemID.ToString) Is Nothing Then
                                    rc.Inherited_Report_LineItemID = CInt(Request.Form("Inherited_Report_LineItemID_" & rc.Report_LineItemID.ToString))

                                    Dim irc = db.Report_LineItems.Single(Function(f) f.Report_LineItemID = rc.Inherited_Report_LineItemID)

                                    If irc.ReportID = rc.ReportID Then
                                        Dim calcItem = irc.LineItem_Columns.Single(Function(f) f.Report_LineItemID = irc.Report_LineItemID And f.Report_ColumnID = lc.Report_ColumnID)

                                        lc.Inherited_Lineitem_ColumnID = calcItem.LineItemColumnID

                                    Else
                                        Try
                                            Dim calcItem = irc.LineItem_Columns.Single(Function(f) f.Report_LineItemID = irc.Report_LineItemID And f.Report_Columns.Sort_Order = count)

                                            lc.Inherited_Lineitem_ColumnID = calcItem.LineItemColumnID
                                        Catch ex As Exception

                                        End Try

                                    End If


                                End If

                            Else
                                lc.Is_Inherited = False
                                rc.Is_Inherited = False
                                lc.Inherited_Lineitem_ColumnID = Nothing
                            End If
                        Else
                            lc.Is_Inherited = False
                            rc.Is_Inherited = False
                            lc.Inherited_Lineitem_ColumnID = Nothing
                        End If
                    Next

                    db.SaveChanges()
                    For Each lc In rc.LineItem_Columns
                        lc.Updated = True
                    Next
                    db.SaveChanges()

                End If

                If action = "submitEReportRowUpdate" Then

                    Dim id = CInt(parameter)
                    Dim egc = db.EntityGroupLineItem_Columns.Single(Function(f) f.EntityGroupLineItem_ColumnID = id)
                    egc.Updated = False
                    db.SaveChanges()

                    egc.Updated = True
                    egc.CountDataType = Request.Form("ECountDataType_" & egc.EntityGroupLineItem_ColumnID.ToString)
                    egc.CountParameter = Request.Form("ECountParameter_" & egc.EntityGroupLineItem_ColumnID.ToString)
                    egc.TableName = Request.Form("ETableName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                    egc.ColumnName = Request.Form("EColumnName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                    'egc.StoredProc = Request.Form("EStoredProc_" & egc.EntityGroupLineItem_ColumnID.ToString)


                    Dim Eold As String = egc.SchemaFieldName
                    egc.SchemaFieldName = Request.Form("ESchemaFieldName_" & egc.EntityGroupLineItem_ColumnID.ToString)

                    If Not egc.SchemaFieldName Is Nothing Then
                        If (egc.SchemaFieldName.Contains("+") Or egc.SchemaFieldName.Contains("-") Or egc.SchemaFieldName.Contains("/") Or egc.SchemaFieldName.Contains("*")) And egc.SchemaFieldName.Contains("[") And egc.SchemaFieldName.Contains("]") Then

                            If Eold <> egc.SchemaFieldName Then
                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("+", "+,")
                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("-", "-,")
                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("/", "/,")
                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("*", "*,")
                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("[", "")
                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("]", "")

                                Dim fields = egc.SchemaFieldName.Split(",")
                                egc.SchemaFieldName = Request.Form("ESchemaFieldName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                Dim expression As String = ""
                                Dim count As Integer = 1
                                For Each fs In fields

                                    Dim operation As String
                                    Dim fieldName As String
                                    If fs.Contains("+") Or fs.Contains("-") Or fs.Contains("/") Or fs.Contains("*") Then
                                        operation = fs.Substring(fs.Length - 1)
                                        fieldName = fs.Substring(0, fs.Length - 1)
                                    Else
                                        operation = ""
                                        fieldName = fs
                                    End If


                                    Dim func As String = "dbo.getRLIV_" & egc.LineItem_Columns.Report_LineItemID.ToString & "_" & egc.LineItemColumnID.ToString & "_" & egc.EntityGroupLineItem_ColumnID.ToString & "_" & count.ToString
                                    Try
                                        Dim schema = db.Schemata.First(Function(f) f.FieldName = fieldName)
                                        If Not schema Is Nothing Then
                                            'lc.SchemaID = schema.SchemaID
                                            'lc.TableName = schema.DatabaseTable
                                            'lc.ColumnName = schema.DatabaseField

                                            db.CreateStoredFunctionsv3(schema.DatabaseTable, schema.DatabaseField, 1, func, egc.LineItem_Columns.Report_Columns.DataType, egc.LineItem_Columns.QueryWhere)
                                            expression = expression & func & operation

                                        End If
                                    Catch ex As Exception

                                    End Try
                                    count = count + 1

                                Next

                                egc.Is_CalculatedValue = True
                                egc.Expression = expression

                            End If




                        Else

                            If Not Request.Form("EUpdateSchema_" & egc.EntityGroupLineItem_ColumnID.ToString) Is Nothing Then
                                Try
                                    Dim schema = db.Schemata.First(Function(f) f.FieldName = egc.SchemaFieldName)
                                    If Not schema Is Nothing Then
                                        'lc.SchemaID = schema.SchemaID
                                        egc.TableName = schema.DatabaseTable
                                        egc.ColumnName = schema.DatabaseField




                                    End If
                                Catch ex As Exception

                                End Try
                            End If

                            egc.Expression = Request.Form("EExpression_" & egc.EntityGroupLineItem_ColumnID.ToString)

                            If Not Request.Form("EIs_Calculated_Value_" & egc.EntityGroupLineItem_ColumnID.ToString) Is Nothing Then
                                If Request.Form("EIs_Calculated_Value_" & egc.EntityGroupLineItem_ColumnID.ToString) = "Yes" Then
                                    egc.Is_CalculatedValue = True

                                Else
                                    egc.Is_CalculatedValue = False
                                End If
                            Else
                                egc.Is_CalculatedValue = False
                            End If
                            If Request.Form("EColumnName_" & egc.EntityGroupLineItem_ColumnID.ToString) = "" Or Request.Form("ETableName_" & egc.EntityGroupLineItem_ColumnID.ToString) = "" Then
                                egc.StoredProc = ""
                            End If
                        End If
                    End If
                    Try
                        egc.Updated = True
                        db.SaveChanges()
                        egc.Updated = False
                        db.SaveChanges()
                        egc.Updated = True
                        db.SaveChanges()
                    Catch ex As Exception
                        Response.Write(ex.ToString)
                        Response.Write(ex.InnerException)
                    End Try


                End If



                If action = "submitColumnLineItemUpdate" Then

                    Dim id = CInt(parameter)
                    Dim lc = db.LineItem_Columns.Single(Function(f) f.LineItemColumnID = id)

                    If lc.Report_Columns.Show Then
                        If Not Request.Form("EntityGroupBasedMapping_" & lc.LineItemColumnID.ToString) Is Nothing Then



                            lc.AggregationTypeID = Long.Parse(Request.Form("AggregationTypeID_" & lc.LineItemColumnID.ToString))

                            If lc.Report_Columns.DataType <> "decimal" Then
                                lc.AggregationTypeID = 3
                            End If
                            lc.CountDataType = Request.Form("CountDataType_" & lc.LineItemColumnID.ToString)
                            lc.CountParameter = Request.Form("CountParameter_" & lc.LineItemColumnID.ToString)
                            lc.TableName = Request.Form("TableName_" & lc.LineItemColumnID.ToString)
                            lc.ColumnName = Request.Form("ColumnName_" & lc.LineItemColumnID.ToString)
                            lc.StoredProc = Request.Form("StoredProc_" & lc.LineItemColumnID.ToString)
                            lc.QueryWhere = Request.Form("QueryWhere_" & lc.LineItemColumnID.ToString)
                            lc.ProcessTypeID = 1

                            If lc.Is_Static_Value Then
                                lc.Static_Value = Request.Form("Static_Value_" & lc.LineItemColumnID.ToString)
                            End If

                            If Not Request.Form("Is_Calculated_Value_" & lc.LineItemColumnID.ToString) Is Nothing Then
                                lc.ProcessTypeID = 2
                                If Request.Form("Is_Calculated_Value_" & lc.LineItemColumnID.ToString) = "Yes" Then
                                    lc.Is_CalculatedValue = True

                                Else
                                    lc.Is_CalculatedValue = False
                                End If
                            Else
                                lc.Is_CalculatedValue = False
                            End If

                            If Not Request.Form("Is_Static_Value_" & lc.LineItemColumnID.ToString) Is Nothing Then

                                If Request.Form("Is_Static_Value_" & lc.LineItemColumnID.ToString) = "Yes" Then
                                    lc.Is_Static_Value = True

                                Else
                                    lc.Is_Static_Value = False
                                End If
                            Else
                                lc.Is_Static_Value = False
                            End If

                            If Not Request.Form("Is_Inherited_" & lc.LineItemColumnID.ToString) Is Nothing Then
                                If Request.Form("Is_Inherited_" & lc.LineItemColumnID.ToString) = "1" Then
                                    lc.Is_Inherited = True
                                    lc.ProcessTypeID = 3
                                    If Not Request.Form("Inherited_Lineitem_ColumnID_" & lc.LineItemColumnID.ToString) Is Nothing Then
                                        lc.Inherited_Lineitem_ColumnID = CInt(Request.Form("Inherited_Lineitem_ColumnID_" & lc.LineItemColumnID.ToString))

                                    End If

                                Else
                                    lc.Is_Inherited = False
                                    lc.Inherited_Lineitem_ColumnID = Nothing
                                End If
                            Else
                                lc.Is_Inherited = False
                                lc.Inherited_Lineitem_ColumnID = Nothing
                            End If


                            Try
                                lc.AddMonthToReportingPeriod = CInt(Request.Form("AddMonthToReportingPeriod_" & lc.LineItemColumnID.ToString))
                            Catch ex As Exception
                                lc.AddMonthToReportingPeriod = 0
                            End Try

                            lc.Updated = False

                            If Not Request.Form("EntityGroupBasedMapping_" & lc.LineItemColumnID.ToString) Is Nothing Then
                                If Request.Form("EntityGroupBasedMapping_" & lc.LineItemColumnID.ToString) = "1" Then
                                    lc.EntityGroupBasedMapping = True

                                    If lc.EntityGroupLineItem_Columns.Count <> lc.Report_LineItems.Report.ReportEntityGroups.Count Then
                                        For Each reg In lc.Report_LineItems.Report.ReportEntityGroups
                                            If lc.EntityGroupLineItem_Columns.Where(Function(ff) ff.EntityGroupID = reg.EntityGroupID).Count = 0 Then
                                                Dim egc As New EntityGroupLineItem_Columns
                                                egc.EntityGroupID = reg.EntityGroupID
                                                egc.LineItemColumnID = lc.LineItemColumnID

                                                lc.EntityGroupLineItem_Columns.Add(egc)

                                            End If
                                        Next
                                    End If

                                    For Each egc In lc.EntityGroupLineItem_Columns
                                        egc.CountDataType = Request.Form("ECountDataType_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                        egc.CountParameter = Request.Form("ECountParameter_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                        egc.TableName = Request.Form("ETableName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                        egc.ColumnName = Request.Form("EColumnName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                        egc.StoredProc = Request.Form("EStoredProc_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                        egc.Updated = False


                                        Dim Eold As String = egc.SchemaFieldName
                                        egc.SchemaFieldName = Request.Form("ESchemaFieldName_" & egc.EntityGroupLineItem_ColumnID.ToString)

                                        If Not egc.SchemaFieldName Is Nothing Then
                                            If (egc.SchemaFieldName.Contains("+") Or egc.SchemaFieldName.Contains("-") Or egc.SchemaFieldName.Contains("/") Or egc.SchemaFieldName.Contains("*")) And egc.SchemaFieldName.Contains("[") And egc.SchemaFieldName.Contains("]") Then

                                                If Eold <> egc.SchemaFieldName Then
                                                    egc.SchemaFieldName = egc.SchemaFieldName.Replace("+", "+,")
                                                    egc.SchemaFieldName = egc.SchemaFieldName.Replace("-", "-,")
                                                    egc.SchemaFieldName = egc.SchemaFieldName.Replace("/", "/,")
                                                    egc.SchemaFieldName = egc.SchemaFieldName.Replace("*", "*,")
                                                    egc.SchemaFieldName = egc.SchemaFieldName.Replace("[", "")
                                                    egc.SchemaFieldName = egc.SchemaFieldName.Replace("]", "")

                                                    Dim fields = egc.SchemaFieldName.Split(",")
                                                    egc.SchemaFieldName = Request.Form("ESchemaFieldName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                                    Dim expression As String = ""
                                                    Dim count As Integer = 1
                                                    For Each fs In fields

                                                        Dim operation As String
                                                        Dim fieldName As String
                                                        If fs.Contains("+") Or fs.Contains("-") Or fs.Contains("/") Or fs.Contains("*") Then
                                                            operation = fs.Substring(fs.Length - 1)
                                                            fieldName = fs.Substring(0, fs.Length - 1)
                                                        Else
                                                            operation = ""
                                                            fieldName = fs
                                                        End If


                                                        Dim func As String = "dbo.getRLIV_" & lc.Report_LineItemID.ToString & "_" & lc.LineItemColumnID.ToString & "_" & egc.EntityGroupLineItem_ColumnID.ToString & "_" & count.ToString
                                                        Try
                                                            fieldName = fieldName.Replace("<>", "-")
                                                            Dim schema = db.GetSchemaDatabaseColumnByFieldName(fieldName)

                                                            If Not schema Is Nothing Then
                                                                For Each s In schema

                                                                    Dim path = s.path
                                                                    path = path.Replace("//", "/")
                                                                    path = path.Substring(0, (path.Length - fieldName.Length) - 1)

                                                                    Dim group = db.GetSchemaGroupByPath(path)

                                                                    For Each g In group
                                                                        db.CreateStoredFunctionsv3(g.table_name, s.column_name, 1, func, lc.Report_Columns.DataType, lc.QueryWhere)
                                                                        expression = expression & func & operation
                                                                    Next


                                                                Next
                                                            End If

                                                        Catch ex As Exception

                                                        End Try
                                                        count = count + 1

                                                    Next

                                                    egc.Is_CalculatedValue = True
                                                    egc.Expression = expression

                                                End If




                                            Else

                                                If Not Request.Form("EUpdateSchema_" & egc.EntityGroupLineItem_ColumnID.ToString) Is Nothing Then
                                                    Try
                                                        Dim schema = db.Schemata.First(Function(f) f.FieldName = egc.SchemaFieldName)
                                                        If Not schema Is Nothing Then
                                                            'lc.SchemaID = schema.SchemaID
                                                            egc.TableName = schema.DatabaseTable
                                                            egc.ColumnName = schema.DatabaseField




                                                        End If
                                                    Catch ex As Exception

                                                    End Try
                                                End If

                                                egc.Expression = Request.Form("EExpression_" & egc.EntityGroupLineItem_ColumnID.ToString)

                                                If Not Request.Form("EIs_Calculated_Value_" & egc.EntityGroupLineItem_ColumnID.ToString) Is Nothing Then
                                                    If Request.Form("EIs_Calculated_Value_" & egc.EntityGroupLineItem_ColumnID.ToString) = "Yes" Then
                                                        egc.Is_CalculatedValue = True

                                                    Else
                                                        egc.Is_CalculatedValue = False
                                                    End If
                                                Else
                                                    egc.Is_CalculatedValue = False
                                                End If
                                                If Request.Form("EColumnName_" & egc.EntityGroupLineItem_ColumnID.ToString) = "" Or Request.Form("ETableName_" & egc.EntityGroupLineItem_ColumnID.ToString) = "" Then
                                                    egc.StoredProc = ""
                                                End If
                                            End If
                                        End If

                                    Next

                                Else
                                    lc.EntityGroupBasedMapping = False
                                End If
                            End If

                            Dim old As String = lc.SchemaFieldName
                            lc.SchemaFieldName = Request.Form("SchemaFieldName_" & lc.LineItemColumnID.ToString)

                            If Not lc.SchemaFieldName Is Nothing Then
                                If (lc.SchemaFieldName.Contains("+") Or lc.SchemaFieldName.Contains("-") Or lc.SchemaFieldName.Contains("/") Or lc.SchemaFieldName.Contains("*")) And lc.SchemaFieldName.Contains("[") And lc.SchemaFieldName.Contains("]") Then

                                    lc.SchemaFieldName = Helper.ProcessSchemaName(lc.SchemaFieldName)


                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("+", "+,")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("-", "-,")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("/", "/,")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("*", "*,")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("[", "")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("]", "")

                                    Dim fields = lc.SchemaFieldName.Split(",")
                                    lc.SchemaFieldName = Request.Form("SchemaFieldName_" & lc.LineItemColumnID.ToString)
                                    Dim expression As String = ""
                                    Dim count As Integer = 1
                                    For Each fs In fields

                                        Dim operation As String
                                        Dim fieldName As String
                                        If fs.Contains("+") Or fs.Contains("-") Or fs.Contains("/") Or fs.Contains("*") Then
                                            operation = fs.Substring(fs.Length - 1)
                                            fieldName = fs.Substring(0, fs.Length - 1)
                                        Else
                                            operation = ""
                                            fieldName = fs
                                        End If


                                        Dim func As String = "dbo.getRLIV_" & lc.Report_LineItemID.ToString & "_" & lc.LineItemColumnID.ToString & "_" & count.ToString
                                        Try
                                            fieldName = fieldName.Replace("<>", "-")
                                            fieldName = fieldName.Replace("{}", "/")
                                            Dim schema = db.GetSchemaDatabaseColumnByFieldName(fieldName)
                                            Dim tableName = ""
                                            Dim columnName = ""
                                            If Not schema Is Nothing Then
                                                For Each s In schema

                                                    Dim path = s.path
                                                    path = path.Replace("//", "/")
                                                    path = path.Substring(0, (path.Length - fieldName.Length) - 1)

                                                    Dim group = db.GetSchemaGroupByPath(path)

                                                    For Each g In group
                                                        tableName = g.table_name
                                                        columnName = s.column_name
                                                    Next


                                                Next
                                            End If

                                            If tableName <> "" And columnName <> "" Then
                                                db.CreateStoredFunctionsv3(tableName, columnName, 1, func, lc.Report_Columns.DataType, lc.QueryWhere)
                                                expression = expression & func & operation
                                            End If
                                        Catch ex As Exception

                                        End Try
                                        count = count + 1

                                    Next

                                    lc.Is_CalculatedValue = True
                                    lc.Expression = expression



                                ElseIf lc.SchemaFieldName.StartsWith("RS_SUM_") Then
                                    Dim calculation As String = ""
                                    If lc.SchemaFieldName = "RS_SUM_COLUMN" Then
                                        For Each cc In lc.Report_Columns.LineItem_Columns.Where(Function(f) f.LineItemColumnID <> lc.LineItemColumnID)

                                            If cc.Is_CalculatedValue Then
                                                If Not cc.Expression Is Nothing And cc.Expression <> "" Then
                                                    calculation = calculation & cc.Expression & "+"
                                                End If

                                            Else
                                                If Not cc.StoredProc Is Nothing And cc.StoredProc <> "" Then

                                                    calculation = calculation & cc.StoredProc & "+"
                                                End If
                                            End If




                                        Next
                                        If calculation.Length > 0 Then
                                            calculation = calculation.Substring(0, calculation.Length - 1)

                                            lc.Is_CalculatedValue = True
                                            lc.ProcessTypeID = 2
                                            lc.Expression = calculation


                                        End If

                                    ElseIf lc.SchemaFieldName = "RS_SUM_ROW" Then

                                    End If

                                Else

                                    If Not Request.Form("UpdateSchema_" & lc.LineItemColumnID.ToString) Is Nothing Or Not Request.Form("UpdateAllSchema") Is Nothing Then
                                        Try
                                            Dim schema = db.Schemata.First(Function(f) f.FieldName = lc.SchemaFieldName)
                                            If Not schema Is Nothing Then
                                                'lc.SchemaID = schema.SchemaID
                                                lc.TableName = schema.DatabaseTable
                                                lc.ColumnName = schema.DatabaseField




                                            End If
                                        Catch ex As Exception

                                        End Try
                                    End If




                                    lc.Expression = Request.Form("Expression_" & lc.LineItemColumnID.ToString)

                                    If Not Request.Form("Is_Calculated_Value_" & lc.LineItemColumnID.ToString) Is Nothing Then
                                        If Request.Form("Is_Calculated_Value_" & lc.LineItemColumnID.ToString) = "Yes" Then
                                            lc.Is_CalculatedValue = True
                                            lc.ProcessTypeID = 2
                                        Else
                                            lc.Is_CalculatedValue = False
                                        End If
                                    Else
                                        lc.Is_CalculatedValue = False
                                    End If
                                    If Request.Form("ColumnName_" & lc.LineItemColumnID.ToString) = "" Or Request.Form("TableName_" & lc.LineItemColumnID.ToString) = "" Then
                                        lc.StoredProc = ""
                                    End If
                                End If
                            End If

















                            'Console.WriteLine("Column: " & lc.LineItemColumnID.ToString & "<br/>"
                            '   )

                            Try
                                db.SaveChanges()
                            Catch ex As Exception
                                Response.Write(ex.ToString)
                                Response.Write(ex.InnerException)
                            End Try


                        End If

                    End If




                    Dim formula As String = ""
                    If lc.Is_CalculatedValue Then
                        If Not lc.Expression Is Nothing Or lc.Expression <> "" Then
                            Dim func As String
                            func = lc.Expression
                            func = func.Replace("+", "|")
                            func = func.Replace("-", "|")
                            func = func.Replace("*", "|")
                            func = func.Replace("/", "|")

                            Dim items = func.Split("|")

                            For Each i In items
                                If Not i.StartsWith("dbo") Then
                                    Try
                                        func = lc.Expression.Replace(i, Helper.getBaseFormula(i, func, db))
                                    Catch ex As Exception

                                    End Try

                                End If
                            Next


                            lc.RuntimeExpression = func
                        End If
                    End If

                    lc.Updated = True
                    db.SaveChanges()
                End If




                If action = "submitColumnLineItemCalc" Then

                    Dim id = CInt(parameter)
                    Dim lc = db.LineItem_Columns.Single(Function(f) f.LineItemColumnID = id)

                    If Not Request.Form("Type_" & lc.LineItemColumnID.ToString) Is Nothing And Request.Form("Type_" & lc.LineItemColumnID.ToString) <> "" Then
                        If Request.Form("Type_" & lc.LineItemColumnID.ToString) = "1" Then
                            lc.Expression = Request.Form("Expression_" & lc.LineItemColumnID.ToString)
                            lc.ProcessTypeID = 2


                        End If

                        If Request.Form("Type_" & lc.LineItemColumnID.ToString) = "2" Then


                            If Not Request.Form("Expression_" & lc.LineItemColumnID.ToString) Is Nothing And Not Request.Form("Expression2_" & lc.LineItemColumnID.ToString) Is Nothing And Not Request.Form("Expression3_" & lc.LineItemColumnID.ToString) Is Nothing Then
                                lc.Expression = Request.Form("Expression_" & lc.LineItemColumnID.ToString) & "," & Request.Form("Expression2_" & lc.LineItemColumnID.ToString) & "," & Request.Form("Expression3_" & lc.LineItemColumnID.ToString)

                                lc.ProcessTypeID = 4
                            End If

                        End If
                    End If




                    lc.Updated = True

                    db.SaveChanges()
                End If


                If action = "submitColumnLineItemCalcAllC" Then

                    Dim id = CInt(parameter)
                    Dim rc = db.Report_Columns.Single(Function(f) f.Report_ColumnID = id)

                    rc.Expression = Request.Form("ExpressionAllC_" & rc.Report_ColumnID.ToString)

                    If Not rc.Expression Is Nothing And rc.Expression <> "" Then

                        For Each lic In rc.LineItem_Columns

                            Dim originalFormula = rc.Expression
                            Dim columnFormula = rc.Expression
                            Dim formalaItems = rc.Expression

                            formalaItems = formalaItems.Replace("+", ",")
                            formalaItems = formalaItems.Replace("-", ",")
                            formalaItems = formalaItems.Replace("/", ",")
                            formalaItems = formalaItems.Replace("*", ",")

                            For Each i In formalaItems.Split(",")
                                Try
                                    Dim rcid = CInt(i)
                                    Dim rc_calc = db.Report_Columns.Single(Function(f) f.Report_ColumnID = rcid)

                                    Dim sourceLC = db.LineItem_Columns.Single(Function(f) f.Report_ColumnID = rc_calc.Report_ColumnID And f.Report_LineItemID = lic.Report_LineItemID)

                                    columnFormula = columnFormula.Replace(i, sourceLC.LineItemColumnID.ToString)

                                    If sourceLC.Is_CalculatedValue Then
                                        columnFormula = columnFormula.Replace(i, sourceLC.Expression)
                                    Else
                                        If sourceLC.Is_Inherited And Not sourceLC.Inherited_Lineitem_ColumnID Is Nothing Then
                                            columnFormula = columnFormula.Replace(i, sourceLC.LineItemColumnID.ToString())
                                        Else
                                            columnFormula = columnFormula.Replace(i, sourceLC.StoredProc)
                                        End If
                                    End If
                                Catch ex As Exception
                                End Try
                            Next
                            lic.Expression = columnFormula
                            lic.ProcessTypeID = Long.Parse("2")
                        Next
                    Else
                        For Each lic In rc.LineItem_Columns.Where(Function(f) f.Show)
                            lic.Expression = Nothing
                            lic.ProcessTypeID = Nothing
                        Next
                    End If
                    db.SaveChanges()
                End If


                If action = "submitColumnLineItemCalcAllR" Then

                    Dim id = CInt(parameter)
                    Dim rc = db.Report_LineItems.Single(Function(f) f.Report_LineItemID = id)

                    Dim displayF = Request.Form("ExpressionAllR_" & rc.Report_LineItemID.ToString)
                    Dim hiddenF = Request.Form("formulaDisplay_" & rc.Report_LineItemID.ToString)

                    If displayF = hiddenF Then
                        rc.Expression = hiddenF
                    Else
                        If (hiddenF Is Nothing Or hiddenF = "") And (Not displayF Is Nothing And displayF <> "") Then
                            rc.Expression = displayF
                        Else
                            rc.Expression = Nothing
                        End If
                    End If

                    'rc.Expression = Request.Form("ExpressionAllR_" & rc.Report_LineItemID.ToString)

                    If Not rc.Expression Is Nothing And rc.Expression <> "" Then

                        For Each lic In rc.LineItem_Columns

                            Dim originalFormula = rc.Expression
                            Dim columnFormula = rc.Expression
                            Dim formalaItems = rc.Expression

                            formalaItems = formalaItems.Replace("+", ",")
                            formalaItems = formalaItems.Replace("-", ",")
                            formalaItems = formalaItems.Replace("/", ",")
                            formalaItems = formalaItems.Replace("*", ",")

                            For Each i In formalaItems.Split(",")
                                Try
                                    Dim rcid = CInt(i)
                                    Dim rc_calc = db.Report_LineItems.Single(Function(f) f.Report_LineItemID = rcid)

                                    Dim sourceLC = db.LineItem_Columns.Single(Function(f) f.Report_ColumnID = lic.Report_ColumnID And f.Report_LineItemID = rc_calc.Report_LineItemID)

                                    columnFormula = columnFormula.Replace(i, sourceLC.LineItemColumnID.ToString)

                                    If sourceLC.Is_CalculatedValue Then
                                        columnFormula = columnFormula.Replace(i, sourceLC.Expression)
                                    Else
                                        If sourceLC.Is_Inherited And Not sourceLC.Inherited_Lineitem_ColumnID Is Nothing Then
                                            columnFormula = columnFormula.Replace(i, sourceLC.LineItemColumnID.ToString())
                                        Else
                                            columnFormula = columnFormula.Replace(i, sourceLC.StoredProc)
                                        End If
                                    End If
                                Catch ex As Exception
                                End Try
                            Next
                            lic.Expression = columnFormula
                            lic.ProcessTypeID = Long.Parse("2")
                            lic.Is_CalculatedValue = True
                        Next
                    Else
                        For Each lic In rc.LineItem_Columns.Where(Function(f) f.Show)
                            lic.Expression = Nothing
                            lic.ProcessTypeID = Nothing
                        Next
                    End If
                    db.SaveChanges()
                End If

            End If




            If action = "updateFieldOnChange" Then
                Dim table = Request.Form("table")
                Dim field = Request.Form("field")
                Dim value = Request.Form("value")
                Dim idfield = Request.Form("idfield")
                Dim idvalue = Request.Form("idvalue")

                If Not ReportAccessService.IsCurrentUserAdministrator(Context) Then
                    Response.StatusCode = 403
                    Response.Write("Administrator access is required.")
                    Return
                End If

                If Not String.Equals(table, "Report_LineItems", StringComparison.Ordinal) OrElse
                   Not String.Equals(idfield, "Report_LineItemID", StringComparison.Ordinal) Then
                    Response.StatusCode = 400
                    Response.Write("The requested Designer update is not supported.")
                    Return
                End If

                Dim lineItemId As Long
                If Not Long.TryParse(idvalue, lineItemId) Then
                    Response.StatusCode = 400
                    Response.Write("The line item identifier is invalid.")
                    Return
                End If

                Dim lineItem = db.Report_LineItems.SingleOrDefault(Function(item) item.Report_LineItemID = lineItemId)
                If lineItem Is Nothing Then
                    Response.StatusCode = 404
                    Response.Write("The line item could not be found.")
                    Return
                End If

                Dim numericValue As Integer
                Select Case field
                    Case "Sort_Order"
                        If Not Integer.TryParse(value, numericValue) Then
                            Response.StatusCode = 400
                            Response.Write("Sort order must be a number.")
                            Return
                        End If
                        lineItem.Sort_Order = numericValue
                    Case "Line_itemName"
                        lineItem.Line_itemName = If(value, String.Empty).Trim()
                    Case "Is_Main"
                        lineItem.Is_Main = String.Equals(value, "1", StringComparison.Ordinal) OrElse
                                           String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) OrElse
                                           String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                    Case "Is_Title"
                        lineItem.Is_Title = String.Equals(value, "1", StringComparison.Ordinal) OrElse
                                            String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) OrElse
                                            String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                    Case "Show"
                        lineItem.Show = String.Equals(value, "1", StringComparison.Ordinal) OrElse
                                        String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) OrElse
                                        String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                    Case "ReportSectionID"
                        If Not Integer.TryParse(value, numericValue) OrElse
                           Not db.ReportSections.Any(Function(section) section.ReportSectionID = numericValue AndAlso section.ReportID = lineItem.ReportID) Then
                            Response.StatusCode = 400
                            Response.Write("Select a section that belongs to this report.")
                            Return
                        End If
                        lineItem.ReportSectionID = numericValue
                    Case Else
                        Response.StatusCode = 400
                        Response.Write("The requested Designer field is not supported.")
                        Return
                End Select

                db.SaveChanges()
                Response.ContentType = "text/plain"
                Response.Write("OK")
            End If

        Catch ex As Exception
            Response.Write(ex.Message)
            Response.Write(ex.StackTrace)
        End Try

    End Sub



End Class
