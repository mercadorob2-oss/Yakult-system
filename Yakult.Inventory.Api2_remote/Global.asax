<%@ Application Language="C#" %>
<script runat="server">
void Application_BeginRequest(object sender, EventArgs e) {
    var path = Request.Url.AbsolutePath;
    if (path.EndsWith("/SerialLookup", StringComparison.OrdinalIgnoreCase)) {
        var qs = Request.QueryString.Count > 0 ? "?" + Request.QueryString.ToString() : "";
        Server.TransferRequest("~/slookup.ashx" + qs, true);
        return;
    }
    var apiIndex = path.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
    if (apiIndex < 0 && path.EndsWith("/api", StringComparison.OrdinalIgnoreCase)) {
        apiIndex = path.Length - 4;
    }
    if (apiIndex >= 0) {
        var route = path.Substring(apiIndex + 1);
        var qs = Request.QueryString.Count > 0 ? "&" + Request.QueryString.ToString() : "";
        Server.TransferRequest("~/api.ashx?__route=" + Uri.EscapeDataString(route) + qs, true);
    }
}
</script>
