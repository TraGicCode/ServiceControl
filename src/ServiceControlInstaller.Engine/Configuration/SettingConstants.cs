﻿namespace ServiceControlInstaller.Engine.Configuration
{
    public class SettingConstants
    {
        public const int ErrorRetentionPeriodMaxInDays = 45;
        public const int ErrorRetentionPeriodMinInDays = 10;
        public const int ErrorRetentionPeriodDefaultInDaysForUI = 15;

        public const int AuditRetentionPeriodMaxInHours = 8760;
        public const int AuditRetentionPeriodMinInHours = 1;
        public const int AuditRetentionPeriodDefaultInHoursForUI = 720;
    }
}