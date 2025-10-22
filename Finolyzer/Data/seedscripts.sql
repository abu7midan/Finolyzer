INSERT INTO [Finolyzerdb].[dbo].[Portfolios] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'AO - Products Safety & Logistics', GETDATE(), 0, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[Portfolios] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'AO - Quality of Life', GETDATE(), 0, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[Portfolios] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'AO - Enterprise Solutions', GETDATE(), 0, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[Portfolios] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'AO - Justice, Real Estate and Urban', GETDATE(), 0, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[Portfolios] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'AO-INTG', GETDATE(), 0, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[Portfolios] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'AO - Mobility And Industrial', GETDATE(), 0, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[Portfolios] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'AO – Digital Ventures', GETDATE(), 0, N'', NEWID());


  INSERT INTO [Finolyzerdb].[dbo].[ApplicationSystems] ([Name], [PortfolioId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Mwathiq', (select top 1 id from [Portfolios] where [Name]=N'AO - Justice, Real Estate and Urban'),GETDATE(), 0, N'', NEWID());
  INSERT INTO [Finolyzerdb].[dbo].[ApplicationSystems] ([Name], [PortfolioId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Maroof', (select top 1 id from [Portfolios] where [Name]=N'AO - Enterprise Solutions'),GETDATE(), 0, N'', NEWID());
  INSERT INTO [Finolyzerdb].[dbo].[ApplicationSystems] ([Name], [PortfolioId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Vsafty Isoft', (select top 1 id from [Portfolios] where [Name]=N'AO - Products Safety & Logistics'),GETDATE(), 0, N'', NEWID());
  INSERT INTO [Finolyzerdb].[dbo].[ApplicationSystems] ([Name], [PortfolioId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Mazad', (select top 1 id from [Portfolios] where [Name]=N'AO - Justice, Real Estate and Urban'),GETDATE(), 0, N'', NEWID());
  INSERT INTO [Finolyzerdb].[dbo].[ApplicationSystems] ([Name], [PortfolioId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'COO', (select top 1 id from [Portfolios] where [Name]=N'AO - Mobility And Industrial'),GETDATE(), 0, N'', NEWID());
  INSERT INTO [Finolyzerdb].[dbo].[ApplicationSystems] ([Name], [PortfolioId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Wathq', (select top 1 id from [Portfolios] where [Name]=N'AO – Digital Ventures'),GETDATE(), 0, N'', NEWID());
  INSERT INTO [Finolyzerdb].[dbo].[ApplicationSystems] ([Name], [PortfolioId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'MC - CR Renew', (select top 1 id from [Portfolios] where [Name]=N'AO - Enterprise Solutions'),GETDATE(), 0, N'', NEWID());

INSERT INTO [Finolyzerdb].[dbo].[Providers] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Thiqah Integration Operation - Apigee', GETDATE(), 1, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[Providers] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Google', GETDATE(), 1, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[Providers] ([Name], [CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'STC', GETDATE(), 1, N'', NEWID());

INSERT INTO [Finolyzerdb].[dbo].[IntegrationServices] ([SystemMappingKey],[Description],[URL],[IntegrationSubscriptionType],UnitCost,[ProviderId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'P-ELM_fingerprint',N'Elm finger print', N'https://internal-api.thiqah.sa/elm/fingerprint', 1,3,(select top 1 id from [Providers] where [Name]=N'Thiqah Integration Operation - Apigee'),GETDATE(), 1, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[IntegrationServices] ([SystemMappingKey],[Description],[URL],[IntegrationSubscriptionType],UnitCost,[ProviderId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'P-ELM_MVPI',N'VSafty Servicess provided by ELM', N'https://internal-api.thiqah.sa/elm/mvpi', 1,3,(select top 1 id from [Providers] where [Name]=N'Thiqah Integration Operation - Apigee'),GETDATE(), 1, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[IntegrationServices] ([SystemMappingKey],[Description],[URL],[IntegrationSubscriptionType],UnitCost,[ProviderId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'P-ELM_absher-notification',N'SMS notification Services provided by ELM', N'https://internal-api.thiqah.sa/elm/absher-notification', 1,3,(select top 1 id from [Providers] where [Name]=N'Thiqah Integration Operation - Apigee'),GETDATE(), 1, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[IntegrationServices] ([SystemMappingKey],[Description],[URL],[IntegrationSubscriptionType],UnitCost,[ProviderId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'nationalNotification_v1',N'SMS notification Services provided by ELM', N'https://internal-api.thiqah.sa/v1/nationalnotification', 1,3,(select top 1 id from [Providers] where [Name]=N'Thiqah Integration Operation - Apigee'),GETDATE(), 1, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[IntegrationServices] ([SystemMappingKey],[Description],[URL],[IntegrationSubscriptionType],UnitCost,[ProviderId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'P-ELM_yakeen',N'Yakeen Services provided by ELM', N'https://internal-api.thiqah.sa/elm/yakeen', 1,3,(select top 1 id from [Providers] where [Name]=N'Thiqah Integration Operation - Apigee'),GETDATE(), 1, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[IntegrationServices] ([SystemMappingKey],[Description],[URL],[IntegrationSubscriptionType],UnitCost,[ProviderId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'P-ELM_yakeen-vehicle',N'Yakeen vehicle Services provided by ELM', N'https://internal-api.thiqah.sa/elm/yakeen/vehicle', 1,3,(select top 1 id from [Providers] where [Name]=N'Thiqah Integration Operation - Apigee'),GETDATE(), 1, N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[IntegrationServices] ([SystemMappingKey],[Description],[URL],[IntegrationSubscriptionType],UnitCost,[ProviderId],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'P-ELM_absher-nabaa-notification',N'absher nabaa notification Services provided by ELM', N'https://internal-api.thiqah.sa/elm/absher/nabaa/notification', 1,3,(select top 1 id from [Providers] where [Name]=N'Thiqah Integration Operation - Apigee'),GETDATE(), 1, N'', NEWID());



INSERT INTO [Finolyzerdb].[dbo].[ApplicationIntegrationKeys] ([Description],[UserName],[Password],[IntegrationServiceId],[ApplicationSystemId], [CreationTime], [ExtraProperties], [ConcurrencyStamp])  VALUES (N'Thiqah Apigee Wathq Yakeen App Keys',N'u9mmdEV6LHOTq7dUZUCO3Ga9AXm5TG6G',N'mnyUsh38H0BQpyty',(select top 1 id from [IntegrationServices] where SystemMappingKey=N'P-ELM_yakeen'),(select top 1 id from [ApplicationSystems] where [Name]=N'Wathq'), GETDATE(), N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[ApplicationIntegrationKeys] ([Description],[UserName],[Password],[IntegrationServiceId],[ApplicationSystemId], [CreationTime], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Thiqah Apigee Mwathiq Yakeen App Keys',N'cvGgonF0WFvKckcWFikUlfYQRqEfGqsb',N'2A2uorQluZJOFofC',(select top 1 id from [IntegrationServices] where SystemMappingKey=N'P-ELM_yakeen'),(select top 1 id from [ApplicationSystems] where [Name]=N'Mwathiq'), GETDATE(), N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[ApplicationIntegrationKeys] ([Description],[UserName],[Password],[IntegrationServiceId],[ApplicationSystemId], [CreationTime], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Thiqah Apigee Mazad Yakeen App Keys',N'X38ix4QwofiuU2HpC08NU58T4sVnSMza',N'a9NEpGh4Em5uxgFL',(select top 1 id from [IntegrationServices] where SystemMappingKey=N'P-ELM_yakeen'),(select top 1 id from [ApplicationSystems] where [Name]=N'Mazad'), GETDATE(), N'', NEWID());
INSERT INTO [Finolyzerdb].[dbo].[ApplicationIntegrationKeys] ([Description],[UserName],[Password],[IntegrationServiceId],[ApplicationSystemId], [CreationTime], [ExtraProperties], [ConcurrencyStamp]) VALUES (N'Thiqah Apigee COO Yakeen App Keys',N'bCycKyfwwHqoM2omUS01vCVcLuFEaHK4',N'22maOLPpwocjL119',(select top 1 id from [IntegrationServices] where SystemMappingKey=N'P-ELM_yakeen'),(select top 1 id from [ApplicationSystems] where [Name]=N'COO'), GETDATE(), N'', NEWID());



INSERT INTO dbo.Resources ([Name],NationalId ,[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES(N'mohammad',N'237778966',GETDATE(),0, N'', NEWID())
INSERT INTO dbo.Resources ([Name],NationalId ,[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES(N'mahdi',N'237778966',GETDATE(),0, N'', NEWID())


INSERT INTO dbo.Resources ([Name],NationalId ,[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp],[YearlyCost]) VALUES(N'mohammad',N'237778966',GETDATE(),0, N'', NEWID(),50000)
INSERT INTO dbo.Resources ([Name],NationalId ,[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp],[YearlyCost]) VALUES(N'mahdi',N'237778966',GETDATE(),0, N'', NEWID(),70000)

INSERT INTO dbo.ProviderSubscriptions ([Description],[URL] ,ProviderId,[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp],[YearlyCost]) VALUES(N'Apigee license',N'google.com',2,GETDATE(),1, N'', NEWID(),10000)


INSERT INTO dbo.[Servers] ([Specification],[ProviderId], [Shared], [CreationTime],[ExtraProperties], [ConcurrencyStamp],[YearlyCost]) VALUES(N'ruh02-tbs-app-k8s-master-03 8GB Ram 8 CPUs Windows',3,0,GETDATE(), N'', NEWID(),50000)
INSERT INTO dbo.[Servers] ([Specification],[ProviderId], [Shared], [CreationTime],[ExtraProperties], [ConcurrencyStamp],[YearlyCost]) VALUES(N'240-SOCPA-APP01 8GB Ram 8 CPUs Windows',3,0,GETDATE(), N'', NEWID(),50000)
INSERT INTO dbo.[Servers] ([Specification],[ProviderId], [Shared], [CreationTime],[ExtraProperties], [ConcurrencyStamp],[YearlyCost]) VALUES(N'tbs17-prolapi2.thiqah.sa 8GB Ram 8 CPUs Windows',3,0,GETDATE(), N'', NEWID(),50000)
INSERT INTO dbo.SharedServices ([Description],[Name] ,ProviderId,[Year],[Month],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp],[YearlyCost]) VALUES(N'F5',N'F5',3,2025,10,GETDATE(),1, N'', NEWID(),90000)


INSERT INTO dbo.SystemDependencies ([ApplicationSystemId],[DependencyType],[ServerId],[Year],[Month],[SharePercentage],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES(1,1,1,2025,10,100,GETDATE(),0, N'', NEWID())
INSERT INTO dbo.SystemDependencies ([ApplicationSystemId],[DependencyType],[ProviderSubscriptionId],[Year],[Month],[SharePercentage],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES(1,2,1,2025,10,100,GETDATE(),0, N'', NEWID())

INSERT INTO dbo.SystemDependencies ([ApplicationSystemId],[DependencyType],[IntegrationServiceId],[Year],[Month],[SharePercentage],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES(1,3,5,2025,10,100,GETDATE(),0, N'', NEWID())


INSERT INTO dbo.SystemDependencies ([ApplicationSystemId],[DependencyType],[ResourceId],[Year],[Month],[SharePercentage],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES(1,4,5,2025,10,100,GETDATE(),0, N'', NEWID())
INSERT INTO dbo.SystemDependencies ([ApplicationSystemId],[DependencyType],[ResourceId],[Year],[Month],[SharePercentage],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES(1,4,6,2025,10,100,GETDATE(),0, N'', NEWID())
INSERT INTO dbo.SystemDependencies ([ApplicationSystemId],[DependencyType],[SharedServiceId],[Year],[Month],[SharePercentage],[CreationTime], [Shared], [ExtraProperties], [ConcurrencyStamp]) VALUES(1,5,2,2025,10,100,GETDATE(),0, N'', NEWID())





--SELECT 'select * from '+TABLE_SCHEMA+'.' +TABLE_NAME+' order by 1 desc'
--FROM INFORMATION_SCHEMA.TABLES
--WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = 'Finolyzerdb';
select * from dbo.Portfolios order by 1 desc
select * from dbo.ApplicationSystems order by 1 desc
select * from dbo.IntegrationServices order by 1 desc
select * from dbo.Resources order by 1 desc
select * from dbo.Providers order by 1 desc
select * from dbo.ProviderSubscriptions order by 1 desc
delete  from dbo.Resources
select * from dbo.[Servers] order by 1 desc
select * from dbo.SystemIntegrationTransactions order by 1 desc
select * from dbo.ApplicationIntegrationKeys order by 1 desc
select * from dbo.SharedServices order by 1 desc
select * from dbo.SystemDependencies order by 1 desc



select * from dbo.CostSummaryRequests order by 1 desc
