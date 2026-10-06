-- Run in: SQL database > Query editor (preview)
-- Make sure you are connected to the SAME database that is in your connection string (Initial Catalog=...)
CREATE TABLE dbo.Students (
    Id   INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL
);

INSERT INTO dbo.Students (Name) VALUES ('Asha'), ('Rahul'), ('Priya'), ('Kiran');

SELECT * FROM dbo.Students;
