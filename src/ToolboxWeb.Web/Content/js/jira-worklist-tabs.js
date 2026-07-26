(function (window, document) {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        const boardTabButton = document.getElementById("jira-tab-board");
        const autoTabButton = document.getElementById("jira-tab-auto");

        if (boardTabButton) {
            boardTabButton.addEventListener("shown.bs.tab", function () {
                window.ToolboxJiraTaskBoard?.init();
            });
            boardTabButton.addEventListener("hide.bs.tab", function () {
                window.ToolboxJiraTaskBoard?.destroy();
            });
        }

        if (autoTabButton) {
            autoTabButton.addEventListener("shown.bs.tab", function () {
                window.ToolboxJiraAutoSchedule?.init();
            });
            autoTabButton.addEventListener("hide.bs.tab", function () {
                window.ToolboxJiraAutoSchedule?.destroy();
            });
        }
    });
})(window, document);
