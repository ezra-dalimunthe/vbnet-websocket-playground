var ws = null;
$(function () {
    $("#btnConnect").click(function (e) {
        e.preventDefault();
        if (ws == null) {
            $(this).text("Connecting...")
        
            ws = new WebSocket($("#wsUri").val())
            ws.onopen = ws_onopen;
            ws.onclose = ws_onclose;
            ws.onerror = ws_onerror;
            ws.onmessage = ws_onmessage;
         
        } else {
            ws.close()
            ws = null;
            $(this).text("Connect")
        }

    })
    $("#btnSend").click(function (e) {
        e.preventDefault();
        var msg = $("#messageToSend").val();
        sendmsg(msg);
    })
    $("#btnClearLog").click(function (e) {
        $("#consoleLog").empty();
    })
})

ws_onopen = function (e) {
    var msg = $("<div>", {
        class: "alert alert-info",
        text: "CONNECTED"
    });
    $("#btnSend").removeAttr("disabled");
    $("#btnConnect").text("Disconnect")
    addlog(msg)
}
ws_onclose = function (e) {
    var msg = $("<div>", {
        class: "alert alert-secondary",
        text: "DISCONNECTED"
    });
    $("#btnSend").attr("disabled", true);
    $("#btnConnect").text("Connect")
    addlog(msg)
}
ws_onerror = function (e) {
    console.log(e)
    var msg = $("<div>", {
        class: "alert alert-danger",
        text: "ERROR"
    });
    ws.close();
    addlog(msg)
}
ws_onmessage = function (e) {
    var msg = $("<div>", {
        class: "alert alert-success",
        text: "Receive: " + e.data
    })
    addlog(msg)
}
sendmsg = function (message) {
    ws.send(message);
    var msg = $("<div>", {
        class: "text-success",
        text: "Send:" + message
    })
    addlog(msg)
}
addlog = function (msg) {

    $("#consoleLog").append(msg)
    $('#consoleLog').animate({ scrollTop: $("#consoleLog")[0].scrollHeight }, 200);
}