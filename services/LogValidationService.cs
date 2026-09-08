using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;

namespace DadPlanner2.Services;

public sealed class LogValidationService
{
    public string? ValidateThresholds(double minHours, double maxHours)
    {
        if (double.IsNaN(minHours) || double.IsInfinity(minHours) || minHours < 0)
            return "Minimum recovery time must be a finite, non-negative number.";

        if (double.IsNaN(maxHours) || double.IsInfinity(maxHours) || maxHours <= 0)
            return "Maximum recovery time must be a finite number greater than zero.";

        if (maxHours <= minHours)
            return "Maximum recovery time must be greater than the minimum recovery time.";

        return null;
    }

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
