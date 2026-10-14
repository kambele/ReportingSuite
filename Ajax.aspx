<%@ Page Title="" Language="vb" AutoEventWireup="false" CodeBehind="Ajax.aspx.vb" Inherits="ReportingSuite.Ajax" %>

<% If action = "schema" Then
        Dim schemas = db.GetSchemaDatabaseColumnByFieldName(parameter)
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select id="<%=Request.QueryString("field") %>_<%=Request.QueryString("id") %>" class="form-control" onchange="<%=Request.QueryString("prefix") %>update_select_value('<%=Request.QueryString("id") %>', this);">
            <option>--PLEASE SELECT (Parameter: <%=parameter %>)--</option>

            <%
                For Each s In schemas
                    Dim path = s.path
                    path = path.Replace("//", "/")
                    path = path.Substring(0, (path.Length - parameter.Length) - 1)

                    Dim group = db.GetSchemaGroupByPath(path)

                    For Each g In group
            %><option value="<%=g.table_name %>,<%=s.column_name %>,<%=path %>"><%=g.name %> - <%=s.path %> </option>
            <%
                    Next

                Next
            %>
        </select>
    </div>
</div>
<%
%>

<%
    End If%>

<% If action = "smart_suggest" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Try
            Dim lc = db.LineItem_Columns.Single(Function(f) f.LineItemColumnID = id)

            Dim lname As String = lc.Report_LineItems.Line_itemName
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select id="<%=Request.QueryString("field") %>_<%=Request.QueryString("id") %>" class="form-control" onchange="<%=Request.QueryString("prefix") %>update_select_value('<%=Request.QueryString("id") %>', this);">
            <option>--PLEASE SELECT--</option>

            <%
                Dim p = Request.QueryString("formset")
                Dim found = 0
                For Each s In db.GetSchemaItems2.ToList.Where(Function(f) f.path.Contains(p))
                    Dim path = s.path
                    path = path.Substring(0, path.LastIndexOf("/"))

                    Dim group = db.GetSchemaGroupByPath(path)


                    For Each g In group
                        Dim contain = False
                        'For Each n In lc.Report_Columns.ColumnName.Split(" ")
                        '    If s.path.ToLower.Contains(n.ToLower) Then
                        '        contain = True
                        '    End If
                        'Next
                        'For Each n In lc.Report_LineItems.Line_itemName.Split(" ")

                        'Next

                        Dim searchKey = lc.Report_Columns.ColumnName
                        searchKey = searchKey.Replace(" ", "")
                        searchKey = searchKey.Replace("|", "")
                        'searchKey = searchKey.Replace("-", "")
                        searchKey = searchKey.Replace("&", "")
                        searchKey = searchKey.ToLower


                        Dim pathhKey = s.path.Substring(s.path.LastIndexOf("/"))
                        pathhKey = pathhKey.Replace(" ", "")
                        pathhKey = pathhKey.Replace("|", "")
                        'searchKey = searchKey.Replace("-", "")
                        pathhKey = pathhKey.Replace("&", "")
                        pathhKey = pathhKey.ToLower
                        'If s.path.ToLower.Contains(lc.Report_LineItems.Line_itemName.Replace(" ", "").ToLower) Then
                        '    contain = True
                        'End If
                        If pathhKey.Contains(searchKey) Then
                            contain = True
                        End If
                        If contain Then
                            found = found + 1
            %><option value="<%=g.table_name %>,<%=s.column_name %>"><%=g.name %> - <%=s.path %></option>
            <%
                        End If

                    Next

                Next

                If found = 0 Then
                    For Each s In db.GetSchemaItems2.ToList.Where(Function(f) f.path.Contains(p))
                        Dim path = s.path
                        path = path.Substring(0, path.LastIndexOf("/"))

                        Dim group = db.GetSchemaGroupByPath(path)


                        For Each g In group
                            Dim contain = False
                            'For Each n In lc.Report_Columns.ColumnName.Split(" ")
                            '    If s.path.ToLower.Contains(n.ToLower) Then
                            '        contain = True
                            '    End If
                            'Next
                            'For Each n In lc.Report_LineItems.Line_itemName.Split(" ")

                            'Next

            %><option value="<%=g.table_name %>,<%=s.column_name %>"><%=g.name %> - <%=s.path %></option>
            <%


                        Next

                    Next

                End If

            %>
        </select>
    </div>
</div>
<%
%>

<%
        Catch ex As Exception

        End Try
    End If%>

<% If action = "smart_schema" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Try
            Dim lc = db.LineItem_Columns.Single(Function(f) f.LineItemColumnID = id)

            Dim lname As String = lc.Report_LineItems.Line_itemName
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select id="Smart_Schema_<%=Request.QueryString("id") %>" class="form-control" onchange="<%=Request.QueryString("prefix") %>smart_update_select_value('<%=Request.QueryString("id") %>', this);">
            <option>--PLEASE SELECT--</option>

            <%

                If lc.Report_LineItems.Report.Is_MulitpleEntities Then

                Else
                    For Each s In db.Report_LineItems.Where(Function(f) f.Report.Entity_GroupID = lc.Report_LineItems.Report.Entity_GroupID)



                        If s.Line_itemName.ToLower.Contains(lname.ToLower) Then
                            For Each g In s.LineItem_Columns.Where(Function(f) (f.StoredProc <> "" Or Not f.StoredProc Is Nothing) Or Not f.Expression Is Nothing Or f.Expression <> "")
                                Dim schemas = db.GetSchemaDatabaseColumnByFieldName(g.SchemaFieldName)
                                Dim more As String = "Many"
                                Try
                                    If schemas.Count = 1 Then
                                        more = schemas(0).path
                                    End If
                                Catch ex As Exception

                                End Try

            %><option value="<%=g.TableName %>,<%=g.ColumnName %>,<%=g.SchemaFieldName %>,<%=g.Expression %>"><%=s.Report.Report_Name %> - <%=s.Line_itemName %> - <%=g.SchemaFieldName %> (<%=more %>)</option>
            <%
                            Next
                        End If
                    Next
                End If

            %>
        </select>
    </div>
</div>
<%

        Catch ex As Exception

        End Try



    End If%>


<% If action = "inherit_report_row_list" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Try
            Dim lc = db.Report_LineItems.Single(Function(f) f.Report_LineItemID = id)

%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select id="Inherit_Report_Row_List_<%=Request.QueryString("id") %>" class="form-control" onchange="<%=Request.QueryString("prefix") %>inheritReportRow('<%=Request.QueryString("id") %>',this);">
            <option>--PLEASE SELECT--</option>

            <%
                For Each r In db.Reports.Where(Function(f) f.Is_Dynamic And f.LineItemTypeID = 2 And f.Is_Deleted = False And f.Entity_GroupID = lc.Report.Entity_GroupID)



            %><option value="<%=r.ReportID.ToString %>"><%=r.Report_Name %>  </option>
            <%


                Next
            %>
        </select>
    </div>
</div>
<%

        Catch ex As Exception

        End Try



    End If%>


<% If action = "inherit_report_column_list" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Try
            Dim lc = db.Report_Columns.Single(Function(f) f.Report_ColumnID = id)

%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select id="Inherit_Report_Column_List_<%=Request.QueryString("id") %>" class="form-control" onchange="<%=Request.QueryString("prefix") %>inheritReportColumn('<%=Request.QueryString("id") %>',this);">
            <option>--PLEASE SELECT--</option>

            <%
                For Each r In db.Reports.Where(Function(f) f.Is_Dynamic And f.LineItemTypeID = 2 And f.Is_Deleted = False And f.Entity_GroupID = lc.Report.Entity_GroupID)



            %><option value="<%=r.ReportID.ToString %>"><%=r.Report_Name %>  </option>
            <%


                Next
            %>
        </select>
    </div>
</div>
<%

        Catch ex As Exception

        End Try



    End If%>

<% If action = "inherit_report" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Try
            Dim lc = db.LineItem_Columns.Single(Function(f) f.LineItemColumnID = id)

            Dim lname As String = lc.Report_LineItems.Line_itemName
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select id="Inherit_<%=Request.QueryString("id") %>" class="form-control" onchange="<%=Request.QueryString("prefix") %>inherit_item('<%=Request.QueryString("id") %>',this);">
            <option>--PLEASE SELECT--</option>

            <%
                For Each r In db.Reports.Where(Function(f) f.Is_Dynamic And f.LineItemTypeID = 2 And f.Is_Deleted = False)



            %><option value="<%=r.ReportID.ToString %>"><%=r.Report_Name %>  </option>
            <%


                Next
            %>
        </select>
    </div>
</div>
<%

        Catch ex As Exception

        End Try



    End If%>

<% If action = "inherit_report_column" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Try
            Dim rc = db.Report_Columns.Single(Function(f) f.Report_ColumnID = id)

            Dim lname As String = rc.ColumnName
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select name="Inherited_Report_ColumnID_<%=Request.QueryString("id") %>" id="Inherited_Report_ColumnID_<%=Request.QueryString("id") %>" class="form-control" onchange="<%=Request.QueryString("prefix") %>inherit_item('<%=Request.QueryString("id") %>',this);">
            <option>--PLEASE SELECT--</option>

            <%
                For Each r In rc.Report.Report_Columns



            %><option value="<%=r.Report_ColumnID.ToString %>"><%=r.ColumnName %>  </option>
            <%


                Next
            %>
        </select>
    </div>
    <div class="form-group">
        <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Inherited Value</label>
        <div class="checkbox col-md-9 col-sm-9 col-xs-12">
            <select class="form-control" name="Inherited_Type_<%=Request.QueryString("id") %>">
                <option value="0">Mapping</option>
                <option value="1">Schema</option>
            </select>
        </div>

    </div>
</div>
<%

        Catch ex As Exception

        End Try



    End If%>


<% If action = "inherit_report_row" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Try
            Dim rc = db.Report_LineItems.Single(Function(f) f.Report_LineItemID = id)

            Dim lname As String = rc.ColumnName
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select name="Inherited_Report_LineItemID_<%=Request.QueryString("id") %>" id="Inherited_Report_LineItemID_<%=Request.QueryString("id") %>" class="form-control" onchange="<%=Request.QueryString("prefix") %>inherit_item('<%=Request.QueryString("id") %>',this);">
            <option>--PLEASE SELECT--</option>

            <%
                For Each r In rc.Report.Report_LineItems.Where(Function(f) f.Report_LineItemID <> id)



            %><option value="<%=r.Report_LineItemID.ToString %>"><%=r.Line_itemName %>  </option>
            <%


                Next
            %>
        </select>
    </div>
</div>
<%

        Catch ex As Exception

        End Try



    End If%>
<% If action = "inherit_item" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Try
            Dim lc = db.Reports.Single(Function(f) f.ReportID = id)

            Dim lname As String = lc.Report_Name
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select name="Inherited_Lineitem_ColumnID_<%=Request.QueryString("parameter") %>" id="Inherited_Lineitem_ColumnID_<%=Request.QueryString("parameter") %>" class="form-control">
            <option>--PLEASE SELECT--</option>

            <%
                For Each r In lc.Report_Columns.OrderBy(Function(f) f.Sort_Order)

                    For Each rr In r.LineItem_Columns.Where(Function(f) f.StoredProc <> "" Or f.StoredProc IsNot Nothing Or f.Is_CalculatedValue Or (f.Is_Inherited And Not f.Inherited_Lineitem_ColumnID Is Nothing)).OrderBy(Function(f) f.Sort_Order)
            %><option value="<%=rr.LineItemColumnID.ToString() %>"><%=rr.Report_LineItems.Line_itemName %> - <%=r.ColumnName %>  </option>
            <%
                    Next




                Next
            %>
        </select>
    </div>
    <div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Inherited Value</label>
    <div class="checkbox col-md-9 col-sm-9 col-xs-12">
        <select class="form-control" name="Inherited_Type_<%=Request.QueryString("id") %>">
            <option value="0">Mapping</option>
            <option value="1">Schema</option>
        </select>
    </div>

</div>
</div>
<%

        Catch ex As Exception

        End Try



    End If%>

<% If action = "inherit_report_row_item" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Dim param As Integer = CInt(Request.QueryString("param"))
        Try
            Dim lc = db.Reports.Single(Function(f) f.ReportID = id)

            Dim lname As String = lc.Report_Name
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select name="Inherited_Report_LineItemID_<%=param %>" id="Inherited_Report_LineItemID_<%=param %>" class="form-control">
            <option>--PLEASE SELECT--</option>

            <%
                For Each r In lc.Report_LineItems.OrderBy(Function(f) f.Sort_Order)

            %><option value="<%=r.Report_LineItemID.ToString() %>"><%=r.Line_itemName %>  </option>
            <%

                Next
            %>
        </select>
    </div>
</div>
<%

        Catch ex As Exception

        End Try



    End If%>

<% If action = "inherit_report_column_item" Then
        Dim id As Integer = CInt(Request.QueryString("id"))
        Dim param As Integer = CInt(Request.QueryString("param"))
        Try
            Dim lc = db.Reports.Single(Function(f) f.ReportID = id)

            Dim lname As String = lc.Report_Name
%>
<div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12"></label>
    <div class="col-md-9 col-sm-9 col-xs-12">
        <select name="Inherited_Report_ColumnID_<%=param %>" id="Inherited_Report_ColumnID_<%=param %>" class="form-control">
            <option>--PLEASE SELECT--</option>

            <%
                For Each r In lc.Report_Columns.Where(Function(f) f.Report_ColumnID <> param).OrderBy(Function(f) f.Sort_Order)

            %><option value="<%=r.Report_ColumnID.ToString() %>"><%=r.ColumnName %>  </option>
            <%

                Next
            %>
        </select>
    </div>
</div>    <div class="form-group">
    <label class="control-label col-md-3 col-sm-3 col-xs-12">Inherite Type</label>
    <div class="checkbox col-md-9 col-sm-9 col-xs-12">
        <select class="form-control" name="Inherited_Type_<%=param %>">
            <option value="0">Mapping</option>
            <option value="1">Schema</option>
        </select>
    </div>

</div>
<%

        Catch ex As Exception

        End Try



    End If%>