# Task Manager SQL Managed Instances

## Set up guide

*Ensure db connection strings are updated in Azure Key Vault or Azure App Configuration where necessary*

### WorkflowDatabase

1). SQL DBA to add a SQL login to the server that has DBCreator access. This account will be used by the Azure DevOps release pipeline to create the Workflowdatabase.
2). SQL DBA to add OSET ADUsers group to the server.
3). Grant OSET ADUsers db_owner permission against the WorkflowDatabase so Tamatoa devs can access:

`USE [WorkflowDatabase]
GO
CREATE USER [OSET ADUsers] FOR LOGIN [OSET ADUsers]
GO
ALTER ROLE [db_owner] ADD MEMBER [OSET ADUsers]
GO`

4). Whilst logged in with a UKHO A account, grant the Portal app service access to the database so the Portal can access it:

`CREATE USER [portal-appservice-name] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [portal-appservice-name];
ALTER ROLE db_datawriter ADD MEMBER [portal-appservice-name];
ALTER ROLE db_ddladmin ADD MEMBER [portal-appservice-name];
GO`

4). Create the PortalUITestDev account for use in automated UI testing (the password will be the workflow db pwd as found in Key Vault):

`CREATE LOGIN [PortalUITestDev] WITH
PASSWORD=N'****
GO`

5). Add that user to the WorkflowDatabase:

`USE [WorkflowDatabase]
GO
CREATE USER [PortalUITestDev] FOR LOGIN [PortalUITestDev]
go
ALTER ROLE [db_owner] ADD MEMBER [PortalUITestDev]
GO`

6). Create a database role that will control access to the HpdUser table:

`USE [WorkflowDatabase]
GO
CREATE ROLE [HPDUser_Role]
GO
GRANT DELETE ON [dbo].[HpdUser] TO [HPDUser_Role]
GO
GRANT INSERT ON [dbo].[HpdUser] TO [HPDUser_Role]
go
GRANT SELECT ON [dbo].[HpdUser] TO [HPDUser_Role]
go
GRANT UPDATE ON [dbo].[HpdUser] TO [HPDUser_Role]
GO`

7). DBA to add DL group in AD that will then be added to this role. The PSCT AD group will then be added to this DL.

### SQLDatabase (used by NSB)

*Based on the above steps for the WorkflowDatabase being done*

1). Log in as the DBCreator SQL account and create the NSB database.

2). Whilst logged in with a UKHO A account, grant the NSB app services, and the Event Service app service, access to the database:

`CREATE USER [nsb-app-service-name] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [nsb-app-service-name];
ALTER ROLE db_datawriter ADD MEMBER [nsb-app-service-name];
ALTER ROLE db_ddladmin ADD MEMBER [nsb-app-service-name];
GO`

3). Deploy the NSB endpoints and ensure the relevant tables are created in the SQL database.