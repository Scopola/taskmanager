﻿$(document).ready(function () {
    getEditDatabase();
});

function getEditDatabase() {

    var processId = { "processId": Number($("#hdnProcessId").val()) };

    $.ajax({
        type: "GET",
        url: "_EditDatabase",
        beforeSend: function (xhr) {
            xhr.setRequestHeader("XSRF-TOKEN", $('input:hidden[name="__RequestVerificationToken"]').val());
        },
        contentType: "application/json; charset=utf-8",
        data: processId,
        success: function (result) {
            $("#editDatabase").html(result);
            launchSourceEditorDownloadHandler();
            initialiseWorkspaceTypeahead();
        },
        error: function (error) {

            var errorMessage = error.getResponseHeader("Error");

            $("#editDatabaseError")
                .html("<div class=\"alert alert-danger\" role=\"alert\">Failed to load Edit Database. " + errorMessage + "</div>");
        }
    });
}

function launchSourceEditorDownloadHandler() {
    $("#btnLaunchSourceEditorDownload").on("click", function () {
        $("#btnLaunchSourceEditorDownload").prop("disabled", true);

        var processId = Number($("#hdnProcessId").val());
        var pageIdentity = $("#pageIdentity").val();
        var sessionFilename = $(this).data("sessionfilename");

        $.ajax({
            type: "GET",
            xhrFields: {
                responseType: 'blob'
            },
            url: "_EditDatabase/?handler=LaunchSourceEditor",
            beforeSend: function (xhr) {
                xhr.setRequestHeader("XSRF-TOKEN", $('input:hidden[name="__RequestVerificationToken"]').val());
            },
            contentType: "application/json; charset=utf-8",
            data: {
                "processId": processId,
                "taskStage": pageIdentity,
                "sessionFilename": sessionFilename
            },
            success: function (data) {

                var url = window.URL.createObjectURL(data);
                $("#hdnDownloadLink").attr("href", url);
                $("#hdnDownloadLink").attr("download", sessionFilename);
                $("#hdnDownloadLink")[0].click();
            },
            error: function (error) {
                var errorMessage = error.getResponseHeader("Error");

                $("#launchSourceEditorDownloadError")
                    .html("<div class=\"alert alert-danger\" role=\"alert\">Failed to generate Session File. " + errorMessage + "</div>");
            },
            complete: function() {

                $("#btnLaunchSourceEditorDownload").prop("disabled", false);
                $("#hdnDownloadLink").removeAttr("href");
                $("#hdnDownloadLink").removeAttr("download");
            }
        });

    });

function initialiseWorkspaceTypeahead() {
    $('#workspaceTypeaheadError').collapse("hide");
    // Constructing the suggestion engine
    var workspace = new Bloodhound({
        datumTokenizer: Bloodhound.tokenizers.whitespace,
        queryTokenizer: Bloodhound.tokenizers.whitespace,
        prefetch: {
            url: "_EditDatabase/?handler=Workspaces",
            ttl: 600000
        },
        initialize: false
    });

    var promise = workspace.initialize();
    promise.fail(function () {
        $('#workspaceTypeaheadError').collapse("show");
    });

    // Initializing the typeahead
    $('.typeahead').typeahead({
        hint: true,
        highlight: true, /* Enable substring highlighting */

        minLength:
            3 /* Specify minimum characters required for showing result */
    },
        {
            name: 'workspace',
            source: workspace,
            limit: 100,
            templates: {
                notFound: '<div>No results</div>'
            }
        });
}