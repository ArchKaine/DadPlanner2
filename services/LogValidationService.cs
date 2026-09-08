using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;

namespace DadPlanner2.Services;

public sealed class LogValidationService
{
    public string? Validate(
        IEnumerable<LogRecord> existingLogs,
        string mode,
        double? clinicalVol,
        int? concentration,
        int? motility,
        int? progMotility,
        int? morphology,
        double? phLevel,
        long timestamp,
        long? existingId = null)
    {
        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return "A log cannot be recorded in the future.";

        if (existingLogs.Any(log => log.Id != existingId && log.Timestamp == timestamp))
            return "A log already exists at this exact date and time.";

        if (mode != "Clinical-Lab")
            return null;

        if (!clinicalVol.HasValue || clinicalVol.Value <= 0)
            return "Clinical volume must be greater than zero for a lab record.";
        if (!concentration.HasValue || concentration.Value < 0)
            return "Concentration is required and cannot be negative.";
        if (!motility.HasValue || motility.Value is < 0 or > 100)
            return "Total motility is required and must be between 0 and 100%.";
        if (!progMotility.HasValue || progMotility.Value is < 0 or > 100 || progMotility.Value > motility.Value)
            return "Progressive motility must be between 0 and total motility.";
        if (!morphology.HasValue || morphology.Value is < 0 or > 100)
            return "Morphology is required and must be between 0 and 100%.";
        if (!phLevel.HasValue || phLevel.Value is < 0 or > 14)
            return "pH is required and must be between 0 and 14.";

        return null;
    }
}
