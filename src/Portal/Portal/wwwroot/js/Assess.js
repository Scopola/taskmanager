﻿$(document).ready(function() {
    setAssessDoneHandler();
});

function setAssessDoneHandler() {
    $("#btnDone").prop("disabled", false);

    $("#assessDone").on("submit", function (e) {
        $("#btnDone").prop("disabled", true);
        $("#modalWaitAssessDone").modal("show");

        //e.preventDefault();

    });
}