﻿$(document).ready(function () {
    var processId = { "processId": $("#hdnProcessId").val() };

    function getAssignTasks() {
        $.ajax({
            type: "GET",
            url: "Review/?handler=RetrieveAssignTasks",
            beforeSend: function (xhr) {
                xhr.setRequestHeader("XSRF-TOKEN", $('input:hidden[name="__RequestVerificationToken"]').val());
            },
            contentType: "application/json; charset=utf-8",
            data: processId,
            success: function (result) {
                $("#assignTasks").append(result);
            },
            error: function (error) {
                $("#assignTasksError")
                    .html("<div class=\"alert alert-danger\" role=\"alert\">Failed to create new assign task section.</div>");
            }
        });
    }

    $.ajax({
        type: "GET",
        url: "Review/?handler=RetrieveComments",
        beforeSend: function (xhr) {
            xhr.setRequestHeader("XSRF-TOKEN", $('input:hidden[name="__RequestVerificationToken"]').val());
        },
        contentType: "application/json; charset=utf-8",
        data: processId,
        success: function (result) {
            $("#existingComments").html(result);
        },
        error: function (error) {
            $("#AddCommentError")
                .html("<div class=\"alert alert-danger\" role=\"alert\">Failed to load comments.</div>");
        }
    });

    getAssignTasks();

    // Create child div under assignTasks and insert partial into it
    $("#btnCreateTask").click(function () {
        getAssignTasks();
    });
});