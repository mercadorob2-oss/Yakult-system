ALTER ROLE [db_owner] ADD MEMBER [IIS APPPOOL\YakultInventoryApi];


GO
ALTER ROLE [db_owner] ADD MEMBER [yakult_user];


GO
ALTER ROLE [db_owner] ADD MEMBER [yakult_partner];


GO
ALTER ROLE [db_owner] ADD MEMBER [yakult_worker];


GO
ALTER ROLE [db_owner] ADD MEMBER [IIS APPPOOL\YIMS SERVER];


GO
ALTER ROLE [db_owner] ADD MEMBER [yims_dev];


GO
ALTER ROLE [db_datareader] ADD MEMBER [yakult_partner];


GO
ALTER ROLE [db_datareader] ADD MEMBER [yakult_it_encoder];


GO
ALTER ROLE [db_datareader] ADD MEMBER [yims_user];


GO
ALTER ROLE [db_datareader] ADD MEMBER [IIS APPPOOL\yakult_api_dev];


GO
ALTER ROLE [db_datawriter] ADD MEMBER [yakult_it_encoder];


GO
ALTER ROLE [db_datawriter] ADD MEMBER [yims_user];


GO
ALTER ROLE [db_datawriter] ADD MEMBER [IIS APPPOOL\yakult_api_dev];


GO
ALTER ROLE [db_datawriter] ADD MEMBER [dev_admin2];


GO
ALTER ROLE [db_datareader] ADD MEMBER [dev_admin2];


GO
ALTER ROLE [db_owner] ADD MEMBER [remote_user];

