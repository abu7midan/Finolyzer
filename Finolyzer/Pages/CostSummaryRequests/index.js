$(function () {
    var l = abp.localization.getResource('Finolyzer');

    var createModal = new abp.ModalManager(abp.appPath + 'CostSummaryRequests/CreateModal');
    //var createModal = new abp.ModalManager({
    //    viewUrl: abp.appPath + 'CostSummaryRequests/CreateModal',
    //    scriptUrl: '/Pages/CostSummaryRequests/create.js', //Lazy Load URL
    //});
    //var editModal = new abp.ModalManager(abp.appPath + 'CostSummaryRequests/EditModal');

    var dataTable = $('#CostSummaryRequestsTable').DataTable(
        abp.libs.datatables.normalizeConfiguration({
            serverSide: true,
            paging: true,
            order: [[0, 'asc']],
            searching: false,
            scrollX: true,
            ajax: abp.libs.datatables.createAjax(
                finolyzer.services.costSummaryRequests.costSummaryRequest.getList
            ),
            columnDefs: [
                {
                    title: l('Actions'),
                    rowAction: {
                        items: [
                            {
                                text: l('Edit'),
                                visible: true,
                                action: function (data) {
                                    editModal.open({ id: data.record.id });
                                }
                            },
                            {
                                text: l('Delete'),
                                visible: true,
                                confirmMessage: function (data) {
                                    return l('CostSummaryRequestDeletionConfirmationMessage', data.record.description);
                                },
                                action: function (data) {
                                    finolyzer.services.costSummaryRequests.costSummaryRequest
                                        .delete(data.record.id)
                                        .then(function () {
                                            abp.notify.info(l('SuccessfullyDeleted'));
                                            dataTable.ajax.reload();
                                        });
                                }
                            }
                        ]
                    }
                },
                {
                    title: l('Description'),
                    data: 'description'
                },
                {
                    title: l('Notes'),
                    data: 'notes'
                },
                {
                    title: l('CalculationFor'),
                    data: 'calculationFor',
                    render: function (data) {
                        return l('Enum:CalculationForType.' + data);
                    }
                },
                {
                    title: l('TimlyRequestType'),
                    data: 'timlyRequestType',
                    render: function (data) {
                        return l('Enum:TimlyRequestType.' + data);
                    }
                },
                {
                    title: l('CalculationBeforeDate'),
                    data: 'calculationBeforeDate',
                    render: function (data) {
                        return luxon.DateTime.fromISO(data).toLocaleString(); // optional: format nicely
                    }
                },
                {
                    title: l('IncludeSharedService'),
                    data: 'includeSharedService',
                    render: function (data) {
                        return data ? l('Yes') : l('No');
                    }
                },
                {
                    title: l('CreationTime'),
                    data: 'creationTime',
                    render: function (data) {
                        return luxon.DateTime.fromISO(data).toLocaleString(); // or use ABP date format
                    }
                }
            ]
        })
    );

    // Reload table on Create or Edit
    createModal.onResult(function (event, result) {
        if (result?.responseText?.redirectUrl) {
            window.location.href = result.responseText.redirectUrl;
        } else {
            console.warn("No redirectUrl returned:", result);
        }
    });
    //editModal.onResult(function () {
    //    dataTable.ajax.reload();
    //});

    // Handle new entry button
    $('#NewCostSummaryRequestButton').click(function (e) {
        e.preventDefault();
        createModal.open();
    });


});