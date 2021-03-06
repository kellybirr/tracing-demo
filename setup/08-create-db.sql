CREATE DATABASE GreetingDB;
GO
USE GreetingDB;
GO
CREATE TABLE dbo.Greetings (
	[Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY CLUSTERED,
	[Name] NVARCHAR(50) NOT NULL,
	[Utc] [DATETIME] NOT NULL DEFAULT(GETUTCDATE())
);
GO
INSERT INTO dbo.Greetings (Name) VALUES ('My Greeting DB')
SELECT * FROM dbo.Greetings WHERE Id = SCOPE_IDENTITY()
GO