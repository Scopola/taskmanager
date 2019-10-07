﻿$(document).ready(function () {

    $("#btnPostComment").click(function () {

        if ($('#txtComment').val() === "") {
            $("#AddCommentError")
                .html("<div class=\"alert alert-danger\" role=\"alert\">Please enter a comment.</div>");
            $('#txtComment').focus();
        } else {

            var jsonData = {
                "comment": $("#txtComment").val(),
                "processId": $("#hdnProcessId").val()
            };

            $.ajax({
                type: "GET",
                url: "Review/?handler=CommentsPartial",
                beforeSend: function(xhr) {
                    xhr.setRequestHeader("XSRF-TOKEN", $('input:hidden[name="__RequestVerificationToken"]').val());
                },
                contentType: 'application/json; charset=utf-8"',
                data: jsonData,
                success: function(result) {
                    $("#existingComments").html(result);
                    $("#addCommentModal").modal("hide");
                    $(".modal-backdrop").remove();
                    $("body").removeClass("modal-open");
                },
                error: function(error) {
                    console.log(error);
                }
            });
        }
    });
});