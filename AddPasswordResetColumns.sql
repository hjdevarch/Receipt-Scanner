-- SQL script to manually add password reset columns to AspNetUsers table
BEGIN TRANSACTION;

-- Add PasswordResetToken column if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'PasswordResetToken')
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PasswordResetToken] nvarchar(max) NULL;
    PRINT 'Added PasswordResetToken column';
END
ELSE
BEGIN
    PRINT 'PasswordResetToken column already exists';
END

-- Add PasswordResetTokenExpiryTime column if it doesn't exist  
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'PasswordResetTokenExpiryTime')
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PasswordResetTokenExpiryTime] datetime2 NULL;
    PRINT 'Added PasswordResetTokenExpiryTime column';
END
ELSE
BEGIN
    PRINT 'PasswordResetTokenExpiryTime column already exists';
END

-- Update migration history if not already recorded
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20251112142357_AddPasswordResetTokenFields')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251112142357_AddPasswordResetTokenFields', N'9.0.10');
    PRINT 'Added migration history record';
END
ELSE
BEGIN
    PRINT 'Migration history already recorded';
END

COMMIT TRANSACTION;

PRINT 'Password reset columns setup completed successfully!';