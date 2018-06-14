CREATE TABLE [dbo].[Table] (
    [Name]         VARCHAR (50) NOT NULL,
    [PasswordHash] BINARY (16)  NOT NULL,
    PRIMARY KEY CLUSTERED ([Name] ASC)
);

