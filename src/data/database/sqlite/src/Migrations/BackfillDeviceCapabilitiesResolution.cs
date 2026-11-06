namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <summary>
    /// SQL that fills DeviceCapabilities.Resolution from each device's own video (mp4) media. The largest frame is picked
    /// by pixel count: as text, "640x360" sorts after "1920x1080". Snapshots (jpg) are ignored, and a device that already
    /// has a resolution is left unchanged.
    /// </summary>
    public static class BackfillDeviceCapabilitiesResolution
    {
        public const string Sql = """
            UPDATE DeviceCapabilities
            SET Resolution = (
                SELECT m.Resolution
                FROM MediaItems m
                WHERE m.DeviceId = DeviceCapabilities.DeviceId
                  AND m.MediaFormat = 'mp4'
                  AND m.Resolution IS NOT NULL
                ORDER BY CAST(substr(m.Resolution, 1, instr(m.Resolution, 'x') - 1) AS INTEGER)
                       * CAST(substr(m.Resolution, instr(m.Resolution, 'x') + 1) AS INTEGER) DESC
                LIMIT 1)
            WHERE Resolution IS NULL
              AND EXISTS (
                  SELECT 1 FROM MediaItems m
                  WHERE m.DeviceId = DeviceCapabilities.DeviceId
                    AND m.MediaFormat = 'mp4'
                    AND m.Resolution IS NOT NULL);
            """;
    }
}
