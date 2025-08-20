

//namespace Finolyzer.Jobs;
//public static class HangfireJobs
//{
//    public static void ExecuteHangfireJobs(
//        string uploadTempAttachmentExecutionTime,
//        string notificationExecutionTime,
//        string cleanTempAttachmentExecutionTime,
//        string updateLicensesStatusExecutionTime,
//        string unpaidLicenseRequestsExecutionTime,
//        string markLicensesForRenewelExecutionTime,
//        string sendRenewNotificaitonsExecutionTime,
//        string sendDraftotificaitonsExecutionTime,
//        string cancelNotCorrectedRequestsExecutionTime,
//        string updatePermitStatusExecutionTime,
//        string renewLicenseExecutionTime,
//        string cancelUnPaidRenewExecutionTime,
//        string updateCinemaTheaterNotStartedLicensesExecutionTime,
//        string sendAskAdeaInquiryToIvntExecutionTime,
//        string updateSevenHundredServiceNumberExecutionTime,
//        string sendLicenseUpdateAvailableNotificationExecutionTime,
//        string sendMaxProcessingTimeReminderNotificationExecutionTime,
//        string notifyMocEmployeeForRenewExecutionTime,
//        string SendInternationalUserRequestStatusToKeycloakExecutionTime,

//        bool isActiveUploadTempAttachmentExecutionTime,
//        bool isActiveNotificationExecutionTime,
//        bool isActiveCleanTempAttachmentExecutionTime,
//        bool isActiveUpdateLicensesStatusExecutionTime,
//        bool isActiveUnpaidLicenseRequestsExecutionTime,
//        bool isActiveMarkLicensesForRenewelExecutionTime,
//        bool isActiveSendRenewNotificaitonsExecutionTime,
//        bool isActiveSendDraftNotificaitonsExecutionTime,
//        bool isActiveCancelNotCorrectedRequestsExecutionTime,
//        bool isActiveUpdatePermitStatusExecutionTime,
//        bool isActiveRenewLicense,
//        bool isActiveCancelUnPaidRenew,
//        bool isActiveupdateCinemaTheaterLicenses,
//        bool isActiveAskAdeaInquiryToIvnt,
//        bool isActiveUpdateSevenHundredServiceNumber,
//        bool isActiveSendLicenseUpdateAvailableNotification,
//        bool isActiveSendWaitingProcessingTimeReminderNotificationExecutionTime,
//        bool isActiveNotifyMocEmployeeForRenew,
//        bool isActiveSendInternationalUserRequestStatusToKeycloakExecutionTime
//        )
//    {
//        RecurringJob.RemoveIfExists(nameof(IS3UploaderJob.UploadTempAttachmentToS3));
//        if (isActiveUploadTempAttachmentExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<IS3UploaderJob>(recurringJobId: nameof(IS3UploaderJob.UploadTempAttachmentToS3), methodCall: a => a.UploadTempAttachmentToS3(), uploadTempAttachmentExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(INotificationJob.SendNotification));
//        if (isActiveNotificationExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<INotificationJob>(recurringJobId: nameof(INotificationJob.SendNotification), methodCall: a => a.SendNotification(), notificationExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(ICleanTempAttachmentJob.CleanTempAttachment));
//        if (isActiveCleanTempAttachmentExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<ICleanTempAttachmentJob>(recurringJobId: nameof(ICleanTempAttachmentJob.CleanTempAttachment), methodCall: a => a.CleanTempAttachment(), cleanTempAttachmentExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(IExpirationLicensesJob.UpdateLicensesStatus));
//        if (isActiveUpdateLicensesStatusExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<IExpirationLicensesJob>(recurringJobId: nameof(IExpirationLicensesJob.UpdateLicensesStatus), methodCall: a => a.UpdateLicensesStatus(), updateLicensesStatusExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(IHandleUnPaidRequests.ReminderOrUpdateUnpaidLicenseRequests));
//        if (isActiveUnpaidLicenseRequestsExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<IHandleUnPaidRequests>(recurringJobId: nameof(IHandleUnPaidRequests.ReminderOrUpdateUnpaidLicenseRequests), methodCall: a => a.ReminderOrUpdateUnpaidLicenseRequests(), unpaidLicenseRequestsExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(IMarkLicensesForRenewelJob.MarkLicensesForRenewel));
//        if (isActiveMarkLicensesForRenewelExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<IMarkLicensesForRenewelJob>(recurringJobId: nameof(IMarkLicensesForRenewelJob.MarkLicensesForRenewel), methodCall: a => a.MarkLicensesForRenewel(), markLicensesForRenewelExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(ISendRenewNotificationJob.SendRenewNotificaitons));
//        if (isActiveSendRenewNotificaitonsExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<ISendRenewNotificationJob>(recurringJobId: nameof(ISendRenewNotificationJob.SendRenewNotificaitons), methodCall: a => a.SendRenewNotificaitons(), sendRenewNotificaitonsExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(IDraftRequestReminderJob.SendDraftNotificaitons));
//        if (isActiveSendDraftNotificaitonsExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<IDraftRequestReminderJob>(recurringJobId: nameof(IDraftRequestReminderJob.SendDraftNotificaitons), methodCall: a => a.SendDraftNotificaitons(), sendDraftotificaitonsExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(ICancelNotCorrectedRequestJob.CancelLicensesFoNotCorrectionAsync));
//        if (isActiveCancelNotCorrectedRequestsExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<ICancelNotCorrectedRequestJob>(recurringJobId: nameof(ICancelNotCorrectedRequestJob.CancelLicensesFoNotCorrectionAsync), methodCall: a => a.CancelLicensesFoNotCorrectionAsync(), cancelNotCorrectedRequestsExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(IUpdatePermitStatusLicensesJob.UpdatePermitStatus));
//        if (isActiveUpdatePermitStatusExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<IUpdatePermitStatusLicensesJob>(recurringJobId: nameof(IUpdatePermitStatusLicensesJob.UpdatePermitStatus), methodCall: a => a.UpdatePermitStatus(), updatePermitStatusExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(IRenewLicenseJob.RenewLicenses));

//        if (isActiveRenewLicense)
//        {
//            RecurringJob.AddOrUpdate<IRenewLicenseJob>(recurringJobId: nameof(IRenewLicenseJob.RenewLicenses), methodCall: a => a.RenewLicenses(), renewLicenseExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(ICancelUnPaidRenewRequestsJob.CancelRenewRequests));
//        if (isActiveCancelUnPaidRenew)
//        {
//            RecurringJob.AddOrUpdate<ICancelUnPaidRenewRequestsJob>(recurringJobId: nameof(ICancelUnPaidRenewRequestsJob.CancelRenewRequests), methodCall: a => a.CancelRenewRequests(), cancelUnPaidRenewExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(UpdateCinemaTheaterLicensesStatusJob.UpdateNotStartedLicenses));
//        if (isActiveupdateCinemaTheaterLicenses)
//        {
//            RecurringJob.AddOrUpdate<UpdateCinemaTheaterLicensesStatusJob>(recurringJobId: nameof(UpdateCinemaTheaterLicensesStatusJob.UpdateNotStartedLicenses), methodCall: a => a.UpdateNotStartedLicenses(), updateCinemaTheaterNotStartedLicensesExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(SendAskAdeaInquiryToIvntJob.SendAskAdeaInquiryToIvnt));
//        if (isActiveAskAdeaInquiryToIvnt)
//        {
//            RecurringJob.AddOrUpdate<SendAskAdeaInquiryToIvntJob>(recurringJobId: nameof(SendAskAdeaInquiryToIvntJob.SendAskAdeaInquiryToIvnt), methodCall: a => a.SendAskAdeaInquiryToIvnt(), sendAskAdeaInquiryToIvntExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(UpdateSevenHundredServiceNumberJob.UpdateSevenHundredServiceNumber));
//        if (isActiveUpdateSevenHundredServiceNumber)
//        {
//            RecurringJob.AddOrUpdate<UpdateSevenHundredServiceNumberJob>(recurringJobId: nameof(UpdateSevenHundredServiceNumberJob.UpdateSevenHundredServiceNumber), methodCall: a => a.UpdateSevenHundredServiceNumber(), updateSevenHundredServiceNumberExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(ISendLicenseUpdateAvailableNotificationJob.SendLicenseUpdateAvailableNotification));
//        if (isActiveSendLicenseUpdateAvailableNotification)
//        {
//            RecurringJob.AddOrUpdate<ISendLicenseUpdateAvailableNotificationJob>(recurringJobId: nameof(ISendLicenseUpdateAvailableNotificationJob.SendLicenseUpdateAvailableNotification), methodCall: a => a.SendLicenseUpdateAvailableNotification(), sendLicenseUpdateAvailableNotificationExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(ISendWaitingProcessingTimeReminderNotificationJob.SendWaitingProcessingTimeReminderNotification));
//        if (isActiveSendWaitingProcessingTimeReminderNotificationExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<ISendWaitingProcessingTimeReminderNotificationJob>(recurringJobId: nameof(ISendWaitingProcessingTimeReminderNotificationJob.SendWaitingProcessingTimeReminderNotification), methodCall: a => a.SendWaitingProcessingTimeReminderNotification(), sendMaxProcessingTimeReminderNotificationExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(INotifyMocEmployeeForRenewJob.NotifyMocEmployeeForRenew));
//        if (isActiveNotifyMocEmployeeForRenew)
//        {
//            RecurringJob.AddOrUpdate<INotifyMocEmployeeForRenewJob>(recurringJobId: nameof(INotifyMocEmployeeForRenewJob.NotifyMocEmployeeForRenew), methodCall: a => a.NotifyMocEmployeeForRenew(), notifyMocEmployeeForRenewExecutionTime);
//        }

//        RecurringJob.RemoveIfExists(nameof(ISendInternationalUserRequestStatusToKeycloakJob.SendInternationalUserRequestStatusToKeycloak));
//        if (isActiveSendInternationalUserRequestStatusToKeycloakExecutionTime)
//        {
//            RecurringJob.AddOrUpdate<ISendInternationalUserRequestStatusToKeycloakJob>(recurringJobId: nameof(ISendInternationalUserRequestStatusToKeycloakJob.SendInternationalUserRequestStatusToKeycloak), methodCall: a => a.SendInternationalUserRequestStatusToKeycloak(), SendInternationalUserRequestStatusToKeycloakExecutionTime);
//        }
//    }
//}