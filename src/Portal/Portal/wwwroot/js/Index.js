﻿$(document).ready(function () {

    $(".taskNoteItem").on("click",
        function () {
            var processId = $(this).data("processid");
            $("#hdnProcessId").val(processId);
            var taskNote = $(this).data("tasknote");
            $("#txtNote").val(taskNote);
            $("#editTaskNoteModal").modal("show");
        });

    $("#btnClearTaskNote").click(function() {
        $("#txtNote").val("");
    });

    $("#btnPostTaskNote").click(function () {

        $.ajax({
            type: "POST",
            url: "Index/?handler=TaskNote",
            beforeSend: function (xhr) {
                xhr.setRequestHeader("RequestVerificationToken", $('input:hidden[name="__RequestVerificationToken"]').val());
            },
            data: {
                "taskNote": $("#txtNote").val(),
                "processId": $("#hdnProcessId").val()
            },
            success: function (result) {
                $("#editTaskNoteModal").modal("hide");
                $(".modal-backdrop").remove();
                $("body").removeClass("modal-open");
            },
            error: function (error) {
                console.log(error);
            }
        });
    });

});